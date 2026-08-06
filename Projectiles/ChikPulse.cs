using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class ChikPulse : ModProjectile
    {
        // Menggunakan sprite/asset CrystalPulse2 vanilla
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalPulse2;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.CrystalPulse2];
        }

        public override void SetDefaults()
        {
            // Clone AI dan sifat dasar dari CrystalPulse2
            Projectile.CloneDefaults(ProjectileID.CrystalPulse2);
            AIType = ProjectileID.CrystalPulse2;

            // Pengaturan Khusus
            Projectile.DamageType = DamageClass.Melee; // Tipe Damage Melee
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.penetrate = -1; // Infinite Piercing (menembus musuh tanpa batas)
        }
    }
}