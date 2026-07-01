using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class SanityClothierDoll : ModItem
    {
        // Ambil sprite dari item ID 1281 (Clothier Voodoo Doll)
        public override string Texture => "Terraria/Images/Item_" + 1307; 

        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 28;
            Item.maxStack = 1;
            Item.rare = ItemRarityID.Green;
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.UseSound = SoundID.Item44;
            Item.consumable = false; // Unconsumable! (Tidak akan berkurang)
        }

        // Syarat pemakaian: Hanya cek apakah Skeletron sudah ada atau belum (Bisa di semua bioma & layer!)
        public override bool CanUseItem(Player player) {
            return !NPC.AnyNPCs(NPCID.SkeletronHead);
        }

        // Eksekusi spawn boss saat diklik kiri
        public override bool? UseItem(Player player) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                // Spawn Skeletron tepat 400 pixel di atas posisi player (Bypass batasan malam/siang & bioma)
                int npcIndex = NPC.NewNPC(player.GetSource_ItemUse(Item), (int)player.position.X, (int)player.position.Y - 400, NPCID.SkeletronHead);
                
                if (npcIndex < Main.maxNPCs) {
                    Main.npc[npcIndex].target = player.whoAmI; // Paksa target mengunci ke player
                    
                    // Sinkronisasi data ke multiplayer agar tidak desync
                    if (Main.netMode == NetmodeID.Server) {
                        NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
                    }
                }
            }
            return true;
        }

        public override void ModifyTooltips(System.Collections.Generic.List<TooltipLine> tooltips) {
            TooltipLine unconsumableLine = new TooltipLine(Mod, "UnconsumableText", "[c/00FF7F:Unconsumable] (Sanity Rework)");
            TooltipLine descLine = new TooltipLine(Mod, "SummonDesc", "[c/FF4500:Summons Skeletron anywhere]");
            tooltips.Add(unconsumableLine);
            tooltips.Add(descLine);
        }

        // KOSONG / TANPA AddRecipes() karena item ini TIDAK BISA DI-CRAFT!
    }
}