using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class ValorMeteor : ModProjectile
    {
        private static readonly int[] MeteorProjectileTypes = new int[]
        {
            ProjectileID.Meteor1,
            ProjectileID.Meteor2,
            ProjectileID.Meteor3
        };

        public override string Texture
        {
            get
            {
                int selectedIndex = (int)(Projectile.ai[0] % 3);
                return "Terraria/Images/Projectile_" + MeteorProjectileTypes[selectedIndex];
            }
        }

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            
            Projectile.penetrate = 1; // Cuma 1 target (langsung hancur pas kena 1 musuh)

            Projectile.tileCollide = false; // Tetap menembus block/ubin
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
        }

        public override void AI()
        {
            // Rotasi mengikuti arah jatuhnya meteor
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // Efek Partikel Api Vanilla
            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 1.5f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.OnFire, 180); // Buff membakar 3 detik
        }
    }
}