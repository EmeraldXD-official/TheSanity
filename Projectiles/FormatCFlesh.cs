using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class FormatCFlesh : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.PurificationPowder;

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 60;
            Projectile.alpha = 255;
        }

        public override void AI()
        {
            // PARTIKEL JEJAK DAGING TERBANG MASUK KE MUSUH
            for (int i = 0; i < 2; i++)
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GemRuby, -Projectile.velocity * 0.05f, 100, default, 1.3f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // MUNCRATAN DARAH & DAGING SAAT KENA MUSUH
            SoundEngine.PlaySound(SoundID.NPCHit1, target.Center);

            for (int k = 0; k < 8; k++)
            {
                Vector2 splatterVel = Main.rand.NextVector2Circular(6f, 6f);
                Dust blood = Dust.NewDustPerfect(target.Center, DustID.Blood, splatterVel, 0, default, 1.4f);
                blood.noGravity = false;
            }

            for (int j = 0; j < 2; j++)
            {
                Vector2 fleshVel = Main.rand.NextVector2Circular(4f, 4f);
                Gore.NewGore(Projectile.GetSource_OnHit(target), target.Center, fleshVel, Main.rand.Next(142, 144));
            }
        }
    }
}