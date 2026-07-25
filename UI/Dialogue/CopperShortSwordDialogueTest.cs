using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.UI.DialogueSystem
{
    /// <summary>
    /// FILE TEST DOANG - dipakai buat REVIEW SEMUA FITUR Dialogue Box dalam satu tempat.
    /// Begitu player nge-klik/pakai item CopperShortsword (item vanilla), Dialogue Box kebuka
    /// nampilin beberapa baris obrolan yang masing-masing sengaja dipilih buat mendemokan
    /// 1-2 fitur berbeda (ditandai komentar "FITUR:" di tiap baris biar gampang di-scan).
    ///
    /// Yang DIDEMOKAN LANGSUNG jalan (aman, ga butuh asset tambahan apapun):
    ///   - Tema dipaksa (SetTheme) + Palet warna (SetPaletteColorByName)
    ///   - Font custom per-baris (CustomFontPath) - otomatis fallback aman kalau font belum ada
    ///   - Icon item inline [i:ID] pakai texture item vanilla (selalu ada)
    ///   - Teks berwarna per-bagian [c/RRGGBB:teks]
    ///   - Timer per-baris custom, timer OFF, dan InfiniteTime
    ///   - Cleanup tema/palet otomatis pas box ditutup (OnClosed)
    ///
    /// Yang SENGAJA DIBIARIN KOSONG/COMMENT (butuh asset yang belum tentu ada di project kamu,
    /// biar ga crash pas testing) - tinggal isi/un-comment begitu asset-nya udah ada:
    ///   - Icon karakter P1/P2 (SpeakerData.Icon)
    ///   - BGM / TypingSound / SoundEffect (butuh file audio asli)
    ///
    /// Pakai GlobalItem + AppliesToEntity biar cuma nempel ke CopperShortsword aja, item
    /// lain ga kesenggol sama sekali.
    /// </summary>
    public class CopperShortSwordDialogueTest : GlobalItem
    {
        public override bool AppliesToEntity(Item item, bool lateInstantiation) =>
            item.type == ItemID.CopperShortsword;

        public override bool? UseItem(Item item, Player player)
        {
            // Cuma jalanin di sisi client punya si player sendiri, biar ga kepanggil
            // berkali-kali di server / punya player lain pas multiplayer.
            if (player.whoAmI == Main.myPlayer && Main.netMode != NetmodeID.Server)
                TryOpenTestDialogue();

            return base.UseItem(item, player);
        }

        private void TryOpenTestDialogue()
        {
            var system = ModContent.GetInstance<DialogueUISystem>();
            if (system?.DialogueBox == null) return;

            UIDialogueBox box = system.DialogueBox;

            // Guard: kalau box lagi kebuka, jangan Open() lagi tiap kali ayunan pedang
            // (UseItem bisa kepanggil tiap swing) - ini cuma buat testing biar ga numpuk.
            if (box.IsOpen) return;

            // ================================================================
            // FITUR: Tema dipaksa (bagian 6) + Palet warna (bagian 6)
            // Dipanggil SEBELUM Open() - "Dark Retro" (default) di-hue-shift jadi "Dark Cyan"
            // biar gampang kebedain dari tema default pas testing. Ganti/hapus baris ini
            // kalau mau tetap ikut pilihan tema player di Mod Config.
            // ================================================================
            box.SetTheme(DialogueThemes.DarkRetro);
            box.SetPaletteColorByName("Cyan");

            // Cleanup otomatis: balikin tema/palet ke default pas box ditutup, biar dialog
            // NPC/item lain (yang ga manggil SetTheme sendiri) ga ikut kebawa tema tes ini.
            // Pola "unsubscribe diri sendiri" ini penting karena TryOpenTestDialogue() bisa
            // kepanggil berkali-kali (tiap kali item dipakai lagi) - tanpa ini, handler bakal
            // numpuk subscribe berkali-kali tiap kali dialog dibuka ulang.
            Action<UIDialogueBox> cleanupOnClose = null;
            cleanupOnClose = b =>
            {
                b.ClearForcedTheme();
                b.ClearPaletteColor();
                b.OnClosed -= cleanupOnClose;
            };
            box.OnClosed += cleanupOnClose;

            var lines = new List<DialogueLine>
            {
                // ============================================================
                // BARIS 1 - percakapan dasar P1/P2 (lihat bagian 2 panduan)
                // BGM/TypingSound/SoundEffect sengaja ga di-set (null) = ga ada musik/SFX -
                // un-comment & ganti path di bawah begitu udah ada file audio asli.
                // ============================================================
                new DialogueLine
                {
                    // BGM = "TheSanity/Music/EmeraldTheme", // FITUR: BGM (bagian 3) - un-comment kalau udah ada asset
                    // TypingSound = "TheSanity/Sounds/TypeTick", // FITUR: suara ketik custom
                    // SoundEffect = "TheSanity/Sounds/SwordClink", // FITUR: SFX sekali pas baris ini tampil
                    P1 = new SpeakerData
                    {
                        Active   = true,
                        Nametag  = "Copper Shortsword",
                        // Icon  = null -> kotak icon tetap kegambar, cuma kosong dulu.
                        // Icon  = ModContent.Request<Texture2D>("Path/Ke/Texture"), // FITUR: icon karakter
                        Dialogue = "Woy! Baru aja lo ayun-ayunin gue buat nebas slime.",
                    },
                    P2 = new SpeakerData
                    {
                        Active   = true,
                        Nametag  = "Kamu",
                        Dialogue = "", // belum ngomong, cuma nongol redup
                    },
                },

                // ============================================================
                // BARIS 2 - FITUR: Font Custom per-baris + Ukuran Custom (bagian 12)
                // CustomFontPath cuma ngaruh ke baris ini doang. Kalau path-nya belum ada
                // (kamu belum bikin SaiFont.dynamicfont), DialogueFontCache otomatis fallback
                // ke font default TANPA crash - jadi aman dites walau asset-nya belum siap.
                //
                // CustomFontScale = 1.5f di sini contoh KOMPENSASI ukuran: font "Sai" (gaya
                // tulisan tangan) secara alami tampil lebih kecil/tipis dibanding font default
                // Terraria kalau dipakai di TextScale yang sama, jadi dinaikin 150% biar tetap
                // gampang dibaca. Angka ini BEBAS disesuaikan (mis. 1.2f, 2f, dst) - ga
                // ngaruh ke baris lain yang ga di-set / diset null.
                // ============================================================
                new DialogueLine
                {
                    CustomFontPath = "TheSanity/Fonts/SaiFont",
                    CustomFontScale = 1.5f,
                    P1 = new SpeakerData { Active = true, Nametag = "Copper Shortsword", Dialogue = "" },
                    P2 = new SpeakerData
                    {
                        Active   = true,
                        Nametag  = "Kamu",
                        Dialogue = "Baris ini pakai font custom (kalau udah kamu setup) - baris lain tetap font default.",
                    },
                },

                // ============================================================
                // BARIS 3 - FITUR: Icon item inline [i:ID] + teks berwarna [c/RRGGBB:teks] (bagian 13)
                // Icon pakai ID item vanilla (selalu ada, aman buat testing). Kata "BAHAYA"
                // sengaja diwarnain merah, sisanya tetap ikut warna teks tema aktif.
                // ============================================================
                new DialogueLine
                {
                    P1 = new SpeakerData
                    {
                        Active   = true,
                        Nametag  = "Copper Shortsword",
                        Dialogue = $"Coba lihat [i:{ItemID.IronBroadsword}] ini, kelihatan [c/FF0000:BAHAYA] banget kan?",
                    },
                    P2 = new SpeakerData { Active = true, Nametag = "Kamu", Dialogue = "" },
                },

                // ============================================================
                // BARIS 4 - FITUR: gabungan icon + warna + emosi P2 gantian ngomong (bagian 2 & 13)
                // ============================================================
                new DialogueLine
                {
                    P1 = new SpeakerData { Active = true, Nametag = "Copper Shortsword", Dialogue = "" },
                    P2 = new SpeakerData
                    {
                        Active   = true,
                        Nametag  = "Kamu",
                        Dialogue = $"Santai, ini cuma tes doang - aku [c/00FF00:AMAN] kok walau bawa [i:{ItemID.LifeCrystal}].",
                    },
                },

                // ============================================================
                // BARIS 5 - FITUR: Timer custom per-baris (bagian 9) - 8 detik, lebih cepat dari default 15 detik
                // ============================================================
                new DialogueLine
                {
                    TimerDuration = 8f,
                    P1 = new SpeakerData
                    {
                        Active   = true,
                        Nametag  = "Copper Shortsword",
                        Dialogue = "Baris ini punya timer custom 8 detik doang, lebih cepet dari default.",
                    },
                    P2 = new SpeakerData { Active = true, Nametag = "Kamu", Dialogue = "" },
                },

                // ============================================================
                // BARIS 6 - FITUR: InfiniteTime (bagian 9) - timer OFF total, nunggu klik manual
                // ============================================================
                new DialogueLine
                {
                    InfiniteTime = true,
                    P1 = new SpeakerData { Active = true, Nametag = "Copper Shortsword", Dialogue = "" },
                    P2 = new SpeakerData
                    {
                        Active   = true,
                        Nametag  = "Kamu",
                        Dialogue = "Baris ini nunggu aku klik sendiri, ga ada batas waktu (InfiniteTime).",
                    },
                },

                // ============================================================
                // BARIS 7 (terakhir) - penutup, KeepBgmAfterClose didemokan di komentar
                // ============================================================
                new DialogueLine
                {
                    // KeepBgmAfterClose = true, // FITUR: BGM tetap lanjut muter walau box ditutup
                    P1 = new SpeakerData
                    {
                        Active   = true,
                        Nametag  = "Copper Shortsword",
                        Dialogue = "Oh gitu. Yaudah, semua fitur udah kepamer, tinggal pasang asset asli aja nanti.",
                    },
                    P2 = new SpeakerData { Active = true, Nametag = "Kamu", Dialogue = "" },
                },
            };

            box.Open(lines);
        }
    }
}
