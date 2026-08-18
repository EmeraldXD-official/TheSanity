using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace YourModName.Content.NPCs
{
    // =========================================================================================
    // 🛑 [KAMERA SPAWN INTRO TWINS] Cuma nge-handle 1 hal: nambahin offset kamera (ditarik ke
    // atas player) SELAMA Spawn Intro cutscene Twins aktif DAN NPC.target Spazmatism lagi ini
    // player -- SESUAI REQUEST/pola yang sama kayak PlutoSpawnCameraPlayer.cs, cuma player yang
    // di-target Twins yang layarnya ketarik, player lain (multiplayer) tetep liat kamera normal.
    //
    // Ini murni VISUAL/client-side (ModifyScreenPosition cuma jalan buat nge-render, ga ngubah
    // posisi Player.Center beneran), jadi aman dipakai di multiplayer -- tiap client ngitung
    // offset-nya sendiri berdasarkan field SpawnIntro* milik TwinsReworkOverride.
    // =========================================================================================
    public class TwinsSpawnCameraPlayer : ModPlayer
    {
        public override void ModifyScreenPosition() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != NPCID.Spazmatism) continue;

                TwinsReworkOverride global = npc.GetGlobalNPC<TwinsReworkOverride>();
                if (global == null || !global.SpawnIntroActive) continue;
                if (npc.target != Player.whoAmI) continue;

                Main.screenPosition += global.SpawnIntroCameraOffset;
            }
        }
    }
}
