using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Tiles;

namespace TheSanity.Items.OreBar.Regilia
{
    public class ReligiaBar : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 25;
        }

        public override void SetDefaults() {
            Item.width = 30;
            Item.height = 22;
            Item.maxStack = 9999;
            Item.value = Item.sellPrice(gold: 1, silver: 50);
            Item.rare = ItemRarityID.Pink;

            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.autoReuse = true;
            Item.consumable = true;

            Item.createTile = ModContent.TileType<ReligiaBarTile>();
        }

        public override void AddRecipes() {
            // Sesuaikan rasio & tile crafting-nya kalau perlu (mis. furnace khusus)
            CreateRecipe(1)
                .AddIngredient<ReligiaOre>(4)
                .AddTile(TileID.Furnaces)
                .Register();
        }
    }
}