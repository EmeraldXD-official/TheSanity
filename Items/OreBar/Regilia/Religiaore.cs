using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Tiles;

namespace TheSanity.Items.OreBar.Regilia
{
    public class ReligiaOre : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 25;
        }

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 20;
            Item.maxStack = 9999;
            Item.value = Item.sellPrice(silver: 50);
            Item.rare = ItemRarityID.Pink;

            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTurn = true;
            Item.useAnimation = 15;
            Item.useTime = 10;
            Item.autoReuse = true;
            Item.consumable = true;

            Item.createTile = ModContent.TileType<ReligiaOreTile>();
        }
    }
}