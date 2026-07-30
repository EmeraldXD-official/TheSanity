using System.Collections.Generic;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TheSanity.UI.DialogueSystem
{
    /// <summary>
    /// Cache font custom yang diminta lewat DialogueLine.CustomFontPath, biar ga nge-Request()
    /// ulang dari asset system tiap frame (Request itu murah kalau udah kepanggil sekali & di-cache
    /// sendiri sama tModLoader, tapi tetap lebih murah kalau kita cache di sisi kita juga).
    ///
    /// Kalau path invalid/gagal di-load (misal typo nama file/mod), otomatis fallback ke font
    /// default (FontAssets.MouseText) DAN path itu ditandai gagal biar ga terus-terusan dicoba
    /// ulang tiap frame (yang bisa nge-spam performa/log kalau kejadian tiap Draw()).
    /// </summary>
    public static class DialogueFontCache
    {
        private static readonly Dictionary<string, Asset<DynamicSpriteFont>> _cache = new Dictionary<string, Asset<DynamicSpriteFont>>();
        private static readonly HashSet<string> _failed = new HashSet<string>();

        /// <summary>
        /// path null/kosong ATAU path yang sebelumnya gagal di-load -> otomatis balik ke
        /// font default sistem (FontAssets.MouseText). Ini yang bikin custom font sifatnya
        /// "opt-in per baris" - kalau DialogueLine.CustomFontPath ga diisi, dialog itu tetap
        /// pakai font bawaan seperti biasa, ga ke-ubah semua.
        /// </summary>
        public static DynamicSpriteFont Get(string path)
        {
            if (string.IsNullOrWhiteSpace(path) || _failed.Contains(path))
                return FontAssets.MouseText.Value;

            if (_cache.TryGetValue(path, out Asset<DynamicSpriteFont> cached))
                return cached.Value;

            try
            {
                Asset<DynamicSpriteFont> asset = ModContent.Request<DynamicSpriteFont>(path, AssetRequestMode.ImmediateLoad);
                _cache[path] = asset;
                return asset.Value;
            }
            catch
            {
                _failed.Add(path);
                return FontAssets.MouseText.Value;
            }
        }

        /// <summary>
        /// Reset daftar path yang pernah gagal di-load. Berguna kalau misalnya kamu benerin
        /// typo path pas development terus mau dicoba lagi tanpa perlu restart game/reload mod.
        /// </summary>
        public static void ClearFailedCache() => _failed.Clear();
    }
}
