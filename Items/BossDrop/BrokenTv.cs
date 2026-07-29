using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.BossDrop
{
    public class BrokenTv : ModItem
    {
        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 9999;             // Standar max stack tModLoader 1.4.4
            Item.value = Item.sellPrice(0, 0, 20, 0); // Harga jual: 20 Silver
            Item.rare = ItemRarityID.Blue;     // Rarity Biru (Pre-Hardmode Boss Drop)
        }
    }
}