using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using System;
using Terraria.GameContent;

namespace TheSanity.CostumeRarity
{
    public class UnknownRaritySystem : GlobalItem
    {
        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
        {
            // Hanya untuk rarity UnknownRarity dan baris nama
            if (item.rare != ModContent.RarityType<UnknownRarity>() || line.Name != "ItemName")
                return true;

            SpriteBatch sb = Main.spriteBatch;
            Vector2 basePos = new Vector2(line.X, line.Y);
            float scale = line.BaseScale.X;

            // Hitung area teks (untuk partikel)
            Vector2 textSize = line.Font.MeasureString(line.Text) * scale;
            Rectangle area = new Rectangle(
                (int)basePos.X - 10,
                (int)basePos.Y - 6,
                (int)textSize.X + 20,
                (int)textSize.Y + 12
            );

            float time = (float)Main.timeForVisualEffects * 0.01f;

            // --- Partikel glow ungu (25 titik) ---
            for (int i = 0; i < 25; i++)
            {
                float offsetX = (float)Math.Sin(time + i * 0.7f) * 12f;
                float offsetY = (float)Math.Cos(time * 0.6f + i * 0.8f) * 8f;
                Vector2 pos = new Vector2(
                    area.X + (i * 15f + time * 2.5f) % area.Width + offsetX,
                    area.Y + (i * 10f + time * 1.7f) % area.Height + offsetY
                );

                // Warna ungu ke magenta, transparansi bergerak
                Color color = Color.Lerp(Color.Purple, Color.Magenta, 
                    (float)(Math.Sin(time + i * 0.3f) * 0.5f + 0.5f));
                color.A = (byte)(120 + (byte)(Math.Sin(time * 2f + i * 0.5f) * 60 + 60));

                float size = 3f + (float)Math.Sin(time * 2.5f + i) * 1.5f;

                sb.Draw(
                    Terraria.GameContent.TextureAssets.MagicPixel.Value,
                    pos,
                    new Rectangle(0, 0, 1, 1),
                    color,
                    0f,
                    Vector2.Zero,
                    size,
                    SpriteEffects.None,
                    0f
                );
            }

            // --- Bintik terang (10 titik) ---
            for (int i = 0; i < 10; i++)
            {
                float offsetX = (float)Math.Sin(time * 0.8f + i * 1.2f) * 6f;
                float offsetY = (float)Math.Cos(time * 0.9f + i * 1.1f) * 4f;
                Vector2 pos = new Vector2(
                    area.X + area.Width / 2 + offsetX + (float)Math.Sin(time + i) * 20f,
                    area.Y + area.Height / 2 + offsetY + (float)Math.Cos(time * 1.1f + i) * 10f
                );

                Color color = new Color(180, 50, 255, 180);
                float size = 4f + (float)Math.Sin(time * 3f + i * 2f) * 2f;

                sb.Draw(
                    Terraria.GameContent.TextureAssets.MagicPixel.Value,
                    pos,
                    new Rectangle(0, 0, 1, 1),
                    color,
                    0f,
                    Vector2.Zero,
                    size,
                    SpriteEffects.None,
                    0f
                );
            }

            // Kembalikan true agar teks digambar normal (tanpa modifikasi)
            return true;
        }
    }
}