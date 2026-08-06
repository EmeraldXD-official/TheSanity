using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class PlanteraThornBallProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.ThornBall;

        private int StuckNPCIndex
        {
            get => (int)Projectile.ai[0] - 1;
            set => Projectile.ai[0] = value + 1;
        }

        private Vector2 OffsetFromNPC;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.ThornBall];
        }

        public override void SetDefaults()
        {
            // DIBERSIHKAN: Tanpa CloneDefaults agar ai[0] tidak bentrok dengan AI bawaan game
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 600; // Menempel selama 10 Detik
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20; // Hit damage setiap ~0.3 detik
        }

        public override void OnSpawn(IEntitySource source)
        {
            // Damage ThornBall diset 35% dari main weapon
            Projectile.damage = (int)(Projectile.damage * 0.35f);
        }

        public override void AI()
        {
            // Jika sedang menempel di musuh
            if (StuckNPCIndex >= 0 && StuckNPCIndex < Main.maxNPCs)
            {
                NPC target = Main.npc[StuckNPCIndex];

                if (target.active && target.life > 0)
                {
                    // Kunci posisi ThornBall di tubuh musuh
                    Projectile.Center = target.Center + OffsetFromNPC;
                    Projectile.velocity = Vector2.Zero;
                    Projectile.tileCollide = false;
                }
                else
                {
                    // Jika musuh mati/hilang, hancurkan ThornBall
                    Projectile.Kill();
                }
            }
            else
            {
                // Rotasi visual saat terbang / memantul
                Projectile.rotation += 0.2f * Projectile.direction;

                // Gravitasi halus agar ThornBall bisa memantul di tanah sebelum menempel musuh
                Projectile.velocity.Y += 0.2f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Tempelkan ThornBall jika belum menempel di NPC manapun
            if (StuckNPCIndex < 0)
            {
                StuckNPCIndex = target.whoAmI;
                OffsetFromNPC = Projectile.Center - target.Center;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // Memantul jika mengenai permukaan sebelum menempel ke musuh
            if (StuckNPCIndex < 0)
            {
                if (Projectile.velocity.X != oldVelocity.X) Projectile.velocity.X = -oldVelocity.X * 0.75f;
                if (Projectile.velocity.Y != oldVelocity.Y) Projectile.velocity.Y = -oldVelocity.Y * 0.75f;
                return false;
            }
            return true;
        }
    }
}