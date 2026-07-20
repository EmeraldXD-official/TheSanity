using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.EmpressRework
{
    public class ChromaticBurstEffect : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/EmpressRework/ChromaticBurst";

        private const int Lifetime = 18;        
        private const float MaxOpacity = 0.12f; // Diperhalus dari 0.2f
        private const float StartScale = 0.9f;  // Diperkecil dari 1.6f
        private const float EndScale = 0.1f;

        public override void SetDefaults()
        {
            Projectile.width = 2;
            Projectile.height = 2;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Lifetime;
            Projectile.alpha = 0;
        }

        public override void AI()
        {
            Projectile.velocity = Vector2.Zero;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;

            float progress = 1f - (Projectile.timeLeft / (float)Lifetime);
            float scale = MathHelper.Lerp(StartScale, EndScale, progress);
            float opacity = MathHelper.Lerp(MaxOpacity, 0f, progress);

            float hue = Main.GlobalTimeWrappedHourly * 0.25f % 1f;
            Color burstColor = Main.hslToRgb(hue, 0.55f, 0.8f) * opacity;
            burstColor.A = 0; 

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred, 
                BlendState.Additive, 
                Main.DefaultSamplerState, 
                DepthStencilState.None, 
                RasterizerState.CullCounterClockwise, 
                null, 
                Main.GameViewMatrix.TransformationMatrix
            );

            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, burstColor, 0f, tex.Size() / 2f, scale, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred, 
                BlendState.AlphaBlend, 
                Main.DefaultSamplerState, 
                DepthStencilState.None, 
                RasterizerState.CullCounterClockwise, 
                null, 
                Main.GameViewMatrix.TransformationMatrix
            );

            return false;
        }
    }
}