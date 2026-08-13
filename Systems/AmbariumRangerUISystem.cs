using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.Systems
{
    public class AmbariumRangerUISystem : ModSystem
    {
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            // Menautkan UI tepat di atas layer mouse text agar selalu terlihat jelas
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "TheSanity: Ambarium Ranger Bar",
                    delegate
                    {
                        DrawRangerBar(Main.spriteBatch);
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }

        private void DrawRangerBar(SpriteBatch spriteBatch)
        {
            Player player = Main.LocalPlayer;
            if (player == null || !player.active || player.dead) return;

            // Cek apakah pemain menggunakan 1 Set Ambarium Visor
            var modPlayer = player.GetModPlayer<Items.Armor.Ambarium.AmbariumArmorPlayer>();
            if (modPlayer == null || !modPlayer.rangerSet) return;

            // 📍 POSISI BAR: Kiri Layar di bawah area Buff (X: 20, Y: 260)
            Vector2 position = new Vector2(20, 260);
            int barWidth = 140;
            int barHeight = 18;

            float fillAmount = 0f;
            Color barColor = Color.Purple;
            Color borderColor = new Color(185, 90, 255); // Warna Ungu Ambarium
            string labelText = "";

            // 1. TAHAP BURST MODE ACTIVE (5 Detik)
            if (modPlayer.rangerActiveTimer > 0)
            {
                fillAmount = modPlayer.rangerActiveTimer / 300f; // 300 tick = 5 detik
                barColor = new Color(200, 80, 255); // Ungu Terang Menyala
                float secondsLeft = modPlayer.rangerActiveTimer / 60f;
                labelText = $"BURST: {secondsLeft:F1}s";
            }
            // 2. TAHAP RECHARGE / COOLDOWN (10 Detik)
            else if (modPlayer.rangerRechargeTimer > 0)
            {
                fillAmount = 1f - (modPlayer.rangerRechargeTimer / 600f); // Mengisi perlahan sampai penuh
                barColor = new Color(90, 40, 110); // Ungu Gelap / Cooldown
                float secondsLeft = modPlayer.rangerRechargeTimer / 60f;
                labelText = $"RECHARGE: {secondsLeft:F1}s";
            }
            // 3. TAHAP CHARGING HIT (0 - 8 Hit)
            else
            {
                fillAmount = modPlayer.rangerHits / 8f;
                barColor = new Color(120, 160, 255); // Biru Muda / Ready
                labelText = modPlayer.rangerHits >= 8 ? "READY!" : $"CHARGE: {modPlayer.rangerHits}/8";
            }

            // Tekstur pixel bawaan Terraria untuk background & bar
            Texture2D texture = TextureAssets.MagicPixel.Value;

            // 🖼️ 1. Gambar Background Box (Hitam Transparan)
            Rectangle bgRect = new Rectangle((int)position.X, (int)position.Y, barWidth, barHeight);
            spriteBatch.Draw(texture, bgRect, Color.Black * 0.65f);

            // 🖼️ 2. Gambar Border Bingkai
            DrawBorder(spriteBatch, texture, bgRect, borderColor * 0.8f, 2);

            // 🖼️ 3. Gambar Isi Bar Progress
            int fillWidth = (int)((barWidth - 4) * MathHelper.Clamp(fillAmount, 0f, 1f));
            if (fillWidth > 0)
            {
                Rectangle fillRect = new Rectangle((int)position.X + 2, (int)position.Y + 2, fillWidth, barHeight - 4);
                spriteBatch.Draw(texture, fillRect, barColor);
            }

            // 📝 4. Gambar Teks Indikator di Tengah Bar
            Vector2 textPos = position + new Vector2(barWidth / 2f, barHeight / 2f);
           Utils.DrawBorderString(spriteBatch, labelText, textPos, Color.White, 0.75f, 0.5f, 0.5f);
        }

        private void DrawBorder(SpriteBatch spriteBatch, Texture2D texture, Rectangle rect, Color color, int borderWidth)
        {
            spriteBatch.Draw(texture, new Rectangle(rect.X, rect.Y, rect.Width, borderWidth), color); // Top
            spriteBatch.Draw(texture, new Rectangle(rect.X, rect.Y + rect.Height - borderWidth, rect.Width, borderWidth), color); // Bottom
            spriteBatch.Draw(texture, new Rectangle(rect.X, rect.Y, borderWidth, rect.Height), color); // Left
            spriteBatch.Draw(texture, new Rectangle(rect.X + rect.Width - borderWidth, rect.Y, borderWidth, rect.Height), color); // Right
        }
    }
}