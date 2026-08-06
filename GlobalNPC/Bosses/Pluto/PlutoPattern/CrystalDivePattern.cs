using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    // =====================================================================================
    // 🛑 [PATTERN BARU - CRYSTAL DIVE] Mirip NukeDash (aim lalu dash) -- SESUAI REQUEST, format
    // target dash-nya sekarang PERSIS gaya NukeDash.cs (ExecuteTrickDashPattern): arah ke player
    // dulu (baseDir), diputer tegak lurus (±90°, acak kiri/kanan), jadi Pluto MENDEKAT dulu lalu
    // LEWAT MENYAMPING player (bukan menjauh dari awal), baru abis itu terus menjauh ke sisi situ.
    // Jarak sisinya (DiveSideOffsetDistance) 45 block (720px) -- SESUAI REQUEST, agak lebih jauh
    // dari NukeDash punya (450f) biar areanya kerasa beda, tapi gak se-signifikan 100 block yang
    // sempet dicoba sebelumnya. BEDA UTAMA sama NukeDash: Stage Dash di sini punya peluang 40%
    // lintasannya MELENGKUNG (curvedDiveActive), fitur yang NukeDash sendiri gak punya. Crystal
    // juga nembak di SEPANJANG JEJAK dash tanpa gate radius-ke-player (lihat
    // SpawnCrystalsAlongHeadTrail), beda sama NukeDash yang cuma nembak bomb pas udah deket player.
    //
    // 🛑 [SESUAI REQUEST - SPAWN DARI KEPALA DOANG] Crystal SEKARANG cuma di-spawn dari Head
    // (bukan dari tiap segmen body/tail lagi). SELAMA Pluto lagi menukik (Stage Dash), tiap
    // 0,25 detik kita jalan MUNDUR di sepanjang JEJAK lurus yang udah ditempuh Head sejak titik
    // spawn terakhir, dan nembakin sepasang RedCrystal (kiri-kanan) tiap kelipatan 5 block di
    // sepanjang jejak itu (lihat CrystalTrailSpacing & SpawnCrystalsAlongHeadTrail) -- efeknya
    // masih dapet "wall" yang nyebar sepanjang lintasan dash, tapi tanpa perlu looping semua
    // segmen NPC tiap interval. 🛑 [SESUAI REQUEST] SEKARANG nembak di SEPANJANG JEJAK-nya, GAK
    // ADA lagi radius-ke-player yang nge-skip titik yang jauh -- beda sama NukeDash yang cuma
    // nembak bomb pas Pluto UDAH DEKET player. Nembak-nya BERHENTI TOTAL begitu Stage Dash
    // selesai. Arah tiap RedCrystal itu SENDIRI FIX ke SAMPING jejak dash (kiri/kanan), BUKAN ke
    // arah player -- ini dikunci pas spawn dan gak pernah berubah (gak homing sama sekali).
    // RedCrystal itu sendiri yang nanti nampilin garis aim, nembak RedBeam (laser sprite PNG,
    // hidup 1,5 detik biar animasi membesarnya kelihatan -- lihat RedBeam.cs), lalu dash fisik
    // TANPA lifetime dengan kecepatan yang BUILD UP terus (lihat RedCrystal.cs).
    //
    // 🛑 [SESUAI REQUEST - RETREAT] Selama Stage Aim (siap-siap dash), kalau player kedeketan
    // sama Pluto, Pluto bakal MENJAUH dulu (lihat RetreatTriggerDistance) biar dapet jarak
    // sebelum charge dash-nya kelar, alih-alih cuma diem ngerem di tempat.
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
        // 🛑 [SESUAI REQUEST - DIHAPUS] Dulu ada CrystalDiveSpawnRadius buat nge-skip titik jejak
        // yang jauh dari player (biar hemat performa). SEKARANG dihapus -- crystal SENGAJA nembak
        // di SEPANJANG JEJAK dash tanpa gate radius apapun (lihat SpawnCrystalsAlongHeadTrail),
        // biar beda sama NukeDash yang cuma nembak pas Pluto udah deket player. Total crystal
        // per dash tetap kebatas natural kok, dari panjang dash & CrystalTrailSpacing di bawah,
        // jadi gak perlu jaring pengaman radius lagi.
        private const int CrystalSpawnIntervalTicks = 15; // 0,25 detik @60 tick/detik -- interval antar
                                                            // "gelombang" cek+tembak sepanjang trail Head
        private const float CrystalSideOffsetSpeed = 6f;  // kecepatan awal crystal "meleset" ke samping badan

        // 🛑 [SESUAI REQUEST - SPAWN DARI KEPALA DOANG] Dulu tiap segmen body/tail Pluto ngecek
        // radius sendiri-sendiri terus nembak sepasang crystal kalau kedeteksi player -- sekarang
        // itu DIHAPUS TOTAL. Crystal SEKARANG cuma nongol dari Head, tapi biar efek "wall"-nya
        // tetap lebar (dulu didapat dari nyebar di sepanjang badan Pluto), tiap kali interval
        // kecek, kita jalan MUNDUR sepanjang JEJAK (trail) posisi Head sejak spawn terakhir, dan
        // nembak sepasang crystal tiap kelipatan CrystalTrailSpacing (5 block) di sepanjang jejak
        // itu -- SESUAI REQUEST "gap-nya kegedean/sempit, kasih jarak 5 block". Ini juga otomatis
        // lebih murah dari versi lama karena gak perlu looping Main.maxNPCs buat nyari body/tail
        // tiap interval.
        private const float CrystalTrailSpacing = 5f * 16f; // 5 block (80px) antar titik spawn

        // 🛑 [SESUAI REQUEST - JARAK SISI DIPERBESAR DIKIT DARI NUKEDASH, TAPI GAK SEGEDE
        // SEBELUMNYA] NukeDash pakai 450f. Sebelumnya di sini sempet 1600f (100 block) -- SESUAI
        // REQUEST kekgedean, jadi diturunin ke 720f (45 block): tetep lebih jauh dari NukeDash
        // (biar areanya kerasa beda & lebih lega), tapi gak se-signifikan 1600f yang lama.
        private const float DiveSideOffsetDistance = 45f * 16f;

        // 🛑 [SESUAI REQUEST - RETREAT] Kalau player kedeketan sama Pluto pas dia lagi
        // siap-siap dash (Stage Aim), Pluto bakal MENJAUH dulu (bukan cuma diem ngerem) biar
        // punya jarak buat nge-charge dash-nya. Rotasi tetep ngikutin player (buat telegraph
        // arah aim), cuma pergerakannya yang dibalik jadi retreat.
        private const float RetreatTriggerDistance = 700f; // ~44 block -- di bawah ini Pluto mundur
        private const float RetreatSpeed = 11f;

        private const int StageAim = 0;
        private const int StageDash = 1;
        private const int StageWaitForCrystals = 2; // nunggu semua crystal masuk state Dash/"meluncur"

        // 🛑 [FIX PLUTO DIEM SELAMANYA] Jaring pengaman TERAKHIR buat Stage WaitForCrystals.
        // Normalnya semua RedCrystal yang disemburkan PASTI selesai masuk Stage Dash-nya sendiri
        // dalam waktu terbatas (state machine RedCrystal itu sendiri deterministik & dibatasi
        // timer -- lihat RedCrystal.cs), jadi AllSpawnedCrystalsAreDashing() harusnya balik true
        // gak lebih dari ~2,5 detik sejak crystal PALING BARU disemburkan. TAPI kalau karena sebab
        // apapun (crystal gagal ke-spawn, whoAmI nyasar, atau state lain yang somehow bikin
        // kondisinya gak PERNAH ke-satisfy) Pluto bisa nyangkut diem di Stage ini SELAMANYA --
        // makanya sekarang dikasih batas atas: begitu nunggu kelewat lama, Pluto MAKSA lanjut ke
        // StageAim lagi walau masih ada crystal yang belum "meluncur". Crystal yang ketinggalan
        // tetap hidup & bakal nyelesain state machine-nya sendiri secara independen, cuma gak
        // ditunggu lagi sama Pluto.
        private const int WaitForCrystalsSafetyCap = 240; // ~4 detik, jauh di atas skenario normal

        private int crystalSpawnDelayTimer = 0;

        // 🛑 [SESUAI REQUEST - 40% MELENGKUNG] Pas mulai Stage Dash, ada 40% peluang lintasan
        // menukiknya jadi MELENGKUNG (arc), bukan garis lurus horizontal/diagonal/vertikal biasa
        // -- velocity-nya diputer sedikit demi sedikit tiap tick sepanjang durasi dash (lihat
        // curveTurnRatePerTick di bawah), sisanya (60%) tetap garis lurus seperti biasa.
        private bool curvedDiveActive = false;
        private float curveTurnRatePerTick = 0f; // radian/tick, tanda (+/-) nentuin arah belok

        // 🛑 [STAGGER URUTAN SPAWN] Jarak (tick) antar titik crystal yang ke-spawn DALAM SATU
        // panggilan SpawnCrystalsAlongHeadTrail yang sama (bisa lebih dari 1 pasang kalau Pluto
        // nempuh jarak jauh dalam 1 interval) -- SESUAI REQUEST biar urutan nembak/dash-nya
        // ngalir dari yang paling awal (jejak paling belakang) ke yang paling baru (jejak paling
        // deket Head sekarang), bukan numpuk nembak bareng semua dalam 1 tick yang sama.
        private const int CrystalFireStaggerTicks = 5;

        // 🛑 [TRAIL HEAD] Posisi Head terakhir kali kita nge-spawn titik crystal di sepanjang
        // jejaknya -- di-reset ke NPC.Center pas mulai Stage Dash yang baru.
        private Vector2 lastCrystalTrailPos = Vector2.Zero;

        private void ExecuteCrystalDivePattern(Player player) {
            int stage = (int)NPC.ai[1];
            int timer = (int)NPC.ai[2];

            if (stage == StageAim) {
                // --- STAGE 0: AIM (gayanya identik NukeDash Stage 0) ---
                Vector2 targetDir = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                if (targetDir != Vector2.Zero) {
                    NPC.rotation = NPC.rotation.AngleLerp(targetDir.ToRotation(), 0.15f);
                }

                // 🛑 [SESUAI REQUEST - RETREAT] Kalau player kedeketan pas Pluto lagi charge-up,
                // Pluto mundur menjauh dulu (bukan cuma ngerem diem) biar dapet jarak buat dash --
                // rotasi tetap ngikutin player buat telegraph, cuma pergerakan yang dibalik.
                float distToPlayer = Vector2.Distance(NPC.Center, player.Center);
                if (distToPlayer < RetreatTriggerDistance && targetDir != Vector2.Zero) {
                    Vector2 retreatVel = -targetDir * RetreatSpeed;
                    NPC.velocity = Vector2.Lerp(NPC.velocity, retreatVel, 0.1f);
                }
                else {
                    NPC.velocity *= 0.82f;
                }

                timer++;
                if (timer >= 30) {
                    NPC.ai[1] = StageDash;
                    NPC.ai[2] = 0f;

                    // 🛑 [FORMAT DISAMAIN KE NUKEDASH.CS] SESUAI REQUEST: format target dash-nya
                    // sekarang PERSIS gaya NukeDash.cs punya (ExecuteTrickDashPattern) -- baseDir
                    // ngarah ke player DULU, terus diputer tegak lurus (RotatedBy ±90°, acak kiri
                    // /kanan), BUKAN dihitung dari sisi seberang + sebaran ±70° kayak fix
                    // sebelumnya. Efeknya sama-sama "approach lalu lewat menyamping" (bukan
                    // langsung nembus TENGAH player, tapi tetap MENDEKAT dulu baru menjauh --
                    // karena komponen utama arahnya tetap ke arah player, cuma digeser ke samping
                    // sejauh DiveSideOffsetDistance), persis gaya "Trick Dash". BEDA-nya sama
                    // NukeDash cuma di jarak sisinya -- SESUAI REQUEST dipatok 45 block (720px,
                    // DiveSideOffsetDistance), agak lebih jauh dari NukeDash (450f) biar area
                    // "wall"-nya kerasa beda, tapi gak segede 100 block yang sempet dicoba
                    // sebelumnya. Fitur dash MELENGKUNG (curvedDiveActive di bawah) tetap
                    // eksklusif punya Crystal Dive -- NukeDash sendiri gak punya ini.
                    Vector2 baseDir = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
                    Vector2 sideOffsetDir = baseDir.RotatedBy(Main.rand.NextBool() ? MathHelper.PiOver2 : -MathHelper.PiOver2);

                    Vector2 diveTargetPos = player.Center + sideOffsetDir * DiveSideOffsetDistance;
                    Vector2 dashDir = (diveTargetPos - NPC.Center).SafeNormalize(Vector2.Zero);
                    float distanceToTarget = Vector2.Distance(NPC.Center, diveTargetPos);

                    float totalDashDistance = distanceToTarget + 3200f;
                    float dashSpeed = 44f;

                    NPC.velocity = dashDir * dashSpeed;
                    NPC.rotation = dashDir.ToRotation();
                    dashDuration = totalDashDistance / dashSpeed;
                    if (dashDuration > 150f) dashDuration = 150f;

                    // 🛑 [SESUAI REQUEST - 40% MELENGKUNG] Roll sekali di sini, tiap kali dash
                    // baru dimulai. Kalau kena, velocity bakal diputer pelan-pelan tiap tick di
                    // Stage Dash (lihat di bawah) sepanjang total sudut acak (50-100 derajat) yang
                    // disebar merata ke seluruh durasi dash-nya -- hasilnya lintasan berbentuk
                    // busur/lengkung, bukan garis lurus predictable ke satu arah.
                    curvedDiveActive = Main.rand.NextFloat() < 0.4f;
                    if (curvedDiveActive) {
                        float turnDirSign = Main.rand.Next(2) == 0 ? 1f : -1f;
                        float totalTurnDegrees = Main.rand.NextFloat(50f, 100f);
                        curveTurnRatePerTick = MathHelper.ToRadians(totalTurnDegrees) / dashDuration * turnDirSign;
                    }
                    else {
                        curveTurnRatePerTick = 0f;
                    }

                    int soundNum = Main.rand.Next(1, 3);
                    SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{soundNum}"), NPC.Center);

                    crystalSpawnDelayTimer = 0;
                    lastCrystalTrailPos = NPC.Center; // reset titik awal jejak buat dash yang baru
                    NPC.ai[3]++;
                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
            else if (stage == StageDash) {
                // --- STAGE 1: MENUKIK, ngecek tiap interval & nembak dari titik-titik jejak Head
                // yang KEDETEKSI player dalam radius (laser wall, tapi cuma di titik yang relevan) ---

                // 🛑 [40% MELENGKUNG] Kalau roll-nya kena (lihat StageAim), velocity diputer
                // dikit tiap tick di sini -- rotation Pluto otomatis ngikut karena diambil dari
                // arah velocity SETELAH diputer, jadi visual "moncong ngikutin lengkungan" juga
                // ikut kebawa, bukan cuma posisinya doang yang belok.
                if (curvedDiveActive) {
                    NPC.velocity = NPC.velocity.RotatedBy(curveTurnRatePerTick);
                }
                NPC.rotation = NPC.velocity.ToRotation();

                crystalSpawnDelayTimer++;
                if (crystalSpawnDelayTimer >= CrystalSpawnIntervalTicks) {
                    crystalSpawnDelayTimer = 0;
                    SpawnCrystalsAlongHeadTrail(player);
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

                timer++;

                // 🛑 [FIX PLUTO DIEM SELAMANYA] Lihat komentar WaitForCrystalsSafetyCap di atas --
                // lanjut kalau SEMUA crystal udah dashing SEPERTI BIASA, ATAU kalau nunggu-nya udah
                // kelewat lama (safetyTriggered), yang mana pun duluan.
                bool allDashing = AllSpawnedCrystalsAreDashing();
                bool safetyTriggered = timer >= WaitForCrystalsSafetyCap;

                if (allDashing || safetyTriggered) {
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
                else {
                    NPC.ai[2] = timer;
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

        // 🛑 [SESUAI REQUEST - SPAWN DARI KEPALA DOANG, TIAP 5 BLOCK] Nembakin sepasang
        // RedCrystal (kiri & kanan) dari sepanjang JEJAK Head sejak titik spawn terakhir, tiap
        // kelipatan CrystalTrailSpacing (5 block) -- BUKAN dari body/tail lagi. Karena dash-nya
        // lurus (atau melengkung, lihat curvedDiveActive di Stage Dash), jejak dari
        // lastCrystalTrailPos ke NPC.Center diinterpolasi di sepanjang garis antar-titik itu --
        // gak perlu looping Main.maxNPCs kayak versi lama.
        // 🛑 [SESUAI REQUEST - CRYSTAL NEMBAK SEPANJANG SELURUH JEJAK] Beda sama NukeDash yang
        // cuma nembak bomb kalau Pluto UDAH DEKET player (radius 800px, lihat NukeDash.cs
        // triggeredProjThisDash), Crystal Dive SEKARANG nembak SEPANJANG JEJAK dash dari awal
        // sampai akhir, GAK PEDULI seberapa jauh titik itu dari player -- makanya gak ada lagi
        // pengecekan radius per titik di bawah. Kalau khawatir lag, ini tetep murah karena
        // TOTAL crystal per dash sudah dibatasi natural sama panjang dash (dashDuration/dashSpeed)
        // dan spacing tetap (CrystalTrailSpacing), bukan per-frame loop yang gak kebatas.
        private void SpawnCrystalsAlongHeadTrail(Player player) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 currentPos = NPC.Center;
            Vector2 travel = currentPos - lastCrystalTrailPos;
            float traveledDist = travel.Length();
            if (traveledDist < CrystalTrailSpacing) return; // belum nempuh 5 block sejak titik terakhir

            Vector2 trailDir = travel / traveledDist;
            int steps = (int)(traveledDist / CrystalTrailSpacing);

            for (int s = 1; s <= steps; s++) {
                Vector2 spawnPos = lastCrystalTrailPos + trailDir * CrystalTrailSpacing * s;

                // Arah "samping badan" buat nentuin kiri/kanan spawn dihitung tegak lurus
                // (perpendicular) dari arah jejak (trailDir) di titik ini.
                Vector2 segLeft = trailDir.RotatedBy(-MathHelper.PiOver2);
                Vector2 segRight = trailDir.RotatedBy(MathHelper.PiOver2);

                // 🛑 [STAGGER URUTAN SPAWN] s=1 = titik jejak paling BELAKANG/paling AWAL di
                // sepanjang trail ini -> delay paling kecil (nembak/dash duluan). s=steps = titik
                // paling BARU/paling deket Head sekarang -> delay paling besar (nembak/dash
                // belakangan). Jadi walau semuanya SECARA TEKNIS di-spawn di tick yang sama,
                // urutan tembak & dash-nya tetap ngalir dari yang paling awal ke paling baru.
                int staggerDelay = (s - 1) * CrystalFireStaggerTicks;
                SpawnSingleCrystal(spawnPos, segLeft, player, staggerDelay);
                SpawnSingleCrystal(spawnPos, segRight, player, staggerDelay);
            }

            // Majuin titik jejak PERSIS sejauh titik terakhir yang kepakai, sisa jarak yang
            // belum genap 5 block dibawa ke interval berikutnya biar spacing-nya tetap konsisten
            // (gak numpuk error tiap interval).
            lastCrystalTrailPos += trailDir * CrystalTrailSpacing * steps;
        }

        private void SpawnSingleCrystal(Vector2 spawnPos, Vector2 outwardDir, Player player, int startDelay = 0) {
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
                    modProj.InitTarget(player.whoAmI, outwardDir, NPC.whoAmI, startDelay);
                }
            }
        }
    }
}
