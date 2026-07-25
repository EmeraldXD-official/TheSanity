using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics; // Luminance ScreenShakeSystem
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile;
using System;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    public partial class PlutoHead
    {
        // ==================================================================================
        // PATTERN 4: ELECTRO NOVA
        // Stage 0 (Approach) : Pluto mendekati player yang dia agro dulu sampai jarak tertentu
        //                      (atau timeout), sambil menghadap ke arah gerakannya.
        // Stage 1 (Rise)     : Setelah cukup dekat, Pluto naik sedikit ke atas sambil
        //                      perlahan menghadap lurus ke atas, bersiap nge-charge.
        // Stage 2 (Charge)   : Pluto diam total menghadap atas, memunculkan bola listrik merah
        //                      di atas kepalanya. BOLA-nya (bukan Pluto) yang membesar sampai
        //                      5x ukuran Pluto selama kurang lebih 5 detik (lihat PlutoElectroBall.cs).
        // Stage 3 (Push)     : Pluto "mendorong" bola itu (lunge ke atas) lalu melempar bola
        //                      ke arah player (prediktif) dan meninggalkannya.
        // Stage 4 (Chase Dash): Selama bola masih hidup & masih homing (belum masuk fase akhir),
        //                      Pluto dash berulang ke arah player dengan sudut miring searah gerak
        //                      bola (atas/bawah sama kayak bola), lalu dash lagi tiap kali bola
        //                      udah sejajar / ninggalin dia. Begitu bola masuk fase akhir (balik
        //                      melayang di atas kepala Pluto & mengecil paksa sebelum meledak),
        //                      Pluto berhenti dash, diam di tempat & noleh ke atas lagi (posisi
        //                      sama kayak pas nge-charge) sampai bolanya meledak/hilang.
        // Stage 5 (Recover)  : Pluto pulih sebentar, lalu reset ke NPC.ai[0] = 0 supaya
        //                      PlutoHead.AI() otomatis gacha pattern lain lagi.
        //
        // Bola sendiri (PlutoElectroBall.cs) yang menangani logic charge/terbang/ledakan-nya,
        // supaya file ini cuma fokus ke gerakan & timing si Pluto.
        // ==================================================================================
        // 🛑 [LOKASI BALANCING KECEPATAN APPROACH] Dinaikin lagi jauh (7f -> 24f) -- sekarang Pluto
        // BERGERAK CEPET ngedeketin player sebelum charge, bukan jalan santai lagi.
        private const float NovaApproachSpeed = 24f;
        // 🛑 [LOKASI BALANCING JARAK CHARGE] Naik dari ~10 block ke ~25 block (1 block = 16px ->
        // 400px). Ini tetap REQUIRED: Pluto TIDAK akan mulai Charging sebelum bener-bener masuk
        // jarak ini ke player.
        private const float NovaApproachStopDistance = 400f;
        // 🛑 [LOKASI REQUIRED JARAK CHARGE] Sebelumnya ada "timedOut" yang bisa maksa Pluto lanjut ke
        // Charge walau belum nyampe jarak 10 block (cuma modal nunggu 2 detik). Sekarang jarak itu
        // jadi REQUIREMENT beneran -- timeout ini dinaikin jauh (120 -> 1800 tick / 30 detik) dan
        // cuma jadi jaring pengaman anti-softlock (misal player kabur/nyangkut di tempat aneh),
        // BUKAN cara normal buat skip syarat jaraknya.
        private const int NovaApproachMaxTime = 1800;

        private const int NovaRiseTime = 35;               // durasi naik & menghadap ke atas

        // 🛑 [LOKASI BALANCING DURASI CHARGE] -> dinaikin jadi 5 detik (HARUS SAMA dengan
        // PlutoElectroBall.ChargeTime biar timing bola & Pluto sinkron)
        private const int NovaChargeTime = 300;

        private const int NovaPushTime = 20;                // durasi lunge/dorongan

        // 🛑 [LOKASI BALANCING DASH KEJAR SAAT BOLA TERBANG]
        private const float NovaChaseDashSpeed = 22f;       // kecepatan dash Pluto ngejar player
        private const int NovaChaseDashDuration = 20;       // durasi tiap 1x dash burst
        private const float NovaChaseDashAlignThreshold = 70f; // jarak horizontal dianggap "sejajar" dgn bola

        private const int NovaRecoverTime = 25;             // jeda sebelum ganti pattern baru

        // 🛑 [LOKASI BALANCING DAMAGE REDUCTION SAAT CHARGE] Selama Stage 2 (Charge) -- pas bola lagi
        // membesar di atas kepala Pluto -- Pluto (Head, Body, DAN Tail) dapet damage reduction gede:
        // dikurangin 90% dari SEMUA jenis damage. Tujuannya biar charge beneran kerasa kayak "jendela
        // rentan" yang butuh usaha buat dimanfaatin, bukan malah jadi kesempatan combo damage gede-gedean
        // pas Pluto lagi diem total nge-charge.
        public const float ElectroNovaChargeDamageReduction = 0.9f;

        // Dipakai PlutoBody/PlutoTail (lewat referensi ke NPC Head-nya) buat tau kapan reduction ini
        // lagi aktif -- true kalau Pluto lagi di Pattern 4 (Electro Nova) Stage 2 (Charge).
        public bool IsElectroNovaCharging => (int)NPC.ai[0] == 4 && (int)NPC.ai[1] == 2;

        // 🛑 [LAST FIX] Begitu bola masuk fase akhir, dia diam total di tempat (BUKAN ditarik ke
        // Pluto). Sekarang Pluto sendiri yang harus mendekat ke bola, dan begitu udah cukup deket,
        // baru bola boleh mulai mengecil (lihat PlutoElectroBall.BeginShrinking()).
        private const float NovaFinalApproachSpeed = 14f;       // kecepatan Pluto mendekati bola yang diam
        // 🛑 [LOKASI BALANCING JARAK TRIGGER MENGECIL] seberapa deket Pluto harus sampai ke bola
        private const float NovaFinalApproachStopDistance = 70f;

        private void ExecuteElectroNovaPattern(Player player) {
            int stage = (int)NPC.ai[1];
            int timer = (int)NPC.ai[2];

            if (stage == 0) {
                ExecuteNovaApproachStage(player, timer);
            }
            else if (stage == 1) {
                ExecuteNovaRiseStage(timer);
            }
            else if (stage == 2) {
                ExecuteNovaChargeStage(timer);
            }
            else if (stage == 3) {
                ExecuteNovaPushStage(player, timer);
            }
            else if (stage == 4) {
                ExecuteNovaChaseDashStage(player, timer);
            }
            else if (stage == 5) {
                ExecuteNovaRecoverStage(timer);
            }
        }

        private void ExecuteNovaApproachStage(Player player, int timer) {
            Vector2 dirToPlayer = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
            float distanceToPlayer = Vector2.Distance(NPC.Center, player.Center);

            if (distanceToPlayer > NovaApproachStopDistance) {
                NPC.velocity = Vector2.Lerp(NPC.velocity, dirToPlayer * NovaApproachSpeed, 0.06f);
                if (dirToPlayer != Vector2.Zero) {
                    NPC.rotation = NPC.rotation.AngleLerp(dirToPlayer.ToRotation(), 0.1f);
                }
            }
            else {
                NPC.velocity *= 0.9f;
            }

            timer++;
            bool reachedPlayer = distanceToPlayer <= NovaApproachStopDistance;
            bool timedOut = timer >= NovaApproachMaxTime;

            if (reachedPlayer || timedOut) {
                NPC.ai[1] = 1f;
                NPC.ai[2] = 0f;
                NPC.netUpdate = true;
            }
            else {
                NPC.ai[2] = timer;
            }
        }

        private void ExecuteNovaRiseStage(int timer) {
            // Naik pelan ke atas sambil ngerem gerak horizontal & mulai menghadap ke atas
            NPC.velocity = new Vector2(NPC.velocity.X * 0.9f, -9f);
            NPC.rotation = NPC.rotation.AngleLerp(-MathHelper.PiOver2, 0.12f);

            timer++;
            if (timer >= NovaRiseTime) {
                NPC.ai[1] = 2f;
                NPC.ai[2] = 0f;
                NPC.netUpdate = true;
            }
            else {
                NPC.ai[2] = timer;
            }
        }

        private void ExecuteNovaChargeStage(int timer) {
            // Pluto berhenti total mengambang dan menghadap lurus ke atas
            NPC.velocity *= 0.9f;
            NPC.rotation = NPC.rotation.AngleLerp(-MathHelper.PiOver2, 0.15f);

            if (timer == 0) {
                Vector2 spawnPos = NPC.Center + new Vector2(0f, -(NPC.height * NPC.scale) - 40f);

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    spawnPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<PlutoElectroBall>(),
                    NPC.damage,
                    0f,
                    Main.myPlayer,
                    NPC.whoAmI, // ai[0] = pemilik (index PlutoHead ini)
                    0f          // ai[1] = state 0 -> charging
                );

                SoundEngine.PlaySound(SoundID.Item29, NPC.Center); // desis charge (vanilla)
                NPC.netUpdate = true;
            }

            // Catatan: Pluto TIDAK ikut membesar di sini -- yang membesar cuma bolanya
            // (PlutoElectroBall.cs, lewat MaxScaleMultiplier). Progress dipakai buat efek layar aja.
            float chargeScaleProgress = MathHelper.Clamp(timer / (float)NovaChargeTime, 0f, 1f);

            // Guncangan layar kecil berkala saat bola membesar, makin kuat mendekati puncak charge
            if (timer > 0 && timer % 15 == 0) {
                float chargeProgress = timer / (float)NovaChargeTime;
                ScreenShakeSystem.StartShake(2f + chargeProgress * 7f, 10, Vector2.Zero);

                Vector2 ballPos = NPC.Center + new Vector2(0f, -(NPC.height * NPC.scale) - 40f - (chargeProgress * 160f));
                for (int i = 0; i < 6; i++) {
                    Dust d = Dust.NewDustPerfect(ballPos, DustID.Electric, Main.rand.NextVector2Circular(3f, 3f));
                    d.noGravity = true;
                    d.color = Color.Red;
                }
            }

            timer++;
            if (timer >= NovaChargeTime) {
                NPC.ai[1] = 3f;
                NPC.ai[2] = 0f;
                NPC.netUpdate = true;
            }
            else {
                NPC.ai[2] = timer;
            }
        }

        private void ExecuteNovaPushStage(Player player, int timer) {
            if (timer == 0) {
                // Cari bola listrik yang lagi di-charge milik Pluto ini, lalu luncurkan
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile proj = Main.projectile[i];
                    if (proj.active && proj.type == ModContent.ProjectileType<PlutoElectroBall>() &&
                        (int)proj.ai[0] == NPC.whoAmI && (int)proj.ai[1] == 0) {

                        Vector2 predictedPos = player.Center + player.velocity * 12f;
                        Vector2 launchDir = (predictedPos - proj.Center).SafeNormalize(-Vector2.UnitY);

                        proj.velocity = launchDir * 15f;
                        proj.ai[1] = 1f; // state 1 -> meluncur & bisa meledak
                        proj.netUpdate = true;
                        break;
                    }
                }

                // Pluto melakukan gerakan "mendorong" bola ke atas
                NPC.velocity = new Vector2(NPC.velocity.X, -13f);
                ScreenShakeSystem.StartShake(9f, 18, -Vector2.UnitY);
                SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{Main.rand.Next(1, 3)}"), NPC.Center);
                NPC.netUpdate = true;
            }

            NPC.velocity *= 0.9f;

            timer++;
            if (timer >= NovaPushTime) {
                // Lanjut ke Stage 4: Pluto dash-dash ngejar player selama bolanya masih hidup
                NPC.ai[1] = 4f;
                NPC.ai[2] = 0f;
                NPC.ai[3] = 0f; // 0 = belum dash / lagi nunggu, 1 = lagi dash
                NPC.netUpdate = true;
            }
            else {
                NPC.ai[2] = timer;
            }
        }

        // =========================================================================
        // STAGE 4: CHASE DASH
        // Selama PlutoElectroBall masih ada, Pluto berulang kali dash ke arah player
        // dengan sudut miring (atas/bawah) searah gerak si bola. Begitu bola sejajar
        // dengannya atau sudah ninggalin dia, dia dash lagi. Kalau bolanya udah meledak
        // / hilang, langsung lanjut ke Recover.
        // =========================================================================
        private void ExecuteNovaChaseDashStage(Player player, int timer) {
            Projectile ball = FindOwnedElectroBall();

            if (ball == null) {
                // Bola sudah meledak/hilang -> Pluto boleh pulih & ganti pattern lain
                NPC.ai[1] = 5f;
                NPC.ai[2] = 0f;
                NPC.ai[3] = 0f;
                NPC.netUpdate = true;
                return;
            }

            // 🛑 [FINAL FIX] Bola udah masuk fase akhir (balik melayang di atas kepala Pluto,
            // berputar & mengecil paksa sebelum meledak) -> Pluto berhenti dash, diam di tempat
            // & noleh ke atas lagi (sama kayak posisi pas charge), BUKAN muter ngelilingin bola.
            if (ball.ModProjectile is PlutoElectroBall electroBall && electroBall.IsInFinalPhase) {
                ExecuteNovaFinalApproachStage(ball, electroBall);
                return;
            }

            bool isDashing = NPC.ai[3] == 1f;

            if (!isDashing) {
                NPC.velocity *= 0.92f;

                bool ballLevelOrLeftHim = IsBallLevelOrPassed(ball);

                // Dash pertama kali langsung (timer == 0), atau tiap kali bola udah sejajar/ninggalin dia
                if (timer == 0 || ballLevelOrLeftHim) {
                    Vector2 dirToPlayer = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitX);

                    // 🛑 [LOKASI BALANCING KEMIRINGAN DASH] miring atas/bawah searah gerak si bola
                    float verticalSign = ball.velocity.Y != 0f ? Math.Sign(ball.velocity.Y) : Math.Sign(dirToPlayer.Y);
                    if (verticalSign == 0) verticalSign = 1;

                    Vector2 dashDir = new Vector2(dirToPlayer.X, Math.Abs(dirToPlayer.Y) * verticalSign).SafeNormalize(dirToPlayer);

                    NPC.velocity = dashDir * NovaChaseDashSpeed;
                    NPC.rotation = dashDir.ToRotation();

                    NPC.ai[3] = 1f;
                    timer = 0;

                    SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{Main.rand.Next(1, 3)}"), NPC.Center);
                    NPC.netUpdate = true;
                }
            }
            else {
                NPC.velocity *= 0.97f;

                timer++;
                if (timer >= NovaChaseDashDuration) {
                    NPC.ai[3] = 0f; // selesai dash, nunggu bola sejajar/ninggalin dia lagi
                    timer = 0;
                    NPC.netUpdate = true;
                }
            }

            NPC.ai[2] = timer;
        }

        // 🛑 [LAST FIX] Bola sekarang diam total di posisi terakhirnya begitu masuk fase akhir
        // (bukan ditarik ke Pluto lagi). Jadi Pluto sendiri yang harus terbang mendekati bola itu.
        // Begitu udah cukup deket, panggil BeginShrinking() -> baru dari situ bolanya boleh mulai
        // mengecil & meledak. Kalau belum deket, bola nunggu terus (ga bakal mengecil).
        // 🛑 [LOKASI BALANCING JARAK "DEPAN" PLUTO] seberapa jauh titik charge-nya nempel di depan
        // badan Pluto (bukan di tengah badannya). Disesuaikan kira-kira setengah lebar hitbox Pluto.
        private const float NovaFrontChargeOffset = 34f;

        private void ExecuteNovaFinalApproachStage(Projectile ball, PlutoElectroBall electroBall) {
            Vector2 dirToBall = (ball.Center - NPC.Center).SafeNormalize(Vector2.Zero);
            float distanceToBall = Vector2.Distance(NPC.Center, ball.Center);

            // 🛑 [FIX] Titik charge sekarang nempel di BAGIAN DEPAN Pluto (sisi yang ngadep ke
            // bola), BUKAN di tengah badannya -> ikut gerak & muter bareng Pluto tiap frame lewat
            // dirToBall, jadi keliatan kek bagian depannya yang lagi "nyambungin diri" ke bolanya.
            Vector2 frontChargePos = NPC.Center + dirToBall * ((NPC.width * 0.5f * NPC.scale) + NovaFrontChargeOffset);

            if (distanceToBall > NovaFinalApproachStopDistance) {
                NPC.velocity = Vector2.Lerp(NPC.velocity, dirToBall * NovaFinalApproachSpeed, 0.08f);
                if (dirToBall != Vector2.Zero) {
                    NPC.rotation = NPC.rotation.AngleLerp(dirToBall.ToRotation(), 0.12f);
                }

                // Makin deket Pluto ke bola, makin deras percikan listrik di bagian depannya --
                // kesannya kayak lagi "ngecas" nyambung ke bola, bukan seluruh badan yang nyala.
                float approachProgress = 1f - MathHelper.Clamp((distanceToBall - NovaFinalApproachStopDistance) / 260f, 0f, 1f);
                if (Main.rand.NextFloat() < 0.12f + approachProgress * 0.45f) {
                    Dust d = Dust.NewDustPerfect(frontChargePos, DustID.Electric, Main.rand.NextVector2Circular(2.5f, 2.5f));
                    d.noGravity = true;
                    d.color = Color.Red;
                    d.scale = 0.9f + approachProgress * 0.7f;
                }
                Lighting.AddLight(frontChargePos, 0.55f * approachProgress, 0.05f, 0.05f);
            }
            else {
                // Udah cukup deket -> berhenti di situ & picu bolanya buat mulai mengecil
                NPC.velocity *= 0.9f;

                // Percikan "connect" sekali doang, pas bagian depan Pluto pertama kali nyampe deket
                // bolanya (bukan tiap tick selama nunggu di situ).
                if (!electroBall.HasBegunShrinking) {
                    SoundEngine.PlaySound(SoundID.Item29, frontChargePos);
                    for (int i = 0; i < 10; i++) {
                        Dust d = Dust.NewDustPerfect(frontChargePos, DustID.Electric, Main.rand.NextVector2Circular(4.5f, 4.5f));
                        d.noGravity = true;
                        d.color = Color.Red;
                        d.scale = Main.rand.NextFloat(1f, 1.7f);
                    }
                }

                electroBall.BeginShrinking();
            }

            NPC.ai[2] = 0f;
            NPC.ai[3] = 0f;
        }

        private Projectile FindOwnedElectroBall() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == ModContent.ProjectileType<PlutoElectroBall>() && (int)proj.ai[0] == NPC.whoAmI) {
                    return proj;
                }
            }
            return null;
        }

        private bool IsBallLevelOrPassed(Projectile ball) {
            // "Sejajar" -> posisi horizontal bola udah deket sama Pluto
            bool isLevel = Math.Abs(ball.Center.X - NPC.Center.X) < NovaChaseDashAlignThreshold;

            // "Ninggalin dia" -> Pluto sekarang ada di belakang arah gerak si bola
            Vector2 ballToNpc = NPC.Center - ball.Center;
            bool hasPassed = ball.velocity != Vector2.Zero && Vector2.Dot(ballToNpc, ball.velocity) < 0f;

            return isLevel || hasPassed;
        }

        private void ExecuteNovaRecoverStage(int timer) {
            NPC.velocity *= 0.94f;

            timer++;
            if (timer >= NovaRecoverTime) {
                // Selesai, biarkan PlutoHead.AI() gacha pattern lain
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.ai[2] = 0f;
                NPC.ai[3] = 0f;
                NPC.netUpdate = true;
            }
            else {
                NPC.ai[2] = timer;
            }
        }
    }
}
