using Microsoft.Xna.Framework;
using Luminance.Core.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // PATTERN BARU: CURSED RAIN
    // ==========================================
    // Dipisah ke file sendiri kayak TwinsRetBeam.cs, biar TwinsRework.cs (dispatcher utama)
    // gak makin gemuk pas pattern-nya nambah banyak.
    //
    // Alur pattern:
    //   0. Seluruh siklus di bawah (langkah 1-5) diulang 3-4x (di-random tiap Start(), lihat
    //      CursedRainRepeatsRemaining) sebelum pattern ini beneran Done - TIAP ulangan Twin
    //      balik dulu ke ChaseAbove, jadi dia NGEPASIN ULANG posisi di atas kepala player,
    //      bukan langsung muter lagi dari tempat terakhir dia beam.
    //   1. ChaseAbove -> Twin ngejar posisi ~30 block (480px) DI ATAS KEPALA player yang
    //                     diincar (npc.target). Begitu udah sedeket ArriveThreshold ke titik
    //                     itu (atau timeout jaga-jaga), lanjut ke step 2.
    //   2. Twin BERHENTI TOTAL di situ, langsung MENGHADAP KE BAWAH (rotation di-set sekali).
    //   3. Spinning  -> Twin berputar 1 PUTARAN PENUH (360 derajat / 2*PI radian) di tempat,
    //                    SAMBIL nembakin CursedFlameHostile dari "mulut"/depan tiap 0.1 detik
    //                    (6 tick) — arah tembaknya ngikutin arah hadap LIVE selama muter,
    //                    jadi cursed flame-nya nyebar ke segala penjuru kayak "kembang api".
    //   4. Begitu SpinAngle nyampe 2*PI (1 putaran penuh kelar), Twin nembak SATU
    //      TwinsCursedBeam (lihat file itu) LURUS KE BAWAH dari posisinya. Beam itu yang
    //      smart-scan nyari block solid pertama, dan begitu nemu, ngeluarin ledakan
    //      RedPhantasmalBolt (5-7 biji) ke ARAH ATAS dari titik impact-nya. TEPAT pas beam
    //      ini lepas tembak, dipicu efek yang sama kayak TwinsRetBeam: sound Zombie104,
    //      screen shake, dan flashbang merah non-solid (lihat FireCursedBeamDown).
    //   5. Beaming  -> Twin diem nunggu selama beam itu hidup (BeamHoldDuration, dikasih
    //                   sedikit buffer di atas umur asli TwinsCursedBeam). Begitu itu kelar,
    //                   kalau masih ada ulangan tersisa -> BALIK ke langkah 1 (ChaseAbove),
    //                   kalau udah habis (3-4x) -> Done.
    //   6. Done     -> dispatcher (TwinsRework.cs) gantian ke pattern lain.
    //
    // CATATAN INTEGRASI: Start(self) dipanggil SEKALI buat mulai pattern ini (dispatcher milih
    // "giliran CursedRain"). Abis itu Tick(npc, self, target) dipanggil tiap tick sama kayak
    // TwinDash.Pattern1 / TwinsRetBeam.Tick, sampai IsDone(self) true.
    public static class TwinsCursedRain
    {
        private enum State
        {
            ChaseAbove,
            Spinning,
            Beaming,
            Done
        }

        // ---- Tunable knobs ----
        private const float HeightAboveHead = 480f;   // 30 block * 16px/block
        private const float ArriveThreshold = 50f;    // dianggap "sampai" kalau udah sedeket ini
        private const float ChaseSpeed = 16f;
        private const float ChaseMaxDuration = 240f;  // safety timeout ~4 detik biar gak nyangkut selamanya

        private const float SpinDuration = 90f;           // ~1.5 detik buat 1 putaran penuh (360°)
        private const int CursedFlameIntervalTicks = 6;   // tiap 0.1 detik (60 tick/detik)
        private const float CursedFlameSpeed = 7f;
        private const int CursedFlameDamage = 5; // Curse Bolt/Curse Ball: target 15 DMG Master / 3 (engine auto-triples proyektil di Master mode)

        // ---- ENRAGED (Phase 3): muter lebih kenceng + CursedFlame lebih rapat selama Spinning ----
        private const float EnragedSpinSpeedMultiplier = 1.6f;
        private const int EnragedCursedFlameIntervalTicks = 3; // 2x lebih rapat dari normal

        // Damage TwinsCursedBeam pas nyentuh player — ini yang PENTING dipass di parameter
        // Damage NewProjectile (BUKAN 0), soalnya parameter itu yang FINAL nge-set
        // Projectile.damage, nimpa apapun yang di-set di SetDefaults() proyektilnya.
        private const int CursedBeamDamage = 19; // Beam Predik: base 15 kena ~80 di game (rasio observasi ~5.33x, BUKAN 3x kayak dugaan awal) - dinaikin ke 19 buat target 100+

        // Jarak "moncong"/ujung depan muka Ret dari titik tengah NPC — beam-nya keluar dari
        // sini (bukan dari Center polos), sama konvensi kayak mouthPos di FireCursedFlame &
        // FireEyeFire/telegraph line pattern lain, biar keliatan "keluar dari muka" bukan
        // nongol tiba-tiba dari titik tengah badan.
        private const float BeamMuzzleOffset = 42f;

        // Buffer dikit di atas umur asli TwinsCursedBeam (BeamLifeTime = 80 tick), biar Twin
        // gak keburu gerak lagi/pindah pattern sebelum beam-nya beneran kelar kelihatan.
        private const float BeamHoldDuration = 95f;

        // Berapa kali seluruh siklus (ChaseAbove -> Spinning -> Beaming) diulang sebelum
        // pattern ini dianggap Done. Di-random ULANG tiap kali Start() dipanggil dari awal,
        // sama pola-nya kayak MinLaps/MaxLapsInclusive di TwinsBorderShot atau
        // MinRepeats/MaxRepeatsInclusive di TwinsRetBeam.
        private const int MinRepeats = 3;
        private const int MaxRepeatsInclusive = 4;

        public static void Start(TwinsReworkOverride self)
        {
            self.CursedRainStateRaw = (float)State.ChaseAbove;
            self.CursedRainTimer = 0f;
            self.CursedRainSpinAngle = 0f;

            // Tentukan berapa kali siklus penuh (kejar-atas-kepala -> muter -> beam) diulang
            // sebelum pattern ini kelar (3-4x, di-random ulang tiap Start()).
            self.CursedRainRepeatsRemaining = Main.rand.Next(MinRepeats, MaxRepeatsInclusive + 1);
        }

        public static void Tick(NPC npc, TwinsReworkOverride self, Player target)
        {
            State state = (State)self.CursedRainStateRaw;

            switch (state)
            {
                case State.ChaseAbove:
                    {
                        Vector2 hoverSpot = target.Center - new Vector2(0f, HeightAboveHead);
                        Vector2 toSpot = hoverSpot - npc.Center;
                        float distance = toSpot.Length();

                        if (distance > ArriveThreshold)
                        {
                            Vector2 moveDirection = toSpot / distance;
                            npc.velocity = Vector2.Lerp(npc.velocity, moveDirection * ChaseSpeed, 0.08f);

                            Vector2 aimVector = target.Center - npc.Center;
                            npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
                        }

                        self.CursedRainTimer++;
                        if (distance <= ArriveThreshold || self.CursedRainTimer >= ChaseMaxDuration)
                        {
                            // Sampai (atau timeout) -> berhenti total & langsung menghadap ke bawah.
                            npc.velocity = Vector2.Zero;
                            npc.rotation = Vector2.UnitY.ToRotation() - MathHelper.PiOver2;

                            self.CursedRainSpinAngle = 0f;
                            self.CursedRainTimer = 0f;
                            self.CursedRainStateRaw = (float)State.Spinning;
                            npc.netUpdate = true;
                        }
                        break;
                    }

                case State.Spinning:
                    {
                        // Diam total di tempat selama muter — cuma redam sisa laju kalau ada.
                        npc.velocity *= 0.8f;

                        // ---- ENRAGED (Phase 3): muter lebih cepat + CursedFlame lebih rapat ----
                        float spinSpeed = MathHelper.TwoPi / SpinDuration;
                        if (self.IsEnraged)
                            spinSpeed *= EnragedSpinSpeedMultiplier;
                        self.CursedRainSpinAngle += spinSpeed;

                        // Rotasi = menghadap-bawah (base) + progres putaran sejauh ini.
                        float baseDownRotation = Vector2.UnitY.ToRotation() - MathHelper.PiOver2;
                        npc.rotation = baseDownRotation + self.CursedRainSpinAngle;

                        self.CursedRainTimer++;
                        int flameInterval = self.IsEnraged ? EnragedCursedFlameIntervalTicks : CursedFlameIntervalTicks;
                        if ((int)self.CursedRainTimer % flameInterval == 0)
                        {
                            FireCursedFlame(npc);
                        }

                        if (self.CursedRainSpinAngle >= MathHelper.TwoPi)
                        {
                            // 1 putaran penuh kelar -> lepasin beam ke bawah.
                            FireCursedBeamDown(npc, self);

                            self.CursedRainTimer = 0f;
                            self.CursedRainStateRaw = (float)State.Beaming;
                            npc.netUpdate = true;
                        }
                        break;
                    }

                case State.Beaming:
                    npc.velocity *= 0.85f;
                    self.CursedRainTimer++;

                    if (self.CursedRainTimer >= BeamHoldDuration)
                    {
                        self.CursedRainTimer = 0f;
                        self.CursedRainRepeatsRemaining--;

                        if (self.CursedRainRepeatsRemaining > 0)
                        {
                            // Masih ada ulangan tersisa -> BALIK ke ChaseAbove, jadi Twin
                            // beneran ngepasin ulang posisi di atas kepala player (BUKAN
                            // langsung muter lagi dari tempatnya sekarang).
                            self.CursedRainStateRaw = (float)State.ChaseAbove;
                        }
                        else
                        {
                            self.CursedRainStateRaw = (float)State.Done;
                        }

                        npc.netUpdate = true;
                    }
                    break;

                case State.Done:
                    // Pattern selesai. Dispatcher (TwinsRework.cs) yang manggil Start() lagi
                    // kalau suatu saat giliran pattern ini balik lagi di rotasi.
                    break;
            }
        }

        public static bool IsDone(TwinsReworkOverride self)
        {
            return (State)self.CursedRainStateRaw == State.Done;
        }

        // Nembak CursedFlameHostile dari "depan", ngikutin arah hadap LIVE si Twin (yang lagi
        // muter). Konversi rotasi balik ke arah dunia nyata pakai formula yang sama kayak
        // "facingDir" di TwinsReworkOverride.PreDraw (rotation + PiOver2) — kebalikan dari
        // formula (direction.ToRotation() - PiOver2) yang dipakai buat nge-set rotasi.
        private static void FireCursedFlame(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 facingDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
            Vector2 mouthPos = npc.Center + facingDir * 24f;

            int flameIndex = Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                mouthPos,
                facingDir * CursedFlameSpeed,
                ProjectileID.CursedFlameHostile,
                CursedFlameDamage,
                2f,
                Main.myPlayer
            );
            Main.projectile[flameIndex].GetGlobalProjectile<TwinsDebuffGlobalProjectile>().IsFromTwins = true;
        }

        // Lepasin SATU TwinsCursedBeam lurus ke bawah, keluar dari ujung depan/moncong muka
        // Ret (BUKAN dari titik tengah NPC polos) — sama kayak beam/proyektil lain di pattern
        // ini/pattern lain yang keluar dari "mouthPos". Rotasi proyektilnya (arah beam, BUKAN
        // rotasi sprite NPC) di-set manual abis spawn, soalnya NewProjectile gak punya
        // parameter rotation langsung.
        private static void FireCursedBeamDown(NPC npc, TwinsReworkOverride self)
        {
            // ==========================================
            // EFEK "LASER LEPAS TEMBAK" - sound Zombie104 + screen shake + flashbang merah
            // non-solid, PERSIS pola yang sama kayak TwinsRetBeam pas beam-nya nembak. Sengaja
            // dijalanin di SEMUA client (sebelum netMode check di bawah), soalnya ini murni
            // audio/visual - biar semua orang di sesi MP tetap kerasa efeknya, walau
            // proyektil beam-nya sendiri cuma di-spawn dari server.
            // ==========================================
            SoundEngine.PlaySound(SoundID.Zombie104, npc.Center);
            TwinsRetBeamFlash.Trigger();
            ScreenShakeSystem.StartShake(6f, 0.3f);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // Arah hadap LIVE (harusnya udah lurus ke bawah lagi begitu 1 putaran penuh kelar,
            // tapi dihitung dari rotation asli biar konsisten, bukan asumsi Vector2.UnitY mentah).
            Vector2 facingDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
            Vector2 muzzlePos = npc.Center + facingDir * BeamMuzzleOffset;

            int beamIndex = Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                muzzlePos,
                Vector2.Zero, // beam diem di tempat, arah cukup disimpen di rotation
                ModContent.ProjectileType<TwinsCursedBeam>(),
                CursedBeamDamage,
                0f,
                Main.myPlayer
            );

            Main.projectile[beamIndex].rotation = facingDir.ToRotation(); // lurus ke bawah, dari titik moncong

            // ENRAGED: impact beam ini juga nyemburin RedPhantasmalBolt ke kanan-kiri
            // (lihat TwinsCursedBeam.FireImpactBolts), bukan cuma ke "atas" doang.
            if (Main.projectile[beamIndex].ModProjectile is TwinsCursedBeam cursedBeam)
            {
                cursedBeam.Enraged = self.IsEnraged;
            }

            // Simpen index-nya di field NPC — dibaca di TwinsReworkOverride.PreDraw buat
            // gambar manual beam ini SEBELUM sprite Twins, biar kegambar DI BAWAH mereka
            // (lihat komentar panjang di ActiveCursedBeamIndex & DrawBeamVisual).
            self.ActiveCursedBeamIndex = beamIndex;
        }
    }
}
