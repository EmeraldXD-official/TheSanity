using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items // Sesuai folder Content/Items kamu
{
    public class WhaleWhiteRelic : ModItem
    {
        // Sesuaikan dengan nama file .png relic kamu di folder tersebut
        // Jika gambarnya bernama WhaleWhiteRelic.png, gunakan path di bawah ini:
        public override string Texture => "TheSanity/Items/WhaleWhiteRelicItem"; 

        public override void SetDefaults() {
            // Menghubungkan ke WhaleWhiteRelicTile yang berada di namespace yang sama
            Item.DefaultToPlaceableTile(ModContent.TileType<WhaleWhiteRelicTile>());
            
            Item.width = 48;
            Item.height = 48;
            Item.maxStack = Item.CommonMaxStack;
            Item.rare = ItemRarityID.Master;
            Item.master = true; 
            Item.value = Item.buyPrice(0, 5, 0, 0); 
        }
    }
}