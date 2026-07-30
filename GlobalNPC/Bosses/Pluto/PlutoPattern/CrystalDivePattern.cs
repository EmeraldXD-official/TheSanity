using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    // =====================================================================================
    // 🛑 [PATTERN BARU - CRYSTAL DIVE] Mirip NukeDash (aim lalu dash), tapi target dash-nya
    // BUKAN nembus tepat ke player -- Pluto menukik ke salah satu dari 8 ARAH ACAK relatif ke
    // player (kanan / kiri / atas / bawah / DAN 4 diagonal), sejauh 100 block (1600px) dari
    // player -- SESUAI REQUEST, biar manuvernya variatif (vertikal/horizontal/diagonal) dan
    // areanya lebih luas & lebih susah di-dodge. SELAMA Pluto lagi menukik (Stage Dash), tiap
    // 0,25 detik SEMUA segmen body & tail DICEK satu-satu -- CUMA segmen yang BENERAN kedeteksi
    // player dalam radius 100 block (lihat CrystalDiveSpawnRadius) yang nembakin sepasang
    // RedCrystal (kiri-kanan badan). SESUAI REQUEST ini buat motong lag (dulu semua segmen
    // nembak terus tiap interval walau jauh dari player). Nembak-nya BERHENTI TOTAL begitu
    // Stage Dash-nya selesai. Arah tiap RedCrystal itu SENDIRI FIX ke SAMPING BADAN Pluto
    // (kiri/kanan segmen si penembak), BUKAN ke arah player -- ini dikunci pas spawn dan gak
    // pernah berubah (gak homing sama sekali). RedCrystal itu sendiri yang nanti nampilin garis
    // aim, nembak RedBeam (laser), lalu dash fisik TANPA lifetime (SESUAI REQUEST, cuma dibatasi
    // Projectile.timeLeft bawaan sebagai jaring pengaman) -- lihat RedCrystal.cs & RedBeam.cs.
    //
    // 🛑 [GERBANG "LASER WALL" ala Devourer of God] Begitu durasi dash abis, Pluto GAK LANGSUNG
    // dash lagi -- dia masuk Stage 2 (WaitForCrystals) dan NUNGGU sampai SEMUA RedCrystal yang
    // udah dia keluarkan di dash ini beneran masuk state Dash/"meluncur" sendiri (lihat
    // AllSpawnedCrystalsAreDashing()). Baru setelah itu semua kelar, Pluto boleh dash lagi.
    //
    // Struktur stage (0 = aim, 1 = dash, 2 = wait-for-crystals) sengaja dibikin mirip
    // ExecuteTrickDashPattern biar konsisten sama pattern lain, TAPI pakai field timer sendiri
    // (crystalSpawnDelayTimer) biar ga bentrok sama state pattern lain yang reuse
    // projSequenceActive dkk.
    //
    // 🛠️ INTEGRASI KE PlutoHead.cs (WAJIB, manual -- lihat instruksi lengkap di chat):
    //   1. Gacha pattern: Main.rand.Next(1, 7)  ->  Main.rand.Next(1, 8)
    //   2. Tambah case maxDashes buat pattern 7 (sejajar pattern lain di dalam blok `if
    //      (NPC.ai[0] == 0f)`):
    //         else if (NPC.ai[0] == 7f) {
    //             maxDashes = Main.rand.Next(4, 9); // minimal 4x, maksimal 8x dash -- SESUAI REQUEST
    //             crystalSpawnDelayTimer = 0;
    //         }
    //   3. Tambah dispatch di AI():
    //         else if (NPC.ai[0] == 7f) {
    //             ExecuteCrystalDivePattern(player);
    //         }
    // =====================================================================================
    public partial class PlutoHead
    {
        // 🛑 [FIX LAG - PER-SEGMEN] SESUAI REQUEST: radius deteksi player dari TIAP SEGMEN
        // (bukan dari Head doang) -- segmen cuma nembak sepasang crystal kalau player BENERAN
        // ada dalam radius ini dari segmen tsb. Ini yang motong lag-nya: dulu SEMUA segmen
        // nembak terus tiap 0,25 detik gak peduli separo badan Pluto ada yang jauh banget dari
        // player, jadi banyak crystal numpuk sia-sia di tempat yang gak kena siapa-siapa.
        private const float CrystalDiveSpawnRadius = 100f * 16f; // 100 block (1600px)
        private const int CrystalSpawnIntervalTicks = 15; // 0,25 detik @60 tick/detik -- interval antar
                                                            // "gelombang" cek+tembak per segmen yang lolos radius
        private const float CrystalSideOffsetSpeed = 6f;  // kecepatan awal crystal "meleset" ke samping badan

        // 🛑 [JARAK DASH] SESUAI REQUEST: titik tujuan dash sekarang 100 block (1600px) dari
        // player (dulu cuma 450px / ~28 block), biar area "wall"-nya lebih luas & lebih susah
        // di-dodge player.
        private const float DiveSideOffsetDistance = 100f * 16f;

        private const int StageAim = 0;
        private const int StageDash = 1;
        private const int StageWaitForCrystals = 2; // nunggu semua crystal masuk state Dash/"meluncur"

        private int crystalSpawnDelayTimer = 0;

        private void ExecuteCrystalDivePattern(Player player) {
            int stage = (int)NPC.ai[1];
            int timer = (int)NPC.ai[2];

            if (stage == StageAim) {
                // --- STAGE 0: AIM (gayanya identik NukeDash Stage 0) ---
                NPC.velocity *= 0.82f;
                Vector2 targetDir = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                if (targetDir != Vector2.Zero) {
                    NPC.rotation = NPC.rotation.AngleLerp(targetDir.ToRotation(), 0.15f);
                }

                timer++;
                if (timer >= 30) {
                    NPC.ai[1] = StageDash;
                    NPC.ai[2] = 0f;

                    // 🛑 BEDA UTAMA sama Trick Dash: sisi tujuannya BUKAN cuma kiri/kanan, tapi
                    // acak salah satu dari 8 arah (kanan, kiri, atas, bawah, DAN 4 diagonal)
                    // relatif ke player -- SESUAI REQUEST, sekarang termasuk diagonal biar
                    // manuvernya lebih variatif (vertikal/horizontal/diagonal acak).
                    Vector2 sideOffsetDir;
                    int sideRoll = Main.rand.Next(8);
                    switch (sideRoll) {
                        case 0: sideOffsetDir = new Vector2(1f, 0f); break;                          // kanan
                        case 1: sideOffsetDir = new Vector2(-1f, 0f); break;                         // kiri
                        case 2: sideOffsetDir = new Vector2(0f, -1f); break;                         // atas
                        case 3: sideOffsetDir = new Vector2(0f, 1f); break;                          // bawah
                        case 4: sideOffsetDir = Vector2.Normalize(new Vector2(1f, -1f)); break;       // diagonal kanan-atas
                        case 5: sideOffsetDir = Vector2.Normalize(new Vector2(-1f, -1f)); break;      // diagonal kiri-atas
                        case 6: sideOffsetDir = Vector2.Normalize(new Vector2(1f, 1f)); break;        // diagonal kanan-bawah
                        default: sideOffsetDir = Vector2.Normalize(new Vector2(-1f, 1f)); break;      // diagonal kiri-bawah
                    }

                    Vector2 diveTargetPos = player.Center + sideOffsetDir * DiveSideOffsetDistance;
                    Vector2 dashDir = (diveTargetPos - NPC.Center).SafeNormalize(Vector2.Zero);
                    float distanceToTarget = Vector2.Distance(NPC.Center, diveTargetPos);

                    float totalDashDistance = distanceToTarget + 3200f;
                    float dashSpeed = 44f;

                    NPC.velocity = dashDir * dashSpeed;
                    NPC.rotation = dashDir.ToRotation();
                    dashDuration = totalDashDistance / dashSpeed;
                    if (dashDuration > 150f) dashDuration = 150f;

                    int soundNum = Main.rand.Next(1, 3);
                    SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{soundNum}"), NPC.Center);

                    crystalSpawnDelayTimer = 0;
                    NPC.ai[3]++;
                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
            else if (stage == StageDash) {
                // --- STAGE 1: MENUKIK, ngecek tiap interval & nembak dari segmen yang KEDETEKSI
                // player dalam radius (laser wall, tapi cuma di segmen yang relevan) ---
                NPC.rotation = NPC.velocity.ToRotation();

                crystalSpawnDelayTimer++;
                if (crystalSpawnDelayTimer >= CrystalSpawnIntervalTicks) {
                    crystalSpawnDelayTimer = 0;
                    SpawnCrystalsFromAllSegments(player);
                }

                timer++;
                if (timer >= (int)dashDuration) {
                    // 🛑 [GERBANG LASER WALL] Durasi dash abis -- TAPI Pluto gak langsung dash
                    // lagi. Masuk dulu ke Stage WaitForCrystals sampai semua RedCrystal yang
                    // udah disemburkan di dash ini beneran "meluncur" (Stage Dash) sendiri.
                    NPC.velocity *= 0.9f;
                    NPC.ai[1] = StageWaitForCrystals;
                    NPC.ai[2] = 0f;
                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
            else if (stage == StageWaitForCrystals) {
                // --- STAGE 2: TUNGGU sampai SEMUA crystal yang Pluto keluarkan udah "meluncur" ---
                NPC.velocity *= 0.9f;
                NPC.rotation = NPC.velocity.LengthSquared() > 0.01f ? NPC.velocity.ToRotation() : NPC.rotation;

                if (AllSpawnedCrystalsAreDashing()) {
                    if ((int)NPC.ai[3] >= maxDashes) {
                        NPC.ai[0] = 0f;
                        NPC.ai[1] = StageAim;
                        NPC.ai[3] = 0f;
                    }
                    else {
                        NPC.ai[1] = StageAim;
                    }
                    NPC.ai[2] = 0f;
                    NPC.netUpdate = true;
                }
            }
        }

        // Ngecek apakah SEMUA RedCrystal yang di-spawn NPC (Head) ini masih hidup UDAH masuk
        // Stage Dash ("meluncur")-nya sendiri. Crystal yang sudah mati/despawn otomatis gak
        // dihitung lagi (jadi gak bakal nge-lock nunggu selamanya kalau ada yang ke-Kill duluan,
        // misal karena target-nya disconnect).
        private bool AllSpawnedCrystalsAreDashing() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Terraria.Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != ModContent.ProjectileType<RedCrystal>()) continue;
                if (proj.ModProjectile is RedCrystal rc && rc.OwnerNPCWhoAmI == NPC.whoAmI) {
                    if (!rc.IsDashing) return false;
                }
            }
            return true;
        }

        // Nembakin sepasang RedCrystal (kiri & kanan) dari segmen body/tail yang masih hidup,
        // punya Head ini, DAN kedeteksi player dalam radius CrystalDiveSpawnRadius dari segmen
        // itu sendiri -- SESUAI REQUEST, ini yang motong lag (dulu SEMUA segmen nembak terus
        // walau jauh dari player).
        private void SpawnCrystalsFromAllSegments(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC pot = Main.npc[i];
                if (!pot.active || pot.ai[3] != NPC.whoAmI) continue;
                if (pot.type != ModContent.NPCType<PlutoBody>() && pot.type != ModContent.NPCType<PlutoTail>()) continue;

                // 🛑 [FIX LAG] Cuma lanjut nembak kalau player BENERAN ada dalam radius 100 block
                // dari segmen INI (bukan dari Head Pluto secara keseluruhan) -- segmen yang jauh
                // dari player di-skip total, gak nembak apa-apa.
                if (Vector2.Distance(pot.Center, player.Center) > CrystalDiveSpawnRadius) continue;

                // Arah "samping badan" buat nentuin kiri/kanan spawn dihitung tegak lurus
                // (perpendicular) dari rotasi segmen itu SENDIRI (bukan rotasi Head), supaya
                // crystal-nya nyebar ngikutin lekuk badan Pluto, bukan cuma 1 garis lurus.
                Vector2 segForward = pot.rotation.ToRotationVector2();
                Vector2 segLeft = segForward.RotatedBy(-MathHelper.PiOver2);
                Vector2 segRight = segForward.RotatedBy(MathHelper.PiOver2);

                SpawnSingleCrystal(pot.Center, segLeft, player);
                SpawnSingleCrystal(pot.Center, segRight, player);
            }
        }

        private void SpawnSingleCrystal(Vector2 spawnPos, Vector2 outwardDir, Player player) {
            // 🛑 Spawn-nya sengaja dikasih velocity awal ke arah `outwardDir` (bukan langsung diem
            // di tempat), jadi visualnya crystal "meleset dikit menjauh dari badan" dulu baru
            // pelan² berhenti (diredam sendiri di RedCrystal.AI() Stage Emerge).
            Vector2 initialVel = outwardDir * CrystalSideOffsetSpeed;

            int idx = Terraria.Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                spawnPos,
                initialVel,
                ModContent.ProjectileType<RedCrystal>(),
                NPC.damage / 5,
                0f,
                Main.myPlayer
            );

            if (idx != Main.maxProjectiles) {
                if (Main.projectile[idx].ModProjectile is RedCrystal modProj) {
                    // 🛑 SESUAI REQUEST: arah dikirim FIX sebagai `outwardDir` (samping badan
                    // Pluto), BUKAN player.whoAmI doang buat dihitung ke arah player. `NPC.whoAmI`
                    // dikirim juga biar Pluto bisa ngecek nanti apakah crystal ini udah "meluncur".
                    modProj.InitTarget(player.whoAmI, outwardDir, NPC.whoAmI);
                }
            }
        }
    }
}
