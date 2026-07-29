using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class MechanicalCamera : ModItem
    {
        public override void SetDefaults() {
            Item.width = 30;
            Item.height = 30;
            Item.value = Item.sellPrice(0, 10, 0, 0);
            Item.rare = ModContent.RarityType<CostumeRarity.SanityRarity>(); 
            Item.accessory = true;
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            // Imunitas Debuff
            player.buffImmune[BuffID.CursedInferno] = true;
            player.buffImmune[BuffID.Ichor] = true;

            // Stat Tambahan
            player.statDefense += 15;
            player.endurance += 0.05f; // 5% DR

            // Mengaktifkan efek kustom di ModPlayer (termasuk KB Resist via Hook)
            player.GetModPlayer<CameraPlayer>().HasCameraAccessory = true;
        }

        public override void AddRecipes() {
            // Resep Pembuatan
            CreateRecipe()
                .AddIngredient(ItemID.MechanicalWheelPiece, 1)
                .AddIngredient(ItemID.MechanicalWagonPiece, 1)
                .AddIngredient(ItemID.MechanicalBatteryPiece, 1)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();

            // Resep Konversi dari MinecartMech ke MechanicalCamera
            Recipe.Create(Type)
                .AddIngredient(ItemID.MinecartMech, 1)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }

    public class MinecartMechConversion : ModSystem
    {
        public override void AddRecipes() {
            Recipe.Create(ItemID.MinecartMech)
                .AddIngredient(ModContent.ItemType<MechanicalCamera>(), 1)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }
}