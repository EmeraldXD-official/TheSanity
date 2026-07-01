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
            Item.useTime = 20;
            Item.useAnimation = 20;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.rare = ItemRarityID.Cyan;
            Item.autoReuse = false;
        }

        public override bool AltFunctionUse(Player player) {
            return true; // Izinkan Klik Kanan
        }

        public override bool CanUseItem(Player player) {
            if (player.altFunctionUse == 2) {
                // TUGAS RMB UTAMA: Buka atau Tutup GUI secara instan
                if (Main.netMode != NetmodeID.Server) {
                    ModContent.GetInstance<DebuffUISystem>().ToggleUI();
                }
                return false; // Return false agar tangan player tidak memainkan animasi ayun item saat buka menu
            }
            return true;
        }

        public override bool? UseItem(Player player) {
            if (player.altFunctionUse != 2 && player.whoAmI == Main.myPlayer) {
                Vector2 mousePos = Main.MouseWorld;
                
                // Ambil ID Debuff yang saat ini sedang aktif dipilih pada GUI
                int selectedBuffID = 0;
                if (Main.netMode != NetmodeID.Server) {
                    selectedBuffID = ModContent.GetInstance<DebuffUISystem>().DebuffUI.SelectedBuffID;
                }

                // PROTEKSI MAKSIMAL 1 DUMMY: Jika sudah ada dummy lama di dunia, langsung hapus
                for (int i = 0; i < Main.maxNPCs; i++) {
                    if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<NPCs.DummyTargetNPC>()) {
                        Main.npc[i].active = false; 
                    }
                }

                // Spawn Dummy Baru di posisi kursor mouse
                int npcID = NPC.NewNPC(player.GetSource_ItemUse(Item), (int)mousePos.X, (int)mousePos.Y, ModContent.NPCType<NPCs.DummyTargetNPC>());
                
                if (npcID < Main.maxNPCs) {
                    Main.npc[npcID].ai[0] = selectedBuffID; // Salurkan ID debuff terpilih ke memori dummy
                    Main.npc[npcID].netUpdate = true;
                }
            }
            return true;
        }

        // ==================== SYSTEM CRAFTING RECIPE ====================
        public override void AddRecipes() {
            CreateRecipe()
                .AddRecipeGroup(RecipeGroupID.Wood, 7) // Menggunakan semua jenis kayu (Normal, Boreal, Palm, dll.) sebanyak 7 biji
                .AddIngredient(ItemID.DirtBlock, 5)     // Membutuhkan Dirt Block sebanyak 5 biji
                // Tanpa .AddTile(...) artinya item ini bisa langsung di-craft menggunakan tangan kosong!
                .Register();
        }
    }
}