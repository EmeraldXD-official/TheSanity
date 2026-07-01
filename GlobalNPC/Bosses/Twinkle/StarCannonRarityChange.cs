using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // 1. PENGUBAHAN RARITY (GLOBAL ITEM)
    // =========================================================================
    public class StarCannonRarityChange : GlobalItem
    {
        public override void SetDefaults(Item item) {
            
            // Cek apakah item yang sedang dimuat adalah Star Cannon, Jester's Arrow, atau Fallen Star
            if (item.type == ItemID.StarCannon || item.type == ItemID.JestersArrow || item.type == ItemID.FallenStar) {
                
                // Ubah rarity ketiganya menjadi TwinkleRarity kustom milikmu
                item.rare = ModContent.RarityType<CostumeRarity.TwinkleRarity>();
                
            }
        }
    }

    // =========================================================================
    // 2. SISTEM NONAKTIFKAN CRAFTING (MOD SYSTEM)
    // =========================================================================
    public class DisableCustomRecipes : ModSystem
    {
        // PostAddRecipes berjalan otomatis setelah semua resep (vanilla & mod lain) selesai dimuat
        public override void PostAddRecipes() {
            
            // Lakukan perulangan di seluruh resep yang terdaftar di dalam game
            for (int i = 0; i < Recipe.numRecipes; i++) {
                Recipe recipe = Main.recipe[i];

                // Cek jika hasil akhir (output) dari resep tersebut adalah Star Cannon atau Jester's Arrow
                if (recipe.createItem.type == ItemID.StarCannon || recipe.createItem.type == ItemID.JestersArrow) {
                    
                    // Matikan resepnya secara total agar tidak bisa dibuat oleh player
                    recipe.DisableRecipe();
                    
                }
            }
        }
    }
}