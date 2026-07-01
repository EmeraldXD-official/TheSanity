using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures; 
using Microsoft.Xna.Framework;
using TheSanity.GlobalNPC.Bosses.WhiteWhale;

namespace TheSanity.Items
{
    public class FluegelWhaleBone : ModItem
    {
        public override void SetStaticDefaults() {
            Item.ResearchUnlockCount = 1;
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(6, 4));
            ItemID.Sets.AnimatesAsSoul[Item.type] = true;
        }

        public override void SetDefaults() {
            Item.width = 40;
            Item.height = 42;
            Item.useAnimation = 45;
            Item.useTime = 45;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.rare = ItemRarityID.Red;
            Item.consumable = true;
        }

        // PEMBARUAN: Batasi penggunaan item dan tampilkan pesan kesalahan jika digunakan saat siang hari
        public override bool CanUseItem(Player player) {
            int bossType = ModContent.NPCType<WhiteWhaleBoss>();
            
            // Jika sudah ada boss yang aktif, langsung gagalkan tanpa pesan teks
            if (NPC.AnyNPCs(bossType)) {
                return false;
            }

            // Jika dipanggil saat siang hari, tampilkan pesan peringatan lalu gagalkan
            if (Main.dayTime) {
                if (player.whoAmI == Main.myPlayer) {
                    Main.NewText("The White Whale only slumbers during the day... Try again at night.", Color.DeepSkyBlue);
                }
                return false;
            }

            return true;
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                int bossType = ModContent.NPCType<WhiteWhaleBoss>();
                
                // Spawn boss agak jauh di atas langit
                NPC.SpawnOnPlayer(player.whoAmI, bossType);
                SoundEngine.PlaySound(SoundID.Roar, player.position); 
            }
            return true;
        }

        // TAMBAHAN: Resep pembuatan item (Crafting Recipe)
        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.EmpressButterfly, 1)
                .AddIngredient(ItemID.BoneWand, 10)
                .AddIngredient(ItemID.LightShard, 3)
                .AddTile(TileID.DemonAltar) // Ini otomatis mencakup Crimson Altar di tModLoader
                .Register();
        }
    }
}