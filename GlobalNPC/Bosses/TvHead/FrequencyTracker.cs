using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.TvHead
{
    public class FrequencyTracker : ModItem
    {
        // Memakai sprite Radar vanilla (Item_3084)
        public override string Texture => "TheSanity/GlobalNPC/Bosses/TvHead/Radar1"; 

        public override void SetDefaults() {
            Item.width = 20;
            Item.height = 20;
            Item.rare = ItemRarityID.Blue;
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
        }

        public override bool CanUseItem(Player player) {
            return !NPC.AnyNPCs(ModContent.NPCType<TvHead>());
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                SoundEngine.PlaySound(SoundID.Roar, player.position);
                
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.SpawnOnPlayer(player.whoAmI, ModContent.NPCType<TvHead>());
                }
                else {
                    // Menggunakan angka 51 (ID Jaringan Spawn Boss Terraria)
                    NetMessage.SendData(51, -1, -1, null, player.whoAmI, ModContent.NPCType<TvHead>());
                }
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.IronBar, 5)
                .AddIngredient(3084)
                .AddIngredient(751,50)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}