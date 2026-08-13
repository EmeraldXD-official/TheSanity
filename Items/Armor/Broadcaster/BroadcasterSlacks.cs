using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.Broadcaster
{
    [AutoloadEquip(EquipType.Legs)]
    public class BroadcasterSlacks : ModItem
    {
        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(0, 0, 60, 0);
            Item.rare = ItemRarityID.Blue;
            Item.defense = 3;
        }

        public override void UpdateEquip(Player player) {
            player.moveSpeed += 0.08f; // +8% Move speed
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<BossDrop.BrokenTv>(), 5)
                .AddIngredient(5063)
                .AddIngredient(1017)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}