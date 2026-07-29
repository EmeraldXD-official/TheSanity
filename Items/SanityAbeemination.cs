using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class SanityAbeemination : ModItem
    {
        // Ambil sprite asli vanilla milik Abeemination (Item ID 1133)
        public override string Texture => "Terraria/Images/Item_" + ItemID.Abeemination; 

        public override void SetDefaults() {
            Item.width = 24;
            Item.height = 24;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Orange; // Warna kelangkaan jingga (sesuai item aslinya)
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.UseSound = SoundID.Item44;
            Item.consumable = false; // Unconsumable! (Tidak akan berkurang)
        }

        // Syarat pemakaian: Hanya cek apakah Queen Bee sudah ada atau belum (Bisa di semua bioma & layer!)
        public override bool CanUseItem(Player player) {
            return !NPC.AnyNPCs(NPCID.QueenBee);
        }

        // Eksekusi spawn boss saat diklik kiri dari tangan
        public override bool? UseItem(Player player) {
            // Memastikan kode eksekusi berjalan dengan benar di Singleplayer maupun Multiplayer Server
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                
                // Spawn Queen Bee tepat 400 pixel di atas posisi player (Bypass semua batasan bioma)
                int npcIndex = NPC.NewNPC(player.GetSource_ItemUse(Item), (int)player.position.X, (int)player.position.Y - 400, NPCID.QueenBee);
                
                if (npcIndex < Main.maxNPCs) {
                    Main.npc[npcIndex].target = player.whoAmI; // Paksa targetnya langsung mengunci ke player kamu
                    
                    // Sinkronisasi data ke multiplayer agar tidak desync di server
                    if (Main.netMode == NetmodeID.Server) {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
                    }
                }
            }
            return true;
        }

        // Kustomisasi teks Tooltip info item
        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            TooltipLine unconsumableLine = new TooltipLine(Mod, "UnconsumableText", "[c/00FF7F:Unconsumable] (Sanity Rework)");
            TooltipLine descLine = new TooltipLine(Mod, "SummonDesc", "[c/FF4500:Summons Queen Bee anywhere]");
            tooltips.Add(unconsumableLine);
            tooltips.Add(descLine);
        }

        // Resep Crafting Bolak-balik (By Hand)
        public override void AddRecipes() {
            // Convert dari Original Abeemination -> Sanity Abeemination
            CreateRecipe()
                .AddIngredient(ItemID.Abeemination, 1)
                .Register();

            // Convert balik dari Sanity Abeemination -> Original Abeemination
            Recipe.Create(ItemID.Abeemination)
                .AddIngredient(Type, 1)
                .Register();
        }
    }
}