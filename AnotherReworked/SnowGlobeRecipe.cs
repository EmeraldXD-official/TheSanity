using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // Menggunakan ModSystem karena kita hanya menambahkan data ke sistem game, bukan membuat item baru
    public class SnowGlobeRecipe : ModSystem
    {
        // =========================================================================
        // [RECIPE SYSTEM]: RESEP MANDIRI UNTUK SNOW GLOBE VANILLA
        // =========================================================================
        public override void AddRecipes()
        {
            // Membuat resep baru dengan hasil akhir (output) berupa item Snow Globe vanilla
            Recipe recipe = Recipe.Create(ItemID.SnowGlobe);

            // =========================================================================
            // [GUIDE & BALANCING LOKASI: BAHAN & KONDISI CRAFTING]
            // =========================================================================
            recipe.AddIngredient(ItemID.SnowBlock, 20);      // Membutuhkan 20 Snow Block
            recipe.AddRecipeGroup(RecipeGroupID.Wood, 15);   // Membutuhkan 15 Kayu apa saja (Any Wood)
            
            recipe.AddTile(TileID.Anvils);                   // Dibuat di Iron Anvil / Lead Anvil biasa
            recipe.AddCondition(Condition.Hardmode);         // Dikunci dan hanya bisa dibuat saat Hardmode
            // =========================================================================

            // Daftarkan resep secara resmi ke dalam database game
            recipe.Register();
        }
    }
}