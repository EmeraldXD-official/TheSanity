using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Placeable
{
    public class TwinkleRelic : ModItem
    {
        public override string Texture => "TheSanity/CostumeTile/TwinkleRelicTop";

        public override void SetDefaults() {
            // Menghubungkan ke namespace Tiles yang memiliki akhiran 's'
            Item.DefaultToPlaceableTile(ModContent.TileType<Tiles.TwinkleRelicTile>());
            
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = Item.CommonMaxStack;
            Item.rare = ItemRarityID.Master;
            Item.master = true; 
            Item.value = Item.buyPrice(0, 5, 0, 0); 
        }
    }
}