using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace YourModName.Content.NPCs
{
    // =========================================================================================
    // 🛑 [SPAWN INTRO] Cutscene pembuka Twins, SATU KALI doang seumur hidup NPC leader
    // (Spazmatism) ini — di-trigger dari blok `if (!SpawnIntroInitialized)` di PreAI
    // TwinsReworkOverride.cs, dicek PALING DULUAN sebelum SEMUA dispatcher lain (dawn
    // despawn, Last Stand, Phase 2, rotasi pattern normal).
    //
    // Alurnya numpang PERSIS pola PlutoHead / PlutoSpawnDash.cs (4 stage, reuse
    // SpawnIntroStage & SpawnIntroTimer, gaya sama kayak state machine pattern lain di
    // file ini — RetBeam*, CursedRain*, dst — field terpisah, BUKAN npc.ai[] soalnya itu
    // udah kepake Dash+animasi frame):
    //
    //   Stage 0 (CameraPullIn)  : layar player (target) ditarik ke titik ~50 block DI ATAS
    //                             dirinya sendiri. Twins sendiri diem dulu di posisi dia
    //                             ke-spawn (ga dipindah sama sekali).
    //   Stage 1 (RunIn)         : Begitu kamera sampai, Twins (Spazmatism) "berlari" (dash)
    //                             CEPAT dari posisi dia SEKARANG menuju titik fokus kamera
    //                             itu. Retinazer OTOMATIS ikut nempel (mirror block normal
    //                             di PreAI paling atas yang udah ada, gak perlu logic
    //                             tambahan di sini sama sekali).
    //   Stage 2 (BossIntro)     : Twins berhenti mendadak. Kamera TETEP di posisi ketarik.
    //                             Sub-fase (murni dari timer):
    //                               a) Darken   : layar pelan-pelan nge-gelap
    //                               b) Type     : judul "Mechanical Nightmare" muncul PER
    //                                             HURUF (typewriter)
    //                               c) Name     : begitu judul kelar diketik, nama boss
    //                                             "The Twins" LANGSUNG muncul utuh
    //                               d) GradientReveal : BEDA dari Pluto (yang glow scan-nya
    //                                             putih) — di sini teks disapu REVEAL
    //                                             gradasi warna MERAH (kiri) ke KUNING
    //                                             (kanan), kiri ke kanan, sekali jalan.
    //                                             Sesudahnya teks TETAP nampil dengan
    //                                             gradasi merah->kuning itu (bukan balik
    //                                             putih polos) sampai fade out.
    //                               e) Hold     : teks diem sebentar, gradasi tetap nyala
    //                               f) FadeOut  : teks & layar hitam fade away BARENGAN,
    //                                             abis ini fight beneran dimulai
    //   Stage 3 (CameraReturn)  : Layar player pelan-pelan ditarik balik ke posisi normal,
    //                             lalu CurrentPattern dipaksa balik ke Dash (ResetToHover)
    //                             biar rotasi pattern normal mulai bersih dari awal.
    //
    // Invincibility & no-contact-damage SELAMA SELURUH cutscene ini (SESUAI REQUEST "twins
    // ngga bisa di serang begitupula player ngga bisa serang twins") di-paksa MANUAL tiap
    // tick di dalam Tick() di bawah (npc.dontTakeDamage = true; npc.damage = 0;) —
    // Retinazer otomatis ikut invincible juga lewat mirror block normalnya sendiri
    // (npc.dontTakeDamage = true selalu di mode mirror, gak berubah).
    //
    // 🎥 [KAMERA] Ditarik lewat ModPlayer.ModifyScreenPosition() -- lihat
    // TwinsSpawnCameraPlayer.cs. Field SpawnIntroCameraOffset dihitung LOKAL & murni dari
    // SpawnIntroStage/SpawnIntroTimer milik instance Spazmatism, sama kayak pola Pluto.
    // CATATAN MULTIPLAYER: sama kayak field pattern lain di TwinsReworkOverride.cs, field-
    // field di sini BELUM ke-sync manual lewat ModPacket — cukup buat singleplayer/testing
    // dulu (npc.netUpdate tetap dipanggil tiap transisi stage buat jaga-jaga field ai[]
    // vanilla lain yang mungkin ikut numpang sinkron, tapi field custom-nya sendiri enggak).
    //
    // 🖋️ [OVERLAY TEKS] Semua sub-progress Stage 2 (darken alpha, jumlah huruf, dst)
    // di-expose lewat field public di TwinsReworkOverride.cs, dipakai buat DRAW aja di
    // TwinsSpawnIntroSystem.cs (ModSystem terpisah) — file ini SENGAJA cuma ngitung angka
    // progress-nya doang, ga megang SpriteBatch/Draw sama sekali. Font-nya numpang font
    // yang SAMA kayak dipakai Pluto (lihat komentar TwinsSpawnIntroSystem.cs — SESUAI
    // REQUEST "pakek font yang sama").
    // =========================================================================================
    public static class TwinsSpawnIntro
    {
        public const int StageCameraPullIn = 0;
        public const int StageRunIn = 1;
        public const int StageBossIntro = 2;
        public const int StageCameraReturn = 3;

        // Sama persis angkanya kayak Pluto — 50 block di atas player (16px/block). Titik
        // ini JUGA jadi titik tujuan lari Twins di Stage 1.
        private const float CamAboveDistance = 50f * 16f;

        private const int CamPullInDuration = 40;   // ~0.67 detik
        private const int CamReturnDuration = 35;   // ~0.58 detik

        // Kecepatan lari dinamis — sama filosofi kayak Pluto: berapapun jarak Twins ke
        // titik tujuan, durasi larinya diusahakan tetep konsisten (distance/DesiredRunDuration,
        // di-clamp biar ga lemot/ngebut ga masuk akal).
        private const float DesiredRunDuration = 50f;
        private const float MinRunSpeed = 40f;
        private const float MaxRunSpeed = 260f;

        // =====================================================================================
        // TEKS BOSS INTRO — SESUAI REQUEST: "Mechanical Collapse" -> "Mechanical Nightmare",
        // "XL-08 Pluto" -> "The Twins".
        // =====================================================================================
        public const string BossIntroTitleText = "Mechanical Nightmare"; // teks gede di atas
        public const string BossIntroNameText = "The Twins";             // nama boss, di tengah bawah judul

        private const int IntroDarkenDuration = 15;      // layar mulai nge-gelap (~0.25 detik)
        private const float TicksPerTypedChar = 2.5f;    // kecepatan ketik judul atas (per huruf)
        private const int IntroGradientRevealDuration = 40; // sapuan reveal gradasi merah->kuning kiri->kanan (~0.67 detik)
        private const int IntroHoldDuration = 40;         // teks diem abis reveal (~0.67 detik)
        private const int IntroFadeOutDuration = 30;      // teks + layar hitam fade bareng (~0.5 detik)

        private static readonly int BossIntroTypeDuration = (int)Math.Ceiling(BossIntroTitleText.Length * TicksPerTypedChar);

        private static int BossIntroDarkenEnd => IntroDarkenDuration;
        private static int BossIntroTypeEnd => BossIntroDarkenEnd + BossIntroTypeDuration;
        private static int BossIntroGradientEnd => BossIntroTypeEnd + IntroGradientRevealDuration;
        private static int BossIntroHoldEnd => BossIntroGradientEnd + IntroHoldDuration;
        private static int BossIntroFadeOutEnd => BossIntroHoldEnd + IntroFadeOutDuration;

        // Dipanggil SEKALI dari PreAI begitu SpawnIntroInitialized baru aja di-set true.
        public static void Start(TwinsReworkOverride self)
        {
            self.SpawnIntroActive = true;
            self.SpawnIntroStage = StageCameraPullIn;
            self.SpawnIntroTimer = 0f;
            self.SpawnIntroDarkenAlpha = 0f;
            self.SpawnIntroTypedCharCount = 0;
            self.SpawnIntroShowBottomText = false;
            self.SpawnIntroGradientRevealProgress = -1f;
            self.SpawnIntroContentAlpha = 1f;
            self.SpawnIntroCameraOffset = Vector2.Zero;
        }

        public static void Tick(NPC npc, TwinsReworkOverride self, Player target)
        {
            // ==========================================
            // 🔒 [REQUEST] Twins gak bisa diserang & gak ngasih contact damage sama sekali
            // SELAMA seluruh cutscene ini — dipaksa tiap tick di sini (dispatcher normal
            // yang biasanya ngitung npc.damage dinamis 20/120 di-skip total selama
            // SpawnIntroActive true, jadi harus di-set manual di sini).
            // ==========================================
            npc.dontTakeDamage = true;
            npc.damage = 0;

            int stage = self.SpawnIntroStage;
            float timer = self.SpawnIntroTimer;

            if (stage == StageCameraPullIn)
            {
                // Twins diem dulu di posisi dia ke-spawn — TIDAK dipindah/di-teleport sama
                // sekali, cuma diredam biar ga ngambang aneh kalau ada sisa velocity spawn.
                npc.velocity *= 0.9f;

                float pullProgress = MathHelper.Clamp(timer / (float)CamPullInDuration, 0f, 1f);
                pullProgress = pullProgress * pullProgress * (3f - 2f * pullProgress); // smoothstep
                self.SpawnIntroCameraOffset = new Vector2(0f, -CamAboveDistance) * pullProgress;

                timer++;
                if (timer >= CamPullInDuration)
                {
                    self.SpawnIntroStage = StageRunIn;
                    self.SpawnIntroTimer = 0f;

                    if (target != null && target.active)
                    {
                        Vector2 arrivalPoint = target.Center + new Vector2(0f, -CamAboveDistance);
                        Vector2 runDir = (arrivalPoint - npc.Center).SafeNormalize(Vector2.Zero);
                        float distanceToArrival = Vector2.Distance(npc.Center, arrivalPoint);

                        float runSpeed = MathHelper.Clamp(distanceToArrival / DesiredRunDuration, MinRunSpeed, MaxRunSpeed);
                        npc.velocity = runDir * runSpeed;
                        npc.rotation = runDir.ToRotation();
                        self.SpawnIntroRunDashDuration = MathHelper.Clamp(distanceToArrival / runSpeed, 20f, 240f);
                    }
                    else
                    {
                        // Fallback edge-case (target somehow gak valid) — langsung anggap
                        // udah nyampe, biar cutscene ga nyangkut selamanya nunggu target.
                        self.SpawnIntroRunDashDuration = 0f;
                    }

                    npc.netUpdate = true;
                }
                else
                {
                    self.SpawnIntroTimer = timer;
                }
            }
            else if (stage == StageRunIn)
            {
                // 🛑 [FIX] Konvensi rotation di codebase ini itu rotation = velocity.ToRotation()
                // - PiOver2 (lihat TickDawnDespawn, baris ~1015) — sebelumnya kurang -PiOver2 di
                // sini jadi Twins keliatan miring 90° pas lari.
                npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

                timer++;
                if (timer >= self.SpawnIntroRunDashDuration)
                {
                    npc.velocity = Vector2.Zero;
                    self.SpawnIntroStage = StageBossIntro;
                    self.SpawnIntroTimer = 0f;
                    npc.netUpdate = true;
                }
                else
                {
                    self.SpawnIntroTimer = timer;
                }
            }
            else if (stage == StageBossIntro)
            {
                npc.velocity = Vector2.Zero;
                FaceTowardsTarget(npc, target); // 🛑 [REQUEST] Twins menghadap ke bawah/ke player selama teks intro nongol

                // Simpen nilai SEBELUM di-update, dipake buat deteksi "baru aja berubah" di
                // bawah (biar SFX Typing/Roar cuma bunyi PAS TRANSISI-nya doang).
                int previousTypedCount = self.SpawnIntroTypedCharCount;
                bool previousShowBottomText = self.SpawnIntroShowBottomText;

                // -- a) Darken: layar pelan-pelan nge-gelap --
                self.SpawnIntroDarkenAlpha = MathHelper.Clamp(timer / (float)BossIntroDarkenEnd, 0f, 1f);

                // -- b) Type: judul atas diketik per huruf, mulai abis darken kelar --
                if (timer <= BossIntroDarkenEnd)
                {
                    self.SpawnIntroTypedCharCount = 0;
                }
                else
                {
                    float typeProgress = MathHelper.Clamp((timer - BossIntroDarkenEnd) / (float)BossIntroTypeDuration, 0f, 1f);
                    self.SpawnIntroTypedCharCount = (int)(typeProgress * BossIntroTitleText.Length);
                }

                // 🔊 [SFX - TYPING] Tiap ada huruf BARU yang nongol, mainin sound vanilla
                // NPCHit4 (metal/mekanik, senada sama HitSound Twins yang lain), tapi pitch-nya
                // diturunin (WithPitchOffset negatif) biar kedengeran lebih "berat"/dalem
                // dibanding versi normalnya — bukan sound ketik custom lagi kayak Pluto.
                // 🛑 Angka -0.4f di bawah bisa disetel lagi kalau masih kurang berat/kelewat
                // berat kedengerannya (range wajar sekitar -1f s/d 1f, makin negatif makin berat).
                if (!Main.dedServ && self.SpawnIntroTypedCharCount > previousTypedCount)
                {
                    SoundEngine.PlaySound(SoundID.NPCHit4.WithPitchOffset(-0.4f));
                }

                // -- c) Name: nama boss LANGSUNG muncul utuh begitu judul atas kelar diketik --
                self.SpawnIntroShowBottomText = timer >= BossIntroTypeEnd;

                // 🔊 [SFX - ROAR] Pas nama boss "The Twins" PERTAMA KALI muncul, mainin COSTUME
                // TwinsSounds.TwinRoar (dulu SoundID.Roar bawaan Terraria, sama kayak Pluto) -
                // sama sound yang dipakai di semua roar/dash Twins lain, pitch-nya udah
                // di-naikin bawaan dari definisi TwinsSounds.TwinRoar.
                if (!Main.dedServ && self.SpawnIntroShowBottomText && !previousShowBottomText)
                {
                    SoundEngine.PlaySound(TwinsSounds.TwinRoar);
                }

                // -- d) GradientReveal: BEDA dari Pluto — bukan sapuan glow putih, tapi
                // sapuan REVEAL gradasi warna merah (kiri) ke kuning (kanan), kiri->kanan,
                // sekali jalan, abis kedua teks kelar tampil. Sesudah fase ini kelar,
                // progress-nya TETAP dikunci di 1f (bukan balik -1f) supaya teks TERUS
                // tampil dengan gradasi merah->kuning-nya sampai fade out (lihat
                // TwinsSpawnIntroSystem.cs — nilai < 0 berarti "belum mulai reveal sama
                // sekali", nilai 0..1 dipakai buat nge-mask seberapa jauh gradasi udah
                // "nyala" dari kiri).
                if (timer >= BossIntroTypeEnd && timer < BossIntroGradientEnd)
                {
                    self.SpawnIntroGradientRevealProgress = (timer - BossIntroTypeEnd) / (float)IntroGradientRevealDuration;
                }
                else if (timer >= BossIntroGradientEnd)
                {
                    self.SpawnIntroGradientRevealProgress = 1f;
                }

                // -- e)+f) Hold lalu FadeOut: teks & layar hitam kompak nge-fade bareng --
                if (timer >= BossIntroHoldEnd)
                {
                    float fadeOutProgress = MathHelper.Clamp((timer - BossIntroHoldEnd) / (float)IntroFadeOutDuration, 0f, 1f);
                    self.SpawnIntroContentAlpha = 1f - fadeOutProgress;
                    self.SpawnIntroDarkenAlpha *= (1f - fadeOutProgress);
                }
                else
                {
                    self.SpawnIntroContentAlpha = 1f;
                }

                timer++;
                if (timer >= BossIntroFadeOutEnd)
                {
                    // Boss Intro kelar total — reset bersih sebelum lanjut Stage 3 (fight
                    // belum mulai di sini, masih nunggu kamera balik dulu).
                    self.SpawnIntroDarkenAlpha = 0f;
                    self.SpawnIntroContentAlpha = 0f;
                    self.SpawnIntroGradientRevealProgress = -1f;

                    self.SpawnIntroStage = StageCameraReturn;
                    self.SpawnIntroTimer = 0f;
                    npc.netUpdate = true;
                }
                else
                {
                    self.SpawnIntroTimer = timer;
                }
            }
            else if (stage == StageCameraReturn)
            {
                FaceTowardsTarget(npc, target); // tetep menghadap player selagi kamera balik

                float returnProgress = MathHelper.Clamp(timer / (float)CamReturnDuration, 0f, 1f);
                returnProgress = returnProgress * returnProgress * (3f - 2f * returnProgress); // smoothstep
                self.SpawnIntroCameraOffset = new Vector2(0f, -CamAboveDistance) * (1f - returnProgress);

                timer++;
                if (timer >= CamReturnDuration)
                {
                    // --- Cutscene kelar total — fight beneran dimulai dari sini. Twins
                    // balik bisa diserang & ngasih contact damage lagi. npc.damage otomatis
                    // ke-hitung ulang tiap tick sama dispatcher normal, TAPI npc.dontTakeDamage
                    // HARUS di-reset manual di sini soalnya di luar Spawn Intro, Spazmatism
                    // gak pernah nge-set-false-in itu di tempat lain (cuma di-set true
                    // kondisional pas butuh, misal Last Stand) — kalau gak di-reset di sini,
                    // Twins bakal nempel invincible SELAMANYA abis intro kelar. 🛑 [FIX]
                    self.SpawnIntroCameraOffset = Vector2.Zero;
                    self.SpawnIntroActive = false;
                    self.SpawnIntroStage = StageCameraPullIn;
                    self.SpawnIntroTimer = 0f;

                    npc.dontTakeDamage = false;

                    self.CurrentPattern = TwinsReworkOverride.SpazPattern.Dash;
                    TwinDash.ResetToHover(npc);

                    npc.netUpdate = true;
                }
                else
                {
                    self.SpawnIntroTimer = timer;
                }
            }
        }

        // ==========================================
        // 🛑 [REQUEST] Twins menghadap ke bawah/ke player selama stage BossIntro & CameraReturn
        // (pas dia berdiri diem nunggu teks selesai) — dihitung LIVE tiap tick dari posisi
        // player SAAT ITU (bukan snapshot), pakai konvensi rotation yang sama kayak dispatcher
        // normal (rotation = arah.ToRotation() - PiOver2, lihat TickDawnDespawn buat
        // pembandingnya). Kalau target somehow gak valid, fallback ke lurus ke bawah aja
        // biar gak ngaco.
        // ==========================================
        private static void FaceTowardsTarget(NPC npc, Player target)
        {
            Vector2 dir = (target != null && target.active)
                ? (target.Center - npc.Center).SafeNormalize(new Vector2(0f, 1f))
                : new Vector2(0f, 1f); // fallback: lurus ke bawah

            npc.rotation = dir.ToRotation() - MathHelper.PiOver2;
        }
    }
}
