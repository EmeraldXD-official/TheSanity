using Microsoft.Xna.Framework;
using Luminance.Core.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;
using TheSanity.Systems;
using TheSanity.GlobalNPCs;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // LAST STAND — event SEKALI PAKAI di HP <= 1% (Spazmatism, sumber kebenaran HP), TERPISAH
    // TOTAL dari rotasi pattern normal DAN dari Phase 2 (50% HP), TAPI awalannya SENGAJA
    // NYONTEK PERSIS konsep Phase 2 Trigger(). Alur:
    //
    //   1. Trigger()      -> PERSIS kayak Phase 2: Twin original LANGSUNG diem & INVINCIBLE
    //                         detik itu juga (motong pattern yang lagi jalan, TIDAK nunggu
    //                         siklusnya kelar dulu - beda dari Phase 2 yang masih nunggu),
    //                         snap ke tengah arena, arena MELEBAR 20% (animasi halus - beda
    //                         dari Phase 2 yang 40%), spawn 2 Spectre clone
    //                         (SpectreSpazmatism + SpectreRetinazer).
    //
    //   2. HoldForSpectres -> Twin diem total ngadap player (invincible, semi-transparent -
    //                         lihat LastStandHoldingForSpectres di TwinsRework.cs), NUNGGU
    //                         kedua Spectre clone ini tumbang.
    //
    //   3. RoarAndRefill   -> Begitu kedua Spectre tumbang: Twin diem dulu sebentar
    //                         (RoarPauseDurationTicks), BARU meng-Roar (SoundID.Roar) DAN
    //                         HP-nya di-refill ke 100% PERSIS di tick yang sama - dari titik
    //                         INI juga timer master 60 detik (HealthDrainDurationTicks) MULAI
    //                         jalan. Tahan sebentar lagi (RoarPostDurationTicks) buat kasih
    //                         "beat" ke animasi/suara Roar-nya sebelum mulai nyerang.
    //
    //   4. Dash..LaserBarrage (attack loop) -> Twin ngejalanin 6 pattern attack normal (Dash,
    //                         RetBeam, CursedRain, SpinningCurse, BorderShot, LaserBarrage,
    //                         otomatis versi ENRAGED karena IsEnraged pasti udah true) SECARA
    //                         LOOPING TERUS-MENERUS (abis LaserBarrage kelar, balik lagi ke
    //                         Dash, dst) - BUKAN cuma 1x muter kayak versi sebelumnya. HP
    //                         terus berkurang SEDIKIT DEMI SEDIKIT sepanjang loop ini
    //                         (lihat UpdateHealthDrainAndExpiry). Looping ini berlangsung
    //                         sampai AttackLoopDurationTicks dari total budget 60 detik
    //                         kelar (dicek di titik "gantian pattern", biar gak motong
    //                         pattern lagi di tengah jalan) - abis itu lanjut ke (5).
    //
    //   5. ReturnToCenter  -> abis attack loop kelar, Twin gerak cepat BALIK ke tengah arena
    //                         buat mulai Deathray.
    //
    //   6. Deathray (Clockwise -> Transition -> CounterClockwise -> Transition -> Clockwise ->
    //                         ...) -> beam merah panjang dari mukanya MUTER TERUS-MENERUS
    //                         (gantian CW/CCW, smooth decelerate+accelerate tiap ganti arah),
    //                         NGEDAMAGE TERUS, LOOPING TANPA HENTI - beneran cuma berhenti
    //                         kalau timer master 60 detik abis (lihat poin 7), BUKAN abis
    //                         sekian putaran kayak versi sebelumnya. Tiap 1 detik, muncul 1
    //                         RedPhantasmalBolt random dari tepi border (nembak ke tengah,
    //                         terus tembus) BARENGAN 8 RedPhantasmalBolt dari Twin sendiri
    //                         nyebar ke segala arah.
    //
    //   7. Timer habis     -> begitu LastStandHealthTimer nyampe HealthDrainDurationTicks (60
    //                         detik sejak Roar+refill) - DICEK TIAP TICK, gak peduli lagi di
    //                         attack loop atau Deathray - LANGSUNG dipotong ke DeathAnimation.
    //
    //   8. DeathAnimation  -> Twin "jatuh" (gravity diaktifin, noGravity = false) sampai
    //                         nyentuh block - border arena LANGSUNG DIHAPUS TOTAL (visual +
    //                         hitbox) di titik ini juga, biar player bisa lihat semuanya
    //                         tanpa terhalang. Begitu Twin BENERAN landing (bukan dari saat
    //                         gravity baru nyala), BARU timer 5 detik ledakan mulai jalan:
    //                         random muncul DD2ExplosiveTrapT1/T2/T3Explosion (VISUAL DOANG,
    //                         damage di-nol-in manual) di sekitarnya selama 5 detik penuh.
    //
    //   9. Done            -> abis 5 detik ledakan, Twin di-"bunuh" resmi manual (npc.life = 0;
    //                         npc.HitEffect(0,10); npc.checkDead();) - OnKill (TwinsRework.cs)
    //                         otomatis masak-masakin Retinazer ikut mati bareng.
    //
    // INVINCIBLE: Twin BENERAN gak bisa diserang sama sekali dari Trigger() sampai
    // DeathAnimation (npc.dontTakeDamage = true DIPAKSA tiap tick di awal Tick(), jaga-jaga
    // penuh) - beda dari desain sebelumnya yang sempat vulnerable di awal.
    //
    // CATATAN INTEGRASI: ShouldTrigger()/Trigger() dicek dari PreAI TwinsReworkOverride (mirip
    // pola TwinsPhaseTransition.ShouldArm/Arm), lalu selama LastStandActive true, dispatcher
    // pattern NORMAL (termasuk Phase 2) di-skip TOTAL dan Tick() di sini yang megang kendali
    // penuh tiap tick.
    // ==========================================
    public static class TwinsLastStand
    {
        private enum State
        {
            MovingToCenter, // FIX: terbang pelan-pelan ke tengah dulu, BUKAN teleport instan lagi
            HoldForSpectres,
            RoarAndRefill,
            Dash,
            RetBeam,
            CursedRain,
            SpinningCurse,
            BorderShot,
            LaserBarrage,
            SplitCombo,
            ReturnToCenter,
            DeathrayClockwise,
            DeathrayTransition,
            DeathrayCounterClockwise,
            DeathAnimation,
            Done
        }

        private const float TriggerLifeFraction = 0.01f; // 1% HP

        // ---- Arena expand (20%, beda dari Phase 2 yang 40%) ----
        private const float ArenaExpandMultiplier = 1.2f;
        private const int ArenaExpandDurationTicks = 45; // ~0.75 detik, sama kayak Phase 2

        // ---- FIX: terbang pelan-pelan ke tengah dulu (BUKAN teleport instan lagi) ----
        private const float MoveToCenterSpeed = 26f; // dipercepat dikit dari Phase 2 - "urgent"
        private const float MoveToCenterArriveThreshold = 30f;
        private const float MoveToCenterMaxDuration = 150f; // safety timeout ~2.5 detik

        // ---- Roar + refill ----
        private const float RoarPauseDurationTicks = 30f; // "diam dulu" sebelum Roar
        private const float RoarPostDurationTicks = 30f;  // tahan sebentar lagi abis Roar

        // ---- Timer master (invincible SAMPAI ini abis - HARD CAP durasi Last Stand) ----
        private const float HealthDrainDurationTicks = 3600f;  // 60 detik penuh
        private const float AttackLoopDurationTicks = 2100f;   // ~35 detik pertama buat looping attack, sisanya (~25 detik) Deathray

        // ---- ReturnToCenter ----
        private const float ReturnSpeed = 30f;
        private const float ReturnArriveThreshold = 30f;
        private const float ReturnMaxDuration = 120f; // safety timeout ~2 detik

        // ---- Deathray ----
        // Diturunin dikit dari sebelumnya (0.05f) - "muternya dibuat agak lambat" per request.
        private const float DeathrayAngularSpeed = 0.035f;     // radian/tick pas kecepatan penuh (CW atau CCW)
        private const int MinDeathrayReps = 3;
        private const int MaxDeathrayRepsInclusive = 4;

        // ==========================================
        // FIX: dulu 1 konstanta (DeathrayTransitionDuration) dipakai buat kurva cosine
        // simetris (decel separuh durasi, accel separuh durasi) - dan itu CUMA kepakai pas
        // CW->CCW doang (CCW->CW malah restart INSTAN full speed tanpa transisi sama sekali,
        // begitu juga start PERTAMA dari ReturnToCenter - dua-duanya lompat langsung ke
        // kecepatan penuh, ga ada ramp).
        //
        // Sekarang SEMUA "mulai spin ke arah X" - baik start pertama (dari diam) MAUPUN tiap
        // ganti arah (CW<->CCW) - lewat 1 mekanisme yang sama (lihat BeginDeathrayTransition/
        // TickDeathrayTransition), dipecah 2 fase:
        //   1. DECEL (durasi PENDEK) - REM cepat dari speed lama ke 0. Di-skip total kalau
        //      ini start pertama (emang udah diam, gak ada speed lama buat di-rem).
        //   2. RAMP-IN (durasi lebih PANJANG, kurva cubic ease-in t^3) - dari 0 ke speed
        //      penuh arah baru. Ini yang bikin "diawali bener-bener lambat" (deket t=0 laju
        //      naiknya pelan banget) TAPI "transisi dari lambatnya cepat" (mendekati t=1 laju
        //      naik makin curam, jadi gak lama-lama di zona lambat / gak buang waktu).
        // ==========================================
        private const float DeathrayDecelDuration = 12f;   // ~0.2 detik, REM cepat ke 0 (di-skip kalau start dari diam)
        private const float DeathrayRampInDuration = 45f;  // ~0.75 detik, ease-in lambat->cepat ke speed penuh arah baru

        private const int DeathrayDamage = 45;
        private const int DeathrayDamageIntervalTicks = 8;
        private const float DeathrayLength = 2600f; // sama panjangnya kayak garis render (DrawDeathrayCone)

        // Ketebalan hit-detection bentuk "V" - KECIL di deket muka, MELEBAR seiring jauh -
        // PERSIS ngikutin ketebalan core visual di DrawDeathrayCone (TwinsRework.cs) biar
        // damage-nya konsisten sama apa yang keliatan di layar.
        private const float DeathrayStartThickness = 12f;
        private const float DeathrayEndThickness = 100f;

        // Titik awal beam (muzzle) digeser MAJU dari npc.Center ke ujung sprite (bukan pas di
        // tengah badan) - dikali npc.width biar proporsional ke ukuran sprite Twins.
        private const float DeathrayMuzzleOffsetMultiplier = 0.5f;

        private const float BoltBurstIntervalTicks = 60f; // 1 detik
        private const int DeathrayTwinBoltCount = 8;
        private const int DeathrayBorderBoltDamage = 13; // RedBolt: target 40 DMG Master / 3 (engine auto-triples proyektil di Master mode)
        private const int DeathrayTwinBoltDamage = 13;   // RedBolt: target 40 DMG Master / 3 (engine auto-triples proyektil di Master mode)

        // ---- Death animation ----
        // Timer 5 detik ledakan BARU MULAI NGITUNG begitu Twin beneran landing (nyentuh
        // block) - BUKAN dari saat gravity baru diaktifin (lihat LastStandHasLanded).
        private const int DeathAnimationExplosionDurationTicks = 300; // 5 detik penuh SETELAH landing
        private const int LandingCheckMinTicks = 4;       // grace period dikit biar gravity sempat kerja dulu
        private const float LandingVelocityThreshold = 0.4f; // velocity.Y di bawah ini dianggap "udah landing"
        private const int ExplosionIntervalMinTicks = 10;
        private const int ExplosionIntervalMaxTicks = 22;
        private const float ExplosionSpawnRadius = 90f;

        // ==========================================
        // TRIGGER (dicek dari PreAI Spazmatism, mirip pola TwinsPhaseTransition.ShouldArm)
        // ==========================================
        public static bool ShouldTrigger(NPC npc, TwinsReworkOverride self)
        {
            return self.IsEnraged && !self.LastStandTriggered
                && npc.life > 0 && npc.life <= npc.lifeMax * TriggerLifeFraction;
        }

        public static void Trigger(NPC npc, TwinsReworkOverride self)
        {
            self.LastStandTriggered = true;
            self.LastStandActive = true;
            self.LastStandTimer = 0f;
            self.LastStandHealthDrainStarted = false;
            self.LastStandHealthTimer = 0f;
            self.LastStandExpiryPending = false; // reset jaga-jaga (harusnya emang masih false di titik ini)
            self.LastStandHoldingForSpectres = false; // belum holding - masih dalam perjalanan ke tengah

            // FIX: beda dari Phase 2 (yang nunggu siklus pattern kelar dulu baru Trigger),
            // Last Stand.Trigger() ini bisa motong pattern APAPUN di TENGAH JALAN (lihat
            // ShouldTrigger() - dicek tiap tick tanpa nunggu apa-apa). Jadi WAJIB bersihin
            // flag visual "kontinu" (aim line RetBeam, glow, dst) di sini - kalau enggak,
            // garis/glow itu bisa nyangkut nyala terus SELAMANYA karena Tick() pattern yang
            // punya flag itu gak akan pernah kepanggil lagi begitu Last Stand ambil alih.
            self.ResetTransientPatternVisuals();

            self.LastStandArenaCenter = GetArenaCenter(npc);

            // FIX: gak lagi teleport instan ke tengah - invincible LANGSUNG detik ini juga
            // (motong pattern yang lagi jalan TANPA nunggu siklusnya kelar dulu, beda dari
            // Phase 2 - Last Stand = urgent), TAPI posisinya terbang pelan-pelan dulu (lihat
            // State.MovingToCenter di bawah), BARU expand arena + spawn clone begitu beneran
            // nyampe.
            npc.dontTakeDamage = true;
            npc.netUpdate = true;

            self.LastStandStateRaw = (float)State.MovingToCenter;
        }

        private static void ExpandArena()
        {
            if (ArenaBorderSystem.ActiveBorders.Count > 0)
            {
                ArenaBorderSystem.Border border = ArenaBorderSystem.ActiveBorders[0];
                float newRadius = border.Radius * ArenaExpandMultiplier;
                border.AnimateRadiusTo(newRadius, ArenaExpandDurationTicks);
            }
        }

        private static void SpawnClones(NPC npc, TwinsReworkOverride self)
        {
            self.LastStandSpectreSpazIndex = -1;
            self.LastStandSpectreRetIndex = -1;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            const float SpawnDistance = 260f;
            Vector2 spazSpawnPos = self.LastStandArenaCenter + new Vector2(-SpawnDistance, 0f);
            Vector2 retSpawnPos = self.LastStandArenaCenter + new Vector2(SpawnDistance, 0f);

            self.LastStandSpectreSpazIndex = NPC.NewNPC(
                npc.GetSource_FromAI(),
                (int)spazSpawnPos.X,
                (int)spazSpawnPos.Y,
                ModContent.NPCType<SpectreSpazmatism>()
            );

            self.LastStandSpectreRetIndex = NPC.NewNPC(
                npc.GetSource_FromAI(),
                (int)retSpawnPos.X,
                (int)retSpawnPos.Y,
                ModContent.NPCType<SpectreRetinazer>()
            );
        }

        // ==========================================
        // Dipanggil TIAP TICK selama LastStandActive true (menggantikan dispatcher normal
        // TOTAL).
        // ==========================================
        public static void Tick(NPC npc, TwinsReworkOverride self, Player target)
        {
            // Invincible BENERAN dipaksa tiap tick, sepanjang SELURUH Last Stand (dari
            // Trigger() sampai DeathAnimation) - jaga-jaga penuh, gak ada celah sedikit pun.
            npc.dontTakeDamage = true;

            UpdateHealthDrainAndExpiry(npc, self);

            State state = (State)self.LastStandStateRaw;

            switch (state)
            {
                case State.MovingToCenter:
                    {
                        Vector2 toCenter = self.LastStandArenaCenter - npc.Center;
                        float distance = toCenter.Length();

                        self.LastStandTimer++;

                        if (distance > MoveToCenterArriveThreshold && self.LastStandTimer < MoveToCenterMaxDuration)
                        {
                            Vector2 moveDirection = toCenter / distance;
                            npc.velocity = Vector2.Lerp(npc.velocity, moveDirection * MoveToCenterSpeed, 0.12f);

                            Vector2 aimVector = target.Center - npc.Center;
                            npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
                            break;
                        }

                        // Udah sampai (atau timeout) -> posisi PAS di tengah, berhenti total,
                        // BARU di titik INI expand arena + spawn clone beneran kejadian.
                        npc.Center = self.LastStandArenaCenter;
                        npc.velocity = Vector2.Zero;

                        ExpandArena();
                        SpawnClones(npc, self);

                        // Bersihin trail SEKETIKA - jaga-jaga (walau sekarang gerakannya udah
                        // gradual/gak ada lompatan besar lagi, masih ada kemungkinan sisa
                        // ghost dari saat baru berhenti persis di titik ini).
                        self.Trail.Clear();
                        int retIndexForTrail = NPC.FindFirstNPC(NPCID.Retinazer);
                        if (retIndexForTrail != -1 && Main.npc[retIndexForTrail].active)
                        {
                            Main.npc[retIndexForTrail].GetGlobalNPC<TwinsReworkOverride>().Trail.Clear();
                        }

                        self.LastStandHoldingForSpectres = true;
                        self.LastStandTimer = 0f;
                        self.LastStandStateRaw = (float)State.HoldForSpectres;
                        npc.netUpdate = true;
                        break;
                    }

                case State.HoldForSpectres:
                    {
                        // FIX: sebelumnya cuma di-snap SEKALI di Trigger() terus velocity
                        // didamp pelan-pelan (*0.85) - kalau ada sisa momentum gede (misal
                        // Twin lagi di tengah Dash super cepat pas ke-trigger) dia masih bisa
                        // "meleset" jauh dari titik tengah sebelum keredam total. Sekarang
                        // posisi DIPAKSA PERSIS ke tengah arena TIAP TICK selama fase ini,
                        // jadi dijamin diem total di tempat dari tick pertama, gak ada celah
                        // drift sama sekali.
                        npc.Center = self.LastStandArenaCenter;
                        npc.velocity = Vector2.Zero;

                        Vector2 aimVector = target.Center - npc.Center;
                        npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;

                        bool spazAlive = self.LastStandSpectreSpazIndex != -1 && Main.npc[self.LastStandSpectreSpazIndex].active;
                        bool retAlive = self.LastStandSpectreRetIndex != -1 && Main.npc[self.LastStandSpectreRetIndex].active;

                        if (!spazAlive && !retAlive)
                        {
                            self.LastStandHoldingForSpectres = false;
                            self.LastStandStateRaw = (float)State.RoarAndRefill;
                            self.LastStandTimer = 0f;
                        }
                        break;
                    }

                case State.RoarAndRefill:
                    {
                        npc.Center = self.LastStandArenaCenter;
                        npc.velocity = Vector2.Zero;
                        self.LastStandTimer++;

                        // Diam dulu sebentar (RoarPauseDurationTicks), BARU Roar + refill HP
                        // PERSIS di tick ini juga - timer master 60 detik mulai jalan dari sini.
                        if ((int)self.LastStandTimer == (int)RoarPauseDurationTicks)
                        {
                            SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                            npc.life = npc.lifeMax;
                            npc.netUpdate = true;

                            self.LastStandHealthDrainStarted = true;
                            self.LastStandHealthTimer = 0f;
                        }

                        if (self.LastStandTimer >= RoarPauseDurationTicks + RoarPostDurationTicks)
                        {
                            self.LastStandStateRaw = (float)State.Dash;
                            TwinDash.ResetToHover(npc);
                        }
                        break;
                    }

                case State.Dash:
                    {
                        bool cycleFinished = TwinDash.Pattern1(npc, self, target);
                        if (cycleFinished)
                        {
                            self.LastStandStateRaw = (float)State.RetBeam;
                            TwinsRetBeam.Start(npc, self);
                        }
                        break;
                    }

                case State.RetBeam:
                    {
                        TwinsRetBeam.Tick(npc, self, target);
                        if (TwinsRetBeam.IsDone(self))
                        {
                            self.LastStandStateRaw = (float)State.CursedRain;
                            TwinsCursedRain.Start(self);
                        }
                        break;
                    }

                case State.CursedRain:
                    {
                        TwinsCursedRain.Tick(npc, self, target);
                        if (TwinsCursedRain.IsDone(self))
                        {
                            self.LastStandStateRaw = (float)State.SpinningCurse;
                            TwinsSpinningCurse.Start(npc, self);
                        }
                        break;
                    }

                case State.SpinningCurse:
                    {
                        TwinsSpinningCurse.Tick(npc, self, target);
                        if (TwinsSpinningCurse.IsDone(self))
                        {
                            self.LastStandStateRaw = (float)State.BorderShot;
                            TwinsBorderShot.Start(npc, self);
                        }
                        break;
                    }

                case State.BorderShot:
                    {
                        TwinsBorderShot.Tick(npc, self, target);
                        if (TwinsBorderShot.IsDone(self))
                        {
                            self.LastStandStateRaw = (float)State.LaserBarrage;
                            TwinsLaserBarrage.Start(npc, self);
                        }
                        break;
                    }

                case State.LaserBarrage:
                    {
                        TwinsLaserBarrage.Tick(npc, self, target);
                        if (TwinsLaserBarrage.IsDone(self))
                        {
                            self.LastStandStateRaw = (float)State.SplitCombo;
                            TwinsSplitCombo.Start(npc, self);
                        }
                        break;
                    }

                case State.SplitCombo:
                    {
                        TwinsSplitCombo.Tick(npc, self, target);
                        if (TwinsSplitCombo.IsDone(self))
                        {
                            // Budget attack-loop DICEK DI SINI - tepat begitu 1 PUTARAN PENUH
                            // (Dash->RetBeam->CursedRain->SpinningCurse->BorderShot->
                            // LaserBarrage->SplitCombo, SEMUA 7 pattern) kelar - dijamin SETIAP
                            // putaran selalu nyertain LENGKAP semua 7 pattern, gak dipotong di
                            // tengah (termasuk SplitCombo - kalau dipotong di tengah, Retinazer
                            // bisa ke-tinggal jauh dari Spaz / nyangkut vulnerable, makanya
                            // checkpoint aman WAJIB nunggu dia beneran regroup dulu, sama kayak
                            // gimana ResetTransientPatternVisuals di TwinsRework.cs jadi jaring
                            // pengaman kalau Last Stand/Phase 2 laen yang motong duluan).
                            //
                            // FIX: sekarang di-OR sama LastStandExpiryPending juga - kalau
                            // budget waktu master (60 detik) udah abis TAPI attack loop-nya
                            // baru aja kelar duluan (kondisi paling umum, soalnya 1 putaran
                            // biasanya emang udah lebih lama dari budget-nya sendiri), lanjut
                            // ke Deathray sekarang juga alih-alih nunggu AttackLoopDurationTicks
                            // (yang notabene udah kelewat jauh) - biar Deathray tetep kebagian
                            // jatah jalan.
                            bool loopBudgetSpent = self.LastStandHealthTimer >= AttackLoopDurationTicks;
                            if (self.LastStandExpiryPending || loopBudgetSpent)
                            {
                                self.LastStandStateRaw = (float)State.ReturnToCenter;
                                self.LastStandTimer = 0f;
                            }
                            else
                            {
                                self.LastStandStateRaw = (float)State.Dash;
                                TwinDash.ResetToHover(npc);
                            }
                        }
                        break;
                    }

                case State.ReturnToCenter:
                    {
                        Vector2 toCenter = self.LastStandArenaCenter - npc.Center;
                        float distance = toCenter.Length();

                        self.LastStandTimer++;

                        if (distance > ReturnArriveThreshold && self.LastStandTimer < ReturnMaxDuration)
                        {
                            Vector2 moveDirection = toCenter / distance;
                            npc.velocity = Vector2.Lerp(npc.velocity, moveDirection * ReturnSpeed, 0.12f);

                            Vector2 aimVector = target.Center - npc.Center;
                            npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
                            break;
                        }

                        npc.Center = self.LastStandArenaCenter;
                        npc.velocity = Vector2.Zero;

                        // FIX: dulu langsung StartDeathray() - snap ke kecepatan penuh CW
                        // instan dari diam. Sekarang lewat BeginDeathrayTransition dengan
                        // fromSign 0f (nandain "mulai dari diam") - TickDeathrayTransition yang
                        // urus ease-in-nya (lambat di awal, cepat ngeramp menjelang penuh).
                        // Sudut hadap awal di-random SEKALI di sini aja (start pertama) -
                        // restart-restart berikutnya TIDAK di-random ulang lagi, biar
                        // nyambung mulus dari sudut terakhir (lihat StartDeathray).
                        self.LastStandDeathrayAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                        BeginDeathrayTransition(npc, self, fromSign: 0f, toClockwise: true);
                        break;
                    }

                case State.DeathrayClockwise:
                    TickDeathraySpin(npc, self, target, spinSign: 1f);
                    break;

                case State.DeathrayTransition:
                    TickDeathrayTransition(npc, self, target);
                    break;

                case State.DeathrayCounterClockwise:
                    TickDeathraySpin(npc, self, target, spinSign: -1f);
                    break;

                case State.DeathAnimation:
                    TickDeathAnimation(npc, self);
                    break;

                case State.Done:
                    break;
            }
        }

        public static bool IsDone(TwinsReworkOverride self)
        {
            return (State)self.LastStandStateRaw == State.Done;
        }

        // ==========================================
        // Dipakai dari TwinsRework.cs buat nentuin contact damage dinamis (20 normal / 120
        // pas Dash & BorderShot) - private enum State di file ini gak bisa diakses langsung
        // dari luar, jadi expose lewat method public ini.
        // ==========================================
        public static bool IsCurrentlyDashOrBorderShot(TwinsReworkOverride self)
        {
            State state = (State)self.LastStandStateRaw;
            return state == State.Dash || state == State.BorderShot;
        }

        // ==========================================
        // Timer master - dicek TIAP TICK begitu drain-nya udah mulai (sejak Roar+refill),
        // gak peduli lagi di attack-loop atau Deathray.
        //
        // FIX: dulu begitu nyampe HealthDrainDurationTicks (60 detik) LANGSUNG dipotong ke
        // StartDeathAnimation() detik itu juga, gak peduli lagi di tengah pattern/putaran
        // apa. Masalahnya, 1 putaran PENUH attack loop (Dash->RetBeam->CursedRain->
        // SpinningCurse->BorderShot->LaserBarrage, apalagi versi ENRAGED) gampang makan
        // waktu jauh lebih dari 60 detik tergantung RNG repeats masing-masing pattern -
        // jadi timer ini abis DUAN sebelum 1 putaran attack loop aja sempet kelar, apalagi
        // sampe ke Deathray. Makanya Deathray-nya gak pernah nongol sama sekali.
        //
        // Sekarang timer ini CUMA nandain "pending" (LastStandExpiryPending) - keputusan
        // beneran berhenti dipindah ke checkpoint AMAN di masing-masing state (lihat
        // "case State.LaserBarrage" - akhir 1 putaran attack loop penuh, dan
        // TickDeathraySpin - akhir 1 pasang Deathray CW+CCW penuh). Jadi walau timer udah
        // lewat cap, Twin TETAP nyelesein dulu attack yang lagi jalan (plus minimal 1x
        // Deathray penuh) sebelum beneran masuk DeathAnimation.
        // ==========================================
        private static void UpdateHealthDrainAndExpiry(NPC npc, TwinsReworkOverride self)
        {
            if (!self.LastStandHealthDrainStarted)
                return;

            State currentState = (State)self.LastStandStateRaw;
            if (currentState == State.DeathAnimation || currentState == State.Done)
                return;

            self.LastStandHealthTimer++;

            float drainPerTick = MathHelper.Max(1f, npc.lifeMax / HealthDrainDurationTicks);
            npc.life = Math.Max(1, npc.life - (int)drainPerTick);

            if (self.LastStandHealthTimer >= HealthDrainDurationTicks)
            {
                self.LastStandExpiryPending = true;
            }
        }

        // ==========================================
        // DEATHRAY
        // ==========================================

        // Dipanggil dari ReturnToCenter (start pertama, fromSign 0f) dan dari TickDeathraySpin
        // (tiap kali 1 arah kelar reps-nya, mau ganti arah) - MULAI fase transisi/ramp, BUKAN
        // langsung nge-spin di speed penuh. Lihat TickDeathrayTransition buat kurvanya.
        private static void BeginDeathrayTransition(NPC npc, TwinsReworkOverride self, float fromSign, bool toClockwise)
        {
            self.LastStandDeathrayTransitionFromSign = fromSign;
            self.LastStandDeathrayTransitionToClockwise = toClockwise;
            self.LastStandDeathrayActive = true;
            self.LastStandStateRaw = (float)State.DeathrayTransition;
            self.LastStandTimer = 0f;

            SoundEngine.PlaySound(SoundID.Zombie104, npc.Center);
            ScreenShakeSystem.StartShake(8f, 0.4f);
        }

        // Dipanggil PERSIS begitu ramp-in (TickDeathrayTransition) kelar - lanjut ke fase spin
        // beneran di kecepatan PENUH, siap ngitung reps target baru buat arah ini.
        //
        // FIX: dulu di sini juga yang nge-random LastStandDeathrayAngle ULANG tiap kali
        // dipanggil (termasuk tiap restart dari transisi) - bikin beam "snap" ngelompat ke
        // sudut baru yang gak nyambung PAS TEPAT selesai transisi mulus, ngerusak seluruh
        // efek easing yang barusan kejadian. Sekarang angle CUMA di-random SEKALI, di titik
        // paling awal (ReturnToCenter, sebelum BeginDeathrayTransition pertama) - di sini
        // sudut TERKINI dibiarin lanjut apa adanya.
        private static void StartDeathray(NPC npc, TwinsReworkOverride self, bool clockwise)
        {
            self.LastStandDeathrayAccumAngle = 0f;
            self.LastStandDeathrayRepsTarget = Main.rand.Next(MinDeathrayReps, MaxDeathrayRepsInclusive + 1);
            self.LastStandDeathrayActive = true;
            self.LastStandBoltTimer = 0f;
            self.LastStandTimer = 0f;

            self.LastStandStateRaw = clockwise ? (float)State.DeathrayClockwise : (float)State.DeathrayCounterClockwise;
        }

        private static void TickDeathraySpin(NPC npc, TwinsReworkOverride self, Player target, float spinSign)
        {
            npc.velocity = Vector2.Zero;
            npc.Center = self.LastStandArenaCenter;

            float angularSpeed = DeathrayAngularSpeed * spinSign;
            self.LastStandDeathrayAngle += angularSpeed;
            self.LastStandDeathrayAccumAngle += DeathrayAngularSpeed; // magnitude doang, arah gak ngaruh ke progress

            npc.rotation = self.LastStandDeathrayAngle - MathHelper.PiOver2;

            self.LastStandTimer++;
            TickDeathrayDamageAndBolts(npc, self, target);

            float targetAngle = self.LastStandDeathrayRepsTarget * MathHelper.TwoPi;
            if (self.LastStandDeathrayAccumAngle >= targetAngle)
            {
                if (spinSign > 0f)
                {
                    // CW kelar -> SELALU lanjut ke CCW dulu (expiry BELUM dicek di sini) -
                    // ini yang jamin 1 PASANG CW+CCW penuh selalu selesai utuh, gak dipotong
                    // pas baru separuh (cuma CW doang) walau budget waktu udah abis.
                    BeginDeathrayTransition(npc, self, fromSign: 1f, toClockwise: false);
                }
                else
                {
                    // CCW kelar -> 1 PASANG CW+CCW barusan baru aja kelar UTUH - ini titik
                    // AMAN buat cek expiry (checkpoint, sama filosofinya kayak checkpoint di
                    // akhir attack loop / LaserBarrage.IsDone). Kalau budget waktu master
                    // udah abis (LastStandExpiryPending) -> beneran berhenti sekarang, masuk
                    // DeathAnimation. Kalau belum -> looping lagi ke CW (Deathray emang
                    // dirancang looping terus-menerus sampai waktunya abis).
                    if (self.LastStandExpiryPending)
                    {
                        StartDeathAnimation(npc, self);
                    }
                    else
                    {
                        BeginDeathrayTransition(npc, self, fromSign: -1f, toClockwise: true);
                    }
                }
            }
        }

        // ==========================================
        // FIX: dulu 1 kurva cosine simetris (decel separuh durasi, accel separuh durasi) yang
        // CUMA kepakai pas CW->CCW - CCW->CW malah restart instan tanpa transisi apa pun.
        // Sekarang dipecah 2 fase & berlaku UNIVERSAL buat semua kasus "mulai spin ke arah
        // baru" (start pertama dari diam MAUPUN tiap ganti arah):
        //   - fromSign == 0f (start pertama, dari diam) -> LANGSUNG fase ramp-in, gak ada
        //     fase decel sama sekali (emang belum ada speed lama buat di-rem).
        //   - fromSign != 0f (abis nge-spin arah itu, mau ganti arah) -> fase DECEL dulu
        //     (durasi pendek, DeathrayDecelDuration) dari speed penuh arah lama ke 0, BARU
        //     fase ramp-in (durasi lebih panjang, DeathrayRampInDuration) dari 0 ke speed
        //     penuh arah baru.
        // Kurva ramp-in-nya cubic ease-in (t^3) - laju naik PELAN BANGET deket t=0 ("diawali
        // bener-bener lambat"), tapi naik MAKIN CURAM mendekati t=1 ("transisi dari lambatnya
        // cepat", gak lama-lama nongkrong di zona lambat / gak buang waktu).
        // ==========================================
        private static void TickDeathrayTransition(NPC npc, TwinsReworkOverride self, Player target)
        {
            npc.velocity = Vector2.Zero;
            npc.Center = self.LastStandArenaCenter;

            self.LastStandTimer++;

            float fromSign = self.LastStandDeathrayTransitionFromSign;
            bool toClockwise = self.LastStandDeathrayTransitionToClockwise;
            float toSign = toClockwise ? 1f : -1f;
            float speedFactor;

            if (fromSign == 0f)
            {
                // Start pertama (dari diam) - gak ada fase decel, langsung ramp-in.
                float rampT = MathHelper.Clamp(self.LastStandTimer / DeathrayRampInDuration, 0f, 1f);
                float eased = rampT * rampT * rampT; // cubic ease-in
                speedFactor = toSign * eased;

                if (self.LastStandTimer >= DeathrayRampInDuration)
                {
                    StartDeathray(npc, self, toClockwise);
                    return;
                }
            }
            else if (self.LastStandTimer <= DeathrayDecelDuration)
            {
                // Fase 1: REM cepat dari speed penuh arah lama ke 0.
                float decelT = self.LastStandTimer / DeathrayDecelDuration;
                speedFactor = fromSign * (1f - decelT);
            }
            else
            {
                // Fase 2: ramp-in dari 0 ke speed penuh arah baru (cubic ease-in, dihitung
                // relatif dari akhir fase decel, BUKAN dari awal TickDeathrayTransition).
                float rampTicks = self.LastStandTimer - DeathrayDecelDuration;
                float rampT = MathHelper.Clamp(rampTicks / DeathrayRampInDuration, 0f, 1f);
                float eased = rampT * rampT * rampT;
                speedFactor = toSign * eased;

                if (rampTicks >= DeathrayRampInDuration)
                {
                    StartDeathray(npc, self, toClockwise);
                    return;
                }
            }

            self.LastStandDeathrayAngle += DeathrayAngularSpeed * speedFactor;
            npc.rotation = self.LastStandDeathrayAngle - MathHelper.PiOver2;

            TickDeathrayDamageAndBolts(npc, self, target);
        }

        // Damage garis (segment check, sama filosofi kayak RetBeam.DamageBeam) + spawn bolt
        // per detik (border + 8 dari Twin) - dipanggil dari SEMUA sub-state Deathray (CW,
        // transition, CCW), jadi selalu aktif/konsisten sepanjang seluruh fase Deathray.
        private static void TickDeathrayDamageAndBolts(NPC npc, TwinsReworkOverride self, Player target)
        {
            Vector2 direction = self.LastStandDeathrayAngle.ToRotationVector2();

            // Muzzle: titik awal beam digeser MAJU dari Center ke ujung sprite (BUKAN pas di
            // tengah badan) - konsisten sama titik awal yang dipakai di render (TwinsRework.cs).
            Vector2 muzzlePos = npc.Center + direction * (npc.width * DeathrayMuzzleOffsetMultiplier);

            if ((int)self.LastStandTimer % DeathrayDamageIntervalTicks == 0 && target.active && !target.dead)
            {
                Vector2 segStart = muzzlePos;
                Vector2 segEnd = segStart + direction * DeathrayLength;

                float dist = DistancePointToSegment(target.Center, segStart, segEnd, out float t);

                // Ketebalan hit-nya ngikutin bentuk "V" - kecil di deket muka (t=0), lebar di
                // ujung jauh (t=1) - PERSIS ngikutin lebar visual core di DrawDeathrayCone.
                float hitThicknessAtT = MathHelper.Lerp(DeathrayStartThickness, DeathrayEndThickness, t) * 0.5f;

                if (dist <= hitThicknessAtT)
                {
                    // Beam ini nembak lewat target.Hurt() manual (bukan lewat proyektil
                    // beneran) - armor penetration & debuff WAJIB di-apply manual di sini.
                    target.Hurt(PlayerDeathReason.ByNPC(npc.whoAmI), DeathrayDamage, 0, armorPenetration: 999f);
                    TwinsDebuffGlobalProjectile.ApplyDebuffs(target);
                }
            }

            self.LastStandBoltTimer++;
            if (self.LastStandBoltTimer >= BoltBurstIntervalTicks)
            {
                self.LastStandBoltTimer = 0f;
                FireBorderBolt(npc, self);
                FireTwinRadialBolts(npc);
            }
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 segStart, Vector2 segEnd, out float t)
        {
            Vector2 seg = segEnd - segStart;
            float lengthSquared = seg.LengthSquared();
            if (lengthSquared <= 0.0001f)
            {
                t = 0f;
                return Vector2.Distance(point, segStart);
            }

            t = MathHelper.Clamp(Vector2.Dot(point - segStart, seg) / lengthSquared, 0f, 1f);
            Vector2 closest = segStart + seg * t;
            return Vector2.Distance(point, closest);
        }

        // 1 RedPhantasmalBolt dari titik RANDOM di tepi border, ngincer ke tengah arena tapi
        // TERUS MELUNCUR BEBAS (proyektilnya sendiri emang gak "berhenti" di titik manapun -
        // dia cuma melesat lurus terus sampai timeLeft habis/nembus keluar arena).
        private static void FireBorderBolt(NPC npc, TwinsReworkOverride self)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            float arenaRadius = GetArenaRadius();
            float randomAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            Vector2 spawnPoint = self.LastStandArenaCenter + randomAngle.ToRotationVector2() * arenaRadius;

            Vector2 direction = (self.LastStandArenaCenter - spawnPoint).SafeNormalize(Vector2.UnitY);
            int boltType = ModContent.ProjectileType<RedPhantasmalBolt>();

            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                spawnPoint,
                direction * RedPhantasmalBolt.StartSpeed,
                boltType,
                DeathrayBorderBoltDamage,
                1.5f,
                Main.myPlayer,
                direction.ToRotation(), // ai0: sudut arah terkunci
                0f                       // ai1: counter tick exponential
            );
        }

        // 8 RedPhantasmalBolt dari Twin sendiri, nyebar merata ke segala penjuru (45 derajat
        // per biji) - BARENGAN timing-nya sama FireBorderBolt (dipanggil di tick yang sama).
        private static void FireTwinRadialBolts(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int boltType = ModContent.ProjectileType<RedPhantasmalBolt>();
            float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi); // offset random tiap burst biar gak monoton di sudut yang sama tiap kali

            for (int i = 0; i < DeathrayTwinBoltCount; i++)
            {
                float angle = baseAngle + i * (MathHelper.TwoPi / DeathrayTwinBoltCount);
                Vector2 dir = angle.ToRotationVector2();

                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    dir * RedPhantasmalBolt.StartSpeed,
                    boltType,
                    DeathrayTwinBoltDamage,
                    1.5f,
                    Main.myPlayer,
                    angle,
                    0f
                );
            }
        }

        private static float GetArenaRadius()
        {
            if (ArenaBorderSystem.ActiveBorders.Count > 0)
                return ArenaBorderSystem.ActiveBorders[0].Radius;

            return 700f;
        }

        private static Vector2 GetArenaCenter(NPC npc)
        {
            if (ArenaBorderSystem.ActiveBorders.Count > 0)
                return ArenaBorderSystem.ActiveBorders[0].Center;

            return npc.Center;
        }

        // ==========================================
        // DEATH ANIMATION
        // ==========================================
        private static void StartDeathAnimation(NPC npc, TwinsReworkOverride self)
        {
            self.LastStandDeathrayActive = false;
            self.LastStandStateRaw = (float)State.DeathAnimation;
            self.LastStandTimer = 0f;
            self.LastStandHasLanded = false;

            // Border arena (visual + hitbox collider-nya) LANGSUNG DIHAPUS TOTAL detik ini
            // juga - biar player bisa lihat seluruh death animation (jatuh + ledakan) tanpa
            // border ngalangin pandangan/gerakan sama sekali.
            RemoveArenaBorderImmediately();

            // Aktifin gravity DAN tile collision sesaat biar Twin "jatuh" ke lantai/block
            // terdekat lalu BENERAN BERHENTI di situ (Twins vanilla defaultnya terbang bebas
            // - noGravity DAN noTileCollide biasanya true biar dia bisa nembus terrain pas
            // dash/reposisi di fight normal). Kalau cuma noGravity yang dimatiin tapi
            // noTileCollide masih true, dia tetap jatuh TEMBUS block tanpa pernah berhenti -
            // makanya keduanya WAJIB dimatiin bareng di sini.
            npc.noGravity = false;
            npc.noTileCollide = false;
            npc.velocity = Vector2.Zero;
            npc.rotation = 0f;
            npc.dontTakeDamage = true;
        }

        // Hapus border arena aktif (kalau ada) TOTAL & INSTAN - beda dari shrink-animasi
        // normal (AnimateRadiusTo) yang dipakai di tempat lain, di sini SENGAJA gak dianimasikan
        // sama sekali karena diminta "langsung hilang". Radius di-nolin manual, border-nya
        // dicabut langsung dari ActiveBorders (bukan nunggu RemovalCondition-nya sendiri jalan
        // - kondisi itu baru true begitu Twin beneran gak aktif lagi, padahal di titik ini
        // Twin masih hidup, cuma lagi death animation), dan callback OnFullyRemoved tetap
        // dipanggil manual biar state internal (mis. TwinsArenaGlobalNPC.arenaBorder) ikut
        // ke-reset bersih. Collider fisiknya (ArenaBorderColliderNPC) juga langsung
        // dinonaktifin paksa, biar hitbox-nya ikut hilang di tick yang sama.
        private static void RemoveArenaBorderImmediately()
        {
            if (ArenaBorderSystem.ActiveBorders.Count > 0)
            {
                ArenaBorderSystem.Border border = ArenaBorderSystem.ActiveBorders[0];
                border.Radius = 0f;
                ArenaBorderSystem.ActiveBorders.Remove(border);
                border.OnFullyRemoved?.Invoke();
            }

            int colliderIndex = NPC.FindFirstNPC(ModContent.NPCType<ArenaBorderColliderNPC>());
            if (colliderIndex != -1)
            {
                Main.npc[colliderIndex].active = false;
                Main.npc[colliderIndex].netUpdate = true;
            }
        }

        private static void TickDeathAnimation(NPC npc, TwinsReworkOverride self)
        {
            // Diem total (redam sisa laju horizontal kalau ada dari momentum jatuh) - gravity
            // vanilla yang urus jatuhnya, kita cukup redam gerak horizontal biar gak "geser".
            npc.velocity.X *= 0.9f;

            if (!self.LastStandHasLanded)
            {
                // Selama masih jatuh, self.LastStandTimer dipakai CUMA buat grace period
                // (nunggu gravity sempat kerja dulu) sebelum ngecek "udah landing apa belum" -
                // BUKAN timer 5 detik ledakan (itu baru mulai ngitung abis landing, di bawah).
                self.LastStandTimer++;

                bool longEnoughToCheck = self.LastStandTimer >= LandingCheckMinTicks;
                bool velocitySettled = Math.Abs(npc.velocity.Y) < LandingVelocityThreshold;

                if (longEnoughToCheck && velocitySettled)
                {
                    self.LastStandHasLanded = true;
                    self.LastStandTimer = 0f; // reset - timer 5 detik ledakan MULAI dari sini
                    self.LastStandNextExplosionCountdown = Main.rand.Next(ExplosionIntervalMinTicks, ExplosionIntervalMaxTicks + 1);
                }

                return; // belum landing -> belum mulai ngeledak & belum ngitung timer 5 detik
            }

            self.LastStandNextExplosionCountdown--;
            if (self.LastStandNextExplosionCountdown <= 0f)
            {
                SpawnHarmlessExplosion(npc);
                self.LastStandNextExplosionCountdown = Main.rand.Next(ExplosionIntervalMinTicks, ExplosionIntervalMaxTicks + 1);
            }

            self.LastStandTimer++;
            if (self.LastStandTimer >= DeathAnimationExplosionDurationTicks)
            {
                // Kelar 5 detik ledakan -> "bunuh" Twin resmi secara manual. OnKill
                // (TwinsRework.cs) otomatis masak-masakin Retinazer ikut mati bareng di tick
                // yang sama.
                npc.life = 0;
                npc.HitEffect(0, 10);
                npc.checkDead();

                self.LastStandActive = false;
                self.LastStandStateRaw = (float)State.Done;
            }
        }

        // Ledakan VISUAL DOANG dari salah satu dari 3 tipe DD2 Explosive Trap secara random,
        // di titik random di sekitar Twin - damage-nya di-NOL-in manual (friendly=false,
        // hostile=false, damage=0 di parameter NewProjectile) biar BENERAN gak ngenain siapa
        // pun (baik player maupun NPC lain), murni efek visual buat death animation.
        private static readonly int[] ExplosionProjectileIds =
        {
            ProjectileID.DD2ExplosiveTrapT1Explosion,
            ProjectileID.DD2ExplosiveTrapT2Explosion,
            ProjectileID.DD2ExplosiveTrapT3Explosion,
        };

        // Sound ledakan vanilla (bom/granat) - dipicu tiap SpawnHarmlessExplosion, DI SEMUA
        // client (bukan cuma server) biar semua orang di sesi MP kedengeran ledakannya,
        // walau proyektil visualnya sendiri cuma di-spawn dari server (lihat netMode check
        // di bawah baris ini). Posisinya di spawnPos (titik ledakan itu sendiri), bukan
        // npc.Center, biar sumber suaranya kerasa nyebar sesuai titik random tiap ledakan.
        private static void SpawnHarmlessExplosion(NPC npc)
        {
            Vector2 offset = new Vector2(
                Main.rand.NextFloat(-ExplosionSpawnRadius, ExplosionSpawnRadius),
                Main.rand.NextFloat(-ExplosionSpawnRadius, ExplosionSpawnRadius)
            );
            Vector2 spawnPos = npc.Center + offset;

            SoundEngine.PlaySound(SoundID.Item14, spawnPos);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int chosenType = ExplosionProjectileIds[Main.rand.Next(ExplosionProjectileIds.Length)];

            int index = Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                spawnPos,
                Vector2.Zero,
                chosenType,
                0,      // damage 0 - gak ngenain siapa pun
                0f,
                Main.myPlayer
            );

            // Jaga-jaga tambahan (belt-and-suspenders) di luar damage=0 di atas - paksa
            // friendly & hostile-nya false juga biar collision damage-nya bener-bener nol.
            Main.projectile[index].friendly = false;
            Main.projectile[index].hostile = false;
        }
    }
}
