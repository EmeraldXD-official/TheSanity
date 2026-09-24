using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Linq;
using ReLogic.Content;
using ReLogic.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;
using TheSanity.Common.Systems; // ShockwaveSystem - manager shockwave beneran yang udah ada di mod

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // PHASE 3 - "CARTESIAN GAUNTLET" (dipicu di bawah 10% HP, SETELAH Phase 2 udah kejadian)
    // ================================================================================================
    // Ide besarnya: begitu HP < 10%, boss "menyeret" player keluar dari fight normal ke sebuah arena
    // kosong jauh di atas dunia (void/langit, "jauh dari daratan") yang digambar sebagai bidang
    // koordinat Cartesius (-10..10 di X dan Y, lengkap sama garis & angkanya). Boss sendiri jadi
    // INVINCIBLE (NPC.dontTakeDamage) dan DIAM TOTAL sepanjang fase ini - satu-satunya "serangan"
    // yang kejadian datang dari senjata yang dia summon berdasarkan class gear player SEKARANG
    // (lihat WhoAmI.TryDetectPlayerClass, WhoAmI_Helpers.cs). Player nggak bisa kabur (teleport item
    // diblokir, posisi di-clamp ke dalam kotak arena - lihat WhoAmIPhase3ArenaPlayer di bawah).
    //
    // Satu "wave" = satu putaran pattern (grow-in -> active -> resolve). Tiap wave yang berhasil
    // dilewati (baik player kena atau nggak - yang penting waktunya habis) motong sebagian HP boss
    // secara manual (combat damage nggak berlaku sama sekali selama dontTakeDamage aktif). Begitu HP
    // boss abis / batas wave kecapai, kita teleport semuanya balik ke lokasi awal fight dan lanjut ke
    // STATE_DESPERATION_CUTSCENE yang udah ada (WhoAmI.cs, HandleDesperationCutscene) - PERSIS jalur
    // yang sama kayak CheckDead() kalau boss mati lewat combat normal.
    //
    // File ini SEPENUHNYA baru, nggak gantiin apapun - cuma numpang di 3 titik placeholder yang udah
    // sengaja ditinggal di WhoAmI.cs (trigger di AI(), dispatch di AI(), sama draw hook di PreDraw()).
    //
    // CATATAN MULTIPLAYER: sama kayak WhoAmIMirrorPaintingTile.TrySummon (WhoAmI_MirrorPainting.cs),
    // ini dirancang buat singleplayer/host-and-play. Field phase3* di bawah nggak di-SendExtraAI ke
    // client lain secara eksplisit - di dedicated server + client remote, state gauntrol ini bakal
    // desync. Belum diimplementasikan karena scope-nya sendiri (perlu paket mod tambahan), sama
    // kayak catatan yang udah ada di MirrorPainting.
    // ================================================================================================
    public partial class WhoAmI : ModNPC
    {
        // ---------------- STATE IDS ----------------
        // Nomor bebas dipilih (40/41) - nggak nabrak state lain (0-27 dipakai attack pattern normal,
        // 100/101/102/2 cutscene intro & phase2, 103 = STATE_DESPERATION_CUTSCENE).
        // FIX (boss "despawn" random pas archetype Summon/Whip): dua konstanta ini SEBELUMNYA
        // dinomori 40/41 - persis bentrok sama STATE_SUMMON_SPECTRAL_CAROUSEL (=40) & 
        // STATE_WHIP_SERPENTS_COIL (=41) yang didefinisikan di WhoAmI.cs. AI() ngecek
        // `if (aiState == STATE_PHASE3_TRANSITION || aiState == STATE_PHASE3_ARENA)` SEBELUM
        // switch(aiState) yang punya `case STATE_SUMMON_SPECTRAL_CAROUSEL`/`case
        // STATE_WHIP_SERPENTS_COIL` - dan karena C# cuma lihat NILAI numeriknya (bukan nama
        // konstantanya), tiap kali pattern selector Summon/Whip normal (WhoAmI_Patterns.cs, BUKAN
        // cuma debug forcer) ngerol "Spectral Carousel" atau "Serpent's Coil", boss langsung
        // ke-hijack masuk HandlePhase3() padahal TriggerPhase3Start() nggak pernah beneran
        // dipanggil - arena bounds/return position/camera lock/wave setup semua belum di-init,
        // jadi boss-nya kefade/keteleport ke state Phase3 yang rusak alih2 nyerang - dari sudut
        // pandang player kebaca persis kayak "boss-nya despawn random", dan "random"-nya itu
        // sendiri karena kejadiannya tergantung RNG pattern selector, bukan sesuatu yang player
        // lakuin. Lihat juga komentar di WhoAmI_DebugForcer.cs (DebugPatternCatalog) yang udah
        // nge-flag collision ini dari awal.
        //
        // Fix: pindahin dua state Phase 3 ini ke ID yang bener2 kosong (50/51 - semua STATE_*
        // lain 0-49 udah kepakai penuh tanpa celah, dan 100-103 dipakai state cutscene lain).
        // Direferensikan lewat NAMA konstanta di mana-mana (WhoAmI.cs, WhoAmI_DebugForcer.cs,
        // WhoAmIProjectileGuard.cs pakai Phase3ArenaActive bukan angka ini langsung) - jadi ganti
        // di sini aja udah cukup, nggak ada literal 40/41 lain yang perlu diubah manual.
        private const int STATE_PHASE3_TRANSITION = 50; // fade ke hitam, teleport ke arena, fade balik
        private const int STATE_PHASE3_ARENA = 51;       // loop wave di dalam arena Cartesius

        // ---------------- TUNING CONSTANTS ----------------
        private const int Phase3FadeTicks = 45;               // ~0.75 detik tiap arah (in/out)
        private const int Phase3GrowTicks = 55;                // durasi senjata "membesar" di awal tiap wave
        private const int Phase3ResolveTicks = 40;             // durasi senjata mengecil/hilang di akhir tiap wave
        private const int Phase3StandardActiveDuration = 600;  // 10 detik - Melee/Ranged/Magic
        private const int Phase3RiftCycleDuration = 600;       // 10 detik per celah - Summon (x3 = 30 detik)
        private const int Phase3MaxWaves = 5;                  // jaring pengaman biar gauntlet nggak infinite loop

        private const float Phase3GridUnit = 130f;                     // px dunia per 1 satuan grid (dinaikin dari 58 - arena kerasa kecil di 58)
        // FIX (batas arena nggak sesuai diagram Cartesius): dulu ada "+ 140f" tambahan di sini, jadi
        // dinding arena beneran (player clamp, ujung garis grid, jangkauan blade melee mentok, dst)
        // nangkring 140px DI LUAR garis angka 10 yang digambar/dilabeli (DrawPhase3Cartesian cuma
        // nge-loop i dari -10..10). Diagramnya bilang -10..10, tapi batas sebenernya ~11.08 - nggak
        // nyambung. Sekarang HalfExtent PAS di 10 * GridUnit, jadi dinding arena persis nempel di
        // garis angka 10/-10, sinkron sama apa yang digambar & dilabeli di layar.
        private const float Phase3ArenaHalfExtent = 10f * Phase3GridUnit; // setengah lebar arena - PERSIS di garis angka 10 grid Cartesius

        // FIX (batas atas Y "cuma sampe tempat lagi terbang", nggak nyampe garis +10 yang digambar):
        // safeArenaY lama (110*16 = 1760px dari puncak dunia) cuma ngasih center-nya jarak aman, TAPI
        // lupa nyisain jarak buat HalfExtent (1300px) di ATAS center itu juga. Tembok atas arena =
        // center.Y - HalfExtent = 1760 - 1300 = cuma 460px (~28 tile) dari Y=0 (puncak absolut dunia).
        // Kamera Terraria sendiri nggak bisa scroll lewat Y=0 - begitu player deket ke tembok atas,
        // KAMERA duluan yang kehabisan ruang scroll (karena setengah tinggi layar aja biasanya udah
        // >30-40 tile, lebih gede dari 28 tile sisa itu), jauh SEBELUM player fisik-nya nabrak clamp
        // kode kita di ClampToArena(). Efeknya kelihatan kayak "batas atas cuma sampe sini", padahal
        // itu bukan dinding yang dimaksud - cuma kamera yang mentok tepi dunia duluan.
        // Sekarang safeArenaY dihitung dari HalfExtent + margin tambahan, jadi tembok atas arena PASTI
        // punya buffer nyata (700px / ~44 tile) ke Y=0, cukup buat kamera scroll normal tanpa kepotong.
        private const float Phase3ArenaTopMargin = 700f; // buffer ekstra px ANTARA tembok atas arena dengan Y=0 (puncak dunia)
        // Trigger gauntlet-nya sekarang di 10% HP (bukan 35% lagi, lihat threshold-check di
        // WhoAmI.cs AI()) - budget HP yang tersedia buat "dihabisin" sepanjang gauntlet jadi jauh
        // lebih kecil dari sebelumnya. Nilai lama (0.09f / ~9% per wave) dipertahankan proporsinya
        // terhadap threshold lama (9/35 ≈ 25.7% dari budget per wave), diterapkan ke budget baru:
        // 10% * 25.7% ≈ 2.5% per wave - jadi ritme "abis di wave ke-berapa" & jumlah wave yang
        // kelewatin sebelum HandlePhase3 masuk FinishPhase3AndEnterDesperation kerasa SAMA kayak
        // sebelumnya (masih sekitar 4 wave dari 5 max, bukan langsung kelar 1 wave doang gara2
        // budget HP-nya sekarang jauh lebih tipis).
        private const float Phase3DamagePerWave = 0.025f;      // ~2.5% max HP boss per wave yang berhasil dilewati
        private const float Phase3RiftSafeRadius = 70f;
        private const int Phase3RiftFailureDamage = 500;

        private const int Phase3RangedFireInterval = 40;
        private const float Phase3RangedBoltSpeed = 17f;
        private const int Phase3RangedBoltDamage = 90;
        private const float Phase3RangedBoltScaleMultiplier = 2.8f; // "projectile digedein" - peluru/panah normal kecil banget & ketelen sama tekstur lantai arena kalau dibiarin scale 1x default

        private const int Phase3MageVolleyInterval = 120;      // 2 detik - jeda antar volley "ke segala arah" (dipakai lagi buat stagger awal FireTimer di SetupMagePattern)
        private const int Phase3MageBoltsPerVolley = 10;       // jumlah arah dalam satu volley 360 derajat (sisa dari FireRealWeaponBoltsInAllDirections, masih dipakai kalau fallback)
        private const float Phase3MageBoltSpeed = 13f;
        private const float Phase3MageBoltScaleMultiplier = 1.9f; // "projectile besar" - proyektil ASLI senjatanya digedein dari ukuran normalnya

        // ---- Mage "merge & spin" bullet hell (improvement baru) ----
        private const int Phase3MageMergeTicks = 45;              // durasi animasi 2 senjata kegabung dari kiri/kanan ke tengah arena
        private const float Phase3MageSpinRadius = 26f;           // seberapa jauh tiap senjata dari titik pusat pas udah "kegabung" & muter (bukan 0 total biar putarannya kelihatan, tapi tetap kerasa "nempel jadi satu")
        private const float Phase3MageSpinSpeed = 0.045f;         // kecepatan putar pasangan senjata - arah nembak ikut berubah sesuai ini
        private const int Phase3MageSpiralFireInterval = 5;       // tiap berapa tick masing2 senjata nembak 1 giliran (bikin bullet hell rapat tanpa spam tiap tick)
        private const int Phase3MageSpiralArms = 3;                // jumlah proyektil per giliran tembak per senjata, disebar rata 360/arms - biar kerasa "bunga" spiral, bukan satu garis tipis
        private const float Phase3MageSpiralBoltSpeed = 7f;        // SENGAJA lebih lambat dari Phase3MageBoltSpeed lama (13f) - "kecepatan projectile dilambatin" sesuai request, biar bullet hell-nya masih bisa di-dodge

        private const int Phase3MeleeContactDamage = 500; // dinaikin dari 90 - request "kontak dmg gede, bukan pajangan doang". Immunity slot-nya sendiri (ImmunityCooldownID.DD2OgreKnockback, lihat TickMeleeActive) yang bikin damage-nya BENERAN kena, bukan nilainya doang
        private const int Phase3MeleeHitCooldown = 40;
        private const float Phase3MeleeSpinSpeed = 0.012f;         // LEGACY - dipakai pola "3 bilah muter statis" lama, nggak dipakai lagi sama sweep state machine (lihat Phase3MeleeSweep* di bawah)

        // ---- Melee (Phase 3) "270-degree sweep" - timing tiap state (lihat TickMeleeActive) ----
        // REVISI (request "hapus semua patern spoilernya"): state 0 sekarang MURNI diam - blade
        // disembunyikan total (Scale 0, lihat TickMeleeActive case 0) selama 5 detik penuh, TANPA
        // indikator visual apapun di layar (arc/panah/lingkaran-nya udah dihapus, lihat catatan di
        // dekat DrawPhase3Cartesian). Blade baru muncul & mulai ngayun begitu 5 detik itu abis (lihat
        // transisi state 0->1 di TickMeleeActive).
        private const int Phase3MeleeSweepTelegraphTicks = 300; // state 0 - 5 DETIK diam total, blade tersembunyi (Scale 0), nggak ada indikator visual
        private const int Phase3MeleeSweepSweepTicks = 18;     // state 1 - ayunan CEPAT 270 derajat, ~15-20 tick sesuai request; blade BARU digambar begitu state ini mulai
        private const int Phase3MeleeSweepCooldownTicks = 30;  // state 2 - blade disembunyikan/diredupin sebentar sebelum siklus berikutnya di-random ulang

        // REVISI (request "swing - berpindah - swing - berpindah lagi - terus swing", bukan cuma 1x
        // swing per wave): dulu wave Melee numpang Phase3StandardActiveDuration (600 tick) yang SAMA
        // kayak Ranged/Magic - itu nggak nyambung sama panjang 1 siklus Melee sendiri (Telegraph 300 +
        // Sweep 18 + Cooldown 30 = 348 tick), makanya cuma pas buat 1 swing penuh + sisa 252 tick
        // "nanggung" yang jadi akar bug windup-gantung sebelumnya (lihat FIX di TickMeleeActive case 2).
        // Sekarang durasi Active KHUSUS Melee dihitung sebagai KELIPATAN PAS dari 1 siklus penuh -
        // dijamin selalu dapet Phase3MeleeSwingsPerWave swing UTUH per wave, TANPA sisa waktu random
        // sama sekali (remainingActiveTicks di TickMeleeActive case 2 bakal selalu pas 0 di akhir siklus
        // terakhir, bukan pernah nanggung di tengah).
        private const int Phase3MeleeSwingsPerWave = 3; // jumlah swing PENUH yang dijamin kejadian per wave Melee - request "swing-berpindah-swing-berpindah-swing"
        private const int Phase3MeleeCycleTicks = Phase3MeleeSweepTelegraphTicks + Phase3MeleeSweepSweepTicks + Phase3MeleeSweepCooldownTicks; // 1 siklus penuh Telegraph->Sweep->Cooldown
        private const int Phase3MeleeActiveDuration = Phase3MeleeSwingsPerWave * Phase3MeleeCycleTicks; // durasi Active KHUSUS wave Melee (menggantikan Phase3StandardActiveDuration buat class ini doang - lihat TickMeleeActive)
        // FIX (afterimage nyaris nggak kelihatan): cap lama (5) cuma nampung sepertiga dari
        // Phase3MeleeSweepSweepTicks (18) - separo awal ayunan nggak ninggalin jejak sama sekali
        // karena entrinya udah kebuang duluan sebelum ayunan selesai. Sekarang dipatok >=
        // Phase3MeleeSweepSweepTicks (30, dikasih sedikit headroom) supaya SATU siklus ayunan penuh
        // selalu nyisain trail dari awal sampai akhir - "after image" beneran kebaca sepanjang sweep,
        // bukan cuma ekor pendek di belakang blade.
        private const int Phase3MeleeSweepTrailMaxPoints = 30;
        // FIX (hit sample per-tick doang -> berpotensi "loncat" ngelewatin celah tipis antara 2 sudut
        // per tick pas ayunan cepat, ini akar dari "celah spoiler ga sesuai celah safe beneran" versi
        // gameplay-nya): ayunan 270 derajat cuma 18 tick = ~15 derajat lompatan sudut TIAP TICK kalau
        // dicek sekali doang per tick. Sekarang tiap tick kontak dicek di BEBERAPA sudut antara (sudut
        // tick sebelumnya) sampai (sudut tick sekarang), bukan cuma di sudut akhir tick itu doang -
        // jadi nggak ada rentang sudut yang "kelewatan" sama sekali, hitbox-nya selalu 1:1 nutupin
        // PERSIS busur yang digambar (arc spoiler & sprite blade), nggak lebih nggak kurang.
        private const int Phase3MeleeSweepHitSubsteps = 6;
        // FIX (spoiler kemarin ukurannya nyamain full jangkauan bilah ~1300 unit - "kegedean" &
        // renderingnya rawan keliatan kayak jari-jari nembak keluar): radius visual spoiler SEKARANG
        // dipisah total dari reach blade yang sebenarnya, dipatok compact & tetap (nggak berubah-ubah
        // ngikut ukuran arena) - cuma indikator arah/peringatan, bukan representasi jangkauan hit
        // beneran (hitbox tetap pakai reach asli blade, lihat TickMeleeActive - itu yang nentuin kena
        // apa nggak, bukan lingkaran spoiler ini).
        // REVISI (request "hapus semua patern spoilernya"): visual arc/panah/lingkaran-nya udah
        // dihapus total (lihat catatan di deket DrawPhase3Cartesian) - konstanta ini SEKARANG cuma
        // nentuin radius burst dust "materialize" sesaat pas blade muncul di awal state Sweep (lihat
        // TickMeleeActive, transisi state 0->1), bukan lagi radius sebuah arc yang digambar terus-
        // terusan. Tetap proporsional ke Phase3ArenaHalfExtent biar burst-nya otomatis nyesuaiin kalau
        // ukuran arena di-tweak lagi.
        private const float Phase3MeleeSpoilerRadiusFraction = 0.45f;
        private const float Phase3MeleeSpoilerRadius = Phase3ArenaHalfExtent * Phase3MeleeSpoilerRadiusFraction;

        // FIX BARU (blade muter beneran 0 damage sama sekali, bukan cuma "kadang meleset"): akar
        // masalahnya ada di DUA konstanta lama di sini (Phase3MeleeBladeReachPerScale=95f &
        // Phase3MeleeBladeThicknessPerScale=6f) - keduanya cuma TEBAKAN kasar "px dunia per 1.0 Scale"
        // yang SAMA SEKALI NGGAK NYAMBUNG ke ukuran piksel asli PNG (Night_s_Edge_Horizontal dkk,
        // lihat GetCustomMeleeSwordTexture) yang beneran digambar. meleeTargetScale dihitung MUNDUR
        // dari konstanta itu (SetupMeleePattern) supaya hitbox pas mentok ke batas arena - tapi karena
        // konstantanya nggak match ukuran tekstur asli, sprite yang BENERAN kegambar di layar (lihat
        // DrawPhase3Cartesian) posisinya bisa jauh beda dari garis hitbox yang dihitung TickMeleeActive
        // - hasilnya pemain bisa keliatan "nabrak" sprite di layar tapi hitbox-nya udah lewat/belum
        // sampai di situ, jadi Hurt() nggak pernah kepanggil.
        //
        // Fix-nya: hitbox SEKARANG dihitung LANGSUNG dari prop.CustomTexture.Width/Height (ukuran
        // piksel tekstur ASLI yang beneran dipakai spriteBatch.Draw), pakai rumus origin yang PERSIS
        // sama kayak DrawPhase3Cartesian (MeleeBladeOriginXFraction). Jadi apapun ukuran asli
        // PNG-nya, hitbox dijamin selalu ngikutin sprite yang beneran kegambar - bukan angka tebakan
        // lepas lagi. meleeTargetScale (SetupMeleePattern) juga diitung ulang per-senjata dari lebar
        // tekstur asli masing2 (3 PNG beda bisa beda ukuran), bukan satu angka global lagi.
        private const float MeleeBladeOriginXFraction = 0.12f; // gagang (pivot) ada di 12% dari kiri tekstur - HARUS sama persis dengan customOrigin.X di DrawPhase3Cartesian

        // ---- Melee (Phase 3) "safe-gap marker" (request baru): Terra Blade KEDUA, jauh lebih kecil,
        // nongkrong DI TENGAH celah aman 90 derajat sepanjang wave Melee aktif - MURNI penanda visual,
        // TIDAK ikut hit-testing sama sekali (nggak bisa nyakitin ATAU ngelindungin player, cuma nunjukin
        // "sini aman"). Beda TOTAL dari blade sweep utama (phase3Props[0]): pivot-nya di TENGAH tekstur
        // (bukan hilt-anchored di ujung), jadi visualnya muter di POROSNYA SENDIRI kayak baling-baling,
        // BUKAN ngayun radial dari pusat arena. Posisinya nempel di titik tengah celah aman (dihitung dari
        // phase3MeleeEndAngle + 45 derajat - lihat komentar ResetMeleeSweep soal gimana celah aman
        // ditentukan) dan otomatis PINDAH tiap kali ResetMeleeSweep() re-random-in celah baru di akhir
        // tiap siklus Telegraph->Sweep->Cooldown (lihat TickMeleeActive case 2 -> ResetMeleeSweep).
        private const float Phase3SafeGapBladeRadiusFraction = 0.55f; // jarak marker dari pusat arena, sepanjang arah celah aman - beda dari Phase3MeleeSpoilerRadiusFraction (0.45) biar nggak numpuk visual sama burst dust "materialize" blade sweep
        private const float Phase3SafeGapBladeRadius = Phase3ArenaHalfExtent * Phase3SafeGapBladeRadiusFraction;
        // REVISI (request "diperbesar, muternya cepet, ada after image"): scale dinaikin dari 0.9f jadi
        // 1.8f (2x) - masih jelas lebih kecil dari blade sweep utama (yang mentok ke batas arena), tapi
        // sekarang beneran kebaca sebagai "senjata", bukan ikon kecil. Spin speed dinaikin ~6x (0.06 ->
        // 0.35 radian/tick, ~20 derajat/tick) biar keliatan muter cepet kayak baling-baling beneran,
        // bukan pelan-pelan puter.
        private const float Phase3SafeGapBladeScale = 1.8f; // jauh lebih kecil dari blade sweep utama (targetScale dihitung mentok ke batas arena) - ini cuma penanda, bukan senjata beneran
        private const float Phase3SafeGapBladeSelfSpinSpeed = 0.35f; // radian/tick - muter CEPAT di porosnya sendiri kayak baling-baling, independen total dari phase3MeleeGroupSpin/prop.Rotation blade sweep
        // After-image trail buat marker - sama polanya kayak OldRotations punya blade sweep utama
        // (Phase3MeleeSweepTrailMaxPoints), tapi ini history-nya SENDIRI (bukan numpang punya blade
        // sweep) karena marker muter terus-terusan sepanjang wave, bukan cuma pas state Sweep doang.
        private const int Phase3SafeGapBladeTrailMaxPoints = 12; // lebih pendek dari trail blade sweep (30) - putarannya cepet & terus-terusan, trail panjang bakal numpuk jadi blur gepeng

        // DEBUG SEMENTARA: set true buat nampilin info kontak blade tiap 10 tick di layar (dist,
        // contactRadius, reach, hitCooldown) + notif pas kondisi kontak match & pas Hurt() beneran
        // dipanggil. Matiin lagi (set false) begitu udah ketemu akar masalahnya - ini bukan buat
        // production, cuma alat diagnosa.
        private const bool MeleeDebugPrint = true;

        // ---- Broken Hero Sword - obstacle/pengganggu di 4 batas arena (improvement baru) ----
        private const int Phase3ObstacleTelegraphTicks = 50;   // ~0.83 detik garis spoiler kelihatan sebelum dash-nya jalan
        private const float Phase3ObstacleDashSpeed = 30f;     // px dunia per tick pas ngedash
        private const int Phase3ObstacleCooldownTicks = 120;   // 2 detik jeda SETELAH dash selesai, sebelum spoiler garis berikutnya muncul (persis sesuai request)
        private const int Phase3ObstacleDamage = 85;
        private const float Phase3ObstacleContactRadius = 30f;
        // FIX #4 ("harus disetiap angka cartesius"): setiap angka grid dari -range..+range SEKARANG
        // dijamin punya hazard sendiri (lihat InitPhase3ObstacleSwords, WhoAmI_Phase3ObstacleSwords.cs)
        // - jumlah hazard nggak lagi diatur constant terpisah, tapi langsung diturunkan dari range ini
        // (2*range+1 angka x 2 sumbu). -8..8 (bukan -10..10 penuh) kasih sedikit margin dari sudut
        // biar nggak numpuk sama prop Ranged yang nangkring di pojok.
        private const int Phase3ObstacleLaneRange = 8;

        // ---- Asset kustom (sprite sword sendiri, BUKAN icon item dari inventory - lihat catatan di
        // SetupMeleePattern & DrawPhase3Cartesian soal kenapa icon item bikin bilahnya "miring") ----
        // CATATAN PENTING: sesuaikan folder ini kalau lokasi PNG di project kalian beda - path di bawah
        // cuma nebak berdasarkan namespace file ini (TheSanity.GlobalNPC.Bosses.WhoAmI). Taruh keempat
        // PNG horizontal (Night_s_Edge_Horizontal, True_Night_s_Edge_Horizontal, Terra_Blade_Horizontal,
        // Broken_Hero_Sword_Horizontal) persis di folder ini (build action-nya otomatis "Content" asal
        // ekstensinya .png, standar tModLoader).
        private const string SwordAssetFolder = "GlobalNPC/Bosses/WhoAmI/";

        private static Texture2D _swordTexNightsEdge;
        private static Texture2D _swordTexTrueNightsEdge;
        private static Texture2D _swordTexTerraBlade;
        private static Texture2D _swordTexBrokenHero;

        // 3 sprite melee sendiri, di-cycle per index prop (i % 3) - lihat SetupMeleePattern.
        private Texture2D GetCustomMeleeSwordTexture(int index)
        {
            switch (((index % 3) + 3) % 3)
            {
                case 0: return _swordTexNightsEdge ??= Mod.Assets.Request<Texture2D>(SwordAssetFolder + "Night_s_Edge_Horizontal", AssetRequestMode.ImmediateLoad).Value;
                case 1: return _swordTexTrueNightsEdge ??= Mod.Assets.Request<Texture2D>(SwordAssetFolder + "True_Night_s_Edge_Horizontal", AssetRequestMode.ImmediateLoad).Value;
                default: return _swordTexTerraBlade ??= Mod.Assets.Request<Texture2D>(SwordAssetFolder + "Terra_Blade_Horizontal", AssetRequestMode.ImmediateLoad).Value;
            }
        }

        private Texture2D BrokenHeroSwordTexture => _swordTexBrokenHero ??= Mod.Assets.Request<Texture2D>(SwordAssetFolder + "Broken_Hero_Sword_Horizontal", AssetRequestMode.ImmediateLoad).Value;

        // ---------------- STATIC (dibaca WhoAmIPhase3ArenaPlayer & WhoAmIPhase3Bolt tanpa perlu
        // referensi ke instance NPC-nya) ----------------
        public static bool Phase3ArenaActive = false;
        public static Vector2 Phase3ArenaCenterStatic = Vector2.Zero;
        public static float Phase3ArenaHalfExtentStatic = Phase3ArenaHalfExtent;
        public static float Phase3FadeAlpha = 0f;

        // FIX (batas projectile break nggak sesuai diagram Cartesius): dulu 2 tempat beda (proyektil
        // "real weapon" di MaintainPhase3MageBolts & WhoAmIPhase3Bolt.AI di bawah) mutusin "keluar
        // arena" pakai Vector2.Distance ke titik pusat - itu cek LINGKARAN (radius). Padahal arena
        // Cartesian di sini bentuknya KOTAK axis-aligned (persis kayak grid yang digambar & player
        // clamp di WhoAmIPhase3ArenaPlayer, yang emang udah bener per-sumbu). Efeknya proyektil yang
        // jalan DIAGONAL (misal ke arah pojok) mati jauh SEBELUM nyampe batas kotak beneran, karena
        // radius lingkaran = HalfExtent lebih pendek dari jarak ke pojok kotak (~1.414x HalfExtent) -
        // persis bug yang sama yang udah dicatet di komentar SetupRangedPattern soal prop kepental
        // keluar. Helper ini nyamain logikanya jadi per-sumbu (X dan Y dicek terpisah), match 1:1
        // sama WhoAmIPhase3ArenaPlayer.PreUpdateMovement, dipakai di semua tempat proyektil dicek
        // keluar arena atau nggak.
        public static bool IsOutsidePhase3Arena(Vector2 point, Vector2 center, float halfExtent, float tolerance = 0f)
        {
            Vector2 rel = point - center;
            float limit = halfExtent + tolerance;
            return Math.Abs(rel.X) > limit || Math.Abs(rel.Y) > limit;
        }

        // ---------------- INSTANCE FIELDS ----------------
        private bool phase3Triggered = false;
        private Vector2 phase3ArenaCenter = Vector2.Zero;
        private Vector2 phase3ReturnPlayerPos = Vector2.Zero;
        private Vector2 phase3ReturnBossPos = Vector2.Zero;
        private int phase3TransitionTimer = 0;
        private bool phase3TeleportedIn = false;

        private int phase3WaveNumber = 0;
        private int phase3ClassIndex = -1; // 0 Melee, 1 Ranged, 2 Magic, 3 Summon (sama urutan TryDetectPlayerClass)
        private int phase3SubStage = 0;    // 0 grow-in, 1 active, 2 resolve
        private int phase3StageTimer = 0;

        private readonly List<WhoAmIPhase3WeaponProp> phase3Props = new List<WhoAmIPhase3WeaponProp>();

        private Vector2 phase3RiftWorldPos = Vector2.Zero;
        private Point phase3RiftGrid = Point.Zero;
        private int phase3RiftCycle = 0;
        private int phase3RiftTimer = 0;
        private float phase3MeleeGroupSpin = 0f; // sudut bersama buat SEMUA bilah melee - LEGACY (peninggalan pola "3 bilah muter statis" lama), nggak dipakai lagi sama state machine sweep di bawah, ditinggal biar nggak nabrak referensi lama yang lain

        // ---- Melee (Phase 3) "270-degree sweep" - 1 Terra Blade raksasa, ngayun cepat lewat busur
        // 270 derajat, nyisain SATU celah aman 90 derajat yang posisinya di-random ULANG tiap siklus
        // (lihat ResetMeleeSweep). State machine ini loop terus sepanjang Phase3MeleeActiveDuration -
        // kelipatan pas dari Phase3MeleeSwingsPerWave siklus penuh (lihat TickMeleeActive). ----
        private int phase3MeleeState = 0;         // 0 = Telegraph (wind-up, diam di start angle), 1 = Sweep (ngayun cepat), 2 = Cooldown (sembunyi/redup)
        private int phase3MeleeTimer = 0;          // tick counter internal buat state saat ini
        private float phase3MeleeStartAngle = 0f;  // sudut awal ayunan - sisi lain dari celah aman 90 derajat
        private float phase3MeleeEndAngle = 0f;    // sudut akhir ayunan = phase3MeleeStartAngle + 270 derajat (MathHelper.Pi * 1.5f)
        private float phase3MeleePrevSweepAngle = 0f; // sudut blade di TICK SEBELUMNYA selama state Sweep - dipakai buat sub-step hit testing (lihat Phase3MeleeSweepHitSubsteps di TickMeleeActive) biar nggak ada celah sudut yang "kelewatan" antar tick
        private float phase3SafeGapBladeSpin = 0f; // sudut putar SENDIRI (poros sendiri, kayak baling-baling) buat Terra Blade kecil penanda celah aman - TOTAL independen dari phase3MeleeStartAngle/EndAngle (yang cuma dipakai buat nentuin POSISI marker, bukan rotasinya)
        private readonly List<float> phase3SafeGapBladeSpinTrail = new List<float>(); // history sudut spin tiap tick, dipakai gambar after-image marker - lihat Phase3SafeGapBladeTrailMaxPoints
        private float phase3MageSpinAngle = 0f;  // sudut bersama buat 2 senjata mage SETELAH kegabung ke tengah - dipakai buat posisi orbit KECIL & arah tembak spiral (lihat TickMagicActive)

        // Lacak proyektil Phase 3 yang pakai proyektil ASLI senjatanya (bukan WhoAmIPhase3Bolt generik
        // - lihat FireRealWeaponBoltsInAllDirections buat Magic & FireRealRangedProjectileAt buat
        // Ranged). Sama persis pola-nya kayak activeYoyoBoomerangProjectiles (WhoAmI.cs/
        // WhoAmI_Helpers.cs): key = index proyektil, value = kecepatan garis lurus yang DIKUNCI di saat
        // spawn. Perlu dikunci manual tiap tick karena proyektil ASLI senjata (bukan bikinan sendiri)
        // bisa punya AI vanilla-nya sendiri yang homing/melengkung/kena gravity dsb - itu yang mau kita
        // matiin ("homingnya dimatikan"). Nama field-nya masih "MageBolt" dari waktu cuma dipakai
        // pattern Magic, tapi sekarang dipakai bareng sama pattern Ranged juga - bukan cuma magic.
        //
        // INTERNAL (bukan private lagi): dibaca WhoAmIProjectileGuard.PostAI (WhoAmIProjectileGuard.cs)
        // supaya penguncian kecepatan lurus + Kill()-di-batas-arena bisa DIPAKSA ULANG persis SETELAH
        // AI asli tiap proyektil jalan tiap tick - dulu cuma dikunci sekali dari sisi NPC
        // (MaintainPhase3MageBolts, dipanggil dari HandlePhase3Arena), yang jalan SEBELUM proyektilnya
        // sendiri dapat giliran AI di tick yang sama. Kalau proyektil ASLI itu punya homing bawaan
        // sendiri (Chlorophyte Bullet, Vampire Knives, dst - bukan cuma Typhoon/minion yang udah
        // dipatch WhoAmIProjectileGuard), homing bawaan itu masih sempat "menang" telat di tick yang
        // sama sebelum sempat dikoreksi lagi tick berikutnya - kadang nyeret proyektilnya nabrak ke
        // boss sendiri (satu2nya NPC lain di arena) & mati sebelum nyentuh batas. Ngoreksi ulang di
        // PostAI proyektilnya sendiri (yang DIJAMIN jalan setelah AI proyektil itu, apapun urutan
        // NPC-vs-Projectile update) nutup celah itu total.
        internal readonly Dictionary<int, Vector2> phase3MageBoltLockedVelocity = new Dictionary<int, Vector2>();

        // ============================================================================================
        // TRIGGER (dipanggil dari AI(), lihat WhoAmI.cs) & TOP-LEVEL DISPATCH
        // ============================================================================================
        private void TriggerPhase3Start(Player player)
        {
            phase3Triggered = true;
            phase3ReturnPlayerPos = player.Center;
            phase3ReturnBossPos = NPC.Center;

            // Arena-nya ditaruh di ketinggian TETAP dekat langit-langit dunia (lapisan space), BUKAN
            // hasil pengurangan dari posisi boss - Y negatif (di luar 0..Main.maxTilesY) bisa bikin
            // banyak sistem vanilla (lighting, dust, dsb) yang nge-index Main.tile[x,y] tanpa
            // bounds-check langsung crash (IndexOutOfRangeException). X tetap disamain sama posisi
            // fight sekarang (biar nggak jauh2 amat), tapi di-clamp juga biar nggak kebawa ke luar
            // batas dunia kalau fight-nya kebetulan lagi deket pinggir map.
            //
            // FIX (lihat catatan Phase3ArenaTopMargin di deklarasi konstanta): safeArenaY SEKARANG
            // dihitung dari HalfExtent + margin, BUKAN angka tile tetap (110) kayak sebelumnya - biar
            // tembok ATAS arena (safeArenaY - HalfExtent) selalu punya buffer nyata ke Y=0 (puncak
            // absolut dunia), nggak peduli GridUnit/HalfExtent di-tweak lagi nanti ke berapapun.
            float safeArenaX = MathHelper.Clamp(NPC.Center.X, Phase3ArenaHalfExtent + 200f, (Main.maxTilesX * 16f) - Phase3ArenaHalfExtent - 200f);
            float safeArenaY = Phase3ArenaHalfExtent + Phase3ArenaTopMargin;
            phase3ArenaCenter = new Vector2(safeArenaX, safeArenaY);
            Phase3ArenaCenterStatic = phase3ArenaCenter;
            Phase3ArenaHalfExtentStatic = Phase3ArenaHalfExtent;
            Phase3ArenaActive = false; // baru bener2 "active" (containment dkk mulai kepakai) begitu fade udah gelap total - lihat HandlePhase3Transition

            aiState = STATE_PHASE3_TRANSITION;
            aiTimer = 0;
            phase3TransitionTimer = 0;
            phase3TeleportedIn = false;
            phase3WaveNumber = 0;
            Phase3FadeAlpha = 0f;

            // Broken Hero Sword hazard di 4 batas arena - hidup terus sepanjang gauntlet, LEPAS dari
            // wave/class apa yang lagi aktif (bukan bagian dari SetupPhase3Wave), makanya di-init sekali
            // di sini pas gauntlet mulai, bukan tiap wave ganti. Lihat WhoAmI_Phase3ObstacleSwords.cs.
            InitPhase3ObstacleSwords();

            NPC.velocity = Vector2.Zero;
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            isCurrentlyChanneling = false;
            IsCutsceneActive = true;
            CutsceneCameraTarget = player.Center;
            NPC.netUpdate = true;

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Roar, player.Center);
            Main.NewText("The mirror can't hold this shape any longer...", 190, 150, 240);
        }

        private void HandlePhase3(Player player)
        {
            UpdateAmbientBossVFX(player); // aura tetap "napas" selama gauntlet, sama kayak state cutscene lain

            NPC.dontTakeDamage = true;
            NPC.damage = 0;

            if (aiState == STATE_PHASE3_TRANSITION)
                HandlePhase3Transition(player);
            else
                HandlePhase3Arena(player);
        }

        // ============================================================================================
        // TRANSITION: fade ke hitam -> teleport player+boss ke arena -> fade balik
        // ============================================================================================
        private void HandlePhase3Transition(Player player)
        {
            IsCutsceneActive = true;

            if (!phase3TeleportedIn)
            {
                // Belum gelap total - kamera masih di posisi normal (nyusul player), biar transisinya
                // kerasa halus, bukan potongan mendadak.
                CutsceneCameraTarget = Vector2.Lerp(CutsceneCameraTarget, player.Center, 0.08f);

                phase3TransitionTimer++;
                Phase3FadeAlpha = MathHelper.Clamp(phase3TransitionTimer / (float)Phase3FadeTicks, 0f, 1f);

                if (phase3TransitionTimer >= Phase3FadeTicks)
                {
                    // Layar udah hitam total - aman buat mindahin posisi tanpa kelihatan "lompat".
                    player.Center = phase3ArenaCenter;
                    player.velocity = Vector2.Zero;
                    NPC.Center = phase3ArenaCenter;
                    NPC.velocity = Vector2.Zero;
                    CutsceneCameraTarget = phase3ArenaCenter;

                    Phase3ArenaActive = true; // containment/anti-teleport (WhoAmIPhase3ArenaPlayer) mulai kepakai dari sini

                    phase3TeleportedIn = true;
                    phase3TransitionTimer = 0;

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Shatter, phase3ArenaCenter);
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, phase3ArenaCenter);
                }
            }
            else
            {
                // Masih di balik layar hitam (lagi fade OUT) - kunci posisi keduanya di arena biar
                // gak ada yang somehow kegeser sebelum player beneran "lihat" tempatnya.
                player.Center = phase3ArenaCenter;
                player.velocity = Vector2.Zero;
                NPC.Center = phase3ArenaCenter;
                NPC.velocity = Vector2.Zero;

                phase3TransitionTimer++;
                Phase3FadeAlpha = MathHelper.Clamp(1f - phase3TransitionTimer / (float)Phase3FadeTicks, 0f, 1f);

                if (phase3TransitionTimer >= Phase3FadeTicks)
                {
                    Phase3FadeAlpha = 0f;
                    phase3TransitionTimer = 0;
                    aiState = STATE_PHASE3_ARENA;
                    aiTimer = 0;
                }
            }
        }

        // ============================================================================================
        // ARENA LOOP
        // ============================================================================================
        private void HandlePhase3Arena(Player player)
        {
            Phase3ArenaActive = true;
            IsCutsceneActive = true;
            CutsceneCameraTarget = phase3ArenaCenter;

            // "bossnya tidak akan bisa menerima dmg dan tidak akan melakukan apa apa saat phase 3,
            // tidak akan bergerak" - dikunci mati total di tengah arena.
            NPC.Center = phase3ArenaCenter;
            NPC.velocity = Vector2.Zero;
            NPC.direction = 1;

            if (phase3WaveNumber == 0)
            {
                phase3WaveNumber = 1;
                SetupPhase3Wave(player);
            }

            // Jalan tiap tick TERLEPAS dari sub-stage (grow/active/resolve) - bolt yang masih kebang
            // dari volley sebelumnya tetap harus dijaga tetap lurus & kena batas arena walaupun wave-nya
            // udah pindah sub-stage.
            MaintainPhase3MageBolts();

            // FIX (request "broken hero sword cuma pas pattern melee"): dulu jalan TIAP TICK lepas dari
            // class wave manapun (Melee/Ranged/Magic/Summon), makanya obstacle sword ini nongol terus di
            // SEMUA pattern. Sekarang dibatesin cuma jalan pas phase3ClassIndex == 0 (Melee) - di wave
            // Ranged/Magic/Summon, hazard-nya berhenti nge-tick (freeze di state terakhirnya) dan nggak
            // digambar (lihat gate yang sama di DrawPhase3ObstacleSwords, dipanggil dari
            // DrawPhase3Cartesian), jadi otomatis nyambung lagi persis dari state yang sama begitu wave
            // Melee berikutnya mulai - nggak perlu re-init.
            if (phase3ClassIndex == 0)
                TickPhase3ObstacleSwords(player);

            switch (phase3SubStage)
            {
                case 0: TickPhase3Grow(player); break;
                case 1: TickPhase3Active(player); break;
                default: TickPhase3Resolve(player); break;
            }
        }

        private void SetupPhase3Wave(Player player)
        {
            phase3Props.Clear();
            phase3RiftCycle = 0;
            phase3RiftTimer = 0;
            KillAllTrackedPhase3MageBolts(); // wave (dan bisa jadi class) ganti - jangan biarin bolt volley kemarin nyangkut ke wave baru

            if (!TryDetectPlayerClass(player, out int detected, out _))
                detected = 0; // gear player nggak lagi condong ke class manapun - fallback ke Melee daripada nge-stuck

            phase3ClassIndex = detected;

            switch (phase3ClassIndex)
            {
                case 0: SetupMeleePattern(player); break;
                case 1: SetupRangedPattern(player); break;
                case 2: SetupMagePattern(player); break;
                default: /* Summon (3): nggak butuh weapon props, cuma rift minigame */ break;
            }

            phase3SubStage = 0;
            phase3StageTimer = 0;

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, phase3ArenaCenter);
        }

        // Referensi: 3 senjata (beda-beda, diambil dari inventory player) nempel deket TENGAH arena
        // (gagang di dekat pusat, bilah ngarah keluar ke kuadrannya masing2), muter BARENG sebagai
        // satu grup kayak gear/gergaji berputar PELAN - BUKAN masing2 orbit sendiri2 jauh di pinggir
        // arena (desain lama). Kecepatan putar sama buat semua bilah (lihat phase3MeleeGroupSpin di
        // TickMeleeActive), cuma sudut awalnya beda 120 derajat per senjata (360/3) biar nyebar rata.
        // Scale-nya membesar terus sampai ujung bilah MENTOK pas di batas arena (lihat
        // meleeTargetScale di bawah, dihitung dari Phase3ArenaHalfExtent).
        // Referensi baru (request "270-degree sweep"): SATU Terra Blade raksasa nempel gagangnya di
        // tengah arena, ngayun CEPAT lewat busur 270 derajat, nyisain satu celah aman 90 derajat yang
        // posisinya di-random ULANG tiap siklus (Telegraph -> Sweep -> Cooldown -> ulang, lihat
        // TickMeleeActive & ResetMeleeSweep). Bukan lagi 3 bilah muter statis terus-terusan.
        private void SetupMeleePattern(Player player)
        {
            var weapons = CollectClassWeapons(player, DamageClass.Melee, 1);
            Texture2D bladeTex = _swordTexTerraBlade ??= Mod.Assets.Request<Texture2D>(SwordAssetFolder + "Terra_Blade_Horizontal", AssetRequestMode.ImmediateLoad).Value;

            // "membesar sampai mentok ke batas arena" - target scale dihitung MUNDUR dari radius arena
            // dari lebar piksel ASLI tekstur Terra Blade itu sendiri (sama persis rumusnya kayak versi
            // lama), pakai MeleeBladeOriginXFraction & Phase3ArenaHalfExtent yang sudah ada supaya
            // estimasi panjang bilah tetap akurat mentok pas di batas arena.
            float reachPerScale = bladeTex != null ? bladeTex.Width * (1f - MeleeBladeOriginXFraction) : 1f;
            float targetScale = reachPerScale > 0.01f ? Phase3ArenaHalfExtent / reachPerScale : 1f;

            phase3Props.Add(new WhoAmIPhase3WeaponProp
            {
                Item = weapons.Count > 0 ? weapons[0] : null,
                Scale = targetScale, // langsung full scale - nggak ada animasi "membesar" lagi buat sweep tunggal ini, cuma Telegraph diam di start angle
                TargetScale = targetScale,
                Orbits = true, // dipakai DrawPhase3Cartesian buat milih origin gambar hilt-anchored
                CustomTexture = bladeTex,
            });

            ResetMeleeSweep();
            phase3MeleeState = 0;
            phase3MeleeTimer = 0;

            // Wave Melee baru mulai - reset spin & trail marker celah aman biar nggak ada "loncatan"
            // after-image nyambung dari wave/class sebelumnya begitu marker muncul lagi.
            phase3SafeGapBladeSpin = 0f;
            phase3SafeGapBladeSpinTrail.Clear();
        }

        // Helper: random-in ULANG posisi celah aman 90 derajat & susun ulang start/end angle ayunan.
        // Celah aman = 90 derajat yang TIDAK dilewati sweep (dari phase3MeleeEndAngle putaran
        // sebelumnya sampai phase3MeleeStartAngle putaran berikutnya). Start angle di-random bebas
        // 0..2pi, lalu end angle = start + 270 derajat (MathHelper.Pi * 1.5f) - sisa 90 derajat yang
        // nggak kelewatan itu otomatis jadi celah amannya.
        private void ResetMeleeSweep()
        {
            phase3MeleeStartAngle = (float)Main.rand.NextDouble() * MathHelper.TwoPi;
            phase3MeleeEndAngle = phase3MeleeStartAngle + MathHelper.Pi * 1.5f; // 270 derajat maju dari start - sisa 90 derajat di belakangnya jadi celah aman

            foreach (var prop in phase3Props)
                prop.OldRotations.Clear(); // siklus baru, trail lama nggak relevan lagi
        }

        private void SetupRangedPattern(Player player)
        {
            var weapons = CollectClassWeapons(player, DamageClass.Ranged, 4);
            // FIX ("projectile-nya ilang instan, kayaknya senjatanya diluar arena"): (-1,-1)/(1,-1)/dst
            // itu vektor DIAGONAL yang panjangnya bukan 1, tapi sqrt(2) ~ 1.414 (Pythagoras). Dikali
            // (Phase3ArenaHalfExtent - 70f) hasilnya prop kepental ~1.414x lebih jauh dari radius yang
            // dimaksud - itu udah ngelewatin Phase3ArenaHalfExtent, bahkan ngelewatin toleransi
            // "Phase3ArenaHalfExtent + 60f" yang dipakai MaintainPhase3MageBolts buat mutusin kapan
            // Kill() proyektil yang keluar arena. Efeknya: prop senjatanya sendiri render-nya udah di
            // LUAR arena/grid, dan proyektil yang di-spawn dari posisi prop itu literally udah "diluar
            // batas" sejak lahir - kena Kill() di tick maintenance berikutnya sebelum sempat kelihatan
            // jalan ke player. Normalize() di bawah bikin panjang vektornya jadi 1 dulu, baru dikali
            // radius yang dimaksud - hasilnya prop-nya beneran duduk di SUDUT arena TAPI masih di
            // DALAM batasnya, bukan nembus keluar.
            Vector2[] corners =
            {
                new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1)
            };
            for (int c = 0; c < corners.Length; c++)
                corners[c].Normalize();

            for (int i = 0; i < weapons.Count; i++)
            {
                phase3Props.Add(new WhoAmIPhase3WeaponProp
                {
                    Item = weapons[i],
                    Scale = 0.05f,
                    TargetScale = 4.5f,
                    Orbits = false,
                    BaseOffset = corners[i % corners.Length] * (Phase3ArenaHalfExtent - 70f),
                    FireTimer = 40 + i * 10, // stagger tembakan pertama biar nggak semua nembak bebarengan persis
                });
            }
        }

        // "Senjata magic tsb" - 2 senjata magic beda-beda diambil dari inventory player, diem di
        // kiri/kanan pusat arena (BUKAN muter - dulu ada prop.Rotation += 0.02f di TickMagicActive
        // yang muterin sprite-nya terus2an tanpa alasan/gak nyambung sama arah tembakan, itu bug
        // "muter gajelas" yang dilaporkan - sekarang dihapus, Rotation dipatok diem). Tiap senjata
        // nembak volley PROYEKTIL ASLI-nya sendiri ke SEGALA ARAH tiap 2 detik - lihat TickMagicActive
        // & FireRealWeaponBoltsInAllDirections.
        private void SetupMagePattern(Player player)
        {
            var weapons = CollectClassWeapons(player, DamageClass.Magic, 2);
            for (int i = 0; i < weapons.Count; i++)
            {
                phase3Props.Add(new WhoAmIPhase3WeaponProp
                {
                    Item = weapons[i],
                    Scale = 0.05f,
                    TargetScale = 4f,
                    Orbits = false,
                    // BaseOffset sekarang cuma titik AWAL sebelum animasi "kegabung ke tengah" (lihat
                    // TickMagicActive) - begitu merge selesai, posisi aktualnya dihitung dari
                    // phase3MageSpinAngle/Phase3MageSpinRadius, BaseOffset ini nggak dipakai lagi.
                    BaseOffset = new Vector2(i == 0 ? -90f : 90f, 0f),
                    // Stagger dikit biar 2 senjata nggak mulai nembak PERSIS di tick yang sama begitu
                    // fase merge selesai.
                    FireTimer = i * (Phase3MageSpiralFireInterval / 2),
                });
            }

            phase3MageSpinAngle = 0f;
        }

        // ---------------- SUB-STAGE 0: GROW-IN ----------------
        private void TickPhase3Grow(Player player)
        {
            phase3StageTimer++;
            float t = MathHelper.Clamp(phase3StageTimer / (float)Phase3GrowTicks, 0f, 1f);
            float eased = 1f - (float)Math.Pow(1f - t, 3);

            // FIX ("kadang bug swordnya mengecil"): akar masalahnya di SINI. SetupMeleePattern taruh
            // blade Melee LANGSUNG di full Scale (targetScale, nggak ada animasi membesar - lihat
            // komentarnya), tapi loop grow-in ini dulu nge-lerp SEMUA prop (termasuk blade Melee) dari
            // 0.05 ke TargetScale tiap tick TANPA kecuali. Akibatnya di tick PERTAMA grow-in, blade
            // yang barusan di-set full-size sama SetupMeleePattern langsung KETIBAN nilai lerp yang
            // masih deket 0.05 (eased-nya kecil banget di awal) - kelihatan di layar sebagai blade
            // yang sekilas full-size lalu TIBA-TIBA MENGECIL sebelum growing lagi ke ukuran normal.
            // Ranged/Magic nggak kena masalah ini karena mereka MEMANG di-setup mulai dari Scale=0.05
            // (grow-in beneran dipakai buat mereka), cuma Melee (prop.Orbits == true) yang harusnya
            // dikecualikan total dari lerp ini. Blade Melee tetap disembunyikan (Scale 0) sepanjang
            // grow-in - konsisten sama state Telegraph (TickMeleeActive case 0) yang juga
            // menyembunyikannya total sampai sweep beneran mulai; nggak ada lagi kedipan gede->kecil.
            foreach (var prop in phase3Props)
                prop.Scale = prop.Orbits ? 0f : MathHelper.Lerp(0.05f, prop.TargetScale, eased);

            if (phase3StageTimer >= Phase3GrowTicks)
            {
                foreach (var prop in phase3Props)
                    prop.Scale = prop.Orbits ? 0f : prop.TargetScale;

                phase3SubStage = 1;
                phase3StageTimer = 0;

                if (phase3ClassIndex == 3)
                    StartNextRift(player);

                Main.NewText(Phase3WaveStartMessage(phase3ClassIndex), ArchetypeGlowColor(phase3ClassIndex));
            }
        }

        // ---------------- SUB-STAGE 1: ACTIVE ----------------
        private void TickPhase3Active(Player player)
        {
            phase3StageTimer++;

            switch (phase3ClassIndex)
            {
                case 0: TickMeleeActive(player); break;
                case 1: TickRangedActive(player); break;
                case 2: TickMagicActive(player); break;
                default: TickSummonActive(player); break;
            }
        }

        // State machine "270-degree sweep": Telegraph (diam di start angle) -> Sweep (ngayun cepat
        // Start->End lewat Lerp, ngecek kontak & nyalain dust) -> Cooldown (blade disembunyikan) ->
        // ResetMeleeSweep() buat random-in celah aman berikutnya -> ulang dari Telegraph. Loop ini
        // jalan terus sepanjang Phase3MeleeActiveDuration - dijamin tepat Phase3MeleeSwingsPerWave (3)
        // siklus penuh (swing) per wave, BUKAN cuma 1x.
        private void TickMeleeActive(Player player)
        {
            // Marker "celah aman" muter TERUS di porosnya sendiri sepanjang wave Melee aktif (state
            // Telegraph/Sweep/Cooldown manapun juga) - nggak ngikut state machine sweep sama sekali,
            // cuma putaran baling-baling yang jalan konstan. Posisinya sendiri (dihitung di
            // DrawPhase3Cartesian dari phase3MeleeEndAngle) baru pindah pas ResetMeleeSweep() jalan.
            phase3SafeGapBladeSpin += Phase3SafeGapBladeSelfSpinSpeed;
            if (phase3SafeGapBladeSpin > MathHelper.TwoPi) phase3SafeGapBladeSpin -= MathHelper.TwoPi;

            // Trail-nya numpuk tiap tick JALAN TERUS sepanjang wave Melee aktif (beda dari trail blade
            // sweep utama yang cuma numpuk pas state Sweep) - konsisten sama marker yang muter terus
            // nggak peduli state Telegraph/Sweep/Cooldown yang mana.
            phase3SafeGapBladeSpinTrail.Add(phase3SafeGapBladeSpin);
            while (phase3SafeGapBladeSpinTrail.Count > Phase3SafeGapBladeTrailMaxPoints)
                phase3SafeGapBladeSpinTrail.RemoveAt(0);

            var prop = phase3Props.Count > 0 ? phase3Props[0] : null;

            if (prop != null)
            {
                prop.WorldPosition = phase3ArenaCenter; // gagang tetap nempel PERSIS di tengah arena

                if (prop.HitCooldown > 0) prop.HitCooldown--;

                phase3MeleeTimer++;

                switch (phase3MeleeState)
                {
                    // ---- STATE 0: TELEGRAPH (wind-up) ----
                    // REVISI (request "hapus semua patern spoilernya"): blade-nya SENDIRI disembunyikan
                    // total (Scale 0) sepanjang 5 detik ini, dan sekarang NGGAK ADA lagi apapun yang
                    // digambar di layar sebagai indikator - murni diam. Rotation tetap dikunci ke
                    // phase3MeleeStartAngle (nggak kepakai buat gambar apa2 selama Scale 0, tapi
                    // disiapin duluan biar nggak ada "loncatan" sudut pas blade muncul di tick pertama
                    // state Sweep).
                    case 0:
                        prop.Rotation = phase3MeleeStartAngle;
                        prop.Scale = 0f;

                        if (phase3MeleeTimer >= Phase3MeleeSweepTelegraphTicks)
                        {
                            phase3MeleeState = 1;
                            phase3MeleeTimer = 0;
                            phase3MeleePrevSweepAngle = phase3MeleeStartAngle; // titik awal buat sub-step hit testing di tick pertama Sweep
                            prop.OldRotations.Clear(); // trail dimulai bersih tiap kali sweep baru mulai

                            // "Sword muncul setelah 5 detik spoiler pattern" - cue kemunculan blade:
                            // burst dust persis di sepanjang garis arc spoiler (bukan di satu titik)
                            // biar kerasa "materialize sepanjang busur", plus suara singkat.
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, phase3ArenaCenter);
                            for (int b = 0; b < 20; b++)
                            {
                                float bAngle = MathHelper.Lerp(phase3MeleeStartAngle, phase3MeleeEndAngle, b / 19f);
                                Vector2 burstPos = phase3ArenaCenter + new Vector2((float)Math.Cos(bAngle), (float)Math.Sin(bAngle)) * Phase3MeleeSpoilerRadius;
                                Dust d = Dust.NewDustPerfect(burstPos, DustID.GreenTorch, Vector2.Zero, 0, Color.LimeGreen, 1.4f);
                                d.noGravity = true;
                            }
                        }
                        break;

                    // ---- STATE 1: SWEEPING ----
                    // Ayunan cepat dari Start ke End lewat MathHelper.Lerp, ngecek kontak SEPANJANG
                    // bilah (dari pusat sampai ujung, sama kayak logic lama) tiap tick sweep-nya jalan.
                    // Sword-nya cuma nongol/kegambar mulai state ini (Scale kembali ke TargetScale) -
                    // ini persis momen "muncul setelah 5 detik spoiler pattern" yang diminta.
                    case 1:
                    {
                        float sweepT = Phase3MeleeSweepSweepTicks > 0 ? MathHelper.Clamp(phase3MeleeTimer / (float)Phase3MeleeSweepSweepTicks, 0f, 1f) : 1f;
                        float angle = MathHelper.Lerp(phase3MeleeStartAngle, phase3MeleeEndAngle, sweepT);
                        prop.Rotation = angle;
                        prop.Scale = prop.TargetScale;

                        // Trail: simpan rotation tiap tick, cap ke Phase3MeleeSweepTrailMaxPoints entri
                        // terbaru (buang yang paling lama begitu kepenuhan) - dipakai DrawPhase3Cartesian
                        // buat gambar "bekas ayunan" blade yang fade makin lama makin transparan. Cap-nya
                        // sekarang >= Phase3MeleeSweepSweepTicks (lihat konstanta) jadi SATU siklus penuh
                        // selalu nyisain jejak dari awal sampai akhir ayunan - after image beneran kebaca.
                        prop.OldRotations.Add(angle);
                        while (prop.OldRotations.Count > Phase3MeleeSweepTrailMaxPoints)
                            prop.OldRotations.RemoveAt(0);

                        // reach & tebal dihitung LANGSUNG dari prop.CustomTexture.Width/Height (ukuran
                        // piksel tekstur ASLI yang beneran dipakai spriteBatch.Draw), pakai origin
                        // fraction yang sama persis (MeleeBladeOriginXFraction) biar hitbox selalu
                        // sinkron sama sprite yang beneran kegambar.
                        Texture2D bladeTex = prop.CustomTexture;
                        float texWidth = bladeTex?.Width ?? 0f;
                        float texHeight = bladeTex?.Height ?? 0f;
                        float bladeReach = texWidth * (1f - MeleeBladeOriginXFraction) * prop.Scale;
                        Vector2 tip = phase3ArenaCenter + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * bladeReach;

                        const float minContactRadius = 24f;
                        float contactRadius = MathHelper.Max(minContactRadius, (texHeight * 0.5f) * prop.Scale);

                        // FIX ("celah spoiler ga sesuai celah safe beneran"): cek kontak nggak cuma
                        // SEKALI di sudut akhir tick ini (angle) - tapi di Phase3MeleeSweepHitSubsteps
                        // titik yang disebar merata antara phase3MeleePrevSweepAngle (sudut tick
                        // sebelumnya) sampai angle (sudut tick sekarang). Ayunan 270 derajat cuma 18
                        // tick = ~15 derajat lompatan per tick kalau dicek sekali doang - cukup lebar
                        // buat "kelewatan" celah tipis antara 2 sample. Sub-step ini mastiin hitbox-nya
                        // nutupin BENERAN seluruh busur yang disapu blade tiap tick, PERSIS sama kayak
                        // yang digambar (arc spoiler & sprite blade) - bukan cuma titik-titik diskrit.
                        bool hitThisTick = false;
                        for (int s = 1; s <= Phase3MeleeSweepHitSubsteps && !hitThisTick; s++)
                        {
                            float subAngle = MathHelper.Lerp(phase3MeleePrevSweepAngle, angle, s / (float)Phase3MeleeSweepHitSubsteps);
                            Vector2 subTip = phase3ArenaCenter + new Vector2((float)Math.Cos(subAngle), (float)Math.Sin(subAngle)) * bladeReach;

                            Vector2 segDir = subTip - phase3ArenaCenter;
                            float segLenSq = segDir.LengthSquared();
                            float t = segLenSq > 0.0001f ? MathHelper.Clamp(Vector2.Dot(player.Center - phase3ArenaCenter, segDir) / segLenSq, 0f, 1f) : 0f;
                            Vector2 closestOnBlade = phase3ArenaCenter + segDir * t;

                            if (MeleeDebugPrint && Main.GameUpdateCount % 10 == 0 && s == Phase3MeleeSweepHitSubsteps)
                            {
                                float dist = Vector2.Distance(player.Center, closestOnBlade);
                                Main.NewText($"[MeleeDebug] dist={dist:F0} radius={contactRadius:F0} reach={bladeReach:F0} cd={prop.HitCooldown}", Color.Yellow);
                            }

                            if (prop.HitCooldown <= 0 && Vector2.Distance(player.Center, closestOnBlade) < contactRadius)
                                hitThisTick = true;
                        }

                        phase3MeleePrevSweepAngle = angle; // simpan buat sub-step tick BERIKUTNYA

                        if (hitThisTick)
                        {
                            // Slot immunity dipertahankan sama persis (ImmunityCooldownID.DD2OgreKnockback) -
                            // lihat catatan FIX panjang di versi lama soal kenapa slot ini yang dipilih.
                            player.Hurt(PlayerDeathReason.ByCustomReason(player.name + " was caught by the sweeping blade."), Phase3MeleeContactDamage, 0, dodgeable: false, cooldownCounter: ImmunityCooldownID.DD2OgreKnockback);
                            prop.HitCooldown = Phase3MeleeHitCooldown;

                            if (MeleeDebugPrint)
                                Main.NewText($"[MeleeDebug] Hurt() called - life after={player.statLife}", Color.Orange);
                        }

                        // Dust di ujung bilah - kerasa "berat"/motion nyata sepanjang ayunan.
                        if (Main.rand.NextBool(2))
                        {
                            Dust d = Dust.NewDustPerfect(tip, DustID.GreenTorch, Vector2.Zero, 0, Color.LimeGreen, 1.6f);
                            d.noGravity = true;
                        }

                        if (phase3MeleeTimer >= Phase3MeleeSweepSweepTicks)
                        {
                            phase3MeleeState = 2;
                            phase3MeleeTimer = 0;
                        }
                        break;
                    }

                    // ---- STATE 2: COOLDOWN ----
                    // Blade disembunyikan (Scale 0) sebentar sebelum siklus berikutnya dimulai lagi.
                    case 2:
                        prop.Scale = 0f;

                        if (phase3MeleeTimer >= Phase3MeleeSweepCooldownTicks)
                        {
                            // FIX BUG ("kadang ga ngeswing" - pola 1 swing lalu 1 windup yang nggak
                            // pernah ngeswing, berulang): akar masalahnya di sini. Satu siklus penuh
                            // (Telegraph 300 + Sweep 18 + Cooldown 30 = 348 tick) MUAT di dalam budget
                            // lama (Phase3StandardActiveDuration, 600 tick), tapi sisanya cuma 252 tick -
                            // nggak cukup buat Telegraph KEDUA (butuh 300 tick sendirian). Dulu kode di
                            // sini SELALU mulai ResetMeleeSweep() lagi tanpa ngecek sisa waktu, jadi
                            // siklus kedua mulai windup normal (blade ilang, nunggu 5 detik) TAPI keburu
                            // kepotong ke Resolve (wave abis) SEBELUM sempat nyampe state Sweep - dari
                            // sudut pandang player kelihatan kayak "boss ngambil ancang-ancang tapi
                            // nggak jadi nyerang", persis pola "1 swing 1 nggak" yang dilaporkan.
                            //
                            // REVISI LANJUTAN (request "swing-berpindah-swing-berpindah-swing", bukan
                            // cuma 1x): sekarang budget-nya sendiri (Phase3MeleeActiveDuration) udah
                            // dipatok PERSIS kelipatan Phase3MeleeCycleTicks (lihat deklarasi konstanta),
                            // jadi cek di bawah ini SELALU meloloskan tepat Phase3MeleeSwingsPerWave (3)
                            // siklus penuh berturut-turut - TIAP kali Cooldown abis bakal ada cukup sisa
                            // waktu buat Telegraph+Sweep lagi, KECUALI pas siklus terakhir udah kelar,
                            // baru berhenti (nggak ada lagi sisa "nanggung" yang bikin windup gantung).
                            int remainingActiveTicks = Phase3MeleeActiveDuration - phase3StageTimer;
                            int ticksNeededForFullSwing = Phase3MeleeSweepTelegraphTicks + Phase3MeleeSweepSweepTicks;

                            if (remainingActiveTicks >= ticksNeededForFullSwing)
                            {
                                ResetMeleeSweep();
                                phase3MeleeState = 0;
                                phase3MeleeTimer = 0;
                            }
                            // else: tetap di state 2 (blade diam/Scale 0) sampai wave ini abis - nggak
                            // ada lagi pengecekan berulang yang perlu, karena remainingActiveTicks cuma
                            // makin abis tiap tick (nggak akan pernah balik cukup lagi di wave yang sama).
                        }
                        break;
                }
            }

            // REVISI (request "swing-berpindah-swing-berpindah-swing"): wave Melee sekarang numpang
            // budget waktunya SENDIRI (Phase3MeleeActiveDuration, kelipatan pas dari 3 siklus penuh),
            // BUKAN Phase3StandardActiveDuration generik yang dipakai Ranged/Magic (600 tick, cuma pas
            // buat 1 siklus Melee + sisa nanggung). Class lain (TickRangedActive/TickMagicActive) tetap
            // numpang Phase3StandardActiveDuration seperti biasa, nggak berubah.
            if (phase3StageTimer >= Phase3MeleeActiveDuration)
            {
                phase3SubStage = 2;
                phase3StageTimer = 0;
            }
        }

        private void TickRangedActive(Player player)
        {
            foreach (var prop in phase3Props)
            {
                prop.WorldPosition = phase3ArenaCenter + prop.BaseOffset;

                Vector2 toPlayer = player.Center - prop.WorldPosition;
                if (toPlayer != Vector2.Zero)
                    prop.Rotation = toPlayer.ToRotation();

                if (prop.FireTimer > 0)
                {
                    prop.FireTimer--;
                }
                else
                {
                    prop.FireTimer = Phase3RangedFireInterval;
                    // FIX ("attack pattern ranger di cartesian tidak menembakkan apa apa"): sebelumnya
                    // ini manggil FireBoltAt, yang cuma nembak WhoAmIPhase3Bolt generik (blob glow
                    // 34px doang, gak nyambung sama identitas senjatanya) - sekarang nembak proyektil
                    // ASLI dari senjata ranged yang lagi dipegang prop ini (bullet/ammo khusus/panah),
                    // sama filosofinya kayak FireRealWeaponBoltsInAllDirections buat Magic. Lihat
                    // FireRealRangedProjectileAt & ResolveRangedRealProjectileType di bawah.
                    FireRealRangedProjectileAt(prop.WorldPosition, prop.Item, player.Center, Phase3RangedBoltSpeed, Phase3RangedBoltDamage);
                }
            }

            if (phase3StageTimer >= Phase3StandardActiveDuration)
            {
                phase3SubStage = 2;
                phase3StageTimer = 0;
            }
        }

        // IMPROVEMENT: dulu 2 senjata magic cuma diem statis di kiri/kanan pusat arena, nembak volley
        // "ke segala arah" tiap 2 detik. Sekarang di awal wave keduanya PUNYA animasi kegabung
        // (converge) dari posisi kiri/kanan menuju TEPAT ke tengah arena, lalu begitu udah nyampe,
        // mereka muter bareng sebagai satu pasangan (kayak baling2 - 180 derajat kebalikan satu sama
        // lain) SAMBIL terus menembak - dan karena arah tembaknya dikunci ke sudut putar saat itu,
        // arah tembaknya ikut berubah terus selama muter (bukan volley diem 360 derajat sekali tembak).
        // Proyektilnya juga sengaja lebih lambat (Phase3MageSpiralBoltSpeed) daripada versi lama
        // (Phase3MageBoltSpeed) biar bullet hell spiral-nya masih kebaca/di-dodge, bukan cuma tembok
        // peluru instan.
        private void TickMagicActive(Player player)
        {
            bool stillMerging = phase3StageTimer <= Phase3MageMergeTicks;

            if (stillMerging)
            {
                float mergeT = MathHelper.Clamp(phase3StageTimer / (float)Phase3MageMergeTicks, 0f, 1f);
                float eased = 1f - (float)Math.Pow(1f - mergeT, 3); // ease-out - cepat di awal, "landing" halus pas nyampe tengah

                foreach (var prop in phase3Props)
                {
                    // Interpolasi dari titik awal (kiri/kanan, BaseOffset) menuju PERSIS titik pusat arena.
                    prop.WorldPosition = phase3ArenaCenter + Vector2.Lerp(prop.BaseOffset, Vector2.Zero, eased);
                    prop.Rotation = MathHelper.Lerp(0f, phase3MageSpinAngle, eased);
                }
            }
            else
            {
                // Udah "kegabung" - dari sini muter bareng sebagai satu pasangan di sekitar titik pusat
                // (radius kecil, Phase3MageSpinRadius, biar masih kebaca sebagai 2 senjata terpisah tapi
                // tetap kerasa "menyatu" di tengah, bukan ngambang jauh kayak posisi awal).
                phase3MageSpinAngle += Phase3MageSpinSpeed;

                for (int i = 0; i < phase3Props.Count; i++)
                {
                    var prop = phase3Props[i];
                    // +Pi per index (i=0/1) => 180 derajat kebalikan antar 2 senjata, kesan baling2 kembar.
                    float angle = phase3MageSpinAngle + MathHelper.Pi * i;
                    Vector2 dir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));

                    prop.WorldPosition = phase3ArenaCenter + dir * Phase3MageSpinRadius;
                    prop.Rotation = angle; // arah hadap sprite ngikutin arah putar - "arah nembaknya juga ikut berubah saat berputar"

                    if (prop.FireTimer > 0)
                    {
                        prop.FireTimer--;
                        continue;
                    }

                    prop.FireTimer = Phase3MageSpiralFireInterval;

                    // Tiap giliran tembak, sebar Phase3MageSpiralArms proyektil rata 360 derajat DI
                    // SEKITAR sudut putar saat ini - karena sudut ini terus berubah tiap tick, garis
                    // proyektil yang kebentuk otomatis jadi pola spiral berputar (bullet hell), bukan
                    // cuma satu volley 360 derajat statis kayak versi lama.
                    for (int arm = 0; arm < Phase3MageSpiralArms; arm++)
                    {
                        float shotAngle = angle + MathHelper.TwoPi * arm / Phase3MageSpiralArms;
                        FireRealWeaponBoltSpiral(prop.WorldPosition, prop.Item, shotAngle, Phase3MageSpiralBoltSpeed);
                    }
                }
            }

            if (phase3StageTimer >= Phase3StandardActiveDuration)
            {
                phase3SubStage = 2;
                phase3StageTimer = 0;
                phase3MageSpinAngle = 0f;
            }
        }

        private void TickSummonActive(Player player)
        {
            if (phase3RiftTimer > 0)
            {
                phase3RiftTimer--;
                if (phase3RiftTimer <= 0)
                    ResolveRift(player);
                else if (Main.rand.NextBool(4))
                {
                    // Ambient spark drifting off the rift every few ticks, so it reads as an active,
                    // unstable tear instead of a static glow circle.
                    Vector2 vel = Main.rand.NextVector2Circular(1.6f, 1.6f);
                    LuminanceUtilities.SpawnParticle(phase3RiftWorldPos + Main.rand.NextVector2Circular(20f, 20f), vel, ArchetypeGlowColor(3), 30, 1f, ParticleType.Spark);
                }
            }
        }

        // ---------------- SUB-STAGE 2: RESOLVE ----------------
        private void TickPhase3Resolve(Player player)
        {
            phase3StageTimer++;
            float t = MathHelper.Clamp(phase3StageTimer / (float)Phase3ResolveTicks, 0f, 1f);

            // FIX ("paternya cuman ngeswing, bukan mengecilkan pedang" - bug "sword mengecil terus
            // ilang" yang SERING kejadian): akar masalahnya di sini. Lerp lama nge-interpolasi dari
            // prop.TargetScale (bukan dari prop.Scale AKTUAL saat ini) turun ke 0 - jadi begitu Resolve
            // mulai SEMENTARA blade Melee lagi disembunyikan (Scale 0, kondisi paling sering karena
            // Telegraph state-nya sendiri 5 detik/300 tick dari total 348 tick per siklus, jauh lebih
            // lama dari Sweep yang cuma 18 tick), blade langsung KETIBAN LONCAT ke full TargetScale di
            // tick pertama Resolve (Lerp(TargetScale, 0, t=0) = TargetScale), BARU abis itu keliatan
            // "mengecil" turun ke 0 selama 40 tick berikutnya - persis bug yang dilaporkan, kejadian
            // TIAP wave berakhir (bukan kadang-kadang).
            //
            // Fix: blade Melee (prop.Orbits == true) dikecualikan total dari animasi shrink ini - tetap
            // dipatok 0 (sama kayak Telegraph/Cooldown/Grow-in, lihat TickMeleeActive & TickPhase3Grow),
            // NGGAK PERNAH loncat ke full size buat di-shrink lagi. Pattern-nya jadi murni "muncul ->
            // ngayun -> ilang instan", nggak ada animasi membesar/mengecil di luar sweep-nya sendiri.
            // Ranged/Magic TETAP pakai shrink animation ini seperti semula (mereka MEMANG didesain
            // "membesar" di awal wave & "mengecil" di akhir, beda dari Melee).
            foreach (var prop in phase3Props)
                prop.Scale = prop.Orbits ? 0f : MathHelper.Lerp(prop.TargetScale, 0f, t);

            if (phase3StageTimer < Phase3ResolveTicks) return;

            phase3Props.Clear();

            // Combat damage nggak berlaku sama sekali selama dontTakeDamage aktif - jadi "kerusakan"
            // dari berhasil ngelewatin satu wave dikasih manual di sini.
            int dmg = (int)(NPC.lifeMax * Phase3DamagePerWave);
            NPC.life = Math.Max(1, NPC.life - dmg);
            NPC.HitEffect(0, 0); // percikan/flash kosmetik doang, murni visual (boss tetap invincible)
            ScreenShakeSystem.StartShakeAtPoint(phase3ArenaCenter, 10f, 0.3f);
            Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCHit4, phase3ArenaCenter);
            Main.NewText("The mirror cracks a little more...", 190, 150, 240);

            if (NPC.life <= NPC.lifeMax * 0.02f || phase3WaveNumber >= Phase3MaxWaves)
            {
                FinishPhase3AndEnterDesperation(player);
            }
            else
            {
                phase3WaveNumber++;
                SetupPhase3Wave(player);
            }
        }

        private void FinishPhase3AndEnterDesperation(Player player)
        {
            Phase3ArenaActive = false;
            IsCutsceneActive = false;
            phase3Props.Clear();
            KillAllTrackedPhase3MageBolts();

            player.Center = phase3ReturnPlayerPos;
            player.velocity = Vector2.Zero;

            NPC.Center = phase3ReturnBossPos;
            NPC.life = 1;
            NPC.dontTakeDamage = true;
            NPC.damage = 0;
            NPC.velocity = Vector2.Zero;
            NPC.netUpdate = true;

            // Sama persis jalur yang dipakai CheckDead() kalau boss mati lewat combat normal (lihat
            // WhoAmI.cs) - dari titik ini HandleDesperationCutscene yang ambil alih sepenuhnya.
            aiState = STATE_DESPERATION_CUTSCENE;
            aiTimer = 0;

            Main.NewText("The reflection can't take any more of itself.", 190, 150, 240);
        }

        // ============================================================================================
        // SUMMON PATTERN: rift/celah - lihat penjelasan lengkap di komentar besar atas file ini.
        // ============================================================================================
        private void StartNextRift(Player player)
        {
            phase3RiftGrid = new Point(Main.rand.Next(-10, 11), Main.rand.Next(-10, 11));
            phase3RiftWorldPos = phase3ArenaCenter + new Vector2(phase3RiftGrid.X * Phase3GridUnit, -phase3RiftGrid.Y * Phase3GridUnit);
            phase3RiftTimer = Phase3RiftCycleDuration;

            Main.NewText($"A rift opens at ({phase3RiftGrid.X}, {phase3RiftGrid.Y}) - reach it in {Phase3RiftCycleDuration / 60} seconds!", ArchetypeGlowColor(3));
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, phase3RiftWorldPos);
        }

        private void ResolveRift(Player player)
        {
            // Ledakan celahnya SENDIRI - dipicu SETIAP KALI hitungan waktu habis, baik player berhasil
            // masuk MAUPUN nggak (beda dari SpawnRiftFailureVFX di bawah, yang KHUSUS buat feedback
            // "kena hukuman" pas gagal doang). Ini representasi visual "celahnya nutup paksa/collapse"
            // begitu 10 detiknya abis - request: "visual effect ledakan saat hitungan waktu habis".
            SpawnRiftExplosionVFX(phase3RiftWorldPos);

            // Shockwave dari BOSS (NPC.Center) - BUKAN dari celah atau posisi player. Sekarang JALAN DI
            // KEDUA KASUS (safe/nggak), sama kayak SpawnRiftExplosionVFX di atas - jadi shockwave-nya
            // tetap keliatan walaupun player berhasil masuk celah tepat waktu, bukan cuma pas kena damage.
            // Pakai ShockwaveSystem yang UDAH ADA di mod ini (Common/Systems/ShockwaveSystem.cs,
            // yang sama juga dipakai PlutoBomb) lewat TriggerBombShockwave() - itu jalur yang auto-maju
            // & auto-selesai sendiri (ambil slot dari pool, majuin progress/opacity tiap tick, terus
            // deactivate sendiri di PostUpdateEverything), jadi nggak perlu manggil UpdateProgress()/
            // Stop() manual dari sini. Tint pakai ArchetypeGlowColor(3) - magenta yang sama persis
            // kayak warna rift/Summon yang udah dipakai di teks & partikel celah di atas, biar satu
            // tema visual. maxRangeTiles di-0-kan (TANPA batas) karena default bawaan TriggerBombShockwave
            // (20 tile / 320px) itu dituning buat ledakan lokal PlutoBomb - kekecilan buat efek yang
            // harus kebaca di seluruh layar arena Phase 3 ini.
            ShockwaveSystem.TriggerBombShockwave(NPC.Center, tintColor: ArchetypeGlowColor(3), tintStrength: 0.45f, maxRangeTiles: 0f);

            bool safe = Vector2.Distance(player.Center, phase3RiftWorldPos) <= Phase3RiftSafeRadius;
            if (!safe)
            {
                // Phase3RiftFailureDamage (100) sekarang TRUE DAMAGE - armorPenetration digedein jauh
                // di atas defense player mentok manapun, jadi defense/damage reduction dari armor
                // NGGAK ngurangin damage ini sama sekali (beda dari sebelumnya yang kena defense normal).
                player.Hurt(PlayerDeathReason.ByCustomReason(player.name + " didn't reach the rift in time."), Phase3RiftFailureDamage, 0, dodgeable: false, armorPenetration: 9999f);
                SpawnRiftFailureVFX(player.Center);
            }
            else
            {
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item4, phase3RiftWorldPos);
            }

            phase3RiftCycle++;
            if (phase3RiftCycle >= 3)
            {
                phase3SubStage = 2;
                phase3StageTimer = 0;
            }
            else
            {
                StartNextRift(player);
            }
        }

        // Ledakan visual titik celah pas 10 detiknya abis - dust ring + spark burst + screenshake
        // ringan, dipakein tint magenta (ArchetypeGlowColor(3)) yang sama kayak warna rift/Summon di
        // tempat lain (teks StartNextRift, spark ambient TickSummonActive, dll) biar satu tema.
        // JALAN DI KEDUA KASUS (safe/nggak) - lihat komentar pemanggilnya di ResolveRift.
        private void SpawnRiftExplosionVFX(Vector2 pos)
        {
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, pos);
            ScreenShakeSystem.StartShakeAtPoint(pos, 8f, 0.3f); // lebih ringan dari SpawnRiftFailureVFX (12f/0.4f) - ini "ledakan netral", bukan hukuman

            Color riftColor = ArchetypeGlowColor(3);

            for (int i = 0; i < 50; i++)
            {
                Vector2 vel = Main.rand.NextVector2CircularEdge(7f, 7f) * Main.rand.NextFloat(0.4f, 1f);
                Dust d = Dust.NewDustPerfect(pos, DustID.PurpleTorch, vel, 0, riftColor, 1.9f);
                d.noGravity = true;
            }

            // Spark tambahan - konsisten sama gaya LuminanceUtilities.SpawnParticle yang dipakai di
            // seluruh file ini (ParticleType.Spark, lihat ambient spark rift di TickSummonActive).
            for (int i = 0; i < 3; i++)
            {
                Vector2 sparkVel = Main.rand.NextVector2Circular(6f, 6f);
                LuminanceUtilities.SpawnParticle(pos + Main.rand.NextVector2Circular(15f, 15f), sparkVel, riftColor, 35, 1.6f, ParticleType.Spark);
            }
        }

        private void SpawnRiftFailureVFX(Vector2 pos)
        {
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, pos);
            ScreenShakeSystem.StartShakeAtPoint(pos, 12f, 0.4f);
            for (int i = 0; i < 40; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(9f, 9f);
                Dust d = Dust.NewDustPerfect(pos, DustID.RedTorch, vel, 0, Color.OrangeRed, 1.8f);
                d.noGravity = true;
            }
        }

        // ============================================================================================
        // HELPERS
        // ============================================================================================

        // Ambil sampai `desiredCount` senjata UNIK milik class `dc` dari inventory player (sumbernya
        // sama kayak ScanAndSelectWeapon di WhoAmI_Helpers.cs: held item + slot 0-49, filter
        // IsWeaponItem/BannedWeapons yang sama). Kalau kurang dari desiredCount (gear player berubah
        // di tengah fight, atau emang belum punya cukup variasi), diisi duplikat/placeholder biar
        // pattern-nya tetap punya sesuatu buat ditampilkan, bukan kosong.
        private List<Item> CollectClassWeapons(Player player, DamageClass dc, int desiredCount)
        {
            var unique = new HashSet<int>();
            var found = new List<Item>();

            void Consider(Item item)
            {
                if (item == null || !IsWeaponItem(item) || BannedWeapons.Contains(item.type)) return;
                if (!item.CountsAsClass(dc)) return;
                if (!unique.Add(item.type)) return;
                Item copy = new Item();
                copy.SetDefaults(item.type);
                found.Add(copy);
            }

            if (player.selectedItem >= 0 && player.selectedItem < player.inventory.Length)
                Consider(player.inventory[player.selectedItem]);
            for (int i = 0; i < 50 && i < player.inventory.Length; i++)
                Consider(player.inventory[i]);

            // Kocok urutan biar wave berikutnya (kalau class-nya sama) nggak selalu nampilin senjata
            // yang persis sama duluan.
            for (int i = found.Count - 1; i > 0; i--)
            {
                int j = Main.rand.Next(i + 1);
                (found[i], found[j]) = (found[j], found[i]);
            }

            if (found.Count > desiredCount)
                found.RemoveRange(desiredCount, found.Count - desiredCount);

            // Kalau senjata class yang cocok kurang dari desiredCount (gear player berubah di tengah
            // fight, atau emang belum punya cukup variasi class itu), lengkapin sisanya pakai senjata
            // LAIN yang beneran ada di inventory player (class apapun) - "ngespawn sprite senjata acak
            // yg ada di inven player", bukan filler item vanilla generik kayak sebelumnya. Item
            // vanilla generik cuma dipakai sebagai jaring pengaman paling akhir kalau player literally
            // nggak bawa senjata sama sekali.
            if (found.Count < desiredCount)
            {
                var anyWeapon = CollectAnyWeapons(player, unique, desiredCount - found.Count);
                found.AddRange(anyWeapon);
            }

            if (found.Count == 0)
            {
                Item filler = new Item();
                filler.SetDefaults(FallbackWeaponForClass(dc));
                found.Add(filler);
            }
            while (found.Count < desiredCount)
            {
                Item dup = new Item();
                dup.SetDefaults(found[Main.rand.Next(found.Count)].type); // duplikat acak dari yang udah kekumpul, bukan selalu index 0
                found.Add(dup);
            }

            return found;
        }

        // Ambil sampai `desiredCount` senjata UNIK (class APAPUN) dari inventory player, mengecualikan
        // type yang udah ada di `exclude`. Dipakai CollectClassWeapons di atas sebagai pelengkap kalau
        // senjata class yang diminta kurang - biar prop yang muncul tetap senjata beneran milik player,
        // bukan item generik.
        private List<Item> CollectAnyWeapons(Player player, HashSet<int> exclude, int desiredCount)
        {
            var found = new List<Item>();
            if (desiredCount <= 0) return found;

            void Consider(Item item)
            {
                if (item == null || !IsWeaponItem(item) || BannedWeapons.Contains(item.type)) return;
                if (!exclude.Add(item.type)) return;
                Item copy = new Item();
                copy.SetDefaults(item.type);
                found.Add(copy);
            }

            if (player.selectedItem >= 0 && player.selectedItem < player.inventory.Length)
                Consider(player.inventory[player.selectedItem]);
            for (int i = 0; i < 50 && i < player.inventory.Length; i++)
                Consider(player.inventory[i]);

            for (int i = found.Count - 1; i > 0; i--)
            {
                int j = Main.rand.Next(i + 1);
                (found[i], found[j]) = (found[j], found[i]);
            }

            if (found.Count > desiredCount)
                found.RemoveRange(desiredCount, found.Count - desiredCount);

            return found;
        }

        private int FallbackWeaponForClass(DamageClass dc)
        {
            if (dc == DamageClass.Ranged) return ItemID.WoodenBow;
            if (dc == DamageClass.Magic) return ItemID.WandofSparking;
            return ItemID.CopperShortsword;
        }

        private void FireBoltAt(Vector2 origin, Vector2 targetPos, float speed, int damage, Color color)
        {
            Vector2 dir = targetPos - origin;
            if (dir == Vector2.Zero) dir = -Vector2.UnitY;
            dir.Normalize();

            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), origin, dir * speed, ModContent.ProjectileType<WhoAmIPhase3Bolt>(), damage, 0f, proxySlot);
            SetBoltColor(p, color);
        }

        // Nembak proyektil ASLI dari senjata ranged yang dipegang prop corner ini, diarahkan ke player
        // - beda dari FireBoltAt (WhoAmIPhase3Bolt generik) yang dipakai sebelumnya. Aturan pemilihan
        // proyektilnya (lihat ResolveRangedRealProjectileType):
        //   - senjata ammo peluru (bullet)      -> proyektil peluru kecepatan tinggi (high velocity bullet)
        //   - senjata ammo khusus (rocket/dart/flare/dst) -> proyektil ASLI ammo khusus itu
        //   - bow tanpa proyektil unik sendiri  -> Jester's Arrow
        //   - bow YANG PUNYA proyektil unik sendiri -> proyektil unik bow itu
        // Proyektil hasil ammo/bow yang bawaannya bisa kena gravity nggak akan keliatan jatuh biarpun
        // Projectile gak punya field noGravity bawaan (lihat catatan di dalam method) - kecepatannya
        // dikunci ulang tiap tick lewat phase3MageBoltLockedVelocity/MaintainPhase3MageBolts, jadi efek
        // gravity internal proyektilnya ketimpa balik sebelum sempat kelihatan ngedrop.
        private void FireRealRangedProjectileAt(Vector2 origin, Item weapon, Vector2 targetPos, float speed, int damage)
        {
            if (weapon == null)
            {
                // Jaring pengaman kalau prop somehow gak punya Item (harusnya gak pernah kejadian -
                // CollectClassWeapons selalu ngisi minimal 1 filler) - tetap nembak sesuatu daripada diam.
                FireBoltAt(origin, targetPos, speed, damage, ArchetypeGlowColor(1));
                return;
            }

            Vector2 dir = targetPos - origin;
            if (dir == Vector2.Zero) dir = -Vector2.UnitY;
            dir.Normalize();

            int projType = ResolveRangedRealProjectileType(weapon);
            Vector2 vel = dir * speed;
            Vector2 spawnPos = origin + dir * 20f;

            SpawnWeaponMuzzleFlash(weapon);
            PlayWeaponFireSound(weapon);

            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel, projType, damage, 0f, proxySlot);
            if (p < 0 || p >= Main.maxProjectiles) return;

            Projectile proj = Main.projectile[p];
            proj.hostile = true;
            proj.friendly = false;
            proj.tileCollide = false; // arena Cartesius kosong di langit - gak ada tile buat ditabrak
            // FIX (request: "limit projectile dari senjatanya juga kita hapus"): banyak proyektil
            // vanilla nyimpen penetrate/pierce count bawaan senjatanya sendiri (misal panah biasa
            // penetrate=1, mati begitu "nembus" 1 target) - itu bikin proyektil ini bisa ilang
            // duluan begitu sempet nyenggol/"nembus" boss (satu2nya NPC lain di arena) SEBELUM
            // sempat nyentuh batas arena, keliatan kayak "proyektilnya dilimit". Dipaksa infinite di
            // sini biar SATU-SATUNYA cara proyektil ini hilang cuma lewat cek batas arena di bawah
            // (atau timeLeft safety net) - persis kayak WhoAmIPhase3Bolt.SetDefaults.
            proj.penetrate = -1;
            if (proj.timeLeft <= 0 || proj.timeLeft > Phase3StandardActiveDuration + 120)
                proj.timeLeft = Phase3StandardActiveDuration + 120;

            // "Harusnya projectile dari senjatanya digedein": peluru/panah vanilla di scale default
            // (1x) itu sprite-nya cuma beberapa pixel - gampang banget ketelen sama tekstur lantai
            // arena yang rame (lihat screenshot bug report), jadi kerasa kayak "muncul di senjatanya
            // terus ilang" padahal proyektilnya beneran jalan ke arah player, cuma nyaris nggak
            // kelihatan. Digedein di sini sama kayak "projectile besar" versi Magic
            // (Phase3MageBoltScaleMultiplier), plus nambahin light biar lebih nyala di atas lantai
            // yang gelap/textured itu.
            proj.scale = Phase3RangedBoltScaleMultiplier;
            proj.light = 0.6f;

            // "Set no gravity biar ga jatuh": Projectile (beda dari NPC/Dust) nggak punya field
            // noGravity bawaan sama sekali - CS1061 kalau dipaksa (proj.noGravity gak exist). Gravity
            // di proyektil vanilla itu logic INTERNAL di dalam AI() masing2 proyektil (nambahin ke
            // velocity.Y pelan2 pake state ai[]-nya sendiri), bukan satu flag global yang bisa
            // dimatikan dari luar. Solusinya: kecepatan garis lurus dikunci ULANG tiap tick lewat
            // phase3MageBoltLockedVelocity/MaintainPhase3MageBolts di bawah - jadi walau AI internal
            // proyektil itu nambahin efek gravity ke velocity-nya sendiri, tiap tick abis itu langsung
            // ditimpa balik ke kecepatan lurus yang dikunci di sini SEBELUM posisi ke-render, jadi
            // efeknya "nggak pernah kelihatan jatuh" walau field noGravity beneran gak ada. Kunci
            // kecepatan garis lurus SEKARANG - MaintainPhase3MageBolts maksa balikin ke nilai ini tiap
            // tick biar AI vanilla proyektil asli (homing dart, arrow yang bisa nge-drop, dsb) nggak
            // pernah sempet ngebelokin arah tembakannya dari titik player yang ditarget tadi.
            phase3MageBoltLockedVelocity[p] = vel;
        }

        // Nentuin proyektil ASLI yang harus ditembak buat mewakili sebuah senjata ranged, berdasarkan
        // jenis ammo yang dipakai senjata itu (weapon.useAmmo) - BUKAN cuma weapon.shoot mentah2, karena
        // hampir semua senjata ranged berbasis-ammo (gun/launcher) nyimpen placeholder yang gak berarti
        // di situ (proyektil aslinya nentuinnya dari ammo yang di-load, bukan dari senjatanya sendiri -
        // sama catatan yang udah ada di FireAttackProjectileAimed soal PurificationPowder placeholder).
        private int ResolveRangedRealProjectileType(Item weapon)
        {
            // Bow (atau repeater sejenis) - ammo-nya AmmoID.Arrow. Kalau bow itu punya proyektil unik
            // bawaan sendiri (bukan placeholder panah kayu generik), pakai itu; kalau nggak, defaultnya
            // Jester's Arrow.
            if (weapon.useAmmo == AmmoID.Arrow)
            {
                int ownShoot = weapon.shoot > 0 ? weapon.shoot : ContentSamples.ItemsByType[weapon.type].shoot;
                bool hasUniqueArrow = ownShoot > 0 && ownShoot != ProjectileID.WoodenArrowFriendly && ownShoot != ProjectileID.None;
                return hasUniqueArrow ? ownShoot : ProjectileID.JestersArrow;
            }

            // Senjata ammo peluru (bullet) - S.D.M.G., Chain Gun, dst semuanya nyimpen placeholder di
            // weapon.shoot (proyektil aslinya dari ammo bullet yang di-load Player.PickAmmo(), yang
            // gak pernah jalan buat dummy owner ini) - kasih proyektil peluru kecepatan tinggi biar
            // keliatan beneran "senjata itu nembak", bukan diam.
            if (weapon.useAmmo == AmmoID.Bullet)
                return ProjectileID.BulletHighVelocity;

            // Senjata dengan ammo khusus lain (rocket/dart/flare/gel/nail/stake/snowball/dst) - ambil
            // proyektil ASLI dari item ammo representatif class itu sendiri (AmmoID.* di ModLoader itu
            // sebenernya ID item ammo representatifnya, bukan angka kategori polos - jadi
            // ContentSamples.ItemsByType[useAmmo].shoot ngasih proyektil yang beneran keluar dari ammo
            // itu di gameplay normal).
            if (weapon.useAmmo != AmmoID.None)
            {
                int ammoProj = ContentSamples.ItemsByType[weapon.useAmmo].shoot;
                if (ammoProj > 0) return ammoProj;
            }

            // Senjata "self-ammo" (nggak butuh ammo dari inventory - proyektilnya emang langsung dari
            // weapon.shoot sendiri, misal senjata2 unik/modded tanpa slot ammo).
            if (weapon.shoot > 0) return weapon.shoot;

            return ProjectileID.BulletHighVelocity; // fallback aman terakhir kalau semua di atas gak ketemu
        }

        // "Projectile asli dari senjata mage" ditembak ke SEGALA ARAH sekaligus (satu volley = 360
        // derajat dibagi rata `count` arah), digedein pakai Phase3MageBoltScaleMultiplier, dan
        // homing-nya dipaksa MATI TOTAL (lihat phase3MageBoltLockedVelocity/MaintainPhase3MageBolts -
        // dipanggil tiap tick dari HandlePhase3Arena buat maksa balik ke kecepatan garis lurus ini,
        // jaga2 kalau AI vanilla si proyektil asli punya homing/curve/gravity bawaan sendiri).
        //
        // Beda dari FireBoltRing lama (yang numpang texture custom WhoAmIPhase3Bolt/AuraGlow): di sini
        // proyektilnya BENERAN proyektil bawaan si senjata (activeWeapon.shoot / ContentSamples
        // fallback), sama kayak pattern normal WhoAmI_Helpers.FireAttackProjectileAimed - biar
        // keliatannya beneran "senjata itu nembak", bukan generic glow bulet doang.
        private void FireRealWeaponBoltsInAllDirections(Vector2 origin, Item weapon, int count, float speed)
        {
            if (weapon == null || count <= 0) return;

            int projType = weapon.shoot > 0 ? weapon.shoot : ContentSamples.ItemsByType[weapon.type].shoot;
            if (projType <= 0 || projType == ProjectileID.PurificationPowder)
                projType = ProjectileID.WaterBolt; // fallback aman kalau senjata magic-nya somehow gak punya proyektil sendiri yang jelas (placeholder ammo-based dsb - sama filosofi fallback di FireAttackProjectileAimed, WhoAmI_Helpers.cs)

            int dmg = CalculateScaledDamage(weapon);

            for (int i = 0; i < count; i++)
            {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 dir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
                Vector2 vel = dir * speed;
                Vector2 spawnPos = origin + dir * 30f; // sedikit offset ke arah masing2 biar nggak numpuk persis di satu titik pixel yang sama

                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel, projType, dmg, 0f, proxySlot);
                if (p < 0 || p >= Main.maxProjectiles) continue;

                Projectile proj = Main.projectile[p];
                proj.hostile = true;
                proj.friendly = false;
                proj.tileCollide = false; // arena Cartesius kosong di langit, gak ada tile buat ditabrak - matiin biar nggak nyangkut/mantul aneh
                proj.penetrate = -1; // hapus limit pierce/hit bawaan senjatanya - lihat catatan di FireRealRangedProjectileAt
                proj.scale = Phase3MageBoltScaleMultiplier; // "projectile besar"
                if (proj.timeLeft <= 0 || proj.timeLeft > Phase3StandardActiveDuration + 120)
                    proj.timeLeft = Phase3StandardActiveDuration + 120; // safety net - biasanya udah kena Kill() duluan lewat MaintainPhase3MageBolts pas keluar arena

                // Kunci kecepatan garis lurus SEKARANG - MaintainPhase3MageBolts maksa balikin ke nilai
                // ini tiap tick biar AI vanilla si proyektil asli (yang bisa aja homing/melengkung/kena
                // gravity bawaan) nggak pernah sempet ngebelokin arahnya. "Homing dimatikan total."
                phase3MageBoltLockedVelocity[p] = vel;
            }

            var fireSound = weapon.UseSound ?? SoundID.Item43;
            Terraria.Audio.SoundEngine.PlaySound(fireSound.WithPitchOffset(Main.rand.NextFloat(-0.1f, 0.1f)), origin);
        }

        // Varian SATU tembakan dari FireRealWeaponBoltsInAllDirections di atas - dipakai buat pola
        // spiral bullet hell Mage yang baru (TickMagicActive), di mana sudutnya bukan dibagi rata 360
        // derajat sekali volley, tapi ngikutin sudut putar prop yang terus berubah tiap tick (`angle`
        // dari pemanggil). Tetap dilacak lewat phase3MageBoltLockedVelocity/MaintainPhase3MageBolts
        // yang sama biar homing-nya tetap mati total & ke-Kill() begitu keluar arena.
        private void FireRealWeaponBoltSpiral(Vector2 origin, Item weapon, float angle, float speed)
        {
            if (weapon == null) return;

            int projType = weapon.shoot > 0 ? weapon.shoot : ContentSamples.ItemsByType[weapon.type].shoot;
            if (projType <= 0 || projType == ProjectileID.PurificationPowder)
                projType = ProjectileID.WaterBolt; // fallback sama kayak FireRealWeaponBoltsInAllDirections

            int dmg = CalculateScaledDamage(weapon);
            Vector2 dir = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle));
            Vector2 vel = dir * speed;
            Vector2 spawnPos = origin + dir * 24f;

            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel, projType, dmg, 0f, proxySlot);
            if (p < 0 || p >= Main.maxProjectiles) return;

            Projectile proj = Main.projectile[p];
            proj.hostile = true;
            proj.friendly = false;
            proj.tileCollide = false;
            proj.penetrate = -1; // hapus limit pierce/hit bawaan senjatanya - lihat catatan di FireRealRangedProjectileAt
            proj.scale = Phase3MageBoltScaleMultiplier;
            if (proj.timeLeft <= 0 || proj.timeLeft > Phase3StandardActiveDuration + 120)
                proj.timeLeft = Phase3StandardActiveDuration + 120;

            phase3MageBoltLockedVelocity[p] = vel; // "homing dimatikan total", sama kayak semua proyektil Phase 3 lainnya
        }

        // Dipanggil TIAP TICK selama arena aktif (lihat HandlePhase3Arena). Sama persis pola-nya kayak
        // CleanupReturningYoyoBoomerangs (WhoAmI_Helpers.cs): buang entry yang udah nggak aktif, dan
        // buat yang masih aktif - paksa balik ke kecepatan garis lurus yang dikunci pas spawn (matiin
        // homing/AI vanilla apapun yang coba ngebelokin), lalu Kill() begitu keluar batas arena
        // ("hilang jika keluar dari arena").
        private void MaintainPhase3MageBolts()
        {
            if (phase3MageBoltLockedVelocity.Count == 0) return;

            List<int> toRemove = null;
            foreach (var kvp in phase3MageBoltLockedVelocity)
            {
                int idx = kvp.Key;
                if (idx < 0 || idx >= Main.maxProjectiles)
                {
                    if (toRemove == null) toRemove = new List<int>();
                    toRemove.Add(idx);
                    continue;
                }

                Projectile proj = Main.projectile[idx];
                if (!proj.active || proj.owner != proxySlot)
                {
                    if (toRemove == null) toRemove = new List<int>();
                    toRemove.Add(idx);
                    continue;
                }

                proj.velocity = kvp.Value; // homing dimatikan total - dipaksa lurus tiap tick

                if (!Phase3ArenaActive || IsOutsidePhase3Arena(proj.Center, phase3ArenaCenter, Phase3ArenaHalfExtent, 60f))
                {
                    proj.Kill();
                    if (toRemove == null) toRemove = new List<int>();
                    toRemove.Add(idx);
                }
            }

            if (toRemove != null)
                foreach (int idx in toRemove) phase3MageBoltLockedVelocity.Remove(idx);
        }

        // Dipanggil pas wave/class ganti atau Phase 3 selesai sama sekali - jangan biarin bolt volley
        // sebelumnya nyangkut nembus ke wave/state berikutnya.
        private void KillAllTrackedPhase3MageBolts()
        {
            foreach (var kvp in phase3MageBoltLockedVelocity)
            {
                int idx = kvp.Key;
                if (idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].active)
                    Main.projectile[idx].Kill();
            }
            phase3MageBoltLockedVelocity.Clear();
        }

        // Projectile (beda dari NPC) nggak punya property .Color bawaan - jadi tint-nya disimpan di
        // field custom BoltColor milik WhoAmIPhase3Bolt sendiri (ModProjectile instance-nya), diakses
        // lewat Main.projectile[p].ModProjectile.
        private static void SetBoltColor(int projectileIndex, Color color)
        {
            if (projectileIndex < 0 || projectileIndex >= Main.maxProjectiles) return;
            if (Main.projectile[projectileIndex].ModProjectile is WhoAmIPhase3Bolt bolt)
                bolt.BoltColor = color;
        }

        private static string Phase3WaveStartMessage(int classIndex)
        {
            switch (classIndex)
            {
                case 0: return "Three edges begin to turn...";
                case 1: return "The corners take aim...";
                case 2: return "The mirrors begin to fire...";
                default: return "A rift is about to open somewhere in the grid...";
            }
        }

        private static Color ArchetypeGlowColor(int classIndex)
        {
            switch (classIndex)
            {
                case 0: return new Color(255, 90, 70);   // Melee
                case 1: return new Color(110, 230, 130); // Ranged
                case 2: return new Color(170, 100, 255); // Magic
                case 3: return new Color(230, 110, 230); // Summon / rift
                default: return Color.White;
            }
        }

        // ============================================================================================
        // DRAW - dipanggil dari PreDraw() di WhoAmI.cs
        // ============================================================================================
        private void DrawPhase3Cartesian(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            if (aiState != STATE_PHASE3_TRANSITION && aiState != STATE_PHASE3_ARENA) return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            DynamicSpriteFont font = FontAssets.MouseText.Value;

            // Backdrop tipis biar grid-nya kebaca jelas di atas langit/void.
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black * 0.15f);

            Color gridColor = new Color(140, 110, 200) * 0.55f;
            Color axisColor = new Color(210, 180, 255) * 0.9f;

            for (int i = -10; i <= 10; i++)
            {
                bool isAxis = i == 0;
                Color c = isAxis ? axisColor : gridColor;
                float thickness = isAxis ? 3f : 1f;

                float worldX = phase3ArenaCenter.X + i * Phase3GridUnit;
                DrawWorldLineVertical(spriteBatch, pixel, worldX, phase3ArenaCenter.Y - Phase3ArenaHalfExtent, phase3ArenaCenter.Y + Phase3ArenaHalfExtent, screenPos, c, thickness);

                float worldY = phase3ArenaCenter.Y + i * Phase3GridUnit;
                DrawWorldLineHorizontal(spriteBatch, pixel, worldY, phase3ArenaCenter.X - Phase3ArenaHalfExtent, phase3ArenaCenter.X + Phase3ArenaHalfExtent, screenPos, c, thickness);
            }

            for (int i = -10; i <= 10; i++)
            {
                if (i == 0) continue;
                string label = i.ToString();
                Vector2 size = font.MeasureString(label);

                Vector2 xLabelPos = new Vector2(phase3ArenaCenter.X + i * Phase3GridUnit, phase3ArenaCenter.Y) - screenPos;
                spriteBatch.DrawString(font, label, xLabelPos + new Vector2(-size.X / 2f, 8f), axisColor);

                // -i di Y biar angka positif kegambar ke ATAS layar, sesuai konvensi diagram Cartesius
                // beneran (Y dunia Terraria positif itu ke BAWAH).
                Vector2 yLabelPos = new Vector2(phase3ArenaCenter.X, phase3ArenaCenter.Y - i * Phase3GridUnit) - screenPos;
                spriteBatch.DrawString(font, label, yLabelPos + new Vector2(8f, -size.Y / 2f), axisColor);
            }

            // Sama kayak gate di HandlePhase3Arena (TickPhase3ObstacleSwords) - obstacle sword cuma
            // muncul pas pattern Melee sekarang, bukan lepas dari class manapun.
            if (phase3ClassIndex == 0)
                DrawPhase3ObstacleSwords(spriteBatch, screenPos); // Broken Hero Sword di 4 batas arena - digambar SEBELUM weapon props biar props pattern (melee/ranged/magic) tetap kebaca di depan

            // REVISI (request "hapus semua patern spoilernya"): DrawPhase3MeleeSpoilerArc (lingkaran +
            // 2 panah merah + busur biru + label + countdown) DIHAPUS total sesuai permintaan - state
            // Telegraph sekarang nggak nggambar apa2 di layar sama sekali selama 5 detiknya (blade
            // sendiri masih Scale 0/tersembunyi di TickMeleeActive case 0, itu logic-nya nggak
            // diubah). Kalau nanti mau ditambahin lagi indikator lain yang lebih minimal, tinggal
            // panggil ulang di sini.

            foreach (var prop in phase3Props)
            {
                if (prop.Item == null && prop.CustomTexture == null) continue;
                if (prop.Scale <= 0.01f) continue;

                Vector2 drawPos = prop.WorldPosition - screenPos;
                Color glow = ArchetypeGlowColor(phase3ClassIndex) * 0.4f;

                if (prop.CustomTexture != null)
                {
                    // Melee sprite kustom sendiri - horizontal, gagang di ujung KIRI tekstur (origin.X
                    // kecil, deket 0) biar pas Scale naik bilahnya keliatan MEMANJANG keluar dari pusat
                    // arena (gagangnya yang nempel PERSIS di phase3ArenaCenter, lihat TickMeleeActive),
                    // origin.Y tetap tengah tinggi tekstur karena sprite-nya udah horizontal (bukan
                    // vertikal kayak icon item lama).
                    Texture2D customTex = prop.CustomTexture;
                    // MeleeBladeOriginXFraction dipakai bareng di TickMeleeActive/SetupMeleePattern -
                    // JANGAN diubah sendiri-sendiri di sini, biar hitbox selalu match sprite yang
                    // beneran kegambar (lihat catatan FIX di deklarasi konstantanya).
                    Vector2 customOrigin = new Vector2(customTex.Width * MeleeBladeOriginXFraction, customTex.Height / 2f);

                    // Trail/after-image effect: SELAMA state Sweep aja, iterasi MUNDUR lewat
                    // OldRotations (paling lama duluan) biar entri yang paling lama digambar PALING
                    // DULU (jadi ketiban sama entri yang lebih baru) - alpha makin gede (makin nggak
                    // transparan) makin baru datanya, ngasih kesan motion blur/bekas ayunan
                    // Color.LimeGreen di belakang blade. Sekarang Phase3MeleeSweepTrailMaxPoints (30)
                    // udah cukup gede buat nampung SATU sweep 18-tick penuh (lihat FIX di deklarasi
                    // konstantanya) jadi range age-nya beneran ke-manfaatin dari ujung baru sampai
                    // ekor tua - dilebarin ke 0.05..0.85 (dari 0.05..0.55 lama) biar "after image"-nya
                    // beneran kebaca jelas, bukan cuma bayangan samar.
                    if (phase3MeleeState == 1 && prop.OldRotations.Count > 0)
                    {
                        for (int h = 0; h < prop.OldRotations.Count; h++)
                        {
                            float age = (prop.OldRotations.Count - 1 - h) / (float)Math.Max(1, Phase3MeleeSweepTrailMaxPoints);
                            float alpha = MathHelper.Clamp(0.85f - age * 0.8f, 0.05f, 0.85f);
                            spriteBatch.Draw(customTex, drawPos, null, Color.LimeGreen * alpha, prop.OldRotations[h], customOrigin, prop.Scale, SpriteEffects.None, 0f);
                        }
                    }

                    // Glow transparan merah/oranye di belakang sprite utama - dipertahankan PERSIS
                    // seperti versi lama (scale * 1.15f), tetap tampil selama state Sweep (state 1).
                    spriteBatch.Draw(customTex, drawPos, null, glow, prop.Rotation, customOrigin, prop.Scale * 1.15f, SpriteEffects.None, 0f);
                    spriteBatch.Draw(customTex, drawPos, null, Color.White, prop.Rotation, customOrigin, prop.Scale, SpriteEffects.None, 0f);
                    continue;
                }

                Texture2D tex = TextureAssets.Item[prop.Item.type].Value;

                // Ranged/Magic (Orbits==false) - masih numpang icon item, origin di tengah tekstur
                // seperti biasa (nggak kena masalah "miring" karena nggak dirotasi radial kayak Melee).
                Vector2 origin = tex.Size() / 2f;

                spriteBatch.Draw(tex, drawPos, null, glow, prop.Rotation, origin, prop.Scale * 1.15f, SpriteEffects.None, 0f);
                spriteBatch.Draw(tex, drawPos, null, Color.White, prop.Rotation, origin, prop.Scale, SpriteEffects.None, 0f);
            }

            // Marker "celah aman": Terra Blade kecil terpisah dari phase3Props[0] (blade sweep utama) -
            // SENGAJA nggak numpang loop foreach di atas, karena butuh anchor/rotasi yang beda TOTAL
            // (pivot di tengah tekstur, muter di porosnya sendiri, posisinya sendiri di titik tengah
            // celah aman - bukan hilt-anchored radial dari pusat arena kayak blade sweep). Cuma tampil
            // sepanjang wave Melee aktif (SubStage 1, sama kayak blade sweep utama sendiri jalan),
            // hilang otomatis pas Grow-in/Resolve (transisi masuk/keluar wave).
            if (phase3ClassIndex == 0 && phase3SubStage == 1)
            {
                Texture2D safeGapTex = _swordTexTerraBlade ??= Mod.Assets.Request<Texture2D>(SwordAssetFolder + "Terra_Blade_Horizontal", AssetRequestMode.ImmediateLoad).Value;
                if (safeGapTex != null)
                {
                    // Celah aman = 90 derajat yang TIDAK dilewati sweep, terhitung dari phase3MeleeEndAngle
                    // sampai phase3MeleeStartAngle + 2pi (lihat ResetMeleeSweep) - titik tengahnya persis
                    // 45 derajat (seperempat dari 90 derajat) maju dari EndAngle.
                    float midAngle = phase3MeleeEndAngle + MathHelper.PiOver4;
                    Vector2 markerWorldPos = phase3ArenaCenter + new Vector2((float)Math.Cos(midAngle), (float)Math.Sin(midAngle)) * Phase3SafeGapBladeRadius;
                    Vector2 markerDrawPos = markerWorldPos - screenPos;

                    // Origin di TENGAH tekstur (BUKAN MeleeBladeOriginXFraction hilt-anchored kayak blade
                    // sweep utama) - ini yang bikin visualnya keliatan muter di porosnya sendiri kayak
                    // baling-baling, bukan ngayun radial.
                    Vector2 markerOrigin = safeGapTex.Size() / 2f;

                    // After-image: gambar history sudut spin (phase3SafeGapBladeSpinTrail) PALING LAMA
                    // duluan (biar ketiban sama yang lebih baru), alpha makin gede makin baru datanya -
                    // pola yang sama persis kayak trail OldRotations blade sweep utama, cuma sumber
                    // history-nya sendiri (marker muter terus tiap tick, bukan cuma pas state Sweep).
                    // Posisi (markerDrawPos/markerOrigin) tetap SAMA buat semua frame trail - yang beda
                    // cuma sudut rotasinya, jadi keliatan "baling-baling ngeblur" di tempat, bukan
                    // ngerembet kemana-mana.
                    if (phase3SafeGapBladeSpinTrail.Count > 0)
                    {
                        for (int h = 0; h < phase3SafeGapBladeSpinTrail.Count; h++)
                        {
                            float age = (phase3SafeGapBladeSpinTrail.Count - 1 - h) / (float)Math.Max(1, Phase3SafeGapBladeTrailMaxPoints);
                            float trailAlpha = MathHelper.Clamp(0.55f - age * 0.5f, 0.03f, 0.55f);
                            spriteBatch.Draw(safeGapTex, markerDrawPos, null, new Color(150, 255, 190) * trailAlpha, phase3SafeGapBladeSpinTrail[h], markerOrigin, Phase3SafeGapBladeScale, SpriteEffects.None, 0f);
                        }
                    }

                    // Tint hijau lembut - beda dari warna sweep utama (putih/LimeGreen glow merah-oranye)
                    // biar kebaca sebagai "penanda aman", bukan bagian dari ancaman/hitbox.
                    Color markerGlow = new Color(150, 255, 190) * 0.5f;
                    spriteBatch.Draw(safeGapTex, markerDrawPos, null, markerGlow, phase3SafeGapBladeSpin, markerOrigin, Phase3SafeGapBladeScale * 1.2f, SpriteEffects.None, 0f);
                    spriteBatch.Draw(safeGapTex, markerDrawPos, null, Color.White * 0.85f, phase3SafeGapBladeSpin, markerOrigin, Phase3SafeGapBladeScale, SpriteEffects.None, 0f);
                }
            }

            if (phase3ClassIndex == 3 && phase3SubStage == 1 && phase3RiftTimer > 0)
            {
                // FIX ("visual celahnya malah panjang kebawah, kelewat polos"): versi lama nge-scale
                // TextureAssets.MagicPixel (1x1) pakai SATU float scale lewat spriteBatch.Draw yang
                // origin-nya Vector2(0.5f) - itu origin dalam PIXEL LOKAL tekstur (bukan 0..1 relatif),
                // jadi buat tekstur 1x1 origin-nya ketiban ke ujung, bukan ke tengah, dan begitu
                // scale-nya gede (70-220px) hasilnya jadi blok miring/kepotong ke satu arah (kebawah)
                // alih-alih lingkaran rapi di tengah rift. Sekarang numpang tekstur AuraGlow yang
                // beneran bundar (udah ke-load lewat WhoAmIPhase3Bolt, texture-nya sama) dan origin
                // dihitung dari tex.Size()/2 yang bener-bener di tengah tekstur - circle pulse-nya
                // sekarang selalu bundar & center-nya presisi di titik rift, nggak peduli scale-nya.
                Vector2 riftScreen = phase3RiftWorldPos - screenPos;
                float pulse = 0.7f + 0.3f * (float)Math.Sin(Main.GameUpdateCount * 0.15f);
                float ringPulse = 1.4f + pulse * 0.3f;
                Color riftColor = ArchetypeGlowColor(3);

                Texture2D glowTex = TextureAssets.Projectile[ModContent.ProjectileType<WhoAmIPhase3Bolt>()].Value;
                Vector2 glowOrigin = glowTex.Size() / 2f;

                float outerDiameter = 190f * ringPulse;
                spriteBatch.Draw(glowTex, riftScreen, null, riftColor * 0.30f, 0f, glowOrigin, outerDiameter / Math.Max(1f, glowTex.Width), SpriteEffects.None, 0f);

                float innerDiameter = 95f * ringPulse;
                spriteBatch.Draw(glowTex, riftScreen, null, riftColor * 0.65f, 0f, glowOrigin, innerDiameter / Math.Max(1f, glowTex.Width), SpriteEffects.None, 0f);

                float coreDiameter = 30f * ringPulse;
                spriteBatch.Draw(glowTex, riftScreen, null, Color.White * 0.85f, 0f, glowOrigin, coreDiameter / Math.Max(1f, glowTex.Width), SpriteEffects.None, 0f);

                int secondsLeft = phase3RiftTimer / 60 + 1;
                string countdown = secondsLeft.ToString();
                Vector2 cdSize = font.MeasureString(countdown);
                spriteBatch.DrawString(font, countdown, riftScreen - cdSize / 2f, Color.White);
            }
        }

        // REVISI (request "hapus semua patern spoilernya"): DrawPhase3MeleeSpoilerArc dan seluruh
        // helper-nya (AngleToOffset/DrawWorldCircleOutline/DrawWorldLineSegment/DrawWorldArrow) DIHAPUS
        // total. State Telegraph (TickMeleeActive case 0, 5 detik) sekarang murni diam - blade
        // disembunyikan (Scale 0) tanpa indikator visual apapun di layar sampai Sweep beneran mulai.
        private static void DrawWorldLineVertical(SpriteBatch sb, Texture2D pixel, float worldX, float worldY1, float worldY2, Vector2 screenPos, Color color, float thickness)
        {
            float x = worldX - screenPos.X;
            float y1 = worldY1 - screenPos.Y;
            float y2 = worldY2 - screenPos.Y;
            Rectangle rect = new Rectangle((int)(x - thickness / 2f), (int)Math.Min(y1, y2), (int)Math.Max(1f, thickness), (int)Math.Abs(y2 - y1));
            sb.Draw(pixel, rect, color);
        }

        private static void DrawWorldLineHorizontal(SpriteBatch sb, Texture2D pixel, float worldY, float worldX1, float worldX2, Vector2 screenPos, Color color, float thickness)
        {
            float y = worldY - screenPos.Y;
            float x1 = worldX1 - screenPos.X;
            float x2 = worldX2 - screenPos.X;
            Rectangle rect = new Rectangle((int)Math.Min(x1, x2), (int)(y - thickness / 2f), (int)Math.Abs(x2 - x1), (int)Math.Max(1f, thickness));
            sb.Draw(pixel, rect, color);
        }
    }

    // ================================================================================================
    // Data plain-object buat satu "prop" senjata raksasa di arena (Melee/Ranged/Magic) - BUKAN entity
    // Terraria (nggak ada NPC/Projectile beneran per prop), murni state yang di-update & digambar
    // manual dari WhoAmI (lihat TickMeleeActive/TickRangedActive/TickMagicActive & DrawPhase3Cartesian
    // di atas). Alasan nggak dijadiin Projectile: propnya nggak butuh acuh sama tileCollide/pierce/dsb
    // sistem projectile, dan kita perlu kontrol penuh atas Scale/OrbitAngle per tick tanpa gangguan AI
    // vanilla apapun.
    // ================================================================================================
    internal class WhoAmIPhase3WeaponProp
    {
        public Item Item;
        public Vector2 WorldPosition;
        public Vector2 BaseOffset;   // dipakai prop yang diem di tempat (Ranged/Magic)
        public float Scale;
        public float TargetScale;
        public float Rotation;
        public float SelfSpinSpeed;  // dipakai prop yang orbit (Melee) - putaran di porosnya sendiri
        public float OrbitAngle;     // dipakai prop yang orbit (Melee)
        public float OrbitRadius;
        public float OrbitSpeed;
        public bool Orbits;
        public int FireTimer;
        public int HitCooldown;
        public Texture2D CustomTexture; // sprite bikinan sendiri (dipakai Melee - lihat SetupMeleePattern/DrawPhase3Cartesian); null berarti masih pakai TextureAssets.Item lama (Ranged/Magic)

        // Histori rotation tiap tick SELAMA state Sweep (lihat TickMeleeActive state 1) - dipakai
        // DrawPhase3Cartesian buat gambar trail "bekas ayunan" blade (semakin lama semakin transparan).
        // Di-cap ke Phase3MeleeSweepTrailMaxPoints entri terbaru aja, jangan dibiarin numpuk tanpa batas.
        public List<float> OldRotations = new List<float>();
    }

    // ================================================================================================
    // Proyektil generik buat bolt turret Ranged (pojok arena Phase 3). Sengaja custom (bukan numpang
    // projectile ID vanilla kayak pattern2 lain di WhoAmI_Helpers.cs / WhoAmI_ModdedWeaponOverrides.cs)
    // karena butuh kontrol penuh: LURUS TOTAL (nggak ada homing sama sekali) dan otomatis Kill() begitu
    // keluar batas arena, bukan ngikut AI/lifetime bawaan proyektil vanilla manapun.
    //
    // CATATAN: Magic (Mage) di Phase 3 nggak numpang class ini lagi - sekarang nembak proyektil ASLI
    // dari senjata magic yang disummon (lihat FireRealWeaponBoltsInAllDirections di atas), biar
    // keliatannya beneran "senjata itu nembak". Homing-nya tetap dipaksa mati & tetap ke-Kill() begitu
    // keluar arena, cuma caranya beda: dikontrol manual tiap tick lewat phase3MageBoltLockedVelocity/
    // MaintainPhase3MageBolts, bukan lewat AI() class ini.
    //
    // Nggak butuh art baru - numpang texture AuraGlow yang udah ada & di-load di WhoAmI_VFX.cs (soft
    // glow blob polos), di-tint per pattern lewat field BoltColor sendiri (di-set di FireBoltAt lewat
    // SetBoltColor, WhoAmI_Phase3Cartesian.cs - Projectile nggak punya .Color bawaan).
    // ================================================================================================
    public class WhoAmIPhase3Bolt : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/AuraGlow";

        // Projectile nggak punya property .Color bawaan (beda dari NPC.color) - jadi tint per pattern
        // (hijau=Ranged, dst - lihat ArchetypeGlowColor) disimpan sendiri di sini, di-set dari luar
        // lewat SetBoltColor (WhoAmI.FireBoltAt) begitu proyektilnya baru dibuat.
        public Color BoltColor = Color.White;

        public override void SetDefaults()
        {
            Projectile.width = 28;
            Projectile.height = 28;
            Projectile.friendly = false;
            Projectile.hostile = true; // WhoAmIProjectileGuard juga maksa ini true karena owner == proxySlot - di-set eksplisit juga di sini biar jelas
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300; // safety net - biasanya udah Kill() duluan lewat cek batas arena di AI()
            Projectile.alpha = 40;
            Projectile.scale = 1f; // cosmetic size sekarang dihitung manual di PreDraw (lihat catatan di sana) - field ini nggak dipakai buat itu lagi
            Projectile.light = 0.4f;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();

            // Homing dimatikan total - velocity nggak pernah disentuh/di-retarget di sini, cuma jalan
            // lurus sesuai arah waktu di-NewProjectile. Satu-satunya alasan proyektil ini hilang
            // adalah timeLeft habis ATAU keluar dari batas arena (di bawah).
            if (WhoAmI.Phase3ArenaActive && WhoAmI.IsOutsidePhase3Arena(Projectile.Center, WhoAmI.Phase3ArenaCenterStatic, WhoAmI.Phase3ArenaHalfExtentStatic, 60f))
            {
                Projectile.Kill();
                return;
            }

            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.SilverCoin, -Projectile.velocity * 0.1f, 0, Projectile.GetAlpha(Color.White), 1.1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = tex.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color glowColor = Projectile.GetAlpha(BoltColor);

            // FIX (blob raksasa + lag): AuraGlow itu tekstur soft-blob yang didesain buat nutup aura
            // SELURUH badan boss (gede), bukan buat satu bolt kecil ini. Kemarin scale-nya numpang
            // Projectile.scale mentah2 (~1.4x) LANGSUNG ke tekstur itu - hasilnya satu bolt jadi
            // segede layar, dan begitu satu ring volley nembak banyak bolt sekaligus dari titik yang
            // sama, semuanya numpuk jadi blob raksasa yang keliatan di screenshot (overdraw parah -
            // itu penyebab lag-nya). Sekarang scale-nya dihitung manual dari target diameter TETAP di
            // layar dibagi lebar asli tekstur-nya, jadi bolt-nya selalu kecil & konsisten nggak peduli
            // resolusi PNG AuraGlow yang sebenernya. Draw-nya juga dipangkas jadi 1 layer aja (dari 2)
            // buat motong overdraw lebih jauh lagi.
            const float TargetOnScreenDiameter = 34f;
            float scale = TargetOnScreenDiameter / Math.Max(1f, tex.Width);

            Main.EntitySpriteDraw(tex, drawPos, null, glowColor, Projectile.rotation, origin, scale, SpriteEffects.None, 0);
            return false;
        }
    }

    // ================================================================================================
    // Containment (player nggak bisa keluar dari kotak arena) + blokir item teleport-keluar selama
    // Phase 3 aktif. Dicek lewat static WhoAmI.Phase3ArenaActive/Phase3ArenaCenterStatic/
    // Phase3ArenaHalfExtentStatic (WhoAmI_Phase3Cartesian.cs) biar nggak butuh referensi langsung ke
    // instance NPC boss-nya.
    // ================================================================================================
    public class WhoAmIPhase3ArenaPlayer : ModPlayer
    {
        // Daftar item vanilla yang bisa "kabur" dari suatu tempat - diblokir selama Phase 3 aktif.
        // Silakan ditambah kalau ada item modded serupa yang perlu ikut diblokir.
        private static readonly HashSet<int> BlockedTeleportItems = new HashSet<int>
        {
            ItemID.MagicMirror,
            ItemID.IceMirror,
            ItemID.CellPhone,
            ItemID.Shellphone,
            ItemID.RecallPotion,
            ItemID.TeleportationPotion,
            ItemID.WormholePotion,
            ItemID.RodofDiscord,
        };

        // FIX (batas arena ke atas/samping suka "ketutup" lebih cepat dari yang digambar diagram):
        // sebelumnya containment 100% ngandelin clamp LUNAK di bawah (rel.X/rel.Y), dengan asumsi
        // seluruh kotak arena itu kosong total (void). Tapi phase3ArenaCenter ditaruh di baris tile
        // TETAP (safeArenaY = 110*16, lihat TriggerPhase3Start) yang cuma dijamin "index valid"
        // (nggak crash lighting/dust dkk), BUKAN dijamin kosong dari tile solid asli - world manapun
        // bisa aja punya floating island/gunung/struktur lain nongkrong persis di ketinggian itu.
        // Karena collision tile normal Terraria nggak pernah dimatikan buat player di sini, begitu
        // player kena tile solid ASLI itu, dia berhenti/ketahan di situ - jauh sebelum nyampe garis
        // atas (+10) yang digambar grid Cartesius-nya. Hasilnya "dinding" yang beneran dialami player
        // jadi nggak sinkron sama diagram (kelihatan di screenshot: background-nya terrain asli, bukan
        // void kosong). Fix-nya: matiin tile collision total selama arena aktif, jadi satu-satunya
        // "dinding" yang berlaku ya clamp lunak di bawah - PERSIS ngikutin kotak -10..10 yang digambar,
        // nggak peduli ada tile asli apa di posisi itu.
        public override void ResetEffects()
        {
            if (WhoAmI.Phase3ArenaActive)
            {
                Player.noFallDmg = true; // arena nggak ada "lantai" nyata - clamp di atas yang jadi batasnya, jangan sampai kena fall damage begitu keluar lagi
            }
        }

        // FIX (build error CS1061: 'Player' does not contain a definition for 'tileCollide'):
        // Projectile dan NPC punya field tileCollide bawaan buat matiin tile collision, tapi Player
        // TIDAK - Player nggak expose toggle collision sama sekali di tModLoader/vanilla, jadi baris
        // lama `Player.tileCollide = false;` di sini (dan di ResetEffects) nggak pernah valid, cuma
        // kebetulan lolos sampai sekarang karena belum sempet nge-build ulang.
        //
        // Solusinya bukan "matiin collision-nya" (nggak bisa), tapi "menang lawan collision-nya":
        // clamp posisi player balik ke kotak arena di DUA TITIK -
        //   1. PreUpdateMovement (di bawah)  - SEBELUM gerakan/collision vanilla jalan tick ini.
        //   2. PostUpdate (lihat di bawah)   - SESUDAH gerakan/collision vanilla selesai tick ini.
        // Kalau kebetulan ada tile solid asli nongkrong di ketinggian arena dan vanilla collision
        // sempat "nahan"/nge-block player pas Movement() jalan, PostUpdate langsung narik balik ke
        // kotak yang sama persis di akhir tick yang sama - player nggak akan pernah kelihatan
        // "nempel"/ketahan di tile asli itu barang 1 frame pun, walau collision-nya sendiri tetap
        // aktif normal di balik layar (cuma efeknya "dikalahkan" tiap tick).
        public override void PreUpdateMovement()
        {
            if (!WhoAmI.Phase3ArenaActive) return;
            ClampToArena();
        }

        public override void PostUpdate()
        {
            if (!WhoAmI.Phase3ArenaActive) return;
            ClampToArena();
        }

        // Logic clamp lunak yang sama dipakai di PreUpdateMovement DAN PostUpdate (lihat komentar
        // fix di atas) - satu sumber biar dua titik panggilnya nggak ketinggalan sinkron kalau nanti
        // batas arena-nya di-tweak lagi.
        private void ClampToArena()
        {
            Vector2 center = WhoAmI.Phase3ArenaCenterStatic;
            float half = WhoAmI.Phase3ArenaHalfExtentStatic;

            Vector2 rel = Player.Center - center;
            bool clamped = false;

            if (Math.Abs(rel.X) > half) { rel.X = MathHelper.Clamp(rel.X, -half, half); clamped = true; }
            if (Math.Abs(rel.Y) > half) { rel.Y = MathHelper.Clamp(rel.Y, -half, half); clamped = true; }

            if (clamped)
            {
                Player.Center = center + rel;
                Player.velocity *= 0.2f;
            }
        }

        public override bool CanUseItem(Item item)
        {
            if (WhoAmI.Phase3ArenaActive && BlockedTeleportItems.Contains(item.type))
            {
                if (Player.whoAmI == Main.myPlayer)
                    Main.NewText("Something holds you here - you can't leave yet.", 190, 150, 240);
                return false;
            }

            return true;
        }
    }

    // ================================================================================================
    // Fade hitam fullscreen buat transisi masuk/keluar arena (STATE_PHASE3_TRANSITION) - pola yang
    // sama persis kayak WhoAmIDefeatMenuSystem.PostDrawInterface (Whoamidefeatmenusystem.cs).
    // ================================================================================================
    public class WhoAmIPhase3TransitionOverlay : ModSystem
    {
        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (WhoAmI.Phase3FadeAlpha <= 0f) return;

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            spriteBatch.Draw(pixel, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black * WhoAmI.Phase3FadeAlpha);
        }
    }
}