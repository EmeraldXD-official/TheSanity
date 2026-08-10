using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.ModLoader.IO;
using Terraria.Audio;
using Terraria.Graphics.CameraModifiers;
using TheSanity.GlobalNPC.Bosses.Skeletron;

namespace TheSanity.GlobalNPC.Bosses.Skeletron
{
    // Detour vanilla Skeletron Head lewat PreAI() -> return false.
    // Efeknya: vanilla AI method (termasuk logic spawn 2 SkeletronHand) TIDAK
    // pernah dijalankan sama sekali. NPC.type tetap NPCID.SkeletronHead, jadi
    // loot table, bestiary, boss bar, nama, dan flag unlock dungeon
    // (downedBoss3) tetap otomatis jalan normal via vanilla checkDead().
    public class SkeletronReworkGlobalNPC : Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC npc, bool lateInstantiation)
            => npc.type == NPCID.SkeletronHead;

        // === FIX BARU: despawn kalau semua player mati ===
        // CheckActive() adalah hook resmi ModLoader buat nentuin boleh-
        // gaknya jalur despawn vanilla jalan (lihat dokumentasi hook ini di
        // GlobalNPC). Biasanya npc.boss=true bikin dia kebal despawn jarak,
        // tapi itu gak nyangkut kasus "semua player mati" — player yang
        // mati TETAP kehitung "active" (masih spectating), jadi vanilla
        // sendiri gak akan pernah nganggep boss ini "gak ada yang jagain".
        // Makanya kita override manual pakai AnyPlayerAlive() (cek .dead,
        // bukan cuma .active) sebagai syarat despawn — begitu false, boss
        // ini jadi elegible buat despawn jalur vanilla yang sama & sudah
        // network-safe (gak perlu kirim paket kill manual), dipercepat
        // lewat DespawnBoss() di bawah biar gak nunggu timer default yang
        // lumayan lama.
        public override bool CheckActive(NPC npc) => AnyPlayerAlive();

        static bool AnyPlayerAlive()
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead) return true;
            }
            return false;
        }

        // === State machine ===
        enum State
        {
            Idle,           // reposisi ke dekat player
            RoarTelegraph,  // raungan + getar, INVULNERABLE
            PreCastReposition, // FIX: SEBELUM tiap pattern (phase 1 & 2), Head masuk portal & muncul lagi di ATAS player dulu, INVULNERABLE
            PortalCast,     // PATTERN 1: spawn 15 portal BERGANTIAN, INVULNERABLE (windup)
            BoneActive,     // PATTERN 1: portal + tulang aktif, VULNERABLE (window damage utama)
            BoneThrowCast,  // PATTERN 2: windup singkat lalu lempar 3 ThrownBone, INVULNERABLE (windup)
            ThrowActive,    // PATTERN 2: nunggu ThrownBone terbang & pecah, VULNERABLE (window damage utama)
            SkullCast,      // PATTERN 3: telegraph singkat sebelum skull muncul, INVULNERABLE (windup)
            SkullActive,    // PATTERN 3: skull muncul & nembak charge blaster, VULNERABLE (window damage utama)
            Recovery,       // jeda singkat, VULNERABLE (punish window ekstra)

            // === PHASE 2 (nyala pas HP di bawah Phase2HpThreshold) ===
            // Head sendiri sekarang JADI "pattern"-nya, bukan cuma manggil
            // entitas lain kayak phase 1. Tiap pattern phase 2 ini adalah
            // versi "Head ngelakuin sendiri" dari pattern 1/2/3 phase 1 —
            // lihat komentar di tiap case buat pemetaannya.
            Phase2Awaken,     // telegraph 1x pas transisi ke phase 2, INVULNERABLE
            PortalDashCast,   // versi Head dari PATTERN 1: dash berantai masuk-keluar portal menuju player, INVULNERABLE
            PortalDashActive, // VULNERABLE, jeda sesudah dash terakhir
            ShatterCast,      // versi Head dari PATTERN 2: muter ke player lalu "pecah" jadi shard, hilang, reappear lewat portal, INVULNERABLE
            ShatterActive,    // VULNERABLE, sesudah reappear
            BeamMimicCast,    // versi Head dari PATTERN 3: telegraph singkat sebelum reposisi/orbit/barrage, INVULNERABLE
            BeamMimicActive,  // VULNERABLE, Head reposisi/orbit/barrage sendiri lalu nembak ChargeBlaster versi lebar

            // === PHASE 2, pattern BARU: Hand Slam ===
            // Beda dari 3 pattern phase 2 lain (semuanya "Head sendiri yang
            // ngelakuin") — di sini Head TETEP DI TEMPAT (mirip
            // SkullCast/SkullActive), yang jadi aktor serangan adalah 2
            // entitas terpisah (SkeletonHandSlam) yang dia panggil.
            HandSlamCast,     // telegraph singkat sebelum portal+tangan muncul, INVULNERABLE
            HandSlamActive    // VULNERABLE, nunggu 2 tangan swing & pecah jadi 12 ThrownBone total
        }

        // Tiga pola serangan yang gantian dipilih RANDOM (1/3 masing-masing)
        // tiap kali masuk RoarTelegraph baru — lihat pemilihannya di case
        // State.RoarTelegraph dan percabangannya pas RoarTelegraph selesai.
        enum AttackPattern { PortalCrack, BoneThrow, SkullBlaster }

        // Sub-pattern KHUSUS buat AttackPattern.SkullBlaster — dipilih
        // random (1/3 masing-masing) pas awal masuk State.SkullCast.
        // Lihat SkelySkull.cs buat detail tiap mode gerak.
        enum SkullSubPattern { DirectDuo, OrbitDuo, Barrage5 }

        // === PHASE 2 ===
        // Empat pola serangan phase 2 (dulu 3, nambah HandSlam), dipilih
        // RANDOM (1/4 masing-masing) di RoarTelegraph sama kayak
        // currentPattern, tapi cuma dipakai kalau isPhase2 == true.
        enum Phase2Pattern { PortalDash, ShardShatter, BeamMimic, HandSlam }

        // Sub-pattern KHUSUS buat Phase2Pattern.BeamMimic — dipilih random
        // pas awal masuk State.BeamMimicCast, konsepnya niru
        // SkullSubPattern tapi dilakuin Head sendiri (bukan spawn skull):
        // Direct = Head reposisi 1x lalu nembak, Orbit = Head muter lalu
        // nembak, Barrage = Head hop 3x sambil nembak tiap hop.
        enum BeamMimicSubPattern { Direct, Orbit, Barrage }

        // Sub-pattern KHUSUS buat Phase2Pattern.HandSlam — dipilih random
        // (1/2 masing-masing) pas awal masuk State.HandSlamCast.
        // ClapPair = pattern PERTAMA (rework): 2 tangan muncul bareng dari
        // portal SELALU di sisi berlawanan (180 derajat), MUTERIN player
        // bareng-bareng dulu, baru DASH konvergen bareng ke titik player
        // yang sama (kesan "mengepakkan"/tepuk tangan), lalu pecah (lihat
        // SkeletonHandSlam.SpawnPair / SkeletonHandSlam.Mode.Clap).
        // DashQuad = pattern KEDUA: 4 tangan muncul SATU-SATU (bergantian)
        // dari portal, tiap satu langsung dash fisik SENDIRIAN ke player
        // lalu pecah (lihat SkeletonHandSlam.SpawnDashHand / Mode.Dash).
        enum HandSlamSubPattern { ClapPair, DashQuad }

        State state = State.Idle;
        int stateTimer = 0;
        int portalsSpawnedThisWave = 0;
        int skullsSpawnedThisWave = 0;
        int handsSpawnedThisWave = 0; // FIX BARU: dipakai Phase2Pattern.HandSlam sub-pattern DashQuad, mirip portalsSpawnedThisWave/skullsSpawnedThisWave
        AttackPattern currentPattern = AttackPattern.PortalCrack;
        SkullSubPattern currentSkullSubPattern = SkullSubPattern.DirectDuo;

        // === FIX BARU: despawn kalau semua player mati ===
        // Ditrack terpisah dari targeting normal (npc.target) karena target
        // cuma nunjuk SATU player — di multiplayer bisa aja target lagi mati
        // tapi player lain masih idup, dan boss harusnya tetap lanjut fight
        // (retarget), bukan despawn. Baru despawn kalau BENER2 semua player
        // di dunia ini mati bersamaan.
        int noPlayerAliveTimer = 0;
        const int NoPlayerGraceTime = 120; // ~2 detik grace sebelum dianggap "wipe" beneran, jaga2 dari kedip 1 tick pas transisi respawn

        // === FIX BARU: Dungeon Guardian (subuh) ===
        // Persis vanilla: kalau boss ini masih idup pas Main.dayTime jadi
        // true, dia PERMANEN "ngamuk" jadi Dungeon Guardian (defense &
        // damage kontak digedein gila2an, ngejar terus non-stop) dan gak
        // pernah balik ke pola serangan normal lagi walau ntar malem lagi.
        bool isDungeonGuardian = false;

        // === PHASE 2 state ===
        bool isPhase2 = false;
        Phase2Pattern currentPhase2Pattern = Phase2Pattern.PortalDash;
        BeamMimicSubPattern currentBeamSub = BeamMimicSubPattern.Direct;
        HandSlamSubPattern currentHandSlamSubPattern = HandSlamSubPattern.ClapPair; // FIX BARU
        Vector2 dashPortalPos;         // PortalDash & ShardShatter: titik reappear/tujuan terkini
        Vector2 dashStartPos;          // PortalDash & ShardShatter: titik berangkat (buat lerp fisik dash)
        float dashArcSign = 1f;        // PortalDash: sisi lengkungan lompat (gantian tiap hop biar gak monoton searah)

        // Goyangan gerak biar Head keliatan "jiggle" (bergoyang pelan),
        // bukan gerak kaku lurus kayak robot — dipakai MoveTowardPlayer.
        float jiggleTimer = 0f;

        // durasi tiap state (tick, 60 tick = 1 detik)
        const int IdleDuration = 60;
        const int RoarDuration = 50;

        // FIX (BARU): reposisi wajib sebelum tiap pattern — masuk portal
        // di tempat, muncul lagi di atas player. Dipakai State.PreCastReposition.
        const int PreCastFadeOutTime = 12;   // fade out pas masuk portal
        const float PreCastAboveHeight = 200f; // seberapa tinggi di atas player dia muncul
        const int PreCastFadeInTime = 12;    // fade in pas muncul lagi
        const int PreCastRepositionDuration = PreCastFadeOutTime + PreCastFadeInTime;

        // === FIX: 15 portal muncul BERGANTIAN, bukan 3 sekaligus ===
        // Sebelumnya PortalCast cuma manggil SpawnWave(count: 3) sekali di
        // tick pertama, jadi 3 portal muncul bareng di tick yang sama.
        // Sekarang PortalCast nge-loop lewat PortalSpawnInterval, manggil
        // BonePortal.SpawnSingle() satu-satu tiap interval, sampai 15 total.
        // (Jarak antar portal sendiri dijaga di BonePortal.SpawnSingle biar
        // gak numpuk/overlap satu sama lain — lihat MinPortalSpacing di sana.)
        const int PortalCount = 15;
        const int PortalSpawnInterval = 10; // jeda antar tiap portal (~0.17 detik)
        const int CastDuration = PortalCount * PortalSpawnInterval; // 80 tick, otomatis nyesuain PortalCount

        // FIX: disesuain sama BonePortal.EruptionTick yang dicepetin (90 ->
        // 24 tick) — sekarang cuma 24 tick (growth+warning portal) + 72
        // tick (emerge 0.2 detik + hold + retract BigBoneSpike, lihat
        // BigBoneSpike.EmergeTime/HoldTime/RetractTime) + buffer. Dihitung
        // dari portal TERAKHIR yang muncul (paling telat kelar animasinya),
        // bukan dari portal pertama.
        const int BoneActiveDuration = 130;
        const int RecoveryDuration = 40;

        // === PATTERN 2: Bone Throw ===
        // Windup singkat (raih tulang, bersiap lempar) sebelum 3 ThrownBone
        // dilepas bersamaan di tick terakhir windup ini.
        const int ThrowCastDuration = 25; // ~0.4 detik, INVULNERABLE

        // Sesudah volley dilempar, jendela VULNERABLE ini harus cukup lama
        // buat nutupin seluruh siklus ThrownBone: FlightTime (55) + waktu
        // shard-nya kelar (~35) + sedikit buffer reaksi player.
        const int ThrowActiveDuration = 110;

        // === PATTERN 3: Skull Charge Blaster ===
        // Windup singkat sebelum skull-skull mulai muncul (cuma cue visual,
        // skull-nya sendiri baru di-spawn pas masuk SkullActive).
        const int SkullCastDuration = 30; // ~0.5 detik, INVULNERABLE

        // Harus cukup buat nutupin lifecycle TERPANJANG dari ketiga
        // sub-pattern (DirectDuo/OrbitDuo paling lama: Position+Charge+
        // Firing+Leave ~135-140 tick; Barrage5 staggered ~135 tick juga
        // dari skull terakhir) + sedikit buffer reaksi.
        const int SkullActiveDuration = 150;

        // Barrage5: 5 skull muncul satu-satu tiap interval ini (mirip
        // PortalSpawnInterval di pattern 1, cuma buat skull).
        const int SkullBarrageCount = 5;
        const int SkullBarrageInterval = 15; // ~0.25 detik antar tiap skull

        // === PHASE 2 ===
        const float Phase2HpThreshold = 0.5f; // trigger pas HP <= 50%
        const int Phase2AwakenDuration = 80;  // telegraph 1x pas transisi, lebih lama & lebih kuat dari roar biasa

        // === FIX BARU: Dungeon Guardian (subuh) ===
        // Niru vanilla persis (lihat wiki Skeletron): defense digedein
        // sampe 9999 (praktis immune — mekanik minimum damage vanilla
        // tetep nyisain ~1 dmg per hit, gak bikin bener2 gak bisa mati),
        // damage KONTAK digedein ke 9999 (nyentuh = one-shot), dan dia
        // ngejar terus non-stop jauh lebih ngebut dari mode kejar biasa
        // (10f), gak pernah jaga jarak lagi (kontak emang tujuannya).
        const int DungeonGuardianDefense = 9999;
        const int DungeonGuardianDamage = 9999;
        const float DungeonGuardianSpeed = 26f;
        const float DungeonGuardianSpinSpeed = 0.85f; // radian/tick, niru "head spin attack"

        // --- Phase2Pattern.PortalDash (versi Head dari pattern 1) ---
        // FIX total alur sesuai spek: tiap hop dia MUNCUL di portal keluar
        // yang posisinya RANDOM (bukan nerusin dari posisi lama), lalu
        // ngedash PANJANG dari situ ke portal masuk yang selalu diukur dari
        // TENGAH PLAYER (bukan dari posisi Head), lalu masuk & ilang ke
        // portal itu, lalu ulang lagi dari portal keluar random yang baru.
        const int PortalDashHops = 6;                 // FIX: dinaikin (3 -> 6) jumlah dash portal-ke-portal, sesuai request
        // FIX: dinaikin lagi (420-650 -> 650-950) — soalnya player kadang
        // lebih cepet dari Skeletron, jadi dash-nya perlu lebih PANJANG.
        // Travel time (PortalDashTravelTime) TETAP sama, jadi jarak lebih
        // jauh otomatis bikin dash-nya nempuh lebih cepat juga (distance
        // naik, waktu tetap = speed rata-rata ikut naik) — bukan cuma
        // "keliatan" lebih jauh doang, tapi beneran lebih ngebut ngejarnya.
        const float PortalDashRandomMinRange = 650f;
        const float PortalDashRandomMaxRange = 950f;
        const float PortalDashArriveOffset = 70f;     // portal masuk = tengah player + offset kecil random segini (biar gak pas nempel di hitbox player)
        const int PortalDashAppearFade = 10;          // fade-in pas baru muncul di portal keluar (random), sebelum mulai dash
        const int PortalDashTravelTime = 30;          // FIX: dash fisik — jaraknya sekarang jauh (420-650px) jadi beneran kerasa "panjang"
        const int PortalDashExitFade = 10;             // fade-out pas masuk ke portal tujuan (dekat player) & ilang
        const int PortalDashHopDuration = PortalDashAppearFade + PortalDashTravelTime + PortalDashExitFade; // 1 siklus dash penuh
        const int PortalDashCastDuration = PortalDashHopDuration * PortalDashHops;
        const float PortalDashArcHeight = 60f; // tinggi lengkungan "lompat" tegak lurus arah dash, biar gak geser lurus kaku
        const int PortalDashActiveDuration = 90;

        // FIX BARU: tiap dash (tiap hop, 6x total sekarang) juga munculin 1
        // "cursed skull" (SkelySkull versi Mode.Orbit apa adanya — muter
        // ngelilingin player dulu, baru charge & nembak ChargeBlaster,
        // lalu pergi) bareng pas portal keluar dash itu muncul. Lihat
        // panggilan SkelySkull.SpawnCursedOrbit() di hopTick == 1 pada
        // State.PortalDashCast di bawah.

        // --- Phase2Pattern.ShardShatter (versi Head dari pattern 2) ---
        // FIX total alur sesuai spek: dia MELESAT (dash fisik, bukan diem
        // di tempat) ke titik DI ATAS PLAYER sambil MUTER kenceng, begitu
        // nyampe baru ANCUR (shatter jadi shard), lalu MUNCUL LAGI TEPAT DI
        // POSISI PLAYER (bukan di titik dia shatter tadi).
        const int ShatterDashDuration = 32;     // dash + spin menuju titik di atas player
        const float ShatterAboveHeight = 190f;  // seberapa tinggi di atas player titik shatter-nya
        const float ShatterReappearRadius = 55f; // muncul lagi persis di sekitar tengah player (radius kecil)
        const int ShatterFadeInTime = 12;       // fade-in pas reappear di posisi player
        const int ShatterCastDuration = ShatterDashDuration + ShatterFadeInTime;
        const int ShardShatterBurstCount = 18; // lebih banyak dari ThrownBone (9) — ini boss-nya sendiri yang pecah
        const int ShatterActiveDuration = 110;

        // --- Phase2Pattern.BeamMimic (versi Head dari pattern 3) ---
        // FIX total konsep sesuai spek: Head SENDIRI gak lagi reposisi &
        // nembak beam-nya sendiri. Sekarang Head masuk portal & ngilang,
        // lalu SkelySkull (entitas yang sama kayak phase 1) yang keluar
        // gantiin dia — tapi di-scale gede sepadan ukuran Skeletron.
        // Sesudah skull-skull itu selesai nembak & pergi, Head muncul lagi.
        const int BeamMimicCastDuration = SkullCastDuration; // telegraph "masuk portal" sama kayak windup skull asli
        const float BigSkullScale = 2.6f; // skala sprite skull biar sepadan ukuran Skeletron (dipakai buat Projectile.scale & thickness beam-nya)
        const int BeamMimicActiveDuration = SkullActiveDuration + 20; // ngikutin durasi lifecycle skull asli (lihat SkullActiveDuration) + buffer reappear Head
        const int BeamMimicReappearFade = 15; // fade-in Head pas muncul lagi di akhir pattern

        // --- Phase2Pattern.HandSlam ---
        // 2 tangan Skeletron (SkeletonHandSlam, reuse vanilla SkeletronHand)
        // muncul dari portal — sub-pattern ClapPair (rework): muter
        // ngelilingin player dulu, baru dash konvergen bareng ke player lalu
        // PECAH jadi 6 ThrownBone radial per tangan (2 x 6 = 12 total).
        // Beda dari 3 pattern phase 2 lain — Head SENDIRI gak dash/gak
        // ilang, dia cuma "pemicu" (mirip SkullCast/SkullActive di phase
        // 1), aktor serangannya entitas terpisah (SkeletonHandSlam) yang
        // dia panggil.
        const int HandSlamCastDuration = 30;    // telegraph singkat sebelum portal+tangan muncul, INVULNERABLE

        // ClapPair (sub-pattern PERTAMA): harus nutupin lifecycle 2 tangan
        // (appear + orbit muterin player + dash konvergen + shatter) + 12
        // ThrownBone (flight+pecah shard) + buffer.
        const int HandSlamActiveDuration = 210;

        // --- Sub-pattern KEDUA: DashQuad ---
        // 4 tangan muncul SATU-SATU tiap interval ini (mirip
        // SkullBarrageInterval/PortalSpawnInterval), masing-masing baru
        // dash+pecah SESUDAH muncul (lihat SkeletonHandSlam.AI, durasi
        // appear+dash-nya sendiri di sana) — jadi durasi active total pattern
        // ini harus nutupin: waktu tangan TERAKHIR muncul + lifecycle
        // appear+dash+shatter tangan itu sendiri + siklus ThrownBone
        // (flight+shard) hasil pecahannya + buffer.
        const int HandDashCount = 4;
        const int HandDashSpawnInterval = 22; // jeda antar tiap tangan muncul & langsung dash, biar keliatan "bergantian" bukan bareng
        const int HandSlamDashQuadActiveDuration = 240;

        // Durasi active state yang dipakai TERGANTUNG sub-pattern yang
        // ke-roll di HandSlamCast — dicek di State.HandSlamActive.
        int CurrentHandSlamActiveDuration => currentHandSlamSubPattern == HandSlamSubPattern.DashQuad
            ? HandSlamDashQuadActiveDuration
            : HandSlamActiveDuration;

        public override bool PreAI(NPC npc)
        {
            RunStateMachine(npc);
            return false; // vanilla AI (termasuk spawn Hand) TIDAK dijalankan
        }

        void RunStateMachine(NPC npc)
        {
            if (npc.target < 0 || !Main.player[npc.target].active || Main.player[npc.target].dead)
                npc.TargetClosest();

            Player target = Main.player[npc.target];
            stateTimer++;

            // === FIX BARU: despawn kalau semua player mati ===
            // Dicek SEBELUM apa pun lain (termasuk sebelum Dungeon Guardian
            // & Phase2 trigger) — gak ada gunanya lanjutin state apa pun
            // kalau emang gak ada lagi player hidup buat diladenin.
            if (!AnyPlayerAlive())
            {
                noPlayerAliveTimer++;
                if (noPlayerAliveTimer >= NoPlayerGraceTime)
                {
                    DespawnBoss(npc);
                    return;
                }
            }
            else
            {
                noPlayerAliveTimer = 0;
            }

            // === FIX BARU: Dungeon Guardian trigger (subuh) ===
            // Begitu Main.dayTime jadi true dan boss ini masih idup, dia
            // PERMANEN masuk mode Dungeon Guardian — nge-short-circuit SISA
            // method ini (gak lanjut ke Phase2 trigger / switch state
            // normal di bawah sama sekali), berlaku SEKALIPUN lagi di
            // tengah windup/cast state lain (interupsi total, sama kayak
            // vanilla yang langsung ngamuk begitu subuh dateng gak peduli
            // Skeletron lagi ngapain).
            if (!isDungeonGuardian && Main.dayTime)
            {
                isDungeonGuardian = true;
                ApplyDungeonGuardianForm(npc);
            }

            if (isDungeonGuardian)
            {
                RunDungeonGuardian(npc, target);
                return;
            }

            // === PHASE 2 trigger ===
            // Begitu HP nembus threshold, paksa masuk Phase2Awaken TERLEPAS
            // lagi di state apa pun sekarang (kalau lagi di tengah windup
            // invulnerable itu wajar keputus, boss emang lagi gak bisa
            // dihit pas itu). Dicek sebelum switch biar transisinya instan
            // di tick yang sama HP-nya turun ke bawah threshold.
            if (!isPhase2 && npc.life <= npc.lifeMax * Phase2HpThreshold)
            {
                isPhase2 = true;
                npc.dontTakeDamage = true;
                ChangeState(npc, State.Phase2Awaken);
                return; // biar case Phase2Awaken mulai bersih di tick berikutnya (stateTimer 0->1)
            }

            switch (state)
            {
                case State.Idle:
                    npc.dontTakeDamage = false;
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 250f); // FIX: dinaikin lagi (dulu 2.2f -> 4.5f), lebih deket ke agresivitas Skeletron vanilla — tuning lanjut kalau masih kurang/kelewat cepat

                    if (stateTimer >= IdleDuration)
                        ChangeState(npc, State.RoarTelegraph);
                    break;

                case State.RoarTelegraph:
                    npc.dontTakeDamage = true; // INVULNERABLE
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 250f); // FIX: dulu berhenti total (*0.9f) pas raung — sekarang TETEP NGEJAR selama cast/telegraph, gak cuma pas Active

                    if (stateTimer == 1)
                    {
                        TriggerRoarStart(npc); // sound + shock awal + dust ring

                        // Pilih pattern serangan berikutnya SEKALI di awal
                        // raungan (bukan pas RoarTelegraph selesai), biar
                        // ada waktu buat nambahin telegraph visual berbeda
                        // per pattern nanti kalau perlu.
                        // FIX: sekarang 3 pattern (dulu cuma 2 via NextBool),
                        // jadi pakai Next(3) — index enum-nya sengaja
                        // dijajarin PortalCrack=0, BoneThrow=1, SkullBlaster=2
                        // biar castingnya langsung pas tanpa mapping manual.
                        //
                        // PHASE 2: kalau udah isPhase2, yang di-roll gantian
                        // Phase2Pattern (bukan AttackPattern) — enum-nya
                        // sengaja dijajarin sama urutannya (PortalDash=0,
                        // ShardShatter=1, BeamMimic=2) biar konsisten sama
                        // pemetaan pattern 1/2/3 versi phase 1. HandSlam=3
                        // ditambahin di ujung (gak punya pemetaan ke phase 1,
                        // dia pattern baru murni), makanya sekarang Next(4).
                        if (isPhase2)
                            currentPhase2Pattern = (Phase2Pattern)Main.rand.Next(4);
                        else
                            currentPattern = (AttackPattern)Main.rand.Next(3);
                    }

                    // getaran berkelanjutan selama ~setengah durasi roar,
                    // bukan cuma 1 kali shock — biar berasa "raungan yang menggetarkan"
                    if (stateTimer <= RoarDuration / 2 && stateTimer % 5 == 0)
                    {
                        TriggerRumbleTick(npc);
                    }

                    if (stateTimer >= RoarDuration)
                        ChangeState(npc, State.PreCastReposition); // FIX: gak langsung ke pattern — reposisi ke atas player dulu (lihat state baru di bawah)
                    break;

                // === FIX (BARU): sebelum TIAP pattern (phase 1 maupun 2),
                // Head masuk portal di posisi sekarang & muncul lagi TEPAT
                // DI ATAS PLAYER dulu, baru abis itu pattern yang udah
                // di-roll di RoarTelegraph beneran mulai jalan. Ini
                // ngatasin kasus player lagi jauh dari Head — daripada dia
                // capek ngejar dulu baru bisa mulai, dia langsung "pindah"
                // ke atas player lewat portal. Cuma dipakai SEBELUM pattern
                // — sesudah pattern kelar (Recovery) TIDAK pakai step ini.
                case State.PreCastReposition:
                    npc.dontTakeDamage = true;

                    if (stateTimer == 1)
                    {
                        dashStartPos = npc.Center; // titik dia masuk portal
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                            BonePortal.SpawnDashPortal(npc, dashStartPos, target, scaleMul: 1f);
                    }

                    if (stateTimer <= PreCastFadeOutTime)
                    {
                        // FASE MASUK PORTAL: fade out di tempat
                        npc.velocity = Vector2.Zero;
                        npc.alpha = (int)MathHelper.Lerp(0, 255, stateTimer / (float)PreCastFadeOutTime);
                    }
                    else
                    {
                        if (stateTimer == PreCastFadeOutTime + 1)
                        {
                            // titik muncul: TEPAT DI ATAS PLAYER
                            dashPortalPos = target.Center + new Vector2(0f, -PreCastAboveHeight);
                            npc.Center = dashPortalPos;
                            npc.velocity = Vector2.Zero;

                            if (Main.netMode != NetmodeID.MultiplayerClient)
                                BonePortal.SpawnDashPortal(npc, dashPortalPos, target);
                        }

                        // FASE MUNCUL DI ATAS PLAYER: fade in
                        int fadeTick = stateTimer - PreCastFadeOutTime;
                        npc.alpha = (int)MathHelper.Lerp(255, 0, MathHelper.Clamp(fadeTick / (float)PreCastFadeInTime, 0f, 1f));
                    }

                    if (stateTimer >= PreCastRepositionDuration)
                    {
                        // baru sekarang lanjut ke pattern yang UDAH di-roll
                        // di RoarTelegraph (currentPattern / currentPhase2Pattern)
                        State nextState;
                        if (isPhase2)
                        {
                            nextState = currentPhase2Pattern switch
                            {
                                Phase2Pattern.PortalDash => State.PortalDashCast,
                                Phase2Pattern.ShardShatter => State.ShatterCast,
                                Phase2Pattern.BeamMimic => State.BeamMimicCast,
                                Phase2Pattern.HandSlam => State.HandSlamCast,
                                _ => State.PortalDashCast
                            };
                        }
                        else
                        {
                            nextState = currentPattern switch
                            {
                                AttackPattern.PortalCrack => State.PortalCast,
                                AttackPattern.BoneThrow => State.BoneThrowCast,
                                AttackPattern.SkullBlaster => State.SkullCast,
                                _ => State.PortalCast
                            };
                        }
                        npc.alpha = 0;
                        ChangeState(npc, nextState);
                    }
                    break;

                case State.PortalCast:
                    npc.dontTakeDamage = true; // masih INVULNERABLE, fase "cast"
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 300f); // FIX: dulu gak gerak sama sekali pas nunggu portal muncul — sekarang tetep ngejar

                    if (stateTimer == 1)
                        portalsSpawnedThisWave = 0; // reset tiap kali masuk fase cast baru

                    // spawn 1 portal tiap PortalSpawnInterval tick, gantian
                    // sampai PortalCount (8) total — bukan sekaligus barengan
                    if (Main.netMode != Terraria.ID.NetmodeID.MultiplayerClient
                        && portalsSpawnedThisWave < PortalCount
                        && (stateTimer - 1) % PortalSpawnInterval == 0)
                    {
                        BonePortal.SpawnSingle(npc, target);
                        portalsSpawnedThisWave++;
                    }

                    if (stateTimer >= CastDuration)
                        ChangeState(npc, State.BoneActive);
                    break;

                case State.BoneActive:
                    npc.dontTakeDamage = false; // VULNERABLE, window damage utama
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 300f); // FIX: disamain sama Idle, dinaikin lagi

                    if (stateTimer >= BoneActiveDuration)
                        ChangeState(npc, State.Recovery);
                    break;

                case State.BoneThrowCast:
                    npc.dontTakeDamage = true; // masih INVULNERABLE, fase "raih & siap lempar"
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 300f); // FIX: dulu berhenti (*0.92f) pas windup lempar — sekarang tetep ngejar

                    if (stateTimer == 1)
                        TriggerThrowWindupEffect(npc); // dust "narik" ke tangan, cue sebelum lempar

                    // volley 3 ThrownBone dilepas bersamaan tepat di tick
                    // terakhir windup, bukan nyicil kayak portal (biar
                    // kerasa kayak satu lemparan tegas)
                    if (Main.netMode != NetmodeID.MultiplayerClient && stateTimer == ThrowCastDuration)
                    {
                        ThrownBone.SpawnVolley(npc, target);
                    }

                    if (stateTimer >= ThrowCastDuration)
                        ChangeState(npc, State.ThrowActive);
                    break;

                case State.ThrowActive:
                    npc.dontTakeDamage = false; // VULNERABLE, window damage utama pattern 2
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 300f); // FIX: disamain sama Idle, dinaikin lagi

                    if (stateTimer >= ThrowActiveDuration)
                        ChangeState(npc, State.Recovery);
                    break;

                case State.SkullCast:
                    npc.dontTakeDamage = true; // masih INVULNERABLE, fase telegraph
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 300f); // FIX: dulu berhenti (*0.9f) pas telegraph — sekarang tetep ngejar

                    if (stateTimer == 1)
                    {
                        // pilih sub-pattern skull SEKALI di awal telegraph,
                        // sama pola kayak currentPattern di RoarTelegraph
                        currentSkullSubPattern = (SkullSubPattern)Main.rand.Next(3);
                        TriggerSkullTelegraph(npc); // dust biru muda ke arah dalam, cue "mata ketiga fokus"
                    }

                    if (stateTimer >= SkullCastDuration)
                        ChangeState(npc, State.SkullActive);
                    break;

                case State.SkullActive:
                    npc.dontTakeDamage = false; // VULNERABLE, window damage utama pattern 3
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 300f); // FIX: disamain sama Idle, dinaikin lagi

                    if (stateTimer == 1)
                    {
                        skullsSpawnedThisWave = 0;

                        // DirectDuo & OrbitDuo: 2 skull langsung muncul
                        // bareng lalu jalan sesuai mode masing-masing
                        // (lihat SkelySkull.Mode). Barrage5 SENGAJA gak
                        // di-spawn di sini — dia nyicil lewat interval loop
                        // di bawah, mirip logic portal di PortalCast.
                        switch (currentSkullSubPattern)
                        {
                            case SkullSubPattern.DirectDuo:
                                SkelySkull.SpawnPair(npc, target, SkelySkull.Mode.Direct);
                                break;
                            case SkullSubPattern.OrbitDuo:
                                SkelySkull.SpawnPair(npc, target, SkelySkull.Mode.Orbit);
                                break;
                            case SkullSubPattern.Barrage5:
                                // spawn skull pertama langsung di tick 1,
                                // sisanya nyusul lewat interval di bawah
                                SkelySkull.SpawnSingle(npc, target);
                                skullsSpawnedThisWave = 1;
                                break;
                        }
                    }
                    else if (currentSkullSubPattern == SkullSubPattern.Barrage5
                        && Main.netMode != NetmodeID.MultiplayerClient
                        && skullsSpawnedThisWave < SkullBarrageCount
                        && (stateTimer - 1) % SkullBarrageInterval == 0)
                    {
                        SkelySkull.SpawnSingle(npc, target);
                        skullsSpawnedThisWave++;
                    }

                    if (stateTimer >= SkullActiveDuration)
                        ChangeState(npc, State.Recovery);
                    break;

                case State.Recovery:
                    npc.dontTakeDamage = false; // tetap VULNERABLE, punish window ekstra
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 300f); // FIX: dulu gak gerak sama sekali pas recovery — sekarang tetep ngejar

                    if (stateTimer >= RecoveryDuration)
                        ChangeState(npc, State.RoarTelegraph); // loop balik ke raungan (isPhase2 nentuin pattern set berikutnya)
                    break;

                // ============================================================
                // === PHASE 2 ===
                // ============================================================

                case State.Phase2Awaken:
                    npc.dontTakeDamage = true;
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 250f); // FIX: dulu berhenti (*0.9f) pas transisi phase 2 — sekarang tetep ngejar

                    if (stateTimer == 1)
                    {
                        TriggerRoarStart(npc); // reuse cue roar (sound + shock + dust ring) buat telegraph transisi
                    }
                    if (stateTimer <= Phase2AwakenDuration / 2 && stateTimer % 4 == 0)
                    {
                        TriggerRumbleTick(npc); // getaran lebih rapat (tiap 4 tick, bukan 5) biar kerasa lebih intens dari roar biasa
                    }

                    if (stateTimer >= Phase2AwakenDuration)
                        ChangeState(npc, State.RoarTelegraph); // langsung lanjut roar biasa, yang bakal milih Phase2Pattern
                    break;

                // === Versi Head dari PATTERN 1 (portal) ===
                // FIX total alur sesuai spek user: tiap hop —
                //   1) portal KELUAR muncul di titik RANDOM (jarak
                //      PortalDashRandomMinRange..MaxRange dari tengah
                //      player) -> Head langsung "muncul" (fade-in) di situ.
                //   2) portal MASUK muncul deket tengah player (measured
                //      dari target.Center, BUKAN dari posisi Head).
                //   3) Head DASH PANJANG & fisik (gerak beneran, bukan
                //      teleport) dari portal keluar ke portal masuk.
                //   4) nyampe -> fade out, "masuk & ilang" ke portal itu.
                //   5) ulang dari langkah 1 (portal keluar baru, random lagi).
                case State.PortalDashCast:
                    npc.dontTakeDamage = true;

                    int hopIndex = (stateTimer - 1) / PortalDashHopDuration;
                    int hopTick = (stateTimer - 1) % PortalDashHopDuration + 1;

                    if (hopIndex < PortalDashHops)
                    {
                        // FIX: fade & posisi harus jalan di SEMUA client (dihitung
                        // deterministik dari stateTimer yang udah disync via
                        // SendExtraAI) — cuma SPAWN portal-nya yang wajib
                        // server/singleplayer-only, biar gak dobel-spawn.
                        if (hopTick == 1)
                        {
                            // FIX (bug portal keluar random): titik berangkat
                            // BUKAN posisi Head sekarang lagi, tapi titik acak
                            // di sekitar player (jauh, biar dash-nya panjang).
                            float exitAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                            float exitDist = Main.rand.NextFloat(PortalDashRandomMinRange, PortalDashRandomMaxRange);
                            dashStartPos = target.Center + exitAngle.ToRotationVector2() * exitDist;

                            // FIX (ukur dari tengah player): titik tujuan
                            // SELALU dihitung dari target.Center, bukan dari
                            // posisi Head — portal masuk selalu deket player.
                            float arriveAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                            dashPortalPos = target.Center + arriveAngle.ToRotationVector2() * PortalDashArriveOffset;

                            dashArcSign = -dashArcSign; // gantian sisi lengkungan tiap hop

                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                BonePortal.SpawnDashPortal(npc, dashStartPos, target, scaleMul: 1f);   // portal KELUAR (random, kecil)
                                BonePortal.SpawnDashPortal(npc, dashPortalPos, target);                 // portal MASUK (deket player, besar)

                                // FIX BARU: tiap hop dash juga munculin 1 cursed
                                // skull (Mode.Orbit) yang muterin player lalu
                                // nembak ChargeBlaster — lihat komentar di atas
                                // const PortalDashArcHeight.
                                SkelySkull.SpawnCursedOrbit(npc, target);
                            }

                            npc.Center = dashStartPos; // langsung "muncul" di portal keluar
                            npc.velocity = Vector2.Zero;
                            npc.alpha = 255;
                        }

                        if (hopTick <= PortalDashAppearFade)
                        {
                            // FASE MUNCUL: fade-in diem di portal keluar dulu
                            // sebentar sebelum ngedash, biar kelihatan jelas
                            // portalnya "baru muncul".
                            npc.alpha = (int)MathHelper.Lerp(255, 0, hopTick / (float)PortalDashAppearFade);
                        }
                        else if (hopTick <= PortalDashAppearFade + PortalDashTravelTime)
                        {
                            // FASE DASH: gerak fisik PANJANG dari dashStartPos
                            // ke dashPortalPos, dikasih lengkungan tegak lurus
                            // arah gerak biar kerasa "melompat" (bukan geser
                            // lurus kaku) — sesuai referensi gambar user.
                            int travelTick = hopTick - PortalDashAppearFade;
                            float t = travelTick / (float)PortalDashTravelTime;
                            float eased = EaseOutCubic(t);
                            Vector2 straight = Vector2.Lerp(dashStartPos, dashPortalPos, eased);

                            Vector2 pathDir = (dashPortalPos - dashStartPos).SafeNormalize(Vector2.UnitX);
                            Vector2 perp = pathDir.RotatedBy(MathHelper.PiOver2);
                            float arc = (float)System.Math.Sin(t * MathHelper.Pi) * PortalDashArcHeight * dashArcSign;

                            npc.Center = straight + perp * arc;
                            npc.rotation = pathDir.ToRotation() * 0.6f;
                            if (System.Math.Abs(pathDir.X) > 0.05f)
                                npc.spriteDirection = pathDir.X > 0 ? 1 : -1;
                            npc.alpha = 0;
                        }
                        else
                        {
                            // FASE MASUK PORTAL: nyampe titik tujuan (deket
                            // player), diem sambil fade out — "masuk & ilang"
                            // ke portal itu. Hop berikutnya bakal mulai dari
                            // portal keluar RANDOM yang baru (langkah di atas).
                            int fadeTick = hopTick - PortalDashAppearFade - PortalDashTravelTime;
                            npc.Center = dashPortalPos;
                            npc.velocity = Vector2.Zero;
                            npc.rotation = 0f;
                            npc.alpha = (int)MathHelper.Lerp(0, 255, fadeTick / (float)PortalDashExitFade);
                        }
                    }
                    else
                    {
                        npc.alpha = 255; // abis hop terakhir Head emang lagi "di dalam portal" (invisible), lanjut ke PortalDashActive buat reappear
                    }

                    if (stateTimer >= PortalDashCastDuration)
                        ChangeState(npc, State.PortalDashActive);
                    break;

                case State.PortalDashActive:
                    npc.dontTakeDamage = false; // VULNERABLE, jeda punish sesudah dash beruntun
                    npc.alpha = 0; // reappear penuh begitu masuk window vulnerable
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 300f); // FIX: disamain sama Idle, dinaikin lagi

                    if (stateTimer >= PortalDashActiveDuration)
                        ChangeState(npc, State.Recovery);
                    break;

                // === Versi Head dari PATTERN 2 (bone throw) ===
                // FIX total alur sesuai spek user: Head DASH FISIK (melesat,
                // bukan diem di tempat) ke titik DI ATAS PLAYER sambil MUTER
                // kenceng, begitu nyampe baru ANCUR (pecah jadi shard), lalu
                // MUNCUL LAGI TEPAT DI POSISI PLAYER (bukan di titik shatter).
                case State.ShatterCast:
                    npc.dontTakeDamage = true;

                    if (stateTimer == 1)
                    {
                        dashStartPos = npc.Center;
                        dashPortalPos = target.Center + new Vector2(0f, -ShatterAboveHeight); // titik di atas player
                        npc.alpha = 0;
                    }

                    if (stateTimer <= ShatterDashDuration)
                    {
                        // FASE MELESAT + MUTER: fisik gerak dari posisi
                        // sekarang ke titik di atas player, sambil rotasi
                        // cepat berkelanjutan (independen dari arah gerak).
                        float t = stateTimer / (float)ShatterDashDuration;
                        float eased = EaseOutCubic(t);
                        npc.Center = Vector2.Lerp(dashStartPos, dashPortalPos, eased);
                        npc.rotation += 0.6f;
                        npc.velocity = Vector2.Zero;

                        if (stateTimer == ShatterDashDuration)
                        {
                            // FASE ANCUR: nyampe di atas player -> pecah jadi
                            // shard, ilang, terus portal reappear muncul
                            // TEPAT di posisi player (radius kecil).
                            SoundEngine.PlaySound(SoundID.Shatter, npc.Center); // TODO: ganti sound custom "boss bone shatter" kalau asetnya udah ada
                            npc.rotation = 0f;
                            npc.alpha = 255;

                            Vector2 reappearOffset = Main.rand.NextVector2CircularEdge(ShatterReappearRadius, ShatterReappearRadius);
                            dashPortalPos = target.Center + reappearOffset; // reuse field, sekarang jadi titik reappear

                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                BoneShardParticle.SpawnBurst(npc.GetSource_FromAI(), npc.Center, ShardShatterBurstCount, 5f, 9f);
                                BonePortal.SpawnDashPortal(npc, dashPortalPos, target);
                            }
                        }
                    }
                    else
                    {
                        // FASE MUNCUL DI PLAYER: fade-in tepat di titik
                        // reappear deket tengah player.
                        int fadeTick = stateTimer - ShatterDashDuration;
                        npc.Center = dashPortalPos;
                        npc.velocity = Vector2.Zero;
                        npc.alpha = (int)MathHelper.Lerp(255, 0, MathHelper.Clamp(fadeTick / (float)ShatterFadeInTime, 0f, 1f));
                    }

                    if (stateTimer >= ShatterCastDuration)
                        ChangeState(npc, State.ShatterActive);
                    break;

                case State.ShatterActive:
                    npc.dontTakeDamage = false; // VULNERABLE, sesudah reappear
                    npc.alpha = 0;
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 300f); // FIX: disamain sama Idle, dinaikin lagi

                    if (stateTimer >= ShatterActiveDuration)
                        ChangeState(npc, State.Recovery);
                    break;

                // === Versi Head dari PATTERN 3 (skull charge blaster) ===
                // FIX total konsep sesuai spek user: Head SENDIRI gak lagi
                // reposisi & nembak beam-nya sendiri. Sekarang dia MASUK
                // PORTAL & ILANG, terus SkelySkull (entitas SAMA kayak
                // phase 1) yang keluar gantiin dia, tapi di-scale gede
                // (BigSkullScale) sepadan ukuran Skeletron. Sesudah skull-
                // skull itu selesai nembak & pergi, Head muncul lagi.
                case State.BeamMimicCast:
                    npc.dontTakeDamage = true;
                    npc.velocity *= 0.9f;

                    if (stateTimer == 1)
                    {
                        currentBeamSub = (BeamMimicSubPattern)Main.rand.Next(3);
                        dashStartPos = npc.Center; // titik dia "masuk portal", dipakai lagi buat reappear nanti

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                            BonePortal.SpawnDashPortal(npc, dashStartPos, target, scaleMul: 1f);
                    }

                    // fade out — masuk ke portal, ilang sebelum skull2 keluar
                    npc.alpha = (int)MathHelper.Lerp(0, 255, MathHelper.Clamp(stateTimer / (float)BeamMimicCastDuration, 0f, 1f));

                    if (stateTimer >= BeamMimicCastDuration)
                        ChangeState(npc, State.BeamMimicActive);
                    break;

                case State.BeamMimicActive:
                    npc.dontTakeDamage = false; // VULNERABLE — tapi Head sendiri invisible/gak kena, damage dateng dari skull+beam
                    npc.velocity = Vector2.Zero;

                    if (stateTimer == 1)
                    {
                        skullsSpawnedThisWave = 0;

                        switch (currentBeamSub)
                        {
                            case BeamMimicSubPattern.Direct:
                                SkelySkull.SpawnPair(npc, target, SkelySkull.Mode.Direct, BigSkullScale);
                                break;
                            case BeamMimicSubPattern.Orbit:
                                SkelySkull.SpawnPair(npc, target, SkelySkull.Mode.Orbit, BigSkullScale);
                                break;
                            case BeamMimicSubPattern.Barrage:
                                // sama pola kayak SkullSubPattern.Barrage5 di phase 1:
                                // skull pertama langsung, sisanya nyicil lewat interval di bawah
                                SkelySkull.SpawnSingle(npc, target, BigSkullScale);
                                skullsSpawnedThisWave = 1;
                                break;
                        }
                    }
                    else if (currentBeamSub == BeamMimicSubPattern.Barrage
                        && Main.netMode != NetmodeID.MultiplayerClient
                        && skullsSpawnedThisWave < SkullBarrageCount
                        && (stateTimer - 1) % SkullBarrageInterval == 0)
                    {
                        SkelySkull.SpawnSingle(npc, target, BigSkullScale);
                        skullsSpawnedThisWave++;
                    }

                    if (stateTimer < BeamMimicActiveDuration - BeamMimicReappearFade)
                    {
                        npc.alpha = 255; // Head tetep ilang selama skull2 masih beraksi
                    }
                    else
                    {
                        // FASE MUNCUL LAGI: fade-in balik di titik dia masuk
                        // portal tadi (dashStartPos), sesudah skull2 selesai.
                        if (stateTimer == BeamMimicActiveDuration - BeamMimicReappearFade)
                        {
                            npc.Center = dashStartPos;
                            if (Main.netMode != NetmodeID.MultiplayerClient)
                                BonePortal.SpawnDashPortal(npc, dashStartPos, target, scaleMul: 1f);
                        }

                        int reappearTick = stateTimer - (BeamMimicActiveDuration - BeamMimicReappearFade);
                        npc.alpha = (int)MathHelper.Lerp(255, 0, MathHelper.Clamp(reappearTick / (float)BeamMimicReappearFade, 0f, 1f));
                    }

                    if (stateTimer >= BeamMimicActiveDuration)
                        ChangeState(npc, State.Recovery);
                    break;

                // === Pattern: Hand Slam ===
                // Head TETEP DI TEMPAT (gak dash/gak ilang, mirip
                // SkullCast/SkullActive) — aktor serangan ini entitas
                // terpisah (SkeletonHandSlam), bukan Head sendiri. Sesudah
                // spawn, Head gak perlu ngatur apa2 lagi — orbit, converge/
                // dash, & shatter-nya diurus sendiri-sendiri sama AI()
                // masing-masing tangan.
                case State.HandSlamCast:
                    npc.dontTakeDamage = true; // masih INVULNERABLE, fase telegraph
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 300f); // FIX: tetep ngejar selama telegraph, konsisten sama pattern lain

                    if (stateTimer == 1)
                    {
                        // FIX BARU: pilih sub-pattern HandSlam SEKALI di awal
                        // telegraph, sama pola kayak currentSkullSubPattern/
                        // currentBeamSub.
                        currentHandSlamSubPattern = (HandSlamSubPattern)Main.rand.Next(2);
                        TriggerHandSlamTelegraph(npc); // dust cue "manggil sepasang/beberapa tangan"
                    }

                    if (stateTimer >= HandSlamCastDuration)
                        ChangeState(npc, State.HandSlamActive);
                    break;

                case State.HandSlamActive:
                    npc.dontTakeDamage = false; // VULNERABLE, window damage utama pattern ini
                    MoveTowardPlayer(npc, target, speed: 10f, keepDistance: 300f);

                    if (stateTimer == 1)
                        handsSpawnedThisWave = 0; // reset tiap kali masuk fase active baru

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        switch (currentHandSlamSubPattern)
                        {
                            case HandSlamSubPattern.ClapPair:
                                // spawn portal + 2 tangan sekaligus BARENGAN
                                // di tick pertama — sub-pattern PERTAMA
                                // (rework): dua tangan muncul, muterin player
                                // bareng-bareng, baru dash konvergen ke player
                                // ("mengepakkan") lalu pecah. Timing orbit &
                                // converge-nya diurus sendiri di
                                // SkeletonHandSlam.AI, Head cuma spawn sekali.
                                if (stateTimer == 1)
                                    SkeletonHandSlam.SpawnPair(npc, target);
                                break;

                            case HandSlamSubPattern.DashQuad:
                                // sub-pattern KEDUA: 4 tangan muncul SATU-SATU
                                // tiap HandDashSpawnInterval, mirip logic
                                // Barrage5/PortalCast — tangan pertama di
                                // tick 1, sisanya nyusul.
                                if (handsSpawnedThisWave < HandDashCount
                                    && (stateTimer - 1) % HandDashSpawnInterval == 0)
                                {
                                    SkeletonHandSlam.SpawnDashHand(npc, target);
                                    handsSpawnedThisWave++;
                                }
                                break;
                        }
                    }

                    if (stateTimer >= CurrentHandSlamActiveDuration)
                        ChangeState(npc, State.Recovery);
                    break;
            }
        }

        void ChangeState(NPC npc, State next)
        {
            state = next;
            stateTimer = 0;
            npc.netUpdate = true; // paksa sync SEGERA ke semua client, jangan nunggu periodic sync
        }

        // === FIX BARU: despawn kalau semua player mati ===
        // Bukan "dibunuh" — sengaja gak lewat npc.checkDead()/life=0, biar
        // gak ke-trigger loot, downedBoss3, atau efek kematian apa pun.
        // Cukup dipaksa lewat jalur despawn vanilla yang sama kayak NPC
        // biasa ilang pas gak ada player valid di deketnya: npc.timeLeft
        // dipepetin ke hampir 0, dan CheckActive() di atas udah balikin
        // false selama gak ada player hidup (jadi timer ini gak di-reset
        // ulang sama proximity check vanilla). Begitu timeLeft abis, vanilla
        // sendiri yang nge-set active=false + sync ke semua client — gak
        // perlu kirim paket kill manual dari sini.
        void DespawnBoss(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return; // despawn cuma authoritative (server/singleplayer)

            npc.timeLeft = System.Math.Min(npc.timeLeft, 10);
            npc.netUpdate = true;
        }

        // === FIX BARU: Dungeon Guardian (subuh) ===
        // Dipanggil SEKALI pas transisi masuk mode ini (lihat RunStateMachine).
        void ApplyDungeonGuardianForm(NPC npc)
        {
            npc.defense = DungeonGuardianDefense;
            npc.damage = DungeonGuardianDamage;
            npc.dontTakeDamage = false; // tetep bisa "dikalahin" kalau emang nekat & sabar — sama kayak vanilla, walau nyaris mustahil berkat defense di atas
            npc.knockBackResist = 0f;

            // FIX: kalau dawn kebetulan nyamber pas lagi di tengah state
            // yang lagi fade (PreCastReposition/ShatterCast dsb, keduanya
            // ngoprek npc.alpha), paksa balik full-opaque biar gak nyangkut
            // "transparan selamanya" begitu masuk mode Guardian.
            npc.alpha = 0;

            SoundEngine.PlaySound(SoundID.Roar, npc.Center); // TODO: ganti sound custom "enrage/sunrise" kalau asetnya udah ada

            npc.netUpdate = true; // sync stat & mode barunya SEGERA ke semua client
        }

        // Perilaku permanen sesudah masuk mode Dungeon Guardian: ngejar
        // TERUS tanpa jaga jarak (kontak = damage one-shot), jauh lebih
        // ngebut dari mode kejar normal, gak ada telegraph/windup apa pun
        // lagi — dan kepalanya muter kenceng terus-terusan niru "head spin
        // attack" versi vanilla.
        void RunDungeonGuardian(NPC npc, Player target)
        {
            MoveTowardPlayer(npc, target, speed: DungeonGuardianSpeed, keepDistance: 0f);

            npc.rotation += DungeonGuardianSpinSpeed * (npc.spriteDirection >= 0 ? 1f : -1f);

            // Jaga2: paksa ulang tiap tick kalau2 defense/damage sempet
            // ke-override dari tempat lain (mis. hook ExpertMode/scaling
            // NPC lain yang jalan belakangan tiap tick).
            npc.defense = DungeonGuardianDefense;
            npc.damage = DungeonGuardianDamage;
        }

        // === Efek Roar ===

        void TriggerRoarStart(NPC npc)
        {
            // TODO: ganti ke SoundStyle custom begitu asset audio siap, contoh:
            // SoundEngine.PlaySound(SkeletronReworkSounds.Roar with { Volume = 1f, PitchVariance = 0.1f }, npc.Center);
            // Sementara pakai placeholder vanilla biar bisa dites dulu:
            SoundEngine.PlaySound(SoundID.Roar, npc.Center);

            // Shock kuat pas mulai raung (1 kali, lebih besar dari rumble berkelanjutan di bawah)
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                npc.Center, Vector2.UnitY, 8f, 6f, 20, 1000f, "SkeletronRoarStart"));

            SpawnRoarDustRing(npc);
        }

        void TriggerRumbleTick(NPC npc)
        {
            // getaran ringan berulang, arah random tiap tick biar berasa "gemetar"
            // bukan dorongan terarah kayak shock awal
            Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                npc.Center, Main.rand.NextVector2Unit(), 3f, 4f, 10, 1000f,
                "SkeletronRumble" + Main.rand.Next(1000)));
        }

        void SpawnRoarDustRing(NPC npc)
        {
            if (Main.netMode == NetmodeID.Server) return; // visual-only, skip di dedicated server

            const int dustCount = 24;
            for (int i = 0; i < dustCount; i++)
            {
                float angle = MathHelper.TwoPi / dustCount * i;
                Vector2 dir = angle.ToRotationVector2();
                Vector2 dustPos = npc.Center + dir * 20f;

                Dust d = Dust.NewDustPerfect(dustPos, ModContent.DustType<BoneChipDust>(), dir * 6f);
                d.noGravity = true;
                d.scale = 1.4f;
                d.fadeIn = 0.5f;
            }
        }

        // Cue visual buat pattern 2 (bone throw): dust ke-tarik MASUK ke
        // tengah Head sesaat sebelum lemparan, kesan "narik tenaga" sebelum
        // ngeluarin 3 ThrownBone sekaligus. Beda dari SpawnRoarDustRing yang
        // dust-nya keluar/expanding — ini kebalik, dust-nya inward.
        void TriggerThrowWindupEffect(NPC npc)
        {
            if (Main.netMode == NetmodeID.Server) return; // visual-only, skip di dedicated server

            const int dustCount = 16;
            for (int i = 0; i < dustCount; i++)
            {
                float angle = MathHelper.TwoPi / dustCount * i;
                Vector2 dir = angle.ToRotationVector2();
                Vector2 dustPos = npc.Center + dir * 60f;

                Dust d = Dust.NewDustPerfect(dustPos, ModContent.DustType<VoidSparkDust>(), -dir * 5f);
                d.noGravity = true;
                d.scale = 1.1f;
                d.fadeIn = 0.4f;
            }
        }

        // Cue visual buat pattern 3 (skull blaster): mirip
        // TriggerThrowWindupEffect (dust inward), tapi ditintain biru muda
        // biar senada sama ChargeBlaster, dan radiusnya lebih lebar — kesan
        // "manggil" skull dari kejauhan, bukan cuma narik tenaga di tangan.
        void TriggerSkullTelegraph(NPC npc)
        {
            if (Main.netMode == NetmodeID.Server) return; // visual-only, skip di dedicated server

            const int dustCount = 20;
            for (int i = 0; i < dustCount; i++)
            {
                float angle = MathHelper.TwoPi / dustCount * i;
                Vector2 dir = angle.ToRotationVector2();
                Vector2 dustPos = npc.Center + dir * 70f;

                Dust d = Dust.NewDustPerfect(dustPos, ModContent.DustType<VoidSparkDust>(), -dir * 4f);
                d.noGravity = true;
                d.color = new Color(150, 210, 255); // biru muda, senada charge blaster
                d.scale = 1f;
                d.fadeIn = 0.4f;
            }
        }

        // Cue visual buat pattern BARU (Hand Slam): dust tulang
        // (BoneChipDust) ketarik dari DUA SISI (kiri & kanan Head), kesan
        // "manggil sepasang tangan" dari dua arah sebelum portalnya kebuka
        // — beda dari TriggerThrowWindupEffect/TriggerSkullTelegraph yang
        // cuma satu ring di tengah.
        void TriggerHandSlamTelegraph(NPC npc)
        {
            if (Main.netMode == NetmodeID.Server) return; // visual-only, skip di dedicated server

            const int dustPerSide = 10;
            float[] sides = { -1f, 1f };
            foreach (float side in sides)
            {
                for (int i = 0; i < dustPerSide; i++)
                {
                    Vector2 dustPos = npc.Center + new Vector2(side * 90f, Main.rand.NextFloat(-40f, 40f));
                    Vector2 pullDir = (npc.Center - dustPos) * 0.08f;

                    Dust d = Dust.NewDustPerfect(dustPos, ModContent.DustType<BoneChipDust>(), pullDir);
                    d.noGravity = true;
                    d.scale = 1f;
                    d.fadeIn = 0.4f;
                }
            }
        }

        // FIX: dulu gerakannya lurus kaku (lerp murni ke arah player).
        // Sekarang ditambahin "jiggle" — goyangan kecil tegak lurus arah
        // gerak (perp * wobble) plus sedikit rotasi bolak-balik biar Head
        // keliatan gelombang/goyang kayak tulang yang gak sepenuhnya kaku,
        // bukan translasi lurus doang. Lerp ke kecepatan juga dipelanin
        // (0.05 -> 0.035) biar responnya berat/lambat, bukan langsung
        // ngikutin player secepat itu juga.
        void MoveTowardPlayer(NPC npc, Player target, float speed, float keepDistance)
        {
            jiggleTimer += 0.09f;
            float wobble = (float)System.Math.Sin(jiggleTimer) * 0.6f;

            Vector2 toTarget = target.Center - npc.Center;
            if (toTarget.Length() > keepDistance)
            {
                Vector2 dir = toTarget.SafeNormalize(Vector2.UnitX);
                Vector2 perp = dir.RotatedBy(MathHelper.PiOver2);
                Vector2 desiredVel = dir * speed + perp * wobble;
                npc.velocity = Vector2.Lerp(npc.velocity, desiredVel, 0.28f); // FIX: dinaikin lagi (0.035f -> 0.09f -> 0.16f -> 0.22f -> 0.28f)
            }
            else
            {
                npc.velocity *= 0.95f;
            }

            // FIX: PreAI return false = AI vanilla (termasuk update
            // direction/spriteDirection & animasi) SAMA SEKALI gak jalan.
            // Tanpa ini skull gak pernah "noleh" ke arah gerak/player, jadi
            // kesannya cuma digeser-geser kayak stiker, bukan gerak. Sekarang
            // dia flip ngadep arah horizontal gerak, dan rotation-nya nge-
            // bank dikit sesuai velocity (bukan cuma wobble sinus doang),
            // biar ada kesan "melayang" yang natural.
            if (System.Math.Abs(npc.velocity.X) > 0.15f)
            {
                npc.spriteDirection = npc.velocity.X > 0 ? 1 : -1;
                npc.direction = npc.spriteDirection;
            }

            float bank = MathHelper.Clamp(npc.velocity.X * 0.025f, -0.3f, 0.3f);
            npc.rotation = bank + (float)System.Math.Sin(jiggleTimer * 1.4f) * 0.06f;
        }

        // === PHASE 2: BeamMimic sekarang cukup pakai SkelySkull.SpawnPair/
        // SpawnSingle langsung (lihat State.BeamMimicActive) — gak perlu
        // logic reposisi/orbit/barrage manual lagi di sini, karena semua
        // itu udah ditangani sendiri oleh AI() SkelySkull.


        static float EaseOutCubic(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            float f = t - 1f;
            return f * f * f + 1f;
        }

        // Visual cue biar player tau kenapa hit-nya gak masuk pas invulnerable
        public override void DrawEffects(NPC npc, ref Color drawColor)
        {
            if (isDungeonGuardian)
                // FIX BARU: tint merah nyala, beda dari tint biru
                // invulnerable biasa — biar keliatan JELAS ini udah mode
                // "ngamuk" permanen (Dungeon Guardian), bukan cuma windup
                // pattern biasa yang bakal lewat.
                drawColor = Color.Lerp(drawColor, new Color(255, 40, 40), 0.5f);
            else if (npc.dontTakeDamage)
                drawColor = Color.Lerp(drawColor, new Color(120, 180, 255), 0.4f);
        }

        // === Multiplayer sync ===
        // state & stateTimer dikirim tiap kali npc.netUpdate = true di-trigger
        // (lihat ChangeState di atas) — jadi begitu Head ganti state, semua
        // client langsung dapet update, gak nunggu sync periodik biasa.
        //
        // Late-join: player yang connect di tengah fight otomatis dapet data
        // ini juga, karena tModLoader ngirim full NPC sync (termasuk hasil
        // SendExtraAI ini) sebagai bagian dari initial world sync pas mereka
        // join — gak perlu handling manual tambahan.
        //
        // portalsSpawnedThisWave & skullsSpawnedThisWave sengaja TIDAK
        // disync: cuma dipakai buat logging/scaling internal di server, gak
        // memengaruhi apa yang dirender atau dicek client. Kalau nanti
        // dipakai buat logic yang keliatan ke player (misal jumlah portal/
        // skull berubah di HP rendah), baru perlu ditambahin ke sini.
        //
        // currentSkullSubPattern JUGA sengaja gak disync eksplisit di sini —
        // dia cuma nentuin CARA SERVER manggil SkelySkull.SpawnPair/
        // SpawnSingle (spawn selalu server-authoritative, lihat guard
        // Main.netMode != MultiplayerClient di semua pemanggilnya), jadi
        // client gak butuh tau nilainya buat nampilin apa pun secara benar.
        //
        // CATATAN FIX: signature SendExtraAI/ReceiveExtraAI versi tModLoader
        // sekarang butuh parameter tambahan BitWriter/BitReader (dipakai buat
        // packing beberapa bool jadi 1 byte kalau perlu). Kita gak pakai bit
        // packing di sini jadi parameternya cuma didiemin aja, tapi WAJIB ada
        // di signature-nya biar override valid (CS0115 kalau ilang).
        public override void SendExtraAI(NPC npc, BitWriter bitWriter, System.IO.BinaryWriter binaryWriter)
        {
            binaryWriter.Write((byte)state);
            binaryWriter.Write((short)stateTimer);
            binaryWriter.Write(isPhase2); // FIX: perlu disync — late-joiner/client lain harus tau boss udah phase 2
                                           // walau state SEKARANG kebetulan bukan salah satu state phase 2
                                           // (misal lagi Recovery/RoarTelegraph biasa sesudah transisi)
            binaryWriter.Write(isDungeonGuardian); // FIX BARU: sama alasannya kayak isPhase2 di atas — late-joiner/client
                                                    // lain harus tau boss udah permanen jadi Dungeon Guardian
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, System.IO.BinaryReader binaryReader)
        {
            state = (State)binaryReader.ReadByte();
            stateTimer = binaryReader.ReadInt16();
            isPhase2 = binaryReader.ReadBoolean();
            isDungeonGuardian = binaryReader.ReadBoolean();
        }
    }
}