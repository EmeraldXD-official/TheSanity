using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.DiscordantReligia;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia
{
    public class HallowedWisle : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;
            Item.value = Item.sellPrice(0, 1, 0, 0);
            Item.rare = ItemRarityID.Yellow;
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
        }

        public override bool CanUseItem(Player player) {
            // Hanya bisa dipakai jika kedua bos belum ada di dunia
            return !NPC.AnyNPCs(ModContent.NPCType<ChronoReligia>()) && !NPC.AnyNPCs(ModContent.NPCType<PlagueReligia>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Roar, player.position);

                int typeA = ModContent.NPCType<ChronoReligia>();
                int typeB = ModContent.NPCType<PlagueReligia>();

                // NPC.SpawnOnPlayer otomatis menangani spawn di Singleplayer dan sync ke Server di Multiplayer
                NPC.SpawnOnPlayer(player.whoAmI, typeA);
                NPC.SpawnOnPlayer(player.whoAmI, typeB);
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.HallowedBar, 10)
                .AddIngredient(ItemID.Ectoplasm, 5)
                .AddIngredient(2218, 3)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}