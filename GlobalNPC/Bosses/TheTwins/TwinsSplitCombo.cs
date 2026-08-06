using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;
using TheSanity.Systems;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // PATTERN TERAKHIR: SPLIT COMBO
    // ==========================================
    // BEDA MENDASAR dari SEMUA pattern lain di file ini: pattern-pattern sebelumnya cuma
    // ngegerakin Spazmatism (leader), sedangkan Retinazer SELALU nempel 1:1 di posisi Spaz
    // (lihat blok mirror di PreAI/FindFrame TwinsRework.cs - npc.Center = spaz.Center tiap
    // tick, dontTakeDamage = true permanen, sprite-nya digambar manual numpuk di atas Spaz).
    //
    // Pattern INI pertama kalinya Retinazer BENERAN LEPAS dari Spaz dan gerak sendiri:
    //   1. Separating   -> Spaz & Ret terbang ke arah BERLAWANAN (dari titik mereka sekarang,
    //                       sepanjang 1 garis lurus lewat pusat arena) sampai masing-masing
    //                       nyampe TEPI BORDER di sisinya sendiri-sendiri. Gerak pakai
    //                       velocity (lerp ke arah tujuan), BUKAN teleport/snap.
    //   2. Circling     -> begitu dua-duanya nyampe, mereka muter BARENGAN sepanjang tepi
    //                       arena - SELALU di sisi yang PERSIS berlawanan satu sama lain
    //                       (180 derajat terpisah) - sambil terus MENGHADAP KE PLAYER di
    //                       tengah. Tiap 1 detik PERSIS, Spazmatism nembak SATU
    //                       CursedFlameHostile dan Retinazer nembak SATU RedPhantasmalBolt,
    //                       DUA-DUANYA di tick yang SAMA (combo/serentak). Radius muternya
    //                       PERLAHAN MENGECIL seiring putaran (bukan diam di radius penuh
    //                       terus, juga bukan collapse instan ke tengah) - jadi kesannya
    //                       lingkaran yang lambat laun "mendekat/nyempit" ke player. Diulang
    //                       3-6 putaran penuh (di-random tiap Start()), kecepatan sudutnya
    //                       SENGAJA lebih lambat dari TwinsBorderShot.
    //   3. Regrouping   -> abis putaran kelar, Retinazer terbang BALIK ke posisi Spazmatism
    //                       (yang diem di titik terakhirnya) - lagi-lagi pakai velocity,
    //                       bukan snap - sampai jaraknya cukup deket buat dianggap "nempel
    //                       lagi".
    //   4. Done         -> dispatcher (TwinsRework.cs) matiin ComboSplitActive, Retinazer
    //                       balik ke mode mirror normal (invincible, nempel Spaz lagi), lalu
    //                       gantian ke pattern berikutnya (Dash, muter rotasi dari awal lagi).
    //
    // VULNERABILITY: Retinazer TETAP invincible (dontTakeDamage = true) selama Separating
    // MAUPUN Regrouping - CUMA jadi bisa kena damage beneran SELAMA Circling (state serangan
    // combo-nya doang), sesuai request. HP-nya TETAP 1 pool bareng Spazmatism (Spazmatism
    // tetap sumber kebenaran) - lihat komentar panjang di HandleVulnerability soal caranya
    // damage yang kena badan Retinazer di-"transfer" balik ke npc.life Spazmatism, bukan
    // bikin health bar/pool kedua yang terpisah.
    //
    // INTEGRASI: dipanggil dari dispatcher normal (TwinsRework.cs, SEBAGAI PATTERN PALING
    // TERAKHIR di rotasi - LaserBarrage -> SplitCombo -> balik ke Dash) DAN dari attack-loop
    // Last Stand (TwinsLastStand.cs, di posisi yang sama - abis LaserBarrage, sebelum
    // checkpoint budget waktu master).
    // ==========================================
    public static class TwinsSplitCombo
    {
        private enum State
        {
            Separating,
            Circling,
            Regrouping,
            Done
        }

        // ---- Tunable knobs ----
        private const float SeparateSpeed = 18f;
        private const float SeparateArriveThreshold = 40f;
        private const float SeparateMaxDuration = 200f; // safety timeout ~3.3 detik biar gak nyangkut

        // SENGAJA lebih lambat dari TwinsBorderShot (yang 240 tick / ~4 detik per putaran) -
        // request eksplisit "kecepatan nya lebih lambat dari Border shot pattern".
        private const float LapDurationTicks = 380f; // ~6.3 detik per putaran penuh

        private const int MinLaps = 3;
        private const int MaxLapsInclusive = 6;

        // Radius muter PERLAHAN mengecil dari radius penuh (nyampe tepi border pas mulai
        // Circling) turun ke RadiusShrinkFactor dari situ di akhir putaran terakhir - bukan
        // diam di radius penuh terus-terusan, juga bukan collapse instan ke tengah.
        private const float RadiusShrinkFactor = 0.55f;

        private const int FireIntervalTicks = 60; // PERSIS 1 detik (60 tick/detik) - "setiap detiknya bersamaan"
        private const float MouthOffset = 24f;
        private const float CursedFlameSpeed = 8f;
        private const int CursedFlameDamage = 5; // Curse Bolt/Curse Ball: target 15 DMG Master / 3 (engine auto-triples proyektil di Master mode)
        private const int RedBoltDamage = 13;    // RedBolt: target 40 DMG Master / 3 (engine auto-triples proyektil di Master mode)

        private const float RegroupSpeed = 18f;
        private const float RegroupArriveThreshold = 50f;
        private const float RegroupMaxDuration = 200f; // safety timeout jaga-jaga

        // Retinazer DIKASIH buffer nyawa gede banget SELAMA Circling (vulnerable) - ini
        // BUKAN nyawa "beneran" dia, cuma numpang biar vanilla checkDead() gak keburu
        // ke-trigger DI TENGAH tick (hit-resolution vanilla langsung jalan begitu proyektil
        // player nyentuh, bukan nunggu tick AI berikutnya) sebelum sempat kita "transfer"
        // damage-nya balik ke npc.life Spazmatism (lihat HandleVulnerability). Kalau
        // Retinazer beneran dibiarin pakai lifeMax asli Twins terus kena damage numpuk
        // dalam 1 tick sampai lifeMax segitu abis, dia bisa mati SENDIRIAN lewat jalur
        // vanilla (OnKill/NPCLoot Retinazer biasa) SEBELUM kode transfer kita sempat jalan -
        // itu bakal ngerusak seluruh pattern (Ret ilang duluan padahal Spaz masih hidup).
        private const int VulnerableLifeBuffer = 5_000_000;

        public static void Start(NPC spaz, TwinsReworkOverride self)
        {
            self.ComboSplitStateRaw = (float)State.Separating;
            self.ComboSplitTimer = 0f;
            self.ComboSplitSpinAngle = 0f;
            self.ComboSplitFireTimer = 0f;
            self.ComboSplitLapsRequired = Main.rand.Next(MinLaps, MaxLapsInclusive + 1);

            Vector2 arenaCenter = GetArenaCenter(spaz, out float arenaRadius);
            self.ComboSplitArenaCenter = arenaCenter;
            self.ComboSplitRadius = arenaRadius;

            // Sumbu pisah di-random SEKALI di sini - Spaz nuju satu ujung, Ret nuju ujung yang
            // PERSIS berlawanan (+PI radian) di sisi lain arena, jadi mereka beneran "kek
            // berlawanan arah menjauh" dari 1 garis lurus yang sama lewat tengah.
            self.ComboSplitStartAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            self.ComboSplitSpazApproachTarget = arenaCenter + self.ComboSplitStartAngle.ToRotationVector2() * arenaRadius;
            self.ComboSplitRetApproachTarget = arenaCenter + (self.ComboSplitStartAngle + MathHelper.Pi).ToRotationVector2() * arenaRadius;

            // Gerbang utama: begitu true, PreAI/FindFrame/PreDraw Retinazer di TwinsRework.cs
            // otomatis pindah ke jalur independen (lihat komentar di sana) - TIDAK lagi
            // nempel 1:1 ke Spaz sampai pattern ini Done.
            self.ComboSplitActive = true;
            self.ComboSplitVulnerable = false; // masih invincible selama Separating
        }

        // Dipanggil dari dispatcher Spazmatism (leader) tiap tick, sama kayak pattern lain.
        // Urusan gerak/state Retinazer ADA DI FUNGSI TERPISAH (RetinazerTick di bawah),
        // dipanggil dari PreAI Retinazer sendiri - tapi state-machine-nya SATU-SATUNYA
        // "sumber kebenaran" tetap di sini (field-field di self, instance milik Spazmatism),
        // Retinazer cuma MEMBACA state itu & ngikutin, gak pernah mengubahnya sendiri -
        // sama filosofi "leader/fellow" yang dipakai di seluruh file ini.
        public static void Tick(NPC spaz, TwinsReworkOverride self, Player target)
        {
            NPC ret = FindRetinazer();
            State state = (State)self.ComboSplitStateRaw;

            switch (state)
            {
                case State.Separating:
                    {
                        self.ComboSplitTimer++;

                        bool spazArrived = MoveToward(spaz, self.ComboSplitSpazApproachTarget, SeparateSpeed, target, out _);
                        bool retArrived = ret == null || MoveTowardStatic(ret, self.ComboSplitRetApproachTarget, SeparateSpeed, target);

                        bool timedOut = self.ComboSplitTimer >= SeparateMaxDuration;

                        // Dua-duanya WAJIB udah nyampe (atau timeout jaga-jaga) sebelum mulai
                        // muter bareng - biar gak ada satu Twin yang udah muter duluan
                        // sementara satunya masih di jalan.
                        if ((spazArrived && retArrived) || timedOut)
                        {
                            spaz.Center = self.ComboSplitSpazApproachTarget;
                            spaz.velocity = Vector2.Zero;

                            self.ComboSplitSpinAngle = 0f;
                            self.ComboSplitTimer = 0f;
                            self.ComboSplitFireTimer = 0f;
                            self.ComboSplitVulnerable = true; // dari sini Retinazer mulai bisa kena damage beneran
                            self.ComboSplitStateRaw = (float)State.Circling;
                            spaz.netUpdate = true;
                        }
                        break;
                    }

                case State.Circling:
                    {
                        float angularSpeed = MathHelper.TwoPi / LapDurationTicks;
                        self.ComboSplitSpinAngle += angularSpeed;

                        float requiredAngle = MathHelper.TwoPi * self.ComboSplitLapsRequired;
                        float progress = MathHelper.Clamp(self.ComboSplitSpinAngle / requiredAngle, 0f, 1f);

                        // Radius PERLAHAN mengecil seiring progres putaran - lingkaran nyempit
                        // sedikit demi sedikit, bukan diam di radius penuh atau collapse instan.
                        float fullRadius = GetArenaRadiusOnly(spaz);
                        self.ComboSplitRadius = MathHelper.Lerp(fullRadius, fullRadius * RadiusShrinkFactor, progress);

                        float spazAngle = self.ComboSplitStartAngle + self.ComboSplitSpinAngle;
                        spaz.Center = self.ComboSplitArenaCenter + spazAngle.ToRotationVector2() * self.ComboSplitRadius;
                        spaz.velocity = Vector2.Zero;

                        // Selalu menghadap ke player di tengah selama muter - bukan ngikutin
                        // arah tangensial kayak BorderShot.
                        Vector2 aimVector = target.Center - spaz.Center;
                        spaz.rotation = aimVector.ToRotation() - MathHelper.PiOver2;

                        self.ComboSplitTimer++;
                        self.ComboSplitFireTimer++;

                        // Tembakan combo: CursedFlame dari Spaz + RedBolt dari Ret, PERSIS di
                        // tick yang sama, tiap 1 detik penuh.
                        if (self.ComboSplitFireTimer >= FireIntervalTicks)
                        {
                            self.ComboSplitFireTimer = 0f;
                            FireComboBolts(spaz, ret, target);
                        }

                        if ((int)self.ComboSplitTimer % 10 == 0)
                            spaz.netUpdate = true;

                        if (self.ComboSplitSpinAngle >= requiredAngle)
                        {
                            self.ComboSplitVulnerable = false; // Ret balik invincible dari sini
                            self.ComboSplitTimer = 0f;
                            self.ComboSplitStateRaw = (float)State.Regrouping;
                            spaz.netUpdate = true;
                        }
                        break;
                    }

                case State.Regrouping:
                    {
                        // Spaz diem di titik terakhirnya (cukup redam sisa laju kalau ada) -
                        // Retinazer yang terbang BALIK ke sini (lihat RetinazerTick), biar cuma
                        // 1 pihak yang gerak & gampang dipastikan mereka beneran ketemu.
                        spaz.velocity *= 0.9f;

                        Vector2 aimVector = target.Center - spaz.Center;
                        spaz.rotation = aimVector.ToRotation() - MathHelper.PiOver2;

                        self.ComboSplitTimer++;

                        bool retBack = ret == null
                            || Vector2.DistanceSquared(ret.Center, spaz.Center) <= RegroupArriveThreshold * RegroupArriveThreshold;
                        bool timedOut = self.ComboSplitTimer >= RegroupMaxDuration;

                        if (retBack || timedOut)
                        {
                            self.ComboSplitStateRaw = (float)State.Done;
                            self.ComboSplitActive = false; // Retinazer balik ke mode mirror normal mulai tick berikutnya
                            self.ComboSplitVulnerable = false;
                            spaz.netUpdate = true;
                        }
                        break;
                    }

                case State.Done:
                    break;
            }
        }

        public static bool IsDone(TwinsReworkOverride self)
        {
            return (State)self.ComboSplitStateRaw == State.Done;
        }

        // ==========================================
        // Dipanggil dari PreAI Retinazer sendiri (TwinsRework.cs), SETIAP TICK selama
        // spazSelf.ComboSplitActive true - satu-satunya tempat Retinazer punya gerakan
        // independen di SELURUH file ini. "self" di sini adalah instance GlobalNPC milik
        // Retinazer SENDIRI (dipakai buat nyimpen bookkeeping damage-nya sendiri), beda dari
        // spazSelf (instance milik Spazmatism, si "sumber kebenaran" state pattern).
        // ==========================================
        public static void RetinazerTick(NPC ret, NPC spaz, TwinsReworkOverride spazSelf, TwinsReworkOverride retSelf, Player target)
        {
            State state = (State)spazSelf.ComboSplitStateRaw;

            switch (state)
            {
                case State.Separating:
                    MoveTowardStatic(ret, spazSelf.ComboSplitRetApproachTarget, SeparateSpeed, target);
                    break;

                case State.Circling:
                    {
                        // SELALU di sisi yang PERSIS berlawanan sama Spaz (+PI radian), radius &
                        // progres putaran ngikutin field yang sama punya Spaz (satu-satunya
                        // sumber kebenaran), biar dua-duanya konsisten kompak.
                        float retAngle = spazSelf.ComboSplitStartAngle + spazSelf.ComboSplitSpinAngle + MathHelper.Pi;
                        ret.Center = spazSelf.ComboSplitArenaCenter + retAngle.ToRotationVector2() * spazSelf.ComboSplitRadius;
                        ret.velocity = Vector2.Zero;

                        Vector2 aimVector = target.Center - ret.Center;
                        ret.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
                        break;
                    }

                case State.Regrouping:
                case State.Done:
                    // Balik ngejar posisi Spaz LIVE (Spaz diem di Regrouping, jadi ini konvergen
                    // rapi, bukan ngejar target yang terus lari).
                    MoveTowardStatic(ret, spaz.Center, RegroupSpeed, target);
                    break;
            }

            HandleVulnerability(ret, spaz, spazSelf, retSelf);
        }

        // ==========================================
        // VULNERABILITY / DAMAGE TRANSFER
        // ==========================================
        // Retinazer CUMA vulnerable (dontTakeDamage = false) SELAMA spazSelf.ComboSplitVulnerable
        // true (state Circling doang). Karena HP "beneran" tetap 1 pool milik Spazmatism,
        // damage yang beneran ngurangin npc.life Retinazer di sini di-"transfer" balik ke
        // npc.life Spazmatism tiap kali kedeteksi turun - jadi kesannya "1 nyawa dibagi 2
        // badan", bukan 2 pool nyawa terpisah. Lihat VulnerableLifeBuffer di atas kenapa
        // Retinazer dikasih nyawa "dummy" gede banget selama window ini.
        // ==========================================
        private static void HandleVulnerability(NPC ret, NPC spaz, TwinsReworkOverride spazSelf, TwinsReworkOverride retSelf)
        {
            if (spazSelf.ComboSplitVulnerable)
            {
                ret.dontTakeDamage = false;

                if (!retSelf.ComboSplitDamageTrackingInit)
                {
                    ret.lifeMax = VulnerableLifeBuffer;
                    ret.life = VulnerableLifeBuffer;
                    retSelf.ComboSplitLastTrackedLife = VulnerableLifeBuffer;
                    retSelf.ComboSplitDamageTrackingInit = true;
                }

                if (ret.life < retSelf.ComboSplitLastTrackedLife)
                {
                    int damageTaken = retSelf.ComboSplitLastTrackedLife - ret.life;
                    retSelf.ComboSplitLastTrackedLife = ret.life;

                    spaz.life = System.Math.Max(0, spaz.life - damageTaken);
                    spaz.netUpdate = true;

                    if (spaz.life <= 0)
                    {
                        spaz.life = 0;
                        spaz.HitEffect(0, 10);
                        spaz.checkDead(); // OnKill (TwinsRework.cs) otomatis masak-masakin Retinazer ikut mati bareng
                    }
                }
            }
            else
            {
                // Di luar window Circling (Separating/Regrouping) - tetap invincible kayak
                // biasa, dan npc.life/lifeMax-nya di-resync balik ngikutin Spazmatism biar gak
                // nyisa nilai buffer dummy yang gede banget itu kebaca aneh di tempat lain.
                ret.dontTakeDamage = true;
                ret.lifeMax = spaz.lifeMax;
                ret.life = spaz.life;
                retSelf.ComboSplitDamageTrackingInit = false;
            }
        }

        // ==========================================
        // HELPERS
        // ==========================================
        private static NPC FindRetinazer()
        {
            int index = NPC.FindFirstNPC(NPCID.Retinazer);
            return index != -1 ? Main.npc[index] : null;
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

        private static float GetArenaRadiusOnly(NPC npc)
        {
            GetArenaCenter(npc, out float radius);
            return radius;
        }

        // Gerak velocity-lerp ke arah target, TIDAK PERNAH teleport - dipakai dua-duanya
        // (Spaz & Ret). Return true begitu udah dianggap "nyampe" (dalam threshold).
        // Overload versi Spaz (perlu tau apa dia udah nyampe biar Tick() bisa nunggu Ret
        // juga sebelum lanjut state).
        private static bool MoveToward(NPC npc, Vector2 destination, float speed, Player target, out float distanceOut)
        {
            Vector2 toTarget = destination - npc.Center;
            float distance = toTarget.Length();
            distanceOut = distance;

            if (distance <= SeparateArriveThreshold)
                return true;

            Vector2 moveDirection = toTarget / distance;
            npc.velocity = Vector2.Lerp(npc.velocity, moveDirection * speed, 0.12f);

            Vector2 aimVector = target.Center - npc.Center;
            npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
            return false;
        }

        // Versi buat Retinazer (dipanggil dari RetinazerTick, tanpa perlu tau jarak persisnya
        // ke pemanggil) - dipakai baik buat Separating (nuju RetApproachTarget) maupun
        // Regrouping (nuju posisi Spaz yang lagi diem). Threshold arrival-nya generik,
        // aman dipakai di dua konteks itu.
        private static bool MoveTowardStatic(NPC npc, Vector2 destination, float speed, Player target)
        {
            Vector2 toTarget = destination - npc.Center;
            float distance = toTarget.Length();

            if (distance <= SeparateArriveThreshold)
            {
                npc.velocity *= 0.5f;
                return true;
            }

            Vector2 moveDirection = toTarget / distance;
            npc.velocity = Vector2.Lerp(npc.velocity, moveDirection * speed, 0.12f);

            Vector2 aimVector = target.Center - npc.Center;
            npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
            return false;
        }

        // Tembakan combo simultan: CursedFlame dari Spaz, RedPhantasmalBolt dari Ret - DUA-
        // DUANYA diarahkan ke posisi player LIVE saat itu (dua-duanya lagi "ngeliatin" player
        // di tengah selama Circling).
        private static void FireComboBolts(NPC spaz, NPC ret, Player target)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 spazDir = (target.Center - spaz.Center).SafeNormalize(-Vector2.UnitY);
            Vector2 spazMuzzle = spaz.Center + spazDir * MouthOffset;

            int flameIndex = Projectile.NewProjectile(
                spaz.GetSource_FromAI(),
                spazMuzzle,
                spazDir * CursedFlameSpeed,
                ProjectileID.CursedFlameHostile,
                CursedFlameDamage,
                2f,
                Main.myPlayer
            );
            Main.projectile[flameIndex].GetGlobalProjectile<TwinsDebuffGlobalProjectile>().IsFromTwins = true;

            if (ret == null || !ret.active)
                return;

            Vector2 retDir = (target.Center - ret.Center).SafeNormalize(-Vector2.UnitY);
            Vector2 retMuzzle = ret.Center + retDir * MouthOffset;

            int boltType = ModContent.ProjectileType<RedPhantasmalBolt>();
            Projectile.NewProjectile(
                ret.GetSource_FromAI(),
                retMuzzle,
                retDir * RedPhantasmalBolt.StartSpeed,
                boltType,
                RedBoltDamage,
                1.5f,
                Main.myPlayer,
                retDir.ToRotation(), // ai0: sudut arah terkunci
                0f                   // ai1: counter tick exponential
            );
        }
    }
}
