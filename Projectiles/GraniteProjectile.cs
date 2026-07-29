using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    // --- PROYEKTIL KECIL (ORBIT) ---
    public class GraniteOrbitOrb : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_0";

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.tileCollide = false;
            Projectile.hostile = true;
            Projectile.timeLeft = 600;
            Projectile.alpha = 255;
        }

        public override void AI()
        {
            // Deteksi Owner (Golem atau Flyer)
            NPC owner = Main.npc[(int)Projectile.ai[0]];
            if (!owner.active || (owner.type != NPCID.GraniteGolem && owner.type != NPCID.GraniteFlyer)) { Projectile.Kill(); return; }

            float rotationSpeed = 0.07f;
            float radius = 60f;

            if (Projectile.timeLeft < 10) Projectile.Kill();

            float angle = (Main.GameUpdateCount * rotationSpeed) + (Projectile.ai[1] * (MathHelper.TwoPi / 5f));
            Projectile.Center = owner.Center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * radius;

            Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Granite, 0, 0, 100, default, 1f);
            d.noGravity = true;
            Lighting.AddLight(Projectile.Center, 0.1f, 0.2f, 0.4f);
        }

        public override bool PreDraw(ref Color lightColor) => false;
    }

    // --- PROYEKTIL BESAR (BIG BLAST) ---
    public class GraniteBigBlast : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_0";

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 600; // LOKASI LIFE TIME: 10 detik
            Projectile.alpha = 255;
        }

        public override void AI()
        {
            // Homing Logic
            Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            if (target != null && target.active && !target.dead)
            {
                float speed = 8.5f;
                float inertia = 14f;
                Vector2 desiredVelocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * speed;
                Projectile.velocity = (Projectile.velocity * (inertia - 1f) + desiredVelocity) / inertia;
            }

            for (int i = 0; i < 3; i++)
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Granite, 0, 0, 100, default, 1.8f);
                d.noGravity = true;
                if (Main.rand.NextBool(2))
                {
                    Dust g = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Electric, 0, 0, 100, default, 0.7f);
                    g.noGravity = true;
                }
            }
            Lighting.AddLight(Projectile.Center, 0.4f, 0.6f, 1.2f);
        }

        public override bool PreDraw(ref Color lightColor) => false;

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // MEMBERIKAN EFEK ELECTRIFIED SELAMA 3 DETIK (180 Frames)
            target.AddBuff(BuffID.Electrified, 180);
            Projectile.Kill(); 
        }
    }
}