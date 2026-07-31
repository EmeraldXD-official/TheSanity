using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Cultist.Projectiles
{
    /// <summary>
    /// A short-lived expanding hitbox representing one clone's detonation during
    /// "Celestial Rebirth & Supernova". ai[0] selects the visual/dust flavor
    /// (0 Solar/fire, 1 Vortex/cyan, 2 Nebula/pink, 3 Stardust/purple) so the
    /// four detonations read as distinct even though they share logic.
    /// </summary>
    public class SupernovaBurst : ModProjectile
    {
        private int Flavor => (int)Projectile.ai[0];

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 20;
            Projectile.penetrate = -1;
            Projectile.alpha = 0;
            Projectile.DamageType = DamageClass.Magic;
        }

        public override void AI()
        {
            // expand the hitbox over its short lifetime, growing to ~160px wide
            float progress = 1f - (Projectile.timeLeft / 20f);
            int size = (int)MathHelper_Lerp(20, 160, progress);
            Projectile.width = size;
            Projectile.height = size;
            Projectile.Center = Projectile.Center; // keep centered as it grows

            int dustType = Flavor switch
            {
                0 => DustID.Torch,
                1 => DustID.PortalBoltTrail,
                2 => DustID.PinkTorch,
                3 => DustID.PurpleTorch,
                _ => DustID.Torch
            };

            for (int i = 0; i < 3; i++)
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, dustType, 0f, 0f, 0, default, 2f);
        }

        private static float MathHelper_Lerp(float a, float b, float t) => a + (b - a) * MathHelper_Clamp(t, 0f, 1f);
        private static float MathHelper_Clamp(float v, float min, float max) => v < min ? min : (v > max ? max : v);
    }
}
