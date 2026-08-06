using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using TheSanity.Fonts;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    // =========================================================================================
    // 🛑 [OVERLAY BOSS INTRO PLUTO] Murni gambar doang -- semua ANGKA progress-nya (darken alpha,
    // jumlah huruf keketik, dst) udah dihitung di PlutoHead (lihat PlutoSpawnDash.cs, Stage 2 /
    // SpawnStageBossIntro), file ini cuma baca properti itu terus DrawString.
    //
    // Cuma nyala buat player yang jadi target (NPC.target) si Pluto, SESUAI pola yang sama kayak
    // PlutoSpawnCameraPlayer.cs -- player lain (multiplayer) ga keliatan overlay ini.
    //
    // Font-nya diambil dari FontAssetSystem.HerrFochGradient (HerrFochGradient.dynamicfont).
    // =========================================================================================
    public class PlutoBossIntroSystem : ModSystem
    {
        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            if (Main.dedServ || Main.gameMenu) return;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != ModContent.NPCType<PlutoHead>()) continue;
                if (npc.ModNPC is not PlutoHead head) continue;
                if (!head.IsBossIntroActive) continue;
                if (npc.target != Main.myPlayer) continue;

                DrawBossIntro(spriteBatch, head);
            }
        }

        private void DrawBossIntro(SpriteBatch spriteBatch, PlutoHead head) {
            DynamicSpriteFont font = FontAssetSystem.HerrFochGradient?.Value;
            if (font == null) return;

            int screenW = Main.screenWidth;
            int screenH = Main.screenHeight;

            // -- Layar menggelap (rectangle hitam full-screen) --
            // 🛑 Sengaja hampir solid black (0.96) di puncaknya biar bener-bener "gelap", bukan
            // cuma redup transparan kayak sebelumnya.
            if (head.BossIntroDarkenAlpha > 0f) {
                spriteBatch.Draw(
                    TextureAssets.MagicPixel.Value,
                    new Rectangle(0, 0, screenW, screenH),
                    Color.Black * (head.BossIntroDarkenAlpha * 0.96f)
                );
            }

            string typedTitle = PlutoHead.BossIntroTitleText.Substring(0, head.BossIntroTypedCharCount);
            bool hasAnyText = typedTitle.Length > 0 || head.BossIntroShowBottomText;
            if (!hasAnyText) return;

            const float titleScale = 0.55f;
            const float nameScale = 0.5f; // dinaikin dari 0.32 biar font-nya ga keliatan kekecilan/ancur
            const float lineGap = 14f;

            // 🛑 [WARNA] Judul & nama boss sama-sama merah (SESUAI REQUEST), shadow-nya tetep item
            // biar teksnya kebaca jelas walau di atas layar yang udah gelap.
            Color bossIntroRed = new Color(220, 20, 20);

            // Ukuran teks judul PENUH (bukan yang keketik doang) dipake buat centering, biar posisi
            // judul ga geser-geser tiap ada huruf baru muncul.
            Vector2 fullTitleSize = font.MeasureString(PlutoHead.BossIntroTitleText) * titleScale;
            Vector2 nameSize = font.MeasureString(PlutoHead.BossIntroNameText) * nameScale;

            Vector2 titlePos = new Vector2(screenW / 2f - fullTitleSize.X / 2f, screenH * 0.30f);
            Vector2 namePos = new Vector2(screenW / 2f - nameSize.X / 2f, titlePos.Y + fullTitleSize.Y + lineGap);

            Color textColor = bossIntroRed * head.BossIntroContentAlpha;
            Color shadowColor = Color.Black * (head.BossIntroContentAlpha * 0.6f);

            // Judul atas -- typewriter, cuma gambar sepanjang huruf yg udah "keketik"
            if (typedTitle.Length > 0) {
                spriteBatch.DrawString(font, typedTitle, titlePos + new Vector2(3f, 3f), shadowColor, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, typedTitle, titlePos, textColor, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
            }

            // Nama boss -- muncul utuh langsung, ga diketik
            if (head.BossIntroShowBottomText) {
                spriteBatch.DrawString(font, PlutoHead.BossIntroNameText, namePos + new Vector2(2f, 2f), shadowColor, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, PlutoHead.BossIntroNameText, namePos, textColor, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);
            }

            // -- Glow scan kiri -> kanan, ngelewatin bounding box kedua teks --
            if (head.BossIntroGlowScanProgress >= 0f) {
                Rectangle scanBounds = new Rectangle(
                    (int)titlePos.X - 10,
                    (int)titlePos.Y - 6,
                    (int)fullTitleSize.X + 20,
                    (int)((namePos.Y + nameSize.Y) - titlePos.Y) + 12
                );

                DrawGlowScanSweep(spriteBatch, scanBounds, head.BossIntroGlowScanProgress, head.BossIntroContentAlpha);
            }
        }

        // 🛑 [GLOW SCAN] Diimplementasi sebagai beberapa strip vertikal tipis dengan alpha yang
        // ngedrop makin jauh dari posisi scan sekarang (falloff segitiga, di-square biar lebih
        // "lembut" di tepi), digambar pake BlendState.Additive biar keliatan nge-glow nimpa teks.
        // Perlu End()+Begin() ulang karena spritebatch bawaan PostDrawInterface pake AlphaBlend.
        private void DrawGlowScanSweep(SpriteBatch spriteBatch, Rectangle bounds, float progress, float contentAlpha) {
            const int stripCount = 28;
            float scanX = bounds.X + bounds.Width * progress;
            float glowWidth = MathHelper.Max(bounds.Width * 0.16f, 24f);
            float stripWidth = bounds.Width / (float)stripCount;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            for (int s = 0; s < stripCount; s++) {
                float stripX = bounds.X + s * stripWidth;
                float dist = System.Math.Abs((stripX + stripWidth * 0.5f) - scanX);
                float alpha = MathHelper.Clamp(1f - dist / glowWidth, 0f, 1f);
                alpha *= alpha; // falloff lebih lembut di tepi
                if (alpha <= 0.02f) continue;

                Color glowColor = Color.White * (alpha * 0.8f * contentAlpha);
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Rectangle((int)stripX, bounds.Y, (int)stripWidth + 2, bounds.Height), glowColor);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
