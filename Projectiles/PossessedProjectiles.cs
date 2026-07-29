using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Enemy
{
    // =========================================================================
    // PROJECTILE 1: PEA SOUP (MUNTAH HIJAU ASAM)
    // =========================================================================
    public class PossessedPeaSoup : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_188";

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;    
            Projectile.friendly = false;
            Projectile.tileCollide = true; 
            Projectile.timeLeft = 240;     
            Projectile.aiStyle = -1;       
        }

        public override void AI()
        {
            float gravityPull = 0.3f;
            float maxFallSpeed = 12f;

            Projectile.velocity.Y += gravityPull;
            if (Projectile.velocity.Y > maxFallSpeed) Projectile.velocity.Y = maxFallSpeed;

            Projectile.rotation += Projectile.velocity.X * 0.05f + 0.02f;

            if (Main.rand.NextBool(2)) 
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.CursedTorch, 0f, 0f, 150, default, 1.2f);
                d.noGravity = true; 
                d.velocity *= 0.3f;
            }
        }

        // =========================================================================
        // [BALANCING LOCATION 1: EFEK DEBUFF POISONED (RACUN)]
        // Di sini kamu bisa mengatur berapa lama Player akan keracunan saat kena muntah.
        // =========================================================================
        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            // 300 frame = 5 Detik Player akan terkena debuff Poisoned
            int poisonDuration = 300; 
            target.AddBuff(BuffID.Poisoned, poisonDuration);
        }

        public override void Kill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.CursedTorch, Projectile.velocity.X * 0.4f, Projectile.velocity.Y * 0.4f);
            }
        }
    }

    // =========================================================================
    // PROJECTILE 2: TELEKINETIC PULL (TARIKAN JIWA PUTIH)
    // =========================================================================
    public class PossessedPullBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_126";

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false; // Menembus dinding agar tarikan gaib bekerja
            Projectile.timeLeft = 180;     
            Projectile.aiStyle = -1;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GemDiamond, 0f, 0f, 100, Color.White, 1.0f);
                d.noGravity = true;
                d.velocity *= 0.1f;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            int casterIndex = (int)Projectile.ai[0];

            if (casterIndex >= 0 && casterIndex < Main.maxNPCs && Main.npc[casterIndex].active)
            {
                NPC possessedNPC = Main.npc[casterIndex];
                Vector2 pullDirection = possessedNPC.Center - target.Center;
                pullDirection.Normalize(); 

                float pullStrength = 14f; // Kecepatan sentakan tarikan
                target.velocity = pullDirection * pullStrength;

                for (int i = 0; i < 10; i++)
                {
                    Dust.NewDust(target.position, target.width, target.height, DustID.GemDiamond, 0f, 0f, 0, Color.White, 1.5f);
                }

                // =========================================================================
                // LOGIKA SINKRONISASI: RESET TIMING ANTRIAN GLOBAL
                // Saat peluru sukses mengenai Player, kita paksa timer antrean di core AI 
                // kembali ke 30 detik (1800 frame) agar Player tidak langsung ditarik beruntun.
                // =========================================================================
                ThePossessedRework.globalPullTimer = 1800;
            }
        }
    }
}