using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoBackground
{
    // 🛑 [LOKASI AKTIVASI BACKGROUND PLUTO] Sengaja dipisah dari PlutoHead.cs (yang udah panjang
    // & partial-split ke banyak file lain) biar visual sky ini gampang di-maintain sendiri.
    // Logikanya simpel: selama ada PlutoHead yang masih hidup di map, sky-nya nyala (fade in);
    // begitu bossnya kelar/kabur, sky-nya mati (fade out) -- fade-nya sendiri diurus di
    // PlutoBackgroundSky.Update(), di sini cuma toggle Activate/Deactivate doang.
    public class PlutoBackgroundSystem : ModSystem
    {
        public const string SkyKey = "TheSanity:PlutoBackgroundSky";

        public override void Load() {
            if (Main.dedServ) return; // sky itu murni visual client, server ga perlu daftarin ini
            SkyManager.Instance[SkyKey] = new PlutoBackgroundSky();
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) return;

            // 🛑 [SINKRON SAMA ROAR - PATTERN 9] Dulu sky nyala begitu ada PlutoHead aktif di map
            // (dari tick 1 dia ke-spawn). SEKARANG baru nyala begitu PlutoHead-nya udah ngelewatin
            // momen roar di Spawn Animation (HasTriggeredBackgroundReveal, lihat PlutoSpawnDash.cs)
            // -- SESUAI REQUEST, biar background "muncul" barengan roar, bukan duluan pas boss baru
            // keluar dari luar layar.
            bool shouldReveal = false;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == ModContent.NPCType<PlutoHead>() && npc.ModNPC is PlutoHead head && head.HasTriggeredBackgroundReveal) {
                    shouldReveal = true;
                    break;
                }
            }

            if (shouldReveal) {
                SkyManager.Instance.Activate(SkyKey, Main.LocalPlayer.Center);
            }
            else {
                SkyManager.Instance.Deactivate(SkyKey);
            }
        }
    }
}
