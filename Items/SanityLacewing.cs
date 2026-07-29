using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class SanityLacewing : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.EmpressButterfly; // Ambil sprite asli vanilla (EmpressButterfly)

        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Red;
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.UseSound = SoundID.Item44;
            Item.consumable = false; // Unconsumable!
        }

        // Syarat pemakaian: Hanya cek apakah Empress of Light sudah ada atau belum (Bisa di semua bioma!)
        public override bool CanUseItem(Player player) {
            return !NPC.AnyNPCs(NPCID.HallowBoss);
        }

        // Eksekusi spawn boss saat diklik kiri
        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                NPC.SpawnOnPlayer(player.whoAmI, NPCID.HallowBoss);
            }
            return true;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            TooltipLine unconsumableLine = new TooltipLine(Mod, "UnconsumableText", "[c/00FF7F:Unconsumable] (Sanity Rework)");
            TooltipLine descLine = new TooltipLine(Mod, "SummonDesc", "[c/FF4500:Summons Empress of Light anywhere]");
            tooltips.Add(unconsumableLine);
            tooltips.Add(descLine);
        }

        // Resep Crafting (Bolak-balik)
        public override void AddRecipes() {
            // Convert dari Original -> Kustom (By Hand)
            CreateRecipe()
                .AddIngredient(ItemID.EmpressButterfly, 1)
                .Register();

            // Convert balik dari Kustom -> Original (By Hand)
            Recipe.Create(ItemID.EmpressButterfly)
                .AddIngredient(Type, 1)
                .Register();
        }
    }
}