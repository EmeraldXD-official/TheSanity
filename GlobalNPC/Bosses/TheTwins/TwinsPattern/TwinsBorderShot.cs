using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;
using TheSanity.Systems;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // PATTERN: BORDER SHOT (v2 — langsung tembak per titik)
    // ==========================================
    // Twin TERBANG MENGELILINGI TEPI BORDER arena (ArenaBorderSystem.ActiveBorders[0])
    // satu putaran penuh. Tiap nempuh jarak ~8 block (128px) di sepanjang tepi,
    // TITIK ITU LANGSUNG MELESAT SAAT ITU JUGA (bukan nunggu full lap kelar dulu
    // kayak versi sebelumnya) - jadi keliatan kek Twin "muntahin" proyektil satu-satu
    // sepanjang jalur terbangnya, urut, real-time.
    //
    // Proyektilnya sekarang RedPhantasmalBolt (ModProjectile custom kita sendiri,
    // bukan DeathLaser lagi) - arah dikunci ke arah pusat arena pas ditembak
    // (ai0 = sudut), speed exponential dari pelan ke cepat ngikutin logic bawaan
    // RedPhantasmalBolt sendiri.
    //
    // State: Approaching -> Circling -> Cooldown -> Done
    //
    // UPDATE:
    //   - Glow/kedipan bintang merah di wajah Retinazer (TriggerEyeFlash) DIHAPUS dari
    //     pattern ini - FireBoltAt sekarang nembak polos tanpa efek kedip di ujung wajah.
    //   - Pattern ini sekarang WAJIB nyelesein 3-5 putaran PENUH (di-random tiap Start(),
    //     lihat BorderShotLapsRequired) sebelum State.Circling dianggap kelar dan lanjut
    //     ke Cooldown -> Done. Sebelumnya cuma 1 putaran.
    //   - Titik/sudut awal (BorderShotStartAngle) SEKARANG DI-RANDOM PENUH tiap Start()
    //     dipanggil, jadi Twin bisa mulai muter dari SISI mana aja di tepi border, gak
    //     lagi ngikut posisinya saat ini.
    //   - State BARU "Approaching" ditambahkan SEBELUM Circling: Twin gerak CEPAT (via
    //     velocity, sama filosofinya kayak ChaseAbove di TwinsCursedRain) menuju titik
    //     awal di tepi border itu dulu - BUKAN teleport/snap instan kayak sebelumnya
    //     (dulu npc.Center langsung dipindah ke radius penuh di tick pertama Circling).
    //
    // MP-SAFE: NewProjectile dibungkus netMode check, posisi Twin di-sync berkala
    // lewat netUpdate karena gerakannya di-set langsung (bukan lewat velocity).
    public static class TwinsBorderShot
    {
        private enum State
        {
            Approaching,
            Circling,
            Cooldown,
            Done
        }

        // ---- Tunable knobs ----
        private const float ApproachSpeed = 24f;            // "gerak cepat", bukan teleport
        private const float ApproachArriveThreshold = 40f;
        private const float ApproachMaxDuration = 180f;      // safety timeout ~3 detik biar gak nyangkut
        private const float LapDurationTicks = 240f;   // ~4 detik buat 1 putaran penuh
        private const float PlacementGap = 128f;        // 8 block * 16px, jarak antar tembakan di tepi border
        private const int ShotDamage = 10; // RedBolt: target ~30 DMG Master / 3 (engine auto-triples proyektil di Master mode) - +20% otomatis kalau Last Stand
        private const float CooldownDuration = 30f;     // ~0.5 detik jeda visual sebelum Done

        // Minimal & maksimal (inklusif) jumlah putaran penuh sebelum pattern ini
        // dianggap selesai. Nilainya di-random ULANG tiap kali Start() dipanggil,
        // jadi tiap giliran BorderShot kebagian jumlah putaran yang beda-beda.
        private const int MinLaps = 3;
        private const int MaxLapsInclusive = 5;

        // ==========================================
        // ENRAGED (Phase 3): SELAMA Twin muter di tepi border, tiap 1 DETIK muncul BURST
        // 4-5 GreenBolt sekaligus, masing-masing dari TITIK RANDOM berbeda di
        // sepanjang tepi border (BUKAN dari tengah), meluncur UMUMNYA ke arah tengah tapi
        // dibiarin "nyebar" (lihat FireBorderCursedFlame) - pure luck pattern, gak
        // predictable ngincer ke 1 titik.
        // ==========================================
        private const int CenterFlameIntervalTicks = 60; // tiap 1 detik (60 tick/detik)
        private const int CenterFlameMinCount = 4;
        private const int CenterFlameMaxCountInclusive = 5;
        private const float CenterFlameSpeed = 8f;
        private const int GreenBoltDamage = 10; // GreenBolt: target ~30 DMG Master / 3 (engine auto-triples proyektil di Master mode) - +20% otomatis kalau Last Stand

        public static void Start(NPC npc, TwinsReworkOverride self)
        {
            self.BorderShotStateRaw = (float)State.Approaching;
            self.BorderShotTimer = 0f;
            self.BorderShotSpinAngle = 0f;

            // Tentukan berapa kali putaran penuh yang wajib diselesaikan sebelum Done (3-5x).
            // FIX (request "Last Stand pakai jumlah repeat paling maks"): SELAMA Last Stand
            // (self.LastStandActive true), skip random & pakai MaxLapsInclusive langsung -
            // sama pola kayak TwinDash.DashRepeatsRemaining. Di luar Last Stand tetap random.
            self.BorderShotLapsRequired = self.LastStandActive ? MaxLapsInclusive : Main.rand.Next(MinLaps, MaxLapsInclusive + 1);

            // Sudut awal (sekaligus SISI mana Twin mulai muter di tepi border) SEKARANG
            // DI-RANDOM PENUH tiap kali Start() dipanggil - bukan lagi diturunkan dari posisi
            // NPC saat ini. Twin bakal gerak CEPAT (state Approaching di bawah, lewat
            // velocity) menuju titik ini dulu, BUKAN teleport/snap langsung ke tepi border.
            self.BorderShotStartAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);

            Vector2 arenaCenter = GetArenaCenter(npc, out float arenaRadius);
            self.BorderShotApproachTarget = arenaCenter + self.BorderShotStartAngle.ToRotationVector2() * arenaRadius;

            self.BorderShotNextPlacementAngle = 0f;
        }

        public static void Tick(NPC npc, TwinsReworkOverride self, Player target)
        {
            State state = (State)self.BorderShotStateRaw;

            switch (state)
            {
                case State.Approaching:
                    {
                        Vector2 toTarget = self.BorderShotApproachTarget - npc.Center;
                        float distance = toTarget.Length();

                        self.BorderShotTimer++;

                        if (distance > ApproachArriveThreshold && self.BorderShotTimer < ApproachMaxDuration)
                        {
                            Vector2 moveDirection = toTarget / distance;
                            npc.velocity = Vector2.Lerp(npc.velocity, moveDirection * ApproachSpeed, 0.12f);

                            Vector2 aimVector = target.Center - npc.Center;
                            npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
                            break;
                        }

                        // Sampai (atau timeout) -> posisi PAS di titik tepi border, langsung
                        // lanjut mulai muter dari sini (tanpa jeda, tanpa lompatan lagi -
                        // sudut & posisinya udah sama persis sama titik awal Circling).
                        npc.Center = self.BorderShotApproachTarget;
                        npc.velocity = Vector2.Zero;

                        self.BorderShotTimer = 0f;
                        self.BorderShotSpinAngle = 0f;
                        self.BorderShotNextPlacementAngle = 0f;
                        self.BorderShotStateRaw = (float)State.Circling;
                        npc.netUpdate = true;
                        break;
                    }

                case State.Circling:
                    {
                        Vector2 arenaCenter = GetArenaCenter(npc, out float arenaRadius);

                        float angularSpeed = MathHelper.TwoPi / LapDurationTicks;
                        self.BorderShotSpinAngle += angularSpeed;

                        float currentAngle = self.BorderShotStartAngle + self.BorderShotSpinAngle;
                        Vector2 pointOnBorder = arenaCenter + currentAngle.ToRotationVector2() * arenaRadius;

                        // Posisi di-set langsung di sepanjang lingkaran border, rotasi ngikutin
                        // arah tangensial biar keliatan "terbang menyusuri" tepinya.
                        npc.Center = pointOnBorder;
                        Vector2 tangent = (currentAngle + MathHelper.PiOver2).ToRotationVector2();
                        npc.rotation = tangent.ToRotation() - MathHelper.PiOver2;

                        // Tiap nempuh jarak PlacementGap di sepanjang tepi -> LANGSUNG nembak
                        // dari titik itu juga, gak ditaruh dulu buat nanti.
                        float anglePerPlacement = PlacementGap / arenaRadius;
                        if (self.BorderShotSpinAngle >= self.BorderShotNextPlacementAngle)
                        {
                            FireBoltAt(npc, self, pointOnBorder, arenaCenter);
                            self.BorderShotNextPlacementAngle += anglePerPlacement;
                        }

                        self.BorderShotTimer++;

                        // ---- ENRAGED: tiap 1 detik, burst 4-5 CursedFlame muncul RANDOM
                        // dari tepi border, meluncur umumnya ke arah tengah lalu dibiarin
                        // nyebar (pure luck) ----
                        if (self.IsEnraged && (int)self.BorderShotTimer % CenterFlameIntervalTicks == 0)
                        {
                            FireBorderCursedFlame(npc, arenaCenter, arenaRadius);
                        }

                        // Sync posisi berkala biar client lain gak keliatan patah-patah
                        // (posisi di-set manual tiap tick, bukan lewat velocity).
                        if ((int)self.BorderShotTimer % 10 == 0)
                            npc.netUpdate = true;

                        float requiredAngle = MathHelper.TwoPi * self.BorderShotLapsRequired;
                        if (self.BorderShotSpinAngle >= requiredAngle)
                        {
                            self.BorderShotTimer = 0f;
                            self.BorderShotStateRaw = (float)State.Cooldown;
                            npc.netUpdate = true;
                        }
                        break;
                    }

                case State.Cooldown:
                    npc.velocity *= 0.9f;
                    self.BorderShotTimer++;

                    if (self.BorderShotTimer >= CooldownDuration)
                    {
                        self.BorderShotStateRaw = (float)State.Done;
                        npc.netUpdate = true;
                    }
                    break;

                case State.Done:
                    break;
            }
        }

        public static bool IsDone(TwinsReworkOverride self)
        {
            return (State)self.BorderShotStateRaw == State.Done;
        }

        private static Vector2 GetArenaCenter(NPC npc, out float radius)
        {
            if (ArenaBorderSystem.ActiveBorders.Count > 0)
            {
                ArenaBorderSystem.Border b = ArenaBorderSystem.ActiveBorders[0];
                radius = b.Radius;
                return b.Center;
            }

            radius = 700f;
            return npc.Center;
        }

        // Muzzle flash dust doang - dijalanin di SEMUA client (bukan cuma server),
        // biar efek visualnya keliatan di mana-mana walau proyektilnya sendiri
        // tetap spawn cuma sekali dari sisi server.
        private static void SpawnMuzzleDust(Vector2 point)
        {
            for (int i = 0; i < 4; i++)
            {
                int d = Dust.NewDust(point, 2, 2, DustID.CursedTorch, 0f, 0f, 0, default, 1.4f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.4f;
            }
        }

        // ---- ENRAGED (Phase 3): burst 4-5 GreenBolt, MASING-MASING dari titik
        // RANDOM sendiri-sendiri di sepanjang tepi border (BUKAN dari 1 titik yang sama),
        // meluncur UMUMNYA ke arah tengah tapi arahnya di-random dalam rentang spread
        // lumayan lebar - jadi gak presisi nyasar ke 1 titik, dibiarin "nyebar" begitu udah
        // lewatin area tengah (pure luck pattern, gak predictable). ----
        private const float BorderFlameSpreadDegrees = 60f;

        private static void FireBorderCursedFlame(NPC npc, Vector2 arenaCenter, float arenaRadius)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int count = Main.rand.Next(CenterFlameMinCount, CenterFlameMaxCountInclusive + 1);

            for (int i = 0; i < count; i++)
            {
                // Titik spawn RANDOM di sepanjang tepi lingkaran border - independen dari
                // posisi Twin saat ini MAUPUN dari biji lain di burst yang sama, biar
                // beneran "pure luck" bukan predictable.
                float randomAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 spawnPoint = arenaCenter + randomAngle.ToRotationVector2() * arenaRadius;

                Vector2 towardCenter = (arenaCenter - spawnPoint).SafeNormalize(Vector2.UnitY);
                float halfSpread = MathHelper.ToRadians(BorderFlameSpreadDegrees) * 0.5f;
                float angleOffset = Main.rand.NextFloat(-halfSpread, halfSpread);
                Vector2 direction = towardCenter.RotatedBy(angleOffset);

                SpawnMuzzleDust(spawnPoint);

                int boltType = ModContent.ProjectileType<GreenBolt>();
                float angle = direction.ToRotation();

                // CATATAN: TIDAK ada .GetGlobalProjectile<TwinsDebuffGlobalProjectile>().IsFromTwins
                // — GreenBolt adalah custom ModProjectile, TIDAK terdaftar di AppliesToEntity.
                // Manggil GetGlobalProjectile pada tipe yang tidak terdaftar → KeyNotFoundException
                // → crash. GreenBolt handle debuffnya sendiri di OnHitPlayer() (Cursed Inferno),
                // sama persis pola RedPhantasmalBolt.
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    spawnPoint,
                    direction * GreenBolt.StartSpeed,
                    boltType,
                    TwinsReworkOverride.ScaleBoltDamage(GreenBoltDamage),
                    2f,
                    Main.myPlayer,
                    angle, // ai0: sudut arah terkunci (dibaca sendiri sama AI() GreenBolt)
                    0f     // ai1: counter tick buat kurva speed exponential, mulai dari 0
                );
            }
        }

        private static void FireBoltAt(NPC npc, TwinsReworkOverride self, Vector2 point, Vector2 arenaCenter)
        {
            SpawnMuzzleDust(point);

            Vector2 direction = (arenaCenter - point).SafeNormalize(Vector2.UnitY);

            // CATATAN: TriggerEyeFlash() SENGAJA TIDAK dipanggil di sini lagi - pattern
            // BorderShot sekarang nembak TANPA efek kedipan glow di wajah Retinazer,
            // beda dari LaserBarrage yang tetap pakai pulse itu.

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            float angle = direction.ToRotation();

            int boltType = ModContent.ProjectileType<RedPhantasmalBolt>();
            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                point,
                direction * RedPhantasmalBolt.StartSpeed,
                boltType,
                TwinsReworkOverride.ScaleBoltDamage(ShotDamage),
                1.5f,
                Main.myPlayer,
                angle, // ai0: sudut arah terkunci (dibaca sendiri sama AI() RedPhantasmalBolt)
                0f     // ai1: counter tick buat kurva speed exponential, mulai dari 0
            );
        }
    }
}
