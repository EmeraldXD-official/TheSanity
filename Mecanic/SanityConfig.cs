using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TheSanity
{
    public class SanityConfig : ModConfig
    {
        // Menggunakan ClientSide agar setingan ini bisa diatur tiap player sendiri-sendiri
        public override ConfigScope Mode => ConfigScope.ClientSide;

        // =========================================================================
        // FIX CRASH: Atribut [Header] dihapus karena tModLoader 1.4+ melarang spasi di sana.
        // Karena hanya ada 1 settingan, tombol akan langsung muncul dengan rapi tanpa divider.
        // =========================================================================

        [DefaultValue(false)] // Default awal tidak skip (false)
        [Label("Skip Moon Lord Credits & Music")]
        [Tooltip("Jika diaktifkan, kredit akhir dan musiknya setelah mengalahkan Moon Lord akan otomatis dilewati.")]
        public bool SkipCredits;
    }
}