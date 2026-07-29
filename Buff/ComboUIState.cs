using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.UI;

namespace TheSanity.Buff
{
    public class ComboUIState : UIState
    {
        public override void Draw(SpriteBatch spriteBatch)
        {
            base.Draw(spriteBatch);

            Player player = Main.LocalPlayer;
            if (player?.active != true)
            {
                return;
            }

            ComboPlayer modPlayer = player.GetModPlayer<ComboPlayer>();
            if (!modPlayer.comboActive)
            {
                return;
            }

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            // Overlay gelap full screen, makin gelap tiap kali miss
            if (modPlayer.darkAlphaCurrent > 0.001f)
            {
                spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black * modPlayer.darkAlphaCurrent);
            }

            if (modPlayer.darkPhaseActive && modPlayer.darkAlphaCurrent > 0.9f)
            {
                DrawDarkDialogue(spriteBatch, pixel, modPlayer);
            }
            else if (!modPlayer.darkPhaseActive)
            {
                DrawComboBubble(spriteBatch, pixel, player, modPlayer);
            }
        }

        // ===================== BUBBLE COMBO (di atas kepala player) =====================

        private void DrawComboBubble(SpriteBatch sb, Texture2D pixel, Player player, ComboPlayer modPlayer)
        {
            Vector2 headScreenPos = player.Top - Main.screenPosition;

            if (modPlayer.shakeTimer > 0)
            {
                headScreenPos += new Vector2(Main.rand.Next(-4, 5), Main.rand.Next(-4, 5));
            }

            float animT = MathHelper.Clamp(modPlayer.bubbleAnimTimer / (float)ComboPlayer.BubbleAnimDuration, 0f, 1f);
            float scale = MathHelper.Lerp(0.4f, 1f, EaseOutCubic(animT));

            Rectangle baseRect = ComboUILayout.GetBubbleRect(headScreenPos);
            Rectangle bubble = ScaleRect(baseRect, scale);

            // Kotak gaya komik: fill putih + outline hitam tebal + "ekor" mengarah ke player
            sb.Draw(pixel, bubble, Color.White * 0.94f);
            DrawBorder(sb, pixel, bubble, Color.Black, 3);
            DrawComicTail(sb, pixel, bubble, Color.White, Color.Black);

            DynamicSpriteFont smallFont = FontAssets.MouseText.Value;

            // Progress combo (mis. "3 / 7") di atas kotak
            string comboText = $"{modPlayer.currentCombo} / {modPlayer.requiredCombo}";
            Vector2 comboSize = smallFont.MeasureString(comboText);
            Utils.DrawBorderString(sb, comboText, new Vector2(bubble.Center.X - comboSize.X / 2f, bubble.Y - comboSize.Y - 8f), Color.White, 0.95f);

            // Huruf besar di tengah, dengan efek "pulse" tiap kali huruf baru muncul
            float pulse = modPlayer.letterAnimTimer > 0 ? 1f + 0.35f * (modPlayer.letterAnimTimer / 15f) : 1f;
            float letterScale = 1.8f * pulse * scale;
            DynamicSpriteFont bigFont = FontAssets.DeathText.Value;
            string letterText = modPlayer.targetLetter.ToString();
            Vector2 letterSize = bigFont.MeasureString(letterText) * letterScale;
            Vector2 letterPos = new Vector2(bubble.Center.X - letterSize.X / 2f, bubble.Center.Y - letterSize.Y / 2f - 4f);
            Utils.DrawBorderString(sb, letterText, letterPos, Color.Black, letterScale);

            // Border timer: mulai kosong, makin lama makin "menutup" sampai nyambung = waktu habis
            float elapsedFraction = 1f - modPlayer.timer / (float)modPlayer.maxTimer;
            Color timerColor = Color.Lerp(new Color(60, 200, 90), new Color(220, 40, 40), elapsedFraction);
            DrawBorderProgress(sb, pixel, Inflate(bubble, 7), 4, elapsedFraction, timerColor);

            // Hint kecil di bawah bubble
            string hint = "Tekan huruf di keyboard!";
            Vector2 hintSize = smallFont.MeasureString(hint) * 0.8f;
            Utils.DrawBorderString(sb, hint, new Vector2(bubble.Center.X - hintSize.X / 2f, bubble.Bottom + 22f), Color.Yellow, 0.8f);
        }

        private void DrawComicTail(SpriteBatch sb, Texture2D pixel, Rectangle bubble, Color fill, Color outline)
        {
            int tailHeight = 18;
            int baseWidthOutline = 24;
            int baseWidthFill = 16;
            int startX = bubble.Center.X;
            int startY = bubble.Bottom - 2;

            for (int row = 0; row < tailHeight; row++)
            {
                float t = row / (float)tailHeight;
                int rowWidth = (int)MathHelper.Lerp(baseWidthOutline, 2, t);
                sb.Draw(pixel, new Rectangle(startX - rowWidth / 2, startY + row, rowWidth, 3), outline);
            }

            for (int row = 0; row < tailHeight - 4; row++)
            {
                float t = row / (float)(tailHeight - 4);
                int rowWidth = (int)MathHelper.Lerp(baseWidthFill, 2, t);
                sb.Draw(pixel, new Rectangle(startX - rowWidth / 2, startY + row, rowWidth, 3), fill);
            }
        }

        // ===================== DIALOGE HITAM + TOMBOL GIVE UP =====================

        private void DrawDarkDialogue(SpriteBatch sb, Texture2D pixel, ComboPlayer modPlayer)
        {
            float animT = MathHelper.Clamp(modPlayer.dialogueAnimTimer / (float)ComboPlayer.DialogueAnimDuration, 0f, 1f);
            float scale = MathHelper.Lerp(0.7f, 1f, EaseOutCubic(animT));
            float alpha = animT;

            Rectangle baseRect = ComboUILayout.GetDialogueRect();
            Rectangle rect = ScaleRect(baseRect, scale);

            sb.Draw(pixel, rect, Color.Black * 0.92f * alpha);
            DrawBorder(sb, pixel, rect, Color.White * alpha, 3);
            DrawBorder(sb, pixel, Inflate(rect, -6), new Color(150, 20, 20) * alpha, 2);

            DynamicSpriteFont font = FontAssets.MouseText.Value;
            float textScale = 1.05f;
            List<string> lines = WrapText(font, modPlayer.tauntText, rect.Width - 60, textScale);

            float lineHeight = font.MeasureString("A").Y * textScale * 1.2f;
            float textY = rect.Y + 36f;

            foreach (string line in lines)
            {
                Vector2 lineSize = font.MeasureString(line) * textScale;
                Vector2 pos = new Vector2(rect.Center.X - lineSize.X / 2f, textY);
                Utils.DrawBorderString(sb, line, pos, Color.White * alpha, textScale);
                textY += lineHeight;
            }

            if (modPlayer.giveUpButtonRevealTimer >= ComboPlayer.GiveUpRevealDelay)
            {
                DrawGiveUpButton(sb, pixel, font);
            }
        }

        private void DrawGiveUpButton(SpriteBatch sb, Texture2D pixel, DynamicSpriteFont font)
        {
            Rectangle btnRect = ComboUILayout.GetGiveUpButtonRect();
            bool hovering = btnRect.Contains(Main.MouseScreen.ToPoint());

            Color fill = hovering ? new Color(215, 45, 45) : new Color(150, 20, 20);
            sb.Draw(pixel, btnRect, fill);
            DrawBorder(sb, pixel, btnRect, Color.White, 2);

            string text = "Give Up";
            Vector2 size = font.MeasureString(text);
            Vector2 pos = new Vector2(btnRect.Center.X - size.X / 2f, btnRect.Center.Y - size.Y / 2f);
            Utils.DrawBorderString(sb, text, pos, Color.White, 1f);
        }

        // ===================== HELPER UMUM =====================

        private List<string> WrapText(DynamicSpriteFont font, string text, float maxWidth, float scale)
        {
            List<string> lines = new List<string>();
            string[] words = text.Split(' ');
            string currentLine = "";

            foreach (string word in words)
            {
                string testLine = string.IsNullOrEmpty(currentLine) ? word : currentLine + " " + word;
                if (font.MeasureString(testLine).X * scale > maxWidth && !string.IsNullOrEmpty(currentLine))
                {
                    lines.Add(currentLine);
                    currentLine = word;
                }
                else
                {
                    currentLine = testLine;
                }
            }

            if (!string.IsNullOrEmpty(currentLine))
            {
                lines.Add(currentLine);
            }

            return lines;
        }

        private void DrawBorder(SpriteBatch sb, Texture2D pixel, Rectangle rect, Color color, int thickness)
        {
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            sb.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        // Border yang keisi progresif (clockwise dari pojok kiri-atas) sesuai elapsedFraction.
        // Saat elapsedFraction = 1, border sudah tersambung penuh keliling kotak = waktu habis.
        private void DrawBorderProgress(SpriteBatch sb, Texture2D pixel, Rectangle rect, int thickness, float elapsedFraction, Color color)
        {
            float perimeter = 2 * (rect.Width + rect.Height);
            float target = perimeter * MathHelper.Clamp(elapsedFraction, 0f, 1f);
            float remaining = target;

            // Sisi atas (kiri -> kanan)
            float topLen = Math.Min(remaining, rect.Width);
            if (topLen > 0)
            {
                sb.Draw(pixel, new Rectangle(rect.X, rect.Y, (int)topLen, thickness), color);
            }
            remaining -= topLen;

            // Sisi kanan (atas -> bawah)
            if (remaining > 0)
            {
                float rightLen = Math.Min(remaining, rect.Height);
                sb.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, (int)rightLen), color);
                remaining -= rightLen;
            }

            // Sisi bawah (kanan -> kiri)
            if (remaining > 0)
            {
                float bottomLen = Math.Min(remaining, rect.Width);
                sb.Draw(pixel, new Rectangle(rect.Right - (int)bottomLen, rect.Bottom - thickness, (int)bottomLen, thickness), color);
                remaining -= bottomLen;
            }

            // Sisi kiri (bawah -> atas)
            if (remaining > 0)
            {
                float leftLen = Math.Min(remaining, rect.Height);
                sb.Draw(pixel, new Rectangle(rect.X, rect.Bottom - (int)leftLen, thickness, (int)leftLen), color);
            }
        }

        private Rectangle ScaleRect(Rectangle rect, float scale)
        {
            int newW = (int)(rect.Width * scale);
            int newH = (int)(rect.Height * scale);
            return new Rectangle(rect.Center.X - newW / 2, rect.Center.Y - newH / 2, newW, newH);
        }

        private Rectangle Inflate(Rectangle rect, int amount)
        {
            return new Rectangle(rect.X - amount, rect.Y - amount, rect.Width + amount * 2, rect.Height + amount * 2);
        }

        private float EaseOutCubic(float t)
        {
            float inv = 1f - t;
            return 1f - inv * inv * inv;
        }
    }
}