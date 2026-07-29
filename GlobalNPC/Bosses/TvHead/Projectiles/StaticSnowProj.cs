using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.TvHead.Projectiles
{
    public class StaticSnowProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.IceSpike;

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Projectile.rotation += 0.2f;

            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Electric, 0, 0, 150, Color.White, 0.9f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            // Ambil tekstur secara aman langsung dari index ID
            Texture2D tex = TextureAssets.Projectile[ProjectileID.IceSpike].Value;
            if (tex == null) return false;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = tex.Size() / 2f;

            Color staticColor = Main.rand.NextBool() ? Color.White : Color.Gray;

            Main.EntitySpriteDraw(tex, drawPos, null, staticColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}