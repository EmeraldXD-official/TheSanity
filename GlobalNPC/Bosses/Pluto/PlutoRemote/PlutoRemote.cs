using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoRemote
{
    // =====================================================================================
    // 🛑 [PLUTO REMOTE] Item KHUSUS MODDING/TESTING (bukan buat player biasa) -- biar gampang
    // nyari & ngetes pattern attack Pluto tanpa nunggu gacha random.
    //   - RMB (klik kanan)  : buka GUI (lihat PlutoRemoteUIState.cs) buat milih 1 dari 7 pattern.
    //   - LMB (klik kiri)   : paksa Pluto (yang paling deket ke player) LANGSUNG masuk pattern
    //                         yang lagi kepilih -- lihat PlutoHead.ForcePattern() di
    //                         PlutoPart/PlutoRemoteForce.cs buat detail tiap pattern-nya.
    //
    // 🛑 [BATASAN] Cuma valid di singleplayer / dari client yang jadi host server. Kalau dipakai
    // dari client yang numpang konek ke dedicated server, LMB-nya gak bakal ngefek (lihat guard
    // Main.netMode di ForcePattern()) -- pesan peringatan ditampilin lewat chat.
    // =====================================================================================
    public class PlutoRemote : ModItem
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoRemote/PlutoMote";

        public override void SetDefaults() {
            Item.width = 28;
            Item.height = 28;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = 15;
            Item.useAnimation = 15;
            Item.useTurn = true;
            Item.noMelee = true;
            Item.noUseGraphic = false;
            Item.autoReuse = false;
            Item.maxStack = 1;
            Item.value = 0;
            Item.rare = ItemRarityID.Cyan;
            Item.UseSound = SoundID.MenuTick;
        }

        // Bikin item ini punya "fungsi alternatif" (klik kanan), sama polanya kayak grappling
        // hook / item lain yang beda perilaku LMB vs RMB.
        public override bool AltFunctionUse(Player player) => true;

        public override bool CanUseItem(Player player) {
            // Cuma player lokal yang boleh trigger GUI/logic-nya (biar gak dobel di multiplayer).
            return player.whoAmI == Main.myPlayer;
        }

        public override bool? UseItem(Player player) {
            if (player.altFunctionUse == 2) {
                // --- RMB: buka/tutup GUI pilih pattern ---
                ModContent.GetInstance<PlutoRemoteSystem>().ToggleGUI();
                return true;
            }

            // --- LMB: paksa Pluto terdekat masuk pattern yang lagi kepilih di GUI ---
            if (Main.netMode == NetmodeID.MultiplayerClient) {
                Main.NewText("PlutoRemote cuma bisa dipakai di singleplayer / dari host server.", Color.OrangeRed);
                return true;
            }

            int selectedPattern = player.GetModPlayer<PlutoRemotePlayer>().SelectedPattern;
            if (selectedPattern <= 0) {
                Main.NewText("Pilih pattern dulu lewat GUI Remote (klik kanan)!", Color.OrangeRed);
                return true;
            }

            NPC targetHead = FindNearestPlutoHead(player);
            if (targetHead == null) {
                Main.NewText("Ga ada Pluto yang aktif di dunia ini!", Color.OrangeRed);
                return true;
            }

            if (targetHead.ModNPC is PlutoHead headMod) {
                headMod.ForcePattern(selectedPattern);
                Main.NewText($"Pluto dipaksa masuk pattern {selectedPattern}!", Color.Yellow);
            }

            return true;
        }

        private static NPC FindNearestPlutoHead(Player player) {
            NPC closest = null;
            float bestDist = float.MaxValue;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != ModContent.NPCType<PlutoHead>()) continue;

                float d = Vector2.Distance(npc.Center, player.Center);
                if (d < bestDist) {
                    bestDist = d;
                    closest = npc;
                }
            }

            return closest;
        }
    }
}
