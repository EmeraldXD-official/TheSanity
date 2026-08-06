using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Common.Utilities;
using TheSanity.Projectiles;

namespace YourModName.Content.NPCs
{
    // Snapshot 1 titik di trail after-image. Ditaruh di level namespace (bukan di
    // dalam class) supaya gampang dipakai bareng di file lain kalau suatu saat perlu.
    public struct TwinsAfterimageSnapshot
    {
        public Vector2 Center;
        public float Rotation;
        public Rectangle Frame;
        public int SpriteDirection;
    }

    public class TwinsReworkOverride : GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Glow mata Retinazer TIDAK ada di sistem GlowMaskID (yang berbasis index angka) —
        // dia termasuk kategori "special-named glow sprite" vanilla dengan nama internal
        // "Eye_Laser". Di-load dengan try-catch supaya kalau ternyata nama/path asset-nya
        // beda dari dugaan kita, game tidak crash — otomatis null dan kita fallback aman.
        private static Asset<Texture2D> RetinazerGlowTexture;

        // Sprite starburst buat Eye Laser Flash — numpang tekstur VANILLA proyektil
        // "Rainbow" (item Rainbow Rod, ProjectileID.RainbowRodBullet). Sprite ini
        // kebetulan emang udah kebentuk starburst putih polos (bukan pelangi/gradient
        // kayak namanya - warnanya di-cycle lewat kode, bukan lewat sprite), jadi TINGGAL
        // DI-TINT MERAH pas draw, gampang bersih (beda kasus sama PhantasmalBolt yang
        // dasarnya cyan - lihat komentar RedTint di RedPhantasmalBolt.cs). Di-load
        // terpisah (try-catch sendiri) dari RetinazerGlowTexture di atas, biar kalau
        // salah satu gagal load, yang lain tetap jalan normal.
        private static Asset<Texture2D> EyeFlashStarTexture;

        // ==========================================
        // STATE UNTUK PATTERN ATTACK (dibaca/ditulis dari TwinDash.cs & TwinsRetBeam.cs)
        // ==========================================
        // Catatan penting soal multiplayer: field-field public di bawah ini TIDAK
        // ikut disinkron otomatis oleh vanilla (beda dari npc.ai[0]/ai[1] yang emang
        // udah ke-handle vanilla via npc.netUpdate). Untuk versi sekarang ini cukup
        // buat singleplayer / testing. Kalau nanti mau dipakai serius di server MP,
        // field-field ini perlu di-sync manual lewat ModPacket.
        public Vector2 TelegraphDirection;
        public bool IsTelegraphing;
        public bool IsDashing;
        public int DashRepeatsRemaining;

        // ==========================================
        // PHASE 3 / "ENRAGED" — true PERMANEN begitu kedua Spectre clone (spawn dari Phase 2)
        // tumbang (lihat TwinsPhaseTransition.HoldWhileSpectresAlive). Selama flag ini true,
        // SEMUA pattern attack dapet upgrade tambahan (lihat komentar di masing-masing file
        // pattern: TwinDash, TwinsRetBeam, TwinsCursedRain/TwinsCursedBeam, TwinsSpinningCurse,
        // TwinsBorderShot, TwinsLaserBarrage) — bukan pattern baru, cuma versi "lebih ganas"
        // dari pattern yang udah ada.
        // ==========================================
        public bool IsEnraged;

        // Akumulator jarak tempuh (px) sejak terakhir kali TwinDash nembak sepasang
        // RedPhantasmalBolt ke kanan-kiri selama Dashing (cuma dipakai kalau IsEnraged).
        // Di-reset ke 0 tiap kali dash BARU dimulai (lihat TwinDash.Pattern1, transisi
        // Telegraph -> Dashing).
        public float DashBoltDistanceAccum;

        // (Field burst random/rosette lama udah dibuang - lihat TwinsSpinningCurse.cs,
        // sekarang bolt-nya SELARAS sama trigger CursedFlame, gak perlu timer terpisah.)

        // ==========================================
        // DISPATCHER — nentuin pattern mana yang lagi aktif (Dash atau RetBeam), biar
        // urutannya bergantian: Dash (siklus penuh, hover+3-5x dash) -> RetBeam (siklus
        // penuh, 4-7x aim+beam) -> Dash lagi -> dst. Mulai dari Dash secara default.
        // ==========================================
        public enum SpazPattern
        {
            Dash,
            RetBeam,
            CursedRain,
            SpinningCurse,
            BorderShot,
            LaserBarrage,
            SplitCombo // PATTERN PALING TERAKHIR di rotasi - lihat TwinsSplitCombo.cs
        }
        public SpazPattern CurrentPattern = SpazPattern.Dash;

        // ==========================================
        // CONTACT DAMAGE (npc.damage) - dinamis, di-update tiap tick di PreAI: 20 normal,
        // 120 SELAMA pattern Dash atau BorderShot (baik di rotasi normal maupun di dalam
        // Last Stand). Lihat pengaturannya di awal blok PreAI Spazmatism.
        // ==========================================
        private const int ContactDamageNormal = 7;             // Contact: target 20 DMG Master / 3 (engine auto-triples npc.damage di Master mode)
        private const int ContactDamageDashOrBorderShot = 40;  // Contact (dash/BorderShot): target 120 DMG Master / 3

        // ==========================================
        // STATE UNTUK EVENT PHASE TRANSITION 50% HP (dibaca/ditulis dari
        // TwinsPhaseTransition.cs) — lihat komentar panjang di file itu buat alur lengkapnya.
        // Sekarang 2 TAHAP: Armed (invincible langsung, pattern lama tetap diselesaikan)
        // -> Triggered/SpectresActive (snap ke tengah, arena melebar, spawn Spectre, Twin
        // semi-transparent) begitu siklus pattern yang lagi jalan itu kelar.
        // ==========================================
        // True SELAMA window "sudah nyentuh 50% HP tapi masih nunggu pattern lama kelar
        // sebelum Phase 2 beneran mulai" (lihat TwinsPhaseTransition.Arm/ShouldArm). Balik
        // false lagi begitu Trigger() jalan.
        public bool PhaseTwoArmed;

        // ==========================================
        // FIX: sebelumnya Twin original TELEPORT instan ke tengah arena pas Trigger() -
        // sekarang dipecah jadi fase "terbang pelan-pelan ke tengah" dulu (lihat
        // TwinsPhaseTransition.TickMovingToCenter), BARU begitu beneran nyampe baru expand
        // arena + spawn Spectre + masuk PhaseTwoSpectresActive. Ini juga yang nyelesein bug
        // after-image "nyangkut" di posisi lama (dulu trail buffer-nya masih isi posisi
        // SEBELUM teleport, padahal Twin-nya udah instan pindah jauh).
        // ==========================================
        public bool PhaseTwoMovingToCenter;
        public float PhaseTwoMoveTimer;

        // True PERMANEN begitu event ini ke-trigger sekali (gak bisa ke-trigger ulang lagi).
        public bool PhaseTwoTriggered;
        // True SELAMA kedua Spectre clone masih hidup — dispatcher pattern normal & render
        // solid Twin original dua-duanya baca flag ini.
        public bool PhaseTwoSpectresActive;
        public int SpectreSpazIndex = -1;
        public int SpectreRetIndex = -1;

        // Radius arena SEBELUM di-expand 40% pas Trigger() (lihat TwinsPhaseTransition.ExpandArena) —
        // disimpen di sini biar begitu kedua Spectre tumbang, arena bisa di-animasikan MENGECIL
        // BALIK ke ukuran semula (bukan nyangkut di ukuran yang udah di-expand selamanya).
        public float PhaseTwoPreExpandArenaRadius;

        // ==========================================
        // LAST STAND (HP <= 1%) — event SEKALI PAKAI TERAKHIR, TERPISAH TOTAL dari rotasi
        // pattern normal DAN dari Phase 2 (50% HP). Lihat TwinsLastStand.cs buat alur lengkapnya
        // (spawn clone lagi, 1x siklus tiap pattern versi enraged, Deathray muter, lalu death
        // animation). Field-field ini semua "generic slot" dibaca/ditulis dari TwinsLastStand.cs.
        // ==========================================
        public bool LastStandTriggered;  // permanen true begitu event ini mulai, gak pernah re-trigger
        public bool LastStandActive;     // true dari Trigger() sampai proses kill manual di akhir death animation
        public float LastStandStateRaw;
        public float LastStandTimer;
        public int LastStandSpectreSpazIndex = -1;
        public int LastStandSpectreRetIndex = -1;
        public bool LastStandHealthDrainStarted; // true sejak momen Roar+refill (drain & timer master mulai jalan)
        public float LastStandHealthTimer;        // hitung naik dari 0 sampai cap 60 detik sejak Roar+refill
        public Vector2 LastStandArenaCenter;

        // True SELAMA Twin diam nunggu kedua Spectre clone Last Stand tumbang - dibaca render
        // (PreDraw) buat bikin Twin original semi-transparent, sama kayak PhaseTwoSpectresActive.
        public bool LastStandHoldingForSpectres;

        // Rotating "Deathray" beam (CW -> smooth transition -> CCW).
        public float LastStandDeathrayAngle;
        public float LastStandDeathrayAccumAngle;
        public int LastStandDeathrayRepsTarget;
        public bool LastStandDeathrayActive; // dibaca render (PreDraw) buat nampilin garis beam-nya
        public float LastStandBoltTimer;

        // Death animation (jatuh + ledakan harmless selama 5 detik SETELAH landing, sebelum
        // kill manual). LastStandHasLanded jadi penanda kapan timer 5 detik itu MULAI ngitung
        // (begitu Twin beneran nyentuh block, bukan dari saat gravity baru diaktifin).
        public bool LastStandHasLanded;
        public float LastStandNextExplosionCountdown;

        // ==========================================
        // FIX: budget waktu master (HealthDrainDurationTicks) SEKARANG CUMA nandain "pending"
        // di sini - TIDAK LAGI motong paksa StartDeathAnimation() di tengah pattern/spin
        // manapun. Eksekusi berhenti beneran cuma boleh di checkpoint aman (akhir 1 putaran
        // attack loop penuh, ATAU akhir 1 pasang Deathray CW+CCW penuh) - lihat
        // TwinsLastStand.UpdateHealthDrainAndExpiry & TickDeathraySpin.
        // ==========================================
        public bool LastStandExpiryPending;

        // ==========================================
        // FIX: dulu tiap ganti arah (atau mulai pertama kali) Deathray langsung lompat ke
        // kecepatan penuh instan (gak ada ramp sama sekali). Sekarang SEMUA "mulai spin" -
        // baik start pertama dari ReturnToCenter MAUPUN tiap kali CW<->CCW gantian - lewat
        // 1 state transisi yang sama (DeathrayTransition), yang butuh tau "dari speed factor
        // berapa" (0 = mulai dari diam, +-1 = abis nge-spin arah itu) dan "mau ke arah mana" -
        // 2 field ini yang nyimpen itu. Lihat TwinsLastStand.BeginDeathrayTransition/
        // TickDeathrayTransition.
        // ==========================================
        public float LastStandDeathrayTransitionFromSign;
        public bool LastStandDeathrayTransitionToClockwise;

        // ==========================================
        // STATE UNTUK PATTERN RETBEAM (dibaca/ditulis dari TwinsRetBeam.cs)
        // ==========================================
        // npc.ai[0..3] semuanya udah kepake (state+timer dash, counter+index animasi),
        // makanya pattern baru ini nyimpen state/timer-nya sendiri di field terpisah ini,
        // bukan numpang di ai[] lagi. Sama kayak field-field di atas, ini juga BELUM
        // ke-sync otomatis di multiplayer — cukup buat singleplayer/testing dulu.
        public float RetBeamStateRaw;
        public float RetBeamTimer;
        public Vector2 RetBeamAimDirection;
        public bool RetBeamIsAiming;   // true selama fase "predik" (garis merah masih ngikutin player)
        public bool RetBeamIsFullBeam; // true selama fase beam solid (lagi ngedamage)
        public int RetBeamRepeatsRemaining;
        public Vector2 RetBeamRepositionTarget; // titik tujuan pas State.Reposition (NPC gerak cepat ke sini, bukan teleport)

        // ==========================================
        // STATE UNTUK PATTERN CURSED RAIN (dibaca/ditulis dari TwinsCursedRain.cs)
        // ==========================================
        // Sama kayak RetBeam, pattern ini nyimpen state/timer-nya sendiri di field terpisah
        // (bukan numpang di npc.ai[] yang udah kepake Dash). Juga BELUM ke-sync otomatis di
        // multiplayer — cukup buat singleplayer/testing dulu (lihat catatan yang sama di field
        // RetBeam di atas).
        public float CursedRainStateRaw;
        public float CursedRainTimer;
        public float CursedRainSpinAngle; // progres putaran (radian), 0 s/d 2*PI selama Spinning
        public int CursedRainRepeatsRemaining; // sisa ulangan siklus penuh (3-4x, di-random tiap Start())

        // ==========================================
        // STATE UNTUK PATTERN SPINNING CURSE (dibaca/ditulis dari TwinsSpinningCurse.cs)
        // ==========================================
        public float SpinningCurseStateRaw;
        public float SpinningCurseTimer;
        public float SpinningCurseFireCountdown; // countdown tick sampai tembakan berikutnya, di-lerp makin cepat

        // ==========================================
        // STATE UNTUK PATTERN BORDER SHOT (dibaca/ditulis dari TwinsBorderShot.cs)
        // ==========================================
        public float BorderShotStateRaw;
        public float BorderShotTimer;
        public float BorderShotSpinAngle;
        public float BorderShotStartAngle;
        public float BorderShotNextPlacementAngle;

        // Titik di tepi border yang Twin tuju SEBELUM mulai muter (state Approaching).
        // Di-hitung sekali di Start() dari BorderShotStartAngle (yang sekarang di-random),
        // dan Twin gerak CEPAT ke sini pakai velocity — BUKAN teleport/snap langsung.
        public Vector2 BorderShotApproachTarget;
        public List<Vector2> BorderShotPoints = new List<Vector2>();

        // Jumlah putaran penuh yang WAJIB diselesaikan sebelum pattern ini dianggap Done.
        // Di-random SEKALI tiap kali Start() dipanggil (3-5 putaran) - lihat
        // TwinsBorderShot.Start().
        public int BorderShotLapsRequired;

        // ==========================================
        // STATE UNTUK PATTERN LASER BARRAGE (dibaca/ditulis dari TwinsLaserBarrage.cs)
        // ==========================================
        public float LaserBarrageStateRaw;
        public float LaserBarrageTimer;
        public int LaserBarrageSideIndex;
        public int LaserBarrageShotsFired;
        public Vector2 LaserBarrageTargetSpot;

        // ENRAGED (Phase 3): telegraph/aim buat "RetLaserBeam" abis 20x RedLaser kelar -
        // sama filosofinya kayak RetBeamAimDirection/RetBeamIsAiming, cuma arahnya DIKUNCI
        // SEKALI pas mulai ngincer (bukan terus di-update ngikutin player kayak RetBeam),
        // biar player kebagian jeda buat baca garis & minggir dulu sebelum beam-nya beneran
        // lepas tembak (lihat TwinsLaserBarrage.State.AimingRetLaser).
        public Vector2 LaserBarrageAimDirection;
        public bool LaserBarrageIsAiming;

        // ==========================================
        // STATE UNTUK PATTERN SPLIT COMBO (dibaca/ditulis dari TwinsSplitCombo.cs)
        // ==========================================
        // BEDA dari semua field state pattern lain di atas: field-field di bawah ini adalah
        // SATU-SATUNYA "sumber kebenaran" yang dibaca dari DUA NPC sekaligus (Spazmatism
        // MAUPUN Retinazer) - biasanya Retinazer gak pernah punya state sendiri sama sekali
        // (dia cuma nempel ke Spaz), tapi pattern ini butuh Retinazer BENERAN gerak sendiri.
        // Nilai yang dipakai SELALU dari instance milik Spazmatism (persis kayak semua field
        // pattern lain) - instance milik Retinazer tetap punya field yang sama (soalnya
        // InstancePerEntity = true) tapi isinya gak dipakai/gak berarti apa-apa.
        public float ComboSplitStateRaw;
        public float ComboSplitTimer;
        public float ComboSplitSpinAngle;
        public float ComboSplitStartAngle;
        public float ComboSplitRadius;
        public Vector2 ComboSplitArenaCenter;
        public Vector2 ComboSplitSpazApproachTarget;
        public Vector2 ComboSplitRetApproachTarget;
        public int ComboSplitLapsRequired;
        public float ComboSplitFireTimer;

        // Gerbang utama: begitu true (di-set dari TwinsSplitCombo.Start(), dibaca dari
        // instance Spazmatism), PreAI/FindFrame/PreDraw Retinazer di bawah PINDAH JALUR dari
        // "nempel 1:1 ke Spaz" (default, seumur hidup fight) ke jalur independen yang
        // digerakkan TwinsSplitCombo.RetinazerTick(). Balik false lagi begitu Regrouping
        // kelar (lihat TwinsSplitCombo.Tick, state Regrouping).
        public bool ComboSplitActive;

        // True CUMA selama sub-state Circling pattern ini - Retinazer BENERAN bisa kena
        // damage (dontTakeDamage = false) SELAMA ini true, damage-nya "ditransfer" balik ke
        // npc.life Spazmatism (lihat TwinsSplitCombo.HandleVulnerability) biar tetap 1 pool
        // nyawa, bukan 2 health bar terpisah. Di luar window ini (Separating/Regrouping)
        // Retinazer tetap full invincible seperti biasa.
        public bool ComboSplitVulnerable;

        // Bookkeeping damage SELAMA vulnerable - punya Retinazer sendiri (instance-nya
        // sendiri, BUKAN punya Spazmatism), dipakai TwinsSplitCombo.HandleVulnerability buat
        // ngedeteksi seberapa banyak npc.life Retinazer turun tiap tick.
        public bool ComboSplitDamageTrackingInit;
        public int ComboSplitLastTrackedLife;

        // ==========================================
        // tembak, dan BUKAN body-center) - dihitung LIVE tiap frame dari rotation NPC saat
        // itu (lihat EyeGlowMuzzleOffset & DrawEyeLaserFlash), jadi otomatis selalu "nempel"
        // di depan wajah biarpun Twins lagi gerak/muter cepat - gak perlu nyimpen posisi world
        // yang di-snapshot pas trigger (makanya field EyeFlashPosition yang lama DIHAPUS).
        //
        // ADA 2 MODE:
        //  1. PULSE (one-shot) - dipicu TriggerEyeFlash(), buat tembakan sesaat kayak
        //     TwinsBorderShot & TwinsLaserBarrage. Kedip cepat, membesar-lalu-mengecil,
        //     durasi pendek (EyeFlashDuration).
        //  2. CONTINUOUS - aktif SELAMA RetBeamIsAiming atau RetBeamIsFullBeam true (dibaca
        //     langsung di PreDraw, bukan lewat trigger tick) - nyala terus TANPA fade out
        //     selama laser beam RetBeam masih ada, mati OTOMATIS pas beam-nya hilang
        //     (kedua flag itu balik false). TIDAK dipakai buat Dash - Dash sengaja TIDAK
        //     manggil TriggerEyeFlash lagi (lihat TwinDash.FireDeathLaserVolley), jadi laser
        //     dash gak nyalain glow ini sama sekali.
        public int EyeFlashSpawnTick = -99999;
        private const int EyeFlashDuration = 14; // ~0.23 detik, cuma dipakai mode PULSE

        public void TriggerEyeFlash()
        {
            EyeFlashSpawnTick = (int)Main.GameUpdateCount;
        }

        // ==========================================
        // FIX: dipanggil dari TwinsPhaseTransition.Trigger() & TwinsLastStand.Trigger() -
        // dua-duanya event yang bisa MOTONG pattern yang lagi jalan SEBELUM pattern itu
        // sempat nyelesein fase-nya sendiri (Last Stand malah motong TANPA nunggu siklus
        // kelar sama sekali). Kalau ada flag visual "kontinu" (garis aim, glow, dll) yang
        // lagi true persis pas kepotong, dia bakal NYANGKUT nyala terus SELAMANYA - soalnya
        // Tick() pattern pemilik flag itu gak akan pernah kepanggil lagi begitu event ini
        // ambil alih (PreDraw baca flag-nya doang, gak peduli pattern-nya masih "hidup" atau
        // enggak). Reset paksa semua di sini, dipanggil PERSIS di titik potongnya.
        // ==========================================
        public void ResetTransientPatternVisuals()
        {
            IsTelegraphing = false;
            RetBeamIsAiming = false;
            RetBeamIsFullBeam = false;
            LaserBarrageIsAiming = false;

            // FIX/JAGA-JAGA: kalau event lain (Phase 2 / Last Stand) motong pattern SplitCombo
            // PAS Retinazer lagi kepisah/vulnerable (lihat komentar panjang di
            // TwinsSplitCombo.cs), WAJIB dipaksa balik bersih di sini juga - kalau enggak,
            // Retinazer bisa nyangkut invincible=false / posisi jauh dari Spaz SELAMANYA
            // (Tick() pattern ini gak akan pernah kepanggil lagi begitu event lain ambil
            // alih). Begitu ComboSplitActive balik false, blok PreAI/FindFrame Retinazer di
            // bawah otomatis snap dia balik nempel & invincible lagi mulai tick berikutnya.
            ComboSplitActive = false;
            ComboSplitVulnerable = false;
        }

        // Index proyektil TwinsCursedBeam yang lagi aktif (-1 kalau gak ada). Di-set oleh
        // TwinsCursedRain.FireCursedBeamDown pas nembak, dibaca di PreDraw di bawah buat
        // manual-draw beam-nya SEBELUM sprite Spazmatism/Retinazer — biar beam-nya kegambar
        // DI BAWAH Twins, bukan numpuk di atasnya (proyektil normalnya digambar SETELAH NPC
        // di render-loop vanilla, makanya harus di-hijack manual kayak gini).
        public int ActiveCursedBeamIndex = -1;

        // Buffer posisi historis buat after-image trail. Sekarang selalu ke-record tiap tick
        // (bukan cuma pas IsDashing lagi), biar efeknya selalu keliatan.
        public List<TwinsAfterimageSnapshot> Trail = new List<TwinsAfterimageSnapshot>();
        private const int TrailMaxLength = 6;
        private const int AfterimageSlices = 6; // makin banyak = gradasi makin halus

        // Warna gradasi after-image: ATAS merah, BAWAH hijau lime — sesuai request.
        private static readonly Color AfterimageTopColor = Color.Red;
        private static readonly Color AfterimageBottomColor = new Color(140, 255, 110); // lime muda

        public override void SetStaticDefaults()
        {
            if (Main.dedServ)
                return;

            try
            {
                RetinazerGlowTexture = ModContent.Request<Texture2D>("Terraria/Images/Eye_Laser", AssetRequestMode.ImmediateLoad);
            }
            catch
            {
                // Kalau path/nama asset-nya ternyata meleset, jangan crash — fallback aja
                // ke sprite Retinazer biasa tanpa glowmask tambahan.
                RetinazerGlowTexture = null;
            }

            try
            {
                EyeFlashStarTexture = ModContent.Request<Texture2D>("Terraria/Images/Projectile_" + ProjectileID.RainbowRodBullet, AssetRequestMode.ImmediateLoad);
            }
            catch
            {
                // Kalau ternyata gagal (nama path berubah/dsb), jangan crash — DrawEyeLaserFlash
                // di bawah otomatis fallback ke starburst manual (DrawBloomLine) kalau texture ini null.
                EyeFlashStarTexture = null;
            }
        }

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Spazmatism || entity.type == NPCID.Retinazer;
        }

        // ==========================================
        // HIT SOUND — paksa pakai sound "metal" (mekanik), bukan sound organik default
        // ==========================================
        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.Spazmatism || npc.type == NPCID.Retinazer)
            {
                npc.HitSound = SoundID.NPCHit4; // sound hit metal/mekanik vanilla
            }
        }

        // ==========================================
        // DESPAWN OTOMATIS — SEKARANG DIIZINKAN LAGI, TERMASUK "kabur pas menjelang fajar"
        // (vanilla The Twins emang didesain buat retreat/despawn begitu Main.dayTime jadi
        // true sebelum boss-nya dikalahin, PERSIS kayak Eye of Cthulhu/Skeletron). Logic
        // "kabur" beneran (fly up lalu npc.active = false, TANPA loot) ada di PreAI, bukan
        // di sini — CheckActive di sini cuma ngurus jalur despawn OTOMATIS vanilla (no
        // player nearby / off-screen timeout), BUKAN dawn.
        //
        // SATU-SATUNYA pengecualian: SELAMA Last Stand (LastStandActive) despawn TETAP HARUS
        // diblokir total - itu event finale sekali-pakai (jatuh + ledakan 5 detik lalu mati
        // resmi) yang gak boleh kepotong/ilang diam-diam di tengah jalan oleh despawn
        // otomatis manapun.
        // ==========================================
        public override bool CheckActive(NPC npc)
        {
            if (npc.type == NPCID.Spazmatism)
                return !LastStandActive;

            if (npc.type == NPCID.Retinazer)
            {
                // PENTING: LastStandActive itu field per-instance (InstancePerEntity = true),
                // dan cuma di-Set/dibaca dari sisi Spazmatism (lihat PreAI). "this" di sini
                // adalah instance milik Retinazer sendiri, yang LastStandActive-nya SELALU
                // tetap false - jadi WAJIB nengok ke instance GlobalNPC milik Spazmatism
                // buat tau status Last Stand yang sebenarnya, bukan baca "this.LastStandActive"
                // langsung (itu bakal selalu false & bikin Ret ke-despawn independen).
                int spazIndex = NPC.FindFirstNPC(NPCID.Spazmatism);
                bool lastStandActive = spazIndex != -1
                    && Main.npc[spazIndex].active
                    && Main.npc[spazIndex].GetGlobalNPC<TwinsReworkOverride>().LastStandActive;

                return !lastStandActive;
            }

            return base.CheckActive(npc);
        }

        // ==========================================
        // CONTACT DAMAGE: ignore SELURUH armor/damage reduction player + ngasih paket debuff
        // standar Twins (Broken Armor + Weak + Bleeding) - CUMA buat Twin ORIGINAL
        // (Spazmatism/Retinazer), BUKAN buat Spectre clone (SpectreSpazmatism/
        // SpectreRetinazer itu ModNPC KELAS TERPISAH, gak kena override ini sama sekali -
        // clone tetap pakai contact damage vanilla apa adanya, sesuai request).
        // ==========================================
        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            if (npc.type == NPCID.Spazmatism || npc.type == NPCID.Retinazer)
            {
                modifiers.ArmorPenetration += 999f;
            }
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            if (npc.type == NPCID.Spazmatism || npc.type == NPCID.Retinazer)
            {
                TwinsDebuffGlobalProjectile.ApplyDebuffs(target);
            }
        }

        // ==========================================
        // DAMAGE REDUCTION SELAMA SPINNING CURSE — semua damage yang masuk (dari sumber
        // manapun: melee, ranged, magic, summon, dsb) dipotong 80% (cuma nembus 20%) SELAMA
        // pattern SpinningCurse lagi aktif. Cukup dicek di Spazmatism aja, karena Retinazer
        // sendiri sudah dontTakeDamage = true (lihat PreAI) — semua damage asli emang
        // numpuk ke Spazmatism.
        // ==========================================
        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.Spazmatism && CurrentPattern == SpazPattern.SpinningCurse)
            {
                modifiers.FinalDamage *= 0.2f; // reduction 80%
            }
        }

        public override bool PreAI(NPC npc)
        {
            // ==========================================
            // 1. AI LOGIC UNTUK RETINAZER (FELLOW / ATTACHED)
            // ==========================================
            if (npc.type == NPCID.Retinazer)
            {
                int spazIndex = NPC.FindFirstNPC(NPCID.Spazmatism);
                if (spazIndex != -1 && Main.npc[spazIndex].active)
                {
                    NPC spaz = Main.npc[spazIndex];
                    TwinsReworkOverride spazGlobal = spaz.GetGlobalNPC<TwinsReworkOverride>();

                    // Mirror contact damage SELALU jalan gak peduli lagi mode mirror atau
                    // independen (lihat blok if/else di bawah) - Spazmatism yang ngitung nilai
                    // dinamisnya (20 normal / 120 pas Dash/BorderShot), Retinazer tinggal ikutin.
                    npc.damage = spaz.damage;
                    npc.target = spaz.target;

                    if (spazGlobal.ComboSplitActive)
                    {
                        // ==========================================
                        // JALUR INDEPENDEN (SplitCombo) — SATU-SATUNYA momen di seluruh fight
                        // Retinazer BENERAN gerak sendiri (bukan nempel ke Spaz). Lihat
                        // TwinsSplitCombo.cs buat alur lengkap (Separating -> Circling ->
                        // Regrouping -> Done) dan HandleVulnerability buat gimana damage yang
                        // kena badan Retinazer di sini "ditransfer" balik ke 1 pool HP milik
                        // Spazmatism.
                        // ==========================================
                        npc.hide = false; // digambar pakai sprite vanilla-nya sendiri (lihat PreDraw)

                        Player retTarget = (npc.target >= 0 && npc.target < Main.maxPlayers) ? Main.player[npc.target] : null;
                        if (retTarget != null && retTarget.active && !retTarget.dead)
                        {
                            TwinsSplitCombo.RetinazerTick(npc, spaz, spazGlobal, this, retTarget);
                        }
                        // (kalau target-nya somehow gak valid tick ini, Retinazer cukup diem di
                        // posisi terakhirnya - dispatcher Spazmatism sendiri yang bakal
                        // ngurus buat cabut dari player mati/gak aktif lewat jalur normalnya)
                    }
                    else
                    {
                        // ==========================================
                        // JALUR NORMAL (mirror) — default seumur hidup fight, sama persis
                        // kayak sebelumnya.
                        // ==========================================
                        // Tempelkan Retinazer tepat di posisi, kecepatan, dan rotasi Spazmatism
                        npc.Center = spaz.Center;
                        npc.velocity = spaz.velocity;
                        npc.rotation = spaz.rotation;
                        // (frame-nya diatur di FindFrame, bukan di sini, karena vanilla akan
                        // menimpa frame lagi setelah PreAI selesai)

                        // Buat Retinazer tidak mengambil damage & sembunyikan render bawaan
                        npc.dontTakeDamage = true;
                        npc.hide = true;

                        // SHARED HEALTH: mirror life/lifeMax Ret persis dari Spaz tiap tick.
                        // Ini murni buat "tampilan data" (DPS meter/boss checklist mod lain yang
                        // baca npc.life Retinazer) — sumber kebenaran HP tetap cuma 1: Spazmatism.
                        npc.lifeMax = spaz.lifeMax;
                        npc.life = spaz.life;
                    }
                }
                else
                {
                    // Fallback edge-case (mis. desync MP / Ret sempat lolos tanpa sempat
                    // ke-kill lewat OnKill di bawah). Kalau life-nya masih > 0, matikan
                    // dulu secara "resmi" (checkDead) biar tetep konsisten & gak nyisa
                    // NPC hidup nyasar. Kalau udah 0 (baru aja mati lewat OnKill), cukup
                    // pastikan non-aktif.
                    if (npc.life > 0)
                    {
                        npc.life = 0;
                        npc.HitEffect(0, 10);
                        npc.checkDead();
                    }
                    npc.active = false;
                }

                return false; // Override seluruh vanilla AI Retinazer
            }

            // ==========================================
            // 2. AI LOGIC UNTUK SPAZMATIZM (LEADER / MAIN BOSS)
            // ==========================================
            if (npc.type == NPCID.Spazmatism)
            {
                // Targeting Player
                npc.TargetClosest(true);
                Player target = Main.player[npc.target];

                if (!target.active || target.dead)
                {
                    npc.velocity.Y += 0.2f; // Kabur ke bawah jika player mati
                    return false;
                }

                // ==========================================
                // DAWN DESPAWN — begitu Main.dayTime jadi true (matahari terbit) SEBELUM boss
                // ini dikalahin, Twin "kabur" ke atas lalu beneran despawn TANPA loot - PERSIS
                // behavior vanilla boss lain (Eye of Cthulhu, Skeletron, dst) begitu pagi
                // tiba. Dicek DULUAN sebelum semua dispatcher pattern lain (termasuk Last Stand
                // di bawah), TAPI digerbang !LastStandActive - finale sekali-pakai itu GAK
                // BOLEH kepotong walau udah pagi begitu sudah mulai.
                // ==========================================
                if (Main.dayTime && !LastStandActive)
                {
                    TickDawnDespawn(npc, this, target);
                    return false;
                }

                // ==========================================
                // CONTACT DAMAGE DINAMIS: 20 normal, 120 SELAMA pattern Dash ATAU BorderShot
                // (baik di rotasi normal MAUPUN di dalam Last Stand yang ngulang pattern yang
                // sama) - dicek PALING AWAL di sini (sebelum semua branch phase di bawah)
                // biar SELALU ke-update tiap tick gak peduli lagi di fase mana. Retinazer
                // ikut ngambil nilai yang sama (lihat blok mirroring Retinazer di atas -
                // npc.damage = spaz.damage).
                // ==========================================
                bool inHighContactDamagePattern = LastStandActive
                    ? TwinsLastStand.IsCurrentlyDashOrBorderShot(this)
                    : (CurrentPattern == SpazPattern.Dash || CurrentPattern == SpazPattern.BorderShot);

                npc.damage = inHighContactDamagePattern ? ContactDamageDashOrBorderShot : ContactDamageNormal;

                // ==========================================
                // LAST STAND (HP <= 1%) — PRIORITAS PALING TINGGI, motong TOTAL semua
                // dispatcher lain (termasuk Phase 2) begitu event ini aktif. Lihat
                // TwinsLastStand.cs buat alur lengkapnya.
                // ==========================================
                if (LastStandActive)
                {
                    TwinsLastStand.Tick(npc, this, target);
                    return false;
                }

                if (TwinsLastStand.ShouldTrigger(npc, this))
                {
                    TwinsLastStand.Trigger(npc, this);
                    return false;
                }

                // ==========================================
                // EVENT PHASE TRANSITION 50% HP — dicek TIAP TICK, TERPISAH dari dispatcher
                // pattern normal di bawah, dan bisa MOTONG dispatcher itu total (lihat
                // TwinsPhaseTransition.cs buat alur lengkapnya). SEKARANG 2 TAHAP:
                //   - Begitu HP nyampe <= 50% buat pertama kali -> Arm() sekali: Twin LANGSUNG
                //     invincible detik itu juga, TAPI pattern yang lagi berputar (kalau ada)
                //     DIBIARIN JALAN sampai siklusnya sendiri kelar dulu.
                //   - Tepat pada tick pattern itu SELESAI SATU SIKLUS PENUH (momen yang sama
                //     kayak "gantian ke pattern berikutnya" di rotasi normal) DAN PhaseTwoArmed
                //     masih true -> Trigger() jalan: snap ke tengah arena, arena melebar 40%
                //     (animasi halus), spawn 2 Spectre clone. Pattern berikutnya yang harusnya
                //     mulai (rotasi normal) TIDAK PERNAH sempat jalan - "kepotong antrian".
                //   - SELAMA kedua Spectre masih hidup -> Twin original diem total ngadap
                //     player (tetap invincible, tampil semi-transparent), dispatcher pattern
                //     normal di-skip (return false lebih awal).
                //   - Begitu kedua Spectre tumbang -> flag balik false, damage normal balik
                //     jalan, dan CurrentPattern DIPAKSA RESET ke Dash (bukan lanjut dari
                //     pattern yang sempat kepotong tadi) — lihat HoldWhileSpectresAlive.
                // ==========================================
                if (PhaseTwoMovingToCenter)
                {
                    TwinsPhaseTransition.TickMovingToCenter(npc, this, target);
                    return false; // Twin lagi terbang pelan-pelan ke tengah, belum spawn clone/expand arena
                }

                if (PhaseTwoSpectresActive)
                {
                    TwinsPhaseTransition.HoldWhileSpectresAlive(npc, this, target);
                    return false; // Twin original diem total selama Spectre-nya masih hidup
                }

                if (TwinsPhaseTransition.ShouldArm(npc, this))
                {
                    TwinsPhaseTransition.Arm(npc, this);
                }

                // Seluruh logic pattern attack dipindah ke file terpisah (TwinDash.cs, dan
                // pattern RetBeam ada di TwinsRetBeam.cs) supaya file ini gak kepanjangan pas
                // pattern-nya nambah banyak nanti. File-file itu tinggal baca/tulis field-field
                // di atas (TelegraphDirection dkk) buat Dash, dan field-field RetBeam* buat
                // RetBeam.
                //
                // Dispatcher ini yang gantian manggil
                //   Dash -> RetBeam -> CursedRain -> SpinningCurse -> BorderShot -> LaserBarrage
                //   -> SplitCombo -> Dash -> ...
                // berdasarkan CurrentPattern:
                //   - Selama Dash aktif, Pattern1() dipanggil tiap tick. Begitu dia return true
                //     (satu siklus penuh hover+3-5x dash kelar), pindah ke RetBeam dan panggil
                //     Start() SEKALI buat nyiapin state awalnya.
                //   - Selama RetBeam aktif, Tick() dipanggil tiap tick. Begitu IsDone() true
                //     (siklus 4-7x aim+beam kelar), pindah ke CursedRain dan panggil Start().
                //   - Selama CursedRain aktif, Tick() dipanggil tiap tick. Begitu IsDone() true
                //     (siklus kejar-atas-kepala + muter + beam diulang 3-4x, TIAP ulangan
                //     balik ngepasin ulang posisi di atas kepala player dulu), pindah ke
                //     SpinningCurse dan panggil Start().
                //   - Selama SpinningCurse aktif, Tick() dipanggil tiap tick. Begitu IsDone()
                //     true (ke-tengah arena + 15 detik spin+cursed flame kelar), pindah ke
                //     BorderShot dan panggil Start().
                //   - Selama BorderShot aktif, Tick() dipanggil tiap tick. Begitu IsDone() true
                //     (keliling border + ignite kelar), pindah ke LaserBarrage dan panggil
                //     Start().
                //   - Selama LaserBarrage aktif, Tick() dipanggil tiap tick. Begitu IsDone()
                //     true (4 sisi x 20 tembakan kelar), pindah ke SplitCombo dan panggil
                //     Start().
                //   - Selama SplitCombo aktif (PATTERN PALING TERAKHIR), Tick() dipanggil tiap
                //     tick. Begitu IsDone() true (pisah ke border + 3-6x putaran combo + balik
                //     regroup kelar), pindah BALIK ke Dash dan panggil ResetToHover() biar Dash
                //     mulai bersih dari State.Hovering - rotasi ulang dari awal lagi.
                //
                // patternCycleFinished di-set true PERSIS pada tick manapun pattern yang lagi
                // aktif baru aja nyelesein siklus penuhnya. nextPatternToStart CUMA nyimpen
                // "pattern apa yang HARUSNYA mulai berikutnya" - TIDAK langsung di-Start() di
                // dalam switch ini lagi (lihat komentar panjang di bawah switch, ini FIX buat
                // bug aim-line/glow yang nyangkut pas transisi ke Phase 2).
                bool patternCycleFinished = false;
                SpazPattern? nextPatternToStart = null;

                switch (CurrentPattern)
                {
                    case SpazPattern.Dash:
                        bool dashCycleFinished = TwinDash.Pattern1(npc, this, target);
                        if (dashCycleFinished)
                        {
                            patternCycleFinished = true;
                            nextPatternToStart = SpazPattern.RetBeam;
                        }
                        break;

                    case SpazPattern.RetBeam:
                        TwinsRetBeam.Tick(npc, this, target);
                        if (TwinsRetBeam.IsDone(this))
                        {
                            patternCycleFinished = true;
                            nextPatternToStart = SpazPattern.CursedRain;
                        }
                        break;

                    case SpazPattern.CursedRain:
                        TwinsCursedRain.Tick(npc, this, target);
                        if (TwinsCursedRain.IsDone(this))
                        {
                            patternCycleFinished = true;
                            nextPatternToStart = SpazPattern.SpinningCurse;
                        }
                        break;

                    case SpazPattern.SpinningCurse:
                        TwinsSpinningCurse.Tick(npc, this, target);
                        if (TwinsSpinningCurse.IsDone(this))
                        {
                            patternCycleFinished = true;
                            nextPatternToStart = SpazPattern.BorderShot;
                        }
                        break;

                    case SpazPattern.BorderShot:
                        TwinsBorderShot.Tick(npc, this, target);
                        if (TwinsBorderShot.IsDone(this))
                        {
                            patternCycleFinished = true;
                            nextPatternToStart = SpazPattern.LaserBarrage;
                        }
                        break;

                    case SpazPattern.LaserBarrage:
                        TwinsLaserBarrage.Tick(npc, this, target);
                        if (TwinsLaserBarrage.IsDone(this))
                        {
                            patternCycleFinished = true;
                            nextPatternToStart = SpazPattern.SplitCombo;
                        }
                        break;

                    case SpazPattern.SplitCombo:
                        TwinsSplitCombo.Tick(npc, this, target);
                        if (TwinsSplitCombo.IsDone(this))
                        {
                            patternCycleFinished = true;
                            nextPatternToStart = SpazPattern.Dash;
                        }
                        break;
                }

                // ==========================================
                // FIX: Phase 2 DICEK DULU di sini, SEBELUM pattern berikutnya di-Start().
                // Dulu urutannya kebalik: switch di atas langsung manggil Start() pattern
                // berikutnya (misal TwinsRetBeam.Start() abis Dash kelar) DI DALAM case yang
                // sama, BARU SETELAH itu baru dicek apakah momen ini harusnya Phase 2. Itu
                // bikin pattern berikutnya sempat nge-set flag visual "kontinu"-nya duluan
                // (misal RetBeamIsAiming = true di TwinsRetBeam.Start(), plus mainin sound
                // aim-nya) padahal Tick() pattern itu GAK AKAN PERNAH kepanggil lagi (dispatcher
                // di-skip total begitu PhaseTwoMovingToCenter/PhaseTwoSpectresActive true mulai
                // tick berikutnya) - hasilnya garis/glow itu nyangkut nyala terus, gak pernah
                // ke-reset, sampai... yaa gak akan pernah ke-reset sendiri.
                //
                // Sekarang: kalau Phase 2 mau trigger tepat tick ini, pattern berikutnya SAMA
                // SEKALI TIDAK di-Start() - ResetTransientPatternVisuals() (dipanggil dari
                // dalam TwinsPhaseTransition.Trigger()) yang jamin semua flag visual balik
                // bersih. Kalau enggak, baru CurrentPattern di-set & Start()/ResetToHover()
                // pattern berikutnya dipanggil seperti biasa.
                // ==========================================
                if (PhaseTwoArmed && patternCycleFinished)
                {
                    TwinsPhaseTransition.Trigger(npc, this);
                }
                else if (nextPatternToStart.HasValue)
                {
                    CurrentPattern = nextPatternToStart.Value;
                    switch (CurrentPattern)
                    {
                        case SpazPattern.RetBeam:       TwinsRetBeam.Start(npc, this); break;
                        case SpazPattern.CursedRain:    TwinsCursedRain.Start(this); break;
                        case SpazPattern.SpinningCurse: TwinsSpinningCurse.Start(npc, this); break;
                        case SpazPattern.BorderShot:    TwinsBorderShot.Start(npc, this); break;
                        case SpazPattern.LaserBarrage:  TwinsLaserBarrage.Start(npc, this); break;
                        case SpazPattern.SplitCombo:    TwinsSplitCombo.Start(npc, this); break;
                        case SpazPattern.Dash:          TwinDash.ResetToHover(npc); break;
                    }
                }

                return false; // Override seluruh vanilla AI Spazmatism
            }

            return true;
        }

        // ==========================================
        // HELPER — DAWN DESPAWN: Twin kabur ke atas layar begitu pagi tiba (Main.dayTime),
        // lalu beneran despawn TANPA loot/downed-flag sama sekali begitu udah cukup jauh
        // dari target. Cuma dipanggil dari sisi Spazmatism (leader), Retinazer selalu
        // ikut ke-nonaktifin manual di sini juga di tick yang sama - BUKAN lewat
        // checkDead()/OnKill (itu jalur "kalah beneran" yang ngedrop loot), jadi dawn
        // escape harus lewat npc.active = false polos buat KEDUANYA.
        // ==========================================
        private const float DawnFleeAcceleration = 0.15f; // makin lama makin kencang "kabur"-nya
        private const float DawnFleeMaxSpeed = 24f;
        private const float DawnDespawnDistance = 4000f; // udah sejauh ini dari target -> dianggap "kabur", beneran despawn

        private static void TickDawnDespawn(NPC npc, TwinsReworkOverride self, Player target)
        {
            // Gak bisa digebukin lagi selama proses kabur, sama kayak retreat boss vanilla
            // lain (EoC/Skeletron) - dan matiin garis telegraph yang mungkin lagi nongol biar
            // gak nyangkut nampil pas Twin udah terbang menjauh.
            npc.dontTakeDamage = true;
            self.IsTelegraphing = false;

            // Kabur lurus ke atas, akselerasi pelan-pelan sampai mentok kecepatan maksimal -
            // biar keliatan "kabur" bukan langsung ngebut instan.
            npc.velocity.Y -= DawnFleeAcceleration;
            if (npc.velocity.Y < -DawnFleeMaxSpeed)
                npc.velocity.Y = -DawnFleeMaxSpeed;
            npc.velocity.X *= 0.98f;
            npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

            // Begitu udah cukup jauh dari player yang lagi diincar - dicek pakai JARAK
            // (bukan nunggu literally keluar dari layar, lebih reliable karena ukuran layar
            // beda-beda tiap player) - beneran despawn.
            if (Vector2.DistanceSquared(npc.Center, target.Center) >= DawnDespawnDistance * DawnDespawnDistance)
            {
                npc.active = false;

                int retIndex = NPC.FindFirstNPC(NPCID.Retinazer);
                if (retIndex != -1)
                    Main.npc[retIndex].active = false;
            }

            npc.netUpdate = true;
        }

        // ==========================================
        // KEMATIAN — Ret WAJIB mati "resmi" bareng Spaz, bukan sekadar despawn
        // ==========================================
        // OnKill dipanggil vanilla PERSIS pas sebuah NPC resmi kalah (life <= 0 dan sudah
        // lewat checkDead()) — bukan pas dia sekadar despawn/dihapus diam-diam. Dengan
        // hook ini, begitu Spazmatism kalah, Retinazer langsung dipaksa lewat proses mati
        // yang sama (life = 0 lalu checkDead()) di tick yang sama juga.
        public override void OnKill(NPC npc)
        {
            if (npc.type == NPCID.Spazmatism)
            {
                // PENTING: pas hook OnKill ini jalan, vanilla BELUM sempat nge-set
                // npc.active = false buat Spazmatism sendiri (itu baru kejadian setelah
                // NPCLoot/OnKill selesai semua). Kalau kita langsung bunuh Retinazer di
                // sini tanpa baris di bawah ini, logic downed-flag vanilla punya Twins
                // (yang cek "apakah si kembar masih NPC.AnyNPCs aktif?") bakal ngebaca
                // Spazmatism SEOLAH-OLAH masih hidup, karena active-nya belum ke-flip.
                // Solusinya: paksa flip active = false di sini duluan, SEBELUM Retinazer
                // dibunuh, biar pengecekan "apakah kembarannya masih hidup" versi
                // Retinazer membaca hasil yang benar (jadi flag "Defeated" ke-set beneran).
                npc.active = false;

                int retIndex = NPC.FindFirstNPC(NPCID.Retinazer);
                if (retIndex != -1 && Main.npc[retIndex].active && Main.npc[retIndex].life > 0)
                {
                    NPC ret = Main.npc[retIndex];
                    ret.life = 0;
                    ret.HitEffect(0, 10); // efek visual/sound kematian standar
                    ret.checkDead();      // ini yang bikin kill-nya "resmi" (loot, netmessage, downed-flag, dll)
                }
            }
        }

        // ==========================================
        // HEALTH BAR — cuma tampilkan 1 bar HP (punya Spazmatism), Ret disembunyikan
        // ==========================================
        public override bool? DrawHealthBar(NPC npc, byte hbPosition, ref float scale, ref Vector2 position)
        {
            if (npc.type == NPCID.Retinazer)
                return false;

            return null; // Spazmatism tetap pakai behavior default vanilla
        }

        // ==========================================
        // FRAME/ANIMASI — sengaja dipisah dari AI, dijalankan paling akhir
        // ==========================================
        // FindFrame() dipanggil TERPISAH dari AI()/PreAI() setiap tick, dan untuk NPC vanilla
        // seperti Twins, tModLoader tetap menjalankan logic animasi bawaan vanilla-nya duluan
        // (yang membaca npc.ai[] dengan makna berbeda dari state machine kita). Makanya kalau
        // kita atur frame di PreAI, hasilnya ketiban lagi. Override di sini supaya jadi
        // keputusan terakhir/final soal frame yang dipakai.
        public override void FindFrame(NPC npc, int frameHeight)
        {
            if (npc.type == NPCID.Spazmatism)
            {
                npc.frame.Width = TextureAssets.Npc[npc.type].Value.Width;
                npc.frame.Height = frameHeight;

                // PENTING: jangan pakai npc.frame.Y / npc.frameCounter sebagai acuan progres,
                // soalnya vanilla nulis ulang keduanya tiap tick. Progres animasi kita simpan
                // sendiri di ai[2] (counter tick) & ai[3] (index frame 0/1/2), dua slot ini
                // nggak disentuh sama sekali oleh AI vanilla Twins ataupun Pattern1 kita
                // (Pattern1 cuma pakai ai[0] & ai[1]).
                npc.ai[2]++;
                if (npc.ai[2] >= 5f) // Kecepatan animasi frame
                {
                    npc.ai[2] = 0f;
                    npc.ai[3] = (npc.ai[3] + 1f) % 3f; // 0,1,2 -> map ke frame index 3,4,5
                }

                npc.frame.Y = frameHeight * (3 + (int)npc.ai[3]);

                // Rekam snapshot buat after-image trail SETIAP TICK (bukan cuma pas dash lagi),
                // biar efeknya selalu keliatan terus-menerus sesuai request. Buffer-nya sendiri
                // rolling window (TrailMaxLength), jadi ghost paling lama otomatis kebuang.
                //
                // DIKECUALIKAN selama PhaseTwoSpectresActive: Twin original lagi diem total di
                // tengah arena (lihat TwinsPhaseTransition.HoldWhileSpectresAlive), jadi after-image
                // "gerak" gak masuk akal ditampilin pas dia gak kemana-mana - trail-nya DIMATIKAN
                // (gak direkam lagi) selama window ini, bukan cuma diem nunggu buffer lama kosong
                // sendiri (Trail juga langsung di-Clear() sekali pas Trigger(), lihat
                // TwinsPhaseTransition.cs, biar ghost yang udah kebentuk sebelumnya ilang instan).
                if (!PhaseTwoSpectresActive && !LastStandHoldingForSpectres)
                {
                    Trail.Add(new TwinsAfterimageSnapshot
                    {
                        Center = npc.Center,
                        Rotation = npc.rotation,
                        Frame = npc.frame,
                        SpriteDirection = npc.spriteDirection
                    });

                    if (Trail.Count > TrailMaxLength)
                        Trail.RemoveAt(0);
                }
            }
            else if (npc.type == NPCID.Retinazer)
            {
                int spazIndex = NPC.FindFirstNPC(NPCID.Spazmatism);
                bool comboSplit = spazIndex != -1 && Main.npc[spazIndex].active
                    && Main.npc[spazIndex].GetGlobalNPC<TwinsReworkOverride>().ComboSplitActive;

                if (comboSplit)
                {
                    // JALUR INDEPENDEN (SplitCombo) — posisi/rotasi UDAH diatur sendiri di PreAI
                    // (TwinsSplitCombo.RetinazerTick), di sini CUMA ngurus animasi frame-nya
                    // sendiri (bukan numpang frame Spazmatism lagi) - dipakai counter ai[2]/ai[3]
                    // punya Retinazer sendiri (SELALU nganggur/gak dipakai di luar SplitCombo,
                    // aman dipakai di sini).
                    npc.frame.Width = TextureAssets.Npc[npc.type].Value.Width;
                    npc.frame.Height = frameHeight;

                    npc.ai[2]++;
                    if (npc.ai[2] >= 5f)
                    {
                        npc.ai[2] = 0f;
                        npc.ai[3] = (npc.ai[3] + 1f) % 3f;
                    }

                    npc.frame.Y = frameHeight * (3 + (int)npc.ai[3]);
                }
                // Selalu samain persis ke posisi/rotasi/frame Spazmatism, dieksekusi di FindFrame
                // (fase render, dipanggil SETELAH semua NPC selesai AI & gerak untuk tick ini).
                else if (spazIndex != -1 && Main.npc[spazIndex].active)
                {
                    NPC spaz = Main.npc[spazIndex];
                    npc.Center = spaz.Center;
                    npc.rotation = spaz.rotation;
                    npc.frame = spaz.frame;
                }
                else
                {
                    // Fallback andai Spazmatism belum/tidak ada: tetap paksa Phase 2.
                    npc.frame.Width = TextureAssets.Npc[npc.type].Value.Width;
                    npc.frame.Height = frameHeight;
                    npc.frame.Y = frameHeight * 3;
                }

                // After-image Retinazer sendiri — PLEK KETIPLEK sama logic Spazmatism di atas,
                // cuma "Trail" di sini adalah punya instance Retinazer sendiri (this == GlobalNPC
                // instance milik NPC Retinazer, karena InstancePerEntity = true). Sama kayak
                // Spazmatism di atas: DIMATIKAN selama PhaseTwoSpectresActive (dibaca dari
                // TwinsReworkOverride instance Spazmatism, bukan dari "this" - lihat di bawah).
                bool spectresActive = false;
                int spazIndexForTrail = NPC.FindFirstNPC(NPCID.Spazmatism);
                if (spazIndexForTrail != -1 && Main.npc[spazIndexForTrail].active)
                {
                    TwinsReworkOverride spazGlobal = Main.npc[spazIndexForTrail].GetGlobalNPC<TwinsReworkOverride>();
                    spectresActive = spazGlobal.PhaseTwoSpectresActive || spazGlobal.LastStandHoldingForSpectres;
                }

                if (!spectresActive)
                {
                    Trail.Add(new TwinsAfterimageSnapshot
                    {
                        Center = npc.Center,
                        Rotation = npc.rotation,
                        Frame = npc.frame,
                        SpriteDirection = npc.spriteDirection
                    });

                    if (Trail.Count > TrailMaxLength)
                        Trail.RemoveAt(0);
                }
            }
        }

        // ==========================================
        // 3. MANUAL STACKED RENDER (PRE-DRAW)
        // ==========================================
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            // Batalkan render bawaan Retinazer karena digambar manual oleh Spazmatizm - KECUALI
            // selama SplitCombo, di mana Retinazer beneran ada di posisinya sendiri (bukan
            // numpuk di atas Spaz lagi) dan WAJIB digambar sendiri pakai draw vanilla biasa
            // (frame/rotation/spriteDirection udah kita atur sendiri di PreAI/FindFrame -
            // lihat TwinsSplitCombo.cs & blok comboSplit di FindFrame di atas).
            if (npc.type == NPCID.Retinazer)
            {
                int spazIndex = NPC.FindFirstNPC(NPCID.Spazmatism);
                bool comboSplit = spazIndex != -1 && Main.npc[spazIndex].active
                    && Main.npc[spazIndex].GetGlobalNPC<TwinsReworkOverride>().ComboSplitActive;

                return comboSplit; // true = biarin vanilla gambar sendiri, false = tetap disembunyikan kayak biasa
            }

            if (npc.type == NPCID.Spazmatism)
            {
                SpriteEffects effects = npc.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
                Vector2 drawOrigin = new Vector2(npc.frame.Width * 0.5f, npc.frame.Height * 0.5f);
                Vector2 drawPos = npc.Center - screenPos;

                // --- TELEGRAPH LINE (garis hijau arah dash) ---
                if (IsTelegraphing)
                {
                    DrawTelegraphLine(spriteBatch, npc, screenPos);
                }

                // --- RETBEAM AIM/FULL-BEAM LINE (garis merah, plek ketiplek konsep sama
                // kayak telegraph dash, cuma warna beda & progress dari field RetBeam) ---
                if (RetBeamIsAiming)
                {
                    DrawRetBeamAimLine(spriteBatch, npc, screenPos);
                }
                if (RetBeamIsFullBeam)
                {
                    DrawRetBeamFullBeamLine(spriteBatch, npc, screenPos);
                }

                // --- ENRAGED (Phase 3): garis telegraph "RetLaserBeam" LaserBarrage, sebelum
                // beam-nya beneran lepas tembak (lihat TwinsLaserBarrage.State.AimingRetLaser) ---
                if (LaserBarrageIsAiming)
                {
                    DrawLaserBarrageAimLine(spriteBatch, npc, screenPos);
                }

                // --- LAST STAND: garis "Deathray" yang muter terus-menerus (CW -> transisi
                // -> CCW) - SELALU solid & ngedamage selama fase ini aktif, gak ada
                // telegraph/fade sama sekali (gerakan muternya sendiri yang jadi warning).
                // Bentuknya "V" (kecil di ujung muka, MELEBAR seiring jauh - lihat
                // DrawDeathrayCone), dan titik awalnya BUKAN npc.Center persis, tapi digeser
                // maju ke ujung sprite (npc.width * 0.5) biar keliatan keluar dari muka. ---
                if (LastStandDeathrayActive)
                {
                    Vector2 deathrayDirection = LastStandDeathrayAngle.ToRotationVector2();
                    Vector2 deathrayMuzzle = npc.Center + deathrayDirection * (npc.width * 0.5f);

                    // Pass glow (lebih lebar & transparan) dulu, baru core (lebih sempit &
                    // pekat) di atasnya - sama filosofi 2-pass kayak beam lain di codebase ini.
                    DrawDeathrayCone(spriteBatch, deathrayMuzzle, deathrayDirection, screenPos, 2600f, 20f, 160f, new Color(255, 60, 40), 0.45f);
                    DrawDeathrayCone(spriteBatch, deathrayMuzzle, deathrayDirection, screenPos, 2600f, 12f, 100f, new Color(255, 20, 20), 1f);
                }

                // --- EYE LASER FLASH digambar di PALING AKHIR, lihat komentar di bawah
                // dekat "return false" — dulu di sini kegambar SEBELUM sprite Twins,
                // makanya ketutup rapat dan keliatan "gak muncul".

                // --- AFTER-IMAGE TRAIL SPAZMATISM (digambar di bawah sprite utama, selalu tampil) ---
                if (Trail.Count > 0)
                {
                    DrawAfterimageTrail(spriteBatch, TextureAssets.Npc[NPCID.Spazmatism].Value, Trail, npc.scale, screenPos);
                }

                // Spazmatism digeser dikit ke belakang (lawan arah hadap) biar keliatan
                // "mundur" dari Retinazer, bukan numpuk pas persis di posisi yang sama.
                const float SpazBackOffset = 14f;
                Vector2 facingDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
                Vector2 spazDrawPos = drawPos - facingDir * SpazBackOffset;

                // --- CURSED BEAM (laser TwinsCursedRain) — digambar MANUAL DI SINI, SEBELUM
                // sprite Retinazer & Spazmatism di bawah, biar beam-nya kegambar DI BAWAH Twins
                // bukan numpuk di atasnya. Proyektil normal digambar SETELAH NPC di render-loop
                // vanilla (Main.DrawProjectiles jalan abis Main.DrawNPCs), makanya
                // TwinsCursedBeam.PreDraw() sendiri sengaja dikosongin dan visualnya di-hijack
                // manual ke sini lewat DrawBeamVisual().
                if (ActiveCursedBeamIndex != -1)
                {
                    Projectile beamProj = Main.projectile[ActiveCursedBeamIndex];
                    if (beamProj.active && beamProj.ModProjectile is TwinsCursedBeam cursedBeam)
                    {
                        cursedBeam.DrawBeamVisual(screenPos);
                    }
                    else
                    {
                        ActiveCursedBeamIndex = -1; // beam-nya udah mati, bersihin referensi
                    }
                }

                // --- A. DRAW RETINAZER DI LAYER BAWAH ---
                int retIndex = NPC.FindFirstNPC(NPCID.Retinazer);
                if (retIndex != -1)
                {
                    NPC ret = Main.npc[retIndex];
                    Texture2D retTexture = TextureAssets.Npc[NPCID.Retinazer].Value;

                    // After-image Retinazer sendiri — PLEK KETIPLEK kayak punya Spazmatism,
                    // cuma sumber datanya Trail milik INSTANCE Retinazer (bukan "this", yang
                    // instance-nya Spazmatism, karena InstancePerEntity = true tiap NPC punya
                    // instance field-nya sendiri-sendiri).
                    TwinsReworkOverride retGlobal = ret.GetGlobalNPC<TwinsReworkOverride>();
                    if (retGlobal.Trail.Count > 0)
                    {
                        DrawAfterimageTrail(spriteBatch, retTexture, retGlobal.Trail, ret.scale, screenPos);
                    }

                    // 1. Base sprite Retinazer, kena lighting normal (bukan full-bright lagi).
                    // SELAMA PhaseTwoSpectresActive ATAU LastStandHoldingForSpectres (Spectre
                    // clone masih hidup, entah dari Phase 2 atau Last Stand), Twin original
                    // digambar SEMI-TRANSPARENT (lihat TwinsPhaseTransition.OriginalTwinAlpha).
                    float originalAlpha = (PhaseTwoSpectresActive || LastStandHoldingForSpectres) ? TwinsPhaseTransition.OriginalTwinAlpha : 1f;
                    Color retLitColor = Lighting.GetColor(ret.Center.ToTileCoordinates()) * ret.Opacity * originalAlpha;
                    spriteBatch.Draw(
                        retTexture,
                        drawPos,
                        ret.frame,
                        retLitColor,
                        ret.rotation,
                        drawOrigin,
                        ret.scale,
                        effects,
                        0f
                    );

                    // 2. Glowmask asli mata Retinazer ("Eye_Laser"), full-bright di atasnya
                    if (RetinazerGlowTexture != null)
                    {
                        spriteBatch.Draw(
                            RetinazerGlowTexture.Value,
                            drawPos,
                            ret.frame,
                            Color.White * ret.Opacity * originalAlpha,
                            ret.rotation,
                            drawOrigin,
                            ret.scale,
                            effects,
                            0f
                        );
                    }
                }

                // --- B. DRAW SPAZMATISM DI LAYER ATAS, TAPI POSISINYA DIGESER MUNDUR ---
                // Sama kayak Retinazer di atas: SEMI-TRANSPARENT selama PhaseTwoSpectresActive
                // ATAU LastStandHoldingForSpectres.
                Texture2D spazTexture = TextureAssets.Npc[NPCID.Spazmatism].Value;
                Color spazDrawColor = (PhaseTwoSpectresActive || LastStandHoldingForSpectres) ? drawColor * TwinsPhaseTransition.OriginalTwinAlpha : drawColor;
                spriteBatch.Draw(
                    spazTexture,
                    spazDrawPos,
                    npc.frame,
                    spazDrawColor,
                    npc.rotation,
                    drawOrigin,
                    npc.scale,
                    effects,
                    0f
                );

                // --- EYE LASER FLASH (glow merah di depan wajah Retinazer) ---
                // PENTING: ini HARUS digambar PALING TERAKHIR, SETELAH sprite Retinazer (A)
                // & Spazmatism (B) di atas. Sebelumnya dipanggil SEBELUM keduanya, jadi glow
                // additive-nya langsung ketutup rapat sama sprite boss yang opaque digambar
                // abis itu - itu sebabnya efeknya kerasa "gak pernah muncul" padahal logic-nya
                // sendiri jalan normal (bukan soal texture/asset-nya).
                //
                // CONTINUOUS selama laser beam RetBeam masih ada (Aiming ATAU FullBeam) -
                // otomatis mati begitu dua-duanya balik false (beam ilang). PULSE buat
                // tembakan sesaat (BorderShot/LaserBarrage) lewat TriggerEyeFlash(). Dash
                // TIDAK memicu keduanya sama sekali (lihat TwinDash.FireDeathLaserVolley).
                bool continuousGlow = RetBeamIsAiming || RetBeamIsFullBeam;
                bool pulseGlow = Main.GameUpdateCount - EyeFlashSpawnTick <= EyeFlashDuration;

                if (continuousGlow || pulseGlow)
                {
                    DrawEyeLaserFlash(spriteBatch, npc, screenPos, continuousGlow);
                }

                return false; // Batalkan render vanilla Spazmatism
            }

            return true;
        }

        // ==========================================
        // HELPER — Telegraph line (garis lurus arah dash, TIDAK ikut update ke player)
        // ==========================================
        private void DrawTelegraphLine(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos)
        {
            float progress = npc.ai[1] / TwinDash.TelegraphDuration; // 0..1

            // Kedip cepat + makin terang mendekati saat dash dilepas, biar kerasa "charging".
            // Tinggal atur angka 0.9f (kecepatan kedip) & rentang Lerp kalau mau ubah feel-nya.
            float flicker = 0.75f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.9f);
            float alpha = MathHelper.Lerp(0.35f, 1f, progress) * flicker;

            DrawAimLine(spriteBatch, npc, screenPos, TelegraphDirection, new Color(150, 255, 120), alpha);
        }

        // ==========================================
        // HELPER — Garis aim RetBeam (MERAH). "Plek ketiplek" pakai DrawAimLine yang sama
        // kayak telegraph dash di atas, cuma beda warna + progress-nya dari field RetBeam.
        // ==========================================
        private void DrawRetBeamAimLine(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos)
        {
            float progress = RetBeamTimer / TwinsRetBeam.AimDuration; // 0..1

            // Sama kayak telegraph dash: kedip + makin terang mendekati saat jadi full beam.
            float flicker = 0.75f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.9f);
            float alpha = MathHelper.Lerp(0.35f, 1f, progress) * flicker;

            DrawAimLine(spriteBatch, npc, screenPos, RetBeamAimDirection, new Color(255, 40, 40), alpha);

            // ---- ENRAGED: 2 garis tambahan kanan-kiri, bentuk "W" - titik awal SAMA, tapi
            // nyebar keluar. Sisi kanan/kiri cuma NGIKUTIN arah tengah (di-rotate offset tetap),
            // gak punya prediksi sendiri.
            if (IsEnraged)
            {
                DrawAimLine(spriteBatch, npc, screenPos, RetBeamAimDirection.RotatedBy(TwinsRetBeam.SideSpreadAngle), new Color(255, 40, 40), alpha);
                DrawAimLine(spriteBatch, npc, screenPos, RetBeamAimDirection.RotatedBy(-TwinsRetBeam.SideSpreadAngle), new Color(255, 40, 40), alpha);
            }
        }

        // ==========================================
        // HELPER — Full beam RetBeam (MERAH PEKAT, solid, gak kedip — ini fase yang ngedamage)
        // ==========================================
        private void DrawRetBeamFullBeamLine(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos)
        {
            DrawAimLine(spriteBatch, npc, screenPos, RetBeamAimDirection, new Color(255, 10, 10), 1f);

            // ---- ENRAGED: sisi kanan-kiri juga jadi full beam solid bareng yang tengah ----
            if (IsEnraged)
            {
                DrawAimLine(spriteBatch, npc, screenPos, RetBeamAimDirection.RotatedBy(TwinsRetBeam.SideSpreadAngle), new Color(255, 10, 10), 1f);
                DrawAimLine(spriteBatch, npc, screenPos, RetBeamAimDirection.RotatedBy(-TwinsRetBeam.SideSpreadAngle), new Color(255, 10, 10), 1f);
            }
        }

        // ==========================================
        // HELPER — Garis telegraph "RetLaserBeam" LaserBarrage (ENRAGED/Phase 3 saja). Arah
        // udah DIKUNCI sekali (LaserBarrageAimDirection) begitu masuk State.AimingRetLaser,
        // jadi garis ini TIDAK ngikutin player lagi selama window aim-nya - player dikasih
        // waktu buat baca garis & minggir sebelum beam-nya beneran lepas tembak.
        // ==========================================
        private void DrawLaserBarrageAimLine(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos)
        {
            float progress = LaserBarrageTimer / TwinsLaserBarrage.AimBeamDuration; // 0..1

            float flicker = 0.75f + 0.25f * (float)Math.Sin(Main.GameUpdateCount * 0.9f);
            float alpha = MathHelper.Lerp(0.35f, 1f, progress) * flicker;

            DrawAimLine(spriteBatch, npc, screenPos, LaserBarrageAimDirection, new Color(255, 40, 40), alpha);
        }

        // ==========================================
        // HELPER — Eye Laser Flash. Glow merah di ARAH DEPAN wajah Retinazer, dihitung
        // LIVE tiap frame dari npc.rotation (formula facingDir yang sama kayak dipakai
        // FireCursedFlame/FireCursedBeamDown - rotation + PiOver2), BUKAN dari posisi
        // proyektil atau snapshot beku - jadi posisinya otomatis "lock" ngikutin arah
        // hadap DAN kecepatan gerak Twins kapan pun frame ini digambar, gak peduli NPC-nya
        // lagi diem atau lagi ngebut (Reposition, dash, dll).
        //
        // Sprite-nya numpang tekstur VANILLA "Rainbow" (EyeFlashStarTexture, dari proyektil
        // Rainbow Rod - lihat komentar di deklarasi field-nya) - starburst putih polos,
        // gampang di-tint. Digambar 2 LAPIS biar berasa lebih "nge-bloom" (sebelumnya cuma
        // 1 lapis flat, makanya kerasa kurang nendang):
        //   - OUTER: lebih besar & lebih transparan, warna merah-oranye - badan glow yang lembut.
        //   - CORE  : lebih kecil & lebih terang/opaque, warna hampir putih-kemerahan - titik
        //             panas di tengah, ini yang bikin efeknya kerasa "menyala" bukan cuma tempel.
        //
        // DUA MODE ANIMASI (parameter continuous):
        //   - continuous = true  -> dipakai SELAMA RetBeam-nya nge-beam (Aiming/FullBeam).
        //     Ukuran & alpha dijaga TETAP TERANG, cuma dikasih "napas" halus (breathing, sinus
        //     pelan) biar gak keliatan mati/statis - TIDAK fade out sama sekali selama mode
        //     ini aktif (baru ilang begitu caller berhenti manggil dengan continuous=true,
        //     yaitu pas RetBeamIsAiming & RetBeamIsFullBeam dua-duanya udah false).
        //   - continuous = false -> mode PULSE lama: membesar cepat (ease-out, EyeFlashGrowTicks
        //     tick pertama) lalu mengecil+pudar (ease-in) sampai abis di EyeFlashDuration -
        //     dipakai buat tembakan sesaat (BorderShot/LaserBarrage).
        //
        // FALLBACK: kalau EyeFlashStarTexture gagal load (null), balik ke starburst manual
        // 8-spoke pakai Luminance.Common.Utilities.Utilities.DrawBloomLine (fungsi ASLI
        // Luminance), 2 lapis juga (outer+core) biar tetap konsisten kuat/lemahnya.
        // ==========================================
        private const int EyeFlashGrowTicks = 4;           // ~0.07 detik naik cepat ke ukuran puncak (mode PULSE)
        private const float EyeFlashPeakWorldSize = 100f;  // diameter dunia (px) inti glow pas puncak - diperbesar dari 64
        private const float EyeGlowMuzzleOffset = 95f;     // jarak dari Center NPC ke arah depan wajah Retinazer

        private void DrawEyeLaserFlash(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos, bool continuous)
        {
            // Posisi LIVE - dihitung ulang tiap frame dari rotation SAAT INI, bukan snapshot.
            Vector2 facingDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
            Vector2 muzzleWorldPos = npc.Center + facingDir * EyeGlowMuzzleOffset;
            Vector2 center = muzzleWorldPos - screenPos;

            float alpha;
            float coreWorldSize;

            if (continuous)
            {
                // Nyala penuh terus, cuma dikasih napas halus biar keliatan "hidup".
                float breathing = 0.85f + 0.15f * (float)Math.Sin(Main.GameUpdateCount * 0.15f);
                alpha = 1f;
                coreWorldSize = EyeFlashPeakWorldSize * breathing;
            }
            else
            {
                float elapsed = Main.GameUpdateCount - EyeFlashSpawnTick;
                float lifePercent = MathHelper.Clamp(elapsed / EyeFlashDuration, 0f, 1f);
                alpha = 1f - lifePercent; // makin lama makin pudar (independen dari ukuran)

                // Kurva ukuran: 0 -> 1 (ease-out) di EyeFlashGrowTicks tick pertama, lalu
                // 1 -> 0 (ease-in) di sisa durasi - "membesar dulu baru mengecil".
                float pulseProgress;
                if (elapsed < EyeFlashGrowTicks)
                {
                    float growT = MathHelper.Clamp(elapsed / EyeFlashGrowTicks, 0f, 1f);
                    pulseProgress = 1f - (1f - growT) * (1f - growT); // ease-out
                }
                else
                {
                    float shrinkDuration = EyeFlashDuration - EyeFlashGrowTicks;
                    float shrinkT = shrinkDuration > 0f ? MathHelper.Clamp((elapsed - EyeFlashGrowTicks) / shrinkDuration, 0f, 1f) : 1f;
                    pulseProgress = 1f - shrinkT * shrinkT; // ease-in
                }

                coreWorldSize = EyeFlashPeakWorldSize * pulseProgress;
            }

            Color outerColor = new Color(255, 45, 25) * (alpha * 0.65f);
            Color coreColor = new Color(255, 190, 150) * alpha;
            float outerWorldSize = coreWorldSize * 1.7f;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            if (EyeFlashStarTexture != null)
            {
                Texture2D starTexture = EyeFlashStarTexture.Value;
                Vector2 origin = new Vector2(starTexture.Width * 0.5f, starTexture.Height * 0.5f);

                // OUTER - badan glow lembut, lebih besar & transparan.
                spriteBatch.Draw(starTexture, center, null, outerColor, 0f, origin, outerWorldSize / starTexture.Width, SpriteEffects.None, 0f);
                // CORE - titik panas di tengah, lebih kecil & terang.
                spriteBatch.Draw(starTexture, center, null, coreColor, 0f, origin, coreWorldSize / starTexture.Width, SpriteEffects.None, 0f);
            }
            else
            {
                // Fallback: starburst manual 8-spoke pakai DrawBloomLine (2 lapis, konsisten
                // sama kuat/lemahnya sama versi sprite di atas).
                const int SpokeCount = 8;

                for (int i = 0; i < SpokeCount; i++)
                {
                    float angle = MathHelper.TwoPi * i / SpokeCount;
                    Vector2 spokeDir = angle.ToRotationVector2();

                    Utilities.DrawBloomLine(spriteBatch, center, center + spokeDir * (outerWorldSize * 0.5f), outerColor, 6f);
                    Utilities.DrawBloomLine(spriteBatch, center, center + spokeDir * (coreWorldSize * 0.5f), coreColor, 3f);
                }
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // ==========================================
        // HELPER GENERIC — gambar garis lurus (dipakai telegraph dash hijau & RetBeam merah).
        // Core solid setengah block (8px) + outline lebih transparan nutup sampai 1 block (16px).
        // Pakai overload Draw dengan Rectangle TUJUAN langsung supaya ukuran akhirnya PASTI
        // persis sekian pixel, gak peduli ukuran asli tekstur MagicPixel-nya.
        // ==========================================
        private void DrawAimLine(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos, Vector2 direction, Color baseColor, float alpha)
        {
            Vector2 start = npc.Center - screenPos;
            const float LineLength = 2600f; // cukup panjang biar nembus ujung layar manapun

            // 1 block Terraria = 16px.
            const int CoreThickness = 8;
            const int OutlineThickness = 16;

            float rotation = direction.ToRotation();

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle sourceRect = new Rectangle(0, 0, pixel.Width, pixel.Height);
            Vector2 originInSourceSpace = new Vector2(0f, pixel.Height * 0.5f); // kiri, center vertikal

            // --- OUTLINE (digambar duluan, lebih transparan, total tebal 1 block) ---
            Color outlineColor = baseColor * (alpha * 0.35f);
            Rectangle outlineDest = new Rectangle((int)start.X, (int)start.Y, (int)LineLength, OutlineThickness);
            spriteBatch.Draw(
                pixel,
                outlineDest,
                sourceRect,
                outlineColor,
                rotation,
                originInSourceSpace,
                SpriteEffects.None,
                0f
            );

            // --- CORE (digambar di atas outline, solid, setengah block) ---
            Color coreColor = baseColor * alpha;
            Rectangle coreDest = new Rectangle((int)start.X, (int)start.Y, (int)LineLength, CoreThickness);
            spriteBatch.Draw(
                pixel,
                coreDest,
                sourceRect,
                coreColor,
                rotation,
                originInSourceSpace,
                SpriteEffects.None,
                0f
            );
        }

        // ==========================================
        // HELPER — LAST STAND: garis Deathray berbentuk "V" (kecil di deket muka, MELEBAR
        // seiring jauh) - BEDA dari DrawAimLine yang ketebalannya rata dari ujung ke ujung.
        // Digambar per-segmen (stretched-pixel juga), ketebalan tiap segmen di-lerp dari
        // startThickness ke endThickness sepanjang totalLength, biar hasilnya kayak corong/
        // kerucut memanjang bukan garis lurus rata.
        // ==========================================
        private void DrawDeathrayCone(SpriteBatch spriteBatch, Vector2 worldOrigin, Vector2 direction, Vector2 screenPos, float totalLength, float startThickness, float endThickness, Color color, float alpha)
        {
            const int Segments = 24;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle sourceRect = new Rectangle(0, 0, pixel.Width, pixel.Height);
            Vector2 originInSourceSpace = new Vector2(0f, pixel.Height * 0.5f); // kiri, center vertikal
            float rotation = direction.ToRotation();
            float segLength = totalLength / Segments;

            for (int i = 0; i < Segments; i++)
            {
                float tMid = (i + 0.5f) / Segments;
                float thickness = MathHelper.Lerp(startThickness, endThickness, tMid);

                Vector2 segWorldStart = worldOrigin + direction * (segLength * i);
                Vector2 segScreenStart = segWorldStart - screenPos;

                // Sedikit overlap (+2px panjang) antar segmen biar gak ada celah tipis
                // kelihatan di sambungannya.
                Rectangle segDest = new Rectangle((int)segScreenStart.X, (int)segScreenStart.Y, (int)segLength + 2, (int)thickness);
                spriteBatch.Draw(
                    pixel,
                    segDest,
                    sourceRect,
                    color * alpha,
                    rotation,
                    originInSourceSpace,
                    SpriteEffects.None,
                    0f
                );
            }
        }

        // ==========================================
        // HELPER — After-image trail dengan gradasi warna atas-merah / bawah-lime.
        // Generic: dipanggil buat Spazmatism (texture + Trail miliknya sendiri) DAN buat
        // Retinazer (retTexture + Trail milik instance Retinazer sendiri), "plek ketiplek".
        // ==========================================
        private void DrawAfterimageTrail(SpriteBatch spriteBatch, Texture2D texture, List<TwinsAfterimageSnapshot> trail, float scale, Vector2 screenPos)
        {
            for (int i = 0; i < trail.Count; i++)
            {
                TwinsAfterimageSnapshot snap = trail[i];

                // Ghost paling baru (index terakhir) paling keliatan, makin lama makin transparan.
                float ageFactor = (i + 1f) / trail.Count;
                float baseAlpha = ageFactor * 0.45f; // 0.45f = opacity maksimum trail, boleh diubah

                Vector2 pos = snap.Center - screenPos;
                Vector2 fullOrigin = new Vector2(snap.Frame.Width * 0.5f, snap.Frame.Height * 0.5f);
                SpriteEffects effects = snap.SpriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

                // Animasi "nge-blend": rasio campuran warnanya digeser tiap tick pakai gelombang
                // sinus (beda fase per ghost juga, ditambah i * 0.8f), jadi kelihatan kayak
                // gradasinya "napas" bukan statis diam.
                float blendWave = (float)Math.Sin(Main.GameUpdateCount * 0.15f + i * 0.8f) * 0.5f + 0.5f;

                float sliceHeight = snap.Frame.Height / (float)AfterimageSlices;
                for (int s = 0; s < AfterimageSlices; s++)
                {
                    Rectangle sourceSlice = new Rectangle(
                        snap.Frame.X,
                        (int)(snap.Frame.Y + s * sliceHeight),
                        snap.Frame.Width,
                        (int)Math.Ceiling(sliceHeight)
                    );

                    // Origin digeser ke atas sesuai posisi slice, supaya tiap potongan tetap
                    // berputar pas mengelilingi titik pivot yang sama kayak sprite utuh
                    // (jadi rotasinya tetap benar walau kita gambar per-potongan horizontal).
                    Vector2 sliceOrigin = new Vector2(fullOrigin.X, fullOrigin.Y - s * sliceHeight);

                    float sliceT = AfterimageSlices == 1 ? 0f : s / (float)(AfterimageSlices - 1); // 0 atas .. 1 bawah
                    float mixT = MathHelper.Clamp(sliceT + (blendWave - 0.5f) * 0.6f, 0f, 1f);
                    Color sliceColor = Color.Lerp(AfterimageTopColor, AfterimageBottomColor, mixT) * baseAlpha;

                    spriteBatch.Draw(
                        texture,
                        pos,
                        sourceSlice,
                        sliceColor,
                        snap.Rotation,
                        sliceOrigin,
                        scale,
                        effects,
                        0f
                    );
                }
            }
        }
    }
}
