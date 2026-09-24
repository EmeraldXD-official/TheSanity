using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // WhoAmI_VFX_PatternMarker.cs — NEW ADDITIVE LAYER, doesn't touch any existing file.
    // ================================================================================================
    // Tanda pattern marker yang nyala SEPANJANG boss lagi pakai attack pattern: muter cepet di awal,
    // melandai ke kecepatan jelajah yang GAK PERNAH sampai 0 (lihat header v2 di bawah), plus glow +
    // noise + partikel Luminance di sekitarnya.
    //
    // (v2-v9 history dipertahankan apa adanya di bawah - masih akurat buat sistem gerak/fire-stop/
    // satelit/noise/shader yang SEMUANYA tetap dipakai persis sama di v10. Yang berubah cuma CARA
    // gambar siluetnya sendiri - lihat blok v10 di bawah v9.)
    //
    // v3 (REVISI KUALITAS VISUAL - request: "masih burik, tint kurang, tambahin particle glow kecil
    // di sekitarnya"):
    //   1. TINT/GRADASI - sebelumnya cuma 1 warna flat (markColor) dipakai bolak-balik di semua
    //      layer, itu yang bikin kesan "burik"/datar. Sekarang ada gradasi CORE (terang, ke arah
    //      putih) -> MID (markColor asli) -> OUTER (gelap, ke arah hitam) kayak pola yang udah
    //      dipakai DrawBossAura di WhoAmI_VFX.cs - jadi silangnya kebaca punya "kedalaman" warna,
    //      bukan 1 hue rata dari tengah sampai ujung.
    //   2. CHROMATIC-SPLIT CORE - titik inti digambar ulang 2x kecil dengan tint merah/cyan murni
    //      yang di-offset arah rotasi (sama idiom kayak chromatic rim di DrawBossAura) - nambah
    //      kesan "energi tier atas" yang tajam, bukan cuma glow putih polos yang gampang keliatan
    //      pudar/burik pas numpuk banyak layer additive.
    //   3. NOISE DIRAPIHIN - versi lama nge-draw 1 noise blob BULAT nutupin seluruh area (nggak
    //      ngikutin bentuk lengan silangnya) - ini salah satu sumber kesan "burik"/kotor karena
    //      teksturnya nggak nyambung sama bentuk "+"-nya. Sekarang noise digambar 2x PERSIS
    //      ngikutin orientasi & proporsi tiap lengan (bar-shaped, bukan blob bulat), jadi grain-nya
    //      "napel" ke bentuk silang, bukan ngambang independen di atasnya.
    //   4. SATELIT MINI "+" - request "particle glow kecil-kecil di sekitarnya": sekarang ada 5
    //      silang MINI yang orbit di sekitar silang utama, arah putarnya BERLAWANAN (kontras
    //      visual), tiap satelit twinkle (kedip halus) dengan fase beda-beda biar nggak "napas"
    //      bareng-bareng - ini yang bikin areanya kerasa "ramai energi kecil" tapi tetap terbaca,
    //      bukan cuma 1 objek besar sendirian.
    //   5. Spark Luminance yang udah ada TETAP jalan (numpang di ujung lengan) sebagai lapisan
    //      partikel BENERAN yang punya fisika/lifetime sendiri, di atas satelit sprite yang
    //      persistent - dua-duanya saling melengkapi (satelit = "glow yang jelas bentuknya",
    //      spark = "percikan yang menghilang").
    //
    // v4 (request: "glow particle yg muter itu ganti jadi visual senjata dari inventory player,
    //     cuman visual doang, scale kecil"):
    //   Bentuk 5 satelit yang orbit berlawanan arah dari silang utama sekarang sprite senjata yang
    //   lagi dipegang boss (activeWeapon, hasil scan dari inventory player - lihat
    //   ScanAndSelectWeapon di WhoAmI_Helpers.cs) alih-alih silang glow polos. Ukurannya
    //   di-normalize ke target dunia kecil (satelliteTargetSize) supaya konsisten kecil apa pun
    //   senjata yang lagi di-roll. "Cuman visual doang" - jadi TIDAK ada lagi self-rotation
    //   (satSpin) atau halo glow nempel di belakangnya kayak versi sebelumnya; cuma posisi orbit +
    //   fade in/out (twinkle) yang jalan, spritenya tampil polos. Fallback ke glow texture kalau
    //   activeWeapon null/kosong.
    //
    // v5 (request: "tambahin noise, biar makin bagus" + "pakai file .fx"):
    //   1. Noise sprite-layer (yang tadinya cuma 1 layer flat di tiap lengan) sekarang jadi
    //      2-OCTAVE (coarse + fine) yang drift pelan independen dari rotasi utama, plus flicker
    //      alpha halus, dan nambah 1 ring noise tipis di halo belakang. Masih numpang
    //      auraNoiseTexture yang sama - GAK butuh aset baru buat bagian ini.
    //   2. TAMBAHAN shader beneran: WhoAmIPatternMarkerNoise.fx - pixel shader per-pixel yang
    //      generate turbulent noise (FBM, murni matematika, gak butuh texture noise) buat
    //      modulasi alpha/brightness lengan + gradasi core/mid/outer, digambar sebagai pass
    //      TAMBAHAN (Begin/End SpriteBatch sendiri) tepat setelah EndAdditive. Loading-nya pakai
    //      ModContent.Request<Effect> standar tModLoader (BUKAN numpang loader Luminance yang
    //      dipakai WhoAmIMirrorLanceBeam.fx, karena file itu belum tersedia buat dicontek) - path
    //      asset di EnsurePatternMarkerShaderLoaded() masih PLACEHOLDER, sesuaikan ke lokasi asli
    //      file .fx di project. Kalau shader gagal load, pass ini otomatis di-skip (null-check),
    //      jadi gak bikin crash - silangnya tetap tampil normal kayak v4.
    //
    // v6 (request: "setiap patern berbeda kita buat patern marker yg berbeda, dgn bentuk bentuk yg
    //     unik" + "di yg tanda + marker nya itu ada garis + nya biar makin bagus"):
    //   Sebelumnya SEMUA state attack pakai bentuk yang SAMA PERSIS - sekarang tiap pattern dapet
    //   SILUET sendiri via MarkerShape + GetMarkerShapeForState.
    //
    // v9 (request: "gerakannya semua pattern tetap sama persis, mau digerakan beda juga, misal
    //     Pendulum ayun bukan puter penuh"): MarkerMotion motong "gerak" jadi konsep terpisah dari
    //     "bentuk" (MarkerShape) - tiap motion punya rumus sendiri buat patternMarkerRotation, SEMUA
    //     tetap hormat ke stopFactor yang sama, jadi rhythm "charge -> release" tetap konsisten
    //     lintas semua motion.
    //
    // ================================================================================================
    // v10 (request: "ganti vfx pattern marker dari code jadi gambar" - gambar 31 texture
    //      mirror/kaca/mimic-tema udah digenerate terpisah, PNG putih+alpha/tintable, taruh di
    //      Content/.../VFX/PatternMarkers/{nama}.png):
    //   BuildMarkerArms/DrawMarkerArm/MarkerArm/BuildEvenSpokes (mesin "gambar N lengan bar/ray dari
    //   auraGlowTexture yang di-stretch") DIBUANG TOTAL. Setiap MarkerShape sekarang cuma nunjuk ke
    //   SATU texture pra-render (GetMarkerTexture) yang digambar sebagai 1 glyph utuh, bukan disusun
    //   dari puluhan quad kecil lagi.
    //
    //   YANG TETAP SAMA PERSIS (murni "ganti kulit", bukan re-design sistemnya):
    //     - MarkerShape enum & GetMarkerShapeForState - masih key yang sama persis, cuma sekarang
    //       dipetakan ke nama file lewat GetMarkerTextureFile alih-alih ke array MarkerArm.
    //     - MarkerMotion enum & GetMarkerMotionForShape - grammar gerak per-shape TIDAK diubah.
    //     - Fire-stop/fire-flash (SignalPatternMarkerFire, stopFactor) - identik.
    //     - Gradasi core/mid/outer, chromatic-split core, satelit senjata, 2-octave noise, ring
    //       noise, shader .fx pass, spark Luminance, ambient dust - semua konsepnya dipertahankan,
    //       cuma titik acuannya (yang tadinya "ujung lengan ke-i") diganti jadi titik-titik di tepi
    //       piringan glyph (lihat GetGlyphRingPoint) karena udah gak ada array arms[] individual lagi.
    //
    //   YANG DIADAPTASI (karena dulu 4 hal ini kerjanya "goyang tiap lengan SENDIRI-SENDIRI", dan
    //   sekarang cuma ada 1 glyph tunggal buat digoyang, bukan N lengan lepas):
    //     - armLengthPulse -> markerScalePulse: dulu ngali ke LengthMult tiap lengan, sekarang ngali
    //       ke SCALE keseluruhan glyph (efek "napas besar-kecil" tetap kebaca sama, cuma sekarang di
    //       seluruh bentuk sekaligus, bukan per-lengan).
    //     - swayAmplitude (Sway: MirrorStar/EchoTriad/BinaryOrbit/TetherLines - "reflections/echo
    //       yang gak gerak serempak") -> sekarang digambar sebagai 2 GHOST ECHO tambahan dari glyph
    //       yang SAMA, di-offset rotasi ±sway & alpha diturunin - malah lebih pas buat shape-shape
    //       ini karena "echo" jadi LITERAL (bukan cuma metafora lewat lengan yang goyang beda fase).
    //     - chainLagStrength (Chain: ChainCurve - "whip/cambukan") -> sekarang jadi TRAIL 2 ghost
    //       glyph yang keteteran di belakang rotasi utama, alpha makin pudar makin jauh - baca sebagai
    //       "jejak cambukan" dari glyph itu sendiri.
    //     - Carousel (Carousel shape - "kuda-kuda naik turun sendiri-sendiri") -> sekarang jadi 4
    //       MINI-GLYPH kecil yang orbit di ring kecil sekitar glyph utama, tiap satu bobbing scale di
    //       fase sendiri - "kuda carousel"-nya sekarang literal miniatur si glyph, bukan cuma spoke.
    //   Jitter (Shatter/Blink) sebenernya JUSTRU lebih simpel & lebih bersih sekarang - dulu tiap
    //   lengan jitter sendiri-sendiri, sekarang cukup 1 posisi/rotasi glyph yang di-jitter/di-pop,
    //   hasilnya malah lebih terbaca (gak ada N lengan numpuk jitter independen yang bisa kebaca
    //   berantakan).
    //
    //   INTEGRASI ASET: taruh ke-31 PNG di
    //     Content/TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/PatternMarkers/{nama}.png
    //   (samain persis sama MarkerTexturePath di bawah / nama file hasil generate). Semua PNG putih
    //   solid + alpha channel (TINTABLE) - warnanya baru muncul pas ditarik lewat markMid/markCore/
    //   markOuter di sini, PERSIS pola lama (jadi GetSecondWavePatternMarkerColor /
    //   GetAttackPatternColor tetap satu-satunya sumber warna, gak perlu re-export tiap kombinasi
    //   warna x shape).
    // ================================================================================================
    // v2: spin gak pernah berhenti (ngerem ke cruise speed, bukan ke 0), nyala sepanjang aiState
    // pattern-nya masih sama (bukan dibatasi window tick pendek).
    //
    // ─── INTEGRATION (di WhoAmI.cs PreDraw) ────────────────────────────────────────────────────────
    //   Panggil PERSIS SETELAH DrawAttackPatternVFX():
    //       DrawPatternMarkerVFX(spriteBatch, screenPos);
    //   (sudah diterapkan sebelumnya - file ini murni ganti ISI method-nya, bukan lokasi panggilnya)
    // ================================================================================================
    public partial class WhoAmI
    {
        private const float PatternMarkerMaxSpinSpeed = 0.95f;
        private const float PatternMarkerCruiseSpinSpeed = 0.18f;
        private const int PatternMarkerRampDuration = 60;
        private const int PatternMarkerSatelliteCount = 5;

        // v10: diameter target di layar (world px) per 1.0 sizeRef, di popIn=1/breathe=1/scalePulse=1.
        // Dulu ukuran akhir implisit dari armLength * auraGlowTexture.Width (glow blob kecil yang
        // di-stretch) - sekarang texture-nya sendiri udah full 512px penuh detail, jadi ukuran akhir
        // dikontrol LANGSUNG dalam world-px di sini, bukan lewat scale relatif ke texture lama.
        // TUNE nilai ini kalau hasilnya kerasa kegedean/kekecilan dibanding marker versi lama kamu.
        private const float PatternMarkerDiameterPerSizeRef = 320f;

        private float patternMarkerRotation = 0f;
        private int patternMarkerLocalTimer = 0;
        private int patternMarkerLastState = -1;

        // ================================================================================================
        // v8 (request: "marker harus berhenti muter pas nembak projectile, dan markernya harus lebih
        //     punya arti, bukan cuma garis muter"):
        //   SignalPatternMarkerFire() dipanggil PAS pattern beneran ngelepas hit (bukan telegraph,
        //   bukan projectile prop damage 0) - spin snap ke hampir-berhenti + core flash putih sesaat,
        //   baru lanjut lagi. AUTOMATIC COVERAGE: WhoAmIProjectileGuard.OnSpawn juga manggil ini buat
        //   semua projectile hostile-berdamage yang di-spawn lewat proxySlot, jadi pattern baru yang
        //   lupa manggil manual tetap otomatis kebagian efeknya. Continuous puppeted hazard yang gak
        //   punya 1 momen "release" tunggal (mis. Double Helix Sweep) sengaja dibiarin gak kepakai.
        // ================================================================================================
        private const int PatternMarkerFireStopDuration = 16; // how long the spin stays snapped near-0 after a shot
        private const int PatternMarkerFireFlashDuration = 10; // how long the bright "release" flash lasts
        private int patternMarkerFireStopTimer = 0;
        private int patternMarkerFireFlashTimer = 0;

        // Public: called from WhoAmIProjectileGuard.OnSpawn (a different class, WhoAmI_VFX_ProjectileShader.cs)
        // for the automatic-coverage case described above, in addition to the manual call sites.
        public void SignalPatternMarkerFire()
        {
            patternMarkerFireStopTimer = PatternMarkerFireStopDuration;
            patternMarkerFireFlashTimer = PatternMarkerFireFlashDuration;
        }

        // v6/v7: 1 shape unik per pattern (atau per grup pattern yang mekanisnya mirip - sama kayak
        // beberapa pattern udah share warna sebelum ini). v10: setiap entry di sini sekarang juga
        // langsung berkorespondensi ke 1 file PNG lewat GetMarkerTextureFile di bawah.
        private enum MarkerShape
        {
            Cross,        // default/base states (dash, melee combo, ranged barrage, parry, counter, dodge)
            Infinity,     // Magic Spiral Rift / Phantom Mirage Cascade - lemniscate, matches figure-8 orbit
            XBlade,       // Blink & Echo Combo - crossed slash blades + small hilt cross
            Triangle,     // Orbiting Grid Lock - matches the literal in-world triangle cage
            Spiral,       // Gravity Well Torrent / Singularity Overdrive / Double Helix Sweep - vortex pinwheel
            MirrorStar,   // Mirror Mirage - 6-point hexagram, reads as "3 reflections"
            Halo,         // Summon Rift Swarm / Yoyo Tether Storm / Aureola Signet Rain - ring of spokes
            Hex,          // Whip Lash Cage / Orbiting Blade Ring - 6-spoke wheel/cage
            CrossedRays,  // Boomerang Crossfire - two throws crossing from opposite flanks
            Starburst,    // Abyssal Cleave / Homing Cluster Comet - dramatic 8-point impact star
            Grid,         // Vector Laser Grid - dense 4-bar hash/grid
            TwinLance,    // Dimensional Pierce / Mirror Lance Rupture - twin parallel spears
            Shatter,      // Quantum Glitch Phasing - irregular jittered fracture shards

            // ---- v7 (second wave, +3 patterns per class) ----
            Crescent,     // Warped Mirror Waltz / Cascade Unravel - flowing curved sweep
            EchoTriad,    // Fractured Persona Onslaught - offset echoing duplicate blades
            Chevron,      // Riposte Cascade - escalating forward-pointing arrows
            Blink,        // Parallax Volley - scattered uneven teleport points
            Ricochet,     // Mirror Ricochet / Ricochet Triangle - bent bounce path
            Starfall,     // Starfall Convergence - rays converging from directly above
            Bloom,        // Fracture Bloom - soft many-petaled mandala
            BinaryOrbit,  // Umbral Duality / Binary Orbit Snare - two offset orbiting blobs
            PincerMirror, // Paradox Mirror Volley - two angled pairs converging from both sides
            ConstrictNet, // Wraith Convergence - inward-curling closing net
            TetherLines,  // Soul Tether Bind - uneven thin radiating leash-lines
            Carousel,     // Spectral Carousel - ring of alternating long/short turret spokes
            CoilSpiral,   // Serpent's Coil - tight densely-wound coil
            WideFan,      // Cracked Fan Lash - broad single-arc fan (not full ring)
            ChainCurve,   // Puppeteer's Snap - sequential lagging chain segments
            Pendulum,     // Pendulum Reckoning - single long swinging arm + counterweight
            Windmill,     // Windmill Barrage - heavy 4-blade pinwheel
            CurvingArc,   // Curving Return Barrage - paired out-and-back curved arcs
        }

        // v7: color source for the 21 second-wave states, self-contained in this file since
        // GetAttackPatternColor (WhoAmI_VFX_Attacks.cs) wasn't part of this upload/pass - see the
        // call site in DrawPatternMarkerVFX. Colors deliberately match the particle tint each new
        // Handle* method already uses for its own hit/travel VFX (WhoAmI_Pattern_*Extras2.cs), so
        // the telegraph and the payoff read as the same attack rather than two different ones.
        private Color? GetSecondWavePatternMarkerColor(int state)
        {
            switch (state)
            {
                case STATE_MELEE_MIRROR_WALTZ: return new Color(225, 235, 255);
                case STATE_MELEE_FRACTURED_ONSLAUGHT: return new Color(200, 30, 60);
                case STATE_MELEE_RIPOSTE_CASCADE: return new Color(255, 140, 40);

                case STATE_RANGED_PARALLAX_VOLLEY: return new Color(80, 255, 180);
                case STATE_RANGED_MIRROR_RICOCHET: return new Color(150, 255, 90);
                case STATE_RANGED_STARFALL_CONVERGENCE: return new Color(255, 225, 150);

                case STATE_MAGIC_FRACTURE_BLOOM: return new Color(220, 120, 255);
                case STATE_MAGIC_UMBRAL_DUALITY: return new Color(170, 120, 220);
                case STATE_MAGIC_PARADOX_MIRROR: return new Color(200, 90, 220);

                case STATE_SUMMON_WRAITH_CONVERGENCE: return new Color(170, 255, 190);
                case STATE_SUMMON_SOUL_TETHER: return new Color(150, 220, 255);
                case STATE_SUMMON_SPECTRAL_CAROUSEL: return new Color(255, 190, 90);

                case STATE_WHIP_SERPENTS_COIL: return new Color(120, 255, 90);
                case STATE_WHIP_FAN_LASH: return new Color(255, 110, 60);
                case STATE_WHIP_PUPPETEER_SNAP: return new Color(220, 90, 200);

                case STATE_YOYO_PENDULUM_RECKONING: return new Color(140, 170, 220);
                case STATE_YOYO_BINARY_SNARE: return new Color(170, 195, 235);
                case STATE_YOYO_CASCADE_UNRAVEL: return new Color(100, 220, 255);

                case STATE_BOOMERANG_WINDMILL_BARRAGE: return new Color(230, 180, 80);
                case STATE_BOOMERANG_RICOCHET_TRIANGLE: return new Color(230, 235, 255);
                case STATE_BOOMERANG_CURVING_RETURN: return new Color(140, 120, 255);

                default: return null;
            }
        }

        private MarkerShape GetMarkerShapeForState(int state)
        {
            switch (state)
            {
                case STATE_MAGIC_SPIRAL_RIFT: return MarkerShape.Infinity;
                case STATE_BLINK_ECHO_COMBO: return MarkerShape.XBlade;
                case STATE_ORBIT_GRID_LOCK: return MarkerShape.Triangle;
                case STATE_GRAVITY_WELL_TORRENT: return MarkerShape.Spiral;
                case STATE_MIRROR_MIRAGE:
                case STATE_MIRAGE_DECOY_HOLD: return MarkerShape.MirrorStar;
                case STATE_SUMMON_RIFT_SWARM: return MarkerShape.Halo;
                case STATE_WHIP_LASH_CAGE: return MarkerShape.Hex;
                case STATE_YOYO_TETHER_STORM: return MarkerShape.Halo;
                case STATE_BOOMERANG_CROSSFIRE: return MarkerShape.CrossedRays;
                case STATE_ABYSSAL_CLEAVE: return MarkerShape.Starburst;
                case STATE_ORBITING_BLADE_RING: return MarkerShape.Hex;
                case STATE_DIMENSIONAL_PIERCE: return MarkerShape.TwinLance;
                case STATE_VECTOR_LASER_GRID: return MarkerShape.Grid;
                case STATE_HOMING_CLUSTER_COMET: return MarkerShape.Starburst;
                case STATE_SINGULARITY_OVERDRIVE: return MarkerShape.Spiral;
                case STATE_AUREOLA_SIGNET_RAIN: return MarkerShape.Halo;
                case STATE_DOUBLE_HELIX_SWEEP: return MarkerShape.Spiral;
                case STATE_QUANTUM_GLITCH_PHASING: return MarkerShape.Shatter;
                case STATE_MIRROR_LANCE_RUPTURE: return MarkerShape.TwinLance;

                // ---- v7 second wave ----
                case STATE_MELEE_MIRROR_WALTZ: return MarkerShape.Crescent;
                case STATE_MELEE_FRACTURED_ONSLAUGHT: return MarkerShape.EchoTriad;
                case STATE_MELEE_RIPOSTE_CASCADE: return MarkerShape.Chevron;
                case STATE_RANGED_PARALLAX_VOLLEY: return MarkerShape.Blink;
                case STATE_RANGED_MIRROR_RICOCHET: return MarkerShape.Ricochet;
                case STATE_RANGED_STARFALL_CONVERGENCE: return MarkerShape.Starfall;
                case STATE_MAGIC_FRACTURE_BLOOM: return MarkerShape.Bloom;
                case STATE_MAGIC_UMBRAL_DUALITY: return MarkerShape.BinaryOrbit;
                case STATE_MAGIC_PARADOX_MIRROR: return MarkerShape.PincerMirror;
                case STATE_SUMMON_WRAITH_CONVERGENCE: return MarkerShape.ConstrictNet;
                case STATE_SUMMON_SOUL_TETHER: return MarkerShape.TetherLines;
                case STATE_SUMMON_SPECTRAL_CAROUSEL: return MarkerShape.Carousel;
                case STATE_WHIP_SERPENTS_COIL: return MarkerShape.CoilSpiral;
                case STATE_WHIP_FAN_LASH: return MarkerShape.WideFan;
                case STATE_WHIP_PUPPETEER_SNAP: return MarkerShape.ChainCurve;
                case STATE_YOYO_PENDULUM_RECKONING: return MarkerShape.Pendulum;
                case STATE_YOYO_BINARY_SNARE: return MarkerShape.BinaryOrbit;
                case STATE_YOYO_CASCADE_UNRAVEL: return MarkerShape.Crescent;
                case STATE_BOOMERANG_WINDMILL_BARRAGE: return MarkerShape.Windmill;
                case STATE_BOOMERANG_RICOCHET_TRIANGLE: return MarkerShape.Ricochet;
                case STATE_BOOMERANG_CURVING_RETURN: return MarkerShape.CurvingArc;
                default: return MarkerShape.Cross;
            }
        }

        // ================================================================================================
        // v9: MarkerMotion motong "gerak" jadi konsep terpisah dari "bentuk" (MarkerShape) - tiap
        // motion punya rumus sendiri buat patternMarkerRotation & modifier tambahan, dan SEMUANYA
        // tetap hormat ke stopFactor yang sama - rhythm "charge -> release" tetap konsisten lintas
        // semua motion. TIDAK diubah di v10 - masih exact enum & mapping yang sama.
        // ================================================================================================
        private enum MarkerMotion
        {
            SteadySpin, // gerak lama: puter terus, ramp cepat->cruise (dipertahankan buat shape netral)
            Pendulum,   // ayun bolak-balik di satu busur, snap taut di tiap ujung ayunan - GAK puter penuh
            Locked,     // diem di satu heading, sesekali SNAP ke heading baru (bukan geser mulus)
            Pulse,      // rotasi nyaris beku - identitas dibawa lewat breathing scale/alpha yang kuat
            Coil,       // puter pelan + siklus mengerucut/melebar (scale glyph memendek lalu lepas keluar)
            Sway,       // v10: 2 ghost echo dari glyph yang sama, goyang di fase masing-masing
            Chain,      // v10: ghost trail glyph yang keteteran di belakang rotasi utama
            Ricochet,   // rotasi loncat heading-ke-heading (snap tajam+tahan), bukan meluncur mulus
            Heavy,      // puter pelan tapi speednya sendiri "ngayun" (wobble) - kesan berat/lembam
            Surge,      // rotasi nyaris beku - glyph MENUSUK maju-mundur (thrust) via scale punch
            Jitter,     // goyang posisi/rotasi kecil terus-menerus (Shatter) ATAU loncat besar jarang
                        // (Blink, "teleport pop") - gak ada rotasi koheren sama sekali
            Carousel,   // v10: 4 mini-glyph orbit di ring kecil sekitar glyph utama, bobbing sendiri-sendiri
        }

        // Pemetaan shape -> motion. TIDAK diubah dari v9.
        private MarkerMotion GetMarkerMotionForShape(MarkerShape shape)
        {
            switch (shape)
            {
                case MarkerShape.Pendulum:
                case MarkerShape.Crescent:
                case MarkerShape.WideFan:
                    return MarkerMotion.Pendulum;

                case MarkerShape.Triangle:
                case MarkerShape.Grid:
                    return MarkerMotion.Locked;

                case MarkerShape.Halo:
                case MarkerShape.Starfall:
                case MarkerShape.Bloom:
                    return MarkerMotion.Pulse;

                case MarkerShape.Spiral:
                case MarkerShape.CoilSpiral:
                case MarkerShape.ConstrictNet:
                    return MarkerMotion.Coil;

                case MarkerShape.MirrorStar:
                case MarkerShape.EchoTriad:
                case MarkerShape.BinaryOrbit:
                case MarkerShape.TetherLines:
                    return MarkerMotion.Sway;

                case MarkerShape.ChainCurve:
                    return MarkerMotion.Chain;

                case MarkerShape.XBlade:
                case MarkerShape.CrossedRays:
                case MarkerShape.Ricochet:
                case MarkerShape.PincerMirror:
                case MarkerShape.CurvingArc:
                    return MarkerMotion.Ricochet;

                case MarkerShape.Hex:
                case MarkerShape.Windmill:
                    return MarkerMotion.Heavy;

                case MarkerShape.Starburst:
                case MarkerShape.TwinLance:
                case MarkerShape.Chevron:
                    return MarkerMotion.Surge;

                case MarkerShape.Shatter:
                case MarkerShape.Blink:
                    return MarkerMotion.Jitter;

                case MarkerShape.Carousel:
                    return MarkerMotion.Carousel;

                default: // Cross, Infinity - identitas netral/base, tetap puter biasa
                    return MarkerMotion.SteadySpin;
            }
        }

        // Back-out easing kecil (overshoot-lalu-settle) - dipakai motion Surge biar "tusukan" scale-nya
        // kerasa nyentak keluar dulu baru berhenti, bukan ease standar yang mulus.
        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            t = MathHelper.Clamp(t, 0f, 1f) - 1f;
            return 1f + c3 * t * t * t + c1 * t * t;
        }

        // ================================================================================================
        // v10: TEXTURE LOADING - satu Asset<Texture2D> per MarkerShape, lazy-loaded & di-cache manual
        // (ModContent.Request sendiri sudah cache internal, tapi field static di sini hindarin manggil
        // Request tiap frame/tiap shape-switch). Path folder MENGIKUTI struktur folder VFX yang udah
        // ada (WhoAmIPatternMarkerNoise.fx ada di .../VFX/Shaders/) - sesuaikan kalau folder final
        // project kamu beda.
        // ================================================================================================
        private const string MarkerTextureFolder = "TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/PatternMarkers/";
        private static readonly Dictionary<MarkerShape, Asset<Texture2D>> markerTextureCache = new Dictionary<MarkerShape, Asset<Texture2D>>();

        // Nama file HARUS sama persis (tanpa ekstensi) sama PNG hasil generate - lihat
        // WhoAmI_PatternMarkers.zip. Semua PNG 512x512, putih solid RGB + alpha channel (tintable).
        private static string GetMarkerTextureFile(MarkerShape shape)
        {
            switch (shape)
            {
                case MarkerShape.Cross: return "cross_boss_aura";
                case MarkerShape.Infinity: return "infinity_phantom_mirage_cascade";
                case MarkerShape.XBlade: return "xblade_blink_echo_combo";
                case MarkerShape.Triangle: return "triangle_orbit_grid_lock";
                case MarkerShape.Spiral: return "spiral_gravity_well_torrent";
                case MarkerShape.MirrorStar: return "mirrorstar_mirror_mirage";
                case MarkerShape.Halo: return "halo_summon_rift_swarm";
                case MarkerShape.Hex: return "hex_whip_lash_cage";
                case MarkerShape.CrossedRays: return "crossedrays_boomerang_crossfire";
                case MarkerShape.Starburst: return "starburst_abyssal_cleave";
                case MarkerShape.Grid: return "grid_vector_laser_grid";
                case MarkerShape.TwinLance: return "twinlance_mirror_lance_rupture";
                case MarkerShape.Shatter: return "shatter_quantum_glitch_phasing";
                case MarkerShape.Crescent: return "crescent_warped_mirror_waltz";
                case MarkerShape.EchoTriad: return "echotriad_fractured_persona_onslaught";
                case MarkerShape.Chevron: return "chevron_riposte_cascade";
                case MarkerShape.Blink: return "blink_parallax_volley";
                case MarkerShape.Ricochet: return "ricochet_mirror_ricochet";
                case MarkerShape.Starfall: return "starfall_convergence";
                case MarkerShape.Bloom: return "bloom_fracture_bloom";
                case MarkerShape.BinaryOrbit: return "binaryorbit_umbral_duality";
                case MarkerShape.PincerMirror: return "pincermirror_paradox_mirror_volley";
                case MarkerShape.ConstrictNet: return "constrictnet_wraith_convergence";
                case MarkerShape.TetherLines: return "tetherlines_soul_tether_bind";
                case MarkerShape.Carousel: return "carousel_spectral_carousel";
                case MarkerShape.CoilSpiral: return "coilspiral_serpents_coil";
                case MarkerShape.WideFan: return "widefan_cracked_fan_lash";
                case MarkerShape.ChainCurve: return "chaincurve_puppeteers_snap";
                case MarkerShape.Pendulum: return "pendulum_reckoning";
                case MarkerShape.Windmill: return "windmill_barrage";
                case MarkerShape.CurvingArc: return "curvingarc_curving_return_barrage";
                default: return "cross_boss_aura";
            }
        }

        private static Asset<Texture2D> GetMarkerTexture(MarkerShape shape)
        {
            if (markerTextureCache.TryGetValue(shape, out Asset<Texture2D> cached))
                return cached; // may legitimately be null - see below, that's the whole point

            // FIX ("projectile/marker jadi blob magenta gede"): ModContent.Request<Texture2D> TIDAK
            // pernah balikin null buat asset yang hilang - tModLoader diam-diam substitusi tekstur
            // placeholder magenta/hitam bawaannya sendiri (yang notabene VALID, non-null Texture2D).
            // Dulu di sini pakai Request() + null-check (`cached?.Value != null` / `?.Value ?? glow`
            // di call site) - keduanya SELALU lolos begitu placeholder itu ke-load, karena secara
            // teknis .Value memang nggak null. Efeknya: kalau salah satu PNG marker (ada 31 shape,
            // semuanya harus di-drop manual dari WhoAmI_PatternMarkers.zip) belum ada di folder
            // Content/.../VFX/PatternMarkers/, shape itu KE-CACHE PERMANEN sebagai placeholder rusak,
            // dan fallback "?? glow" di DrawPatternMarker gak pernah kepakai - placeholder magenta
            // solid itu yang digambar 3x numpuk (outer/mid/core) di ukuran markerDiameter, kebaca
            // sebagai blob magenta blocky gede persis kayak yang dilaporkan.
            //
            // RequestIfExists adalah API resmi buat kasus ini - balikin FALSE (bukan placeholder)
            // kalau assetnya beneran belum ada, jadi cached bisa di-set null SUNGGUHAN dan fallback
            // "?? glow" di call site akhirnya benar-benar jalan seperti niatnya.
            Asset<Texture2D> asset = ModContent.RequestIfExists<Texture2D>(
                MarkerTextureFolder + GetMarkerTextureFile(shape), out Asset<Texture2D> found, AssetRequestMode.ImmediateLoad)
                ? found
                : null;
            markerTextureCache[shape] = asset;
            return asset;
        }

        // v10: pengganti "posisi ujung lengan ke-i" lama - titik-titik merata di tepi piringan glyph,
        // dipakai buat spark & (kalau nanti dibutuhkan) attach point lain yang dulu numpang arms[].
        private static Vector2 GetGlyphRingPoint(float baseRotation, float radius, int index, int count)
        {
            float a = baseRotation + MathHelper.TwoPi * index / count;
            return new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * radius;
        }

        // v10: gambar 1 glyph utuh (3 pass core/mid/outer buat kedalaman warna - pengganti langsung
        // dari gradasi core/mid/outer yang dulu dibangun dari banyak arm quad). `scaleMult`/`alphaMult`
        // dipakai buat ghost echo (Sway/Chain/Carousel) supaya bisa manggil helper yang sama persis
        // buat glyph utama MAUPUN echo-nya, bukan duplikasi logic gambar.
        private void DrawMarkerGlyph(SpriteBatch spriteBatch, Texture2D marker, Vector2 markerOrigin, Vector2 drawPos,
            float rotation, float scale, Color markCore, Color markMid, Color markOuter, float alpha,
            float scaleMult = 1f, float alphaMult = 1f, float outerFade = 1f)
        {
            float s = scale * scaleMult;
            float a = alpha * alphaMult;

            // OUTER - lebar & gelap, nge-anchor kontras (pengganti "halo belakang statis" lama).
            spriteBatch.Draw(marker, drawPos, null, markOuter * a * 0.40f * outerFade, rotation, markerOrigin, s * 1.32f, SpriteEffects.None, 0f);
            // MID - siluet utama, warna tema pattern, ini yang paling dominan kebaca.
            spriteBatch.Draw(marker, drawPos, null, markMid * a * 0.95f, rotation, markerOrigin, s, SpriteEffects.None, 0f);
            // CORE - inti terang ke arah putih, nambah kedalaman (pengganti "garis inti tajam" lama).
            spriteBatch.Draw(marker, drawPos, null, Color.Lerp(markMid, markCore, 0.85f) * a * 0.85f, rotation, markerOrigin, s * 0.56f, SpriteEffects.None, 0f);
        }

        // ---- Shader (.fx) noise layer - lihat WhoAmIPatternMarkerNoise.fx ----
        private static Asset<Effect> patternMarkerNoiseShader;

        private static void EnsurePatternMarkerShaderLoaded()
        {
            if (patternMarkerNoiseShader != null)
                return;

            patternMarkerNoiseShader = ModContent.Request<Effect>(
                "TheSanity/GlobalNPC/Bosses/WhoAmI/VFX/Shaders/WhoAmIPatternMarkerNoise",
                AssetRequestMode.ImmediateLoad);
        }

        private void DrawPatternMarkerVFX(SpriteBatch spriteBatch, Vector2 screenPos)
        {
            Color? patternColor = GetSecondWavePatternMarkerColor(aiState) ?? GetAttackPatternColor(out _);
            if (patternColor == null)
            {
                patternMarkerLastState = -1;
                return;
            }

            EnsureAuraTexturesLoaded();
            if (auraGlowTexture?.Value == null) return;

            if (aiState != patternMarkerLastState)
            {
                patternMarkerLastState = aiState;
                patternMarkerRotation = 0f;
                patternMarkerLocalTimer = 0;
                patternMarkerFireStopTimer = 0;
                patternMarkerFireFlashTimer = 0;
            }

            patternMarkerLocalTimer++;

            MarkerShape shape = GetMarkerShapeForState(aiState);
            MarkerMotion motion = GetMarkerMotionForShape(shape);
            float time = patternMarkerLocalTimer;

            float rampT = MathHelper.Clamp(patternMarkerLocalTimer / (float)PatternMarkerRampDuration, 0f, 1f);
            float rampEasedT = 1f - (float)Math.Pow(1f - rampT, 3);
            float baseSpinSpeed = MathHelper.Lerp(PatternMarkerMaxSpinSpeed, PatternMarkerCruiseSpinSpeed, rampEasedT);

            float stopFactor = 1f;
            if (patternMarkerFireStopTimer > 0)
            {
                float stopT = 1f - (patternMarkerFireStopTimer / (float)PatternMarkerFireStopDuration);
                stopFactor = 1f - stopT * stopT;
                patternMarkerFireStopTimer--;
            }

            // v10: nama variabel dipertahankan dari v9 (armLengthPulse/swayAmplitude/chainLagStrength)
            // biar diff-nya kebaca jelas, tapi MAKNANYA sekarang "seluruh glyph", bukan per-lengan -
            // lihat blok changelog v10 di atas.
            float armLengthPulse = 1f;       // -> markerScalePulse: kali ke scale keseluruhan glyph
            float swayAmplitude = 0f;        // -> spread rotasi buat 2 ghost echo (Sway)
            float chainLagStrength = 0f;     // -> lag rotasi buat ghost trail (Chain)

            switch (motion)
            {
                case MarkerMotion.SteadySpin:
                    patternMarkerRotation += baseSpinSpeed * stopFactor;
                    break;

                case MarkerMotion.Heavy:
                    float wobble = 1f + 0.35f * (float)Math.Sin(time * 0.05f);
                    patternMarkerRotation += baseSpinSpeed * wobble * stopFactor;
                    break;

                case MarkerMotion.Coil:
                    patternMarkerRotation += baseSpinSpeed * 0.6f * stopFactor;
                    armLengthPulse = MathHelper.Lerp(1f, MathHelper.Lerp(0.62f, 1.05f, 0.5f + 0.5f * (float)Math.Sin(time * 0.045f)), stopFactor);
                    break;

                case MarkerMotion.Pulse:
                    patternMarkerRotation += baseSpinSpeed * 0.08f * stopFactor;
                    armLengthPulse = MathHelper.Lerp(1f, MathHelper.Lerp(0.78f, 1.18f, 0.5f + 0.5f * (float)Math.Sin(time * 0.09f)), stopFactor);
                    break;

                case MarkerMotion.Surge:
                    patternMarkerRotation += baseSpinSpeed * 0.08f * stopFactor;
                    {
                        float surgeCycle = (time % 34f) / 34f;
                        float surgeT = surgeCycle < 0.3f
                            ? EaseOutBack(surgeCycle / 0.3f)
                            : MathHelper.Lerp(1f, 0.85f, (surgeCycle - 0.3f) / 0.7f);
                        armLengthPulse = MathHelper.Lerp(1f, MathHelper.Lerp(0.8f, 1.22f, surgeT), stopFactor);
                    }
                    break;

                case MarkerMotion.Carousel:
                    patternMarkerRotation += baseSpinSpeed * stopFactor;
                    break;

                case MarkerMotion.Sway:
                    patternMarkerRotation += baseSpinSpeed * 0.12f * stopFactor;
                    swayAmplitude = MathHelper.Lerp(0f, 0.5f, stopFactor);
                    break;

                case MarkerMotion.Chain:
                    patternMarkerRotation += baseSpinSpeed * stopFactor;
                    chainLagStrength = MathHelper.Lerp(0f, 1f, stopFactor);
                    break;

                case MarkerMotion.Jitter:
                    patternMarkerRotation += baseSpinSpeed * 0.05f * stopFactor;
                    break;

                case MarkerMotion.Pendulum:
                    {
                        float swingSpeed = MathHelper.Lerp(0.02f, 0.085f, rampEasedT) * MathHelper.Lerp(0.35f, 1f, stopFactor);
                        float swingAmplitude = MathHelper.PiOver4 * 1.3f * MathHelper.Lerp(0.3f, 1f, stopFactor);
                        float swing = (float)Math.Sin(time * swingSpeed) + 0.15f * (float)Math.Sin(time * swingSpeed * 3f);
                        patternMarkerRotation = swing * swingAmplitude;
                    }
                    break;

                case MarkerMotion.Locked:
                case MarkerMotion.Ricochet:
                    {
                        float snapInterval = motion == MarkerMotion.Locked ? 46f : 24f;
                        float snapEase = motion == MarkerMotion.Locked ? 10f : 26f;
                        float cycle = time % snapInterval;
                        int headingIndex = (int)(time / snapInterval);
                        const float goldenAngle = 2.399963f;
                        float targetHeading = headingIndex * goldenAngle;
                        float prevHeading = (headingIndex - 1) * goldenAngle;
                        float snapT = MathHelper.Clamp(cycle / Math.Max(1f, snapEase), 0f, 1f);
                        snapT = 1f - (float)Math.Pow(1f - snapT, 3);
                        patternMarkerRotation = MathHelper.Lerp(prevHeading, targetHeading, snapT * stopFactor);
                    }
                    break;
            }

            float popIn = MathHelper.Clamp(patternMarkerLocalTimer / 6f, 0f, 1f);
            popIn = 1f - (1f - popIn) * (1f - popIn);
            float alpha = popIn;
            if (alpha <= 0.02f) return;

            // ---- GRADASI WARNA (core terang -> mid tema pattern -> outer gelap) ----
            Color markMid = patternColor.Value;
            Color markCore = Color.Lerp(markMid, Color.White, 0.6f);
            Color markOuter = Color.Lerp(markMid, Color.Black, 0.35f);

            Vector2 drawPos = NPC.Center - screenPos;
            float sizeRef = Math.Max(NPC.width, NPC.height) / 90f;

            Texture2D glow = auraGlowTexture.Value;
            Vector2 glowOrigin = new Vector2(glow.Width / 2f, glow.Height / 2f);

            Texture2D marker = GetMarkerTexture(shape)?.Value ?? glow; // fallback kalau asset belum ke-drop di Content/
            Vector2 markerOrigin = new Vector2(marker.Width / 2f, marker.Height / 2f);

            float breathe = 1f + 0.06f * (float)Math.Sin(patternMarkerLocalTimer * 0.08f);
            float markerScalePulse = armLengthPulse; // lihat comment "v10 nama variabel" di atas
            float markerDiameter = sizeRef * PatternMarkerDiameterPerSizeRef * MathHelper.Lerp(0.35f, 1.9f, popIn) * breathe * markerScalePulse;
            float markerScale = markerDiameter / marker.Width;

            // energyFade: pengganti "diagonalFade" lama (dulu ngefade alpha lengan aksen ikut cruise
            // spin) - sekarang ngefade pass OUTER si glyph, biar transisi ramp->cruise tetap kerasa
            // ada "napas" di kontras luar-dalamnya, bukan cuma di kecepatan rotasi.
            float energyFade = MathHelper.Clamp((baseSpinSpeed * stopFactor - PatternMarkerCruiseSpinSpeed * 0.5f) / PatternMarkerMaxSpinSpeed, 0.35f, 1f);

            float fxKickSpeed = baseSpinSpeed * stopFactor;

            bool isJitterMotion = motion == MarkerMotion.Jitter;
            bool isBigJitter = shape == MarkerShape.Blink; // Blink: loncat besar & jarang vs Shatter: goyang kecil konstan

            float extraRot = 0f;
            Vector2 jitterOffset = Vector2.Zero;

            if (isJitterMotion)
            {
                if (isBigJitter)
                {
                    const float holdTicks = 9f;
                    int popIndex = (int)(time / holdTicks);
                    float seed = popIndex * 12.9898f;
                    float rx = (float)((Math.Sin(seed) * 43758.5453) % 1.0);
                    float ry = (float)((Math.Sin(seed * 1.7) * 24634.6345) % 1.0);
                    jitterOffset = new Vector2(rx, ry) * sizeRef * 5f * stopFactor;
                }
                else
                {
                    float jSeed = time * 0.6f;
                    extraRot = (float)Math.Sin(jSeed) * 0.12f * stopFactor;
                    jitterOffset = new Vector2((float)Math.Sin(jSeed * 1.7f), (float)Math.Cos(jSeed * 2.1f)) * sizeRef * 1.5f * stopFactor;
                }
            }

            BeginAdditive(spriteBatch);

            // ---- GLYPH UTAMA ----
            DrawMarkerGlyph(spriteBatch, marker, markerOrigin, drawPos + jitterOffset, patternMarkerRotation + extraRot,
                markerScale, markCore, markMid, markOuter, alpha, outerFade: energyFade);

            // ---- SWAY: 2 ghost echo, tiap satu goyang di fase sendiri (MirrorStar/EchoTriad/
            // BinaryOrbit/TetherLines - "reflections/echo yang gak gerak serempak") ----
            if (swayAmplitude > 0f)
            {
                for (int i = 0; i < 2; i++)
                {
                    float swaySeed = time * 0.03f + i * 2.4f + 1.7f;
                    float echoRot = patternMarkerRotation + (float)Math.Sin(swaySeed) * swayAmplitude * (i == 0 ? 1f : -1f);
                    DrawMarkerGlyph(spriteBatch, marker, markerOrigin, drawPos, echoRot, markerScale, markCore, markMid, markOuter,
                        alpha, scaleMult: 0.86f, alphaMult: 0.4f);
                }
            }

            // ---- CHAIN: ghost trail keteteran di belakang rotasi utama (ChainCurve - "cambukan") ----
            if (chainLagStrength > 0f)
            {
                for (int i = 1; i <= 2; i++)
                {
                    float lagRot = patternMarkerRotation - chainLagStrength * i * 0.22f * (float)Math.Sin(time * 0.05f - i * 0.3f) - i * 0.16f;
                    DrawMarkerGlyph(spriteBatch, marker, markerOrigin, drawPos, lagRot, markerScale, markCore, markMid, markOuter,
                        alpha, scaleMult: 1f - i * 0.1f, alphaMult: 0.35f / i);
                }
            }

            // ---- CAROUSEL: 4 mini-glyph orbit di ring kecil, bobbing sendiri-sendiri (Carousel -
            // "kuda-kuda naik-turun sendiri") ----
            if (motion == MarkerMotion.Carousel)
            {
                const int carouselCount = 4;
                float carouselRingRadius = markerDiameter * 0.62f;
                for (int i = 0; i < carouselCount; i++)
                {
                    float carSeed = time * 0.05f + i * (MathHelper.TwoPi / carouselCount);
                    float carPulse = 0.7f + 0.5f * (0.5f + 0.5f * (float)Math.Sin(carSeed));
                    float carBob = MathHelper.Lerp(1f, carPulse, stopFactor);

                    Vector2 carOffset = GetGlyphRingPoint(patternMarkerRotation, carouselRingRadius, i, carouselCount);
                    DrawMarkerGlyph(spriteBatch, marker, markerOrigin, drawPos + carOffset, patternMarkerRotation * 1.4f,
                        markerScale, markCore, markMid, markOuter, alpha, scaleMult: 0.32f * carBob, alphaMult: 0.75f);
                }
            }

            // ---- NOISE v4 (2-octave, drift, flicker) - masih numpang auraNoiseTexture, sekarang
            // discale mengikuti footprint BULAT si glyph (bukan bar-shaped kayak dulu) ----
            if (auraNoiseTexture?.Value != null)
            {
                Texture2D noise = auraNoiseTexture.Value;
                Vector2 noiseOrigin = new Vector2(noise.Width / 2f, noise.Height / 2f);
                Vector2 noiseScaleCoarse = new Vector2(markerDiameter * 0.62f / noise.Width, markerDiameter * 0.62f / noise.Height);
                Vector2 noiseScaleFine = new Vector2(markerDiameter * 0.46f / noise.Width, markerDiameter * 0.46f / noise.Height);

                float noiseDriftA = (float)Math.Sin(patternMarkerLocalTimer * 0.021f) * 0.35f;
                float noiseDriftB = (float)Math.Cos(patternMarkerLocalTimer * 0.017f) * 0.35f;
                float grainFlicker = 0.55f + 0.25f * (float)Math.Sin(patternMarkerLocalTimer * 0.37f) * (float)Math.Sin(patternMarkerLocalTimer * 0.13f + 1.1f);

                // OCTAVE 1 - COARSE: dasar tekstur lebar, drift pelan, tint OUTER, alpha rendah.
                spriteBatch.Draw(noise, drawPos, null, markOuter * alpha * 0.16f * grainFlicker, patternMarkerRotation + noiseDriftA, noiseOrigin, noiseScaleCoarse, SpriteEffects.None, 0f);
                // OCTAVE 2 - FINE: detail lebih rapat, drift lebih cepet & berkebalikan, tint MID.
                spriteBatch.Draw(noise, drawPos, null, markMid * alpha * 0.30f * grainFlicker, patternMarkerRotation - noiseDriftB, noiseOrigin, noiseScaleFine, SpriteEffects.None, 0f);

                // RING NOISE - lapisan noise BUNDAR tipis nempel di halo belakang.
                float ringSpin = -patternMarkerLocalTimer * 0.01f;
                Vector2 ringScale = new Vector2(markerDiameter * 1.3f / noise.Width, markerDiameter * 1.3f / noise.Height) * popIn;
                spriteBatch.Draw(noise, drawPos, null, markOuter * alpha * 0.07f, ringSpin, noiseOrigin, ringScale, SpriteEffects.None, 0f);
            }

            // ---- FIRE FLASH: bright pop exactly when the marker "lets go" ----
            if (patternMarkerFireFlashTimer > 0)
            {
                float flashT = patternMarkerFireFlashTimer / (float)PatternMarkerFireFlashDuration;
                float flashScale = markerDiameter * MathHelper.Lerp(0.75f, 0.22f, flashT) * popIn / glow.Width;
                spriteBatch.Draw(glow, drawPos, null, Color.White * alpha * flashT * 0.8f, 0f, glowOrigin, flashScale, SpriteEffects.None, 0f);
                patternMarkerFireFlashTimer--;
            }

            // ---- INTI + CHROMATIC-SPLIT ----
            float coreDotScale = markerDiameter * 0.12f * popIn / glow.Width;
            spriteBatch.Draw(glow, drawPos, null, markCore * alpha, 0f, glowOrigin, coreDotScale, SpriteEffects.None, 0f);
            float chromaShift = 3f * (0.6f + 0.4f * (float)Math.Sin(patternMarkerLocalTimer * 0.1f));
            Vector2 chromaDir = new Vector2((float)Math.Cos(patternMarkerRotation), (float)Math.Sin(patternMarkerRotation));
            float chromaDotScale = markerDiameter * 0.09f * popIn / glow.Width;
            spriteBatch.Draw(glow, drawPos - chromaDir * chromaShift, null, new Color(255, 50, 50) * alpha * 0.35f, 0f, glowOrigin, chromaDotScale, SpriteEffects.None, 0f);
            spriteBatch.Draw(glow, drawPos + chromaDir * chromaShift, null, new Color(50, 220, 255) * alpha * 0.35f, 0f, glowOrigin, chromaDotScale, SpriteEffects.None, 0f);

            // ---- SATELIT MINI DI SEKITARNYA (sprite senjata dari inventory player) ----
            Texture2D satelliteTex = (activeWeapon != null && activeWeapon.type > ItemID.None)
                ? TextureAssets.Item[activeWeapon.type].Value
                : glow;
            Vector2 satelliteOrigin = new Vector2(satelliteTex.Width / 2f, satelliteTex.Height / 2f);
            float satelliteTargetSize = markerDiameter * 0.18f;
            float satelliteBaseScale = satelliteTargetSize / Math.Max(satelliteTex.Width, satelliteTex.Height);

            float orbitAngleBase = -patternMarkerRotation * 0.55f; // arah berlawanan, lebih pelan
            float orbitRadius = markerDiameter * 0.5f * 1.35f;
            for (int i = 0; i < PatternMarkerSatelliteCount; i++)
            {
                float orbitAngle = orbitAngleBase + MathHelper.TwoPi * i / PatternMarkerSatelliteCount;
                Vector2 satPos = drawPos + new Vector2((float)Math.Cos(orbitAngle), (float)Math.Sin(orbitAngle)) * orbitRadius;

                float twinklePhase = patternMarkerLocalTimer * 0.06f + i * 1.7f;
                float twinkle = 0.5f + 0.5f * (float)Math.Sin(twinklePhase);
                float satAlpha = alpha * (0.35f + 0.55f * twinkle);
                float satScale = satelliteBaseScale * MathHelper.Lerp(0.75f, 1.15f, twinkle) * popIn;
                Color satColor = Color.Lerp(markMid, Color.White, 0.3f + 0.3f * twinkle);

                spriteBatch.Draw(satelliteTex, satPos, null, satColor * satAlpha, 0f, satelliteOrigin, satScale, SpriteEffects.None, 0f);
            }

            EndAdditive(spriteBatch);

            // ---- SHADER (.fx) NOISE PASS - lapisan EKSTRA di atas semua sprite di atas ----------
            // v10: dulu redraw 2 lengan utama lewat shader - sekarang redraw GLYPH-nya sendiri
            // (1 pass) lewat shader yang sama, biar noise shader-nya modulasi bentuk beneran, bukan
            // 2 bar abstrak.
            EnsurePatternMarkerShaderLoaded();
            if (patternMarkerNoiseShader?.Value != null)
            {
                Effect fx = patternMarkerNoiseShader.Value;
                fx.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
                fx.Parameters["uColorCore"]?.SetValue(markCore.ToVector3());
                fx.Parameters["uColorMid"]?.SetValue(markMid.ToVector3());
                fx.Parameters["uColorOuter"]?.SetValue(markOuter.ToVector3());
                fx.Parameters["uNoiseScale"]?.SetValue(6f);
                fx.Parameters["uNoiseStrength"]?.SetValue(0.6f);

                spriteBatch.End();

                spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                    DepthStencilState.None, RasterizerState.CullNone, fx, Main.GameViewMatrix.TransformationMatrix);

                spriteBatch.Draw(marker, drawPos, null, Color.White * alpha * 0.85f, patternMarkerRotation, markerOrigin, markerScale * 1.02f, SpriteEffects.None, 0f);

                spriteBatch.End();

                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                    DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            // ---- SPARK, numpang Luminance ----
            // v10: dulu numpang arms[] (tiap arm.Angle/LengthMult/PerpOffsetMult) buat cari titik
            // ujung lengan - sekarang gak ada arms[] lagi, jadi dipakein titik-titik merata di tepi
            // piringan glyph (GetGlyphRingPoint) sebagai gantinya. Jumlah titik digenapin 8 biar tetap
            // kerasa "ramai" kayak shape ber-banyak-lengan dulu, terlepas dari shape aslinya berapa
            // "lengan" (toh sekarang shape-nya 1 glyph utuh, bukan N lengan lepas).
            if (patternMarkerLocalTimer % 4 == 0)
            {
                const int sparkPointCount = 8;
                float tipDist = markerDiameter * 0.5f * 0.92f;
                for (int i = 0; i < sparkPointCount; i++)
                {
                    Vector2 dir = GetGlyphRingPoint(patternMarkerRotation, 1f, i, sparkPointCount);
                    Vector2 tip = NPC.Center + dir * tipDist;
                    Color sparkColor = i % 2 == 0 ? markMid : markOuter;
                    LuminanceUtilities.SpawnParticle(tip, dir * (fxKickSpeed + 0.4f) * 2.5f, sparkColor, 14, 0.55f, ParticleType.Spark);
                }
            }

            // ...dan sesekali dari salah satu satelit mini, kesan "glow kecil" itu beneran melepas
            // percikan sendiri, bukan cuma sprite diem yang orbit doang.
            if (patternMarkerLocalTimer % 10 == 0)
            {
                int satIndex = (patternMarkerLocalTimer / 10) % PatternMarkerSatelliteCount;
                float orbitAngle2 = orbitAngleBase + MathHelper.TwoPi * satIndex / PatternMarkerSatelliteCount;
                Vector2 satWorldPos = NPC.Center + new Vector2((float)Math.Cos(orbitAngle2), (float)Math.Sin(orbitAngle2)) * orbitRadius;
                LuminanceUtilities.SpawnParticle(satWorldPos, Main.rand.NextVector2Circular(0.6f, 0.6f), Color.Lerp(markMid, Color.White, 0.4f), 16, 0.4f, ParticleType.Spark);
            }

            // ---- AMBIENT GLOW DUST - TERSEBAR DI SELURUH AREA VFX, BUKAN CUMA DI UJUNG ----
            if (patternMarkerLocalTimer % 5 == 0 && patternMarkerFireStopTimer <= 0 && patternMarkerFireFlashTimer <= 0)
            {
                float dustAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                float dustRadius = (float)Math.Sqrt(Main.rand.NextFloat()) * orbitRadius;
                Vector2 dustSpawn = NPC.Center + new Vector2((float)Math.Cos(dustAngle), (float)Math.Sin(dustAngle)) * dustRadius;

                Vector2 dustDrift = Main.rand.NextVector2Circular(0.7f, 0.7f) + new Vector2(0f, -0.25f);
                Color dustColor = Color.Lerp(markMid, Color.White, Main.rand.NextFloat(0.2f, 0.6f));
                float dustScale = Main.rand.NextFloat(0.28f, 0.5f) * popIn;
                LuminanceUtilities.SpawnParticle(dustSpawn, dustDrift, dustColor * 0.85f, 24, dustScale, ParticleType.Spark);
            }
        }
    }
}