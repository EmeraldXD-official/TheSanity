using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics; // Luminance ScreenShakeSystem
using ReLogic.Content;
using Terraria.GameContent; // TextureAssets.MagicPixel (buat gambar garis listrik hitam & beam cahaya)
using TheSanity.Buff;
using TheSanity.Common.Systems; // ShockwaveSystem (lihat ShockwaveSystem.cs)

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    // ==================================================================================
    // BOLA LISTRIK MERAH RAKSASA (Pattern 4 - Electro Nova)
    // ai[0] = whoAmI NPC PlutoHead pemiliknya
    // ai[1] = state -> 0 = Charging (nempel di atas kepala Pluto & membesar selama ~5 detik)
    //                  1 = Meluncur/homing ke player (di-set oleh PlutoHead saat stage push)
    //                  2 = Meledak (visual aura membesar sebelum bola bener2 hilang)
    //
    // Setelah diluncurkan, bola homing lambat ke player (pelan-pelan nyamain kecepatannya) selama
    // total 15 detik. Di 3 detik terakhir, bola berhenti ngejar & balik melayang di atas kepala
    // Pluto lagi (posisi kayak pas awal charge), sambil berputar & mengecil paksa (jitter) serta
    // nembakin 40 RedBolt ke segala arah random sebanyak 5x, lalu BENER-BENER meledak begitu
    // waktu hidupnya abis. Kena player TIDAK bikin bola meledak (cuma damage+debuff biasa).
    //
    // Visual pakai sprite custom "PlutoElectrictBall.png", + trik shader dye vanilla
    // (GameShaders.Armor, dye "Bloodbath") buat efek warna yang hidup/berdenyut, ditambah petal
    // warna kanan/kiri/atas/bawah yang berdenyut beda fase, digambar dengan blend Additive supaya
    // keliatan menyala kayak bola energi, mirip gaya SunInThePalm_EnergyBall.cs tapi full pakai
    // API vanilla tModLoader (tanpa framework luar). Saat meledak, muncul aura merah berputar
    // dari sprite "ElectrictBallExplosion.png" yang membesar 3x lipat lebih lebar dari bolanya.
    // ==================================================================================
    public class PlutoElectroBall : ModProjectile
    {
        // Harus SAMA dengan PlutoHead.NovaChargeTime di ElectroNovaDash.cs, biar timing sinkron
        // 🛑 [LOKASI BALANCING DURASI CHARGE] -> dinaikin jadi 5 detik
        private const int ChargeTime = 300;

        // 🛑 [LOKASI BALANCING LIFE TIME BOLA SETELAH DILEMPAR] -> dinaikin jauh dari 15 detik (900)
        // ke ~33 detik (1980) sesuai request, biar bola idup jauh lebih lama sebelum meledak
        // otomatis (termasuk fase homing chase + fase mengecil di 3 detik terakhir).
        private const int ExplodeAfterLaunch = 1980;

        // 🛑 [LOKASI BALANCING MENTAL DI ATAS WORLD] Bola TIDAK tembus dinding batas atas world
        // (mental/reflect ke bawah kalau kena situ), TAPI tileCollide tetap false jadi dia tetep
        // nembus block biasa seperti default (ga ada perubahan buat itu).
        private const float TopOfWorldBounceY = 16f;

        // 🛑 [LOKASI BALANCING FASE AKHIR] -> 3 detik terakhir dari ExplodeAfterLaunch: bola berhenti
        // ngejar, berputar & mengecil paksa (jitter), sambil nembakin RedBolt ke segala arah
        private const int FinalPhaseDuration = 180;

        // Berapa kali volley RedBolt ditembakkan selama fase akhir (mengecil) tsb
        private const int RedBoltVolleys = 5;
        private const int RedBoltsPerVolley = 40;

        // 🛑 [LOKASI BALANCING HOMING] bola homing lambat ke player, tapi pelan-pelan bisa
        // menyamai kecepatan player biar ga ketinggalan
        private const float HomingTurnRate = 0.02f;
        private const float HomingSpeedMatchRate = 0.03f;
        private const float HomingMinSpeed = 5f;
        private const float HomingMaxSpeed = 16f;

        // Durasi aura ledakan (pakai sprite ElectrictBallExplosion.png) -> dinaikin dikit (40->55)
        // biar aura yang sekarang GEDE BANGET (10x) sempet keliatan penuh sebelum ilang
        private const int ExplosionVisualDuration = 55;

        // 🛑 [LOKASI BALANCING BESAR AURA LEDAKAN] Aura sekarang LANGSUNG 10x lipat diameter bola
        // PAS dia meledak -- gede banget sesuai request, bukan lagi "+20 block lalu x3" kayak dulu.
        private const float ExplosionAuraSizeMultiplier = 10f; // 10x lipat lebih besar dari diameter bola

        // Bola akan tumbuh sampai 5x ukuran Pluto
        private const float MaxScaleMultiplier = 5f;

        private float chargeTimer = 0f;
        private float flightTimer = 0f;
        private bool hasExploded = false;

        private bool finalPhaseStarted = false;
        private float scaleAtFinalPhaseStart = 1f;
        private int volleysFired = 0;

        // 🛑 [LAST FIX] Bola BERHENTI TOTAL di posisi terakhirnya begitu masuk fase akhir (BUKAN
        // ditarik balik ke atas kepala Pluto lagi). Dia baru boleh mulai mengecil (shrinkTimer jalan)
        // kalau Pluto sendiri yang udah mendekat & manggil BeginShrinking() (lihat ElectroNovaDash.cs).
        private bool shrinkTriggered = false;
        private int shrinkTimer = 0;
        private Vector2 frozenFinalPosition;

        private float explosionVisualTimer = 0f;

        // 🛑 [LOKASI FLASH LEDAKAN] Berapa tick SEBELUM shrinkTimer nyampe FinalPhaseDuration
        // (yaitu momen persis Explode() bakal kepanggil) biar efek flash di PlutoHead.cs udah
        // mulai nyala DULUAN -- kayak ada build-up/telegraph sesaat sebelum boom-nya, bukan
        // baru nongol pas ledakannya kejadian.
        private const int PreExplosionFlashWindow = 25;

        // True dari mulai window early-warning di atas, TERUS true sampai visual ledakan abis.
        // Dipakai PlutoHead.cs sebagai kondisi utama buat masuk mode flash (gantiin IsExploding).
        public bool IsFlashActive => IsExploding || (shrinkTriggered && !hasExploded && (FinalPhaseDuration - shrinkTimer) <= PreExplosionFlashWindow);

        // Progress GABUNGAN dari 0 (mulai window pre-explosion) -> 0.5 (PAS meledak) -> 1
        // (visual ledakan abis). PlutoHead.cs pakai satu kurva ini biar flash-nya nge-ramp
        // naik dulu sebelum boom, baru decay cepat sesudahnya -- bukan lompat tiba-tiba.
        public float FlashProgress {
            get {
                if (hasExploded) return 0.5f + 0.5f * ExplosionFlashProgress;

                if (shrinkTriggered) {
                    int ticksLeft = FinalPhaseDuration - shrinkTimer;
                    if (ticksLeft <= PreExplosionFlashWindow) {
                        float t = 1f - MathHelper.Clamp(ticksLeft / (float)PreExplosionFlashWindow, 0f, 1f);
                        return 0.5f * t;
                    }
                }

                return 0f;
            }
        }

        // 🛑 [LOKASI FLASH LEDAKAN] Dipakai PlutoHead.cs buat bikin efek "kesorot" jadi flash
        // sekejap (kayak shockwave cahaya) tepat pas bola ini meledak, alih-alih efek rim-light
        // yang halus/bertahap kayak biasa. True dari awal Explode() dipanggil sampai
        // ExplosionVisualDuration abis.
        public bool IsExploding => hasExploded && explosionVisualTimer < ExplosionVisualDuration;

        // 0 pas mulai meledak -> 1 pas visual ledakannya abis (durasi = ExplosionVisualDuration,
        // ~0.9 detik). Dipakai PlutoHead.cs buat nge-decay flash-nya CEPAT (bukan fade halus).
        public float ExplosionFlashProgress => hasExploded ? MathHelper.Clamp(explosionVisualTimer / ExplosionVisualDuration, 0f, 1f) : 0f;

        // Diameter bola (px) tepat pas Explode() dipanggil -> dipakai buat nentuin besar aura ledakan
        private float ballDiameterAtExplode = 0f;

        // 🛑 [FIX LEDAKAN KELIATAN KECIL/GA KERASA] Sebelumnya `ballDiameterAtExplode` diambil dari
        // Projectile.width PAS Explode() dipanggil -- padahal di titik itu bola SUDAH dipaksa mengecil
        // habis-habisan (FinalShrinkPhase, turun sampai scale 0.05f / ~8px) sebelum meledak. Akibatnya
        // aura ledakan yang harusnya "10x diameter bola" itu dihitung dari bola yang udah nyaris invisible,
        // jadi ledakannya keliatan cuma kedip kecil, bukan aura raksasa yang dimaksud.
        // Solusinya: lacak diameter TERBESAR yang pernah dicapai bola sepanjang hidupnya (pas charging
        // penuh / lagi terbang sebelum masuk fase mengecil), lalu itu yang dipakai buat hitung aura.
        private float peakDiameter = 0f;

        // Sprite custom milikmu sendiri. Sesuaikan path ini kalau lokasi filenya berbeda.
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/PlutoElectrictBall";

        // 🛑 [FIX LEDAKAN GA MUNCUL] Sprite "ElectrictBallExplosion.png" sebelumnya cuma di-request
        // (ModContent.Request) pas state ai[1] == 2 dimulai (pas bola mulai meledak). Karena default
        // AssetRequestMode itu ASYNC, sementara fase ledakan cuma ~0.67 detik (ExplosionVisualDuration
        // = 40 tick), asset-nya sering belum selesai ke-load pas udah keburu ke-Kill(). Solusinya:
        // preload sprite ini dari awal (ImmediateLoad) & simpan di static field, biar udah pasti siap
        // dipakai kapan aja tanpa delay loading.
        private static Asset<Texture2D> ExplosionAuraTexture;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;

            // 🛑 [FIX LEDAKAN GA MUNCUL] Hitbox bola bisa sekecil 8px pas fase mengecil sebelum
            // meledak, padahal aura ledakannya sekarang bisa 10x diameter bola (bisa RIBUAN pixel).
            // Fluff dinaikin jauh lebih besar (1400 -> 4000) biar Terraria ga nge-cull gambar aura
            // pas Pluto/bola lagi deket pinggir layar.
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;

            ExplosionAuraTexture = ModContent.Request<Texture2D>(
                "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/ElectrictBallExplosion",
                AssetRequestMode.ImmediateLoad
            );
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 100000; // dikontrol manual lewat flightTimer, bukan despawn otomatis
            Projectile.netImportant = true;
            Projectile.light = 0.9f;
            Projectile.alpha = 0;
            Projectile.scale = 0.15f;
        }

        // 🛑 Dipakai PlutoHead (ElectroNovaDash.cs) buat tau kapan bola udah masuk fase akhir
        // (berhenti ngejar, berputar & mengecil paksa) -> di titik ini Pluto bakal berhenti dash
        // dan mulai muter cepat ngelilingin bola sampai bolanya meledak.
        public bool IsInFinalPhase => (int)Projectile.ai[1] == 1 && flightTimer > (ExplodeAfterLaunch - FinalPhaseDuration);

        // 🛑 [LAST FIX] Dipanggil PlutoHead (ElectroNovaDash.cs) begitu Pluto udah cukup deket
        // dengan bola yang lagi diam di fase akhir -> baru dari sini bola boleh mulai mengecil.
        public void BeginShrinking() {
            shrinkTriggered = true;
        }

        // 🛑 Dipakai PlutoHead buat cek apakah BeginShrinking() udah pernah dipanggil, supaya efek
        // percikan "connect" pas bagian depan Pluto nyampe ke bola cuma nyala SEKALI aja
        // (bukan tiap tick selama Pluto masih diam di deket bola).
        public bool HasBegunShrinking => shrinkTriggered;

        // 🛑 [LOKASI BALANCING GLOW & LISTRIK] Ukuran shader effect (arc listrik hitam) sekarang
        // dibuat lebih KECIL lagi (turun dari 1.35x -> 1.1x) -- cuma nyembul dikit dari tepi bola,
        // ga usah gede-gede banget. Hitbox bola (Projectile.width/height) TIDAK ikut berubah -- ini
        // murni ukuran gambar/shader-nya aja.
        private const float GlowExtraSizeMultiplier = 1.1f;

        // 🛑 [LOKASI GLOW PULSE "CEDAT-CEDUT"] Lapisan flare yang membesar-mengecil (pulse) ini
        // sekarang punya base size SENDIRI yang sengaja dibuat lebih GEDE daripada bola itu sendiri
        // dan daripada shader effect/sinar lainnya (GlowExtraSizeMultiplier & LightRayExtraSizeMultiplier
        // di bawah) -- biar keliatan sebagai satu lapisan glow besar yang "berdenyut" ngelilingin
        // semuanya.
        private const float GlowPulseBaseSizeMultiplier = 1.6f;

        // 🛑 [LOKASI BALANCING SINAR/LIGHT RAY BEAMS] Beam cahaya (sorotan) tetap 5% lebih gede dari
        // ukuran shader effect di atas (GlowExtraSizeMultiplier) -- otomatis ikut turun proporsional
        // karena GlowExtraSizeMultiplier-nya udah dikecilin lagi.
        private const float LightRayExtraSizeMultiplier = GlowExtraSizeMultiplier * 1.05f;

        private NPC Owner {
            get {
                int idx = (int)Projectile.ai[0];
                if (idx < 0 || idx >= Main.maxNPCs) return null;
                return Main.npc[idx];
            }
        }

        public override void AI() {
            int state = (int)Projectile.ai[1];

            // 🛑 [FIX LEDAKAN KELIATAN KECIL] Catat diameter terbesar yang pernah dicapai bola
            // SEBELUM state behavior di bawah sempat mengecilkannya (dipakai nanti di Explode()).
            if (Projectile.width > peakDiameter) {
                peakDiameter = Projectile.width;
            }

            if (state == 0) {
                ChargingBehavior();
            }
            else if (state == 1) {
                FlyingBehavior();
            }
            else if (state == 2) {
                ExplodingVisualBehavior();
            }
        }

        private void ChargingBehavior() {
            NPC owner = Owner;
            if (owner == null || !owner.active) {
                Projectile.Kill();
                return;
            }

            chargeTimer++;
            float progress = MathHelper.Clamp(chargeTimer / ChargeTime, 0f, 1f);

            // Bola melayang tepat di atas kepala Pluto, makin naik makin gede
            float hoverOffset = -(owner.height * owner.scale * 0.5f) - 60f - (progress * 160f);
            Projectile.Center = owner.Center + new Vector2(0f, hoverOffset);
            Projectile.velocity = Vector2.Zero;

            float targetScale = MathHelper.Lerp(0.15f, MaxScaleMultiplier * owner.scale, progress);
            Projectile.scale = targetScale;
            UpdateHitboxToScale();

            Lighting.AddLight(Projectile.Center, 0.9f * progress, 0.05f, 0.05f);

            if (Main.rand.NextBool(4)) {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center + Main.rand.NextVector2Circular(Projectile.width / 2f, Projectile.height / 2f),
                    DustID.Electric,
                    Vector2.Zero
                );
                d.noGravity = true;
                d.color = Color.Red;
                d.scale = 1.2f;
            }

            // Percikan listrik lebih deras makin mendekati puncak charge, biar keliatan "hampir siap"
            if (progress > 0.7f && Main.rand.NextBool(3)) {
                Vector2 sparkVel = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2f, 6f);
                Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch, sparkVel);
                spark.noGravity = true;
                spark.scale = Main.rand.NextFloat(1f, 1.6f);
            }
        }

        private void FlyingBehavior() {
            flightTimer++;

            Lighting.AddLight(Projectile.Center, 0.9f, 0.1f, 0.1f);

            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, -Projectile.velocity * 0.2f);
                d.noGravity = true;
                d.color = Color.Red;
                d.scale = 1.4f;
            }

            int chaseDuration = ExplodeAfterLaunch - FinalPhaseDuration;

            if (flightTimer <= chaseDuration) {
                // ------------ FASE HOMING: ngejar player pelan-pelan, nyamain kecepatannya ------------
                Projectile.rotation += 0.08f;
                HomingChase();
                BounceOffTopOfWorld();
            }
            else {
                // ------------ FASE AKHIR: bola diam total, nunggu Pluto mendekat, baru boleh mengecil ------------
                FinalShrinkPhase();
            }

            // 🛑 [LAST FIX] Bola TIDAK lagi meledak otomatis cuma gara-gara total waktu terbang habis.
            // Sekarang ledakan dipicu dari dalam FinalShrinkPhase() setelah shrinkTimer (yang cuma
            // jalan setelah Pluto mendekat & manggil BeginShrinking()) selesai.
        }

        // 🛑 [LOKASI BALANCING MENTAL DI ATAS WORLD] Bola mental kalau mentok dinding batas paling
        // atas world (velocity.Y dibalik), TAPI tetap nembus block seperti biasa (tileCollide tetap
        // false, default, ga diubah).
        private void BounceOffTopOfWorld() {
            if (Projectile.position.Y <= TopOfWorldBounceY && Projectile.velocity.Y < 0f) {
                Projectile.position.Y = TopOfWorldBounceY;
                Projectile.velocity.Y = Math.Abs(Projectile.velocity.Y);
            }
        }

        private void HomingChase() {
            Player target = FindClosestPlayer();
            if (target == null) return;

            Vector2 currentDir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(currentDir);

            // Homing lambat: arah belok pelan-pelan ngejar player
            Vector2 newDir = Vector2.Lerp(currentDir, toTarget, HomingTurnRate).SafeNormalize(toTarget);

            // Pelan-pelan nyamain kecepatan player, biar ga instan cepat tapi juga ga ketinggalan
            float currentSpeed = Projectile.velocity.Length();
            float playerSpeed = target.velocity.Length();
            float targetSpeed = MathHelper.Clamp(playerSpeed + 3f, HomingMinSpeed, HomingMaxSpeed);
            float newSpeed = MathHelper.Lerp(currentSpeed, targetSpeed, HomingSpeedMatchRate);

            Projectile.velocity = newDir * newSpeed;
        }

        private Player FindClosestPlayer() {
            float closestDist = float.MaxValue;
            Player closest = null;
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (p.active && !p.dead) {
                    float dist = Vector2.DistanceSquared(p.Center, Projectile.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closest = p;
                    }
                }
            }
            return closest;
        }

        private void FinalShrinkPhase() {
            if (!finalPhaseStarted) {
                finalPhaseStarted = true;
                scaleAtFinalPhaseStart = Projectile.scale;
                // 🛑 [LAST FIX] Simpan posisi bola PAS masuk fase akhir -> dari sini bola diem
                // total di situ, ga ditarik-tarik ke Pluto lagi.
                frozenFinalPosition = Projectile.Center;
            }

            // Bola diam TOTAL di posisi terakhirnya (bukan Pluto yang narik bola, tapi Pluto yang
            // harus mendekat ke bola -> lihat ExecuteNovaFinalApproachStage di ElectroNovaDash.cs)
            Projectile.Center = frozenFinalPosition;
            Projectile.velocity = Vector2.Zero;

            if (!shrinkTriggered) {
                // Nunggu Pluto mendekat -> belum boleh mengecil, cuma muter pelan + percikan kecil
                Projectile.rotation += 0.05f;
                if (Main.rand.NextBool(6)) {
                    Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, Main.rand.NextVector2Circular(2f, 2f));
                    spark.noGravity = true;
                    spark.color = Color.Red;
                }
                return;
            }

            // 🛑 Pluto udah cukup deket & manggil BeginShrinking() -> baru dari sini bola beneran
            // mengecil paksa (jitter) + nembak RedBolt, dihitung dari shrinkTimer sendiri (bukan
            // dari total waktu terbang), lalu meledak begitu shrinkTimer-nya abis.
            shrinkTimer++;
            Projectile.rotation += 0.35f; // berputar cepat selagi mengecil

            float phaseProgress = MathHelper.Clamp(shrinkTimer / (float)FinalPhaseDuration, 0f, 1f);

            // Mengecil paksa tapi "cedat-cedut" (jitter, kayak ditekan-tekan bukan mengecil polos)
            float baseShrink = MathHelper.Lerp(scaleAtFinalPhaseStart, 0.05f, phaseProgress);
            float jitterWave = (float)Math.Sin(shrinkTimer * 2.6f) * 0.12f * (1f - phaseProgress * 0.5f);
            float jitterNoise = Main.rand.NextFloat(-0.04f, 0.04f);
            Projectile.scale = MathHelper.Clamp(baseShrink + jitterWave + jitterNoise, 0.05f, scaleAtFinalPhaseStart);
            UpdateHitboxToScale();

            // Percikan listrik lebih deras selama fase mengecil
            if (Main.rand.NextBool(2)) {
                Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch, Main.rand.NextVector2Circular(3f, 3f));
                spark.noGravity = true;
                spark.scale = Main.rand.NextFloat(1f, 1.8f);
            }

            // 🛑 [LOKASI BALANCING TEMBAKAN REDBOLT SAAT MENGECIL] 5x volley, 40 RedBolt tiap volley,
            // nyebar ke segala arah random, sepanjang fase mengecil 3 detik ini
            int volleyInterval = FinalPhaseDuration / RedBoltVolleys;
            if (shrinkTimer > 0 && shrinkTimer % volleyInterval == 0 && volleysFired < RedBoltVolleys) {
                FireRedBoltVolley();
                volleysFired++;
            }

            // Meledak begitu durasi mengecilnya selesai (bukan lagi berdasar total waktu terbang)
            if (shrinkTimer >= FinalPhaseDuration) {
                Explode();
            }
        }

        private void FireRedBoltVolley() {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int i = 0; i < RedBoltsPerVolley; i++) {
                float angle = baseAngle + (MathHelper.TwoPi / RedBoltsPerVolley) * i + Main.rand.NextFloat(-0.05f, 0.05f);
                Vector2 dir = angle.ToRotationVector2();
                Vector2 velocity = dir * Main.rand.NextFloat(6f, 9f);

                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, velocity, ModContent.ProjectileType<RedBolt>(), Projectile.damage, 0f, Main.myPlayer);
            }
        }

        private void ExplodingVisualBehavior() {
            explosionVisualTimer++;

            // 🛑 [LOKASI DAMAGE AURA LEDAKAN] Selama aura "ElectrictBallExplosion" membesar, hitbox
            // bola (Projectile.width/height) ikut dibesarkan biar PAS sama ukuran aura yang lagi
            // keliatan di layar -- jadi kalau player kesenggol aura ini, sistem hit vanilla otomatis
            // ngasih damage yang SAMA kayak damage contact biasa (Projectile.damage) + buff yang sama
            // (lihat OnHitPlayer), karena jalur damage-nya sama persis, cuma hitbox-nya aja yang
            // sekarang segede aura, bukan segede bola yang udah ngecil abis.
            UpdateHitboxToAuraSize();

            // 🛑 [LOKASI SHOCKWAVE] Majuin rambatan gelombang tiap tick selama fase ledakan.
            // progress di-map dari 0..ExplosionVisualDuration jadi -1..2 (mulai dari sedikit
            // sebelum titik nol biar begitu meledak langsung "muncul", bukan ngerayap dari diam).
            // Opacity (kekuatan distorsi) ikut fade out di 40% durasi terakhir biar berhentinya halus.
            // 🛑 [LOKASI SHOCKWAVE - JANGKAUAN GEDE] progress di-map ke range -1..3.5 (sebelumnya
            // cuma -1..2) -- makin gede angka progress yang dicapai, makin JAUH gelombang ini
            // ngerambat sebelum habis, jadi sekarang jangkauannya bisa nutup hampir seluruh layar.
            // Opacity (kekuatan distorsi) juga dinaikin (120 -> 170) biar efeknya lebih berasa.
            float shockProgressT = explosionVisualTimer / (float)ExplosionVisualDuration;
            float shockProgress = MathHelper.Lerp(-1f, 3.5f, MathHelper.Clamp(shockProgressT, 0f, 1f));
            float shockFadeStart = 0.6f;
            float shockOpacity = shockProgressT < shockFadeStart
                ? 170f
                : MathHelper.Lerp(170f, 0f, (shockProgressT - shockFadeStart) / (1f - shockFadeStart));
            ShockwaveSystem.UpdateProgress(shockProgress, shockOpacity);

            // 🛑 [TAMBAHAN BULGE] Envelope 0..1 dihitung terpisah dari progress ripple di atas --
            // naik CEPAT ke puncak (15% durasi awal) biar kerasa "muncul mendadak" kayak kubah
            // kaget, terus turun PELAN sisanya (85%) biar meredanya halus. Opacity puncak dibikin
            // lebih rendah (110) dibanding punya ripple (170) -- bulge ini bonus/aksen visual,
            // bukan efek utama, jadi ga perlu sekuat ripple-nya.
            float bulgeRiseEnd = 0.15f;
            float bulgeEnvelope = shockProgressT < bulgeRiseEnd
                ? MathHelper.Lerp(0f, 1f, shockProgressT / bulgeRiseEnd)
                : MathHelper.Lerp(1f, 0f, (shockProgressT - bulgeRiseEnd) / (1f - bulgeRiseEnd));
            ShockwaveSystem.UpdateBulgeProgress(bulgeEnvelope, 110f);

            if (explosionVisualTimer >= ExplosionVisualDuration) {
                Projectile.Kill();
            }
        }

        // 🛑 [LOKASI DAMAGE AURA LEDAKAN] Ngitung diameter aura ledakan SAAT INI persis pakai rumus
        // yang sama kayak di DrawExplosionAura() (easedProgress, targetDiameter, dst), lalu dipakai
        // buat resize hitbox bola supaya area yang bisa ngenain player itu SAMA PERSIS sama area
        // aura merah yang keliatan di layar -- bukan lagi hitbox mini sisa dari fase mengecil.
        private void UpdateHitboxToAuraSize() {
            Vector2 center = Projectile.Center;

            float progress = MathHelper.Clamp(explosionVisualTimer / ExplosionVisualDuration, 0f, 1f);
            float easedProgress = 1f - (1f - progress) * (1f - progress) * (1f - progress);

            float baseDiameter = ballDiameterAtExplode > 0f ? ballDiameterAtExplode : Projectile.width;
            float targetDiameter = baseDiameter * ExplosionAuraSizeMultiplier;
            float currentDiameter = MathHelper.Lerp(targetDiameter * 0.4f, targetDiameter, easedProgress);

            int newSize = (int)MathHelper.Clamp(currentDiameter, 8f, 20000f);
            Projectile.width = newSize;
            Projectile.height = newSize;
            Projectile.Center = center;
        }

        private void UpdateHitboxToScale() {
            Vector2 center = Projectile.Center;
            int baseSize = 40;
            int newSize = (int)(baseSize * Projectile.scale);
            if (newSize < 8) newSize = 8;
            Projectile.width = newSize;
            Projectile.height = newSize;
            Projectile.Center = center;
        }

        public override bool? CanHitNPC(NPC target) => false;

        // 🛑 [FIX HITBOX vs SHADER GEDE] Sebelumnya area yang bisa ngenain player cuma selebar
        // Projectile.width/height (badan sprite bola polos, 40 * scale) -- padahal cahaya/glow yang
        // KELIATAN di layar (lapisan glow pulse & listrik hitam di PreDraw) jauh lebih lebar, dikali
        // GlowPulseBaseSizeMultiplier (1.6x), bahkan sampai ~1.84x pas lagi pulse maksimal. Makanya
        // keliatan cahayanya gede tapi hitbox-nya kecil.
        //
        // Sengaja TIDAK langsung membesarkan Projectile.width itu sendiri, soalnya field itu juga
        // dipakai buat ngitung banyak hal visual lain di file ini (posisi mottling permukaan, offset
        // petal warna, radius sinar cahaya, sampai peakDiameter yang nentuin besar aura ledakan) --
        // kalau ikut dibesarkan, semua itu bakal ikut membengkak ga proporsional. Solusinya: override
        // deteksi tabrakan di sini pakai radius LINGKARAN yang nyamain persis batas terluar lapisan
        // glow pulse itu, tanpa nyentuh Projectile.width sama sekali.
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            // Fase ledakan (ai[1]==2) udah punya hitbox-nya sendiri yang di-resize PERSIS sebesar
            // aura ledakan lewat UpdateHitboxToAuraSize() (lihat ExplodingVisualBehavior) -- di fase
            // itu biarin pakai deteksi rectangle bawaan Terraria (return null), JANGAN dobel-override.
            if ((int)Projectile.ai[1] == 2) return null;

            // Radius hit disamain sama radius lapisan glow pulse TERLUAR yang keliatan di PreDraw
            // (pulse & GlowPulseBaseSizeMultiplier yang sama persis kayak di sana).
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f) * 0.15f + 1f;
            float hitRadius = (Projectile.width * 0.5f) * GlowPulseBaseSizeMultiplier * pulse;

            Vector2 projCenter = Projectile.Center;
            Vector2 closestPointOnTarget = new Vector2(
                MathHelper.Clamp(projCenter.X, targetHitbox.Left, targetHitbox.Right),
                MathHelper.Clamp(projCenter.Y, targetHitbox.Top, targetHitbox.Bottom));

            return Vector2.DistanceSquared(projCenter, closestPointOnTarget) <= hitRadius * hitRadius;
        }

        // 🛑 [LOKASI DAMAGE AURA LEDAKAN] Sebelumnya CanDamage() langsung false abis meledak, jadi
        // aura "ElectrictBallExplosion" itu cuma visual doang, ga ngasih damage sama sekali kalau
        // player kesenggol. Sekarang: selama masih di fase visual ledakan (ai[1] == 2), damage
        // TETAP AKTIF -- hitbox-nya ikut dibesarkan ngikutin ukuran aura tiap tick (lihat
        // UpdateHitboxToAuraSize di ExplodingVisualBehavior), jadi player yang kesenggol AURA-nya
        // kena Projectile.damage yang SAMA PERSIS kayak damage contact biasa sama bolanya. Begitu
        // fase visual ledakan ini selesai & projectile mau Kill(), damage otomatis mati lagi.
        public override bool? CanDamage() => !hasExploded || (int)Projectile.ai[1] == 2;

        // 🛑 [FINAL FIX] Kena player TIDAK lagi bikin bola meledak instan -> cuma kasih damage +
        // debuff aja. Bola bener-bener cuma meledak kalau waktu hidupnya abis (lihat FlyingBehavior
        // -> dipanggil otomatis pas flightTimer >= ExplodeAfterLaunch).
        // Callback ini sekarang juga dipakai buat kena AURA ledakan (state 2) -- buff yang dikasih
        // tetap sama persis, ElectrictDischarge selama 300 tick, ga dibedain sama kontak bola biasa.
        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), 300);
        }

        private void Explode() {
            if (hasExploded) return;
            hasExploded = true;

            // Simpan diameter bola PAS meledak -> tapi pakai peakDiameter (diameter TERBESAR
            // sepanjang hidup bola), BUKAN Projectile.width saat ini yang udah dipaksa mengecil
            // habis-habisan lewat FinalShrinkPhase. Ini yang bikin aura ledakan akhirnya
            // segede yang seharusnya (10x diameter bola PAS lagi gede-gedenya).
            ballDiameterAtExplode = peakDiameter > 0f ? peakDiameter : Projectile.width;

            // 🛑 [LOKASI BALANCING SFX LEDAKAN] ganti suara ledakan jadi PlutoBallExplosion
            SoundEngine.PlaySound(new SoundStyle("TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/PlutoBallExplosion"), Projectile.Center);

            // LUMINANCE: guncangan layar besar saat meledak, makin gede kalau bolanya makin gede
            // 🛑 [LOKASI SHOCKWAVE/SCREENSHAKE - "TERASA KAYAK SHIELD PILLAR HANCUR"] magnitude &
            // durasi dinaikin (20f->34f, 30->42 tick) biar guncangannya lebih berasa "berat",
            // bukan cuma getar tipis.
            ScreenShakeSystem.StartShake(34f * MathHelper.Clamp(Projectile.scale / 2f, 1f, 2.5f), 42, Vector2.Zero);

            // 🛑 [FIX SHOCKWAVE GAK KERASA] Setelah dicek ulang lewat ShockwaveEffect.fx: uColor.z
            // (rippleSpeed) itu ngontrol SEBERAPA CEPAT pita gelombang nyapu dari pusat ke tepi
            // layar -- BUKAN "seberapa jauh jangkauannya". Tuning sebelumnya (speed 34, size 1.5)
            // ternyata bikin gelombang nyampe TEPI LAYAR cuma dalam ~7 tick (~0.12 detik) dari 55 tick
            // total durasi ledakan -- kedip doang, makanya kerasa kayak ga ada efeknya sama sekali.
            //
            // Fix: rippleSpeed diturunin JAUH (34f -> 11f) biar sapuannya butuh waktu ~70% dari
            // durasi ledakan buat nyampe tepi layar (progress dibutuhin buat nyampe tepi layar
            // = dotFieldMax * rippleSize * PI / rippleSpeed ≈ 4.2*1.8*PI/11 ≈ 2.16, yang berarti
            // baru nyampe tepi di ~70% durasi -- pas masih terang, belum full fade out), jadi
            // gelombangnya kerasa MELEBAR pelan-pelan, bukan kedip sekali terus hilang.
            // rippleSize dijaga tetap kecil (1.8f) biar pita gelombangnya tetep TEBAL (rumus lebar
            // pita = 2*rippleCount/rippleSize -- makin kecil size, makin tebal pita-nya).
            // rippleCount diturunin ke 1.5f (dari 2f) biar berasa 1 gelombang besar dominan
            // (mirip shield pillar hancur), bukan beberapa ring kecil yang saling tumpuk.
            // 🛑 [WARNA SHOCKWAVE] Tint oranye-kemerahan (bukan merah polos) sesuai request --
            // biar shockwave ElectroBall keliatan beda karakter dari PlutoBomb yang merah pekat.
            ShockwaveSystem.Trigger(Projectile.Center, rippleCount: 1.5f, rippleSize: 1.8f, rippleSpeed: 11f, tintColor: new Color(255, 110, 30), tintStrength: 0.4f);

            // 🛑 [TAMBAHAN BULGE] Berdampingan sama ripple di atas -- ripple ngasih kesan
            // "gelombang nyapu keluar", bulge ini nambahin kesan "kubah/lensa cembung" yang
            // nongol sesaat pas titik ledakannya, mirip gaya efek pecahnya shield Celestial
            // Pillar / shockwave Infernum. radius 0.75f = cukup lebar biar kerasa nutupin
            // area sekitar bola pas meledak, warna disamain sama ripple (oranye-kemerahan).
            ShockwaveSystem.TriggerBulge(Projectile.Center, radius: 0.75f, tintColor: new Color(255, 110, 30), tintStrength: 0.5f);

            float explosionRadius = 90f + (150f * Projectile.scale);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player p = Main.player[i];
                    if (p.active && !p.dead && Vector2.Distance(p.Center, Projectile.Center) <= explosionRadius) {
                        Vector2 knockDir = (p.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                        int hitDirection = knockDir.X >= 0 ? 1 : -1;

                        p.Hurt(PlayerDeathReason.ByProjectile(p.whoAmI, Projectile.whoAmI), Projectile.damage, hitDirection);
                        p.AddBuff(ModContent.BuffType<ElectrictDischarge>(), 300);
                        p.velocity += knockDir * 6f;
                    }
                }
            }

            // Ledakan visual: dust listrik + api merah + asap, full vanilla dust IDs
            for (int i = 0; i < 60; i++) {
                Vector2 dustVel = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(4f, 14f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, dustVel);
                d.noGravity = true;
                d.color = Color.Red;
                d.scale = Main.rand.NextFloat(1.4f, 2.2f);
            }
            for (int i = 0; i < 30; i++) {
                Vector2 dustVel = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(2f, 8f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.RedTorch, dustVel);
                d.noGravity = true;
                d.scale = Main.rand.NextFloat(1.2f, 1.8f);
            }
            for (int i = 0; i < 20; i++) {
                Vector2 dustVel = Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(1f, 5f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Smoke, dustVel);
                d.scale = Main.rand.NextFloat(1.5f, 2.5f);
            }

            // Jangan langsung Kill() -> pindah ke state visual "meledak" dulu biar aura
            // ElectrictBallExplosion.png sempat kegambar & membesar sebelum bola bener2 hilang
            Projectile.velocity = Vector2.Zero;
            Projectile.ai[1] = 2f;
            Projectile.netUpdate = true;
        }

        public override void OnKill(int timeLeft) {
            // 🛑 [LOKASI SHOCKWAVE] Pastikan filter ke-deactivate begitu bola bener2 hilang, apapun
            // penyebabnya (meledak normal ATAU owner mati duluan), biar ga nyangkut aktif terus.
            ShockwaveSystem.Stop();
            ShockwaveSystem.StopBulge();

            if (!hasExploded) {
                // Kalau projectile hilang bukan karena ledakan (misal owner mati), tetap kasih sedikit efek
                for (int i = 0; i < 15; i++) {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Electric, Main.rand.NextVector2Circular(4f, 4f));
                    d.noGravity = true;
                    d.color = Color.Red;
                }
            }
        }

        // 🛑 [LOKASI BALANCING WARNA BERDENYUT ARAH-ARAH BOLA] warna yang muter dari kanan/kiri/
        // atas/bawah (dari segala arah), biar bolanya ga polos merah doang.
        private Color GetDirectionalPulseColor(float phaseOffset) {
            float t = Main.GlobalTimeWrappedHourly * 4f + phaseOffset;
            float r = 0.65f + 0.35f * (float)Math.Sin(t);
            float g = 0.05f + 0.12f * (float)Math.Sin(t + MathHelper.PiOver2);
            float b = 0.05f + 0.12f * (float)Math.Sin(t + MathHelper.Pi);
            return new Color(MathHelper.Clamp(r, 0f, 1f), MathHelper.Clamp(g, 0f, 1f), MathHelper.Clamp(b, 0f, 1f));
        }

        // ==================================================================================
        // DRAW: sprite custom "PlutoElectrictBall" + trail additive + trik dye shader vanilla
        // (mirip pendekatan SunInThePalm_EnergyBall.cs, tapi murni pakai API vanilla tModLoader:
        // GameShaders.Armor buat shader dye, dan manual SpriteBatch.Begin/End buat blend Additive)
        // ==================================================================================
        public override bool PreDraw(ref Color lightColor) {
            // State 2 = lagi meledak -> gambar aura ledakan aja, bolanya udah ga digambar lagi
            if ((int)Projectile.ai[1] == 2) {
                DrawExplosionAura();
                return false;
            }

            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Rectangle rect = new Rectangle(0, 0, texture.Width, texture.Height);
            Vector2 origin = new Vector2(texture.Width / 2f, texture.Height / 2f);
            Vector2 screenCenter = Projectile.Center - Main.screenPosition;

            // Ambil shader dye vanilla "Bloodbath" buat kasih efek warna berdenyut/hidup di bola
            int shader = GameShaders.Armor.GetShaderIdFromItemId(ItemID.BloodbathDye);

            // 🛑 [LOKASI TEBAL/OPACITY BOLA] Lapisan dasar SOLID (AlphaBlend, bukan Additive),
            // digambar PALING BAWAH sebelum semua efek glow, alpha PENUH (1f, bukan 0.96f lagi)
            // biar badan bolanya beneran opaque/normal, ga keliatan transparan/nembus background.
            // 🛑 [RENDER ANTI-PIXEL] Sama kayak trik di PlutoPortal.cs: pakai SamplerState.LinearClamp
            // (bukan Main.DefaultSamplerState yang Point/nearest-neighbor) biar pas bola di-scale
            // gede (bisa sampai 5x!), tepi sprite-nya halus, ga keliatan kotak-kotak pixel-nya.
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            Color solidBaseColor = new Color(110, 8, 8) * 1f;
            Main.EntitySpriteDraw(texture, screenCenter, rect, solidBaseColor, Projectile.rotation, origin, Projectile.scale * 1.05f, SpriteEffects.None, 0);

            // 🛑 [FIX OPACITY BOLA UTAMA] Bola inti sekarang digambar di SINI, di layer AlphaBlend
            // (opaque, alpha 1f), BUKAN di layer Additive kayak sebelumnya. Additive itu sifatnya
            // "menambah" cahaya ke background, jadi warnanya kelihatan tipis/menerawang kalau
            // numpuk sama background terang -> sekarang badan merahnya solid & normal opacity-nya,
            // warna tetap MERAH sesuai request, cuma cara gambarnya yang dibenerin.
            Main.EntitySpriteDraw(texture, screenCenter, rect, Color.Red, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            // 🛑 [LOKASI TEKSTUR PERMUKAAN BOLA] Sebelumnya badan bola cuma warna merah solid rata
            // (polos banget, ga ada variasi). Sekarang ditambah bercak-bercak gelap & terang acak
            // (mottled/marble look, mirip permukaan lava/batu panas) yang digambar ulang tiap frame
            // di atas badan bolanya, biar permukaannya keliatan bertekstur & "hidup", bukan flat.
            DrawSurfaceMottling(screenCenter, Projectile.width * 0.5f);

            Main.spriteBatch.End();

            // 🛑 [FIX TEPI BOLA MASIH KELIATAN PIXEL] SamplerState.LinearClamp doang ternyata belum
            // cukup pas bola discale GEDE BANGET (sampe 5x) -- soalnya sprite-nya sendiri resolusinya
            // terbatas, jadi tepi bulatannya tetap keliatan "berundak"/blocky walau udah di-interpolasi.
            // Fix-nya: numpuk beberapa lapis sprite yang SAMA di skala dikit lebih besar & alpha makin
            // tipis (efek soft-glow/blur murah), digambar Additive PERSIS di tepi bola. Ini bikin tepi
            // yang keras/pixelated itu "ketutup"/kesamarin sama gradasi cahaya lembut, jadi mata ga
            // nangkep undakan pixel-nya lagi -- warnanya tetap merah, full dari kode (bukan asset baru).
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            // 🛑 [LOKASI COVER TEPI PIXEL] Lapisan-lapisan ini dirapetin lagi biar konsisten sama
            // shader effect yang sekarang lebih kecil (GlowExtraSizeMultiplier) -- tetap cukup buat
            // nutupin tepi sprite bola yang kaku/pixelated, cuma ga melebar jauh-jauh lagi.
            Main.EntitySpriteDraw(texture, screenCenter, rect, new Color(255, 30, 15) * 0.6f, Projectile.rotation, origin, Projectile.scale * 1.03f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, screenCenter, rect, new Color(255, 40, 20) * 0.48f, Projectile.rotation, origin, Projectile.scale * 1.06f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, screenCenter, rect, new Color(255, 60, 30) * 0.36f, Projectile.rotation, origin, Projectile.scale * 1.08f, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, screenCenter, rect, new Color(255, 100, 50) * 0.20f, Projectile.rotation, origin, Projectile.scale * GlowExtraSizeMultiplier, SpriteEffects.None, 0);
            Main.spriteBatch.End();

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            GameShaders.Armor.ApplySecondary(shader, Main.LocalPlayer, null);

            // Trail dari posisi lama (Projectile.oldPos, otomatis diisi engine sesuai TrailCacheLength)
            // 🛑 [LOKASI TEBAL/OPACITY BOLA] alpha trail dinaikin (0.5f -> 0.65f) biar jejaknya juga
            // ga keliatan tipis-tipis nembus banget.
            for (int k = 0; k < Projectile.oldPos.Length; k++) {
                if (Projectile.oldPos[k] == Vector2.Zero) continue;

                Vector2 drawPos = Projectile.oldPos[k] + Projectile.Size / 2f - Main.screenPosition;
                float trailProgress = (Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length;
                Color trailColor = Color.Red * trailProgress * 0.65f;
                float trailScale = Projectile.scale * (0.7f + 0.3f * trailProgress);
                Main.EntitySpriteDraw(texture, drawPos, rect, trailColor, Projectile.rotation, origin, trailScale, SpriteEffects.None, 0);
            }

            // 🛑 [LOKASI BALANCING PETAL WARNA ARAH] 4 titik cahaya kecil di kanan/kiri/atas/bawah
            // bola, tiap titik warnanya berdenyut beda fase & ikut muter bareng rotasi bola, jadi
            // keliatan kayak warna yang bergeser dari segala arah (ga polos merah statis doang).
            // Alpha dinaikin (0.55f -> 0.7f) biar makin "berisi", ga tipis.
            float petalOffset = Projectile.width * 0.32f;
            Vector2[] petalDirs = { -Vector2.UnitY, Vector2.UnitX, Vector2.UnitY, -Vector2.UnitX };
            for (int p = 0; p < petalDirs.Length; p++) {
                Vector2 rotatedDir = petalDirs[p].RotatedBy(Projectile.rotation);
                Vector2 petalPos = Projectile.Center + rotatedDir * petalOffset - Main.screenPosition;
                Color petalColor = GetDirectionalPulseColor(p * MathHelper.PiOver2) * 0.7f;
                Main.EntitySpriteDraw(texture, petalPos, rect, petalColor, Projectile.rotation, origin, Projectile.scale * 0.5f, SpriteEffects.None, 0);
            }

            // 🛑 [LOKASI EFEK SOROTAN CAHAYA] Beam/sorotan yang menjulur keluar dari bola, muter
            // pelan -- ini BUKAN glow bulat biasa, tapi garis-garis cahaya tirus yang nyorot kayak
            // lampu sorot. Masih di batch Additive yang sama kayak trail/petal di atas (ga perlu
            // End/Begin lagi, sama-sama Additive), digambar SEBELUM listrik hitam biar listriknya
            // bisa numpuk di ATAS ini semua (lihat catatan urutan layer di bawah).
            DrawLightRayBeams(screenCenter, Projectile.width * 0.5f * LightRayExtraSizeMultiplier, Main.GlobalTimeWrappedHourly * 0.6f);

            // 🛑 [LOKASI GLOW PULSE "CEDAT-CEDUT"] Lapisan "flare" yang membesar-mengecil (pulse)
            // ini SEKARANG punya base size sendiri (GlowPulseBaseSizeMultiplier) yang SENGAJA dibuat
            // lebih gede daripada bola itu sendiri MAUPUN semua visual lain (shader arc listrik &
            // sinar) -- biar ada satu lapisan glow besar yang jadi "napas"/denyut utama di sekeliling
            // bola, sementara shader effect (arc) & sinar tetap di ukuran yang lebih kalem/kecil.
            float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 10f) * 0.15f + 1f;
            Color flareColor = GetDirectionalPulseColor(0f) * 0.7f;
            Main.EntitySpriteDraw(texture, screenCenter, rect, flareColor, Projectile.rotation * 1.6f, origin, Projectile.scale * pulse * GlowPulseBaseSizeMultiplier, SpriteEffects.None, 0);
            Main.EntitySpriteDraw(texture, screenCenter, rect, flareColor, -Projectile.rotation * 1.6f, origin, Projectile.scale * pulse * GlowPulseBaseSizeMultiplier, SpriteEffects.None, 0);

            // Kilau tengah putih-kemerahan (inti paling terang)
            Main.EntitySpriteDraw(texture, screenCenter, rect, Color.White * 0.6f, Projectile.rotation, origin, Projectile.scale * 0.5f, SpriteEffects.None, 0);

            Main.spriteBatch.End();

            // 🛑 [LOKASI VISUAL LISTRIK HITAM] Sekarang digambar PALING TERAKHIR/PALING ATAS (di atas
            // sinar & glow pulse yang barusan), bukan lagi di tengah-tengah urutan -- biar listriknya
            // KELIATAN JELAS numpuk di atas semua efek cahaya lain, ga ketutup/ketiban. Tetap pakai
            // AlphaBlend (BUKAN Additive) soalnya warna hitam nambah nilai (0,0,0) ke framebuffer
            // kalau di-additive-in, jadi ga bakal kelihatan sama sekali.
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            // Projectile.width udah otomatis ikut scale (lihat UpdateHitboxToScale). 🛑 [LOKASI
            // LEBAR LISTRIK] Radius arc-nya sekarang dikali GlowPulseBaseSizeMultiplier (bukan lagi
            // GlowExtraSizeMultiplier yang kecil) -- jadi listriknya menjangkau SELEBAR lapisan
            // glow pulse paling luar, ga keliatan kecil menciut di tengah doang.
            DrawBlackElectricSurface(screenCenter, Projectile.width * 0.5f * GlowPulseBaseSizeMultiplier);
            Main.spriteBatch.End();

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }

        // =========================================================================
        // 🛑 [LOKASI TEKSTUR PERMUKAAN BOLA] Badan bola sebelumnya cuma ke-tint warna solid rata
        // (Color.Red polos), jadi keliatan flat/polos banget walau ukurannya udah gede. Method ini
        // nambahin bercak-bercak gelap & terang acak (mottled, mirip retakan batu panas/lava) di
        // atas permukaannya, digambar ULANG tiap frame dengan posisi acak baru biar keliatan
        // "hidup"/berpijar ga rata -- murni pakai TextureAssets.MagicPixel (vanilla), ga butuh
        // asset/sprite baru sama sekali.
        // =========================================================================
        private void DrawSurfaceMottling(Vector2 screenCenter, float radius) {
            if (radius < 4f) return; // bola kekecilan (fase mengecil), skip biar ga aneh

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle pixelRect = new Rectangle(0, 0, 1, 1);

            const int blotchCount = 14;
            for (int i = 0; i < blotchCount; i++) {
                // Bercak disebar acak dalam 75% radius bola, biar ga nongol keluar dari tepi bulatnya.
                Vector2 offset = Main.rand.NextVector2Circular(radius * 0.75f, radius * 0.75f);
                Vector2 pos = screenCenter + offset;
                float size = Main.rand.NextFloat(radius * 0.18f, radius * 0.4f);

                // Separuh bercak gelap (kayak bekas gosong/retak), separuh lagi terang (kayak inti
                // panas yang nyembul) -- kombinasi ini yang bikin permukaannya kerasa ga rata/polos.
                bool darker = Main.rand.NextBool();
                Color blotchColor = darker
                    ? new Color(60, 2, 2) * 0.4f
                    : new Color(255, 90, 40) * 0.28f;

                float rot = Main.rand.NextFloat(MathHelper.TwoPi);
                Main.EntitySpriteDraw(pixel, pos, pixelRect, blotchColor, rot, new Vector2(0.5f, 0.5f), new Vector2(size, size * 0.65f), SpriteEffects.None, 0);
            }
        }

        // =========================================================================
        // 🛑 [LOKASI VISUAL LISTRIK HITAM] Gambar beberapa "arc" listrik hitam berbentuk garis
        // patah-patah (bukan garis lurus polos) yang menjalar dari tepi ke tengah permukaan bola.
        // Digambar ulang tiap frame dengan titik acak baru -> hasilnya flicker/berkedip kayak
        // listrik asli, bukan pola statis yang keliatan aneh kalau diperhatiin lama.
        // =========================================================================
        private void DrawBlackElectricSurface(Vector2 screenCenter, float radius) {
            if (radius < 4f) return; // bola udah kekecilan (fase mengecil), ga usah gambar arc lagi

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle pixelRect = new Rectangle(0, 0, 1, 1);

            // 🛑 [LOKASI ANTI-SPAM LISTRIK] arcCount & segmentsPerArc diturunin lagi (4->3, 5->4) --
            // makin sedikit & makin tenang, cuma beberapa arc TEBAL & jelas, bukan rame kayak cacing.
            const int arcCount = 3;
            const int segmentsPerArc = 4;

            for (int a = 0; a < arcCount; a++) {
                float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                // Titik awal di radius PENUH shader effect biar mulai persis di tepi terluarnya.
                Vector2 point = screenCenter + baseAngle.ToRotationVector2() * radius;

                for (int s = 0; s < segmentsPerArc; s++) {
                    float travel = (s + 1) / (float)segmentsPerArc;
                    // Target radius masih menyebar dari tepi ke ~60% radius (BUKAN nyusut ke 0),
                    // jadi arc-nya tetap ngelilingin lebar shader effect. 🛑 [LOKASI ANTI-SPAM
                    // LISTRIK] Sudut & jitter dirapetin lagi (0.9->0.5, 0.16x->0.10x radius) biar
                    // jalurnya makin tenang/rapi, ga zig-zag acak kayak cacing lagi.
                    float radiusFactor = MathHelper.Lerp(1f, 0.6f, travel);
                    Vector2 targetOnRadius = screenCenter + (baseAngle + Main.rand.NextFloat(-0.5f, 0.5f)).ToRotationVector2() * radius * radiusFactor;
                    Vector2 jitter = Main.rand.NextVector2Circular(radius * 0.10f, radius * 0.10f);
                    Vector2 nextPoint = targetOnRadius + jitter;

                    Vector2 diff = nextPoint - point;
                    float length = diff.Length();
                    if (length > 0.5f) {
                        float segRotation = diff.ToRotation();
                        // 🛑 [LOKASI TEBAL SHADER] Ketebalan dinaikin dikit lagi (6.5/3f -> 7.5/3.5f)
                        // biar walau jumlah arc-nya makin dikit, tetap keliatan tebal/jelas (bukan tipis).
                        float thickness = MathHelper.Lerp(7.5f, 3.5f, travel);
                        // Warna dicampur sedikit merah gelap (bukan hitam pekat 0,0,0) biar tetep
                        // kebaca sebagai "listrik hitam" tapi lebih merah/nyatu sama tema bolanya.
                        Color blackElectricColor = new Color(55, 0, 0) * 0.9f;
                        Main.EntitySpriteDraw(pixel, point, pixelRect, blackElectricColor, segRotation, new Vector2(0f, 0.5f), new Vector2(length, thickness), SpriteEffects.None, 0);
                    }
                    point = nextPoint;
                }
            }
        }

        // =========================================================================
        // 🛑 [LOKASI EFEK SOROTAN CAHAYA] Beam cahaya yang menjulur keluar dari pusat bola dan
        // muter pelan, mirip gaya "godray"/light-beam yang sering dipakai boss-boss di mod besar
        // (Calamity Exo Mechs, Wrath of the Gods, Starlight River, dll) -- bedanya sama glow biasa:
        // ini garis TIRUS (makin jauh dari pusat makin tipis & transparan) yang nyorot ke arah
        // tertentu, bukan lingkaran cahaya bulat polos. Diimplementasi murni pakai TextureAssets.
        // MagicPixel (vanilla), dipecah jadi beberapa segmen per beam biar gradasi tirusnya halus.
        // =========================================================================
        private void DrawLightRayBeams(Vector2 screenCenter, float radius, float rotationBase) {
            if (radius < 4f) return; // bola kekecilan (fase mengecil), skip biar ga aneh

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle pixelRect = new Rectangle(0, 0, 1, 1);

            const int beamCount = 10;
            const int segments = 8;
            // 🛑 [LOKASI BALANCING PANJANG SOROTAN] beam menjulur cukup jauh (4.5x radius bola)
            // biar keliatan kayak "sorotan lampu", bukan cuma nempel di permukaan bola doang
            float beamLength = radius * 4.5f;

            for (int i = 0; i < beamCount; i++) {
                float angle = rotationBase + (MathHelper.TwoPi / beamCount) * i;

                // Variasi panjang & fase tiap beam biar hidup (ga statis/robotik semua sama panjang)
                float lengthJitter = 0.65f + 0.45f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.8f + i * 1.7f);
                float thisLength = beamLength * lengthJitter;
                // 🛑 [LOKASI VISUAL SINAR] Ketebalan dasar dinaikin (5.5/2.5 -> 8.5/3.5) biar
                // sorotannya makin kentara/tebal, ga cuma garis tipis.
                float baseThickness = 8.5f + 3.5f * (float)Math.Sin(i * 2.1f);

                Vector2 dir = angle.ToRotationVector2();

                for (int s = 0; s < segments; s++) {
                    float t0 = s / (float)segments;
                    float t1 = (s + 1) / (float)segments;

                    Vector2 segStart = screenCenter + dir * (thisLength * t0);
                    Vector2 segEnd = screenCenter + dir * (thisLength * t1);

                    // Alpha & ketebalan tirus: paling kuat & tebal dekat pusat, menghilang di ujung
                    float taper = (1f - t0) * (1f - t0);
                    float segThickness = MathHelper.Lerp(baseThickness, 0.4f, t0);

                    // 🛑 [LOKASI VISUAL SINAR] Alpha dinaikin (0.55f -> 0.75f) biar sorotannya lebih
                    // kelihatan/menyala, ga tenggelam sama background.
                    Color beamColor = Color.Lerp(Color.White, Color.Red, 0.4f) * taper * 0.75f;

                    Vector2 diff = segEnd - segStart;
                    float segLength = diff.Length();
                    if (segLength <= 0.5f) continue;

                    float segRotation = diff.ToRotation();
                    Main.EntitySpriteDraw(pixel, segStart, pixelRect, beamColor, segRotation, new Vector2(0f, 0.5f), new Vector2(segLength + 1f, segThickness), SpriteEffects.None, 0);
                }
            }
        }

        // =========================================================================
        // 🛑 [LOKASI BALANCING AURA LEDAKAN] pakai sprite "ElectrictBallExplosion.png" (default
        // putih polos -> di-tint MERAH SOLID lewat drawColor), membesar dari titik tengah bola
        // sampai 20 block lebih lebar dari bola, sambil berputar (2 lapis berlawanan arah).
        // =========================================================================
        private void DrawExplosionAura() {
            // 🛑 [FIX LEDAKAN GA MUNCUL] Jaga-jaga kalau field static-nya somehow null/belum
            // ke-load (misal load order aneh) -> paksa load ulang sync di sini juga, jadi ga
            // pernah gagal gambar gara-gara texture kosong.
            if (ExplosionAuraTexture == null || !ExplosionAuraTexture.IsLoaded) {
                ExplosionAuraTexture = ModContent.Request<Texture2D>(
                    "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/ElectrictBallExplosion",
                    AssetRequestMode.ImmediateLoad
                );
            }

            Texture2D auraTexture = ExplosionAuraTexture.Value;
            Rectangle rect = new Rectangle(0, 0, auraTexture.Width, auraTexture.Height);
            Vector2 origin = new Vector2(auraTexture.Width / 2f, auraTexture.Height / 2f);

            float progress = MathHelper.Clamp(explosionVisualTimer / ExplosionVisualDuration, 0f, 1f);
            // Ease-out CEPAT: langsung kelihatan gede dari frame pertama, bukan ngerayap dari nyaris nol
            float easedProgress = 1f - (1f - progress) * (1f - progress) * (1f - progress);

            // Aura membesar dari titik tengah bola sampai diameternya 10x lipat diameter bola
            // itu sendiri PAS dia meledak -- GEDE BANGET sesuai request
            float baseDiameter = ballDiameterAtExplode > 0f ? ballDiameterAtExplode : Projectile.width;
            float targetDiameter = baseDiameter * ExplosionAuraSizeMultiplier;
            // 🛑 Mulai dari 40% ukuran akhir (bukan 10%) biar langsung keliatan begitu meledak
            float auraScale = MathHelper.Lerp(targetDiameter / auraTexture.Width * 0.4f, targetDiameter / auraTexture.Width, easedProgress);

            // Aura berputar selagi melebar, bukan polos melebar aja
            float auraRotation = progress * MathHelper.TwoPi * 1.5f;

            // 🛑 Warna ORANYE-KEMERAHAN & terang (sprite originalnya putih polos, jadi warna akhir
            // sepenuhnya ditentukan drawColor ini). Tetap full terang di ~70% awal durasi,
            // baru fade out di akhir, biar ga langsung pudar dari awal. Dulu merah pekat penuh
            // (255,0,0), sekarang dikasih campuran hijau dikit (255,110,30) biar kerasa oranye
            // membara, bukan merah darah polos.
            float fadeStart = 0.7f;
            float alpha = progress < fadeStart ? 1f : 1f - ((progress - fadeStart) / (1f - fadeStart));
            Color auraColor = new Color(255, 110, 30) * alpha;

            Main.spriteBatch.End();
            // 🛑 [RENDER ANTI-PIXEL] Sama kayak PlutoPortal.cs: pakai SamplerState.LinearClamp,
            // soalnya aura ini bisa membesar sampai 10x diameter bola -- kalau masih pakai sampler
            // Point/nearest-neighbor default, bakal keliatan kotak-kotak pixel banget pas segede itu.
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // Lapisan pertama muter searah jarum jam
            Main.EntitySpriteDraw(auraTexture, Projectile.Center - Main.screenPosition, rect, auraColor, auraRotation, origin, auraScale, SpriteEffects.None, 0);
            // Lapisan kedua muter berlawanan arah & sedikit lebih kecil, biar keliatan hidup
            Main.EntitySpriteDraw(auraTexture, Projectile.Center - Main.screenPosition, rect, auraColor * 0.6f, -auraRotation * 1.3f, origin, auraScale * 0.85f, SpriteEffects.None, 0);
            // 🛑 [LOKASI TEBAL SHADER] Lapisan ketiga: inti tengah, tetap oranye-kemerahan (bukan
            // putih lagi) biar keseluruhan ledakan tetap dominan oranye, cuma sedikit lebih
            // terang & lebih kuning di intinya (efek "inti panas")
            Main.EntitySpriteDraw(auraTexture, Projectile.Center - Main.screenPosition, rect, new Color(255, 150, 60) * alpha * 0.7f, auraRotation * 0.7f, origin, auraScale * 0.5f, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}
