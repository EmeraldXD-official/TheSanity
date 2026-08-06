using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace TheSanity
{
    public class ExplosiveRecipes : ModSystem
    {
        public static RecipeGroup AnyPressurePlateGroup;

        public override void AddRecipeGroups()
        {
            // Membuat grup resep "Any Pressure Plate" dari daftar yang kamu minta
            AnyPressurePlateGroup = new RecipeGroup(() => $"{Language.GetTextValue("LegacyMisc.37")} Pressure Plate",
                ItemID.RedPressurePlate,
                ItemID.GreenPressurePlate,
                ItemID.GrayPressurePlate,
                ItemID.BrownPressurePlate,
                ItemID.BluePressurePlate,
                ItemID.YellowPressurePlate,
                ItemID.LihzahrdPressurePlate,
                ItemID.WeightedPressurePlatePink,
                ItemID.WeightedPressurePlateOrange,
                ItemID.WeightedPressurePlatePurple,
                ItemID.WeightedPressurePlateCyan,
                ItemID.OrangePressurePlate
            );

            RecipeGroup.RegisterGroup("TheSanity:AnyPressurePlate", AnyPressurePlateGroup);
        }

        public override void AddRecipes()
        {
            // 1. Bomb: 5 Coal, 3 Explosive Powder (Anvil)
            Recipe bombRecipe = Recipe.Create(ItemID.Bomb);
            bombRecipe.AddIngredient(ItemID.Coal, 5);
            bombRecipe.AddIngredient(ItemID.ExplosivePowder, 3);
            bombRecipe.AddTile(TileID.Anvils);
            bombRecipe.Register();

            // 2. Dynamite: 8 Coal, 4 Explosive Powder (Anvil)
            Recipe dynamiteRecipe = Recipe.Create(ItemID.Dynamite);
            dynamiteRecipe.AddIngredient(ItemID.Coal, 8);
            dynamiteRecipe.AddIngredient(ItemID.ExplosivePowder, 4);
            dynamiteRecipe.AddTile(TileID.Anvils);
            dynamiteRecipe.Register();

            // 3. Bomb Fish: 1 Bass, 5 Coal, 3 Explosive Powder (Anvil)
            Recipe bombFishRecipe = Recipe.Create(ItemID.BombFish);
            bombFishRecipe.AddIngredient(ItemID.Bass, 1);
            bombFishRecipe.AddIngredient(ItemID.Coal, 5);
            bombFishRecipe.AddIngredient(ItemID.ExplosivePowder, 3);
            bombFishRecipe.AddTile(TileID.Anvils);
            bombFishRecipe.Register();

            // 4. Dry Bomb: 4 Glass, 5 Coal, 3 Explosive Powder (Anvil)
            Recipe dryBombRecipe = Recipe.Create(ItemID.DryBomb);
            dryBombRecipe.AddIngredient(ItemID.Glass, 4);
            dryBombRecipe.AddIngredient(ItemID.Coal, 5);
            dryBombRecipe.AddIngredient(ItemID.ExplosivePowder, 3);
            dryBombRecipe.AddTile(TileID.Anvils);
            dryBombRecipe.Register();

            // 5. Grenade: 2 Coal, 1 Explosive Powder (Anvil)
            Recipe grenadeRecipe = Recipe.Create(ItemID.Grenade);
            grenadeRecipe.AddIngredient(ItemID.Coal, 2);
            grenadeRecipe.AddIngredient(ItemID.ExplosivePowder, 1);
            grenadeRecipe.AddTile(TileID.Anvils);
            grenadeRecipe.Register();

            // 6. Land Mine: 5 Coal, 4 Explosive Powder, 1 Any Pressure Plate (Anvil)
            Recipe landMineRecipe = Recipe.Create(ItemID.LandMine);
            landMineRecipe.AddIngredient(ItemID.Coal, 5);
            landMineRecipe.AddIngredient(ItemID.ExplosivePowder, 4);
            landMineRecipe.AddRecipeGroup(AnyPressurePlateGroup, 1);
            landMineRecipe.AddTile(TileID.Anvils);
            landMineRecipe.Register();
        }

        public override void PostAddRecipes()
        {
            // 7. Scarab Bomb: Menambahkan 5 Coal & 3 Explosive Powder ke resep Scarab Bomb bawaan
            for (int i = 0; i < Recipe.numRecipes; i++)
            {
                Recipe recipe = Main.recipe[i];

                if (recipe.createItem != null && recipe.createItem.type == ItemID.ScarabBomb)
                {
                    recipe.AddIngredient(ItemID.Coal, 5);
                    recipe.AddIngredient(ItemID.ExplosivePowder, 3);
                }
            }
        }
    }
}