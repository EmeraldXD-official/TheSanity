using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Systems
{
    public class RecipeSafetySystem : ModSystem
    {
        // Hook ini berjalan di akhir proses load setelah semua mod mendaftarkan resepnya
        public override void PostAddRecipes() {
            // Lakukan perulangan (loop) di semua resep yang terdaftar di dalam game
            for (int i = 0; i < Recipe.numRecipes; i++) {
                Recipe recipe = Main.recipe[i];

                // KONDISI 1: Jika hasil craft-nya adalah Brain of Confusion
                if (recipe.createItem.type == ItemID.BrainOfConfusion) {
                    // Cek apakah resep tersebut membutuhkan Worm Scarf sebagai bahan
                    if (recipe.HasIngredient(ItemID.WormScarf)) {
                        // Jika iya, matikan resepnya secara paksa!
                        recipe.DisableRecipe();
                    }
                }
                
                // KONDISI 2: Jika hasil craft-nya adalah Worm Scarf
                else if (recipe.createItem.type == ItemID.WormScarf) {
                    // Cek apakah resep tersebut membutuhkan Brain of Confusion sebagai bahan
                    if (recipe.HasIngredient(ItemID.BrainOfConfusion)) {
                        // Jika iya, matikan resepnya secara paksa!
                        recipe.DisableRecipe();
                    }
                }
            }
        }
    }
}