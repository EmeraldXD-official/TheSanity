using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.GameContent;
using System;
using System.Reflection;

namespace TheSanity.Mecanic
{
    public class SanityUI : UIState
    {
        private Rectangle barHitbox;
        private FieldInfo counterField = typeof(SanityPlayer).GetField("internalCounter", BindingFlags.NonPublic | BindingFlags.Instance);

        protected override void DrawSelf(SpriteBatch spriteBatch) {
            base.DrawSelf(spriteBatch);
            var modPlayer = Main.LocalPlayer.GetModPlayer<SanityPlayer>();

            // --- EFEK LAYAR HITAM ---
            if (modPlayer.SanityCurrent >= 70) {
                float intensity = (modPlayer.SanityCurrent - 70f) / 30f * 0.90f;
                spriteBatch.Draw(
                    TextureAssets.MagicPixel.Value, 
                    new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), 
                    Color.Black * intensity
                );
            }

            // --- RENDER BAR ---
            if (!ModContent.HasAsset("TheSanity/Mecanic/SanityBar")) return;
            Texture2D texture = ModContent.Request<Texture2D>("TheSanity/Mecanic/SanityBar").Value;

            int totalFrames = 47;
            int frameHeight = 20; 
            float quotient = MathHelper.Clamp(modPlayer.SanityCurrent / modPlayer.SanityMax, 0f, 1f);
            int frameToDisplay = (int)(quotient * (totalFrames - 1));

            Rectangle sourceRect = new Rectangle(0, frameToDisplay * frameHeight, 100, frameHeight);
            Vector2 position = new Vector2(Main.screenWidth - 300f - 265f, 28f);
            float scale = 0.85f;

            barHitbox = new Rectangle((int)position.X, (int)position.Y, (int)(100 * scale), (int)(frameHeight * scale));

            spriteBatch.Draw(texture, position, sourceRect, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // --- HOVER TEKS FIX INDIKATOR DESIMAL ---
            if (barHitbox.Contains(Main.mouseX, Main.mouseY)) {
                int mainSanity = (int)modPlayer.SanityCurrent;
                int rawCounter = 0;

                if (counterField != null) {
                    rawCounter = (int)counterField.GetValue(modPlayer);
                }

                int subValue = 0;

                if (rawCounter >= 0) {
                    // Jika bertambah (positif): Angka desimal merangkak naik dari .00 ke .99
                    float counterPercentage = (rawCounter / 30000f) * 100f;
                    subValue = (int)MathHelper.Clamp(counterPercentage, 0, 99);
                } 
                else {
                    // Jika berkurang (negatif): Angka desimal kita balik supaya meluncur turun dari .99 ke .00
                    float counterPercentage = (Math.Abs(rawCounter) / 30000f) * 100f;
                    subValue = 99 - (int)MathHelper.Clamp(counterPercentage, 0, 99);
                }

                // Tampilan teks final
                string text = $"Sanity: {mainSanity} / 100 ({mainSanity}.{subValue:D2}%)";
                Main.instance.MouseText(text);
            }
        }
    }
}