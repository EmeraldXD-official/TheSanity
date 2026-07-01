using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TheSanity
{
    // ==========================================
    // 1. SYSTEM PENYIMPANAN PROGRESS BOSS TWINKLE
    // ==========================================
    public class TwinkleDownedSystem : ModSystem
    {
        public static bool downedTwinkle = false;

        public override void ClearWorld() {
            downedTwinkle = false;
        }

        public override void SaveWorldData(TagCompound tag) {
            if (downedTwinkle) {
                tag["downedTwinkle"] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag) {
            downedTwinkle = tag.ContainsKey("downedTwinkle");
        }
    }

    // ==========================================
    // 2. SYSTEM LOCK RECIPE (STARS & METEORITE)
    // ==========================================
    public class TwinkleRecipeLockSystem : ModSystem
    {
        public override void PostAddRecipes() {
            // Membuat pesan peringatan yang muncul di bawah bahan crafting saat belum unlock
            LocalizedText conditionText = Language.GetOrRegister("Mods.TheSanity.Conditions.KillTwinkle", () => "After Twinkle has Defeated");
            Condition killTwinkleCondition = new Condition(conditionText, () => TwinkleDownedSystem.downedTwinkle);

            foreach (Recipe recipe in Main.recipe) {
                bool requiresLockedIngredient = false;

                foreach (Item ingredient in recipe.requiredItem) {
                    // 1. Cek Fallen Star (Kecuali jika hasilnya adalah Mana Crystal)
                    if (ingredient.type == ItemID.FallenStar && recipe.createItem.type != ItemID.ManaCrystal) {
                        requiresLockedIngredient = true;
                        break;
                    }

                    // 2. Cek Meteorite Ore atau Meteorite Bar
                    if (ingredient.type == ItemID.Meteorite || ingredient.type == ItemID.MeteoriteBar) {
                        requiresLockedIngredient = true;
                        break;
                    }
                }

                // Jika resep membutuhkan salah satu bahan di atas, tambahkan gembok progresi
                if (requiresLockedIngredient) {
                    recipe.AddCondition(killTwinkleCondition);
                }
            }
        }
    }
}