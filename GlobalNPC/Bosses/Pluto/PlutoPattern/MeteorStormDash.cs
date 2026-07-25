using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    // =========================================================================
    // 🛑 [PATTERN 8 - METEOR STORM DASH] Cuma boleh KE-ROLL kalau HP Pluto udah
    // <= 50% (lihat CanRollMeteorShowerPattern di bawah). Gerakan dash-nya SAMA
    // PERSIS kayak Pattern 1 (ExecuteDashPattern di NormalDash.cs) -- ancang-ancang
    // 30 tick lalu meluncur lurus ke arah player, diulang sebanyak maxDashes (5-8x,
    // reuse convention "5-9x" yang sama kayak pattern 1/2/3).
    //
    // Bedanya: PAS setiap dash mulai meluncur, Pluto nyebar 3-5 PlutoMeteor di atas
    // player, lalu selama dash berlangsung nembakin PlutoMeteorNuke (versi
    // RedMiniNuke yang ngincer METEOR, bukan player -- lihat PlutoMeteorNuke.cs)
    // satu-satu ke tiap meteor yang barusan disebar, satu nuke = satu target
    // meteor, ngga ada yang dobel/rebutan target.
    //
    // Wiring yang perlu ditambahin manual ke PlutoHead.cs (AI() utama):
    //   1. Perluas pool pattern acak jadi ikutan nyertain pattern 8 KALAU HP <= 50%.
    //   2. Tambahin blok init "else if (NPC.ai[0] == 8f) { maxDashes = ...; ResetMeteorShowerState(); }"
    //   3. Tambahin dispatch "else if (NPC.ai[0] == 8f) { ExecuteMeteorShowerPattern(player); }"
    // Lihat contoh diff-nya di bagian bawah file ini (komentar).
    // =========================================================================
    public partial class PlutoHead
    {
        // Maksimal slot target meteor per siklus dash (dibatasin 5 karena batch per
        // siklus emang cuma 3-5 meteor -- lihat SpawnMeteorBatch).
        private const int MeteorShowerMaxMeteors = 5;

        // Nyimpen IDENTITY (Projectile.identity) tiap meteor yang barusan disebar giliran ini,
        // BUKAN index posisi di Main.projectile -- identity ini yang dipakai buat matching di
        // PlutoMeteorNuke.cs biar tetep valid walau urutan array projectile geser-geser tiap tick.
        // -1 = slot kosong/belum kepake.
        private int[] meteorTargetIdentity = new int[MeteorShowerMaxMeteors];

        // Berapa banyak slot di atas yang valid buat SIKLUS DASH SEKARANG (3-5).
        private int meteorActiveCount = 0;

        // Progress nembakin nuke pengincar meteor: udah nembak sampai index ke berapa.
        private int meteorNukeFireIndex = 0;

        // Jeda antar tembakan nuke pengincar meteor (biar nembaknya beruntun rapi,
        // ga numpuk sekaligus dalam 1 tick yang sama).
        private int meteorNukeFireDelay = 0;

        // 🛑 [SYARAT UNLOCK] Pattern ini CUMA boleh masuk pool pattern acak kalau HP Pluto
        // udah turun ke setengah (atau kurang). Dipanggil dari pattern-picker di PlutoHead.cs.
        private bool CanRollMeteorShowerPattern => NPC.life <= NPC.lifeMax / 2;

        // 🛑 [PEMBUKA PHASE 2] hasEnteredPhase2 cuma keganti true SEKALI seumur hidup si NPC
        // (begitu HP pertama kali nyentuh 50% ke bawah). phase2MeteorOpenerPending ikutan keganti
        // true bareng itu, lalu di-consume (di-set balik false) pas pattern-picker MAKSA Pattern 8
        // jalan duluan -- lihat CheckMeteorPhase2Transition() & pemakaiannya di PlutoHead.cs.
        private bool hasEnteredPhase2 = false;
        private bool phase2MeteorOpenerPending = false;

        // Dipanggil TIAP TICK dari AI() utama PlutoHead, independen dari pattern apapun yang
        // lagi jalan sekarang -- biar begitu HP nembus 50%, momennya kecatet PERSIS saat itu
        // juga, walau attack yang lagi jalan belum tentu abis di tick yang sama. Pattern 8 baru
        // BENERAN jalan nanti pas attack yang jalan sekarang selesai & pattern-picker ngecek
        // flag ini (lihat blok NPC.ai[0]==0f di PlutoHead.cs).
        private void CheckMeteorPhase2Transition() {
            if (!hasEnteredPhase2 && NPC.life <= NPC.lifeMax / 2) {
                hasEnteredPhase2 = true;
                phase2MeteorOpenerPending = true;
            }
        }

        // Dipanggil dari blok init pattern (NPC.ai[0] == 8f pas baru ke-roll) buat
        // ngebersihin sisa state batch meteor giliran sebelumnya.
        private void ResetMeteorShowerState() {
            meteorActiveCount = 0;
            meteorNukeFireIndex = 0;
            meteorNukeFireDelay = 0;
            for (int i = 0; i < MeteorShowerMaxMeteors; i++) meteorTargetIdentity[i] = -1;
        }

        private void ExecuteMeteorShowerPattern(Player player) {
            int stage = (int)NPC.ai[1];
            int timer = (int)NPC.ai[2];

            if (stage == 0) {
                // Stage 0 (ancang-ancang) -- SAMA PERSIS kayak ExecuteDashPattern punya
                // NormalDash.cs, sengaja disalin bukan dipanggil langsung biar pattern ini
                // tetep independen/gampang di-tweak sendiri ke depannya tanpa resiko
                // ngerusak Pattern 1.
                NPC.velocity *= 0.82f;
                Vector2 targetDir = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                if (targetDir != Vector2.Zero) {
                    NPC.rotation = NPC.rotation.AngleLerp(targetDir.ToRotation(), 0.15f);
                }

                timer++;
                if (timer >= 30) {
                    NPC.ai[1] = 1f;
                    NPC.ai[2] = 0f;

                    Vector2 dashDir = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                    float distanceToPlayer = Vector2.Distance(NPC.Center, player.Center);

                    float totalDashDistance = distanceToPlayer + 3200f;
                    float dashSpeed = 44f;

                    NPC.velocity = dashDir * dashSpeed;
                    NPC.rotation = dashDir.ToRotation();
                    dashDuration = totalDashDistance / dashSpeed;

                    if (dashDuration > 150f) dashDuration = 150f;

                    int soundNum = Main.rand.Next(1, 3);
                    SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{soundNum}"), NPC.Center);

                    // 🛑 [SEBAR METEOR] Batch meteor baru disebar PAS dash-nya mulai meluncur
                    // (bukan pas lagi ancang-ancang), biar keliatan Pluto "manggil" meteor
                    // bareng sama dash-nya, bukan sebelumnya.
                    SpawnMeteorBatch(player);

                    NPC.ai[3]++;
                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
            else if (stage == 1) {
                NPC.rotation = NPC.velocity.ToRotation();

                UpdateMeteorNukeFireWave();

                timer++;
                if (timer >= (int)dashDuration) {
                    NPC.ai[2] = 0f;

                    if ((int)NPC.ai[3] >= maxDashes) {
                        NPC.ai[0] = 0f;
                        NPC.ai[1] = 0f;
                        NPC.ai[3] = 0f;
                    } else {
                        NPC.ai[1] = 0f;
                    }
                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
        }

        // Nyebar 3-5 PlutoMeteor secara acak di atas kepala player, lalu nyimpen identity
        // masing-masing biar bisa di-assign satu-satu ke PlutoMeteorNuke yang bakal
        // ditembakin selama dash berlangsung (lihat UpdateMeteorNukeFireWave).
        private void SpawnMeteorBatch(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int count = Main.rand.Next(3, 6); // 3, 4, atau 5 meteor
            meteorActiveCount = count;
            meteorNukeFireIndex = 0;
            meteorNukeFireDelay = 0;

            for (int i = 0; i < MeteorShowerMaxMeteors; i++) meteorTargetIdentity[i] = -1;

            for (int i = 0; i < count; i++) {
                float offsetX = Main.rand.Next(-500, 501);
                // 🛑 Jarak spawn dinaikin dikit (900-1300 -> 1000-1400) nyesuaiin meteor yang
                // sekarang lebih gede/tinggi (350px hitbox), biar tetep ada ruang jatuh yang
                // cukup sebelum nyampe ke player & ngga langsung "muncul dadakan" pas kegedean.
                float spawnY = player.Center.Y - Main.rand.Next(1000, 1400);
                Vector2 spawnPos = new Vector2(player.Center.X + offsetX, spawnY);

                // 🛑 [ARAH JATUH BERVARIASI] Sekitar separuh meteor jatuh nyaris lurus (tilt kecil,
                // 0-8 derajat), separuh lagi jatuh MIRING DIAGONAL jelas (25-45 derajat ke kiri
                // atau kanan secara acak) -- biar polanya ga monoton lurus melulu tiap giliran.
                float tiltDegrees = Main.rand.NextBool(2)
                    ? Main.rand.NextFloat(25f, 45f)   // diagonal jelas
                    : Main.rand.NextFloat(0f, 8f);     // nyaris lurus
                float tiltSign = Main.rand.NextBool() ? 1f : -1f;
                float fallAngle = MathHelper.PiOver2 + MathHelper.ToRadians(tiltDegrees) * tiltSign;

                // 🛑 [KECEPATAN "SEDANG-SEDANG SAJA"] Jauh lebih lambat dari dash Pluto (44f),
                // tapi juga ngga selambat dust jatuh biasa -- disengajain medium biar berasa
                // mengancam tapi masih kelihatan & bisa dihindari duluan.
                float fallSpeed = Main.rand.NextFloat(6.5f, 8f);
                Vector2 fallVelocity = fallAngle.ToRotationVector2() * fallSpeed;

                int proj = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    spawnPos,
                    fallVelocity,
                    ModContent.ProjectileType<PlutoMeteor>(),
                    NPC.damage / 4,
                    0f,
                    Main.myPlayer
                );

                if (proj != Main.maxProjectiles) {
                    meteorTargetIdentity[i] = Main.projectile[proj].identity;
                }
            }
        }

        // Nembakin satu-satu PlutoMeteorNuke buat tiap meteor di batch sekarang, dikasih
        // jeda dikit antar tembakan (bukan sekaligus bareng semua) biar keliatan beruntun/rapi.
        // Setiap nuke dikasih identity target yang BEDA-BEDA (index array berbeda per meteor),
        // jadi ngga mungkin ada 2 nuke rebutan 1 meteor yang sama.
        private void UpdateMeteorNukeFireWave() {
            if (meteorNukeFireIndex >= meteorActiveCount) return; // semua nuke buat batch ini udah ditembak

            meteorNukeFireDelay++;
            if (meteorNukeFireDelay < 6) return;
            meteorNukeFireDelay = 0;

            int targetIdentity = meteorTargetIdentity[meteorNukeFireIndex];
            meteorNukeFireIndex++;

            if (targetIdentity < 0) return; // slot kosong (meteornya gagal ke-spawn), skip aja
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Projectile targetMeteor = FindActiveMeteorByIdentity(targetIdentity);
            if (targetMeteor == null) return; // meteornya udah ilang duluan, ga usah nembak nuke kosongan

            Vector2 launchPos = GetRandomLaunchPointFromSegments();
            Vector2 aimDir = (targetMeteor.Center - launchPos).SafeNormalize(-Vector2.UnitY);
            float launchSpeed = 15f; // 🛑 [DENGAN CEPAT] lebih cepet dari RedMiniNuke biasa (9.5f)

            int nukeIdx = Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                launchPos,
                aimDir * launchSpeed,
                ModContent.ProjectileType<PlutoMeteorNuke>(),
                NPC.damage / 3,
                0f,
                Main.myPlayer
            );

            if (nukeIdx != Main.maxProjectiles) {
                Main.projectile[nukeIdx].ai[0] = launchSpeed;
                Main.projectile[nukeIdx].ai[1] = targetIdentity;
                Main.projectile[nukeIdx].netUpdate = true;
            }
        }

        // Kumpulin semua body/tail milik Head ini yang lagi aktif, terus pilih SATU titik
        // acak (reservoir sampling) buat jadi titik tembak nuke -- gaya visual sama kayak
        // proj wave punya NukeDash.cs (nuke keluar dari badan Pluto, bukan cuma dari kepala).
        private Vector2 GetRandomLaunchPointFromSegments() {
            Vector2 chosen = NPC.Center; // fallback kalau ga ketemu segment aktif sama sekali
            int foundCount = 0;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC pot = Main.npc[i];
                if (pot.active && pot.ai[3] == NPC.whoAmI &&
                   (pot.type == ModContent.NPCType<PlutoBody>() || pot.type == ModContent.NPCType<PlutoTail>())) {
                    foundCount++;
                    if (Main.rand.NextBool(foundCount)) {
                        chosen = pot.Center;
                    }
                }
            }

            return chosen;
        }

        private static Projectile FindActiveMeteorByIdentity(int identity) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == ModContent.ProjectileType<PlutoMeteor>() && p.identity == identity) {
                    return p;
                }
            }
            return null;
        }
    }

    // =========================================================================
    // 📋 WIRING KE PlutoHead.cs (AI() utama) -- SUDAH DITERAPKAN di file PlutoHead.cs
    // yang di-share bareng file ini. Ringkasannya, 4 titik yang disentuh:
    //
    // 1) Panggil CheckMeteorPhase2Transition() TIAP TICK, taruh persis setelah validasi
    //    player aktif/nggak-dead, SEBELUM blok invincibility & blok "if (!initialized)":
    //      CheckMeteorPhase2Transition();
    //
    // 2) Blok pattern-picker (NPC.ai[0]==0f) -- ganti pengambilan nextPattern jadi cek
    //    phase2MeteorOpenerPending DULU sebelum random roll biasa:
    //      int nextPattern;
    //      if (phase2MeteorOpenerPending) {
    //          nextPattern = 8;
    //          phase2MeteorOpenerPending = false;
    //      } else {
    //          int patternPoolMax = CanRollMeteorShowerPattern ? 9 : 8;
    //          nextPattern = Main.rand.Next(1, patternPoolMax);
    //      }
    //      NPC.ai[0] = nextPattern;
    //
    // 3) Blok init pattern baru (sejajar "else if (NPC.ai[0] == 7f) { ... }"):
    //      else if (NPC.ai[0] == 8f) {
    //          maxDashes = Main.rand.Next(5, 9);
    //          ResetMeteorShowerState();
    //      }
    //
    // 4) Dispatch baru (sejajar "else if (NPC.ai[0] == 7f) { ExecuteCrystalDivePattern(player); }"):
    //      else if (NPC.ai[0] == 8f) {
    //          ExecuteMeteorShowerPattern(player);
    //      }
    // =========================================================================
}
