using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    /// <summary>
    /// Ngurus urutan kejadian pas player KALAH lawan WhoAmI: fade layar ke hitam pelan-pelan,
    /// munculin menu "restart dari 50% HP boss?" dengan pilihan Yes/No, lalu eksekusi pilihannya
    /// dan fade balik ke normal.
    ///
    /// Ini SENGAJA dipisah dari NPC WhoAmI itu sendiri (bukan ditaruh di AI-nya) supaya prosesnya
    /// tetap jalan mulus dari awal sampai akhir walaupun si NPC boss-nya beneran dimatiin di
    /// tengah jalan (yang kejadian kalau player milih "No" - NPC.active jadi false dan AI-nya
    /// berhenti di-tick, tapi ModSystem ini tetap jalan tiap frame independen dari itu).
    ///
    /// Ini juga yang jadi penyelesaian buat bug "kamera ngesot di detik-detik terakhir cutscene":
    /// WhoAmI.IsCutsceneActive baru beneran di-set false SETELAH layar hitam total (lihat
    /// PostUpdateEverything di bawah), jadi lompatan kamera pas kontrolnya lepas ke vanilla
    /// kejadian di balik layar hitam - gak pernah kelihatan sama sekali.
    /// </summary>
    public class WhoAmIDefeatMenuSystem : ModSystem
    {
        public static bool SequenceActive = false;
        public static bool MenuActive = false;
        public static bool Resolved = false;
        public static float FadeAlpha = 0f;
        public static int Choice = -1; // -1 = belum milih, 0 = Yes, 1 = No

        private const float FadeSpeed = 1f / 90f; // ~1.5 detik buat fade penuh satu arah

        public override void PostUpdateEverything()
        {
            if (!SequenceActive) return;

            // FIX: menu restart nggak pernah kelihatan / boss keliatan diem doang setelah nebas.
            // Penyebabnya: Main.hideUI masih true dari desperation cutscene (di-set true di
            // HandleDesperationCutscene) dan cuma di-set balik ke false di FASE 3 di bawah, SETELAH
            // player pencet Yes/No. Jadi sepanjang fade-ke-hitam (FASE 1) & nunggu klik menu (FASE 2),
            // hideUI masih nyala - dan custom UI yang digambar lewat PostDrawInterface (fade hitam +
            // tombol Yes/No) gak tergambar selama hideUI aktif, padahal state machine di balik layar
            // (fade alpha, nunggu klik) tetap jalan normal. Makanya kelihatannya "boss nebas lalu diem
            // aja" - sebenarnya lagi nunggu menu yang gak pernah nongol. Fix: paksa hideUI mati begitu
            // sequence kekalahan ini mulai, bukan nunggu sampai player udah milih.
            Main.hideUI = false;

            Player player = Main.LocalPlayer;

            // FASE 1: fade ke hitam. Kamera & kontrol masih dikunci cutscene oleh boss-nya sendiri
            // sepanjang fase ini (lihat STAGE 9 di WhoAmI.HandleDesperationCutscene).
            if (!MenuActive && !Resolved)
            {
                FadeAlpha = Math.Min(1f, FadeAlpha + FadeSpeed);
                if (FadeAlpha >= 1f)
                    MenuActive = true;
                return;
            }

            // FASE 2: layar hitam total, nunggu klik Yes/No (ditangani di PostDrawInterface).
            if (MenuActive)
            {
                if (Choice == -1) return;
                MenuActive = false;
                // lanjut ke FASE 3 di bawah pada tick yang sama.
            }

            // FASE 3: keputusan udah ada - eksekusi SEKALI selagi layar masih hitam total, baru
            // fade balik ke normal.
            if (!Resolved)
            {
                Resolved = true;

                int bossIndex = NPC.FindFirstNPC(ModContent.NPCType<WhoAmI>());
                WhoAmI boss = bossIndex != -1 ? Main.npc[bossIndex].ModNPC as WhoAmI : null;

                if (Choice == 0)
                {
                    // YES - lanjut lawan boss dari 50% HP.
                    boss?.ResumeFromDefeatMenu();
                }
                else
                {
                    // NO - boss ilang, player mati & respawn normal.
                    boss?.EndFromDefeatMenu();
                    player.KillMe(PlayerDeathReason.ByCustomReason($"{player.name} was cut down by the Terra Blade."), 999999, 0);
                }

                // Lepas kunci kamera & kontrol SEKARANG - masih ketutup layar hitam total, jadi
                // lompatan kameranya (kalau ada) gak pernah kelihatan sama sekali.
                WhoAmI.IsCutsceneActive = false;
                Main.hideUI = false;
                player.controlLeft = true;
                player.controlRight = true;
                player.controlUp = true;
                player.controlDown = true;
                player.controlJump = true;
                player.controlUseItem = true;
                player.controlUseTile = true;
                player.controlThrow = true;
            }

            // FASE 4: fade balik dari hitam ke normal - semuanya udah beres di belakang layar.
            FadeAlpha = Math.Max(0f, FadeAlpha - FadeSpeed);
            if (FadeAlpha <= 0f)
            {
                SequenceActive = false;
                Resolved = false;
                Choice = -1;
            }
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (FadeAlpha <= 0f) return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black * FadeAlpha);

            if (MenuActive)
                DrawMenu(spriteBatch, pixel);
        }

        private void DrawMenu(SpriteBatch spriteBatch, Texture2D pixel)
        {
            var font = FontAssets.MouseText.Value;
            const string title = "Do you want to restart from 50% boss HP?";
            Vector2 titleSize = font.MeasureString(title);
            Vector2 center = new Vector2(Main.screenWidth / 2f, Main.screenHeight / 2f);

            Rectangle panel = new Rectangle((int)(center.X - 260), (int)(center.Y - 90), 520, 180);

            // Panel warna-warni: background gelap dengan border yang siklus warna (colorful,
            // sesuai permintaan) biar keliatan beda dari UI vanilla yang monoton.
            spriteBatch.Draw(pixel, panel, new Color(35, 15, 55) * 0.95f);

            Color borderColor = Main.hslToRgb((Main.GameUpdateCount % 240) / 240f, 0.75f, 0.6f);
            const int bt = 4;
            spriteBatch.Draw(pixel, new Rectangle(panel.X - bt, panel.Y - bt, panel.Width + bt * 2, bt), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(panel.X - bt, panel.Y + panel.Height, panel.Width + bt * 2, bt), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(panel.X - bt, panel.Y - bt, bt, panel.Height + bt * 2), borderColor);
            spriteBatch.Draw(pixel, new Rectangle(panel.X + panel.Width, panel.Y - bt, bt, panel.Height + bt * 2), borderColor);

            spriteBatch.DrawString(font, title, new Vector2(center.X - titleSize.X / 2f, panel.Y + 26), Color.White);

            Rectangle yesBtn = new Rectangle((int)center.X - 180, panel.Y + 105, 150, 50);
            Rectangle noBtn = new Rectangle((int)center.X + 30, panel.Y + 105, 150, 50);

            bool clickedYes = DrawButton(spriteBatch, pixel, font, yesBtn, "Yes", new Color(40, 165, 75));
            bool clickedNo = DrawButton(spriteBatch, pixel, font, noBtn, "No", new Color(175, 45, 45));

            if (clickedYes) Choice = 0;
            else if (clickedNo) Choice = 1;
        }

        // Gambar satu tombol berwarna dengan highlight pas di-hover, dan balikin true kalau
        // tombol ini yang baru diklik (deteksi edge klik, bukan ditahan).
        private bool DrawButton(SpriteBatch spriteBatch, Texture2D pixel, DynamicSpriteFont font, Rectangle rect, string label, Color baseColor)
        {
            Point mouse = new Point((int)Main.MouseScreen.X, (int)Main.MouseScreen.Y);
            bool hover = rect.Contains(mouse);

            if (hover)
                Main.LocalPlayer.mouseInterface = true;

            Color fill = hover ? Color.Lerp(baseColor, Color.White, 0.3f) : baseColor;
            spriteBatch.Draw(pixel, rect, fill);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), Color.White * 0.85f);
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - 3, rect.Width, 3), Color.Black * 0.5f);

            Vector2 size = font.MeasureString(label);
            Vector2 pos = new Vector2(rect.X + rect.Width / 2f - size.X / 2f, rect.Y + rect.Height / 2f - size.Y / 2f);
            spriteBatch.DrawString(font, label, pos, Color.White);

            return hover && Main.mouseLeft && Main.mouseLeftRelease;
        }
    }
}