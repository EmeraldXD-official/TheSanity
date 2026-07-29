using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;

namespace TheSanity.Items
{
    public class DummyTool : ModItem
    {
        public override string Texture => "TheSanity/GlobalNPC/Environment/MegaDummy";

        public override void SetDefaults() {
            Item.width = 32;
            Item.height = 32;
            Item.useTime = 12; // Sedikit dicepatkan agar proses spam spawn terasa mulus
            Item.useAnimation = 12;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.rare = ItemRarityID.Cyan;
            Item.autoReuse = true; // Diaktifkan agar bisa memanggil beruntun saat menahan LMB
        }

        public override bool AltFunctionUse(Player player) {
            return true; // Tetap izinkan Klik Kanan
        }

        public override bool CanUseItem(Player player) {
            // Biarkan mengembalikan nilai true agar animasi pukulan item berjalan normal baik LMB / RMB
            return true;
        }

        public override bool? UseItem(Player player) {
            if (player.whoAmI == Main.myPlayer) {
                
                // ✨ SEKARANG RMB BERFUNGSI SEBAGAI FORCE DESPAWN MASSAL
                if (player.altFunctionUse == 2) {
                    int removedCount = 0;
                    
                    for (int i = 0; i < Main.maxNPCs; i++) {
                        if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<NPCs.DummyTargetNPC>()) {
                            Main.npc[i].active = false; // Lenyapkan dari dunia
                            removedCount++;
                        }
                    }
                    
                    if (removedCount > 0) {
                        Main.NewText($"❌ [Dummy System] Success Clear {removedCount} Dummy!", Color.OrangeRed);
                    }
                } 
                // 🛠️ LMB: MEMANGGIL DUMMY BARU (BISA UNLIMITED / SPAM)
                else {
                    Vector2 mousePos = Main.MouseWorld;
                    int selectedBuffID = 0;
                    
                    if (Main.netMode != NetmodeID.Server) {
                        selectedBuffID = ModContent.GetInstance<DebuffUISystem>().DebuffUI.SelectedBuffID;
                    }

                    // ⚠️ PROTEKSI UNTUK MENGHAPUS DUMMY LAMA SUDAH DIHAPUS TOTAL DI SINI ⚠️

                    // Spawn Dummy Baru langsung di area posisi kursor
                    int npcID = NPC.NewNPC(player.GetSource_ItemUse(Item), (int)mousePos.X, (int)mousePos.Y, ModContent.NPCType<NPCs.DummyTargetNPC>());
                    
                    if (npcID < Main.maxNPCs) {
                        Main.npc[npcID].ai[0] = selectedBuffID; 
                        Main.npc[npcID].netUpdate = true;
                    }
                }
            }
            return true;
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddRecipeGroup(RecipeGroupID.Wood, 7)
                .AddIngredient(ItemID.DirtBlock, 5)
                .Register();
        }
    }
}