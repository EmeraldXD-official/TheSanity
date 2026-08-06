using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class RallyNail : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.NailFriendly;

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee; 
            Projectile.penetrate = 1; 
            Projectile.tileCollide = true; 
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300; 
        }

        public override void AI()
        {
            // Menyesuaikan rotasi mengikuti arah terbang velocity secara presisi
            // (Jika masih agak miring 90 derajat, ganti jadi: Projectile.velocity.ToRotation() + MathHelper.PiOver2;)
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }
    }
}