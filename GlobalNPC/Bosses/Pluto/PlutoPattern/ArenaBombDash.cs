using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics; // Luminance ScreenShakeSystem
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    public partial class PlutoHead
    {
        // ==================================================================================
        // PATTERN 5: ARENA BOMB
        // Stage 0 (Approach)  : 🆕 Pluto mendekati player dulu (sama konsepnya kayak Stage 0
        //                       ElectroNovaDash) sampai jaraknya ~20 block dari player, atau
        //                       timeout. Ini WAJIB dilakuin SEBELUM border muncul, soalnya
        //                       border bakal ke-summon persis di posisi Pluto saat itu -- kalau
        //                       Pluto kejauhan / lagi nyasar ke area aneh (misal ketutup tanah),
        //                       border-nya bisa muncul di tempat yang ga fair buat player. Dengan
        //                       mendekat dulu ke sekitar player, border dijamin muncul di area
        //                       yang player lagi tempatin (bukan ujug-ujug di dalam tanah/jauh).
        // Stage 1 (Roar)      : Pluto diam total sebentar & "teriak" (roar) sambil noleh
        //                       ke arah player, bersiap manggil border.
        // Stage 2 (Summon)    : Border lingkaran raksasa muncul dengan posisi Pluto SAAT ITU
        //                       sebagai titik tengahnya (lihat PlutoArenaBorder.cs buat visualnya).
        //                       SEMUA player yang ada bakal ketarik masuk kalau posisinya di
        //                       luar border. Pluto sendiri bergerak keluar dari tengah menuju
        //                       garis border (radius penuh) selama stage ini.
        // Stage 3 (Orbit)     : Pluto muter terus di sepanjang garis border (30 detik) sambil
        //                       tiap 1 detik sekali muntahin 1-3 PlutoBomb (acak) dengan arah &
        //                       kecepatan acak per bomb (bomb-nya sendiri yang melambat &
        //                       berhenti sendiri di titik acak sekitar tengah border, lihat
        //                       PlutoBomb.cs). Selama fase ini (dan sesudahnya) player yang coba
        //                       kabur keluar border bakal terus ketarik balik.
        //                       Begitu 30 detik itu abis, Pluto TIDAK langsung berhenti muter --
        //                       dia bakal terus muter sambil ngecek tiap tick apakah masih ada
        //                       PlutoBomb yang hidup. Baru kalau udah bener-bener bersih, lanjut
        //                       ke stage berikutnya.
        // Stage 4 (Collapse)  : Pluto dash cepat dari posisi terakhirnya di garis border menuju
        //                       titik tengah border, border-nya di-trigger buat mengecil & fade
        //                       out (sama kayak PlutoPortal di PredicMineDash.cs), lalu pattern
        //                       selesai & PlutoHead.AI() otomatis gacha pattern lain lagi.
        // ==================================================================================

        // 🛑 [LOKASI BALANCING JARAK APPROACH] 20 block (1 block = 16px) -> 320px. Pluto berhenti
        // mendekat begitu jaraknya ke player udah <= ini, baru lanjut ke Roar & summon border.
        private const float ArenaApproachStopDistance = 320f;

        // 🛑 [LOKASI BALANCING KECEPATAN APPROACH]
        private const float ArenaApproachSpeed = 20f;

        // 🛑 [LOKASI BALANCING KECEPATAN NOLEH SAAT APPROACH]
        private const float ArenaApproachTurnSpeed = 0.1f;

        // 🛑 [JARING PENGAMAN] Anti-softlock kalau Pluto ga pernah berhasil deket player (misal
        // player kabur jauh / nyangkut). BUKAN cara normal buat skip jarak approach -- di kondisi
        // normal Pluto bakal berhenti duluan begitu masuk ArenaApproachStopDistance.
        private const int ArenaApproachMaxTime = 1800; // 30 detik

        // 🛑 [LOKASI BALANCING DURASI DIAM & TERIAK]
        private const int ArenaRoarTime = 45; // ~0.75 detik

        // 🛑 [LOKASI BALANCING KECEPATAN PLUTO NOLEH KE PLAYER SAAT ROAR]
        private const float ArenaRoarTurnSpeed = 0.12f;

        // 🛑 [LOKASI BALANCING DURASI PLUTO GERAK DARI TENGAH KE GARIS BORDER]
        private const int ArenaSummonRiseTime = 40;

        // 🛑 [LOKASI BALANCING RADIUS ARENA] 150 block x 150 block (1 block = 16px) -> diameter
        // ~2400px, jadi radius-nya ~1200px. Radius ASLI dipegang di PlutoArenaBorder biar cuma ada
        // SATU sumber kebenaran (dipakai bareng buat gambar border-nya juga).
        private static float ArenaRadius => PlutoArenaBorder.Radius;

        // 🛑 [LOKASI BALANCING KECEPATAN MUTER PLUTO DI GARIS BORDER] radian per tick.
        // ~0.012 rad/tick kira-kira 1 putaran penuh tiap ~8-9 detik.
        private const float ArenaOrbitAngularSpeed = 0.012f;

        // 🛑 [LOKASI BALANCING DURASI MUTER UTAMA] 30 detik (1800 tick) sebelum mulai ngecek bomb.
        private const int ArenaOrbitDuration = 1800;

        // 🛑 [LOKASI BALANCING INTERVAL MUNTAH BOMB] tiap 1 detik (60 tick) sekali.
        private const int ArenaBombSpawnInterval = 60;

        // 🛑 [LOKASI BALANCING KECEPATAN AWAL LEMPARAN BOMB] arah & besar kecepatan random per bomb,
        // biar titik berhentinya nyebar acak di sekitar tengah border (bukan numpuk di 1 titik).
        private const float ArenaBombMinLaunchSpeed = 12f;
        private const float ArenaBombMaxLaunchSpeed = 22f;

        // 🛑 [LOKASI BALANCING SEBARAN SUDUT LEMPARAN] deviasi sudut acak dari arah "ke tengah
        // border", biar bomb ga selalu lurus nembak ke 1 titik tengah doang.
        private const float ArenaBombAimSpreadDegrees = 55f;

        // 🛑 [LOKASI BALANCING DAMAGE KONTAK PLUTOBOMB] Sebelumnya damage bomb ikutan
        // NPC.damage/3 punya PlutoHead (jadi ~55 doang). Sekarang di-set independen jadi
        // konstanta tetap sendiri biar gampang di-nerf/buff tanpa keikut kalau damage Head
        // di-balancing ulang nanti.
        //
        // 🛑 [NERF ROUND 2] Diturunin lagi dari 90 -> 38. Round 1 (300 -> 90) ternyata masih
        // ke-observed ~600 di Master Mode (rasio aktual ~6.7x, lebih tinggi dari perkiraan awal).
        // 38 x ~6.7 ≈ 253, masuk ke target ~200-300. Kalau masih meleset pas dites, tinggal geser
        // angka ini lagi (rasio boleh dihitung: observed_baru / 38, terus base_baru = target / rasio).
        private const int ArenaBombContactDamage = 38;

        // 🛑 [LOKASI BALANCING JUMLAH BOMB PER LEMPARAN] tiap kali interval spawn kena (tiap 1
        // detik), Pluto muntahin sejumlah acak PlutoBomb sekaligus (inclusive kedua ujungnya),
        // masing-masing bomb punya arah & kecepatan acak sendiri-sendiri (lihat LaunchArenaBomb).
        private const int ArenaBombMinCountPerVolley = 1;
        private const int ArenaBombMaxCountPerVolley = 3;

        // 🛑 [LOKASI BALANCING KECEPATAN & JARAK "TARIKAN" ARENA] seberapa kuat player ditarik balik
        // kalau nekat keluar garis border.
        private const float ArenaPullMinSpeed = 4f;
        private const float ArenaPullMaxSpeed = 26f;
        private const float ArenaPullBurstSpeed = 34f; // tarikan awal pas border baru muncul

        // 🛑 [LOKASI BALANCING KECEPATAN DASH FINAL KE TENGAH BORDER]
        private const float ArenaCollapseDashSpeed = 40f;

        private void ExecuteArenaBombPattern(Player player) {
            int stage = (int)NPC.ai[1];
            int timer = (int)NPC.ai[2];

            if (stage == 0) {
                ExecuteArenaApproachStage(player, timer);
            }
            else if (stage == 1) {
                ExecuteArenaRoarStage(player, timer);
            }
            else if (stage == 2) {
                ExecuteArenaSummonStage(player, timer);
            }
            else if (stage == 3) {
                ExecuteArenaOrbitStage(timer);
            }
            else if (stage == 4) {
                ExecuteArenaCollapseStage(timer);
            }
        }

        // ======================================================================
        // 🆕 STAGE 0 - APPROACH: Pluto mendekati player sampai ~20 block sebelum lanjut ke
        // Roar & Summon. Konsepnya sama kayak ExecuteNovaApproachStage di ElectroNovaDash.cs.
        // ======================================================================
        private void ExecuteArenaApproachStage(Player player, int timer) {
            Vector2 dirToPlayer = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
            float distanceToPlayer = Vector2.Distance(NPC.Center, player.Center);

            if (distanceToPlayer > ArenaApproachStopDistance) {
                NPC.velocity = Vector2.Lerp(NPC.velocity, dirToPlayer * ArenaApproachSpeed, 0.06f);
                if (dirToPlayer != Vector2.Zero) {
                    NPC.rotation = NPC.rotation.AngleLerp(dirToPlayer.ToRotation(), ArenaApproachTurnSpeed);
                }
            }
            else {
                NPC.velocity *= 0.9f;
            }

            timer++;
            bool reachedPlayer = distanceToPlayer <= ArenaApproachStopDistance;
            bool timedOut = timer >= ArenaApproachMaxTime;

            if (reachedPlayer || timedOut) {
                NPC.ai[1] = 1f;
                NPC.ai[2] = 0f;
                NPC.netUpdate = true;
            }
            else {
                NPC.ai[2] = timer;
            }
        }

        private void ExecuteArenaRoarStage(Player player, int timer) {
            NPC.velocity *= 0.85f;

            Vector2 targetDir = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
            if (targetDir != Vector2.Zero) {
                NPC.rotation = NPC.rotation.AngleLerp(targetDir.ToRotation(), ArenaRoarTurnSpeed);
            }

            if (timer == 0) {
                // 🛑 [CATATAN] Pakai SoundID.Roar (vanilla) buat "teriak"-nya. Kalau nanti udah
                // ada file suara custom sendiri (misal PlutoRoar.wav di folder PlutoSound), tinggal
                // ganti baris ini jadi SoundStyle kayak dash sound di pattern lain.
                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
            }

            timer++;
            if (timer >= ArenaRoarTime) {
                NPC.ai[1] = 2f;
                NPC.ai[2] = 0f;
                NPC.netUpdate = true;
            }
            else {
                NPC.ai[2] = timer;
            }
        }

        private void ExecuteArenaSummonStage(Player player, int timer) {
            if (timer == 0) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center,
                        Vector2.Zero,
                        ModContent.ProjectileType<PlutoArenaBorder>(),
                        0,
                        0f,
                        Main.myPlayer,
                        NPC.whoAmI // ai[0] border = pemilik (Head ini)
                    );
                }

                ScreenShakeSystem.StartShake(16f, 30, Vector2.Zero);
                SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{Main.rand.Next(1, 3)}"), NPC.Center);

                // 🛑 [TARIKAN AWAL] Player yang posisinya di luar radius border langsung kena
                // hentakan tarikan kuat sekali pas border baru muncul.
                ApplyArenaPullBurst(NPC.Center, ArenaRadius);

                NPC.netUpdate = true;
            }

            Projectile border = FindOwnedArenaBorder();

            // Bergerak keluar dari titik tengah menuju garis border, mengikuti arah hadap
            // (NPC.rotation) yang udah dikunci dari akhir stage Roar sebelumnya.
            float progress = MathHelper.Clamp((float)timer / ArenaSummonRiseTime, 0f, 1f);
            float eased = progress * progress * (3f - 2f * progress); // smoothstep

            Vector2 centerPoint = border != null ? border.Center : NPC.Center;
            Vector2 outDir = NPC.rotation.ToRotationVector2();
            NPC.Center = centerPoint + outDir * (ArenaRadius * eased);
            NPC.velocity = Vector2.Zero;

            ConfinePlayersToArena(centerPoint, ArenaRadius);

            timer++;
            if (timer >= ArenaSummonRiseTime) {
                NPC.ai[1] = 3f;
                NPC.ai[2] = 0f;
                NPC.netUpdate = true;
            }
            else {
                NPC.ai[2] = timer;
            }
        }

        private void ExecuteArenaOrbitStage(int timer) {
            Projectile border = FindOwnedArenaBorder();

            if (border == null) {
                // 🛑 [JARING PENGAMAN] Kalau kejadian border-nya ilang duluan (misal dikill server
                // command dsb), jangan sampai softlock -- langsung tutup pattern & gacha lagi.
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.ai[2] = 0f;
                NPC.ai[3] = 0f;
                NPC.netUpdate = true;
                return;
            }

            float currentAngle = (NPC.Center - border.Center).ToRotation();
            float newAngle = currentAngle + ArenaOrbitAngularSpeed;

            NPC.Center = border.Center + newAngle.ToRotationVector2() * ArenaRadius;
            NPC.velocity = Vector2.Zero;
            NPC.rotation = newAngle + MathHelper.PiOver2; // menghadap searah arah muter (tangensial)

            ConfinePlayersToArena(border.Center, ArenaRadius);

            bool stillSpawningBombs = timer < ArenaOrbitDuration;

            if (stillSpawningBombs && timer % ArenaBombSpawnInterval == 0) {
                LaunchArenaBomb(border.Center);
            }

            if (!stillSpawningBombs && !AnyArenaBombsRemain()) {
                // 🛑 Waktu muter utama abis DAN semua PlutoBomb udah bersih -> baru lanjut collapse.
                NPC.ai[1] = 4f;
                NPC.ai[2] = 0f;
                NPC.netUpdate = true;
                return;
            }

            timer++;
            NPC.ai[2] = timer;
        }

        private void ExecuteArenaCollapseStage(int timer) {
            Projectile border = FindOwnedArenaBorder();
            Vector2 targetCenter = border != null ? border.Center : NPC.Center;

            if (timer == 0) {
                Vector2 dashDir = (targetCenter - NPC.Center).SafeNormalize(Vector2.Zero);
                float distanceToCenter = Vector2.Distance(NPC.Center, targetCenter);

                NPC.velocity = dashDir * ArenaCollapseDashSpeed;
                NPC.rotation = dashDir.ToRotation();
                dashDuration = distanceToCenter / ArenaCollapseDashSpeed;
                if (dashDuration > 90f) dashDuration = 90f;

                SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{Main.rand.Next(1, 3)}"), NPC.Center);

                // Trigger border buat mengecil & fade out (sama kayak PlutoPortal di PredicMineDash).
                if (border != null && Main.netMode != NetmodeID.MultiplayerClient) {
                    border.ai[1] = 1f;
                    border.netUpdate = true;
                }

                NPC.netUpdate = true;
            }
            else {
                NPC.rotation = NPC.velocity.ToRotation();
            }

            if (border != null) {
                ConfinePlayersToArena(targetCenter, ArenaRadius);
            }

            timer++;
            if (timer >= (int)dashDuration) {
                // Selesai total -> biarkan PlutoHead.AI() gacha pattern lain.
                NPC.ai[0] = 0f;
                NPC.ai[1] = 0f;
                NPC.ai[2] = 0f;
                NPC.ai[3] = 0f;
                NPC.netUpdate = true;
            }
            else {
                NPC.ai[2] = timer;
            }
        }

        // ======================================================================
        // HELPER - cari border projectile milik Head ini
        // ======================================================================
        private Projectile FindOwnedArenaBorder() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == ModContent.ProjectileType<PlutoArenaBorder>() && (int)proj.ai[0] == NPC.whoAmI) {
                    return proj;
                }
            }
            return null;
        }

        // ======================================================================
        // HELPER - cek apakah masih ada PlutoBomb hidup punya Head ini
        // ======================================================================
        private bool AnyArenaBombsRemain() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.type == ModContent.ProjectileType<PlutoBomb>() && (int)proj.ai[0] == NPC.whoAmI) {
                    return true;
                }
            }
            return false;
        }

        // ======================================================================
        // HELPER - muntahin 1-3 PlutoBomb (acak) sekaligus tiap kali interval kena. Tiap bomb
        // dapet arah & kecepatan acaknya SENDIRI-SENDIRI (dipanggil lewat LaunchSingleArenaBomb),
        // jadi walau nyembur bareng, sebarannya tetap acak & ga numpuk di 1 lintasan yang sama.
        // ======================================================================
        private void LaunchArenaBomb(Vector2 arenaCenter) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int bombCount = Main.rand.Next(ArenaBombMinCountPerVolley, ArenaBombMaxCountPerVolley + 1);
            for (int i = 0; i < bombCount; i++) {
                LaunchSingleArenaBomb(arenaCenter);
            }
        }

        // ======================================================================
        // HELPER - muntahin 1 PlutoBomb dengan arah & kecepatan acak menuju sekitar
        // tengah border, jadi titik berhentinya nyebar random (lihat friction di PlutoBomb.cs)
        // ======================================================================
        private void LaunchSingleArenaBomb(Vector2 arenaCenter) {
            Vector2 dirToCenter = (arenaCenter - NPC.Center).SafeNormalize(Vector2.UnitX);
            float spreadRad = MathHelper.ToRadians(Main.rand.NextFloat(-ArenaBombAimSpreadDegrees, ArenaBombAimSpreadDegrees));
            Vector2 launchDir = dirToCenter.RotatedBy(spreadRad);
            float launchSpeed = Main.rand.NextFloat(ArenaBombMinLaunchSpeed, ArenaBombMaxLaunchSpeed);

            int bomb = Projectile.NewProjectile(
                NPC.GetSource_FromAI(),
                NPC.Center,
                launchDir * launchSpeed,
                ModContent.ProjectileType<PlutoBomb>(),
                ArenaBombContactDamage,
                0f,
                Main.myPlayer
            );

            if (bomb != Main.maxProjectiles) {
                Main.projectile[bomb].ai[0] = NPC.whoAmI; // tandai kepemilikan buat AnyArenaBombsRemain()
                Main.projectile[bomb].netUpdate = true;
            }
        }

        // ======================================================================
        // HELPER - narik SEMUA player yang posisinya keluar dari radius border balik ke dalam.
        // Dipanggil tiap tick selama border aktif (stage Summon, Orbit, & Collapse).
        // ======================================================================
        private void ConfinePlayersToArena(Vector2 arenaCenter, float radius) {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;

                float distance = Vector2.Distance(p.Center, arenaCenter);
                if (distance <= radius) continue;

                Vector2 pullDir = (arenaCenter - p.Center).SafeNormalize(Vector2.Zero);
                float overshoot = distance - radius;
                float pullSpeed = MathHelper.Clamp(overshoot * 0.05f, ArenaPullMinSpeed, ArenaPullMaxSpeed);

                p.velocity = Vector2.Lerp(p.velocity, pullDir * pullSpeed, 0.2f);
            }
        }

        // ======================================================================
        // HELPER - hentakan tarikan sekali pas border baru muncul, biar kerasa "ketarik" beneran
        // ======================================================================
        private void ApplyArenaPullBurst(Vector2 arenaCenter, float radius) {
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (!p.active || p.dead) continue;

                if (Vector2.Distance(p.Center, arenaCenter) <= radius) continue;

                Vector2 pullDir = (arenaCenter - p.Center).SafeNormalize(Vector2.Zero);
                p.velocity = pullDir * ArenaPullBurstSpeed;
            }
        }
    }
}
