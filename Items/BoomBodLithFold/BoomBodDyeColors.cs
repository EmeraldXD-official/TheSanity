using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;

namespace TheSanity.Items
{
    /// <summary>
    /// Terraria mewarnai dye lewat pixel shader (GameShaders.Armor), bukan lewat satu nilai
    /// RGB sederhana yang bisa dibaca balik secara umum -- banyak dye (rainbow, fire, dst)
    /// bahkan animasinya sendiri di GPU, jadi "warna dye" itu bukan data tunggal yang bisa
    /// diekstrak generik untuk SEMUA dye yang ada.
    ///
    /// Solusi pragmatis di sini: petakan dye-dye SOLID/dasar (hasil Dye Vat, yang dipakai
    /// mayoritas pemain buat kebutuhan "warnain jadi biru/merah/dst") ke warna approksimasinya
    /// secara manual. Dye yang tidak ada di daftar (dye spesial/quest/animasi) fallback ke putih.
    /// Gampang ditambah lagi kalau ternyata ada dye favorit yang belum kecover.
    /// </summary>
    public static class BoomBodDyeColors
    {
        private static readonly Dictionary<int, Color> Map = new Dictionary<int, Color>
        {
            [ItemID.RedDye] = new Color(220, 45, 45),
            [ItemID.OrangeDye] = new Color(235, 130, 35),
            [ItemID.YellowDye] = new Color(235, 205, 45),
            [ItemID.LimeDye] = new Color(150, 225, 45),
            [ItemID.GreenDye] = new Color(45, 205, 65),
            [ItemID.TealDye] = new Color(45, 205, 175),
            [ItemID.CyanDye] = new Color(45, 205, 225),
            [ItemID.SkyBlueDye] = new Color(95, 175, 240),
            [ItemID.BlueDye] = new Color(65, 105, 230),
            [ItemID.PurpleDye] = new Color(145, 65, 225),
            [ItemID.VioletDye] = new Color(175, 75, 225),
            [ItemID.PinkDye] = new Color(235, 125, 195),
            [ItemID.BlackDye] = new Color(55, 55, 60),
            [ItemID.SilverDye] = new Color(205, 205, 215),
            [ItemID.BrownDye] = new Color(125, 85, 45),
        };

        /// <summary>
        /// Ambil warna dari item dye. Null/kosong/tidak dikenali -> putih (default,
        /// sesuai request "garis-garisnya putih" kalau belum di-dye).
        /// </summary>
        public static Color GetColor(Item dyeItem)
        {
            if (dyeItem == null || dyeItem.type <= 0 || dyeItem.stack <= 0)
                return Color.White;

            return Map.TryGetValue(dyeItem.type, out Color color) ? color : Color.White;
        }
    }
}
