using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoMinion;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    public partial class PlutoHead
    {
        // ==================================================================================
        // PATTERN 6 (V3): PROBE SWARM
        // PlutoProbe sekarang PERSISTEN -- dia GAK ilang lagi cuma karena Pluto ganti pattern
        // (lihat PlutoProbe.cs, safety-kill-nya sekarang cuma jalan kalau Pluto-nya beneran udah
        // gak ada). Konsekuensinya, pattern ini SEKARANG CUMA "NOMBOKIN" jumlah probe yang KURANG
        // dari target (5 per player) -- KALAU pas Pluto masuk pattern ini ternyata player udah
        // punya 5 probe idup (nyisa dari giliran sebelumnya), Pluto GAK NGELUARIN PROBE BARU SAMA
        // SEKALI di giliran ini -- efeknya persis kayak pattern NormalDash biasa (cuma dash, gak
        // ada spawn tambahan).
        //
        // Gerakan Pluto sendiri tetap reuse dash biasa dari NormalDash.cs (lihat
        // ExecuteDashPattern). Pattern ini tetap punya DURASI TETAP ~20 detik buat GILIRAN
        // SERANGAN Pluto-nya (bukan buat umur si probe -- probe-nya sendiri tetap idup terus
        // walau giliran ini abis).
        // ==================================================================================

        // 🛑 [LOKASI BALANCING JUMLAH TARGET PROBE PER PLAYER]
        private const int ProbeCountPerPlayer = 5;

        // 🛑 [LOKASI BALANCING DURASI GILIRAN SERANGAN INI] dalam tick (60 tick = 1 detik)
        private const int ProbeSwarmPatternDuration = 1200; // ~20 detik

        // 🛑 [LOKASI BALANCING JEDA NOMBOKIN PROBE YANG KURANG] dalam tick
        private const int ProbeReplacementDelay = 60; // 1 detik

        private void ExecuteProbeSwarmPattern(Player player) {
            // Gerakan Pluto -- tetap reuse dash biasa (lihat NormalDash.cs).
            ExecuteDashPattern(player);

            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            probeSwarmPatternTimer++;
            if (probeSwarmPatternTimer >= ProbeSwarmPatternDuration) {
                // 🛑 [GILIRAN SERANGAN HABIS] Paksa balik ke pattern biasa. Probe yang masih idup
                // TETAP IDUP -- gak ada kill manual di sini, sesuai request (persisten).
                probeSwarmPatternTimer = 0;
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.ai[2] = 0f;
                NPC.ai[3] = 0f;
                return;
            }

            // 🛑 [NOMBOKIN PROBE YANG KURANG] Dicek tiap player -- kalau jumlah probe idupnya
            // udah PAS 5 (misal nyisa dari giliran sebelumnya), gak ada yang di-spawn -- kek
            // pattern NormalDash biasa. Kalau kurang, hitung mundur 1 detik lalu nombokin
            // SEKALIGUS sejumlah yang kurang itu.
            for (int p = 0; p < Main.maxPlayers; p++) {
                Player target = Main.player[p];
                if (!target.active || target.dead) { probeMissingDelayTimer[p] = 0; continue; }

                int alive = CountAliveProbesForPlayer(p);
                int missing = ProbeCountPerPlayer - alive;

                if (missing <= 0) { probeMissingDelayTimer[p] = 0; continue; }

                probeMissingDelayTimer[p]++;
                if (probeMissingDelayTimer[p] >= ProbeReplacementDelay) {
                    probeMissingDelayTimer[p] = 0;
                    SpawnProbesForPlayer(target, missing);
                }
            }
        }

        // ======================================================================
        // HELPER - itung berapa PlutoProbe yang masih idup & lagi nargetin player tertentu.
        // ======================================================================
        private int CountAliveProbesForPlayer(int playerIndex) {
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == ModContent.NPCType<PlutoProbe>() && (int)npc.ai[2] == playerIndex) {
                    count++;
                }
            }
            return count;
        }

        // ======================================================================
        // HELPER - munculin `count` PlutoProbe baru buat 1 player. Tiap probe di-mulai dari fase
        // Cooldown dengan timer awal di-random dikit -- biar probe yang baru nongol gak langsung
        // nembak bebarengan sama probe lain yang udah lama idup (staggering alami).
        // ======================================================================
        private void SpawnProbesForPlayer(Player target, int count) {
            for (int i = 0; i < count; i++) {
                Vector2 spawnOffset = Main.rand.NextVector2Circular(24f, 24f);
                Vector2 spawnPos = NPC.Center + spawnOffset;

                // 🛑 Kira-kira nyamain rentang CooldownDuration punya PlutoProbe.cs biar stagger-nya
                // pas -- kalau nanti CooldownDuration di sana diubah, angka 70 di bawah ini
                // sebaiknya disesuain juga (sengaja gak di-share konstanta lintas file biar dua
                // file ini tetap independen).
                float staggeredCooldownTimer = Main.rand.Next(0, 70);

                NPC.NewNPC(
                    NPC.GetSource_FromAI(),
                    (int)spawnPos.X,
                    (int)spawnPos.Y,
                    ModContent.NPCType<PlutoProbe>(),
                    ai0: 2f,                       // fase awal: Cooldown (biar staggered, bukan langsung Fire bareng-bareng)
                    ai1: staggeredCooldownTimer,   // timer awal di-random di dalam rentang cooldown
                    ai2: target.whoAmI,            // player yang jadi target
                    ai3: 0f                        // gak dipakai lagi (gerakan sekarang bebas, gak orbit)
                );
            }
        }
    }
}
