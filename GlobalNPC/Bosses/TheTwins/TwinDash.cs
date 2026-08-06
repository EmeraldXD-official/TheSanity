using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // PATTERN 1: DASH (dulu namanya TwinsAttackPatterns, di file TwinsPatterns.cs)
    // ==========================================
    // Dipisah dari TwinsReworkOverride.cs biar file utama gak kepanjangan pas
    // pattern-nya nambah banyak nanti. Ai utama (PreAI di TwinsReworkOverride)
    // tinggal manggil method static di sini, misal:
    //
    //   TwinDash.Pattern1(npc, this, target);
    //
    // Method di sini baca/tulis:
    //   - npc.ai[0] = state pattern ini (di-cast ke enum State di bawah)
    //   - npc.ai[1] = timer buat state itu
    //   - field-field public di "self" (instance TwinsReworkOverride punya NPC ini)
    //     buat nyimpen hal yang gak muat di ai[0]/ai[1], kek arah dash yg dikunci
    //     dan sisa jumlah pengulangan dash.
    public static class TwinDash
    {
        private enum State
        {
            Hovering,
            Telegraph,
            Dashing,
            DashCooldown
        }

        // ---- Tunable knobs, gampang dioprek kalau mau ubah feel-nya ----
        // SEBELUMNYA HoverDuration = 90f (~1.5 detik) DAN HoverBehavior ngejar ke titik PERSIS
        // di atas kepala player — kesannya nunggu sampai "nyampe" dulu baru mulai nge-dash.
        // SEKARANG DIPERSINGKAT BANGET: cuma gerak sedikit (0.15 detik) abis itu LANGSUNG
        // masuk Telegraph & mulai ngincer buat dash, TANPA perlu beneran nyampe di atas kepala
        // player dulu. HoverBehavior tetap dipanggil (jadi masih ada sedikit gerakan "narik
        // ancang-ancang"), tapi timer-nya udah gak nunggu sampai posisi ideal tercapai.
        private const float HoverDuration = 9f;          // ~0.15 detik doang, gerak sedikit lalu langsung nge-aim dash
        public const float TelegraphDuration = 18f;      // 0.3 detik (60 tick/detik) — dibaca juga dari TwinsReworkOverride buat animasi garis
        private const float DashDuration = 55f;          // diperpanjang biar jarak tempuh jauh
        private const float DashSpeed = 34f;             // dipercepat, biar dash-nya nembus jauh ngelewatin player
        private const float DashCooldownDuration = 12f;  // jeda singkat antar-dash biar gak berasa instan banget
        private const int MinDashRepeats = 3;
        private const int MaxDashRepeatsInclusive = 5;

        // Interval nembak EyeFire SELAMA dash berlangsung (spam). DeathLaser gak pakai
        // interval lagi — cuma sekali di awal tiap dash. Nilai dalam tick (60 tick = 1 detik).
        private const int EyeFireIntervalTicks = 6;      // EyeFire di-spam tiap 0.1 detik

        // ---- ENRAGED (Phase 3): side-bolt RedPhantasmalBolt tiap 5 block selama dash ----
        private const float DashBoltSpacing = 80f;       // 5 block * 16px/block
        private const int DashBoltDamage = 13; // RedBolt: target 40 DMG Master / 3 (engine auto-triples proyektil di Master mode)

        // ==========================================
        // PATTERN 1: Hover/kejar -> (Telegraph -> Dash) diulang 3-5x -> balik Hover
        // ==========================================
        // Return value: true SEKALI di tick pas seluruh siklus pattern ini (hover -> semua
        // dash repeat) baru aja kelar dan balik ke Hovering. Dipakai dispatcher utama di
        // TwinsRework.cs buat tahu kapan waktunya gantian ke pattern lain (RetBeam).
        public static bool Pattern1(NPC npc, TwinsReworkOverride self, Player target)
        {
            ref float stateRaw = ref npc.ai[0];
            ref float timer = ref npc.ai[1];
            State state = (State)stateRaw;
            bool cycleFinished = false;

            switch (state)
            {
                case State.Hovering:
                    HoverBehavior(npc, target);
                    timer++;

                    if (timer >= HoverDuration)
                    {
                        // Tentukan berapa kali dash bakal diulang sebelum kembali hover (3-5x)
                        self.DashRepeatsRemaining = Main.rand.Next(MinDashRepeats, MaxDashRepeatsInclusive + 1);
                        StartTelegraph(npc, self, target);

                        stateRaw = (float)State.Telegraph;
                        timer = 0f;
                        npc.netUpdate = true;
                    }
                    break;

                case State.Telegraph:
                    // Ancang-ancang: hampir diam, arah SUDAH DIKUNCI dari awal telegraph
                    // (self.TelegraphDirection) dan TIDAK diupdate lagi walau player gerak.
                    npc.velocity *= 0.9f;
                    npc.rotation = self.TelegraphDirection.ToRotation() - MathHelper.PiOver2;
                    timer++;

                    if (timer >= TelegraphDuration)
                    {
                        self.IsTelegraphing = false; // garis hijau hilang

                        // LEPASKAN DASH ke arah yang udah dikunci tadi
                        npc.velocity = self.TelegraphDirection * DashSpeed;
                        npc.netUpdate = true;

                        // ENRAGED: reset akumulator jarak buat side-bolt tiap 5 block, biar
                        // tiap dash BARU mulai ngitung dari 0 lagi (bukan nyambung sisa dash
                        // sebelumnya).
                        self.DashBoltDistanceAccum = 0f;

                        // Suara "Roar" tiap dash dilepas — pakai sound internal Terraria,
                        // gak perlu asset custom.
                        SoundEngine.PlaySound(SoundID.Roar, npc.Center);

                        // DeathLaser cukup SEKALI di awal tiap dash (bukan di-spam sepanjang
                        // dash lagi seperti EyeFire) — tapi tetap kepanggil di SETIAP dash
                        // repeat (bukan cuma dash pertama), soalnya baris ini jalan tiap kali
                        // state pindah dari Telegraph ke Dashing, dan itu terjadi tiap repeat.
                        FireDeathLaserVolley(npc, self, self.TelegraphDirection);

                        self.IsDashing = true; // after-image jalan terus, ini cuma nandain "lagi dash" buat animasi/efek lain

                        stateRaw = (float)State.Dashing;
                        timer = 0f;
                    }
                    break;

                case State.Dashing:
                    npc.rotation = npc.velocity.ToRotation() - MathHelper.PiOver2;

                    // EyeFire tetap di-spam selama dash jalan. DeathLaser TIDAK lagi di sini —
                    // dia cuma sekali di awal dash (lihat pemanggilannya di state Telegraph di atas).
                    int tickInDash = (int)timer;
                    if (tickInDash % EyeFireIntervalTicks == 0)
                    {
                        FireEyeFire(npc, self.TelegraphDirection);
                    }

                    // ---- ENRAGED: tiap nempuh 5 block (80px) SELAMA dash, muncrat
                    // RedPhantasmalBolt ke KANAN & KIRI badan (tegak lurus arah dash saat
                    // itu) - dijaga berbasis JARAK TEMPUH (bukan interval tick tetap), jadi
                    // spasinya konsisten walau dash lagi deselerasi menjelang akhir. ----
                    if (self.IsEnraged)
                    {
                        self.DashBoltDistanceAccum += npc.velocity.Length();
                        if (self.DashBoltDistanceAccum >= DashBoltSpacing)
                        {
                            self.DashBoltDistanceAccum -= DashBoltSpacing;
                            Vector2 dashDir = npc.velocity.SafeNormalize(self.TelegraphDirection);
                            FireDashSideBolts(npc, dashDir);
                        }
                    }

                    timer++;

                    // Deselerasi menjelang akhir dash
                    if (timer > DashDuration - 10f)
                    {
                        npc.velocity *= 0.92f;
                    }

                    if (timer >= DashDuration)
                    {
                        self.IsDashing = false; // cuma nandain dash selesai, trail tetap jalan terus

                        self.DashRepeatsRemaining--;
                        if (self.DashRepeatsRemaining > 0)
                        {
                            stateRaw = (float)State.DashCooldown;
                        }
                        else
                        {
                            stateRaw = (float)State.Hovering;
                            cycleFinished = true; // habis semua repeat -> siklus Dash selesai
                        }

                        timer = 0f;
                        npc.netUpdate = true;
                    }
                    break;

                case State.DashCooldown:
                    npc.velocity *= 0.96f;
                    timer++;

                    if (timer >= DashCooldownDuration)
                    {
                        StartTelegraph(npc, self, target);
                        stateRaw = (float)State.Telegraph;
                        timer = 0f;
                        npc.netUpdate = true;
                    }
                    break;
            }

            return cycleFinished;
        }

        // Dipanggil dispatcher (TwinsRework.cs) pas gantian DARI RetBeam BALIK ke Dash,
        // biar Dash mulai bersih dari State.Hovering (bukan nyambung dari state sisa
        // terakhir kali dia aktif).
        public static void ResetToHover(NPC npc)
        {
            npc.ai[0] = (float)State.Hovering;
            npc.ai[1] = 0f;
        }

        private static void HoverBehavior(NPC npc, Player target)
        {
            Vector2 hoverSpot = target.Center - new Vector2(0f, 200f);
            Vector2 moveDirection = hoverSpot - npc.Center;
            npc.velocity = Vector2.Lerp(npc.velocity, moveDirection * 0.05f, 0.1f);

            Vector2 aimVector = target.Center - npc.Center;
            npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
        }

        private static void StartTelegraph(NPC npc, TwinsReworkOverride self, Player target)
        {
            // Arah diambil SEKALI di sini terus DIKUNCI di field self.TelegraphDirection.
            // Makanya garis (dan dash-nya nanti) tidak "nge-lock" ke player walau player
            // pindah posisi selama 0.3 detik telegraph berjalan.
            self.TelegraphDirection = (target.Center - npc.Center).SafeNormalize(-Vector2.UnitY);
            self.IsTelegraphing = true;
        }

        // 1x EyeFire dari "mulut" Spazmatism, nembak SEARAH dash. Dipanggil berkala
        // (tiap EyeFireIntervalTicks) selama state Dashing, jadi berasa "di-spam".
        private static void FireEyeFire(NPC npc, Vector2 dashDirection)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // 1 block Terraria = 16px, jadi 10 block = 160px dari npc.Center (bukan
            // ditambah offset "mulut" lama, langsung diganti jadi 10 block persis).
            const float BlockSize = 16f;
            const float BlocksAhead = 10f;
            Vector2 mouthPos = npc.Center + dashDirection * (BlockSize * BlocksAhead);
            int eyeFireIndex = Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                mouthPos,
                dashDirection * 9f,
                ProjectileID.EyeFire,
                12, // Eye Fire: target 35 DMG Master / 3 (engine auto-triples proyektil di Master mode)
                2f,
                Main.myPlayer
            );
            Main.projectile[eyeFireIndex].GetGlobalProjectile<TwinsDebuffGlobalProjectile>().IsFromTwins = true;
        }

        // ---- ENRAGED (Phase 3): sepasang RedPhantasmalBolt ke KANAN & KIRI badan (tegak
        // lurus arah dash), dipanggil tiap kali akumulator jarak (DashBoltDistanceAccum)
        // nyampe DashBoltSpacing (5 block) selama state Dashing. ----
        private static void FireDashSideBolts(NPC npc, Vector2 dashDirection)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 left = dashDirection.RotatedBy(-MathHelper.PiOver2);
            Vector2 right = -left;
            int boltType = ModContent.ProjectileType<RedPhantasmalBolt>();

            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                npc.Center,
                left * RedPhantasmalBolt.StartSpeed,
                boltType,
                DashBoltDamage,
                1.5f,
                Main.myPlayer,
                left.ToRotation(), // ai0: sudut arah terkunci
                0f                  // ai1: counter tick exponential
            );

            Projectile.NewProjectile(
                npc.GetSource_FromAI(),
                npc.Center,
                right * RedPhantasmalBolt.StartSpeed,
                boltType,
                DashBoltDamage,
                1.5f,
                Main.myPlayer,
                right.ToRotation(),
                0f
            );
        }

        // 5x DeathLaser (RedLaser) nyebar kek shotgun, searah dash. Dipanggil SEKALI tiap awal
        // dash (bukan berkala lagi) — tapi tetap kepanggil di setiap dash repeat, bukan cuma
        // yang pertama. Semua RedLaser di sini tileCollide = false biar tembus block/tile.
        //
        // SENGAJA TIDAK manggil TriggerEyeFlash di sini — laser dash ini nembak TANPA efek
        // glow mata (beda dari BorderShot/LaserBarrage yang tetap pakai pulse, dan RetBeam
        // yang punya continuous glow sendiri). Parameter "self" masih dipertahankan di
        // signature biar minim ubah call site, walau sekarang gak dipakai isinya di sini.
        private static void FireDeathLaserVolley(NPC npc, TwinsReworkOverride self, Vector2 dashDirection)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            const int PelletCount = 5;
            float spreadArc = MathHelper.ToRadians(50f); // total sebaran ~50 derajat, bisa diubah

            for (int i = 0; i < PelletCount; i++)
            {
                float t = PelletCount == 1 ? 0f : (i / (float)(PelletCount - 1)) - 0.5f; // -0.5 .. 0.5
                float angleOffset = t * spreadArc;
                Vector2 shotDirection = dashDirection.RotatedBy(angleOffset);

                int pelletIndex = Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    shotDirection * 11f,
                    ProjectileID.DeathLaser,
                    8, // Red laser/Death laser: target 25 DMG Master / 3 (engine auto-triples proyektil di Master mode)
                    1.5f,
                    Main.myPlayer
                );
                Main.projectile[pelletIndex].tileCollide = false; // RedLaser tembus block
                Main.projectile[pelletIndex].GetGlobalProjectile<TwinsDebuffGlobalProjectile>().IsFromTwins = true;
            }
        }
    }
}
