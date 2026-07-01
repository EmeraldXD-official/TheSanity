using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.ItemDropRules;

namespace TheSanity.Items // Ganti 'YourModName' sesuai dengan nama mod kamu
{
    public class WhiteWhaleBag : ModItem
    {
        public override void SetStaticDefaults() {
            // Memberitahu game bahwa item ini adalah Boss Bag
            ItemID.Sets.BossBag[Type] = true;
            
            // Menentukan efek animasi/cahaya saat bag jatuh di world
            ItemID.Sets.PreHardmodeLikeBossBag[Type] = true; 

            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults() {
            Item.maxStack = Item.CommonMaxStack; // Otomatis 9999 di versi terbaru
            Item.consumable = true;
            Item.width = 24;
            Item.height = 24;
            Item.rare = ItemRarityID.Expert;
            Item.expert = true; // Boss bag hanya aktif di Expert/Master mode
        }

        public override bool CanRightClick() {
            return true;
        }

        public override void ModifyItemLoot(ItemLoot itemLoot) {
            // Isi dari Boss Bag saat klik kanan
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<HaloStaff>()));
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<HaloRune>()));
            // Opsional: Kamu bisa menambahkan koin money drop di sini jika mau
            // itemLoot.Add(ItemDropRule.CoinsGold(5)); 
        }
    }
}