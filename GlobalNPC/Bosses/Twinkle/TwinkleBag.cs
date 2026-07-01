using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.ItemDropRules;
using Microsoft.Xna.Framework;

namespace TheSanity.GlobalNPC.Bosses.Twinkle
{
    public class TwinkleBag : ModItem
    {
        // Jalur sprite otomatis mengarah ke file TwinkleBag.png milikmu
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Twinkle/TwinkleBag";

        public override void SetStaticDefaults() {
            // CRITICAL: Menandai item ini sebagai Boss Bag resmi game
            ItemID.Sets.BossBag[Type] = true;
            ItemID.Sets.PreHardmodeLikeBossBag[Type] = true; 

            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults() {
            Item.maxStack = Item.CommonMaxStack; 
            Item.consumable = true;              
            Item.width = 24;                     
            Item.height = 24;
            Item.expert = true;                  
        }

        public override bool CanRightClick() {
            return true;
        }

        // =================================================================
        // SISTEM LOOT / ISI SAAT BAG DIBUKA (BYPASS NAMESPACE VERSION)
        // =================================================================
        public override void ModifyItemLoot(ItemLoot itemLoot) {
            
            // Menggunakan Mod.Find<ModItem>().Type agar tModLoader mencari langsung berdasarkan 
            // nama Class kustom kamu tanpa mempedulikan di folder mana file tersebut disimpan.
            itemLoot.Add(ItemDropRule.OneFromOptions(1, 
                ItemID.StarCannon, 
                Mod.Find<ModItem>("TwinkleTwinkle").Type, 
                Mod.Find<ModItem>("Starmerang").Type, 
                Mod.Find<ModItem>("StarShineStaff").Type
            ));
            
            // Items pendukung yang pasti didapatkan
            itemLoot.Add(ItemDropRule.Common(ItemID.FallenStar, 1, 10, 20));
            itemLoot.Add(ItemDropRule.Common(ItemID.JestersArrow, 1, 19, 30));
            itemLoot.Add(ItemDropRule.Common(ItemID.HealingPotion, 1, 10, 15));
        }
    }
}