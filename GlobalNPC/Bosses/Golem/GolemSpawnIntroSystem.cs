using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Fonts;

namespace TheSanity.GlobalNPCs
{
    // =========================================================================================
    // 🛑 [OVERLAY BOSS INTRO - GOLEM] Murni gambar doang -- semua ANGKA progress-nya udah
    // dihitung di GolemSpawnIntroOverride.cs, file ini cuma baca properti itu terus DrawString,
    // PERSIS pola PlutoBossIntroSystem.cs / TwinsSpawnIntroSystem.cs (PostDrawInterface).
    //
    // BEDA UTAMA dari Pluto/Twins (SESUAI REQUEST):
    //   - Layar PURE HITAM solid (bukan 0.96 kayak Pluto) - full opaque pas puncaknya.
    //   - Teks warna COKLAT kayak batu candi Lihzahrd (bukan merah/gradasi merah-hijau).
    //   - Sapuan highlight-nya kuning-oranye, MIRING bentuk "/" (bukan vertikal kayak Pluto
    //     atau reveal-per-huruf kayak Twins), nyapu kiri ke kanan sekali jalan lalu fade.
    //   - TIDAK ADA logic kamera SAMA SEKALI di file ini -- kamera player tetep normal dari
    //     awal sampe akhir cutscene, SESUAI REQUEST ("camera nggak dimainin").
    // =========================================================================================
    public class GolemSpawnIntroSystem : ModSystem
    {
        // Warna coklat batu candi Lihzahrd.
        private static readonly Color TempleBrownColor = new Color(150, 95, 45);

        // Warna sapuan glow: kuning-oranye.
        private static readonly Color GlowSweepColor = new Color(255, 170, 50);

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (Main.dedServ || Main.gameMenu) return;

            int golemIdx = NPC.FindFirstNPC(NPCID.Golem);
            if (golemIdx == -1) return;

            NPC golem = Main.npc[golemIdx];
            if (!golem.active) return;

            GolemSpawnIntroOverride intro = golem.GetGlobalNPC<GolemSpawnIntroOverride>();
            if (intro == null || !intro.SpawnIntroActive) return;

            // Cuma nyala buat player yang jadi target Golem -- player lain (multiplayer) gak
            // keliatan overlay ini, pola yang sama kayak Pluto/Twins.
            if (golem.target != Main.myPlayer) return;

            DrawBossIntro(spriteBatch, intro);
        }

        private void DrawBossIntro(SpriteBatch spriteBatch, GolemSpawnIntroOverride intro)
        {
            DynamicSpriteFont font = FontAssetSystem.HerrFochGradient?.Value;
            if (font == null) return;

            int screenW = Main.screenWidth;
            int screenH = Main.screenHeight;

            // -- Layar PURE HITAM (full opaque pas puncaknya, gak dikurangin kayak Pluto) --
            if (intro.SpawnIntroDarkenAlpha > 0f)
            {
                spriteBatch.Draw(
                    TextureAssets.MagicPixel.Value,
                    new Rectangle(0, 0, screenW, screenH),
                    Color.Black * intro.SpawnIntroDarkenAlpha
                );
            }

            string typedTitle = GolemSpawnIntroOverride.BossIntroTitleText.Substring(0, intro.SpawnIntroTypedCharCount);
            bool hasAnyText = typedTitle.Length > 0 || intro.SpawnIntroShowBottomText;
            if (!hasAnyText) return;

            const float titleScale = 0.55f;
            const float nameScale = 0.5f;
            const float lineGap = 14f;

            // Ukuran teks judul PENUH (bukan yang keketik doang) dipake buat centering, biar
            // posisi judul ga geser-geser tiap ada huruf baru muncul.
            Vector2 fullTitleSize = font.MeasureString(GolemSpawnIntroOverride.BossIntroTitleText) * titleScale;
            Vector2 nameSize = font.MeasureString(GolemSpawnIntroOverride.BossIntroNameText) * nameScale;

            Vector2 titlePos = new Vector2(screenW / 2f - fullTitleSize.X / 2f, screenH * 0.30f);
            Vector2 namePos = new Vector2(screenW / 2f - nameSize.X / 2f, titlePos.Y + fullTitleSize.Y + lineGap);

            Color textColor = TempleBrownColor * intro.SpawnIntroContentAlpha;
            Color shadowColor = Color.Black * (intro.SpawnIntroContentAlpha * 0.6f);

            // Judul atas -- typewriter
            if (typedTitle.Length > 0)
            {
                spriteBatch.DrawString(font, typedTitle, titlePos + new Vector2(3f, 3f), shadowColor, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, typedTitle, titlePos, textColor, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
            }

            // Nama boss -- muncul utuh langsung, ga diketik
            if (intro.SpawnIntroShowBottomText)
            {
                spriteBatch.DrawString(font, GolemSpawnIntroOverride.BossIntroNameText, namePos + new Vector2(2f, 2f), shadowColor, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, GolemSpawnIntroOverride.BossIntroNameText, namePos, textColor, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);
            }

            // -- Sapuan glow "/" kiri->kanan, ngelewatin bounding box kedua teks --
            if (intro.SpawnIntroGlowSweepProgress >= 0f)
            {
                Rectangle scanBounds = new Rectangle(
                    (int)titlePos.X - 10,
                    (int)titlePos.Y - 6,
                    (int)fullTitleSize.X + 20,
                    (int)((namePos.Y + nameSize.Y) - titlePos.Y) + 12
                );

                DrawDiagonalGlowSweep(spriteBatch, scanBounds, intro.SpawnIntroGlowSweepProgress, intro.SpawnIntroContentAlpha);
            }
        }

        // ==========================================
        // 🛑 [SAPUAN "/"] Beberapa bar tipis yang di-ROTASI miring (-22°, bikin kemiringan "/")
        // ditumpuk dengan alpha falloff segitiga (puncak di tengah, makin ke pinggir makin
        // transparan) buat efek glow yang lembut, digambar pake BlendState.Additive biar
        // keliatan nge-glow nimpa teks coklatnya. Posisi horizontal (scanX) gerak dari kiri
        // bounds ke kanan bounds sesuai progress (0..1). Teknik gambar bar rotasi: pixel 1x1
        // di-scale jadi (thickness, height) dengan origin (0.5,0.5) biar rotasinya muter pas
        // di tengah bar itu sendiri.
        // ==========================================
        private static void DrawDiagonalGlowSweep(SpriteBatch spriteBatch, Rectangle bounds, float progress, float contentAlpha)
        {
            const int barCount = 7;
            const float angleDegrees = -22f; // miring "/"
            float angle = MathHelper.ToRadians(angleDegrees);

            float scanX = bounds.X + bounds.Width * progress;
            float barHeight = bounds.Height * 1.7f; // dilebihin biar pas dirotasi tetep nutup penuh tinggi bounds
            float barSpacing = MathHelper.Max(bounds.Width * 0.018f, 3f);
            float barThickness = MathHelper.Max(bounds.Width * 0.05f, 8f);

            Texture2D pixel = TextureAssets.MagicPixel.Value;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);

            for (int i = 0; i < barCount; i++)
            {
                float t = i - (barCount - 1) / 2f; // -3..3 buat barCount=7
                float offsetX = t * barSpacing;
                float alphaFactor = 1f - System.Math.Abs(t) / ((barCount - 1) / 2f);
                alphaFactor = MathHelper.Clamp(alphaFactor, 0f, 1f);
                alphaFactor *= alphaFactor; // falloff lebih lembut di tepi

                if (alphaFactor <= 0.02f) continue;

                Vector2 barCenter = new Vector2(scanX + offsetX, bounds.Y + bounds.Height / 2f);
                Color barColor = GlowSweepColor * (alphaFactor * 0.85f * contentAlpha);

                spriteBatch.Draw(pixel, barCenter, null, barColor, angle, new Vector2(0.5f, 0.5f), new Vector2(barThickness, barHeight), SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.UIScaleMatrix);
        }
    }
}
