using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class CascadeBlast : ModProjectile
    {
        // Menggunakan asset/tekstur InfernoFriendlyBlast vanilla
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.InfernoFriendlyBlast;

        public override void SetStaticDefaults()
        {
            // Mengambil jumlah frame animasi dari InfernoFriendlyBlast
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.InfernoFriendlyBlast];
        }

        public override void SetDefaults()
        {
            // Clone AI dan sifat dasar dari InfernoFriendlyBlast
            Projectile.CloneDefaults(ProjectileID.InfernoFriendlyBlast);
            AIType = ProjectileID.InfernoFriendlyBlast;

            // Mengatur tipe damage menjadi Melee
            Projectile.DamageType = DamageClass.Melee;
            Projectile.friendly = true;
            Projectile.hostile = false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Inflict On Fire! selama 3 detik (180 ticks)
            target.AddBuff(BuffID.OnFire, 180);
        }
    }
}