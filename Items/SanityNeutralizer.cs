using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class SanityNeutralizer : ModItem
    {
        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 26;
            Item.useStyle = ItemUseStyleID.DrinkLiquid;
            Item.useAnimation = 17;
            Item.useTime = 17;
            Item.useTurn = true;
            Item.UseSound = SoundID.Item3;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.rare = ItemRarityID.Orange;
            Item.value = Item.sellPrice(0, 0, 50, 0);
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                var modPlayer = player.GetModPlayer<SanityPlayer>();
                // Aktifkan proses penetralan
                modPlayer.isNeutralizing = true; 
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe(10) // Menghasilkan 10
                .AddIngredient(ItemID.DarkShard, 1)
                .AddIngredient(ItemID.LightShard, 1)
                .AddIngredient(ItemID.Bottle, 10) // Sesuai permintaan: 10 Water Bottle
                .AddTile(TileID.Bottles)      // ID 13
                .AddTile(TileID.AlchemyTable)  // ID 355
                .Register();
        }
    }
}