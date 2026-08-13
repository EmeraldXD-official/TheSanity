using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.OreBar.Ambarium
{
    public class AmbariumOre : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 100; // Mode Journey
        }

        public override void SetDefaults()
        {
            Item.width = 16;
            Item.height = 16;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.sellPrice(0, 0, 2, 50); // Harga: 2 Silver 50 Copper
            Item.rare = ItemRarityID.Blue;

            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.autoReuse = true;
            Item.consumable = true;

            // Menghubungkan item ini ke tile yang dipasang di dunia
            Item.createTile = ModContent.TileType<Tiles.AmbariumOreTile>();
        }
    }
}