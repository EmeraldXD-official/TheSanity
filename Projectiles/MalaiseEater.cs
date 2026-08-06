using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class MalaiseEater : ModProjectile
    {
        // Menggunakan tekstur internal TinyEater (ID: 307)
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.TinyEater;

        public override void SetStaticDefaults()
        {
            // Mengambil jumlah frame animasi langsung dari TinyEater vanilla
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.TinyEater];
        }

        public override void SetDefaults()
        {
            // Meng-copy seluruh sifat dasar (AI, hitboxes, tile collision) dari TinyEater
            Projectile.CloneDefaults(ProjectileID.TinyEater);
            
            // Menghubungkan AI agar berjalan persis seperti TinyEater
            AIType = ProjectileID.TinyEater;

            // Mengatur tipe damage ke Melee
            Projectile.DamageType = DamageClass.Melee;
            Projectile.friendly = true;
            Projectile.hostile = false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Chance 5% memberikan debuff Cursed Inferno selama 3 detik (180 ticks)
            if (Main.rand.NextFloat() < 0.05f)
            {
                target.AddBuff(BuffID.CursedInferno, 180);
            }
        }
    }
}