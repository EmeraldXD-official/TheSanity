using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TheSanity.Items
{
    /// <summary>
    /// Mode "kategori audio" yang direspon oleh visualizer Boom Bod. Diganti lewat
    /// ALT + Klik-Kanan item ini di inventory (lihat BoomBodLith.CanRightClick/RightClick).
    /// </summary>
    public enum BoomBodMode
    {
        Music,
        Sound,
        Ambient,
        All
    }

    /// <summary>
    /// Aksesoris "Boom Bod Lith" -- item murni kosmetik/visual. Selama item ini terpasang
    /// (di slot aksesoris FUNGSIONAL ataupun VANITY, keduanya didukung -- lihat
    /// BoomBodPlayer.PostUpdateEquips yang men-scan Player.armor), sebuah bar/linear
    /// spectrum visualizer putih akan tampil mengelilingi (framing) tepi layar, naik-turun
    /// mengikuti volume Musik/Suara/Ambient in-game (lihat AudioSpectrumSampler).
    ///
    /// CATATAN JUJUR soal "audio reactive": tModLoader/Terraria tidak menyediakan API publik
    /// untuk membaca waveform/FFT mentah dari audio yang sedang diputar (mesin audionya
    /// tidak expose buffer sample-nya). Jadi visualizer ini BUKAN FFT asli dari suara game --
    /// dia pakai generator ritme prosedural yang intensitasnya ditarik dari slider volume
    /// asli (Main.musicVolume / Main.soundVolume / Main.ambientVolume) sesuai mode yang
    /// dipilih. Hasilnya tetap kelihatan hidup & "jedag-jedug", tapi bukan representasi
    /// sample-accurate dari lagu yang sedang main. Kalau suatu saat ketemu cara resmi buat
    /// membaca amplitude asli, tinggal ganti isi AudioSpectrumSampler.GetBands() saja --
    /// semua kode di file ini & BoomBodVisualizerSystem tidak perlu diubah.
    ///
    /// ASET: taruh sprite di TheSanity/Items/BoomBodLith.png (34x34 disarankan, sesuaikan
    /// Item.width/height di bawah kalau ukuran spritemu beda).
    /// </summary>
    public class BoomBodLith : ModItem
    {
        // Diset eksplisit (bukan mengandalkan auto-path dari lokasi class) supaya aman
        // walau nanti file .cs ini dipindah ke sub-folder lain.
        public override string Texture => "TheSanity/Items/BoomBodLith";

        /// <summary>Mode aktif saat ini, persist ke save data item (lihat SaveData/LoadData).</summary>
        public BoomBodMode Mode = BoomBodMode.All;

        public override void SetStaticDefaults()
        {
            // Mencegah item ke-stack lebih dari 1 (standar buat aksesoris, tapi dipastikan eksplisit).
        }

        public override void SetDefaults()
        {
            Item.width = 34;
            Item.height = 34;
            Item.accessory = true;
            Item.maxStack = 1;
            Item.value = Item.sellPrice(gold: 3);
            Item.rare = ItemRarityID.LightRed;
        }

        // --- Efek fungsional: TIDAK ADA. Item ini murni visual, semua logika deteksi
        // "apakah lagi dipakai (fungsional ATAU vanity) + dye apa yang kepasang" dikerjakan
        // di BoomBodPlayer.PostUpdateEquips supaya satu jalur kode yang sama menangani
        // kedua jenis slot tanpa perlu mengandalkan hook vanity-only yang lebih jarang dipakai.
        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            // Sengaja dikosongkan -- lihat BoomBodPlayer.
        }

        // ===================== GANTI MODE: ALT + KLIK KANAN DI INVENTORY =====================

        // CanRightClick() dites SETIAP kali item ini di-klik-kanan di slot manapun (inventory,
        // slot aksesoris fungsional, ATAUPUN slot vanity). Kita hanya mengaktifkan perilaku
        // custom kalau tombol ALT lagi ditekan -- tanpa ALT, return false, jadi klik-kanan biasa
        // tetap berperilaku normal/tidak melakukan apa-apa (seperti item non-spesial lainnya).
        public override bool CanRightClick()
        {
            return Main.keyState.IsKeyDown(Keys.LeftAlt) || Main.keyState.IsKeyDown(Keys.RightAlt);
        }

        public override void RightClick(Player player)
        {
            Mode = Mode switch
            {
                BoomBodMode.Music => BoomBodMode.Sound,
                BoomBodMode.Sound => BoomBodMode.Ambient,
                BoomBodMode.Ambient => BoomBodMode.All,
                _ => BoomBodMode.Music,
            };

            string captureStatus = AudioCaptureEngine.IsAvailable
                ? "audio capture: AKTIF (real)"
                : "audio capture: fallback simulasi (belum/nggak tersedia)";

            Main.NewText($"Boom Bod Lith: mode diganti ke [{ModeLabel(Mode)}] -- {captureStatus}", new Color(210, 210, 255));
        }

        // Wajib di-override return false, soalnya default RightClick() akan mengonsumsi 1 stack
        // item begitu CanRightClick() true -- kita cuma mau ganti mode, item-nya jangan hilang.
        public override bool ConsumeItem(Player player)
        {
            return false;
        }

        public static string ModeLabel(BoomBodMode mode)
        {
            return mode switch
            {
                BoomBodMode.Music => "Music",
                BoomBodMode.Sound => "Sound",
                BoomBodMode.Ambient => "Ambient",
                _ => "All",
            };
        }

        // ===================== TOOLTIP =====================

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "BoomBodMode",
                $"Mode saat ini: [{ModeLabel(Mode)}]"));
            tooltips.Add(new TooltipLine(Mod, "BoomBodHint",
                "ALT + Klik Kanan untuk ganti mode (Music / Sound / Ambient / All)"));
            tooltips.Add(new TooltipLine(Mod, "BoomBodDye",
                "Warna garis spectrum mengikuti dye yang dipasang")
            {
                OverrideColor = new Color(180, 180, 255)
            });
        }

        // ===================== PERSISTENSI MODE =====================

        public override void SaveData(TagCompound tag)
        {
            tag["boomBodMode"] = (int)Mode;
        }

        public override void LoadData(TagCompound tag)
        {
            if (tag.ContainsKey("boomBodMode"))
                Mode = (BoomBodMode)tag.GetInt("boomBodMode");
        }
    }
}
