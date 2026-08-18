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
    //                       (180 derajat terpisah). Kecepatan sudutnya SAMA PERSIS kayak
    //                       TwinsBorderShot (request: "kecepatan manuver nya setara dengan
    //                       Border Shot"). Tiap 0,3 DETIK PERSIS, Spazmatism & Retinazer
    //                       nembak BARENGAN, DI TICK YANG SAMA (combo/serentak):
    //                         - Normal (belum Enraged): 1 GreenBolt dari Spaz + 1
    //                           RedPhantasmalBolt dari Ret.
    //                         - ENRAGED (Phase 2/3, setelah Spectre mati - lihat IsEnraged):
    //                           MASING-MASING nembak 3 SEKALIGUS (3 CursedFlame + 3
    //                           RedBolt, di tick yang SAMA juga) - satu LURUS ke tengah, dua
    //                           nyebar ke kanan/kiri dari situ ("kek huruf W").
    //                       Radius muternya DIAM PERSIS di tepi border (pojok border)
    //                       SELAMA SEMUA putaran - BUKAN pelan-pelan nyempit ke tengah di
    //                       tengah-tengah durasi (request: "mau nya mendekat pas dah abis
    //                       aja timer nya"). "Mendekat"-nya baru kejadian SETELAH seluruh
    //                       putaran (5-9x) kelar, lewat state Regrouping di bawah - jumlah
    //                       putarannya SAMA rentangnya baik normal maupun Enraged/Last Stand.
    //   3. Regrouping   -> abis putaran kelar, Retinazer terbang BALIK ke posisi Spazmatism
    //                       (yang diem di titik terakhirnya) - lagi-lagi pakai velocity,
    //                       bukan snap - sampai jaraknya cukup deket buat dianggap "nempel
    //                       lagi".
    //   4. Done         -> dispatcher (TwinsRework.cs) matiin ComboSplitActive, Retinazer
    //                       balik ke mode mirror normal (invincible, nempel Spaz lagi), lalu
    //                       gantian ke pattern berikutnya (Dash, muter rotasi dari awal lagi).
    //
    // ARAH HADAP: SELAMA SELURUH pattern ini (Separating, Circling, MAUPUN Regrouping),
    // Spaz & Ret SELALU menghadap TITIK TENGAH ARENA (bukan ngikutin posisi player LIVE
    // kayak pattern lain) - request eksplisit "buat Twins nya menghadap tengah arena
    // terus". Tembakan combo-nya juga diarahkan ke tengah arena (dari posisi masing-masing
    // Twin saat itu), BUKAN diarahkan ke player - pattern ini murni geometris/area-lock,
    // gak nge-track player sama sekali.
    //
    // VULNERABILITY: Retinazer TETAP invincible (dontTakeDamage = true) selama Separating
    // MAUPUN Regrouping - CUMA jadi bisa kena damage beneran SELAMA Circling (state serangan
    // combo-nya doang), sesuai request. HP-nya TETAP 1 pool bareng Spazmatism (Spazmatism
    // tetap sumber kebenaran) - lihat komentar panjang di HandleVulnerability soal caranya
    // damage yang kena badan Retinazer di-"transfer" balik ke npc.life Spazmatism, bukan
    // bikin health bar/pool kedua yang terpisah.
    //
    // RENDER: Retinazer TIDAK digambar lewat draw vanilla (PreDraw-nya SELALU return false,
    // sama kayak seumur hidup fight) - SELAMA independen (ComboSplitActive), dia tetap
    // digambar MANUAL dari PreDraw Spazmatism, cuma sekarang di POSISINYA SENDIRI (ret.Center)
    // bukan numpuk di posisi Spaz lagi. Lihat komentar di TwinsRework.cs PreDraw kenapa ini
    // WAJIB manual (return true / vanilla draw sempat bikin NullReferenceException nge-crash
    // render NPC - Retinazer "hilang" - di beberapa modpack/hook chain pihak ketiga).
    //
    // INTEGRASI: dipanggil dari dispatcher normal (TwinsRework.cs, SEBAGAI PATTERN PALING
    // TERAKHIR di rotasi - LaserBarrage -> SplitCombo -> balik ke Dash) DAN dari attack-loop
    // Last Stand (TwinsLastStand.cs, di posisi yang sama - abis LaserBarrage, sebelum
    // checkpoint budget waktu master). Last Stand HANYA BISA aktif kalau IsEnraged sudah
    // true (lihat TwinsLastStand.CheckActivationCondition) - jadi versi yang jalan di Last
    // Stand OTOMATIS selalu versi Enraged (triple-shot W), gak perlu flag terpisah lagi.
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

        // SAMA PERSIS kayak TwinsBorderShot.LapDurationTicks - request eksplisit "kecepatan
        // manuver nya setara dengan Border Shot".
        private const float LapDurationTicks = 240f; // ~4 detik per putaran penuh

        // 5-9 putaran penuh, BERLAKU DI SEMUA FASE (normal maupun Enraged/Last Stand) - "jumlah
        // berputar nya jadi 5-9x (all phase)".
        private const int MinLaps = 5;
        private const int MaxLapsInclusive = 9;

        // CATATAN: dulu di sini ada radius yang PERLAHAN mengecil seiring progres putaran -
        // DIHAPUS per request ("kan aku mau nya mendekat pas dah abis aja timer nya"). Sekarang
        // radius Circling DIAM PERSIS di tepi border (pojok border) SELAMA SEMUA putaran, gak
        // nyempit sedikit pun sampai putaran ke-N (LapsRequired) beneran kelar - baru abis itu
        // pattern lanjut ke Regrouping (Retinazer terbang balik ke Spaz).

        private const int FireIntervalTicks = 18; // 0,3 detik (60 tick/detik x 0,3) - normal/Phase 1, TETAP SAMA
        // ENRAGED (Phase 2/3 & Last Stand): interval nembak DIPERLAMBAT jadi 0,5 detik -
        // kompensasi karena tiap tembakan enraged udah 3x lipat proyektil (formasi "W"),
        // request eksplisit "durasi nembak nya jadi per 0,5 detik, yang pas phase 1 mah
        // masih sama".
        private const int EnragedFireIntervalTicks = 18; // disamain ke FireIntervalTicks (0,3 detik) per request —
        // Enraged tetap 3x proyektil (formasi "W") tapi interval nembaknya sama cepetnya kayak Phase 1,
        // jadi DPS-nya 3x lipat dari sebelumnya (bukan cuma 3/0.5 * 1/0.3 = 1.8x).
        private const float MouthOffset = 24f;
        private const int GreenBoltDamage = 10; // GreenBolt: target ~30 DMG Master / 3 (engine auto-triples proyektil di Master mode) - +20% otomatis kalau Last Stand
        private const int RedBoltDamage = 10;    // RedBolt: target ~30 DMG Master / 3 (engine auto-triples proyektil di Master mode) - +20% otomatis kalau Last Stand

        // ---- ENRAGED (Phase 2/3): 3 proyektil sekaligus per Twin, bentuk "W" (1 lurus ke
        // tengah + 2 nyebar kanan-kiri) - SELARAS sama timing normal (tetap tiap 1 detik,
        // tetap di tick yang sama antara Spaz & Ret), cuma jumlahnya jadi 3x lipat. ----
        private const int EnragedProjectileCount = 3;
        private const float ComboSpreadDegrees = 40f; // total sebaran "W", bisa diubah

        private const float RegroupSpeed = 18f;
        private const float RegroupArriveThreshold = 50f;
        private const float RegroupMaxDuration = 200f; // safety timeout jaga-jaga

        // CATATAN: dulu di sini ada VulnerableLifeBuffer (angka nyawa dummy raksasa,
        // 5,000,000) yang di-pompa ke npc.life/lifeMax Retinazer SELAMA vulnerable - itu
        // yang bikin bug "health bar Retinazer nunjukin 500k". DIHAPUS TOTAL. Sekarang
        // Retinazer pakai npc.life/lifeMax REAL (mirror persis dari Spazmatism) bahkan
        // SELAMA vulnerable - amannya dijamin dari sisi LAIN: TwinsRework.cs (GlobalNPC)
        // override CheckDead() buat Retinazer, MEMBATALKAN proses "mati sendiri" dia
        // SELAMA ComboSplitVulnerable true, APAPUN nilai life-nya. Jadi gak perlu lagi
        // trik buffer dummy - angka yang keliatan di health bar (mod manapun yang baca)
        // SELALU angka asli & masuk akal.

        public static void Start(NPC spaz, TwinsReworkOverride self)
        {
            self.ComboSplitStateRaw = (float)State.Separating;
            self.ComboSplitTimer = 0f;
            self.ComboSplitSpinAngle = 0f;
            self.ComboSplitFireTimer = 0f;

            // FIX (request "Last Stand pakai jumlah repeat paling maks"): SELAMA Last Stand
            // (self.LastStandActive true), skip random & pakai MaxLapsInclusive langsung -
            // sama pola kayak TwinDash.DashRepeatsRemaining. Di luar Last Stand tetap random
            // 5-9x seperti biasa (lihat catatan MinLaps/MaxLapsInclusive di atas).
            self.ComboSplitLapsRequired = self.LastStandActive ? MaxLapsInclusive : Main.rand.Next(MinLaps, MaxLapsInclusive + 1);

            Vector2 arenaCenter = GetArenaCenter(spaz, out float arenaRadius);
            self.ComboSplitArenaCenter = arenaCenter;
            self.ComboSplitRadius = arenaRadius;

            // Sumbu pisah di-random SEKALI di sini - Spaz nuju satu ujung, Ret nuju ujung yang
            // PERSIS berlawanan (+PI radian) di sisi lain arena, jadi mereka beneran "kek
            // berlawanan arah menjauh" dari 1 garis lurus yang sama lewat tengah.
            self.ComboSplitStartAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            self.ComboSplitSpazApproachTarget = arenaCenter + self.ComboSplitStartAngle.ToRotationVector2() * arenaRadius;
            self.ComboSplitRetApproachTarget = arenaCenter + (self.ComboSplitStartAngle + MathHelper.Pi).ToRotationVector2() * arenaRadius;

            // Gerbang utama: begitu true, PreAI Retinazer di TwinsRework.cs otomatis pindah
            // ke jalur independen (lihat komentar di sana) - TIDAK lagi nempel 1:1 ke Spaz
            // sampai pattern ini Done.
            self.ComboSplitActive = true;
            self.ComboSplitVulnerable = false; // masih invincible selama Separating
        }

        // Dipanggil dari dispatcher Spazmatism (leader) tiap tick, sama kayak pattern lain.
        // Urusan gerak/state Retinazer ADA DI FUNGSI TERPISAH (RetinazerTick di bawah),
        // dipanggil dari PreAI Retinazer sendiri - tapi state-machine-nya SATU-SATUNYA
        // "sumber kebenaran" tetap di sini (field-field di self, instance milik Spazmatism),
        // Retinazer cuma MEMBACA state itu & ngikutin, gak pernah mengubahnya sendiri -
        // sama filosofi "leader/fellow" yang dipakai di seluruh file ini.
        //
        // CATATAN "target": parameter Player di sini DIPERTAHANKAN cuma biar signature-nya
        // konsisten sama pattern lain (dispatcher manggilnya seragam) - pattern ini SENGAJA
        // TIDAK nge-track posisi player sama sekali (lihat komentar header soal "menghadap
        // tengah arena terus"), jadi parameter ini gak dipakai buat aiming di sini.
        public static void Tick(NPC spaz, TwinsReworkOverride self, Player target)
        {
            NPC ret = FindRetinazer();
            State state = (State)self.ComboSplitStateRaw;
            Vector2 arenaCenter = self.ComboSplitArenaCenter;

            switch (state)
            {
                case State.Separating:
                    {
                        self.ComboSplitTimer++;

                        bool spazArrived = MoveToward(spaz, self.ComboSplitSpazApproachTarget, SeparateSpeed, arenaCenter, out _);
                        bool retArrived = ret == null || MoveTowardStatic(ret, self.ComboSplitRetApproachTarget, SeparateSpeed, arenaCenter);

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

                        // Radius DIAM PERSIS di tepi border sepanjang Circling - request:
                        // "mau nya mendekat pas dah abis aja timer nya", jadi gak ada lagi
                        // shrink bertahap di tengah-tengah putaran (lihat komentar di knob
                        // FireIntervalTicks di atas kenapa RadiusShrinkFactor dihapus).
                        self.ComboSplitRadius = GetArenaRadiusOnly(spaz);

                        float spazAngle = self.ComboSplitStartAngle + self.ComboSplitSpinAngle;
                        spaz.Center = arenaCenter + spazAngle.ToRotationVector2() * self.ComboSplitRadius;
                        spaz.velocity = Vector2.Zero;

                        // SELALU menghadap tengah arena selama muter - bukan ngikutin player,
                        // bukan ngikutin arah tangensial kayak BorderShot.
                        Vector2 aimVector = arenaCenter - spaz.Center;
                        spaz.rotation = aimVector.ToRotation() - MathHelper.PiOver2;

                        self.ComboSplitTimer++;
                        self.ComboSplitFireTimer++;

                        // Tembakan combo: GreenBolt dari Spaz + RedBolt dari Ret, PERSIS di
                        // tick yang sama - normal tiap 0,3 detik (1x1), Enraged tiap 0,5 detik
                        // (3x3, "W") - interval-nya SENGAJA beda, lihat komentar knob di atas.
                        int fireInterval = self.IsEnraged ? EnragedFireIntervalTicks : FireIntervalTicks;
                        if (self.ComboSplitFireTimer >= fireInterval)
                        {
                            self.ComboSplitFireTimer = 0f;
                            FireComboBolts(spaz, ret, arenaCenter, self);
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

                        // Tetap menghadap tengah arena, konsisten sama Circling.
                        Vector2 aimVector = arenaCenter - spaz.Center;
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
        public static void RetinazerTick(NPC ret, NPC spaz, TwinsReworkOverride spazSelf, TwinsReworkOverride retSelf)
        {
            State state = (State)spazSelf.ComboSplitStateRaw;
            Vector2 arenaCenter = spazSelf.ComboSplitArenaCenter;

            switch (state)
            {
                case State.Separating:
                    MoveTowardStatic(ret, spazSelf.ComboSplitRetApproachTarget, SeparateSpeed, arenaCenter);
                    break;

                case State.Circling:
                    {
                        // SELALU di sisi yang PERSIS berlawanan sama Spaz (+PI radian), radius &
                        // progres putaran ngikutin field yang sama punya Spaz (satu-satunya
                        // sumber kebenaran), biar dua-duanya konsisten kompak.
                        float retAngle = spazSelf.ComboSplitStartAngle + spazSelf.ComboSplitSpinAngle + MathHelper.Pi;
                        ret.Center = arenaCenter + retAngle.ToRotationVector2() * spazSelf.ComboSplitRadius;
                        ret.velocity = Vector2.Zero;

                        // SELALU menghadap tengah arena, sama kayak Spaz.
                        Vector2 aimVector = arenaCenter - ret.Center;
                        ret.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
                        break;
                    }

                case State.Regrouping:
                case State.Done:
                    // Balik ngejar posisi Spaz LIVE (Spaz diem di Regrouping, jadi ini konvergen
                    // rapi, bukan ngejar target yang terus lari) - arah hadap tetap ke tengah
                    // arena selama proses ini (diatur di dalam MoveTowardStatic).
                    MoveTowardStatic(ret, spaz.Center, RegroupSpeed, arenaCenter);
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
        // badan", bukan 2 pool nyawa terpisah. Aman dari Retinazer "mati sendiri" karena
        // TwinsRework.cs (GlobalNPC) override CheckDead() Retinazer SELAMA window ini.
        //
        // FIX BUG "Retinazer hilang duluan pas Last Stand" — akar masalahnya: SELAMA Last
        // Stand, Spazmatism SENGAJA DIBUAT TOTAL INVINCIBLE (npc.dontTakeDamage = true
        // dipaksa tiap tick dari TwinsLastStand.Tick, drain HP-nya SENDIRI di-floor minimal 1
        // - lihat UpdateHealthDrainAndExpiry) - kematian "beneran" cuma boleh lewat state
        // DeathAnimation yang di-skrip, BUKAN lewat damage combat manapun. TAPI transfer di
        // bawah ini nge-poke npc.life SECARA LANGSUNG (bukan lewat hit-resolution vanilla),
        // jadi TOTAL GAK PEDULI sama dontTakeDamage Spazmatism - kalau HP Spaz udah di-drain
        // mepet ke floor (1) terus ada hit nyantol ke Retinazer pas Circling, transfer ini
        // bisa nendang spaz.life ke 0 dan manggil checkDead() SECARA PREMATUR - motong finale
        // Last Stand yang seharusnya di-skrip (jatuh+ledakan), DAN nyeret Retinazer ikut
        // dipaksa mati bareng (lewat OnKill) SAAT ITU JUGA, padahal posisinya lagi jauh di
        // sisi lain arena (Circling) - keliatannya kayak "Retinazer hilang duluan" padahal
        // sebenarnya DUA-DUANYA ke-kill bareng, cuma gak kelihatan soalnya jauh dari Spaz.
        //
        // FIX: SELAMA LastStandActive, damage combat yang kena Retinazer TETAP dibiarin
        // ngurangin ret.life sendiri (kosmetik/gak masalah - toh CheckDead-nya di-block penuh
        // selama vulnerable), TAPI TIDAK DITRANSFER ke spaz.life sama sekali. HP Spazmatism
        // SELAMA Last Stand murni cuma berkurang lewat drain terjadwal (UpdateHealthDrainAndExpiry),
        // persis sesuai desain aslinya.
        // ==========================================
        private static void HandleVulnerability(NPC ret, NPC spaz, TwinsReworkOverride spazSelf, TwinsReworkOverride retSelf)
        {
            // FIX: "invincible Spaz = invincible Ret" — kalau Spazmatism SUDAH invincible dari
            // alasan LAIN di luar SplitCombo sendiri (paling umum: TwinsPhaseTransition.Arm()
            // kepicu SELAMA Circling lagi jalan - Spaz langsung dontTakeDamage=true DETIK itu
            // juga begitu HP nyentuh <=50%, TAPI pattern yang lagi berputar sengaja DIBIARIN
            // nyelesein siklusnya sendiri dulu sebelum Trigger() motong ke Phase 2 - lihat
            // komentar TwinsPhaseTransition.Arm()), Retinazer WAJIB ikut invincible juga SAAT
            // ITU JUGA, TERLEPAS dari ComboSplitVulnerable/Circling masih true atau enggak.
            //
            // Sebelumnya field ini SATU-SATUNYA yang nentuin ret.dontTakeDamage, jadi ada
            // window nyata di mana Spaz udah gak bisa diserang sama sekali tapi Ret (1 pool HP
            // yang sama) masih keitung vulnerable sampai putaran Circling-nya beneran kelar -
            // itu penyebab "Ret masih bisa kena hit padahal Spaz udah invincible" pas transisi
            // ke Spectre (berlaku sama di rotasi Phase 1 biasa juga, gak cuma pas 50% HP).
            if (spazSelf.ComboSplitVulnerable && !spaz.dontTakeDamage)
            {
                ret.dontTakeDamage = false;

                if (!retSelf.ComboSplitDamageTrackingInit)
                {
                    // Nilai AWAL tracking = life REAL Retinazer saat ini (yang di tick
                    // sebelumnya udah di-mirror persis dari Spazmatism lewat cabang "else" di
                    // bawah) - BUKAN angka dummy lagi. Aman dari "mati sendiri" karena
                    // CheckDead() Retinazer di-override di TwinsRework.cs (GlobalNPC) buat
                    // SELALU membatalkan proses matinya SELAMA ComboSplitVulnerable true.
                    retSelf.ComboSplitLastTrackedLife = ret.life;
                    retSelf.ComboSplitDamageTrackingInit = true;
                }

                if (ret.life < retSelf.ComboSplitLastTrackedLife)
                {
                    int damageTaken = retSelf.ComboSplitLastTrackedLife - ret.life;
                    retSelf.ComboSplitLastTrackedLife = ret.life;

                    // SELAMA Last Stand, JANGAN transfer damage ini ke spaz.life sama sekali -
                    // Spazmatism WAJIB tetap invincible total, kill "beneran" cuma boleh lewat
                    // DeathAnimation yang di-skrip TwinsLastStand.cs (lihat komentar panjang di
                    // atas). Di luar Last Stand, transfer jalan seperti biasa.
                    if (!spazSelf.LastStandActive)
                    {
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
            }
            else
            {
                // Di luar window Circling (Separating/Regrouping) - tetap invincible kayak
                // biasa, dan npc.life/lifeMax-nya terus di-resync ngikutin Spazmatism
                // (real value) tiap tick, jadi begitu Circling mulai lagi nanti, nilai
                // awal yang dipakai HandleVulnerability di atas udah pasti benar.
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
        // (Spaz & Ret). Return true begitu udah dianggap "nyampe" (dalam threshold). Arah
        // HADAP (rotation) SELALU ke facePoint (tengah arena), TERLEPAS dari arah gerak -
        // request "menghadap tengah arena terus".
        private static bool MoveToward(NPC npc, Vector2 destination, float speed, Vector2 facePoint, out float distanceOut)
        {
            Vector2 toTarget = destination - npc.Center;
            float distance = toTarget.Length();
            distanceOut = distance;

            if (distance <= SeparateArriveThreshold)
                return true;

            Vector2 moveDirection = toTarget / distance;
            npc.velocity = Vector2.Lerp(npc.velocity, moveDirection * speed, 0.12f);

            Vector2 aimVector = facePoint - npc.Center;
            npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
            return false;
        }

        // Versi buat Retinazer (dipanggil dari RetinazerTick, tanpa perlu tau jarak persisnya
        // ke pemanggil) - dipakai baik buat Separating (nuju RetApproachTarget) maupun
        // Regrouping (nuju posisi Spaz yang lagi diem). Threshold arrival-nya generik,
        // aman dipakai di dua konteks itu.
        private static bool MoveTowardStatic(NPC npc, Vector2 destination, float speed, Vector2 facePoint)
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

            Vector2 aimVector = facePoint - npc.Center;
            npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
            return false;
        }

        // Tembakan combo simultan: GreenBolt dari Spaz + RedPhantasmalBolt dari Ret, DUA-
        // DUANYA diarahkan dari posisi masing-masing KE TENGAH ARENA (bukan ke player -
        // pattern ini murni geometris). Normal = 1 proyektil tiap Twin. Enraged = 3 proyektil
        // tiap Twin dalam formasi "W" (1 lurus ke tengah + 2 nyebar kanan/kiri), tapi TETAP
        // di tick yang sama antara Spaz & Ret (combo/serentak, timing gak berubah).
        private static void FireComboBolts(NPC spaz, NPC ret, Vector2 arenaCenter, TwinsReworkOverride spazSelf)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            int count = spazSelf.IsEnraged ? EnragedProjectileCount : 1;

            Vector2 spazBaseDir = (arenaCenter - spaz.Center).SafeNormalize(-Vector2.UnitY);
            FireCursedFlameFan(spaz, spazBaseDir, count);

            if (ret != null && ret.active)
            {
                Vector2 retBaseDir = (arenaCenter - ret.Center).SafeNormalize(-Vector2.UnitY);
                FireRedBoltFan(ret, retBaseDir, count);
            }
        }

        // count == 1 -> tembak lurus doang (t = 0, gak ada offset). count == 3 -> formasi "W"
        // (t = -0.5, 0, 0.5 dari ComboSpreadDegrees, PERSIS pola spread yang sama kayak
        // FireDeathLaserVolley di TwinDash.cs).
        private static void FireCursedFlameFan(NPC spaz, Vector2 baseDir, int count)
        {
            int boltType = ModContent.ProjectileType<GreenBolt>();

            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0f : (i / (float)(count - 1)) - 0.5f;
                float angleOffset = MathHelper.ToRadians(ComboSpreadDegrees) * t;
                Vector2 dir = baseDir.RotatedBy(angleOffset);
                Vector2 muzzle = spaz.Center + dir * MouthOffset;

                // CATATAN: TIDAK ada .GetGlobalProjectile<TwinsDebuffGlobalProjectile>().IsFromTwins
                // — GreenBolt adalah custom ModProjectile, TIDAK terdaftar di AppliesToEntity.
                // Manggil GetGlobalProjectile pada tipe yang tidak terdaftar → KeyNotFoundException
                // → crash. GreenBolt handle debuffnya sendiri di OnHitPlayer() (Cursed Inferno),
                // sama persis pola RedPhantasmalBolt.
                Projectile.NewProjectile(
                    spaz.GetSource_FromAI(),
                    muzzle,
                    dir * GreenBolt.StartSpeed,
                    boltType,
                    TwinsReworkOverride.ScaleBoltDamage(GreenBoltDamage),
                    2f,
                    Main.myPlayer,
                    dir.ToRotation(), // ai0: sudut arah terkunci (dibaca sendiri sama AI() GreenBolt)
                    0f                 // ai1: counter tick buat kurva speed exponential, mulai dari 0
                );
            }
        }

        private static void FireRedBoltFan(NPC ret, Vector2 baseDir, int count)
        {
            int boltType = ModContent.ProjectileType<RedPhantasmalBolt>();

            for (int i = 0; i < count; i++)
            {
                float t = count <= 1 ? 0f : (i / (float)(count - 1)) - 0.5f;
                float angleOffset = MathHelper.ToRadians(ComboSpreadDegrees) * t;
                Vector2 dir = baseDir.RotatedBy(angleOffset);
                Vector2 muzzle = ret.Center + dir * MouthOffset;

                Projectile.NewProjectile(
                    ret.GetSource_FromAI(),
                    muzzle,
                    dir * RedPhantasmalBolt.StartSpeed,
                    boltType,
                    TwinsReworkOverride.ScaleBoltDamage(RedBoltDamage),
                    1.5f,
                    Main.myPlayer,
                    dir.ToRotation(), // ai0: sudut arah terkunci
                    0f                // ai1: counter tick exponential
                );
            }
        }
    }
}
