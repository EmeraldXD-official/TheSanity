using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items; // Memastikan path namespace mengarah ke folder item Soul milikmu

namespace TheSanity
{
    public class MechSpawnRecipeRework : ModSystem
    {
        // =========================================================================
        // [RECIPE REWORK LOCATION]: MENYELIPKAN SOUL OF FORGOTTEN SNOW KE MECH SUMMONS
        // =========================================================================
        public override void PostAddRecipes() 
        {
            // Lakukan perulangan di seluruh database resep Terraria yang sudah terdaftar oleh game
            for (int i = 0; i < Recipe.numRecipes; i++)
            {
                Recipe recipe = Main.recipe[i];

                // Cek apakah resep tersebut menghasilkan salah satu dari 3 item Summon Mech Boss
                if (recipe.createItem.type == ItemID.MechanicalEye || 
                    recipe.createItem.type == ItemID.MechanicalWorm || 
                    recipe.createItem.type == ItemID.MechanicalSkull)
                {
                    // =========================================================================
                    // [GUIDE & BALANCING LOKASI: JUMLAH BAHAN COOLING SYSTEM]
                    // Angka 15 adalah jumlah Soul yang dibutuhkan player untuk merakit mesin boss
                    // =========================================================================
                    recipe.AddIngredient(ModContent.ItemType<SoulofForgottenSnow>(), 15);
                }
            }
        }
    }
}