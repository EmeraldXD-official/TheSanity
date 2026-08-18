using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;
using TheSanity.Projectiles;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // PATTERN: LASER BARRAGE
    // ==========================================
    // Twin gerak SUPER CEPAT ke 4 titik di sekeliling player secara berurutan
    // (ATAS -> KIRI -> BAWAH -> KANAN), dan di TIAP titik dia berhenti sebentar
    // buat nembakin 20x RedLaser (DeathLaser) barrage ke arah player (arah
    // di-aim ULANG tiap tembakan, ngikutin posisi player LIVE - biar tetep
    // ngincer walau player gerak selama barrage).
    //
    // Abis 20 tembakan kelar di 1 titik, ada jeda ~0.5 detik (biar player sempat
    // baca pola/napas), BARU pindah ke titik berikutnya. Titik tujuan tiap leg
    // di-KUNCI sekali pas mulai gerak ke situ (snapshot posisi player saat itu +
    // offset arah), TIDAK ngikutin player lagi selama proses pindah - biar
    // gerakannya predictable, gak "ngejar" terus kek homing.
    //
    // State per leg: Moving -> Firing -> Pausing, diulang 4x (index 0..3),
    // abis index ke-3 (KANAN) kelar Pausing -> Done.
    //
    // MP-SAFE: semua NewProjectile dibungkus netMode check, tiap ganti
    // state/leg netUpdate = true.
    public static class TwinsLaserBarrage
    {
        private enum State
        {
            Moving,
            Firing,
            AimingRetLaser, // ENRAGED (Phase 3) saja - telegraph sebelum beam beneran lepas, biar dodgeable
            Beaming, // ENRAGED (Phase 3) saja - nunggu "RetLaserBeam" ilang sebelum pindah sisi
            Pausing,
            Done
        }

        // Urutan sisi: atas, kiri, bawah, kanan - offset RELATIF ke posisi player,
        // di-snapshot ulang tiap kali masuk state Moving buat leg itu.
        private static readonly Vector2[] SideOffsets =
        {
            new Vector2(0f, -520f),   // atas
            new Vector2(-520f, 0f),   // kiri
            new Vector2(0f, 520f),    // bawah
            new Vector2(520f, 0f),    // kanan
        };

        // ---- Tunable knobs ----
        private const float MoveSpeed = 42f;           // "gerak benar-benar cepat"
        private const float ArriveThreshold = 50f;
        private const float MoveMaxDuration = 90f;     // safety timeout ~1.5 detik per leg

        private const int ShotsPerSide = 20;
        private const int ShotIntervalTicks = 4;       // jeda antar tembakan dalam 1 barrage
        private const float ShotSpeed = 14f;
        private const int ShotDamage = 8; // Red laser/Death laser: target 25 DMG Master / 3 (engine auto-triples proyektil di Master mode)

        private const float PauseDuration = 30f;       // ~0.5 detik jeda sebelum pindah sisi

        // ==========================================
        // ENRAGED (Phase 3): abis 20x RedLaser di 1 sisi, Twin diem sebentar dulu ngincer
        // ("telegraph") ke arah TERAKHIR player kelihatan (dikunci sekali, BUKAN terus
        // ngikutin kayak RetBeam Aiming) selama AimBeamDuration tick, BARU beneran lepas
        // SATU "RetLaserBeam" (numpang TwinsCursedBeam) yang juga muncrat impact
        // RedPhantasmalBolt. Jeda ini yang bikin beam-nya DODGEABLE - player kebagian waktu
        // baca garis & minggir sebelum damage-nya beneran aktif. Twin BARU boleh pindah ke
        // sisi berikutnya SETELAH beam ini beneran hilang (lihat State.Beaming).
        // ==========================================
        public const float AimBeamDuration = 26f; // ~0.43 detik ngincer sebelum lepas tembak
        private const int RetLaserBeamDamage = 36; // Beam Predik: base 15 kena ~80 di game (rasio observasi ~5.33x) - dinaikin ke 36 buat target ~190

        public static void Start(NPC npc, TwinsReworkOverride self)
        {
            self.LaserBarrageStateRaw = (float)State.Moving;
            self.LaserBarrageTimer = 0f;
            self.LaserBarrageSideIndex = 0;
            self.LaserBarrageShotsFired = 0;
            self.LaserBarrageIsAiming = false;
            self.LaserBarrageTargetSpot = npc.Center; // sementara, di-snapshot ulang di bawah
            SnapshotTargetSpot(npc, self, GetLivePlayer(npc));
        }

        public static void Tick(NPC npc, TwinsReworkOverride self, Player target)
        {
            State state = (State)self.LaserBarrageStateRaw;

            switch (state)
            {
                case State.Moving:
                    {
                        Vector2 toSpot = self.LaserBarrageTargetSpot - npc.Center;
                        float distance = toSpot.Length();

                        self.LaserBarrageTimer++;

                        if (distance > ArriveThreshold && self.LaserBarrageTimer < MoveMaxDuration)
                        {
                            Vector2 moveDirection = toSpot / distance;
                            npc.velocity = moveDirection * MoveSpeed;

                            Vector2 aimVector = target.Center - npc.Center;
                            npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
                            break;
                        }

                        // Nyampe (atau timeout) -> berhenti persis di titik, mulai barrage.
                        npc.Center = self.LaserBarrageTargetSpot;
                        npc.velocity = Vector2.Zero;

                        self.LaserBarrageTimer = 0f;
                        self.LaserBarrageShotsFired = 0;
                        self.LaserBarrageStateRaw = (float)State.Firing;
                        npc.netUpdate = true;
                        break;
                    }

                case State.Firing:
                    {
                        npc.velocity *= 0.7f;

                        Vector2 aimVector = target.Center - npc.Center;
                        npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;

                        self.LaserBarrageTimer++;

                        if ((int)self.LaserBarrageTimer % ShotIntervalTicks == 0 && self.LaserBarrageShotsFired < ShotsPerSide)
                        {
                            FireLaserAtPlayer(npc, self, target);
                            self.LaserBarrageShotsFired++;
                        }

                        if (self.LaserBarrageShotsFired >= ShotsPerSide)
                        {
                            self.LaserBarrageTimer = 0f;

                            if (self.IsEnraged)
                            {
                                // ENRAGED: JANGAN langsung nembak - kunci arah ke posisi player
                                // SAAT INI dulu, terus telegraph (garis merah) selama
                                // AimBeamDuration sebelum beam-nya beneran lepas. Ini yang
                                // bikin dodgeable (player kebagian waktu buat minggir).
                                self.LaserBarrageAimDirection = (target.Center - npc.Center).SafeNormalize(-Vector2.UnitY);
                                self.LaserBarrageIsAiming = true;
                                self.LaserBarrageStateRaw = (float)State.AimingRetLaser;

                                // Aim line keluar -> COSTUME BeamCharged (dulu Zombie103), sama
                                // kayak RetBeam pas garis aim-nya nongol pertama kali.
                                SoundEngine.PlaySound(TwinsSounds.BeamCharged, npc.Center);
                            }
                            else
                            {
                                self.LaserBarrageStateRaw = (float)State.Pausing;
                            }

                            npc.netUpdate = true;
                        }
                        break;
                    }

                case State.AimingRetLaser:
                    {
                        // Twin diem total, arah UDAH DIKUNCI (LaserBarrageAimDirection) - TIDAK
                        // di-update lagi walau player gerak, biar player beneran bisa "baca"
                        // garis dan minggir dari titik itu.
                        npc.velocity *= 0.8f;
                        npc.rotation = self.LaserBarrageAimDirection.ToRotation() - MathHelper.PiOver2;

                        self.LaserBarrageTimer++;
                        if (self.LaserBarrageTimer >= AimBeamDuration)
                        {
                            self.LaserBarrageIsAiming = false;
                            FireRetLaserBeam(npc, self, self.LaserBarrageAimDirection);

                            self.LaserBarrageTimer = 0f;
                            self.LaserBarrageStateRaw = (float)State.Beaming;
                            npc.netUpdate = true;
                        }
                        break;
                    }

                case State.Beaming:
                    {
                        npc.velocity *= 0.9f;

                        bool beamStillActive = self.ActiveCursedBeamIndex != -1 && Main.projectile[self.ActiveCursedBeamIndex].active;
                        if (!beamStillActive)
                        {
                            self.ActiveCursedBeamIndex = -1;
                            self.LaserBarrageTimer = 0f;
                            self.LaserBarrageStateRaw = (float)State.Pausing;
                            npc.netUpdate = true;
                        }
                        break;
                    }

                case State.Pausing:
                    {
                        npc.velocity *= 0.9f;
                        self.LaserBarrageTimer++;

                        if (self.LaserBarrageTimer >= PauseDuration)
                        {
                            self.LaserBarrageSideIndex++;

                            if (self.LaserBarrageSideIndex >= SideOffsets.Length)
                            {
                                self.LaserBarrageStateRaw = (float)State.Done;
                            }
                            else
                            {
                                SnapshotTargetSpot(npc, self, target);
                                self.LaserBarrageTimer = 0f;
                                self.LaserBarrageStateRaw = (float)State.Moving;
                            }

                            npc.netUpdate = true;
                        }
                        break;
                    }

                case State.Done:
                    break;
            }
        }

        public static bool IsDone(TwinsReworkOverride self)
        {
            return (State)self.LaserBarrageStateRaw == State.Done;
        }

        // Dipanggil dispatcher kalau mau reset bersih dari luar siklus normal
        // (jarang perlu, tapi disediain biar konsisten sama pattern lain kayak
        // TwinDash.ResetToHover).
        public static void Reset(NPC npc, TwinsReworkOverride self)
        {
            self.LaserBarrageStateRaw = (float)State.Moving;
            self.LaserBarrageTimer = 0f;
            self.LaserBarrageSideIndex = 0;
            self.LaserBarrageShotsFired = 0;
            self.LaserBarrageIsAiming = false;
        }

        private static void SnapshotTargetSpot(NPC npc, TwinsReworkOverride self, Player target)
        {
            Vector2 offset = SideOffsets[self.LaserBarrageSideIndex];
            self.LaserBarrageTargetSpot = target.Center + offset;
        }

        private static Player GetLivePlayer(NPC npc)
        {
            return Main.player[npc.target != -1 ? npc.target : Main.myPlayer];
        }

        // ---- ENRAGED (Phase 3): "RetLaserBeam" - numpang ModProjectile TwinsCursedBeam yang
        // udah di-generalize supaya bisa diarahkan ke mana aja (bukan cuma lurus ke bawah lagi
        // - lihat TwinsCursedBeam.BeamDir). Arahnya di sini UDAH DIKUNCI dari telegraph
        // (State.AimingRetLaser) - bukan dihitung ulang dari posisi player LIVE lagi, biar
        // konsisten sama garis yang barusan ditelegraph-in (kalau player minggir dari garis
        // itu pas telegraph, dia beneran aman). SuppressImpactBolts = true - beam ini SENGAJA
        // TIDAK muncrat RedPhantasmalBolt sama sekali (beda dari CursedRain yang muncrat).
        // Index-nya disimpen di self.ActiveCursedBeamIndex - field yang SAMA yang dipakai
        // TwinsCursedRain, jadi otomatis ke-render manual DI BAWAH sprite Twins lewat
        // TwinsReworkOverride.PreDraw (gak perlu ubah apa-apa lagi di sana).
        private static void FireRetLaserBeam(NPC npc, TwinsReworkOverride self, Vector2 direction)
        {
            // GANTI dari angka lokal 30f ke BeamMuzzleOffset (konstanta bersama di
            // TwinsReworkOverride) biar titik keluar beam ini SAMA kayak semua beam lain
            // (Deathray, Telegraph, RetBeam, DAN tembakan DeathLaser di FireLaserAtPlayer
            // bawah) - dulu tiap tempat punya angka muzzle sendiri-sendiri.
            Vector2 muzzlePos = npc.Center + direction * TwinsReworkOverride.BeamMuzzleOffset;

            // Efek "laser lepas tembak" - COSTUME BeamShot (dulu Zombie104), sama persis pola
            // yang dipakai RetBeam/CursedRain.
            SoundEngine.PlaySound(TwinsSounds.BeamShot, npc.Center);
            TwinsRetBeamFlash.Trigger();
            ScreenShakeSystem.StartShake(6f, 0.3f);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int beamIndex = Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                muzzlePos,
                Vector2.Zero, // beam diem di tempat, arah cukup disimpen di rotation
                ModContent.ProjectileType<TwinsCursedBeam>(),
                RetLaserBeamDamage,
                0f,
                Main.myPlayer
            );

            Main.projectile[beamIndex].rotation = direction.ToRotation();

            if (Main.projectile[beamIndex].ModProjectile is TwinsCursedBeam cursedBeam)
            {
                // Beam ini SENGAJA gak muncrat impact RedPhantasmalBolt sama sekali.
                cursedBeam.SuppressImpactBolts = true;
            }

            self.ActiveCursedBeamIndex = beamIndex;
        }

        private static void FireLaserAtPlayer(NPC npc, TwinsReworkOverride self, Player target)
        {
            Vector2 direction = (target.Center - npc.Center).SafeNormalize(-Vector2.UnitY);
            Vector2 muzzlePos = npc.Center + direction * TwinsReworkOverride.BeamMuzzleOffset;

            // Kedipan glow bintang merah di ARAH DEPAN wajah Retinazer (dihitung live di
            // DrawEyeLaserFlash) - dipicu tiap tembakan (20x per sisi), jalan di semua
            // client (visual doang).
            self.TriggerEyeFlash();

            // COSTUME: DeathLaser barrage ini dulu gak punya sound sendiri sama sekali -
            // request eksplisit "paling jelas di laser Barrage" karena di sini yang di-spam
            // 20x per sisi, jadi paling kentara. Dijalanin di semua client sebelum netMode
            // check (murni audio, biar semua orang di sesi MP tetap kedengeran).
            SoundEngine.PlaySound(TwinsSounds.NormalLaser, npc.Center);

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int index = Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                muzzlePos,
                direction * ShotSpeed,
                ProjectileID.DeathLaser,
                ShotDamage,
                1.5f,
                Main.myPlayer
            );
            Main.projectile[index].tileCollide = false;
            Main.projectile[index].GetGlobalProjectile<TwinsDebuffGlobalProjectile>().IsFromTwins = true;
        }
    }
}
