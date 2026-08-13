using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Luminance.Assets;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia.Effects
{
    public static class VisualUtils
    {
        /// <summary>
        /// Menggambar Shineflare menggunakan MiscTexturesRegistry.Pixel milik Luminance
        /// </summary>
        public static void DrawShineFlare(SpriteBatch spriteBatch, Vector2 position, Color flareColor, float scale = 1f, float rotation = 0f, float intensity = 0.5f) {
            Texture2D pixelTex = MiscTexturesRegistry.Pixel.Value; 
            if (pixelTex == null) return;

            Vector2 origin = pixelTex.Size() * 0.5f;
            Vector2 drawPos = position - Main.screenPosition;

            Color color = flareColor * intensity * 0.45f;
            color.A = 0; 

            // 1. Core Inti (Kotak Pendar Tengah)
            spriteBatch.Draw(pixelTex, drawPos, null, color * 0.8f, 0f, origin, new Vector2(scale * 10f), SpriteEffects.None, 0f);

            // 2. Sinar Horizontal
            Vector2 scaleH = new Vector2(scale * 70f, scale * 2f);
            spriteBatch.Draw(pixelTex, drawPos, null, color, rotation, origin, scaleH, SpriteEffects.None, 0f);

            // 3. Sinar Vertikal
            Vector2 scaleV = new Vector2(scale * 2f, scale * 70f);
            spriteBatch.Draw(pixelTex, drawPos, null, color, rotation, origin, scaleV, SpriteEffects.None, 0f);

            // 4. Sinar Diagonal (Bintang 4 Sudut)
            Vector2 scaleDiag1 = new Vector2(scale * 35f, scale * 1.5f);
            Vector2 scaleDiag2 = new Vector2(scale * 1.5f, scale * 35f);
            spriteBatch.Draw(pixelTex, drawPos, null, color * 0.5f, rotation + MathHelper.PiOver4, origin, scaleDiag1, SpriteEffects.None, 0f);
            spriteBatch.Draw(pixelTex, drawPos, null, color * 0.5f, rotation + MathHelper.PiOver4, origin, scaleDiag2, SpriteEffects.None, 0f);
        }
    }
}