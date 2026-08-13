using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.Aetherfin
{
    [AutoloadEquip(EquipType.Body)]
    public class AetherfinChestplate : ModItem
    {
        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(0, 1, 0, 0);
            Item.rare = ItemRarityID.Blue;
            Item.defense = 6;
        }

        public override void UpdateEquip(Player player) {
    player.GetDamage(DamageClass.Summon) += 0.08f; 
     player.maxMinions += 1;    
}

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<BossDrop.BrokenTv>(), 6)
                .AddIngredient(2499)
                .AddIngredient(2311,10)
                .AddIngredient(1017)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}