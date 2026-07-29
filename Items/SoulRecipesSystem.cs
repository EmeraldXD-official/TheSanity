using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class SoulRecipesSystem : ModSystem
    {
        // PostAddRecipes berjalan setelah game selesai memuat semua resep bawaan Terraria
        public override void PostAddRecipes()
        {
            // Melakukan perulangan (loop) untuk memeriksa seluruh resep yang terdaftar di dalam game
            for (int i = 0; i < Recipe.numRecipes; i++)
            {
                Recipe recipe = Main.recipe[i];

                // Memeriksa apakah hasil kerajinan (createItem) dari resep tersebut cocok dengan daftar armor yang kamu minta
                if (recipe.createItem.type == ItemID.ShroomiteHeadgear ||
                    recipe.createItem.type == ItemID.ShroomiteMask ||
                    recipe.createItem.type == ItemID.ShroomiteHelmet ||
                    recipe.createItem.type == ItemID.ShroomiteBreastplate ||
                    recipe.createItem.type == ItemID.ShroomiteLeggings ||
                    recipe.createItem.type == ItemID.SpectreMask ||
                    recipe.createItem.type == ItemID.SpectreHood ||
                    recipe.createItem.type == ItemID.SpectreRobe ||
                    recipe.createItem.type == ItemID.SpectrePants ||
                    recipe.createItem.type == ItemID.SpookyHelmet ||
                    recipe.createItem.type == ItemID.SpookyBreastplate ||
                    recipe.createItem.type == ItemID.SpookyLeggings)
                {
                    // =========================================================================
                    // LOKASI PENGATURAN JUMLAH BAHAN (BALANCE DI SINI)
                    // =========================================================================
                    // Mengambil ID dari item Soul of the Sun milikmu
                    int bahanSoul = ModContent.ItemType<SoulOfTheSun>();
                    
                    // Angka '5' di bawah ini adalah jumlah Soul of the Sun yang dibutuhkan.
                    // Kamu bisa mengubah angka ini jika ingin menambah atau mengurangi demi keseimbangan (balancing).
                    int jumlahDibutuhkan = 5; 

                    // Menyisipkan Soul of the Sun ke dalam resep asli item tersebut
                    recipe.AddIngredient(bahanSoul, jumlahDibutuhkan);
                }
            }
        }
    }
}