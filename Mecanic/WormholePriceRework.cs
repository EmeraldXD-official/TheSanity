using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Mecanic
{
    // Menggunakan GlobalItem agar bisa memodifikasi properti item vanilla Terraria
    public class WormholePriceRework : GlobalItem
    {
        // =========================================================================
        // ITEM PRICE BALANCING LOCATION
        // =========================================================================
        public override void SetDefaults(Item item)
        {
            // Deteksi secara spesifik jika item tersebut adalah Wormhole Potion
            if (item.type == ItemID.WormholePotion)
            {
                // Mengubah harga dasar item menjadi 0 tembaga (Copper Coined)
                // Di Terraria, value 0 otomatis membuat item tidak memiliki harga jual/beli (No Value)
                item.value = 0;
            }
        }
    }
}