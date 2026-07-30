using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    // =========================================================================================
    // 🛑 [KAMERA SPAWN ANIMATION] Cuma nge-handle 1 hal: nambahin offset kamera (ditarik ke atas
    // player) SELAMA Pattern 9 (Spawn Animation) aktif DAN NPC.target Pluto lagi ini player --
    // SESUAI REQUEST, cuma player yang "menspawn"/di-aggro Pluto yang layarnya ketarik, player
    // lain (kalau ada, multiplayer) tetep liat kamera normal mereka masing-masing.
    //
    // Ini murni VISUAL/client-side (ModifyScreenPosition cuma jalan buat nge-render, ga
    // ngubah posisi Player.Center beneran), jadi aman dipakai di multiplayer -- tiap client
    // ngitung offset-nya sendiri berdasarkan state ai[]/target NPC Pluto yang emang udah
    // network-synced otomatis kayak field NPC bawaan lainnya.
    // =========================================================================================
    public class PlutoSpawnCameraPlayer : ModPlayer
    {
        public override void ModifyScreenPosition() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != ModContent.NPCType<PlutoHead>()) continue;
                if (npc.ModNPC is not PlutoHead head) continue;
                if (!head.IsSpawnAnimationActive) continue;
                if (npc.target != Player.whoAmI) continue;

                Main.screenPosition += head.SpawnAnimCameraOffset;
            }
        }
    }
}
