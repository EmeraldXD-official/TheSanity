using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;
using TheSanity.Systems;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // PATTERN: BORDER HOP VOLLEY (Phase 3 / Enraged ONLY, JUGA dipakai di Last Stand)
    // ==========================================
    // Pattern BARU yang CUMA masuk rotasi kalau self.IsEnraged true (Phase 3, abis kedua
    // Spectre Phase 2 tumbang) - disisipin PERSIS setelah EnrageSweep, SEBELUM balik ke Dash:
    //   - Rotasi NORMAL (dispatcher TwinsRework.cs): EnrageSweep -> BorderHopVolley -> Dash,
    //     CUMA kalau IsEnraged true (EnrageSweep sendiri udah gak pernah kepanggil sebelum
    //     Phase 3, jadi pattern ini otomatis ikut aman).
    //   - Attack-loop Last Stand (TwinsLastStand.cs): ...SplitCombo -> EnrageSweep ->
    //     BorderHopVolley -> (checkpoint budget: Dash lagi ATAU ReturnToCenter/Deathray) -
    //     SELALU lewat pattern ini, Last Stand otomatis selalu Enraged.
    //
    // KONSEP: Spazmatism & Retinazer MISAH (kek TwinsSplitCombo - dua-duanya beneran gerak
    // sendiri-sendiri, bukan nempel), TAPI beda dari SplitCombo yang gerakannya SELALU
    // sinkron/simetris (180 derajat berlawanan, 1 timer bersama) - pattern ini SENGAJA
    // ASINKRON & "TANPA SEJAJAR": masing-masing Twin milih titik acak sendiri-sendiri di
    // tepi border (independen, gak ada hubungan sama sekali sama titik punya Twin
    // satunya), lalu:
    //   1. Terbang CEPAT ke titik itu.
    //   2. Begitu sampai -> LANGSUNG nembak volley "shotgun" (3 proyektil nyebar, arah
    //      dasarnya ke player): Retinazer nembak 3x RedPhantasmalBolt, Spazmatism nembak
    //      3x GreenBolt.
    //   3. Abis nembak, LANGSUNG pilih titik acak BARU di tepi border (lagi-lagi independen,
    //      gak nyambung sama titik sebelumnya) & terbang cepat ke sana lagi -> ulang dari
    //      langkah 2.
    //   4. Ulang "pindah + tembak" ini sebanyak jatah masing-masing (5-10x, DI-ROLL
    //      TERPISAH per Twin - Retinazer & Spazmatism BISA DAPAT jumlah beda-beda,
    //      MURNI independen satu sama lain - SELALU 10x/maksimal buat DUA-DUANYA kalau
    //      Last Stand). Field-nya sendiri per instance: HopVolleyRepeatsRequired.
    //   5. Begitu SALAH SATU Twin abis jatahnya duluan (timer/hitungannya beda-beda,
    //      wajar kalau satu abis lebih dulu), dia BERHENTI & NUNGGU di titik
    //      terakhirnya (HopVolleyFinished = true) - BUKAN lanjut mati gaya/nganggur
    //      pasif doang, tetap siaga di situ sampai partner-nya beneran nyusul kelar.
    //      Begitu KEDUA Twin udah nyelesein jatahnya masing-masing, BARU lanjut ke
    //      Regrouping: Retinazer terbang BALIK ke posisi Spazmatism (yang diem di
    //      titik terakhirnya), sama persis pola Regrouping di TwinsSplitCombo.
    //   6. Done -> dispatcher matiin HopVolleyActive, Retinazer balik ke mode mirror
    //      normal, ganti ke pattern berikutnya (Dash) - alias "loop" balik ke rotasi.
    //
    // "TANPA SEJAJAR" + ANTI-NEMPEL: titik tujuan tiap hop di-roll RANDOM, TAPI SEKARANG
    // ada rule minimum jarak sudut (MinSeparationDegrees) biar TIDAK KEBETULAN milih
    // titik yang (nyaris) sama - dicek terhadap DUA hal: (a) titik yang lagi/baru aja
    // dituju TWIN SATUNYA (biar dua Twin gak numpuk/nempel di 1 spot yang sama), dan
    // (b) titik hop SEBELUMNYA milik diri sendiri (biar gak diem-diem balik ke tempat
    // yang barusan ditinggalin). Reroll max beberapa kali (PickRandomBorderPoint), kalau
    // semua percobaan tetap kena constraint, fallback pakai percobaan terakhir (jaga-jaga
    // biar gak infinite loop kalau border sempit banget).
    //
    // ARAH TEMBAK & HADAP: base arah volley diarahkan ke PLAYER (target.Center) SAAT itu
    // juga (live, bukan dikunci), beda dari SplitCombo yang murni geometris ke tengah
    // arena - pattern ini soal "nyergap" dari sisi random, jadi harus tetap ngincer player.
    //
    // VULNERABILITY: Retinazer vulnerable (dontTakeDamage = false) SELAMA SELURUH fase
    // Active (dari hop pertama sampai hop terakhir kedua Twin kelar) - BEDA dari SplitCombo
    // yang punya fase Separating terpisah yang masih invincible, soalnya di sini SETIAP
    // leg perjalanan langsung berujung nembak (gak ada fase "transit murni" yang perlu
    // dilindungi). Balik invincible pas Regrouping, sama pola transfer damage 1 pool HP
    // PERSIS kayak TwinsSplitCombo.HandleVulnerability (termasuk fix "jangan transfer
    // selama Last Stand" biar gak numbangin finale terjadwal).
    //
    // MP-SAFE: semua NewProjectile dibungkus netMode check, posisi Twin di-sync berkala
    // lewat netUpdate karena gerakannya di-set langsung (velocity, bukan snap).
    // ==========================================
    public static class TwinsBorderHopVolley
    {
        private enum Phase
        {
            Active,
            Regrouping,
            Done
        }

        // ---- Tunable knobs ----
        private const float HopMoveSpeed = 26f;          // "gerak cepat" pindah antar sisi border
        private const float HopArriveThreshold = 40f;
        private const float MouthOffset = 24f;

        // 5-10x "pindah + tembak" per Twin. SELALU 10x (maksimal) kalau Last Stand - sama
        // pola kayak TwinDash.DashRepeatsRemaining/TwinsSplitCombo.ComboSplitLapsRequired.
        private const int MinHops = 5;
        private const int MaxHopsInclusive = 10;

        private const float RegroupSpeed = 18f;
        private const float RegroupArriveThreshold = 50f;
        private const float RegroupMaxDuration = 200f; // safety timeout jaga-jaga

        // Jarak sudut minimal antar titik hop - dicek terhadap titik punya Twin satunya
        // DAN titik hop sebelumnya milik diri sendiri, biar "ngga bisa nempel di tempat
        // yang sama". Reroll max RerollAttempts kali di PickRandomBorderPoint.
        private const float MinSeparationDegrees = 70f;
        private const int RerollAttempts = 8;

        // ---- Shotgun volley ----
        private const int VolleyProjectileCount = 3;
        private const float VolleySpreadDegrees = 45f; // total sebaran nyebar "shotgun"
        private const int GreenBoltDamage = 10; // GreenBolt: target ~30 DMG Master / 3 (engine auto-triples proyektil di Master mode) - +20% otomatis kalau Last Stand
        private const int RedBoltDamage = 10;   // RedBolt: target ~30 DMG Master / 3 (engine auto-triples proyektil di Master mode) - +20% otomatis kalau Last Stand

        // ==========================================
        // Dipanggil dari dispatcher Spazmatism (leader) SEKALI pas pattern ini mulai.
        // Sekaligus nge-reset field punya Retinazer LANGSUNG di sini (lookup NPC-nya),
        // biar gak perlu pola "lazy-init di tick pertama" - lebih simpel & eksplisit.
        // ==========================================
        public static void Start(NPC spaz, TwinsReworkOverride self, Player target)
        {
            self.HopVolleyStateRaw = (float)Phase.Active;
            self.HopVolleyTimer = 0f;
            self.HopVolleyActive = true;
            self.HopVolleyVulnerable = true; // vulnerable dari hop pertama, lihat komentar header

            Vector2 arenaCenter = GetArenaCenter(spaz, out float arenaRadius);

            // Spazmatism (leader) dapat jatah hop-nya SENDIRI (5-10x, di-roll TERPISAH dari
            // punya Retinazer - BISA beda jumlah, BUKAN dipaksa sama kayak versi sebelumnya).
            self.HopVolleyRepeatsRequired = self.LastStandActive
                ? MaxHopsInclusive
                : Main.rand.Next(MinHops, MaxHopsInclusive + 1);
            self.HopVolleyHopsDone = 0;
            self.HopVolleyFinished = false;
            self.HopVolleyMoveTarget = PickRandomBorderPoint(arenaCenter, arenaRadius, null, null);

            // Retinazer dapat jatah hop-nya SENDIRI juga (di-roll TERPISAH LAGI - "timer ke-5"
            // beda-beda per Twin sesuai request), dan titik hop pertamanya WAJIB jaga jarak
            // dari titik punya Spaz biar dua Twin gak start numpuk di spot yang (nyaris) sama.
            NPC ret = FindRetinazer();
            if (ret != null)
            {
                TwinsReworkOverride retSelf = ret.GetGlobalNPC<TwinsReworkOverride>();
                retSelf.HopVolleyRepeatsRequired = self.LastStandActive
                    ? MaxHopsInclusive
                    : Main.rand.Next(MinHops, MaxHopsInclusive + 1);
                retSelf.HopVolleyHopsDone = 0;
                retSelf.HopVolleyFinished = false;
                retSelf.HopVolleyMoveTarget = PickRandomBorderPoint(arenaCenter, arenaRadius, self.HopVolleyMoveTarget, null);
                retSelf.HopVolleyDamageTrackingInit = false;
            }

            spaz.netUpdate = true;
        }

        // Dipanggil dari dispatcher Spazmatism (leader) tiap tick, sama kayak pattern lain.
        // Gerakan/tembakan Retinazer sendiri ADA DI RetinazerTick di bawah (dipanggil dari
        // PreAI Retinazer, TwinsRework.cs) - state-machine fase (Active/Regrouping/Done)
        // TETAP satu-satunya sumber kebenaran di sini (field self, instance Spazmatism).
        public static void Tick(NPC spaz, TwinsReworkOverride self, Player target)
        {
            NPC ret = FindRetinazer();
            Phase phase = (Phase)self.HopVolleyStateRaw;

            switch (phase)
            {
                case Phase.Active:
                    {
                        TwinsReworkOverride retSelf = ret != null ? ret.GetGlobalNPC<TwinsReworkOverride>() : null;

                        // "avoid" buat Spaz = titik yang lagi/baru dituju Retinazer, biar dua
                        // Twin gak numpuk di spot yang sama (lihat komentar header).
                        Vector2? avoidForSpaz = retSelf != null ? (Vector2?)retSelf.HopVolleyMoveTarget : null;
                        bool spazDone = TickHopMovement(spaz, self, target, avoidForSpaz);

                        bool retDone = ret == null || !ret.active || retSelf.HopVolleyFinished;

                        self.HopVolleyTimer++;
                        if ((int)self.HopVolleyTimer % 10 == 0)
                            spaz.netUpdate = true;

                        if (spazDone && retDone)
                        {
                            self.HopVolleyVulnerable = false; // balik invincible, siap regroup
                            self.HopVolleyTimer = 0f;
                            self.HopVolleyStateRaw = (float)Phase.Regrouping;
                            spaz.netUpdate = true;
                        }
                        break;
                    }

                case Phase.Regrouping:
                    {
                        // Spaz diem di titik hop terakhirnya - Retinazer yang terbang BALIK
                        // ke sini (lihat RetinazerTick), sama pola kayak TwinsSplitCombo.
                        spaz.velocity *= 0.9f;

                        Vector2 aimVector = target.Center - spaz.Center;
                        spaz.rotation = aimVector.ToRotation() - MathHelper.PiOver2;

                        self.HopVolleyTimer++;

                        bool retBack = ret == null
                            || Vector2.DistanceSquared(ret.Center, spaz.Center) <= RegroupArriveThreshold * RegroupArriveThreshold;
                        bool timedOut = self.HopVolleyTimer >= RegroupMaxDuration;

                        if (retBack || timedOut)
                        {
                            self.HopVolleyStateRaw = (float)Phase.Done;
                            self.HopVolleyActive = false; // Retinazer balik ke mode mirror normal mulai tick berikutnya
                            self.HopVolleyVulnerable = false;
                            spaz.netUpdate = true;
                        }
                        break;
                    }

                case Phase.Done:
                    break;
            }
        }

        public static bool IsDone(TwinsReworkOverride self)
        {
            return (Phase)self.HopVolleyStateRaw == Phase.Done;
        }

        // ==========================================
        // Dipanggil dari PreAI Retinazer sendiri (TwinsRework.cs), SETIAP TICK selama
        // spazSelf.HopVolleyActive true. "retSelf" = instance GlobalNPC MILIK RETINAZER
        // SENDIRI (InstancePerEntity = true) - dipakai buat nyimpen progress hop-nya
        // sendiri (HopVolleyMoveTarget/HopVolleyHopsDone/HopVolleyFinished), TERPISAH dari
        // punya Spazmatism, soalnya dua Twin ini beneran asinkron di pattern ini.
        // ==========================================
        public static void RetinazerTick(NPC ret, NPC spaz, TwinsReworkOverride spazSelf, TwinsReworkOverride retSelf, Player target)
        {
            Phase phase = (Phase)spazSelf.HopVolleyStateRaw;

            switch (phase)
            {
                case Phase.Active:
                    // "avoid" buat Retinazer = titik yang lagi/baru dituju Spazmatism.
                    TickHopMovement(ret, retSelf, target, spazSelf.HopVolleyMoveTarget);
                    break;

                case Phase.Regrouping:
                case Phase.Done:
                    MoveTowardStatic(ret, spaz.Center, RegroupSpeed, target);
                    break;
            }

            HandleVulnerability(ret, spaz, spazSelf, retSelf);
        }

        // ==========================================
        // MOVEMENT + FIRE — dipakai SAMA PERSIS buat Spazmatism (dari Tick) maupun
        // Retinazer (dari RetinazerTick), "self" di sini instance MILIK NPC yang lagi
        // gerak (npc.type dipakai buat nentuin proyektil mana yang keluar). Return true
        // begitu Twin ini udah nyelesein SELURUH jatah hop-nya (HopVolleyFinished).
        // ==========================================
        private static bool TickHopMovement(NPC npc, TwinsReworkOverride self, Player target, Vector2? partnerAvoidPoint)
        {
            if (self.HopVolleyFinished)
            {
                npc.velocity *= 0.9f; // udah kelar duluan - diem nunggu partner-nya
                return true;
            }

            Vector2 toTarget = self.HopVolleyMoveTarget - npc.Center;
            float distance = toTarget.Length();

            if (distance > HopArriveThreshold)
            {
                Vector2 moveDirection = toTarget / distance;
                npc.velocity = Vector2.Lerp(npc.velocity, moveDirection * HopMoveSpeed, 0.15f);

                Vector2 aimVector = target.Center - npc.Center;
                npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
                return false;
            }

            // Sampai di titik tujuan -> berhenti PAS di situ, langsung lepas volley.
            Vector2 arrivedAt = self.HopVolleyMoveTarget;
            npc.Center = arrivedAt;
            npc.velocity = Vector2.Zero;

            FireShotgunVolley(npc, target);
            self.HopVolleyHopsDone++;

            if (self.HopVolleyHopsDone >= self.HopVolleyRepeatsRequired)
            {
                self.HopVolleyFinished = true;
                npc.netUpdate = true;
                return true;
            }

            // Belum abis jatahnya - pilih titik border BARU. WAJIB jaga jarak (a) dari titik
            // yang lagi/baru dituju TWIN SATUNYA, dan (b) dari titik yang BARU AJA ditinggalin
            // sendiri (arrivedAt) - biar "ngga bisa nempel di tempat yang sama" (lihat komentar
            // header) - lalu langsung gerak cepat ke sana buat hop berikutnya.
            Vector2 arenaCenter = GetArenaCenter(npc, out float arenaRadius);
            self.HopVolleyMoveTarget = PickRandomBorderPoint(arenaCenter, arenaRadius, partnerAvoidPoint, arrivedAt);
            npc.netUpdate = true;
            return false;
        }

        // Volley "shotgun": 3 proyektil nyebar dari posisi Twin SAAT INI, arah dasarnya ke
        // player (live). Retinazer -> 3x RedPhantasmalBolt, Spazmatism -> 3x GreenBolt.
        private static void FireShotgunVolley(NPC npc, Player target)
        {
            Vector2 baseDir = (target.Center - npc.Center).SafeNormalize(-Vector2.UnitY);

            // Snap arah hadap PAS di titik tembak, biar keliatan "beneran nembak ke player"
            // dari sisi itu, bukan masih ngadep arah lama pas lagi terbang tadi.
            npc.rotation = baseDir.ToRotation() - MathHelper.PiOver2;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            bool isRetinazer = npc.type == NPCID.Retinazer;

            // CATATAN: TIDAK ada .GetGlobalProjectile<TwinsDebuffGlobalProjectile>().IsFromTwins
            // di sini — GreenBolt/RedPhantasmalBolt adalah custom ModProjectile milik kita
            // sendiri, TIDAK terdaftar di AppliesToEntity GlobalProjectile (cuma EyeFire/
            // DeathLaser/CursedFlameHostile vanilla yang terdaftar). Manggil GetGlobalProjectile
            // pada proyektil yang tidak terdaftar → KeyNotFoundException → crash. Dua-duanya
            // handle debuff sendiri di OnHitPlayer(), gak perlu IsFromTwins sama sekali.
            int boltType = isRetinazer ? ModContent.ProjectileType<RedPhantasmalBolt>() : ModContent.ProjectileType<GreenBolt>();
            int baseDamage = isRetinazer ? RedBoltDamage : GreenBoltDamage;
            float boltSpeed = isRetinazer ? RedPhantasmalBolt.StartSpeed : GreenBolt.StartSpeed;
            float projScale = isRetinazer ? 1.5f : 2f;

            for (int i = 0; i < VolleyProjectileCount; i++)
            {
                float t = (i / (float)(VolleyProjectileCount - 1)) - 0.5f; // -0.5, 0, 0.5
                float angleOffset = MathHelper.ToRadians(VolleySpreadDegrees) * t;
                Vector2 dir = baseDir.RotatedBy(angleOffset);
                Vector2 muzzle = npc.Center + dir * MouthOffset;

                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    muzzle,
                    dir * boltSpeed,
                    boltType,
                    TwinsReworkOverride.ScaleBoltDamage(baseDamage),
                    projScale,
                    Main.myPlayer,
                    dir.ToRotation(), // ai0: sudut arah terkunci (dibaca sendiri sama AI() bolt-nya)
                    0f                 // ai1: counter tick buat kurva speed exponential, mulai dari 0
                );
            }
        }

        // ==========================================
        // VULNERABILITY / DAMAGE TRANSFER — PERSIS pola TwinsSplitCombo.HandleVulnerability,
        // cuma field & pemicunya beda (HopVolleyVulnerable, bukan ComboSplitVulnerable).
        // Lihat komentar panjang di TwinsSplitCombo.cs buat penjelasan lengkap kenapa
        // transfer manual ini dibutuhkan & kenapa Last Stand WAJIB dikecualikan dari
        // transfer (biar gak numbangin finale terjadwal Last Stand secara prematur).
        // ==========================================
        private static void HandleVulnerability(NPC ret, NPC spaz, TwinsReworkOverride spazSelf, TwinsReworkOverride retSelf)
        {
            if (spazSelf.HopVolleyVulnerable && !spaz.dontTakeDamage)
            {
                ret.dontTakeDamage = false;

                if (!retSelf.HopVolleyDamageTrackingInit)
                {
                    retSelf.HopVolleyLastTrackedLife = ret.life;
                    retSelf.HopVolleyDamageTrackingInit = true;
                }

                if (ret.life < retSelf.HopVolleyLastTrackedLife)
                {
                    int damageTaken = retSelf.HopVolleyLastTrackedLife - ret.life;
                    retSelf.HopVolleyLastTrackedLife = ret.life;

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
                ret.dontTakeDamage = true;
                ret.lifeMax = spaz.lifeMax;
                ret.life = spaz.life;
                retSelf.HopVolleyDamageTrackingInit = false;
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

        // Titik acak di tepi lingkaran border, TAPI SEKARANG jaga jarak sudut minimal
        // (MinSeparationDegrees) dari sampai 2 titik yang mau dihindari - biasanya titik
        // punya Twin satunya (avoidA) & titik yang baru aja ditinggalin sendiri (avoidB).
        // Reroll max RerollAttempts kali; kalau abis kesempatan tetap gagal (kasus ekstrem,
        // arena kecil banget), pakai hasil percobaan terakhir apa adanya (jaga-jaga biar
        // gak infinite loop) - constraint "anti-nempel" ini best-effort, bukan garansi mutlak.
        private static Vector2 PickRandomBorderPoint(Vector2 arenaCenter, float radius, Vector2? avoidA, Vector2? avoidB)
        {
            float minSepRad = MathHelper.ToRadians(MinSeparationDegrees);
            float? avoidAngleA = avoidA.HasValue ? (float?)(avoidA.Value - arenaCenter).ToRotation() : null;
            float? avoidAngleB = avoidB.HasValue ? (float?)(avoidB.Value - arenaCenter).ToRotation() : null;

            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            for (int attempt = 0; attempt < RerollAttempts; attempt++)
            {
                bool tooCloseToA = avoidAngleA.HasValue
                    && System.Math.Abs(MathHelper.WrapAngle(angle - avoidAngleA.Value)) < minSepRad;
                bool tooCloseToB = avoidAngleB.HasValue
                    && System.Math.Abs(MathHelper.WrapAngle(angle - avoidAngleB.Value)) < minSepRad;

                if (!tooCloseToA && !tooCloseToB)
                    break;

                angle = Main.rand.NextFloat(MathHelper.TwoPi);
            }

            return arenaCenter + angle.ToRotationVector2() * radius;
        }

        // Gerak velocity-lerp generik ke sebuah titik statis, arah hadap ngikutin player -
        // dipakai buat Regrouping Retinazer (balik ke posisi Spaz).
        private static bool MoveTowardStatic(NPC npc, Vector2 destination, float speed, Player target)
        {
            Vector2 toTarget = destination - npc.Center;
            float distance = toTarget.Length();

            if (distance <= HopArriveThreshold)
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
    }
}
