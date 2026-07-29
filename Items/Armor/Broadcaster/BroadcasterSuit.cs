using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.Broadcaster
{
    [AutoloadEquip(EquipType.Body)]
    public class BroadcasterSuit : ModItem
    {
        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(0, 1, 0, 0);
            Item.rare = ItemRarityID.Blue;
            Item.defense = 6; // Base Defense Post-EoC
        }

        public override void UpdateEquip(Player player) {
            player.GetCritChance(DamageClass.Generic) += 6f; // +6% Crit Chance
            player.moveSpeed += 0.05f;                       // +5% Movement Speed
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<BossDrop.BrokenTv>(), 6)
                .AddIngredient(5062)
                .AddIngredient(1017)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}