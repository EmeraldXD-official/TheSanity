using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class CoalBarRecipeSystem : ModSystem
    {
        // Menyimpan daftar resep Bar yang membutuhkan Coal
        private static List<Recipe> coalBarRecipes = new List<Recipe>();

        public override void PostAddRecipes()
        {
            coalBarRecipes.Clear();

            for (int i = 0; i < Recipe.numRecipes; i++)
            {
                Recipe recipe = Main.recipe[i];

                if (recipe.createItem == null || recipe.createItem.IsAir)
                    continue;

                int itemType = recipe.createItem.type;

                // Blacklist item
                if (itemType == ItemID.LunarBar || 
                    itemType == ItemID.HellstoneBar || 
                    itemType == ItemID.ShroomiteBar || 
                    itemType == ItemID.Bar)
                {
                    continue;
                }

                // Pengecekan nama item "Bar" & bahan baku "Ore"
                if (recipe.createItem.Name.EndsWith("Bar") || recipe.createItem.Name.Contains(" Bar"))
                {
                    bool hasOreIngredient = false;
                    foreach (Item ingredient in recipe.requiredItem)
                    {
                        if (!ingredient.IsAir && ingredient.Name.Contains("Ore"))
                        {
                            hasOreIngredient = true;
                            break;
                        }
                    }

                    if (hasOreIngredient)
                    {
                        // Menambahkan 1 Coal sebagai standar awal
                        recipe.AddIngredient(ItemID.Coal, 1);
                        
                        // Menyimpan resep ke daftar untuk diacak saat masuk world
                        coalBarRecipes.Add(recipe);
                    }
                }
            }
        }

        public override void OnWorldLoad()
        {
            // Menggunakan Seed Dunia agar acakannya unik dan konsisten di dunia tersebut
            Random worldRand = new Random(Main.ActiveWorldFileData.Seed);
            
            // Menentukan angka acak dari 1 sampai 5
            int randomCoalAmount = worldRand.Next(1, 6);

            // Mengubah jumlah (stack) Coal di semua resep Bar sesuai angka acak world
            foreach (Recipe recipe in coalBarRecipes)
            {
                foreach (Item ingredient in recipe.requiredItem)
                {
                    if (ingredient.type == ItemID.Coal)
                    {
                        ingredient.stack = randomCoalAmount;
                    }
                }
            }
        }
    }
}