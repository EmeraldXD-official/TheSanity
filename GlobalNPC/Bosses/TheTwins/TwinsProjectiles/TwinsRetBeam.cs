using Microsoft.Xna.Framework;
using Luminance.Core.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // PATTERN BARU: RETBEAM (Retinazer-themed laser beam)
    // ==========================================
    // Sengaja dipisah ke file ini sendiri (bukan nambahin ke TwinDash.cs) biar kalau ada
    // bug di pattern ini, gak ikut nyeret/nge-crash pattern dash yang udah jalan duluan.
    //
    // Alur pattern:
    //   1. Aiming    -> Twins diam di tempat, garis merah nge-AIM ke titik EKSTRAPOLASI posisi
    //                    player ke depan (pakai velocity player, bukan cuma posisi SAAT ITU),
    //                    jadi beneran nyoba "nyegat" arah player jalan, bukan lock/nempel di
    //                    player. Arahnya di-UPDATE tiap tick (BEDA sama telegraph dash yang
    //                    di-lock sekali di awal dan gak berubah lagi).
    //   2. FullBeam  -> garis jadi merah pekat solid (gak kedip lagi), arahnya LOCK/BEKU persis
    //                    di prediksi terakhir pas transisi dari Aiming (Twins gak lagi ngikutin
    //                    player selama fase ini), dan ini fase yang beneran ngedamage kalau
    //                    player kena garisnya. ENRAGED (Phase 3): prediksi di fase Aiming jauh
    //                    lebih SENSITIF ke gerakan player (lihat EnragedPredictionLeadTicks),
    //                    dan begitu masuk FullBeam, Twins diem TOTAL terkunci selama 0.5 detik
    //                    (garis full-beam solid udah keliatan dari awal) SEBELUM damage &
    //                    efek "lepas tembak" beneran mulai (lihat EnragedPreFireDelayTicks) -
    //                    ngasih player jendela buat dodge sebelum garisnya beneran nyakitin.
    //   3. Pas FullBeam abis -> dari sepanjang garis, tiap 6 block (96px), muncul sepasang
    //                    RedPhantasmalBolt (ModProjectile custom kita — dulu pakai
    //                    ProjectileID.DeathLaser/"RedLaser" vanilla, sekarang diganti biar
    //                    logic ramp speed + arah terkunci bisa nyatu di satu class) yang
    //                    nembak ke KANAN dan KIRI garis (tegak lurus arah beam) BARENGAN,
    //                    lurus non-homing, dengan kecepatan yang naik exponential.
    //                    Proyektilnya tileCollide = false, jadi tembus block.
    //   4. Kalau RetBeamRepeatsRemaining masih sisa, Twins GERAK CEPAT (bukan teleport instan)
    //                    ke titik random yang jauh dari player (Reposition), baru mulai Aiming
    //                    lagi begitu sampai (atau begitu Reposition timeout).
    //   5. Kalau abis (4-7x), masuk state Done -> dispatcher di TwinsRework.cs (lihat
    //                    CurrentPattern) otomatis gantian balik ke pattern Dash.
    //
    // CATATAN INTEGRASI: Start(npc, self) harus dipanggil SEKALI buat mulai pattern ini (misal
    // pas si pemilih pattern utama nanti milih "giliran RetBeam"). Abis itu, Tick(npc, self,
    // target) tinggal dipanggil tiap tick sama kayak TwinDash.Pattern1, sampai state-nya Done.
    public static class TwinsRetBeam
    {
        private enum State
        {
            Aiming,
            FullBeam,
            Reposition,
            Done
        }

        // ---- Tunable knobs ----
        public const float AimDuration = 50f;            // ~0.83 detik ngincer sambil ngikutin player
        private const float FullBeamDuration = 40f;      // ~0.67 detik beam solid ngedamage
        private const int MinRepeats = 4;
        private const int MaxRepeatsInclusive = 7;

        private const float BeamLength = 2600f;          // sama panjang kayak telegraph dash
        private const float BeamHitThickness = 24f;      // sedikit lebih lebar dari garis visual (8-16px), biar fair buat hitbox player
        private const int BeamDamage = 50; // RetBeam: diturunin ke 50 (manual .Hurt(), gak lewat auto-scaling engine)
        private const int DamageIntervalTicks = 10;       // biar gak ke-Hurt tiap tick pas kena beam

        private const float SpikeSpacing = 96f;          // 6 block (16px/block)
        private const int SpikeDamage = 13; // RedBolt: disamain sama base RedBolt di tempat lain

        // ==========================================
        // ENRAGED (Phase 3): "W" triple-beam — 2 beam tambahan di kanan-kiri beam tengah,
        // TITIK AWAL SAMA (npc.Center) tapi arahnya nyebar keluar sejauh SideSpreadAngle dari
        // arah tengah. Sisi kanan-kiri CUMA NGIKUTIN arah tengah (RetBeamAimDirection) - gak
        // punya prediksi/tracking sendiri, jadi kalau tengah masih di fase Aiming (predik
        // gerak player) dua beam sisi ini otomatis ikut geser bareng. Ketiganya sama-sama
        // damage & sama-sama muncrat RedPhantasmalBolt impact di FireSpikeWall.
        // Sengaja dibikin LEBAR (38 derajat) sesuai request "agak lebar yaa gap nya".
        // ==========================================
        public static readonly float SideSpreadAngle = MathHelper.ToRadians(38f);

        // ---- Spike-wall proyektil ----
        // Dulu pakai ProjectileID.DeathLaser (RedLaser) + GlobalProjectile hack buat ramp-up
        // kecepatan exponential-nya. SEKARANG diganti pakai RedPhantasmalBolt — ModProjectile
        // custom kita sendiri yang udah nyimpen logic ramp exponential + arah terkunci +
        // non-homing LANGSUNG di AI()-nya sendiri (lihat RedPhantasmalBolt.cs). Jadi di sini
        // gak perlu lagi konstanta SpikeStartSpeed/SpikeMaxSpeed/SpikeGrowthRate/SpikeMarkerValue
        // atau GlobalProjectile apa pun — cukup nembak proyektilnya ke arah yang bener (via
        // parameter ai0 = sudut radian), sisanya proyektil sendiri yang urus percepatannya.

        // Berapa tick ke depan posisi player di-ekstrapolasi buat nentuin arah aim. Ini yang
        // bikin garisnya beneran "predik" (nyegat ke arah player jalan), BUKAN cuma ngikutin
        // posisi player SAAT ITU (yang mana itu tetap kerasa kayak "lock" walau di-update tiap
        // tick, soalnya selalu telat 1 tick di belakang gerakan player).
        private const float PredictionLeadTicks = 26f;

        // ==========================================
        // ENRAGED (Phase 3): prediksi jauh LEBIH SENSITIF - lead time-nya digedein banyak
        // (26 -> 70 tick), jadi gerakan player yang keliatannya "dikit doang" (perubahan
        // velocity kecil) bisa nggeser TITIK PREDIKSI (makanya arah aim garisnya) lumayan
        // jauh - kalau player sempet ngerubah arah/kecepatan gerak pas fase Aiming, hasil
        // prediksinya bisa meleset sampai beberapa block (~5 block/80px ke atas,
        // tergantung seberapa jauh Twins dari player) dibanding versi normal yang lebih
        // "stabil"/gak segampang itu ke-influence sama gerakan kecil.
        // ==========================================
        private const float EnragedPredictionLeadTicks = 70f;

        // ==========================================
        // ENRAGED (Phase 3): begitu Aiming kelar & arah udah DIKUNCI, Twins gak langsung
        // mulai ngedamage - dia diem TOTAL dulu di lokasi itu (garis full-beam solid udah
        // keliatan dari tick pertama, jadi player bisa "baca" persis di mana bakal kena)
        // selama EnragedPreFireDelayTicks (0.5 detik) SEBELUM damage checks & efek "lepas
        // tembak" (sound/flash/shake) beneran dipicu - ini yang ngasih player jendela buat
        // dodge. Abis window ini lewat, sisanya jalan PERSIS kayak FullBeam normal
        // (FullBeamDuration tetap durasinya, cuma "ditempel" setelah jeda ini).
        // ==========================================
        private const float EnragedPreFireDelayTicks = 30f; // 0.5 detik (60 tick/detik)

        private const float RepositionMinDistanceFromPlayer = 500f; // titik tujuan jangan boleh deket player
        private const float RepositionMaxDistanceFromPlayer = 900f;
        // Dinaikin JAUH dari sebelumnya (26f -> 58f) - Twins sekarang pindah tempat abis
        // ngelepas laser jauh lebih GESIT/gercep. Masih murni gerak lewat velocity tiap tick
        // (BUKAN snap posisi/teleport), cuma angkanya digedein biar transisi antar-beam
        // kerasa cepat/agresif.
        private const float RepositionSpeed = 58f;           // kecepatan gerak pas pindah tempat (bukan teleport)
        private const float RepositionArriveThreshold = 40f; // dianggap "sampai" kalau udah sedeket ini ke titik tujuan
        private const float RepositionMaxDuration = 60f;     // timeout diperpendek (sejalan sama speed yang naik)

        // Ekstrapolasi posisi player PredictionLeadTicks (atau EnragedPredictionLeadTicks
        // pas enraged) ke depan pakai velocity player saat ini, terus arahin ke titik
        // prediksi itu — ini yang bikin garisnya "nyegat" ke depan arah jalan player, bukan
        // sekadar nempel di posisi player detik itu juga.
        private static Vector2 PredictAimDirection(NPC npc, Player target, bool enraged)
        {
            float leadTicks = enraged ? EnragedPredictionLeadTicks : PredictionLeadTicks;
            Vector2 predictedPos = target.Center + target.velocity * leadTicks;
            return (predictedPos - npc.Center).SafeNormalize(-Vector2.UnitY);
        }

        // Panggil SEKALI buat mulai pattern ini dari 0 (nentuin 4-7x ulangan).
        // Sekarang butuh "npc" juga (sebelumnya cuma "self") - dipakai buat mainin sound
        // "aim line keluar" (Zombie103) tepat di posisi Twins pas garis aim pertama nongol.
        public static void Start(NPC npc, TwinsReworkOverride self)
        {
            self.RetBeamRepeatsRemaining = Main.rand.Next(MinRepeats, MaxRepeatsInclusive + 1);
            self.RetBeamStateRaw = (float)State.Aiming;
            self.RetBeamTimer = 0f;
            self.RetBeamIsAiming = true;
            self.RetBeamIsFullBeam = false;

            // Aim line keluar -> COSTUME BeamCharged (dulu Zombie103, sound id internal Terraria).
            SoundEngine.PlaySound(TwinsSounds.BeamCharged, npc.Center);
        }

        // Panggil TIAP TICK selama pattern ini aktif (mirip TwinDash.Pattern1).
        public static void Tick(NPC npc, TwinsReworkOverride self, Player target)
        {
            State state = (State)self.RetBeamStateRaw;

            switch (state)
            {
                case State.Aiming:
                    // Twins diam di tempat — velocity di-decay ke 0, BUKAN di-set 0 langsung,
                    // biar gerakannya masih agak natural/gak nyentak kalau sebelumnya lagi gerak.
                    npc.velocity *= 0.8f;

                    // Arah di sini TERUS di-update tiap tick, tapi bukan sekadar ngincer posisi
                    // player SAAT ITU (itu masih kerasa "lock") — kita ekstrapolasi ke depan
                    // pakai velocity player, jadi garisnya beneran nyoba nyegat arah jalannya.
                    self.RetBeamAimDirection = PredictAimDirection(npc, target, self.IsEnraged);
                    npc.rotation = self.RetBeamAimDirection.ToRotation() - MathHelper.PiOver2;

                    self.RetBeamTimer++;
                    if (self.RetBeamTimer >= AimDuration)
                    {
                        self.RetBeamIsAiming = false;
                        self.RetBeamIsFullBeam = true;
                        self.RetBeamStateRaw = (float)State.FullBeam;
                        self.RetBeamTimer = 0f;
                        npc.netUpdate = true;

                        // Efek "RetLaser beneran lepas tembak" (sound/flash/shake) TIDAK lagi
                        // dipicu di sini langsung - dipindah ke dalam State.FullBeam di bawah,
                        // biar bisa ditunda kalau enraged (lihat EnragedPreFireDelayTicks).
                        // Buat non-enraged, efeknya tetap kepicu di tick PERTAMA FullBeam -
                        // jadi behavior lama gak berubah sama sekali.
                    }
                    break;

                case State.FullBeam:
                    npc.velocity *= 0.8f;
                    // Arah LOCK/BEKU begitu masuk full beam — gak di-recompute lagi di sini,
                    // tetap pakai self.RetBeamAimDirection persis nilai terakhir dari fase
                    // Aiming (prediksi terakhir). Twins commit ke arah itu; player masih bisa
                    // dodge kalau gerak, tapi Twins-nya sendiri gak ngoreksi arah lagi.
                    npc.rotation = self.RetBeamAimDirection.ToRotation() - MathHelper.PiOver2;

                    // ENRAGED: jeda "diam terkunci" 0.5 detik SEBELUM beneran mulai damage -
                    // buat non-enraged, preFireDelay = 0 jadi behave PERSIS kayak sebelumnya
                    // (damage & efek lepas tembak langsung di tick pertama FullBeam).
                    float preFireDelay = self.IsEnraged ? EnragedPreFireDelayTicks : 0f;
                    float totalFullBeamDuration = preFireDelay + FullBeamDuration;

                    if (self.RetBeamTimer >= preFireDelay)
                    {
                        // Trigger SEKALI, tepat pas window damage mulai (tick pertama setelah
                        // preFireDelay lewat) - ini "RetLaser beneran lepas tembak".
                        if ((int)self.RetBeamTimer == (int)preFireDelay)
                        {
                            SoundEngine.PlaySound(TwinsSounds.BeamShot, npc.Center); // COSTUME - suara RetLaser keluar
                            TwinsRetBeamFlash.Trigger();                          // flashbang merah non-solid
                            ScreenShakeSystem.StartShake(6f, 0.3f);               // sedikit screen shake
                        }

                        if ((int)(self.RetBeamTimer - preFireDelay) % DamageIntervalTicks == 0)
                        {
                            // Muzzle DIHITUNG SEKALI dari arah tengah - dipakai bareng buat
                            // ketiga garis (tengah + W-pattern), konsisten sama titik yang
                            // dipakai render-nya di TwinsRework.cs.
                            Vector2 muzzleOrigin = npc.Center + self.RetBeamAimDirection * TwinsReworkOverride.BeamMuzzleOffset;

                            DamageBeam(npc, muzzleOrigin, self.RetBeamAimDirection, target);

                            // ENRAGED: 2 beam sisi (W-shape) juga ngedamage, ngikutin arah tengah.
                            if (self.IsEnraged)
                            {
                                DamageBeam(npc, muzzleOrigin, self.RetBeamAimDirection.RotatedBy(SideSpreadAngle), target);
                                DamageBeam(npc, muzzleOrigin, self.RetBeamAimDirection.RotatedBy(-SideSpreadAngle), target);
                            }
                        }
                    }

                    self.RetBeamTimer++;
                    if (self.RetBeamTimer >= totalFullBeamDuration)
                    {
                        self.RetBeamIsFullBeam = false;

                        // Beam ilang -> dari sepanjang garis, semburin RedLaser ke kanan+kiri.
                        FireSpikeWall(npc, self.RetBeamAimDirection);

                        // ENRAGED: 2 beam sisi (W-shape) juga muncrat impact RedPhantasmalBolt
                        // sepanjang garisnya masing-masing, sama persis kayak beam tengah.
                        if (self.IsEnraged)
                        {
                            FireSpikeWall(npc, self.RetBeamAimDirection.RotatedBy(SideSpreadAngle));
                            FireSpikeWall(npc, self.RetBeamAimDirection.RotatedBy(-SideSpreadAngle));
                        }

                        self.RetBeamRepeatsRemaining--;
                        if (self.RetBeamRepeatsRemaining > 0)
                        {
                            self.RetBeamRepositionTarget = PickRepositionPoint(npc, target);
                            self.RetBeamStateRaw = (float)State.Reposition;
                        }
                        else
                        {
                            self.RetBeamStateRaw = (float)State.Done;
                        }

                        self.RetBeamTimer = 0f;
                        npc.netUpdate = true;
                    }
                    break;

                case State.Reposition:
                    // Gerak CEPAT ke titik tujuan (bukan teleport instan lagi) — dorong velocity
                    // ke arah titik tujuan tiap tick sampai deket, atau sampai kelamaan (timeout
                    // jaga-jaga biar gak nyangkut selamanya).
                    Vector2 toTarget = self.RetBeamRepositionTarget - npc.Center;
                    float distanceToTarget = toTarget.Length();

                    if (distanceToTarget > RepositionArriveThreshold)
                    {
                        Vector2 moveDirection = toTarget / distanceToTarget;
                        npc.velocity = moveDirection * RepositionSpeed;
                        npc.rotation = moveDirection.ToRotation() - MathHelper.PiOver2;
                    }
                    else
                    {
                        npc.velocity *= 0.5f; // udah nyampe, redam sisa laju
                    }

                    self.RetBeamTimer++;
                    if (distanceToTarget <= RepositionArriveThreshold || self.RetBeamTimer >= RepositionMaxDuration)
                    {
                        self.RetBeamStateRaw = (float)State.Aiming;
                        self.RetBeamIsAiming = true;
                        self.RetBeamTimer = 0f;
                        npc.netUpdate = true;

                        // Aim line keluar lagi buat repeat berikutnya -> COSTUME BeamCharged lagi.
                        SoundEngine.PlaySound(TwinsSounds.BeamCharged, npc.Center);
                    }
                    break;

                case State.Done:
                    // Pattern selesai (4-7x kelar). Belum ada pemilih pattern utama yang manggil
                    // Start(self) lagi otomatis — itu next step kalau mau digabung ke rotasi
                    // pattern bareng TwinDash.Pattern1.
                    break;
            }
        }

        // Dipanggil dispatcher (TwinsRework.cs) tiap tick selama RetBeam aktif, buat tahu
        // kapan seluruh siklus (4-7x aim+beam) udah kelar dan waktunya gantian balik ke Dash.
        public static bool IsDone(TwinsReworkOverride self)
        {
            return (State)self.RetBeamStateRaw == State.Done;
        }

        // Pilih titik acak yang jaraknya dijamin ada di antara RepositionMinDistanceFromPlayer
        // dan RepositionMaxDistanceFromPlayer dari player (dihitung RELATIF ke posisi player,
        // jadi otomatis gak mungkin milih titik "deket player"). Ini CUMA milih titik tujuan —
        // NPC-nya sendiri gerak ke situ pelan-pelan (relatif) tiap tick di state Reposition,
        // bukan langsung dipindah ke sini.
        private static Vector2 PickRepositionPoint(NPC npc, Player target)
        {
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            float distance = Main.rand.NextFloat(RepositionMinDistanceFromPlayer, RepositionMaxDistanceFromPlayer);
            Vector2 offset = Vector2.UnitX.RotatedBy(angle) * distance;

            return target.Center + offset;
        }

        // Cek jarak player ke garis beam (segment dari muzzleOrigin - titik moncong BERSAMA
        // buat KETIGA garis W-pattern, dihitung SEKALI dari arah tengah oleh caller - lihat
        // Tick() - BUKAN dihitung ulang per-garis dari arahnya sendiri-sendiri lagi, biar
        // ketiga garis beneran nongol dari 1 titik yang sama, sama kayak visualnya di
        // DrawRetBeamAimLine/FullBeamLine di TwinsRework.cs - sepanjang BeamLength searah
        // direction). Kalau di bawah BeamHitThickness, kena damage.
        private static void DamageBeam(NPC npc, Vector2 muzzleOrigin, Vector2 direction, Player target)
        {
            if (!target.active || target.dead)
                return;

            Vector2 segStart = muzzleOrigin;
            Vector2 segEnd = segStart + direction * BeamLength;

            float distance = DistancePointToSegment(target.Center, segStart, segEnd);
            if (distance <= BeamHitThickness)
            {
                // Beam ini nembak lewat target.Hurt() manual (BUKAN lewat proyektil beneran),
                // jadi ModifyHitPlayer gak ke-panggil otomatis - armor penetration & debuff
                // WAJIB di-apply manual persis di sini.
                target.Hurt(PlayerDeathReason.ByNPC(npc.whoAmI), BeamDamage, 0, armorPenetration: 999f);
                TwinsDebuffGlobalProjectile.ApplyDebuffs(target);
            }
        }

        private static float DistancePointToSegment(Vector2 point, Vector2 segStart, Vector2 segEnd)
        {
            Vector2 seg = segEnd - segStart;
            float lengthSquared = seg.LengthSquared();
            if (lengthSquared <= 0.0001f)
                return Vector2.Distance(point, segStart);

            float t = Vector2.Dot(point - segStart, seg) / lengthSquared;
            t = MathHelper.Clamp(t, 0f, 1f);
            Vector2 closest = segStart + seg * t;
            return Vector2.Distance(point, closest);
        }

        // Sepanjang garis beam, tiap SpikeSpacing (6 block), tembakin sepasang RedPhantasmalBolt
        // ke KANAN dan KIRI garis (tegak lurus arah beam) BARENGAN. Proyektil ini sendiri yang
        // udah pierce/tembus-block by default (lihat SetDefaults-nya) dan non-homing — arahnya
        // dikunci SEKALI di sini lewat parameter ai0 (sudut radian), gak pernah di-recompute
        // lagi selama hidupnya, jadi dijamin melesat lurus. Kecepatan awalnya juga gak perlu
        // kita atur manual (spawn velocity cuma dipakai buat frame pertama) — begitu AI()
        // proyektilnya jalan, dia langsung nge-ramp exponential sendiri dari StartSpeed sampai
        // MaxSpeed (lihat RedPhantasmalBolt.cs).
        private static void FireSpikeWall(NPC npc, Vector2 beamDirection)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 perpLeft = beamDirection.RotatedBy(MathHelper.PiOver2);
            Vector2 perpRight = -perpLeft;

            int boltType = ModContent.ProjectileType<RedPhantasmalBolt>();

            int steps = (int)(BeamLength / SpikeSpacing);
            for (int i = 1; i <= steps; i++)
            {
                Vector2 point = npc.Center + beamDirection * (i * SpikeSpacing);

                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    point,
                    perpLeft * RedPhantasmalBolt.StartSpeed,
                    boltType,
                    SpikeDamage,
                    1.5f,
                    Main.myPlayer,
                    perpLeft.ToRotation(), // ai0: sudut arah terkunci
                    0f                      // ai1: counter tick, mulai dari 0
                );

                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    point,
                    perpRight * RedPhantasmalBolt.StartSpeed,
                    boltType,
                    SpikeDamage,
                    1.5f,
                    Main.myPlayer,
                    perpRight.ToRotation(), // ai0: sudut arah terkunci
                    0f                       // ai1: counter tick, mulai dari 0
                );
            }
        }
    }
}
