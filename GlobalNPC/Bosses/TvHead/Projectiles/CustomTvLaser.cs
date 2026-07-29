using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent; // DITAMBAHKAN: Diperlukan untuk TextureAssets
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.TvHead.Projectiles
{
    public class CustomTvLaser : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.EyeLaser;

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 300;
            Projectile.extraUpdates = 1;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, Projectile.velocity * 0.2f, 100, Color.Cyan, 1.2f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;

            Color rainbowColor = Main.hslToRgb((Main.GlobalTimeWrappedHourly * 3f + Projectile.whoAmI * 0.1f) % 1f, 1f, 0.7f);

            Main.EntitySpriteDraw(tex, drawPos, null, rainbowColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}