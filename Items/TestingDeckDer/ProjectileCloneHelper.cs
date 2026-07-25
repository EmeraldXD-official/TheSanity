using Terraria;

namespace TheSanity.Items.TestingDeckDer
{
    public static class ProjectileCloneHelper
    {
        // Metode ini disederhanakan agar tidak merusak kompilasi proyek modmu
        public static void SpawnClone(Projectile source, Player caster)
        {
            if (source == null || !source.active) return;
            
            Projectile.NewProjectile(
                caster.GetSource_Misc("TestingDeckClone"),
                Main.MouseWorld,
                source.velocity,
                source.type,
                source.damage,
                source.knockBack,
                caster.whoAmI
            );
        }
    }
}