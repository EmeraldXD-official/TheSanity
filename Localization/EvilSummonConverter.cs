using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Systems
{
    public class ConversionRecipesSystem : ModSystem
    {
        public override void AddRecipes() {
            // 1. RESEP: Bloody Spine -> Worm Food (Tangan Kosong)
            Recipe.Create(ItemID.WormFood)
                .AddIngredient(ItemID.BloodySpine)
                // Tanpa .AddTile() artinya otomatis bisa di-craft pake tangan kosong
                .Register();

            // 2. RESEP: Worm Food -> Bloody Spine (Tangan Kosong)
            Recipe.Create(ItemID.BloodySpine)
                .AddIngredient(ItemID.WormFood)
                // Tanpa .AddTile() artinya otomatis bisa di-craft pake tangan kosong
                .Register();
        }
    }
}