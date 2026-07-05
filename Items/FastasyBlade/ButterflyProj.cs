using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;

namespace TheSanity.Projectiles
{
    public class ButterflyProj : ModProjectile
    {
        // Sesuaikan path ini dengan lokasi file ButterflyProj.png kamu
        public override string Texture => "TheSanity/Projectiles/SulphurButterflyProj";

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 24; // Total frame di spritesheet
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 300; // Hidup selama 5 detik
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            // 1. Animasi Frame
            if (++Projectile.frameCounter >= 5)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= 3) Projectile.frame = 0;
            }

            // 2. Rotasi agar mengikuti arah terbang
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // 3. Efek Visual: Partikel (Dust Trail)
            if (Main.rand.NextBool(3)) // Muncul setiap 3 tick agar tidak terlalu ramai
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy, 0f, 0f, 100, default, 1.2f);
            }

            // 4. Efek Visual: Cahaya
            Lighting.AddLight(Projectile.Center, 0.8f, 0.2f, 0.8f); // Cahaya ungu muda

            // 5. Logika Homing
            NPC target = FindTarget(400f);
            if (target != null)
            {
                Vector2 move = target.Center - Projectile.Center;
                move.Normalize();
                Projectile.velocity = (Projectile.velocity * 20f + move * 8f) / 21f;
            }

            
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Menambahkan debuff ke musuh
            target.AddBuff(BuffID.Poisoned, 300); // 5 detik
            target.AddBuff(BuffID.Confused, 300);
            target.AddBuff(BuffID.Slow, 300);
        }

        private NPC FindTarget(float range)
        {
            NPC closest = null;
            float distance = range;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && npc.CanBeChasedBy())
                {
                    float d = Vector2.Distance(Projectile.Center, npc.Center);
                    if (d < distance)
                    {
                        distance = d;
                        closest = npc;
                    }
                }
            }
            return closest;
        }
    }
}