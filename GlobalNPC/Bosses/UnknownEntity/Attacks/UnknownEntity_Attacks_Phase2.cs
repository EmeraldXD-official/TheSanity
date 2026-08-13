using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    public partial class UnknownEntity
    {
        private Vector2 blenderHoverPos;
        // ==================== PHASE 2 TRANSITION (TAMBAHAN LOGIKA BG) ====================
        private void ExecutePhase2Transition(Player target)
        {
            NPC.velocity *= 0.9f;

            if (StateTimer == 1)
            {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.2f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 8f);

                // --- [TAMBAHAN] Ganti BG Fase 1 ke Fase 2 ---
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // Matikan BG Fase 1
                    if (_spaceBackgroundID != -1 && _spaceBackgroundID < Main.maxProjectiles && Main.projectile[_spaceBackgroundID].active)
                        Main.projectile[_spaceBackgroundID].Kill();

                    // Spawn BG Fase 2 (ai[0] = 1)
                    _spaceBackgroundID = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CosmicSpaceBackground>(), 0, 0f, Main.myPlayer, 1f);
                }
            }

            for (int i = 0; i < 4; i++)
            {
                Vector2 spawnPos = NPC.Center + new Vector2(Main.rand.NextFloat(-100f, 100f), 120f);
                Dust d = Dust.NewDustPerfect(spawnPos, DustID.PurpleTorch, -Vector2.UnitY * Main.rand.NextFloat(3f, 8f), 0, default, 2f);
                d.noGravity = true;
            }

            if (StateTimer >= 100)
            {
                StateTimer = 0;
                SubTimer = 0;
                State = AIState.SerpentPortalDash;
            }
        }

        // ==================== SERPENT PORTAL DASH ====================
        private void ExecuteSerpentPortalDash(Player target)
        {
            int windupDuration = 35;
            int dashDuration = 28;
            int totalDuration = windupDuration + dashDuration + 10;

            if (StateTimer == 1)
            {
                startPos = target.Center + Main.rand.NextVector2CircularEdge(650f, 650f);
                portalExitPos = target.Center - (startPos - target.Center).SafeNormalize(Vector2.Zero) * 650f;

                NPC.Center = startPos;
                dashTargetDir = (portalExitPos - startPos).SafeNormalize(Vector2.UnitX);
                NPC.rotation = dashTargetDir.ToRotation();
                NPC.velocity = Vector2.Zero;

                SoundEngine.PlaySound(SoundID.Item8, startPos);
                SoundEngine.PlaySound(SoundID.Item8, portalExitPos);
            }

            if (StateTimer < windupDuration)
            {
                NPC.velocity *= 0.8f;

                float progress = StateTimer / (float)windupDuration;
                float elasticFactor = EaseInElastic(progress);

                Vector2 pullBackPos = startPos - (dashTargetDir * 180f);
                NPC.Center = Vector2.Lerp(startPos, pullBackPos, elasticFactor);
                NPC.rotation = dashTargetDir.ToRotation();

                if (StateTimer == 10 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int rayCount = 6;
                    float angleStep = MathHelper.ToRadians(60f);

                    for (int i = 0; i < rayCount; i++)
                    {
                        float laserAngle = dashTargetDir.ToRotation() + (i * angleStep);
                        Vector2 laserVel = laserAngle.ToRotationVector2();

                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            startPos,
                            laserVel,
                            ModContent.ProjectileType<UnknownEntityDeathRay>(),
                            NPC.damage / 4,
                            0f,
                            Main.myPlayer,
                            0f,
                            0f
                        );
                    }
                }

                for (int i = 0; i < 3; i++)
                {
                    Dust d1 = Dust.NewDustPerfect(startPos + Main.rand.NextVector2Circular(90f, 90f), DustID.BlueTorch, null, 0, default, 1.8f);
                    d1.noGravity = true;

                    Dust d2 = Dust.NewDustPerfect(portalExitPos + Main.rand.NextVector2Circular(90f, 90f), DustID.PurpleTorch, null, 0, default, 1.8f);
                    d2.noGravity = true;
                }
            }
            else if (StateTimer == windupDuration)
            {
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.4f, Volume = 1.3f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.2f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(target.Center, 5f);

                NPC.velocity = dashTargetDir * 52f;
            }
            else if (StateTimer > windupDuration && StateTimer <= windupDuration + dashDuration)
            {
                NPC.rotation = NPC.velocity.ToRotation();

                Dust d = Dust.NewDustPerfect(NPC.Center, DustID.Electric, -NPC.velocity * 0.2f, 0, Color.Cyan, 1.6f);
                d.noGravity = true;
            }
            else if (StateTimer >= totalDuration)
            {
                SubTimer++;

                if (SubTimer < 5)
                {
                    StateTimer = 0;
                }
                else
                {
                    StateTimer = 0;
                    SubTimer = 0;

                    int nextState = Main.rand.Next(2);
                    State = nextState == 0 ? AIState.SummonMinionHorde : AIState.DeathLaserBlender;
                }
            }
        }

        // ==================== SUMMON MINION HORDE ====================
        private void ExecuteSummonMinionHorde(Player target)
        {
            int windupDuration = 55; 
            int dashDuration = 45;
            int recoveryDuration = 25;
            int totalWaveDuration = windupDuration + dashDuration + recoveryDuration;
            int totalDuration = totalWaveDuration * 2; // 2 gelombang

            // Tentukan pola: X (Diagonal) atau + (Kardinal)
            if (StateTimer == 1)
            {
                SubTimer = 0; // 0 = gelombang 1, 1 = gelombang 2
                bool useXPattern = Main.rand.NextBool();
                dashTargetDir = useXPattern 
                    ? new Vector2(1f, 1f).SafeNormalize(Vector2.UnitX) 
                    : new Vector2(1f, 0f);

                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 1.1f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 8f);

                for (int i = 0; i < 45; i++)
                {
                    float angle = MathHelper.TwoPi * i / 45f;
                    Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 14f);
                    Dust d = Dust.NewDustPerfect(NPC.Center, DustID.RainbowTorch, vel, 0, Color.Lerp(Color.Cyan, Color.Magenta, i / 45f), 2.2f);
                    d.noGravity = true;
                }
            }

            Vector2 hoverTarget = target.Center + new Vector2(0f, -280f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverTarget - NPC.Center) * 0.06f, 0.1f);
            NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.08f);
            NPC.alpha = (int)MathHelper.Lerp(NPC.alpha, 80, 0.02f);

            int currentWave = (int)SubTimer;
            int waveStartTick = currentWave * totalWaveDuration;
            int localTimer = (int)(StateTimer - waveStartTick);

            if (currentWave < 2 && localTimer < totalWaveDuration)
            {
                if (localTimer < windupDuration)
                {
                    float progress = localTimer / (float)windupDuration;
                    Vector2[] directions = (dashTargetDir.X == 1f && dashTargetDir.Y == 1f) 
                        ? new Vector2[] { new Vector2(1f, 1f).SafeNormalize(Vector2.UnitX), new Vector2(-1f, 1f).SafeNormalize(Vector2.UnitX), new Vector2(-1f, -1f).SafeNormalize(Vector2.UnitX), new Vector2(1f, -1f).SafeNormalize(Vector2.UnitX) }
                        : new Vector2[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY };

                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 dir = directions[i];
                        Vector2 portalPos = target.Center + dir * 550f + Main.rand.NextVector2Circular(15f, 15f);

                        for (int j = 0; j < 3; j++)
                        {
                            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                            Vector2 dustVel = angle.ToRotationVector2() * Main.rand.NextFloat(1f, 4f);
                            Color portalColor = i % 2 == 0 ? Color.Cyan : Color.Magenta;
                            Dust d = Dust.NewDustPerfect(portalPos, DustID.Electric, dustVel, 0, portalColor, 1.6f * progress);
                            d.noGravity = true;
                        }

                        if (Main.rand.NextBool(3))
                        {
                            Vector2 linePos = Vector2.Lerp(portalPos, target.Center, Main.rand.NextFloat(0.2f, 0.8f));
                            Dust warn = Dust.NewDustPerfect(linePos + Main.rand.NextVector2Circular(8f, 8f), DustID.WhiteTorch, Vector2.Zero, 100, Color.Red, 0.6f * progress);
                            warn.noGravity = true;
                        }
                    }

                    if (Main.rand.NextBool(2))
                    {
                        Dust charge = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(30f, 30f), DustID.Shadowflame, Main.rand.NextVector2Circular(2f, 2f), 0, Color.Magenta, 2f * progress);
                        charge.noGravity = true;
                    }
                }
                else if (localTimer == windupDuration)
                {
                    SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1.4f, Pitch = 0.1f }, target.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.4f, Volume = 1.1f }, target.Center);
                    ScreenShakeSystem.StartShakeAtPoint(target.Center, 10f);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2[] dirs = (dashTargetDir.X == 1f && dashTargetDir.Y == 1f) 
                            ? new Vector2[] { new Vector2(1f, 1f).SafeNormalize(Vector2.UnitX), new Vector2(-1f, 1f).SafeNormalize(Vector2.UnitX), new Vector2(-1f, -1f).SafeNormalize(Vector2.UnitX), new Vector2(1f, -1f).SafeNormalize(Vector2.UnitX) }
                            : new Vector2[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY };

                        foreach (Vector2 dir in dirs)
                        {
                            for (int s = 0; s < 2 + (currentWave == 1 ? 1 : 0); s++)
                            {
                                Vector2 spawnPos = target.Center + dir * (550f + (s * 20f)) + Main.rand.NextVector2Circular(15f, 15f);
                                int serpentIdx = NPC.NewNPC(NPC.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, ModContent.NPCType<DimensionalSerpentHead>());
                                if (serpentIdx < Main.maxNPCs)
                                {
                                    Vector2 moveDir = (target.Center - spawnPos).SafeNormalize(Vector2.UnitX);
                                    float speed = (currentWave == 0) ? 22f : 28f; 
                                    
                                    Main.npc[serpentIdx].velocity = moveDir * speed;
                                    Main.npc[serpentIdx].ai[0] = 1f;
                                    Main.npc[serpentIdx].ai[2] = currentWave;
                                    Main.npc[serpentIdx].netUpdate = true;
                                }
                            }
                        }
                    }

                    Vector2[] portalDirs = (dashTargetDir.X == 1f && dashTargetDir.Y == 1f) 
                        ? new Vector2[] { new Vector2(1f, 1f).SafeNormalize(Vector2.UnitX), new Vector2(-1f, 1f).SafeNormalize(Vector2.UnitX), new Vector2(-1f, -1f).SafeNormalize(Vector2.UnitX), new Vector2(1f, -1f).SafeNormalize(Vector2.UnitX) }
                        : new Vector2[] { Vector2.UnitX, -Vector2.UnitX, Vector2.UnitY, -Vector2.UnitY };

                    foreach (Vector2 dir in portalDirs)
                    {
                        Vector2 portalPos = target.Center + dir * 550f;
                        for (int i = 0; i < 20; i++)
                        {
                            float angle = MathHelper.TwoPi * i / 20f;
                            Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(5f, 18f);
                            Dust d = Dust.NewDustPerfect(portalPos, DustID.RainbowTorch, vel, 0, Color.Lerp(Color.Cyan, Color.Magenta, i / 20f), 2.0f);
                            d.noGravity = true;
                        }
                    }
                }
                else if (localTimer > windupDuration + dashDuration)
                {
                    if (localTimer == windupDuration + dashDuration + 1)
                    {
                        for (int i = 0; i < Main.maxNPCs; i++)
                        {
                            NPC n = Main.npc[i];
                            if (n.active && n.type == ModContent.NPCType<DimensionalSerpentHead>() && n.ai[2] == currentWave)
                            {
                                if (Main.netMode != NetmodeID.MultiplayerClient)
                                {
                                    int shrapnelCount = (currentWave == 0) ? 4 : 6;
                                    
                                    for (int j = 0; j < shrapnelCount; j++)
                                    {
                                        Vector2 dirToPlayer = (target.Center - n.Center).SafeNormalize(Vector2.UnitX);
                                        float spread = (j - (shrapnelCount / 2f)) * 0.2f;
                                        Vector2 vel = dirToPlayer.RotatedBy(spread) * Main.rand.NextFloat(10f, 16f);

                                        Projectile.NewProjectile(
                                            NPC.GetSource_FromAI(), 
                                            n.Center, 
                                            vel, 
                                            ModContent.ProjectileType<UnknownEntityBolt>(), 
                                            NPC.damage / 4, 
                                            0f, 
                                            Main.myPlayer, 
                                            0f, 
                                            Main.rand.Next(0, 2)
                                        );
                                    }
                                }

                                for (int k = 0; k < 16; k++)
                                {
                                    float a = MathHelper.TwoPi * k / 16f + Main.rand.NextFloat(-0.2f, 0.2f);
                                    Vector2 dustVel = a.ToRotationVector2() * Main.rand.NextFloat(3f, 12f);
                                    Dust d = Dust.NewDustPerfect(n.Center, DustID.RainbowTorch, dustVel, 0, Color.HotPink, 1.8f);
                                    d.noGravity = true;
                                }

                                n.life = 0;
                                n.active = false;
                                n.netUpdate = true;
                            }
                        }
                    }

                    NPC.alpha = (int)MathHelper.Lerp(80, 0, (localTimer - (windupDuration + dashDuration)) / (float)recoveryDuration);
                }
            }
            else
            {
                if (currentWave == 0)
                {
                    SubTimer = 1; 
                    StateTimer = totalWaveDuration; 
                }
                else
                {
                    StateTimer = 0;
                    SubTimer = 0;
                    State = AIState.Chase;
                    NPC.alpha = 0;

                    SoundEngine.PlaySound(SoundID.Item100 with { Volume = 0.8f }, NPC.Center);
                }
            }
        }

        // ==================== DEATH LASER BLENDER ====================
        // Tambahkan variabel ini di dalam kelas ModNPC Anda (di luar method AI):
        // private Vector2 blenderHoverPos;

        private void ExecuteDeathLaserBlender(Player target)
        {
            int windupTime = 40;
            int telegraphTime = 35;
            int laserTime = 202;
            int attackDuration = windupTime + telegraphTime + laserTime;

            // Posisi target arena (mengunci player agar berada di dalam radius arena, misal 800 pixel dari pusat boss/target)
            float arenaRadius = 800f;
            Vector2 arenaCenter = target.Center; // Bisa disesuaikan dengan titik tengah arena jika ada

            // Batasi posisi player agar tidak keluar dari arena selama serangan blender berlangsung
            float distToPlayer = Vector2.Distance(target.Center, arenaCenter);
            if (distToPlayer > arenaRadius)
            {
                Vector2 limitedDir = (target.Center - arenaCenter).SafeNormalize(Vector2.UnitX);
                target.Center = arenaCenter + limitedDir * arenaRadius;
            }

            if (StateTimer == 1)
            {
                NPC.velocity = Vector2.Zero;
                SoundEngine.PlaySound(new SoundStyle("TheSanity/SFX/BlenderSfx") with { Volume = 2f }, NPC.Center);

                // Kunci posisi tepat di atas kepala player HANYA SEKALI saat serangan dimulai
                blenderHoverPos = target.Center + new Vector2(0f, -320f);
            }

            // BOSS BERGESER KE POSISI STATIS (Tidak lagi mengikuti player secara real-time)
            NPC.velocity = Vector2.Lerp(NPC.velocity, (blenderHoverPos - NPC.Center) * 0.1f, 0.15f);
            NPC.rotation = 0f;

            if (StateTimer < windupTime)
            {
                if (StateTimer % 8 == 0)
                {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 0.8f }, NPC.Center);
                }

                for (int i = 0; i < 4; i++)
                {
                    Vector2 spawnDustPos = NPC.Center + Main.rand.NextVector2CircularEdge(200f, 200f);
                    Vector2 dustVel = (NPC.Center - spawnDustPos) * 0.12f;
                    Dust d = Dust.NewDustPerfect(spawnDustPos, DustID.BlueTorch, dustVel, 0, default, 1.6f);
                    d.noGravity = true;
                }
            }
            else if (StateTimer == windupTime)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Vector2 initialDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    deathRaySpinDir = Main.rand.NextBool() ? 1f : -1f;

                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        NPC.Center,
                        initialDir,
                        ModContent.ProjectileType<UnknownEntityDeathRay>(),
                        NPC.damage / 2,
                        0f,
                        Main.myPlayer,
                        0f,
                        1f,
                        deathRaySpinDir
                    );
                }
            }

            int laserActiveTimer = (int)(StateTimer - (windupTime + telegraphTime));

            if (laserActiveTimer >= 0 && laserActiveTimer % 60 == 0 && StateTimer < attackDuration)
            {
                SoundEngine.PlaySound(SoundID.Item12 with { Pitch = -0.2f, Volume = 1.2f }, NPC.Center);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int count = 10;
                    float angleStep = MathHelper.ToRadians(36f);
                    float startAngle = Main.rand.NextFloat(MathHelper.TwoPi);

                    for (int i = 0; i < count; i++)
                    {
                        float currentAngle = startAngle + (i * angleStep);
                        Vector2 shootVel = currentAngle.ToRotationVector2() * 5.5f;

                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            shootVel,
                            ModContent.ProjectileType<UnknownEntityBolt>(),
                            NPC.damage / 2,
                            0f,
                            Main.myPlayer,
                            0f,
                            3f
                        );
                    }
                }
            }

            if (StateTimer >= attackDuration)
            {
                StateTimer = 0;
                SubTimer = 0;
                State = AIState.DimensionalMatrix;
            }
        }

        // ==================== DIMENSIONAL MATRIX ====================
        private void ExecuteDimensionalMatrix(Player target)
        {
            int windup = 25;
            int telegraphDuration = 75;
            int totalDuration = windup + telegraphDuration + 30;

            NPC.velocity *= 0.92f;
            NPC.rotation = 0f;

            if (StateTimer == 1)
            {
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = 0.3f, Volume = 1.1f }, NPC.Center);
            }

            if ((int)StateTimer == windup && Main.netMode != NetmodeID.MultiplayerClient)
            {
                Vector2 centerPos = target.Center;

                int lineSpacing = 110;
                int lineCountEachSide = 9;
                int totalLines = lineCountEachSide * 2 + 1;

                for (int i = -lineCountEachSide; i <= lineCountEachSide; i++)
                {
                    Vector2 linePos = centerPos + new Vector2(0f, i * lineSpacing);
                    float isMagenta = (Math.Abs(i) % 2 == 0) ? 1f : 0f;

                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        linePos,
                        Vector2.UnitX,
                        ModContent.ProjectileType<DimensionalGridLine>(),
                        NPC.damage / 4,
                        0f,
                        Main.myPlayer,
                        0f,
                        isMagenta
                    );
                }

                for (int i = -lineCountEachSide; i <= lineCountEachSide; i++)
                {
                    Vector2 linePos = centerPos + new Vector2(i * lineSpacing, 0f);
                    float isMagenta = (Math.Abs(i) % 2 != 0) ? 1f : 0f;

                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        linePos,
                        Vector2.UnitY,
                        ModContent.ProjectileType<DimensionalGridLine>(),
                        NPC.damage / 4,
                        0f,
                        Main.myPlayer,
                        0f,
                        isMagenta
                    );
                }

                float arenaRadius = 900f;
                for (int i = 0; i < 4; i++)
                {
                    float angle = MathHelper.PiOver2 * i + MathHelper.PiOver4;
                    Vector2 wallPos = centerPos + angle.ToRotationVector2() * arenaRadius;
                    for (int j = 0; j < 8; j++)
                    {
                        Dust d = Dust.NewDustPerfect(wallPos + Main.rand.NextVector2Circular(20f, 20f), DustID.PurpleTorch, Vector2.Zero, 200, Color.Magenta, 0.4f);
                        d.noGravity = true;
                    }
                }
            }

            if (StateTimer >= totalDuration)
            {
                StateTimer = 0;
                SubTimer = 0;
                State = AIState.DimensionalShatter;
            }
        }

        // ==================== DIMENSIONAL SHATTER ====================
        private void ExecuteDimensionalShatter(Player target)
        {
            int windupDuration = 45;
            int shardCount = 12;
            int fireInterval = 6;
            int fireDuration = shardCount * fireInterval;
            int totalDuration = windupDuration + fireDuration + 20;

            Vector2 hoverTarget = target.Center + new Vector2(0f, -270f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverTarget - NPC.Center) * 0.08f, 0.12f);
            NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.1f);

            float radius = 420f;
            Vector2 centerPoint = target.Center;

            if (StateTimer < windupDuration)
            {
                if (StateTimer == 1)
                {
                    SoundEngine.PlaySound(SoundID.Shatter with { Pitch = 0.2f, Volume = 1.1f }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.2f, Volume = 0.9f }, NPC.Center);
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 0f);
                }

                float progress = StateTimer / (float)windupDuration;

                for (int i = 0; i < shardCount; i++)
                {
                    float angle = (MathHelper.TwoPi / shardCount) * i;
                    Vector2 shardPos = centerPoint + angle.ToRotationVector2() * radius;

                    Color shardColor = (i % 2 == 0) ? Color.Cyan : Color.Magenta;
                    Dust d = Dust.NewDustPerfect(shardPos + Main.rand.NextVector2Circular(8f, 8f), DustID.Electric, Vector2.Zero, 0, shardColor, 1.2f * progress);
                    d.noGravity = true;

                    Vector2 lineDir = (target.Center - shardPos).SafeNormalize(Vector2.UnitX);
                    for (float dist = 0; dist < radius; dist += 50f)
                    {
                        Vector2 dustPos = shardPos + lineDir * dist;
                        Dust lineDust = Dust.NewDustPerfect(dustPos, DustID.BlueTorch, Vector2.Zero, 0, default, 0.6f * progress);
                        lineDust.noGravity = true;
                    }
                }
            }
            else if (StateTimer >= windupDuration && StateTimer < windupDuration + fireDuration)
            {
                int fireTimer = (int)(StateTimer - windupDuration);

                if (fireTimer % fireInterval == 0)
                {
                    int currentShardIndex = fireTimer / fireInterval;
                    float angle = (MathHelper.TwoPi / shardCount) * currentShardIndex;
                    Vector2 shardPos = centerPoint + angle.ToRotationVector2() * radius;

                    SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.3f, Volume = 0.8f }, shardPos);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 shootVel = (target.Center - shardPos).SafeNormalize(Vector2.UnitX) * 11f;

                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            shardPos,
                            shootVel,
                            ModContent.ProjectileType<UnknownEntityBolt>(),
                            NPC.damage / 4,
                            0f,
                            Main.myPlayer,
                            0f,
                            3f
                        );
                    }

                    for (int k = 0; k < 12; k++)
                    {
                        Dust.NewDustPerfect(shardPos, DustID.PurpleTorch, Main.rand.NextVector2Circular(6f, 6f), 0, default, 1.6f).noGravity = true;
                    }
                }
            }

            if (StateTimer >= totalDuration)
            {
                StateTimer = 0;
                SubTimer = 0;
                State = AIState.SingularityCollapse;
            }
        }

        // ==================== SINGULARITY COLLAPSE ====================
        private void ExecuteSingularityCollapse(Player target)
        {
            int windupDuration = 40;
            int buildupDuration = 70;
            int explosionDuration = 20;
            int rainDuration = 80;
            int totalDuration = windupDuration + buildupDuration + explosionDuration + rainDuration + 20;

            Vector2 portalCenter = target.Center;

            Vector2 hoverTarget = target.Center + new Vector2(0f, -320f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverTarget - NPC.Center) * 0.08f, 0.12f);
            NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.1f);

            int localTimer = (int)StateTimer;

            if (localTimer < windupDuration)
            {
                if (localTimer == 1)
                {
                    SoundEngine.PlaySound(SoundID.Item84 with { Pitch = -0.4f, Volume = 1.2f }, portalCenter);
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.2f }, portalCenter);
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 0f);
                }

                float progress = localTimer / (float)windupDuration;

                for (int i = 0; i < 5; i++)
                {
                    Vector2 spawnPos = portalCenter + Main.rand.NextVector2CircularEdge(220f * (1f - progress * 0.4f), 220f * (1f - progress * 0.4f));
                    Vector2 dustVel = (portalCenter - spawnPos) * 0.1f;

                    Dust d1 = Dust.NewDustPerfect(spawnPos, DustID.PurpleTorch, dustVel, 0, default, 1.5f);
                    d1.noGravity = true;

                    Dust d2 = Dust.NewDustPerfect(spawnPos, DustID.BlueTorch, dustVel, 0, default, 1.5f);
                    d2.noGravity = true;
                }
            }
            else if (localTimer < windupDuration + buildupDuration)
            {
                int activeTimer = localTimer - windupDuration;
                float progress = activeTimer / (float)buildupDuration;
                float speedFactor = EaseInExpo(progress);

                Vector2 pullDir = (portalCenter - target.Center).SafeNormalize(Vector2.Zero);
                float distToPortal = Vector2.Distance(portalCenter, target.Center);
                if (distToPortal > 30f)
                {
                    float pullStrength = MathHelper.Lerp(0.08f, 0.45f, speedFactor);
                    target.velocity += pullDir * pullStrength;
                }

                int spiralCount = 4 + (int)(speedFactor * 6);
                float baseAngle = activeTimer * (0.02f + speedFactor * 0.06f);

                for (int i = 0; i < spiralCount; i++)
                {
                    float angle = baseAngle + (MathHelper.TwoPi / spiralCount) * i;
                    float radius = MathHelper.Lerp(350f, 60f, speedFactor);
                    Vector2 spiralPos = portalCenter + angle.ToRotationVector2() * radius;

                    Color spiralColor = Color.Lerp(Color.Cyan, Color.Magenta, (i / (float)spiralCount + progress) % 1f);
                    Dust d = Dust.NewDustPerfect(spiralPos, DustID.RainbowTorch, Vector2.Zero, 0, spiralColor, 1.2f + speedFactor * 0.8f);
                    d.noGravity = true;

                    if (Main.rand.NextBool(3))
                    {
                        Vector2 trailPos = portalCenter + (angle - 0.2f).ToRotationVector2() * radius;
                        Dust trail = Dust.NewDustPerfect(trailPos, DustID.Electric, Vector2.Zero, 50, spiralColor, 0.6f);
                        trail.noGravity = true;
                    }
                }

                if (Main.rand.NextBool(2))
                {
                    float pulse = 1f + (float)Math.Sin(activeTimer * 0.3f) * 0.3f;
                    Dust core = Dust.NewDustPerfect(portalCenter + Main.rand.NextVector2Circular(15f, 15f), DustID.WhiteTorch, Vector2.Zero, 0, Color.White, 1.2f * pulse);
                    core.noGravity = true;
                }

                if (progress > 0.6f && Main.rand.NextBool(2))
                {
                    ScreenShakeSystem.StartShakeAtPoint(portalCenter, progress * 2f);
                }

                if (activeTimer == buildupDuration - 1)
                {
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.3f, Volume = 1.5f }, portalCenter);
                    ScreenShakeSystem.StartShakeAtPoint(portalCenter, 14f);
                }
            }
            else if (localTimer < windupDuration + buildupDuration + explosionDuration)
            {
                int expTimer = localTimer - (windupDuration + buildupDuration);
                float progress = expTimer / (float)explosionDuration;

                if (expTimer == 0)
                {
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.8f, Pitch = -0.4f }, portalCenter);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.2f, Volume = 1.4f }, portalCenter);
                    ScreenShakeSystem.StartShakeAtPoint(portalCenter, 18f);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int burstCount = 32;
                        for (int i = 0; i < burstCount; i++)
                        {
                            float angle = MathHelper.TwoPi * i / burstCount + Main.rand.NextFloat(-0.05f, 0.05f);
                            float speed = Main.rand.NextFloat(8f, 20f);
                            Vector2 vel = angle.ToRotationVector2() * speed;

                            int colorMode = Main.rand.Next(0, 3);
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                portalCenter,
                                vel,
                                ModContent.ProjectileType<UnknownEntityBolt>(),
                                NPC.damage / 3,
                                0f,
                                Main.myPlayer,
                                0f,
                                colorMode
                            );
                        }

                        for (int i = 0; i < 6; i++)
                        {
                            Vector2 dirToPlayer = (target.Center - portalCenter).SafeNormalize(Vector2.UnitX);
                            float spread = (i - 2.5f) * 0.25f;
                            Vector2 vel = dirToPlayer.RotatedBy(spread) * 14f;
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                portalCenter,
                                vel,
                                ModContent.ProjectileType<UnknownEntityBolt>(),
                                NPC.damage / 3,
                                0f,
                                Main.myPlayer,
                                0f,
                                2
                            );
                        }
                    }

                    for (int i = 0; i < 80; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 80f;
                        Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 28f);
                        Color burstColor = Main.hslToRgb((i / 80f + Main.GlobalTimeWrappedHourly) % 1f, 1f, 0.8f);
                        Dust d = Dust.NewDustPerfect(portalCenter, DustID.RainbowTorch, vel, 0, burstColor, 2.5f);
                        d.noGravity = true;
                    }
                }

                if (Main.rand.NextBool(3))
                {
                    Vector2 sparkPos = portalCenter + Main.rand.NextVector2Circular(100f * (1f - progress), 100f * (1f - progress));
                    Dust d = Dust.NewDustPerfect(sparkPos, DustID.Electric, Main.rand.NextVector2Circular(4f, 4f), 0, Color.Cyan, 1.2f * (1f - progress));
                    d.noGravity = true;
                }
            }
            else if (localTimer < windupDuration + buildupDuration + explosionDuration + rainDuration)
            {
                int rainTimer = localTimer - (windupDuration + buildupDuration + explosionDuration);
                float progress = rainTimer / (float)rainDuration;

                if (rainTimer % Math.Max(3, (int)(12 - progress * 8)) == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int dropsPerTick = 1 + (int)((1f - progress) * 3f);
                    for (int i = 0; i < dropsPerTick; i++)
                    {
                        float spawnX = portalCenter.X + Main.rand.NextFloat(-700f, 700f);
                        Vector2 spawnPos = new Vector2(spawnX, portalCenter.Y - 800f - Main.rand.NextFloat(0f, 200f));
                        Vector2 vel = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(10f, 18f));
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            spawnPos,
                            vel,
                            ModContent.ProjectileType<UnknownEntityBolt>(),
                            NPC.damage / 4,
                            0f,
                            Main.myPlayer,
                            0f,
                            Main.rand.Next(0, 2)
                        );
                    }
                }

                if (Main.rand.NextBool(3))
                {
                    Vector2 rainPos = portalCenter + new Vector2(Main.rand.NextFloat(-600f, 600f), -600f + Main.rand.NextFloat(0f, 400f));
                    Dust d = Dust.NewDustPerfect(rainPos, DustID.Electric, Vector2.UnitY * Main.rand.NextFloat(2f, 6f), 50, Color.Cyan * (1f - progress), 0.6f);
                    d.noGravity = true;
                }
            }
            else
            {
                NPC.velocity *= 0.9f;

                if (localTimer >= totalDuration)
                {
                    StateTimer = 0;
                    SubTimer = 0;
                    State = AIState.Chase;
                }
            }
        }

        // ==================== MEMORY FRACTURE ====================
        private void ExecuteMemoryFracture(Player target)
        {
            int totalMemories = 8;
            int spawnInterval = 10;
            int spawnPhaseDuration = totalMemories * spawnInterval;
            int recoveryDuration = 35;
            int totalDuration = spawnPhaseDuration + recoveryDuration;

            float[] memoryAngles = { 0f, MathHelper.Pi / 3f, MathHelper.PiOver2, 2f * (MathHelper.Pi / 3f), -MathHelper.PiOver4, -(MathHelper.Pi / 3f), MathHelper.Pi * 0.85f, -MathHelper.PiOver2 - 0.3f };

            if (StateTimer == 1)
            {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.8f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = 0.3f, Volume = 0.4f }, NPC.Center);
                SubTimer = 0;
            }

            Vector2 hoverTarget = target.Center + new Vector2(0f, -260f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverTarget - NPC.Center) * 0.07f, 0.1f);
            NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.1f);

            if (StateTimer <= spawnPhaseDuration && StateTimer % spawnInterval == 1 && SubTimer < totalMemories)
            {
                int memoryIndex = (int)SubTimer;

                Vector2 memoryPos = target.Center + target.velocity * 30f;
                float lineRot = memoryAngles[memoryIndex % memoryAngles.Length];
                float isMagenta = (memoryIndex % 2 == 0) ? 1f : 0f;

                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.8f + memoryIndex * 0.1f, Volume = 0.4f }, memoryPos);

                for (int i = 0; i < 14; i++)
                {
                    Vector2 trailPos = Vector2.Lerp(NPC.Center, memoryPos, i / 13f);
                    Color trailColor = Color.Lerp(Color.Cyan, Color.Magenta, isMagenta);
                    Dust d = Dust.NewDustPerfect(trailPos, DustID.Electric, Vector2.Zero, 150, trailColor, 0.6f);
                    d.noGravity = true;
                }

                for (int i = 0; i < 10; i++)
                {
                    Vector2 markVel = Main.rand.NextVector2Circular(2f, 2f);
                    Dust d = Dust.NewDustPerfect(memoryPos, DustID.PurpleTorch, markVel, 120, default, 0.9f);
                    d.noGravity = true;
                }

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(),
                        memoryPos,
                        lineRot.ToRotationVector2(),
                        ModContent.ProjectileType<DimensionalGridLine>(),
                        NPC.damage / 3,
                        0f,
                        Main.myPlayer,
                        0f,
                        isMagenta,
                        1f
                    );

                    if (memoryIndex >= 1)
                    {
                        Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            memoryPos,
                            (lineRot + MathHelper.PiOver2).ToRotationVector2(),
                            ModContent.ProjectileType<DimensionalGridLine>(),
                            NPC.damage / 3,
                            0f,
                            Main.myPlayer,
                            0f,
                            1f - isMagenta,
                            1f
                        );
                    }
                }

                SubTimer++;
            }

            if (StateTimer >= totalDuration)
            {
                StateTimer = 0;
                SubTimer = 0;
                State = AIState.Chase;
            }
        }

        // ==================== SHATTERED REFLECTION ====================
        private void ExecuteShatteredReflection(Player target)
        {
            int captureTick = 15;
            int primaryWindup = 45;
            int primaryDash = 42;
            int echoTick = 96;
            int echoWindup = 14;
            int echoDash = 42;
            int recoveryDuration = 30;
            int totalDuration = echoTick + echoWindup + echoDash + recoveryDuration;

            if (StateTimer == 1)
            {
                NPC.velocity = Vector2.Zero;

                SoundEngine.PlaySound(SoundID.Shatter with { Pitch = 0.4f, Volume = 1.2f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.3f, Volume = 1f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 6f);
            }

            if (StateTimer == captureTick)
            {
                startPos = target.Center + target.velocity * 14f;

                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.1f }, startPos);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    int cloneCount = 4;

                    for (int i = 0; i < cloneCount; i++)
                    {
                        float jitter = Main.rand.NextFloat(-0.14f, 0.14f);
                        float angle = baseAngle + (MathHelper.TwoPi / cloneCount) * i + jitter;
                        Vector2 dir = angle.ToRotationVector2();

                        Vector2 spawnPos = startPos - dir * (ShatteredReflectionClone.TravelDistance / 2f);

                        int cloneIdx = Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            spawnPos,
                            dir,
                            ModContent.ProjectileType<ShatteredReflectionClone>(),
                            NPC.damage / 3,
                            0f,
                            Main.myPlayer,
                            0f,
                            (float)(i % 3),
                            0f
                        );

                        if (cloneIdx < Main.maxProjectiles)
                        {
                            Main.projectile[cloneIdx].netUpdate = true;
                        }
                    }
                }
            }

            if (StateTimer == echoTick && Main.netMode != NetmodeID.MultiplayerClient)
            {
                SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.6f, Volume = 0.9f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 4f);

                Vector2 echoTargetPos = target.Center + target.velocity * 10f;
                Vector2 echoDir = Main.rand.NextFloat(MathHelper.TwoPi).ToRotationVector2();
                Vector2 echoSpawnPos = echoTargetPos - echoDir * (ShatteredReflectionClone.TravelDistance / 2f);

                int echoIdx = Projectile.NewProjectile(
                    NPC.GetSource_FromAI(),
                    echoSpawnPos,
                    echoDir,
                    ModContent.ProjectileType<ShatteredReflectionClone>(),
                    NPC.damage / 3,
                    0f,
                    Main.myPlayer,
                    0f,
                    2f,
                    1f
                );

                if (echoIdx < Main.maxProjectiles)
                {
                    Main.projectile[echoIdx].netUpdate = true;
                }
            }

            Vector2 hoverAnchor = StateTimer < captureTick ? target.Center : startPos;
            Vector2 hoverTarget = hoverAnchor + new Vector2(0f, -300f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverTarget - NPC.Center) * 0.06f, 0.1f);
            NPC.rotation += 0.03f;

            if (StateTimer < captureTick)
            {
                NPC.alpha = (int)MathHelper.Lerp(0, 210, StateTimer / (float)captureTick);
            }
            else
            {
                NPC.alpha = 210;
            }

            if (Main.rand.NextBool(2))
            {
                Vector2 crackPos = NPC.Center + Main.rand.NextVector2Circular(60f, 60f);
                Dust d = Dust.NewDustPerfect(crackPos, DustID.RainbowTorch, -Main.rand.NextVector2Circular(2f, 2f), 0, default, 1f);
                d.noGravity = true;
            }

            if (StateTimer >= totalDuration - recoveryDuration)
            {
                float recProgress = (StateTimer - (totalDuration - recoveryDuration)) / (float)recoveryDuration;
                NPC.alpha = (int)MathHelper.Lerp(210, 0, MathHelper.Clamp(recProgress, 0f, 1f));

                if (StateTimer == totalDuration - recoveryDuration + 1)
                {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.2f, Volume = 0.7f }, NPC.Center);
                }
            }

            if (StateTimer >= totalDuration)
            {
                NPC.alpha = 0;
                StateTimer = 0;
                SubTimer = 0;
                State = AIState.Chase;
            }
        }

        // ==================== CORROSION SPIRAL ====================
        private void ExecuteCorrosionSpiral(Player target)
        {
            int windupDuration = 35;
            int spiralDuration = 240;
            int recoveryDuration = 30;
            int totalDuration = windupDuration + spiralDuration + recoveryDuration;

            const float startRadius = 520f;
            const float endRadius = 100f;

            if (StateTimer == 1)
            {
                startPos = target.Center;
                SubTimer = 0;

                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.3f, Volume = 0.9f }, startPos);
                SoundEngine.PlaySound(SoundID.Item68 with { Pitch = -0.5f, Volume = 0.6f }, startPos);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 0f);
            }

            Vector2 hoverTarget = startPos + new Vector2(0f, -320f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverTarget - NPC.Center) * 0.05f, 0.09f);
            NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.08f);

            if (StateTimer < windupDuration)
            {
                float progress = StateTimer / (float)windupDuration;

                for (int i = 0; i < 2; i++)
                {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dustPos = startPos + angle.ToRotationVector2() * startRadius;
                    Dust d = Dust.NewDustPerfect(dustPos, DustID.Shadowflame, (startPos - dustPos).SafeNormalize(Vector2.Zero) * 1.2f, 0, default, 1f * progress);
                    d.noGravity = true;
                }

                if (StateTimer == windupDuration - 1)
                {
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.4f, Volume = 0.8f }, startPos);
                }
            }
            else if (StateTimer < windupDuration + spiralDuration)
            {
                int activeTimer = (int)(StateTimer - windupDuration);
                float progress = activeTimer / (float)spiralDuration;
                float speedFactor = EaseInExpo(progress);

                startPos = Vector2.Lerp(startPos, target.Center, 0.045f);

                float angularSpeedPerTick = MathHelper.Lerp(0.012f, 0.34f, speedFactor);
                SubTimer += angularSpeedPerTick;

                float radius = MathHelper.Lerp(startRadius, endRadius, (float)Math.Pow(progress, 1.5f));

                Vector2 arm1Pos = startPos + SubTimer.ToRotationVector2() * radius;
                Vector2 arm2Pos = startPos + (SubTimer + MathHelper.Pi).ToRotationVector2() * radius;

                for (int i = 0; i < 3; i++)
                {
                    Dust d1 = Dust.NewDustPerfect(arm1Pos + Main.rand.NextVector2Circular(10f, 10f), DustID.Electric, Vector2.Zero, 0, Color.Cyan, 1f + speedFactor * 1.4f);
                    d1.noGravity = true;

                    Dust d2 = Dust.NewDustPerfect(arm2Pos + Main.rand.NextVector2Circular(10f, 10f), DustID.Electric, Vector2.Zero, 0, Color.Magenta, 1f + speedFactor * 1.4f);
                    d2.noGravity = true;
                }

                Dust link = Dust.NewDustPerfect(Vector2.Lerp(arm1Pos, arm2Pos, 0.5f), DustID.PurpleTorch, Vector2.Zero, 150, default, 0.6f);
                link.noGravity = true;

                int fireInterval = Math.Max(3, (int)MathHelper.Lerp(16f, 3f, speedFactor));
                if (activeTimer % fireInterval == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    float boltSpeed = MathHelper.Lerp(4.5f, 12.5f, speedFactor);
                    Vector2 predictedTarget = target.Center + target.velocity * 12f;

                    SoundEngine.PlaySound(SoundID.Item12 with { Pitch = -0.2f + speedFactor * 0.5f, Volume = 0.5f }, startPos);

                    int spreadCount = speedFactor > 0.5f ? 2 : 1;
                    float spreadAngle = MathHelper.ToRadians(10f);

                    for (int s = 0; s < spreadCount; s++)
                    {
                        float offset = spreadCount == 1 ? 0f : (s == 0 ? -spreadAngle : spreadAngle);

                        Vector2 vel1 = ((predictedTarget - arm1Pos).SafeNormalize(Vector2.UnitX)).RotatedBy(offset) * boltSpeed;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), arm1Pos, vel1, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 4, 0f, Main.myPlayer, 0f, 0f);

                        Vector2 vel2 = ((predictedTarget - arm2Pos).SafeNormalize(Vector2.UnitX)).RotatedBy(offset) * boltSpeed;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), arm2Pos, vel2, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 4, 0f, Main.myPlayer, 0f, 1f);
                    }
                }

                if (activeTimer == spiralDuration - 12)
                {
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.2f, Pitch = -0.1f }, startPos);
                    ScreenShakeSystem.StartShakeAtPoint(startPos, 10f);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        foreach (Vector2 burstOrigin in new[] { arm1Pos, arm2Pos })
                        {
                            int burstCount = 12;
                            for (int i = 0; i < burstCount; i++)
                            {
                                float angle = MathHelper.TwoPi * i / burstCount;
                                Vector2 vel = angle.ToRotationVector2() * 9f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), burstOrigin, vel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 4, 0f, Main.myPlayer, 0f, 3f);
                            }
                        }
                    }

                    for (int i = 0; i < 50; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 50f;
                        Color burstColor = Color.Lerp(Color.Cyan, Color.Magenta, i / 50f);
                        Dust d = Dust.NewDustPerfect(startPos, DustID.RainbowTorch, angle.ToRotationVector2() * Main.rand.NextFloat(4f, 22f), 0, burstColor, 2.2f);
                        d.noGravity = true;
                    }
                }
            }
            else
            {
                NPC.velocity *= 0.9f;

                if (StateTimer >= totalDuration)
                {
                    StateTimer = 0;
                    SubTimer = 0;
                    State = AIState.Chase;
                }
            }
        }

        // ==================== NULL ZONE ====================
        private void ExecuteNullZone(Player target)
        {
            int chargeDuration = 120; 
            int explosionDuration = 30;
            int recoveryDuration = 30;
            int totalDuration = chargeDuration + explosionDuration + recoveryDuration;

            if (StateTimer == 1)
            {
                startPos = target.Center;
                SubTimer = 0;

                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.4f, Volume = 0.8f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 0.6f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 2f);
            }

            Vector2 hoverTarget = target.Center + new Vector2(0f, -250f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverTarget - NPC.Center) * 0.06f, 0.08f);
            NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.06f);

            if (StateTimer < chargeDuration)
            {
                float progress = StateTimer / (float)chargeDuration;

                for (int i = 0; i < 3 + (int)(progress * 5); i++)
                {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float distance = Main.rand.NextFloat(100f, 450f);
                    Vector2 sourcePos = NPC.Center + angle.ToRotationVector2() * distance;
                    Vector2 vel = (NPC.Center - sourcePos).SafeNormalize(Vector2.Zero) * Main.rand.NextFloat(2f, 6f + progress * 4f);

                    Color chargeColor = Color.Lerp(Color.Cyan, Color.Magenta, (float)Math.Sin(angle + Main.GlobalTimeWrappedHourly * 2f) * 0.5f + 0.5f);
                    Dust d = Dust.NewDustPerfect(sourcePos, DustID.Electric, vel, 0, chargeColor, 1f + progress * 1.2f);
                    d.noGravity = true;

                    if (Main.rand.NextBool(3))
                    {
                        Dust spark = Dust.NewDustPerfect(sourcePos + Main.rand.NextVector2Circular(10f, 10f), DustID.WhiteTorch, vel * 0.5f, 0, Color.White, 0.6f);
                        spark.noGravity = true;
                    }
                }

                int ringCount = 6;
                float ringRadius = MathHelper.Lerp(320f, 60f, progress);
                float ringAngle = StateTimer * 0.04f;

                for (int i = 0; i < ringCount; i++)
                {
                    float angle = ringAngle + (MathHelper.TwoPi / ringCount) * i;
                    Vector2 ringPos = NPC.Center + angle.ToRotationVector2() * ringRadius;

                    Color ringColor = Color.Lerp(Color.Cyan, Color.Magenta, i / (float)ringCount);
                    Dust d = Dust.NewDustPerfect(ringPos, DustID.RainbowTorch, Vector2.Zero, 0, ringColor, 1.2f + progress * 1.2f);
                    d.noGravity = true;
                }

                if (progress > 0.6f && Main.rand.NextBool(2))
                {
                    float pulse = 1f + (float)Math.Sin(StateTimer * 0.2f) * 0.3f;
                    int alpha = (int)(150 * progress);
                    Dust glow = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(20f, 20f), DustID.WhiteTorch, Vector2.Zero, 0, Color.White, 1.2f * pulse);
                    glow.noGravity = true;
                    glow.alpha = alpha;
                }

                if (progress > 0.7f)
                {
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, progress * 3f);
                }

                if (StateTimer % 15 == 0)
                {
                    float pitch = -0.6f + progress * 0.8f;
                    SoundEngine.PlaySound(SoundID.Item12 with { Pitch = pitch, Volume = 0.3f + progress * 0.4f }, NPC.Center);
                }

                if (progress > 0.9f && Main.rand.NextBool(2))
                {
                    Vector2 overchargePos = NPC.Center + Main.rand.NextVector2Circular(30f, 30f);
                    Dust d = Dust.NewDustPerfect(overchargePos, DustID.RainbowTorch, Main.rand.NextVector2Circular(6f, 6f), 0, Color.White, 2f);
                    d.noGravity = true;
                }

                if (StateTimer == chargeDuration - 1)
                {
                    SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 1.8f }, NPC.Center);
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 16f);
                }
            }
            else if (StateTimer < chargeDuration + explosionDuration)
            {
                int expTimer = (int)(StateTimer - chargeDuration);

                if (expTimer == 0)
                {
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.6f, Pitch = -0.3f }, NPC.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.2f, Volume = 1.3f }, NPC.Center);
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 20f);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int burstCount = 48;
                        float angleStep = MathHelper.TwoPi / burstCount;

                        for (int i = 0; i < burstCount; i++)
                        {
                            float angle = angleStep * i + Main.rand.NextFloat(-0.03f, 0.03f);
                            float speed = Main.rand.NextFloat(6f, 22f);
                            Vector2 vel = angle.ToRotationVector2() * speed;

                            int colorMode = Main.rand.Next(0, 4);
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                vel,
                                ModContent.ProjectileType<UnknownEntityBolt>(),
                                NPC.damage / 3,
                                0f,
                                Main.myPlayer,
                                0f,
                                colorMode
                            );
                        }

                        for (int i = 0; i < 8; i++)
                        {
                            Vector2 dirToPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                            float spread = (i - 3.5f) * 0.2f;
                            Vector2 vel = dirToPlayer.RotatedBy(spread) * Main.rand.NextFloat(10f, 18f);
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                vel,
                                ModContent.ProjectileType<UnknownEntityBolt>(),
                                NPC.damage / 2,
                                0f,
                                Main.myPlayer,
                                0f,
                                2
                            );
                        }

                        int ringCount = 16;
                        for (int ring = 0; ring < 3; ring++)
                        {
                            float ringAngle = ring * 0.8f + Main.GlobalTimeWrappedHourly;
                            float ringSpeed = 5f + ring * 3f;
                            for (int i = 0; i < ringCount; i++)
                            {
                                float angle = ringAngle + (MathHelper.TwoPi / ringCount) * i;
                                Vector2 vel = angle.ToRotationVector2() * (ringSpeed + Main.rand.NextFloat(-1f, 1f));
                                int cm = (ring + i) % 3;
                                Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(),
                                    NPC.Center + angle.ToRotationVector2() * 20f,
                                    vel,
                                    ModContent.ProjectileType<UnknownEntityBolt>(),
                                    NPC.damage / 4,
                                    0f,
                                    Main.myPlayer,
                                    0f,
                                    cm
                                );
                            }
                        }
                    }

                    for (int i = 0; i < 100; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 100f;
                        Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 30f);
                        Color burstColor = Main.hslToRgb((i / 100f + Main.GlobalTimeWrappedHourly * 0.5f) % 1f, 1f, 0.85f);
                        Dust d = Dust.NewDustPerfect(NPC.Center, DustID.RainbowTorch, vel, 0, burstColor, 2.8f);
                        d.noGravity = true;
                    }

                    for (int i = 0; i < 30; i++)
                    {
                        Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(40f, 40f), DustID.WhiteTorch, Main.rand.NextVector2Circular(4f, 4f), 0, Color.White, 3f);
                        d.noGravity = true;
                    }
                }

                if (Main.rand.NextBool(3))
                {
                    Vector2 sparkPos = NPC.Center + Main.rand.NextVector2Circular(150f * (1f - expTimer / (float)explosionDuration), 150f * (1f - expTimer / (float)explosionDuration));
                    Dust d = Dust.NewDustPerfect(sparkPos, DustID.Electric, Main.rand.NextVector2Circular(3f, 3f), 0, Color.Cyan, 1.2f);
                    d.noGravity = true;
                }
            }
            else
            {
                NPC.velocity *= 0.92f;

                if (StateTimer >= totalDuration)
                {
                    StateTimer = 0;
                    SubTimer = 0;
                    State = AIState.Chase;
                }
            }
        }
    }
}