using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Fonts;

namespace YourModName.Content.NPCs
{
    // =========================================================================================
    // 🛑 [OVERLAY BOSS INTRO TWINS] Murni gambar doang -- semua ANGKA progress-nya (darken alpha,
    // jumlah huruf keketik, dst) udah dihitung di TwinsReworkOverride (lihat TwinsSpawnIntro.cs,
    // Stage BossIntro), file ini cuma baca properti itu terus DrawString. Numpang PERSIS pola
    // PlutoBossIntroSystem.cs (PostDrawInterface, bukan ModifyInterfaceLayers).
    //
    // Cuma nyala buat player yang jadi target (NPC.target) si Spazmatism, SESUAI pola yang sama
    // kayak PlutoBossIntroSystem.cs -- player lain (multiplayer) ga keliatan overlay ini.
    //
    // Font-nya diambil dari FontAssetSystem.HerrFochGradient (HerrFochGradient.dynamicfont) --
    // path: TheSanity/Fonts/HerrFochGradient, SAMA font yang dipakai Pluto (SESUAI REQUEST).
    //
    // BEDA UTAMA dari versi Pluto:
    //   - Teks "Mechanical Nightmare" / "The Twins" digambar per-HURUF, SETENGAH KIRI solid
    //     MERAH dan SETENGAH KANAN solid HIJAU (split tegas, BUKAN gradasi/Lerp — Lerp antara
    //     merah & hijau ngelewatin kuning/olive di tengah, makanya sebelumnya dihindari).
    //   - Pas awal diketik, huruf tampil abu-abu METAL dulu (belum "nyala").
    //   - Gantiin "glow scan putih" Pluto: sapuan REVEAL kiri->kanan yang bikin tiap huruf
    //     "nyala" ke warna merah/hijau-nya begitu sapuan lewatin posisinya, dan TETAP nampil
    //     gitu (gak balik abu-abu) sampai fade out.
    // =========================================================================================
    public class TwinsSpawnIntroSystem : ModSystem
    {
        // 🛑 [REQUEST] SETENGAH MERAH, SETENGAH HIJAU — bukan gradasi/Lerp(merah, hijau) lagi,
        // soalnya Lerp antara merah & hijau ngelewatin warna kuning/olive di tengah (itu makanya
        // sebelumnya "gradasi hijau selalu jadi kuning"). Sekarang split TEGAS: separuh huruf
        // kiri solid merah, separuh huruf kanan solid hijau, gak ada blend campuran sama sekali.
        private static readonly Color SplitLeftColor = new Color(220, 20, 20);   // merah
        private static readonly Color SplitRightColor = new Color(40, 200, 70);  // hijau

        // 🛑 [REQUEST] Pas awal diketik (belum "nyala" sama sekali), warnanya abu-abu METAL,
        // bukan merah lagi — biar keliatan kayak teks logam polos dulu sebelum "nyala" merah/hijau.
        private static readonly Color GradientPendingColor = new Color(150, 150, 160);

        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            if (Main.dedServ || Main.gameMenu) return;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != NPCID.Spazmatism) continue;

                TwinsReworkOverride global = npc.GetGlobalNPC<TwinsReworkOverride>();
                if (global == null || !global.IsSpawnIntroBossTextActive) continue;
                if (npc.target != Main.myPlayer) continue;

                DrawBossIntro(spriteBatch, npc, global);
            }
        }

        private void DrawBossIntro(SpriteBatch spriteBatch, NPC npc, TwinsReworkOverride global) {
            DynamicSpriteFont font = FontAssetSystem.HerrFochGradient?.Value;
            if (font == null) return;

            int screenW = Main.screenWidth;
            int screenH = Main.screenHeight;

            // -- Layar menggelap (rectangle hitam full-screen), sama kayak Pluto --
            if (global.SpawnIntroDarkenAlpha > 0f) {
                spriteBatch.Draw(
                    TextureAssets.MagicPixel.Value,
                    new Rectangle(0, 0, screenW, screenH),
                    Color.Black * (global.SpawnIntroDarkenAlpha * 0.96f)
                );
            }

            string typedTitle = TwinsSpawnIntro.BossIntroTitleText.Substring(0, global.SpawnIntroTypedCharCount);
            bool hasAnyText = typedTitle.Length > 0 || global.SpawnIntroShowBottomText;
            if (!hasAnyText) return;

            const float titleScale = 0.55f;
            const float nameScale = 0.5f;
            const float lineGap = 14f;

            // Ukuran teks judul PENUH (bukan yang keketik doang) dipake buat centering, biar
            // posisi judul ga geser-geser tiap ada huruf baru muncul -- sama kayak Pluto.
            Vector2 fullTitleSize = font.MeasureString(TwinsSpawnIntro.BossIntroTitleText) * titleScale;
            Vector2 nameSize = font.MeasureString(TwinsSpawnIntro.BossIntroNameText) * nameScale;

            Vector2 titlePos = new Vector2(screenW / 2f - fullTitleSize.X / 2f, screenH * 0.30f);
            Vector2 namePos = new Vector2(screenW / 2f - nameSize.X / 2f, titlePos.Y + fullTitleSize.Y + lineGap);

            float contentAlpha = global.SpawnIntroContentAlpha;
            Color shadowColor = Color.Black * (contentAlpha * 0.6f);

            // Judul atas -- typewriter + gradasi merah->kuning kiri->kanan per huruf
            if (typedTitle.Length > 0) {
                DrawGradientText(spriteBatch, font, typedTitle, titlePos, titleScale, shadowColor,
                    TwinsSpawnIntro.BossIntroTitleText.Length, global.SpawnIntroGradientRevealProgress, contentAlpha);
            }

            // Nama boss -- muncul utuh langsung (ga diketik), gradasi sama
            if (global.SpawnIntroShowBottomText) {
                DrawGradientText(spriteBatch, font, TwinsSpawnIntro.BossIntroNameText, namePos, nameScale, shadowColor,
                    TwinsSpawnIntro.BossIntroNameText.Length, global.SpawnIntroGradientRevealProgress, contentAlpha);

                // 🛑 [PEMANIS] Icon boss head di kiri-kanan nama "The Twins" -- SESUAI REQUEST:
                // ID 15 (Retinazer, mata merah) sebelum "The" & ID 20 (Spazmatism, mata hijau)
                // sesudah "Twins". Cocok sama split warna merah/hijau di teksnya sendiri.
                // Sumber: https://terraria.wiki.gg/wiki/NPC_Head_IDs (Bosses table).
                DrawBossHeadIcons(spriteBatch, namePos, nameSize, contentAlpha);
            }
        }

        // ==========================================
        // HELPER -- gambar 1 baris teks per-HURUF, tiap huruf dikasih warna dari Lerp(merah,
        // kuning) berdasarkan posisi horizontalnya RELATIF KE PANJANG TOTAL teks (fullLength,
        // BUKAN cuma panjang yang udah keketik) -- biar gradasinya konsisten kiri->kanan walau
        // lagi di tengah-tengah proses ngetik.
        //
        // revealProgress (0..1, dikunci ke 1f begitu sapuan reveal kelar, -1f kalau belum
        // mulai sama sekali -- lihat TwinsSpawnIntro.cs) nentuin sampe huruf keberapa dari kiri
        // yang UDAH "nyala" gradasinya; sisanya (belum kelewatan sapuan) digambar pakai
        // GradientPendingColor dulu.
        // ==========================================
        private static void DrawGradientText(SpriteBatch spriteBatch, DynamicSpriteFont font, string visibleText,
            Vector2 position, float scale, Color shadowColor, int fullLength, float revealProgress, float contentAlpha) {

            float x = position.X;
            for (int i = 0; i < visibleText.Length; i++) {
                string ch = visibleText[i].ToString();
                Vector2 chPos = new Vector2(x, position.Y);

                float t = fullLength <= 1 ? 0f : i / (float)(fullLength - 1);
                Color splitColor = t < 0.5f ? SplitLeftColor : SplitRightColor; // split tegas, BUKAN Lerp
                Color baseColor = (revealProgress >= 0f && t <= revealProgress) ? splitColor : GradientPendingColor;

                spriteBatch.DrawString(font, ch, chPos + new Vector2(3f, 3f) * scale, shadowColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, ch, chPos, baseColor * contentAlpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                x += font.MeasureString(ch).X * scale;
            }
        }

        // ==========================================
        // 🛑 [PEMANIS] Icon Boss Head ID 15 (Retinazer, merah) di KIRI sebelum "The", dan ID 20
        // (Spazmatism, hijau) di KANAN sesudah "Twins" -- posisinya nempel ke sisi kiri/kanan
        // bounding box teks nama (namePos/nameSize), di-center vertikal terhadap tinggi teks,
        // di-scale biar tinggi icon-nya kira-kira nyamain tinggi teks nama.
        // ==========================================
        private static void DrawBossHeadIcons(SpriteBatch spriteBatch, Vector2 namePos, Vector2 nameSize, float contentAlpha) {
            Texture2D leftIcon = TextureAssets.NpcHeadBoss[15].Value;  // Retinazer (first form) - merah
            Texture2D rightIcon = TextureAssets.NpcHeadBoss[20].Value; // Spazmatism (first form) - hijau

            const float iconGap = 12f; // jarak icon ke teks
            float iconScale = (nameSize.Y / leftIcon.Height) * 1.1f; // dikit lebih gede biar seimbang sama teks

            Vector2 leftIconSize = new Vector2(leftIcon.Width, leftIcon.Height) * iconScale;
            Vector2 rightIconSize = new Vector2(rightIcon.Width, rightIcon.Height) * iconScale;

            Vector2 leftIconPos = new Vector2(namePos.X - iconGap - leftIconSize.X, namePos.Y + nameSize.Y / 2f - leftIconSize.Y / 2f);
            Vector2 rightIconPos = new Vector2(namePos.X + nameSize.X + iconGap, namePos.Y + nameSize.Y / 2f - rightIconSize.Y / 2f);

            Color iconColor = Color.White * contentAlpha;

            spriteBatch.Draw(leftIcon, leftIconPos, null, iconColor, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(rightIcon, rightIconPos, null, iconColor, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);
        }
    }
}
