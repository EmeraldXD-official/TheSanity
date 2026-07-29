using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.OreBar.Ambarium
{
    public class AmbariumBar : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 25; // Mode Journey
        }

        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 24;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.sellPrice(0, 0, 12, 0); // Harga: 12 Silver
            Item.rare = ItemRarityID.Blue;

            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.autoReuse = true;
            Item.consumable = true;

            // Bisa ditaruh di atas tanah sebagai pajangan
            Item.createTile = ModContent.TileType<Tiles.AmbariumBarTile>();
        }

        // --- RESEP PELEBURAN (FURNACE RECIPE) ---
        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<AmbariumOre>(4) // 4 Ambarium Ore
                .AddTile(TileID.Furnaces)      // Membutuhkan Furnace
                .Register();
        }
    }
}