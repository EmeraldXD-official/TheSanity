using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.Aetherfin
{
    [AutoloadEquip(EquipType.Legs)]
    public class AetherfinGreaves : ModItem
    {
        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(0, 0, 60, 0);
            Item.rare = ItemRarityID.Blue;
            Item.defense = 4;
        }

        public override void UpdateEquip(Player player) {
            player.moveSpeed += 0.10f; // +10% Movement Speed
            
            // PERBAIKAN: Menggunakan GetAttackSpeed(DamageClass.Summon) untuk Kecepatan Cambuk / Whip Speed
            player.GetAttackSpeed(DamageClass.Summon) += 0.10f; 
            
            player.ignoreWater = true; // Bergerak bebas di air
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<BossDrop.BrokenTv>(), 5)
                .AddIngredient(2500)
                .AddIngredient(2321,10)
                .AddIngredient(1017)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}