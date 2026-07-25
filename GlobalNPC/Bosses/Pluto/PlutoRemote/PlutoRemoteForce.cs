using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics; // ScreenShakeSystem
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    // =====================================================================================
    // 🛑 [PLUTO REMOTE] Dipanggil dari item PlutoRemote (TheSanity/GlobalNPC/Bosses/Pluto/
    // PlutoRemote/PlutoRemote.cs) lewat GUI pilih-pattern. Tujuannya buat MEMUDAHKAN MODDING/
    // TESTING -- paksa Pluto langsung masuk ke pattern tertentu TANPA nunggu gacha random
    // (NPC.ai[0] == 0f di PlutoHead.AI()) & TANPA nunggu fase aim/approach yang biasanya makan
    // waktu beberapa detik. Filosofinya: tiap pattern langsung dilempar ke STAGE PALING
    // "ACTION"-nya (biasanya Dash/Orbit/ChaseDash), bukan cuma restart dari Stage 0 -- biar
    // begitu remote dipencet, efeknya kerasa INSTAN.
    //
    // 🛑 [KETERBATASAN - PATTERN 4 / ELECTRO NOVA] Momen "bola listrik lagi diserap/mengecil
    // sebelum meledak" itu logic-nya ada DI DALAM PlutoElectroBall.cs (properti IsInFinalPhase,
    // method BeginShrinking(), dll) -- file itu TIDAK ada di konteks pas nulis file ini, jadi
    // paksaan buat pattern 4 di bawah cuma bisa nembus sampai Stage 4 (Chase Dash) dengan bola
    // yang UDAH di-spawn & di-launch (skip Approach/Rise/Charge ~6 detik), BUKAN persis ke momen
    // "diserap"-nya. Kalau butuh presisi ke situ, tambahin method public di PlutoElectroBall.cs
    // (misal `public void ForceFinalPhase()`) lalu panggil dari ForceElectroNovaPattern() di bawah.
    //
    // 🛑 [BATASAN MULTIPLAYER] ForcePattern() cuma jalan di singleplayer ATAU di sisi yang punya
    // otoritas NPC (netMode != MultiplayerClient). Kalau item ini dipakai dari client yang numpang
    // konek ke dedicated server, dia GAK bakal ngefek (lihat guard paling atas) -- butuh packet
    // custom lewat Mod.HandlePacket buat itu, sengaja belum ditambahin di sini karena file Mod
    // utamanya (TheSanity.cs / Mod class) gak ada di konteks ini.
    // =====================================================================================
    public partial class PlutoHead
    {
        public void ForcePattern(int patternId) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Player player = null;
            if (NPC.target >= 0 && NPC.target < Main.maxPlayers) {
                player = Main.player[NPC.target];
            }
            if (player == null || !player.active || player.dead) {
                NPC.TargetClosest(true);
                player = Main.player[NPC.target];
            }
            if (player == null || !player.active || player.dead) return;

            // Reset state umum biar gak nyisa dari pattern sebelumnya.
            NPC.velocity = Vector2.Zero;
            NPC.alpha = 0;
            NPC.dontTakeDamage = false;
            NPC.ai[3] = 0f;

            switch (patternId) {
                case 1: ForceNormalDashPattern(player); break;
                case 2: ForceTrickDashPattern(player); break;
                case 3: ForceTeleportDashPattern(player); break;
                case 4: ForceElectroNovaPattern(player); break;
                case 5: ForceArenaBombPattern(player); break;
                case 6: ForceProbeSwarmPattern(player); break;
                case 7: ForceCrystalDivePattern(player); break;
                default: return;
            }

            NPC.netUpdate = true;
        }

        // ======================================================================
        // PATTERN 1 - NORMAL DASH: langsung ke Stage Dash (skip 0,5 detik Aim). Logic dash-nya
        // SAMA PERSIS kayak cabang timer>=30 di ExecuteDashPattern (NormalDash.cs).
        // ======================================================================
        private void ForceNormalDashPattern(Player player) {
            maxDashes = Main.rand.Next(5, 9);

            Vector2 dashDir = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
            float distanceToPlayer = Vector2.Distance(NPC.Center, player.Center);
            float totalDashDistance = distanceToPlayer + 3200f;
            float dashSpeed = 44f;

            NPC.velocity = dashDir * dashSpeed;
            NPC.rotation = dashDir.ToRotation();
            dashDuration = totalDashDistance / dashSpeed;
            if (dashDuration > 150f) dashDuration = 150f;

            NPC.ai[0] = 1f;
            NPC.ai[1] = 1f; // Stage Dash
            NPC.ai[2] = 0f;
            NPC.ai[3] = 1f;

            SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{Main.rand.Next(1, 3)}"), NPC.Center);
        }

        // ======================================================================
        // PATTERN 2 - TRICK DASH (NUKE): langsung ke Stage Dash, target offset samping dihitung
        // ulang persis kayak cabang timer>=30 di ExecuteTrickDashPattern (NukeDash.cs).
        // ======================================================================
        private void ForceTrickDashPattern(Player player) {
            maxDashes = Main.rand.Next(3, 6);

            Vector2 baseDir = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero);
            Vector2 sideOffsetDir = baseDir.RotatedBy(Main.rand.NextBool() ? MathHelper.PiOver2 : -MathHelper.PiOver2);
            Vector2 trickTargetPos = player.Center + sideOffsetDir * 450f;

            Vector2 dashDir = (trickTargetPos - NPC.Center).SafeNormalize(Vector2.Zero);
            float distanceToTarget = Vector2.Distance(NPC.Center, trickTargetPos);
            float totalDashDistance = distanceToTarget + 3200f;
            float dashSpeed = 44f;

            NPC.velocity = dashDir * dashSpeed;
            NPC.rotation = dashDir.ToRotation();
            dashDuration = totalDashDistance / dashSpeed;
            if (dashDuration > 150f) dashDuration = 150f;

            triggeredProjThisDash = false;
            projSequenceActive = false;
            projWaveCount = 0;
            projSegmentIndex = 0;
            projDelayTimer = 0;

            NPC.ai[0] = 2f;
            NPC.ai[1] = 1f; // Stage Dash
            NPC.ai[2] = 0f;
            NPC.ai[3] = 1f;

            SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{Main.rand.Next(1, 3)}"), NPC.Center);
        }

        // ======================================================================
        // PATTERN 3 - TELEPORT DASH (PREDICTIVE MINE): teleport instan ke sekitar player (SAMA
        // kayak logic timer==0 di ExecuteTeleportDashPattern), TAPI TANPA fase invisible 2 detik
        // -- begitu Remote dipencet, Pluto langsung KELIATAN & langsung masuk Super Dash + muntah
        // PlutoMine, skip 2 detik nunggu Aiming (PredicMineDash.cs).
        // ======================================================================
        private void ForceTeleportDashPattern(Player player) {
            maxDashes = Main.rand.Next(3, 6);

            float randAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
            Vector2 teleportOffset = randAngle.ToRotationVector2() * 950f;
            NPC.Center = player.Center + teleportOffset;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC segment = Main.npc[i];
                if (segment.active && segment.ai[3] == NPC.whoAmI &&
                   (segment.type == ModContent.NPCType<PlutoBody>() || segment.type == ModContent.NPCType<PlutoTail>())) {
                    segment.Center = NPC.Center;
                    segment.netUpdate = true;
                }
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < Main.maxProjectiles; i++) {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.type == ModContent.ProjectileType<PlutoPortal>() && p.ai[0] == NPC.whoAmI) {
                        p.ai[1] = 1f;
                        p.netUpdate = true;
                    }
                }

                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    NPC.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<PlutoPortal>(),
                    0,
                    0f,
                    Main.myPlayer,
                    NPC.whoAmI
                );
            }

            Vector2 predictedPos = player.Center + player.velocity * 9f;
            Vector2 aimDirection = (predictedPos - NPC.Center).SafeNormalize(Vector2.UnitX);

            float distanceToPredicted = Vector2.Distance(NPC.Center, predictedPos);
            float totalDashDistance = distanceToPredicted + 3500f;
            float dashSpeed = 44f * 5f;

            NPC.velocity = aimDirection * dashSpeed;
            NPC.rotation = aimDirection.ToRotation();
            dashDuration = totalDashDistance / dashSpeed;
            if (dashDuration > 150f) dashDuration = 150f;

            ScreenShakeSystem.StartShake(22f, 35, aimDirection);
            SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{Main.rand.Next(1, 3)}"), NPC.Center);

            NPC.ai[0] = 3f;
            NPC.ai[1] = 1f; // Stage Super Dash (muntah PlutoMine)
            NPC.ai[2] = 0f;
            NPC.ai[3] = 1f;
        }

        // ======================================================================
        // PATTERN 4 - ELECTRO NOVA: langsung ke Stage 4 (Chase Dash) dengan bola udah di-spawn &
        // di-launch (skip Approach/Rise/Charge/Push, ~6+ detik). LIHAT CATATAN KETERBATASAN di
        // banner atas file ini -- ini BUKAN persis momen "bola diserap", cuma bola-udah-terbang.
        // ======================================================================
        private void ForceElectroNovaPattern(Player player) {
            Vector2 spawnPos = NPC.Center + new Vector2(0f, -(NPC.height * NPC.scale) - 40f);
            NPC.rotation = -MathHelper.PiOver2;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int idx = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    spawnPos,
                    Vector2.Zero,
                    ModContent.ProjectileType<PlutoElectroBall>(),
                    NPC.damage,
                    0f,
                    Main.myPlayer,
                    NPC.whoAmI, // ai[0] = pemilik
                    0f          // ai[1] = state awal (charging) -- langsung di-override jadi 1 di bawah
                );

                if (idx != Main.maxProjectiles) {
                    Projectile ball = Main.projectile[idx];
                    Vector2 predictedPos = player.Center + player.velocity * 12f;
                    Vector2 launchDir = (predictedPos - ball.Center).SafeNormalize(-Vector2.UnitY);

                    ball.velocity = launchDir * 15f;
                    ball.ai[1] = 1f; // state 1 -> meluncur & bisa meledak (skip charging)
                    ball.netUpdate = true;
                }
            }

            SoundEngine.PlaySound(SoundID.Item29, NPC.Center);

            NPC.ai[0] = 4f;
            NPC.ai[1] = 4f; // Stage Chase Dash
            NPC.ai[2] = 0f;
            NPC.ai[3] = 0f; // belum lagi dash burst (0 = nunggu giliran dash pertama)
        }

        // ======================================================================
        // PATTERN 5 - ARENA BOMB: langsung ke Stage Orbit (skip Approach/Roar/Summon). Border
        // di-spawn di posisi Pluto SAAT INI (sama kayak ExecuteArenaSummonStage timer==0), lalu
        // Pluto langsung ditaruh di garis border (radius penuh) & mulai muter+muntah bomb.
        // ======================================================================
        private void ForceArenaBombPattern(Player player) {
            Vector2 arenaCenter = NPC.Center;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    arenaCenter,
                    Vector2.Zero,
                    ModContent.ProjectileType<PlutoArenaBorder>(),
                    0,
                    0f,
                    Main.myPlayer,
                    NPC.whoAmI
                );
            }

            ScreenShakeSystem.StartShake(16f, 30, Vector2.Zero);
            SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{Main.rand.Next(1, 3)}"), NPC.Center);
            ApplyArenaPullBurst(arenaCenter, ArenaRadius);

            Vector2 outDir = (player.Center - arenaCenter).SafeNormalize(Vector2.UnitX);
            NPC.rotation = outDir.ToRotation();
            NPC.Center = arenaCenter + outDir * ArenaRadius;

            NPC.ai[0] = 5f;
            NPC.ai[1] = 3f; // Stage Orbit
            NPC.ai[2] = 0f;
            NPC.ai[3] = 0f;
        }

        // ======================================================================
        // PATTERN 6 - PROBE SWARM: reset timer giliran (SAMA kayak gacha normal), TAPI langsung
        // nombokin SEMUA probe yang kurang buat tiap player SEKARANG JUGA (skip 1 detik delay
        // per player) -- biar swarm-nya langsung kerasa penuh pas Remote dipencet.
        // ======================================================================
        private void ForceProbeSwarmPattern(Player player) {
            maxDashes = 999;
            probeSwarmPatternTimer = 0;
            for (int i = 0; i < probeMissingDelayTimer.Length; i++) probeMissingDelayTimer[i] = 0;

            NPC.ai[0] = 6f;
            NPC.ai[1] = 0f; // Stage Aim (ExecuteDashPattern versi NormalDash, cuma 0,5 detik)
            NPC.ai[2] = 0f;
            NPC.ai[3] = 0f;

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                for (int p = 0; p < Main.maxPlayers; p++) {
                    Player target = Main.player[p];
                    if (!target.active || target.dead) continue;

                    int alive = CountAliveProbesForPlayer(p);
                    int missing = ProbeCountPerPlayer - alive;
                    if (missing > 0) SpawnProbesForPlayer(target, missing);
                }
            }
        }

        // ======================================================================
        // PATTERN 7 - CRYSTAL DIVE (LASER WALL): langsung ke Stage Dash (skip 0,5 detik Aim) --
        // ini yang muntahin sepasang RedCrystal (nembak RedBeam) tiap interval sepanjang jalur
        // dash-nya (lihat CrystalSpawnIntervalTicks & radius per-segmen di CrystalDivePattern.cs).
        // Paling relevan buat "gampang nyari pattern laser" sesuai tujuan awal.
        // ======================================================================
        private void ForceCrystalDivePattern(Player player) {
            maxDashes = Main.rand.Next(4, 9);
            crystalSpawnDelayTimer = 0;

            Vector2 sideOffsetDir;
            int sideRoll = Main.rand.Next(8);
            switch (sideRoll) {
                case 0: sideOffsetDir = new Vector2(1f, 0f); break;
                case 1: sideOffsetDir = new Vector2(-1f, 0f); break;
                case 2: sideOffsetDir = new Vector2(0f, -1f); break;
                case 3: sideOffsetDir = new Vector2(0f, 1f); break;
                case 4: sideOffsetDir = Vector2.Normalize(new Vector2(1f, -1f)); break;
                case 5: sideOffsetDir = Vector2.Normalize(new Vector2(-1f, -1f)); break;
                case 6: sideOffsetDir = Vector2.Normalize(new Vector2(1f, 1f)); break;
                default: sideOffsetDir = Vector2.Normalize(new Vector2(-1f, 1f)); break;
            }

            Vector2 diveTargetPos = player.Center + sideOffsetDir * (100f * 16f);
            Vector2 dashDir = (diveTargetPos - NPC.Center).SafeNormalize(Vector2.UnitX);
            float distanceToTarget = Vector2.Distance(NPC.Center, diveTargetPos);
            float totalDashDistance = distanceToTarget + 3200f;
            float dashSpeed = 60f;

            NPC.velocity = dashDir * dashSpeed;
            NPC.rotation = dashDir.ToRotation();
            dashDuration = totalDashDistance / dashSpeed;
            if (dashDuration > 150f) dashDuration = 150f;

            SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{Main.rand.Next(1, 3)}"), NPC.Center);

            NPC.ai[0] = 7f;
            NPC.ai[1] = 1f; // Stage Dash -- ini yang muntahin RedCrystal/RedBeam
            NPC.ai[2] = 0f;
            NPC.ai[3] = 1f;
        }
    }
}
