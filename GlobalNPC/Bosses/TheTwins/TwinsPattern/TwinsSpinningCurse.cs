using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Systems;
using TheSanity.Projectiles;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // PATTERN: SPINNING CURSE (v2 — ramp-up spam)
    // ==========================================
    // Twin gerak dulu ke TITIK TENGAH ARENA, begitu udah deket BERHENTI TOTAL
    // terus MUTER DI TEMPAT selama 15 detik penuh sambil nembakin GreenBolt
    // dari "mulut", arahnya ngikutin rotasi LIVE si Twin yang terus muter.
    //
    // BEDA dari versi sebelumnya: interval antar tembakan SEKARANG GAK TETAP.
    // Di awal spin tembakannya masih pelan (StartFireInterval), terus MAKIN LAMA
    // MAKIN CEPAT (nge-lerp turun ke EndFireInterval) seiring timer mendekati
    // akhir durasi - jadi kesannya "spam makin menggila" menjelang akhir pattern,
    // bukan rate tembak yang konstan dari awal sampai akhir.
    //
    // State: MoveToCenter -> Spinning -> Done
    //
    // MP-SAFE: semua NewProjectile dibungkus netMode check, tiap ganti state
    // netUpdate = true.
    public static class TwinsSpinningCurse
    {
        private enum State
        {
            MoveToCenter,
            Spinning,
            Done
        }

        // ---- Tunable knobs ----
        private const float ArriveThreshold = 40f;
        private const float MoveSpeed = 14f;
        private const float MoveMaxDuration = 180f; // safety timeout ~3 detik biar gak nyangkut

        private const float SpinDurationTicks = 900f;     // 15 detik penuh (60 tick/detik)
        private const float SpinAngularSpeed = 0.05f;      // radian per tick, arah muter konsisten

        // Interval antar tembakan (dalam tick) di AWAL vs di AKHIR durasi spin.
        // Di-lerp turun seiring progres timer - makin kecil intervalnya = makin
        // sering nembak = kerasa makin "spam".
        private const float StartFireInterval = 15f; // ~0.25 detik antar tembakan, masih santai
        private const float EndFireInterval = 2f;    // ~0.033 detik antar tembakan, spam brutal

        private const int GreenBoltDamage = 10; // GreenBolt: target ~30 DMG Master / 3 (engine auto-triples proyektil di Master mode) - +20% otomatis kalau Last Stand
        private const float MouthOffset = 24f;

        // ---- ENRAGED (Phase 3): tiap kali CursedFlame nembak ke DEPAN (lihat di bawah),
        // SEKALIAN nembak 1 RedPhantasmalBolt ke arah BELAKANG (kebalikan arah hadap) -
        // SELARAS sama trigger & interval CursedFlame (currentInterval yang sama, BUKAN
        // timer/interval terpisah lagi kayak versi rosette "+"/"X" sebelumnya), jadi makin
        // akhir durasi spin, bolt belakang ini juga ikut makin rapat munculnya. ----
        private const int BackBoltDamage = 10; // RedBolt: target ~30 DMG Master / 3 (engine auto-triples proyektil di Master mode) - +20% otomatis kalau Last Stand

        public static void Start(NPC npc, TwinsReworkOverride self)
        {
            self.SpinningCurseStateRaw = (float)State.MoveToCenter;
            self.SpinningCurseTimer = 0f;
            self.SpinningCurseFireCountdown = StartFireInterval;
        }

        public static void Tick(NPC npc, TwinsReworkOverride self, Player target)
        {
            State state = (State)self.SpinningCurseStateRaw;

            switch (state)
            {
                case State.MoveToCenter:
                    {
                        Vector2 arenaCenter = GetArenaCenter(npc);
                        Vector2 toCenter = arenaCenter - npc.Center;
                        float distance = toCenter.Length();

                        self.SpinningCurseTimer++;

                        if (distance > ArriveThreshold && self.SpinningCurseTimer < MoveMaxDuration)
                        {
                            Vector2 moveDirection = toCenter / distance;
                            npc.velocity = Vector2.Lerp(npc.velocity, moveDirection * MoveSpeed, 0.1f);

                            Vector2 aimVector = target.Center - npc.Center;
                            npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
                            break;
                        }

                        // Udah nyampe (atau timeout) -> berhenti total, mulai spin dari sini.
                        npc.velocity = Vector2.Zero;
                        self.SpinningCurseTimer = 0f;
                        self.SpinningCurseFireCountdown = StartFireInterval;
                        self.SpinningCurseStateRaw = (float)State.Spinning;
                        npc.netUpdate = true;
                        break;
                    }

                case State.Spinning:
                    {
                        npc.velocity *= 0.8f; // redam sisa laju kalau ada, biar bener-bener diem

                        npc.rotation += SpinAngularSpeed;
                        if (npc.rotation > MathHelper.Pi)
                            npc.rotation -= MathHelper.TwoPi;

                        self.SpinningCurseTimer++;

                        // Progres 0..1 sepanjang durasi spin, dipakai buat nge-lerp interval
                        // tembak dari StartFireInterval (pelan) turun ke EndFireInterval (spam).
                        float progress = MathHelper.Clamp(self.SpinningCurseTimer / SpinDurationTicks, 0f, 1f);
                        float currentInterval = MathHelper.Lerp(StartFireInterval, EndFireInterval, progress);

                        self.SpinningCurseFireCountdown--;
                        if (self.SpinningCurseFireCountdown <= 0f)
                        {
                            FireCursedFlame(npc);

                            // ENRAGED: bolt belakang SELARAS sama trigger CursedFlame ini -
                            // sama-sama nembak di tick yang sama, sama-sama makin rapat
                            // seiring progress (currentInterval yang sama dipakai ulang).
                            if (self.IsEnraged)
                            {
                                FireBackBolt(npc);
                            }

                            self.SpinningCurseFireCountdown = currentInterval;
                        }

                        if (self.SpinningCurseTimer >= SpinDurationTicks)
                        {
                            self.SpinningCurseStateRaw = (float)State.Done;
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
            return (State)self.SpinningCurseStateRaw == State.Done;
        }

        private static Vector2 GetArenaCenter(NPC npc)
        {
            if (ArenaBorderSystem.ActiveBorders.Count > 0)
                return ArenaBorderSystem.ActiveBorders[0].Center;

            return npc.Center;
        }

        // ---- ENRAGED (Phase 3): 1 RedPhantasmalBolt ke arah BELAKANG (kebalikan arah hadap
        // LIVE si Twin yang lagi muter) - dipanggil BARENGAN sama FireCursedFlame di atas,
        // jadi selalu selaras timing-nya, cuma bedanya CursedFlame ke depan & bolt ini ke
        // belakang. Titik keluarnya juga dari "belakang mulut" (MouthOffset yang sama,
        // cuma arahnya dibalik) biar simetris sama titik keluar CursedFlame.
        private static void FireBackBolt(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 facingDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
            Vector2 backDir = -facingDir;
            Vector2 spawnPos = npc.Center + backDir * MouthOffset;

            int boltType = ModContent.ProjectileType<RedPhantasmalBolt>();
            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                spawnPos,
                backDir * RedPhantasmalBolt.StartSpeed,
                boltType,
                TwinsReworkOverride.ScaleBoltDamage(BackBoltDamage),
                1.5f,
                Main.myPlayer,
                backDir.ToRotation(), // ai0: sudut arah terkunci
                0f                     // ai1: counter tick exponential
            );
        }

        // Formula konversi rotation -> arah dunia nyata SAMA kayak FireCursedFlame
        // di TwinsCursedRain.cs (rotation + PiOver2), biar konsisten satu codebase.
        //
        // CATATAN: TIDAK ada .GetGlobalProjectile<TwinsDebuffGlobalProjectile>().IsFromTwins
        // di sini — GreenBolt adalah custom ModProjectile milik kita sendiri, TIDAK
        // terdaftar di AppliesToEntity GlobalProjectile (cuma EyeFire/DeathLaser/
        // CursedFlameHostile vanilla yang terdaftar di sana). Manggil GetGlobalProjectile
        // pada proyektil yang tidak terdaftar → KeyNotFoundException → crash/instant death.
        // GreenBolt nge-handle debuffnya sendiri langsung di OnHitPlayer() (apply Cursed
        // Inferno), sama persis pola RedPhantasmalBolt — TIDAK perlu IsFromTwins sama sekali.
        private static void FireCursedFlame(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 facingDir = (npc.rotation + MathHelper.PiOver2).ToRotationVector2();
            Vector2 mouthPos = npc.Center + facingDir * MouthOffset;

            int boltType = ModContent.ProjectileType<GreenBolt>();
            float angle = facingDir.ToRotation();

            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                mouthPos,
                facingDir * GreenBolt.StartSpeed,
                boltType,
                TwinsReworkOverride.ScaleBoltDamage(GreenBoltDamage),
                2f,
                Main.myPlayer,
                angle, // ai0: sudut arah terkunci (dibaca sendiri sama AI() GreenBolt)
                0f     // ai1: counter tick buat kurva speed exponential, mulai dari 0
            );
        }
    }
}
