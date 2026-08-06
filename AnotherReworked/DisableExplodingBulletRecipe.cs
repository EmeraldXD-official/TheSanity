using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class DisableExplodingBulletRecipe : ModSystem
    {
        public override void PostAddRecipes()
        {
            for (int i = 0; i < Recipe.numRecipes; i++)
            {
                Recipe recipe = Main.recipe[i];

                // Cari resep yang menghasilkan Exploding Bullet lalu nonaktifkan
                if (recipe.createItem != null && recipe.createItem.type == ItemID.ExplodingBullet)
                {
                    recipe.DisableRecipe();
                }
            }
        }
    }
}