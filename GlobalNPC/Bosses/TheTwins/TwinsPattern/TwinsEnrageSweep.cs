using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;
using TheSanity.Systems;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // PATTERN: ENRAGE SWEEP (Phase 3 / Enraged ONLY, JUGA dipakai di Last Stand)
    // ==========================================
    // Pattern BARU yang CUMA masuk rotasi kalau self.IsEnraged true (Phase 3, abis kedua
    // Spectre Phase 2 tumbang) - disisipin PERSIS setelah SplitCombo, SEBELUM balik ke Dash:
    //   - Rotasi NORMAL (dispatcher TwinsRework.cs): SplitCombo -> EnrageSweep -> Dash, TAPI
    //     CUMA kalau IsEnraged true - kalau belum Enraged, tetep SplitCombo -> Dash langsung
    //     kayak biasa (pattern ini gak pernah kepanggil sebelum Phase 3).
    //   - Attack-loop Last Stand (TwinsLastStand.cs): SplitCombo -> EnrageSweep -> (checkpoint
    //     budget: Dash lagi ATAU ReturnToCenter/Deathray) - SELALU lewat EnrageSweep, gak ada
    //     pengecekan IsEnraged lagi soalnya Last Stand SENDIRI otomatis selalu Enraged.
    //
    // Alur per 1 ULANGAN (di-loop 3-6x normal, SELALU 6x kalau Last Stand - lihat
    // MinRepeats/MaxRepeatsInclusive):
    //   1. MoveToSide - Twin terbang ke SATU titik random di sekitar player (radius tetap
    //      dari lokasi TERAKHIR player, lihat catatan di bawah), berhenti di situ.
    //   2. Begitu nyampe: hitung "baseFacingAngle" (arah ke player, SEKALI doang, TIDAK
    //      di-update lagi selama sweep - biar rotasinya presisi, gak "ngambang" ngikutin
    //      player yang mungkin lagi gerak), lalu SNAP LANGSUNG kepala Twin ke arah itu -
    //      Twin MENGHADAP PLAYER DULU sebelum mulai sweep sama sekali. Abis itu random
    //      pilih BIAS arah (ATAS/-1 atau BAWAH/+1) buat nentuin EKSTREM MANA yang
    //      dikunjungi duluan.
    //   3. SweepPhaseA - kepala MUTER KELUAR dari baseFacingAngle (ngadep player) menuju
    //      Ekstrem1 (offset SweepAngleDegrees ke arah bias) selama SweepPhaseADurationTicks
    //      (0.4 detik), sambil BURST nembakin bolt tiap FireIntervalTicks (1 tick /
    //      ~0.017 detik - nembak TIAP tick, tembakan pertama langsung lepas di tick
    //      pertama fase) ngikutin arah hadap LIVE. Warna bolt-nya ngikutin "giliran" attempt ini (lihat poin WARNA di
    //      bawah) - BUKAN selalu Red lagi kayak versi sebelumnya.
    //   4. HoldAtExtreme - begitu PERSIS di Ekstrem1, kepala BERHENTI TOTAL (gak nembak,
    //      gak muter) selama 0.2 detik (HoldAtExtremeDurationTicks) - "jeda napas" di
    //      puncak sweep sisi pertama, sebelum lanjut nyebrang ke Fase B.
    //   5. SweepPhaseB - abis jeda, kepala MUTER NYEBRANG dari Ekstrem1 TERUS LEWAT
    //      baseFacingAngle (persis ngelewatin arah player di TENGAH-TENGAH fase ini) sampai
    //      ke Ekstrem2 di SISI BERLAWANAN (offset SweepAngleDegrees ke arah bias
    //      KEBALIKAN) - jadi total jarak yang ditempuh Fase B itu DUA KALI lipat Fase A
    //      (dari Ekstrem1 ke Ekstrem2, ngelewatin player di tengah jalan), makanya
    //      durasinya SweepPhaseBDurationTicks = 2x SweepPhaseADurationTicks biar
    //      kecepatan sudut (derajat/tick) tetep KONSISTEN sama Fase A, bukan tiba-tiba
    //      ngebut. Nembakin bolt tiap FireIntervalTicks yang sama sepanjang fase ini,
    //      TERMASUK pas lagi persis ngelewatin arah player - warnanya SAMA PERSIS kayak
    //      Fase A di attempt yang sama (satu attempt = satu warna doang, Fase A & B gak
    //      beda warna lagi).
    //
    //      INTINYA: Ekstrem1 dan Ekstrem2 SAMA-SAMA berjarak SweepAngleDegrees dari
    //      baseFacingAngle tapi di sisi BERLAWANAN - jadi player SELALU jadi TITIK TENGAH
    //      GEOMETRIS dari keseluruhan busur (Ekstrem1..Ekstrem2), BUKAN salah satu
    //      ujungnya kayak versi sebelumnya (yang cuma bolak-balik antara player <-> satu
    //      ekstrem doang).
    //   6. DashTelegraph -> Dashing - begitu Fase A+Hold+Fase B abis (kepala sekarang ada
    //      di Ekstrem2), langsung telegraph (garis aim, arah DIKUNCI ke player sekali,
    //      sama kayak TwinDash) lalu DASH ke arah player. Dash-nya REUSE method & angka
    //      PERSIS dari TwinDash versi Phase 1/non-enrage (TwinDash.FireEyeFire +
    //      TwinDash.FireDeathLaserVolley, TwinDash.DashDuration/DashSpeed/
    //      EyeFireIntervalTicks) - SENGAJA TIDAK pakai side-bolt enrage punya TwinDash
    //      (FireDashSideBolts) meskipun self.IsEnraged pasti true di sini (pattern ini
    //      cuma jalan pas Phase 3), soalnya request-nya emang mau versi non-enrage.
    //   7. Balik ke langkah 1 (titik samping BARU + bias baru di-random ulang, WARNA
    //      buat attempt berikutnya ke-toggle - lihat poin WARNA di bawah) sampai jumlah
    //      ulangan abis, baru Done.
    //
    // WARNA GANTIAN PER ATTEMPT (BUKAN LAGI PER FASE): dulu Fase A SELALU RedPhantasmalBolt
    // & Fase B SELALU GreenBolt (dalam SATU attempt yang sama bisa ke-2 warna keluar).
    // SEKARANG satu ATTEMPT PENUH (Fase A + Hold + Fase B, alias 1 kali "titik A -> titik
    // B" pulang-pergi) cuma pakai SATU warna doang - attempt ganjil (1, 3, 5, ...) Red,
    // attempt genap (2, 4, 6, ...) Green, gantian tiap abis Dash & lanjut ke MoveToSide
    // attempt berikutnya. Kalau Last Stand (selalu 6x attempt penuh), urutannya jadi persis
    // Merah -> Hijau -> Merah -> Hijau -> Merah -> Hijau. Field baru self.EnrageSweepRedTurn
    // (bool) nyimpen giliran ini - di-set true di Start() (attempt pertama Red) dan
    // di-toggle (!self.EnrageSweepRedTurn) tiap abis Dashing kelar & masih ada
    // RepeatsRemaining, PERSIS sebelum balik ke State.MoveToSide.
    //
    // >>> ACTION REQUIRED: field self.EnrageSweepRedTurn ini BELUM ada di
    // TwinsReworkOverride.cs (file itu gak ke-upload di sini) - tambahin manual:
    //     public bool EnrageSweepRedTurn;
    // di deket field EnrageSweep lain (EnrageSweepBiasSign dkk), pola sama kayak field
    // bool lain yang udah ada (IsEnraged/IsDashing/IsTelegraphing). <<<
    //
    // TITIK SAMPING SEKARANG DIPUSATKAN DI PLAYER: PickNewSidePoint (langkah 1/7) dulu
    // ngitung titik samping berpusat di TENGAH ARENA (ArenaBorderSystem). SEKARANG
    // dipusatkan di LOKASI TERAKHIR player (target.Center, di-lock SEKALI pas dipanggil -
    // BUKAN live-tracking), dengan jarak tetap SidePointDistanceFromPlayer. Efeknya: Twin
    // selalu mulai sweep dari titik yang relatif deket player, bukan ngacak ke sisi arena
    // yang jauh dari player. Konsekuensinya: Start() SEKARANG BUTUH parameter Player target
    // juga (dulu cuma npc, self) - lihat catatan breaking-change di deklarasi Start().
    //
    // ARAH-nya SEKARANG BUKAN RANDOM LAGI: dulu sideAngle di-roll penuh
    // Main.rand.NextFloat(TwoPi) (Twin bisa aja lari ke arah tengah arena / sisi yang jauh
    // dari tepi). SEKARANG diambil dari arah arenaCenter -> player (ArenaBorderSystem),
    // jadi titik samping selalu di SISI BORDER TERDEKAT dari posisi player saat itu -
    // Twin "kabur" ke tepi yang paling deket, bukan ke arah sembarangan. Fallback ke
    // random cuma dipakai kalau player pas persis di titik tengah arena (arah gak
    // ke-definisi karena arenaCenter == player.Center).
    //
    // Contoh konkret (request): attempt 1 (Red) - kepala mula-mula LANGSUNG ngadep player
    // (baseFacingAngle), bias awal = BAWAH -> SweepPhaseA muter KELUAR ke BAWAH
    // SweepAngleDegrees (burst nembak RED) sampai PERSIS di Ekstrem1 (bawah), JEDA 0.2
    // detik di situ, lalu SweepPhaseB muter NYEBRANG lewat arah player di tengah jalan,
    // TERUS sampai Ekstrem2 di ATAS (burst nembak RED juga, sama kayak Fase A). Dash, lalu
    // attempt 2 (Green) - proses ulang dari titik samping baru, tapi Fase A & B-nya sekarang
    // GREEN semua. Kalau bias awal = ATAS, tinggal dibalik semua. Player ada PERSIS di
    // tengah-tengah Ekstrem1 dan Ekstrem2, bukan di salah satu ujungnya.
    //
    // KONVENSI ARAH: "atas" = rotation MENGECIL, "bawah" = rotation MEMBESAR - standar
    // Vector2.ToRotation()/ToRotationVector2() (X ke kanan, Y ke bawah), SAMA kayak konvensi
    // dipakai di seluruh file pattern lain di codebase ini.
    //
    // Damage RedPhantasmalBolt & GreenBolt SAMA PERSIS kayak pattern lain (base damage lewat
    // TwinsReworkOverride.ScaleBoltDamage(...) - sekarang passthrough doang, gak ada bonus
    // Last Stand lagi, lihat komentar ScaleBoltDamage di TwinsRework.cs).
    //
    // FIELD SHARING: step Dash di sini SENGAJA reuse field self.TelegraphDirection /
    // self.IsTelegraphing / self.IsDashing yang sama dipakai TwinDash.cs (bukan bikin field
    // baru) - aman soalnya dispatcher cuma jalanin SATU pattern per Twin di satu waktu,
    // jadi gak ada dua pattern rebutan field bersamaan. Ini juga otomatis bikin garis aim
    // telegraph & after-image dash keliatan sama persis kayak TwinDash biasa, gak perlu
    // sentuh kode render (PreDraw) sama sekali.
    //
    // MP-SAFE: semua NewProjectile dibungkus netMode check, tiap ganti state netUpdate = true.
    // ==========================================
    public static class TwinsEnrageSweep
    {
        private enum State
        {
            MoveToSide,
            SweepPhaseA,
            HoldAtExtreme,
            SweepPhaseB,
            DashTelegraph,
            Dashing,
            Done
        }

        // ---- Jumlah ulangan (3-6x normal, SELALU 6x kalau Last Stand - pola SAMA kayak
        // TwinDash/TwinsBorderShot/TwinsCursedRain/TwinsSplitCombo) ----
        private const int MinRepeats = 3;
        private const int MaxRepeatsInclusive = 6;

        // ---- MoveToSide ----
        private const float MoveSpeed = 22f;
        private const float ArriveThreshold = 40f;
        private const float MoveMaxDuration = 150f; // safety timeout ~2.5 detik

        // ---- Sweep ----
        private const float SweepAngleDegrees = 120f; // jarak dari baseFacingAngle (player) ke SATU ekstrem - Ekstrem1 & Ekstrem2 sama-sama segini jauhnya dari player tapi di sisi berlawanan
        private static readonly float SweepAngleRadians = MathHelper.ToRadians(SweepAngleDegrees);
        private const float SweepPhaseADurationTicks = 24f; // ~0.4 detik - Fase A cuma nempuh SATU setengah-busur (center -> Ekstrem1)
        private const float SweepPhaseBDurationTicks = SweepPhaseADurationTicks * 2f; // Fase B nempuh DUA setengah-busur (Ekstrem1 -> center -> Ekstrem2), jaraknya 2x Fase A - durasi di-2x-in juga biar kecepatan sudut (derajat/tick) KONSISTEN, bukan tiba-tiba ngebut pas nyebrang lewat player
        private const float HoldAtExtremeDurationTicks = 12f; // 0.2 detik jeda PERSIS di titik ekstrem (puncak sweep) - kepala berhenti sejenak sebelum muter balik ke Fase B
        private const float FireIntervalTicks = 1f;         // BURST - DIPERCEPAT lagi dari 2 tick ke 1 tick (nembak TIAP tick) - celah antar tembakan kegedean (~10° per tembakan pas 2 tick), sekarang jadi ~5°/tembakan biar keliatan padat & rapat, bukan renggang-renggang. Countdown di-set 0 pas fase mulai jadi tembakan pertama lepas SEKETIKA (tick pertama), bukan nunggu interval dulu - biar berasa "letusan" bukan tetesan.

        // PENTING - JANGAN diubah sembarangan tanpa mikirin ini: (SweepPhaseBDurationTicks / 2)
        // HARUS abis dibagi FireIntervalTicks (sekarang 24 / 1 = 24, pas). Ini yang bikin
        // ADA SATU tembakan yang jadwalnya PERSIS jatuh di tick tengah Fase B (waktu kepala
        // PERSIS ngelewatin arah player), bukan cuma "deket-deket" doang. Kalau angka-angka
        // ini diubah dan gak abis dibagi lagi, tembakan tengah itu bisa geser 1 tick dan
        // jadi gak dead-on lagi pas nyebrang.

        private const float MouthOffset = 24f;

        // ---- MoveToSide: SEKARANG dipusatkan di lokasi TERAKHIR player (locked pas
        // dipanggil), BUKAN lagi di tengah arena - lihat PickNewSidePoint. ----
        private const float SidePointDistanceFromPlayer = 450f; // jarak titik samping dari player

        // ---- Dash (setelah tiap sweep atas-bawah selesai) ----
        // Reuse langsung angka & method dari TwinDash versi Phase 1/non-enrage, biar
        // "PERSIS" sama - lihat komentar besar di atas file.
        private const float DashTelegraphDuration = TwinDash.TelegraphDuration;
        private const float DashDuration = TwinDash.DashDuration;
        private const float DashSpeed = TwinDash.DashSpeed;
        private const int EyeFireIntervalTicks = TwinDash.EyeFireIntervalTicks;

        private const int RedBoltDamage = 10;   // target ~30 DMG Master / 3 (engine auto-triples proyektil di Master mode)
        private const int GreenBoltDamage = 10; // target ~30 DMG Master / 3 (engine auto-triples proyektil di Master mode)

        // CATATAN BREAKING CHANGE: signature Start() SEKARANG butuh Player target juga
        // (dulu cuma npc, self) - soalnya PickNewSidePoint sekarang mesti tau lokasi
        // player buat jadi titik tengah. Call site di TwinsLastStand.cs UDAH di-update;
        // call site satunya lagi ada di TwinsRework.cs (dispatcher rotasi normal, file itu
        // gak ke-upload di sini) - itu JUGA WAJIB di-update jadi
        // "TwinsEnrageSweep.Start(npc, self, target)" biar kompatibel.
        public static void Start(NPC npc, TwinsReworkOverride self, Player target)
        {
            self.EnrageSweepStateRaw = (float)State.MoveToSide;
            self.EnrageSweepTimer = 0f;
            self.EnrageSweepRepeatsRemaining = self.LastStandActive ? MaxRepeatsInclusive : Main.rand.Next(MinRepeats, MaxRepeatsInclusive + 1);

            // Giliran warna attempt PERTAMA selalu Red (Merah -> Hijau -> Merah -> ...).
            self.EnrageSweepRedTurn = true;

            PickNewSidePoint(npc, self, target);
        }

        public static void Tick(NPC npc, TwinsReworkOverride self, Player target)
        {
            State state = (State)self.EnrageSweepStateRaw;

            switch (state)
            {
                case State.MoveToSide:
                    {
                        Vector2 toTarget = self.EnrageSweepMoveTarget - npc.Center;
                        float distance = toTarget.Length();

                        self.EnrageSweepTimer++;

                        if (distance > ArriveThreshold && self.EnrageSweepTimer < MoveMaxDuration)
                        {
                            Vector2 moveDirection = toTarget / distance;
                            npc.velocity = Vector2.Lerp(npc.velocity, moveDirection * MoveSpeed, 0.1f);

                            Vector2 aimVector = target.Center - npc.Center;
                            npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
                            break;
                        }

                        // Nyampe (atau timeout) -> berhenti total, hitung baseFacingAngle SEKALI,
                        // SNAP LANGSUNG kepala buat ngadep player dulu (titik TENGAH sweep),
                        // baru random bias arah buat nentuin SweepPhaseA muter keluar ke mana.
                        npc.Center = self.EnrageSweepMoveTarget;
                        npc.velocity = Vector2.Zero;

                        self.EnrageSweepBaseFacingAngle = (target.Center - npc.Center).ToRotation();
                        self.EnrageSweepBiasSign = Main.rand.NextBool() ? 1f : -1f; // +1 = sweep pertama ke BAWAH, -1 = sweep pertama ke ATAS

                        self.EnrageSweepCurrentAngle = self.EnrageSweepBaseFacingAngle;
                        npc.rotation = self.EnrageSweepBaseFacingAngle - MathHelper.PiOver2; // MENGHADAP PLAYER DULU sebelum sweep mulai

                        self.EnrageSweepTimer = 0f;
                        self.EnrageSweepFireCountdown = 0f; // burst pertama lepas SEKETIKA di tick pertama SweepPhaseA
                        self.EnrageSweepStateRaw = (float)State.SweepPhaseA;
                        npc.netUpdate = true;
                        break;
                    }

                case State.SweepPhaseA:
                    {
                        npc.velocity = Vector2.Zero;
                        npc.Center = self.EnrageSweepMoveTarget;

                        // t dihitung dari Timer SEBELUM di-increment - biar TICK PERTAMA
                        // (Timer == 0) itu PERSIS di baseFacingAngle (ngadep player), BUKAN
                        // udah geser dikit. PENTING: ini yang bikin tembakan Red PERTAMA
                        // beneran full dead-on ke arah player dari awal (bukan udah miss
                        // sejak tembakan pertama gara-gara telat 1 tick).
                        float t = MathHelper.Clamp(self.EnrageSweepTimer / SweepPhaseADurationTicks, 0f, 1f);

                        // Muter KELUAR dari baseFacingAngle (ngadep player) menuju Ekstrem1
                        // (offset SweepAngleDegrees ke arah bias).
                        float extreme1Angle = self.EnrageSweepBaseFacingAngle + self.EnrageSweepBiasSign * SweepAngleRadians;
                        self.EnrageSweepCurrentAngle = MathHelper.Lerp(self.EnrageSweepBaseFacingAngle, extreme1Angle, t);
                        npc.rotation = self.EnrageSweepCurrentAngle - MathHelper.PiOver2;

                        self.EnrageSweepFireCountdown--;
                        if (self.EnrageSweepFireCountdown <= 0f)
                        {
                            FireSweepBolt(npc, self.EnrageSweepCurrentAngle, self.EnrageSweepRedTurn);
                            self.EnrageSweepFireCountdown = FireIntervalTicks;
                        }

                        self.EnrageSweepTimer++;

                        if (self.EnrageSweepTimer >= SweepPhaseADurationTicks)
                        {
                            // PERSIS di Ekstrem1 sekarang - JEDA dulu 0.2 detik
                            // (HoldAtExtreme) sebelum nyebrang lewat player ke Ekstrem2.
                            self.EnrageSweepCurrentAngle = extreme1Angle;
                            npc.rotation = self.EnrageSweepCurrentAngle - MathHelper.PiOver2;

                            self.EnrageSweepTimer = 0f;
                            self.EnrageSweepStateRaw = (float)State.HoldAtExtreme;
                            npc.netUpdate = true;
                        }
                        break;
                    }

                case State.HoldAtExtreme:
                    {
                        // Jeda 0.2 detik PERSIS di titik ekstrem (puncak sweep) - kepala
                        // BERHENTI TOTAL sebentar (gak nembak, gak muter) sebelum nyebrang
                        // lewat arah player menuju ekstrem di sisi berlawanan (Fase B).
                        // Kerasa kayak "napas sejenak" di puncak, bukan langsung nyambung
                        // mulus.
                        npc.velocity = Vector2.Zero;
                        npc.Center = self.EnrageSweepMoveTarget;
                        npc.rotation = self.EnrageSweepCurrentAngle - MathHelper.PiOver2;

                        self.EnrageSweepTimer++;
                        if (self.EnrageSweepTimer >= HoldAtExtremeDurationTicks)
                        {
                            self.EnrageSweepTimer = 0f;
                            self.EnrageSweepFireCountdown = 0f; // burst kedua juga lepas SEKETIKA di tick pertama SweepPhaseB
                            self.EnrageSweepStateRaw = (float)State.SweepPhaseB;
                            npc.netUpdate = true;
                        }
                        break;
                    }

                case State.SweepPhaseB:
                    {
                        npc.velocity = Vector2.Zero;
                        npc.Center = self.EnrageSweepMoveTarget;

                        // Sama kayak Fase A: t dihitung dari Timer SEBELUM di-increment.
                        // Efeknya di sini malah lebih penting - karena SweepPhaseBDurationTicks
                        // PERSIS 2x FireIntervalTicks-nya rapi kelipatan, tick PERSIS di
                        // tengah (Timer == SweepPhaseBDurationTicks / 2, alias t == 0.5)
                        // otomatis KEBAGIAN jadwal tembak (0, 2, 4, ... termasuk tengah),
                        // jadi ada SATU tembakan (warnanya ngikutin EnrageSweepRedTurn attempt
                        // ini) yang PERSIS lurus ke arah player pas nyebrang di tengah jalan -
                        // bukan cuma "deket" doang kayak sebelumnya.
                        float t = MathHelper.Clamp(self.EnrageSweepTimer / SweepPhaseBDurationTicks, 0f, 1f);

                        // Muter NYEBRANG dari Ekstrem1 (baseFacingAngle + bias*sweep) TERUS
                        // LEWAT baseFacingAngle (persis ngelewatin arah player di tengah
                        // jalan) sampai ke Ekstrem2 di sisi BERLAWANAN (baseFacingAngle -
                        // bias*sweep). Player jadi TITIK TENGAH GEOMETRIS dari Ekstrem1..
                        // Ekstrem2, bukan salah satu ujungnya.
                        float extreme1Angle = self.EnrageSweepBaseFacingAngle + self.EnrageSweepBiasSign * SweepAngleRadians;
                        float extreme2Angle = self.EnrageSweepBaseFacingAngle - self.EnrageSweepBiasSign * SweepAngleRadians;
                        self.EnrageSweepCurrentAngle = MathHelper.Lerp(extreme1Angle, extreme2Angle, t);
                        npc.rotation = self.EnrageSweepCurrentAngle - MathHelper.PiOver2;

                        self.EnrageSweepFireCountdown--;
                        if (self.EnrageSweepFireCountdown <= 0f)
                        {
                            FireSweepBolt(npc, self.EnrageSweepCurrentAngle, self.EnrageSweepRedTurn);
                            self.EnrageSweepFireCountdown = FireIntervalTicks;
                        }

                        self.EnrageSweepTimer++;

                        if (self.EnrageSweepTimer >= SweepPhaseBDurationTicks)
                        {
                            // PERSIS di Ekstrem2 sekarang - langsung telegraph & dash ke
                            // arah player (StartDashTelegraph ngunci arah fresh dari posisi
                            // sekarang, jadi gak perlu snap rotation balik dulu).
                            self.EnrageSweepCurrentAngle = extreme2Angle;
                            npc.rotation = self.EnrageSweepCurrentAngle - MathHelper.PiOver2;

                            StartDashTelegraph(npc, self, target);

                            self.EnrageSweepTimer = 0f;
                            self.EnrageSweepStateRaw = (float)State.DashTelegraph;
                            npc.netUpdate = true;
                        }
                        break;
                    }

                case State.DashTelegraph:
                    {
                        // Ancang-ancang: hampir diam, arah SUDAH DIKUNCI dari awal telegraph
                        // (self.TelegraphDirection) dan TIDAK diupdate lagi walau player
                        // gerak - SAMA PERSIS behavior-nya kayak TwinDash.Telegraph.
                        npc.velocity *= 0.9f;
                        npc.rotation = self.TelegraphDirection.ToRotation() - MathHelper.PiOver2;

                        self.EnrageSweepTimer++;
                        if (self.EnrageSweepTimer >= DashTelegraphDuration)
                        {
                            self.IsTelegraphing = false; // garis hijau hilang

                            // LEPASKAN DASH ke arah yang udah dikunci tadi
                            npc.velocity = self.TelegraphDirection * DashSpeed;

                            SoundEngine.PlaySound(TwinsSounds.TwinRoar, npc.Center);

                            // DeathLaser sekali di awal dash - reuse method PERSIS milik
                            // TwinDash (versi Phase 1/non-enrage, gak ada bonus apapun).
                            TwinDash.FireDeathLaserVolley(npc, self, self.TelegraphDirection);

                            self.IsDashing = true; // after-image jalan, SAMA kayak TwinDash

                            self.EnrageSweepTimer = 0f;
                            self.EnrageSweepStateRaw = (float)State.Dashing;
                            npc.netUpdate = true;
                        }
                        break;
                    }

                case State.Dashing:
                    {
                        npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

                        // EyeFire spam selama dash - reuse method PERSIS milik TwinDash.
                        // SENGAJA TIDAK ada side-bolt enrage (FireDashSideBolts) di sini
                        // walau self.IsEnraged pasti true - request-nya emang versi
                        // non-enrage/Phase 1 doang buat dash abis sweep ini.
                        int tickInDash = (int)self.EnrageSweepTimer;
                        if (tickInDash % EyeFireIntervalTicks == 0)
                        {
                            TwinDash.FireEyeFire(npc, self.TelegraphDirection);
                        }

                        self.EnrageSweepTimer++;

                        // Deselerasi menjelang akhir dash, sama kayak TwinDash.
                        if (self.EnrageSweepTimer > DashDuration - 10f)
                        {
                            npc.velocity *= 0.92f;
                        }

                        if (self.EnrageSweepTimer >= DashDuration)
                        {
                            self.IsDashing = false;

                            self.EnrageSweepRepeatsRemaining--;
                            if (self.EnrageSweepRepeatsRemaining > 0)
                            {
                                // Gantian warna buat attempt berikutnya - Red -> Green -> Red -> ...
                                self.EnrageSweepRedTurn = !self.EnrageSweepRedTurn;

                                PickNewSidePoint(npc, self, target);
                                self.EnrageSweepTimer = 0f;
                                self.EnrageSweepStateRaw = (float)State.MoveToSide;
                            }
                            else
                            {
                                npc.velocity = Vector2.Zero;
                                self.EnrageSweepStateRaw = (float)State.Done;
                            }
                            npc.netUpdate = true;
                        }
                        break;
                    }

                case State.Done:
                    break;
            }
        }

        // Dikunci SEKALI pas mulai telegraph (bukan live-tracking player), sama pola kayak
        // TwinDash.StartTelegraph - reuse field self.TelegraphDirection/self.IsTelegraphing
        // yang sama biar garis aim-nya otomatis kegambar lewat kode render TwinDash yang
        // udah ada, gak perlu ubah PreDraw.
        private static void StartDashTelegraph(NPC npc, TwinsReworkOverride self, Player target)
        {
            self.TelegraphDirection = (target.Center - npc.Center).SafeNormalize(-Vector2.UnitY);
            self.IsTelegraphing = true;
        }

        public static bool IsDone(TwinsReworkOverride self)
        {
            return (State)self.EnrageSweepStateRaw == State.Done;
        }

        // Titik SAMPING (border point) tempat Twin berhenti buat mulai sweep.
        // BERUBAH: dulu dipusatkan di TENGAH ARENA (GetArenaCenter/GetArenaRadius pakai
        // ArenaBorderSystem), SEKARANG dipusatkan di LOKASI TERAKHIR player - target.Center
        // di-baca SEKALI SAAT DIPANGGIL (locked, bukan live-tracking), jadi "titik tengah"
        // buat ngitung titik samping ini sekarang posisi player, bukan lagi posisi tengah
        // arena. Radius-nya juga ganti pakai SidePointDistanceFromPlayer (jarak tetap dari
        // player), bukan lagi arenaRadius - BorderEdgeInset punya arena.
        //
        // ARAH-nya SEKARANG DIHITUNG (BUKAN RANDOM): diambil dari arah arenaCenter -> player
        // (pakai ArenaBorderSystem.ActiveBorders[0].Center) - itu SEKALIGUS arah ke SISI
        // BORDER TERDEKAT dari posisi player, soalnya kalau ditarik garis lurus dari tengah
        // arena lewat player terus diteruskan ke tepi, situ jaraknya paling pendek ke border
        // dibanding sisi manapun. Efeknya: Twin selalu geser ke tepi yang paling deket sama
        // player, bukan ngacak bisa ke arah tengah/sisi jauh kayak sebelumnya.
        private static void PickNewSidePoint(NPC npc, TwinsReworkOverride self, Player target)
        {
            Vector2 centerPoint = target.Center;

            Vector2 arenaCenter = GetArenaCenter(npc);
            Vector2 towardNearestBorder = centerPoint - arenaCenter;

            // Fallback random cuma buat kasus degenerate (player pas persis di tengah arena
            // banget, arah arenaCenter->player jadi vector nol/gak ke-definisi sudutnya).
            float sideAngle = towardNearestBorder.LengthSquared() > 1f
                ? towardNearestBorder.ToRotation()
                : Main.rand.NextFloat(MathHelper.TwoPi);

            self.EnrageSweepMoveTarget = centerPoint + sideAngle.ToRotationVector2() * SidePointDistanceFromPlayer;
        }

        // Sama polanya kayak TwinsSpinningCurse.GetArenaCenter - fallback ke posisi NPC
        // sendiri kalau gak ada border aktif (jadi towardNearestBorder otomatis vector nol
        // -> jatuh ke fallback random di atas, bukan crash/NaN).
        private static Vector2 GetArenaCenter(NPC npc)
        {
            if (ArenaBorderSystem.ActiveBorders.Count > 0)
                return ArenaBorderSystem.ActiveBorders[0].Center;

            return npc.Center;
        }

        // Dispatcher warna: satu attempt (Fase A + Fase B) SEKARANG cuma pakai SATU warna,
        // ditentukan dari self.EnrageSweepRedTurn (di-toggle tiap abis Dash, lihat Tick()
        // case Dashing). true = RedPhantasmalBolt, false = GreenBolt.
        private static void FireSweepBolt(NPC npc, float angle, bool useRed)
        {
            if (useRed)
                FireRedBolt(npc, angle);
            else
                FireGreenBolt(npc, angle);
        }

        private static void FireRedBolt(NPC npc, float angle)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 direction = angle.ToRotationVector2();
            Vector2 spawnPos = npc.Center + direction * MouthOffset;

            int boltType = ModContent.ProjectileType<RedPhantasmalBolt>();
            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                spawnPos,
                direction * RedPhantasmalBolt.StartSpeed,
                boltType,
                TwinsReworkOverride.ScaleBoltDamage(RedBoltDamage),
                1.5f,
                Main.myPlayer,
                angle, // ai0: sudut arah terkunci
                0f     // ai1: counter tick exponential
            );
        }

        private static void FireGreenBolt(NPC npc, float angle)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 direction = angle.ToRotationVector2();
            Vector2 spawnPos = npc.Center + direction * MouthOffset;

            int boltType = ModContent.ProjectileType<GreenBolt>();
            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                spawnPos,
                direction * GreenBolt.StartSpeed,
                boltType,
                TwinsReworkOverride.ScaleBoltDamage(GreenBoltDamage),
                2f,
                Main.myPlayer,
                angle, // ai0: sudut arah terkunci
                0f     // ai1: counter tick exponential
            );
        }
    }
}
