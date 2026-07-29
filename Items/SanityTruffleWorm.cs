using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class SanityTruffleWorm : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.TruffleWorm; // Ambil sprite asli vanilla

        public override void SetDefaults() {
            Item.width = 22;
            Item.height = 18;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Yellow;
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.UseSound = SoundID.Item44;
            Item.consumable = false; // Unconsumable!
        }

        // Syarat pemakaian: Hanya cek apakah Duke Fishron sudah ada atau belum (Bisa di semua bioma!)
        public override bool CanUseItem(Player player) {
            return !NPC.AnyNPCs(NPCID.DukeFishron);
        }

        // Eksekusi spawn boss saat diklik kiri
        public override bool? UseItem(Player player) {
            // Memastikan kode eksekusi berjalan dengan benar di Singleplayer maupun Multiplayer Server
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                // Spawn Duke Fishron tepat 400 pixel di atas posisi player
                int npcIndex = NPC.NewNPC(player.GetSource_ItemUse(Item), (int)player.position.X, (int)player.position.Y - 400, NPCID.DukeFishron);
                
                if (npcIndex < Main.maxNPCs) {
                    Main.npc[npcIndex].target = player.whoAmI; // Paksa targetnya langsung mengunci ke player
                    
                    // Sinkronisasi data ke multiplayer agar tidak terjadi desync di server
                    if (Main.netMode == NetmodeID.Server) {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
                    }
                }
            }
            return true;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            TooltipLine unconsumableLine = new TooltipLine(Mod, "UnconsumableText", "[c/00FF7F:Unconsumable] (Sanity Rework)");
            TooltipLine descLine = new TooltipLine(Mod, "SummonDesc", "[c/FF4500:Summons Duke Fishron anywhere]");
            tooltips.Add(unconsumableLine);
            tooltips.Add(descLine);
        }

        // Resep Crafting (Bolak-balik)
        public override void AddRecipes() {
            // Convert dari Original -> Kustom (By Hand)
            CreateRecipe()
                .AddIngredient(ItemID.TruffleWorm, 1)
                .Register();

            // Convert balik dari Kustom -> Original (By Hand)
            Recipe.Create(ItemID.TruffleWorm)
                .AddIngredient(Type, 1)
                .Register();
        }
    }
}