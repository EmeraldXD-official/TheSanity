using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures; // WAJIB DIKASIH: Untuk memanggil DrawAnimationVertical

namespace TheSanity.Items
{
    public class DangerSense : ModItem
    {
        public override void SetStaticDefaults()
        {
            // === KUNCI PERBAIKAN 1: REGISTER ANIMASI 2 FRAME ===
            // Angka 12 = Kecepatan animasi (ticks per frame). Semakin kecil nilainya, semakin cepat kedipnya.
            // Angka 2 = Jumlah total frame vertikal gambar kamu.
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(12, 2));
        }

        public override void SetDefaults()
        {
            // === KUNCI PERBAIKAN 2: UKURAN EPS FRAME (39 X 47) ===
            Item.width = 39;  // Lebar item
            Item.height = 47; // Tinggi SATU frame item
            
            Item.accessory = true; 
            Item.rare = ItemRarityID.Cyan; 
            Item.value = Item.sellPrice(gold: 5); 
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetModPlayer<Players.DangerSensePlayer>().dangerSenseEquipped = true;
        }
    }
}