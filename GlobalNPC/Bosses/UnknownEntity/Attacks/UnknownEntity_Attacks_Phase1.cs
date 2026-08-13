using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    public partial class UnknownEntity
    {
        // ==================== CHASE ====================
        private void ExecuteChase(Player target)
        {
            if (NPC.alpha > 0) NPC.alpha = Math.Max(0, NPC.alpha - 15);

            float hoverTime = (float)Main.GlobalTimeWrappedHourly * 2.5f;
            float hoverY = (float)Math.Sin(hoverTime) * 35f;
            float hoverX = (float)Math.Cos(hoverTime * 0.5f) * 50f;

            Vector2 hoverOffset = new Vector2((target.direction * -220f) + hoverX, -180f + hoverY);
            Vector2 targetCenter = target.Center + hoverOffset;

            Vector2 desiredVelocity = (targetCenter - NPC.Center).SafeNormalize(Vector2.Zero) * 11f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVelocity, 0.05f);

            float targetRotation = NPC.velocity.X * 0.035f;
            NPC.rotation = MathHelper.Lerp(NPC.rotation, targetRotation, 0.1f);

            float dist = Vector2.Distance(NPC.Center, target.Center);
            if (dist < 100f && attackTimer <= 0)
            {
                isAttacking = true;
                attackTimer = 20;
            }

            if (StateTimer >= 60)
            {
                StateTimer = 0;
                SubTimer = 0;

                if (isDesperation)
                {
                    int nextAttack = Main.rand.Next(14);
                    switch (nextAttack)
                    {
                        case 0: State = AIState.Dash; break;
                        case 1: State = AIState.ProjectileRing; break;
                        case 2: State = AIState.Teleport; break;
                        case 3: State = AIState.WormholeDash; break;
                        case 4: State = AIState.PrismMirage; break;
                        case 5: State = AIState.PhantomGrid; break;
                        case 6: State = AIState.VoidCollapse; break;
                        case 7: State = AIState.SerpentPortalDash; break;
                        case 8: State = AIState.SummonMinionHorde; break;
                        case 9: State = AIState.DeathLaserBlender; break;
                        case 10: State = AIState.MemoryFracture; break;
                        case 11: State = AIState.ShatteredReflection; break;
                        case 12: State = AIState.CorrosionSpiral; break;
                        default: State = AIState.NullZone; break;
                    }
                }
                else if (isPhase2)
                {
                    int nextAttack = Main.rand.Next(7);
                    if (nextAttack == 0) State = AIState.SerpentPortalDash;
                    else if (nextAttack == 1) State = AIState.SummonMinionHorde;
                    else if (nextAttack == 2) State = AIState.DeathLaserBlender;
                    else if (nextAttack == 3) State = AIState.MemoryFracture;
                    else if (nextAttack == 4) State = AIState.ShatteredReflection;
                    else if (nextAttack == 5) State = AIState.CorrosionSpiral;
                    else State = AIState.NullZone;
                }
                else
                {
                    int nextAttack = Main.rand.Next(7);
                    if (nextAttack == 0) State = AIState.Dash;
                    else if (nextAttack == 1) State = AIState.ProjectileRing;
                    else if (nextAttack == 2) State = AIState.Teleport;
                    else if (nextAttack == 3) State = AIState.WormholeDash;
                    else if (nextAttack == 4) State = AIState.PrismMirage;
                    else if (nextAttack == 5) State = AIState.PhantomGrid;
                    else State = AIState.VoidCollapse;
                }
            }
        }

        // ==================== DASH ====================
        private void ExecuteDash(Player target)
        {
            int windupDuration = 35;
            int dashDuration = 28;
            int recoveryDuration = 25;

            if (StateTimer == 1)
            {
                startPos = NPC.Center;
                dashTargetDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                SubTimer = 0;

                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 0f);

                if (dashCount == 2)
                {
                    for (int i = 0; i < 30; i++)
                    {
                        Vector2 pos = NPC.Center + Main.rand.NextVector2Circular(40f, 40f);
                        Dust d = Dust.NewDustPerfect(pos, DustID.RedTorch, Vector2.Zero, 0, Color.Red, 1.5f);
                        d.noGravity = true;
                    }
                }
            }

            if (StateTimer < windupDuration)
            {
                float progress = StateTimer / windupDuration;
                float elasticFactor = EaseInElastic(progress);

                Vector2 pullBackPos = startPos - (dashTargetDir * 150f);
                NPC.Center = Vector2.Lerp(startPos, pullBackPos, elasticFactor);

                float targetAngle = dashTargetDir.ToRotation() + (NPC.spriteDirection == -1 ? MathHelper.Pi : 0);
                NPC.rotation = MathHelper.Lerp(NPC.rotation, targetAngle * 0.25f, 0.15f);

                for (int i = 0; i < 8; i++)
                {
                    float lerpAmount = Main.rand.NextFloat(0f, 1f);
                    Vector2 dustPos = Vector2.Lerp(pullBackPos, target.Center + new Vector2(0f, -30f), lerpAmount);
                    dustPos += Main.rand.NextVector2Circular(12f, 12f);

                    Dust t = Dust.NewDustPerfect(dustPos, DustID.Electric, Vector2.Zero, 0, Color.Cyan, 0.8f + (1f - lerpAmount) * 0.8f);
                    t.noGravity = true;
                }

                for (int i = 0; i < 2; i++)
                {
                    int dustType = Main.rand.NextBool() ? DustID.PurpleTorch : DustID.PinkTorch;
                    Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(20, 20), dustType, dashTargetDir * -3f);
                    d.noGravity = true;
                }
            }
            else if (StateTimer == windupDuration)
            {
                SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 4f);

                float dashSpeed = 48f;
                if (dashCount == 1) dashSpeed = 54f;
                if (dashCount == 2) dashSpeed = 62f;

                NPC.velocity = dashTargetDir * dashSpeed;
            }
            else if (StateTimer > windupDuration && StateTimer <= windupDuration + dashDuration)
            {
                float slowDownFactor = 0.985f - (dashCount * 0.005f);
                NPC.velocity *= Math.Max(slowDownFactor, 0.97f);

                for (int i = 0; i < NPC.oldPos.Length; i++)
                {
                    if (NPC.oldPos[i] == Vector2.Zero) continue;
                    float progress = i / (float)NPC.oldPos.Length;
                    if (progress > 0.85f) continue;

                    Vector2 dustPos = NPC.oldPos[i] + (NPC.Size * 0.5f) + Main.rand.NextVector2Circular(4f, 4f);

                    Dust d1 = Dust.NewDustPerfect(dustPos, DustID.BlueTorch, Vector2.Zero, 0, GetMainColor(), 1.0f * (1f - progress) * 0.9f);
                    d1.noGravity = true;

                    Dust d2 = Dust.NewDustPerfect(dustPos, DustID.WhiteTorch, Vector2.Zero, 0, Color.White, 0.6f * (1f - progress) * 0.8f);
                    d2.noGravity = true;
                }

                Dust d = Dust.NewDustPerfect(NPC.Center, DustID.Shadowflame, -NPC.velocity * 0.2f, 0, default, 2.2f);
                d.noGravity = true;

                if (StateTimer % 4 == 0)
                {
                    SubTimer++;
                    if (SubTimer % 3 != 0)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            Vector2 perpRight = dashTargetDir.RotatedBy(MathHelper.PiOver2);
                            Vector2 perpLeft = dashTargetDir.RotatedBy(-MathHelper.PiOver2);

                            float projSpeed = 10f;

                            int boltDamage = NPC.damage / 3;
                            if (dashCount == 2)
                            {
                                boltDamage = NPC.damage / 2;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dashTargetDir * 14f, ModContent.ProjectileType<UnknownEntityBolt>(), boltDamage, 1f, Main.myPlayer, 1f, 0f);

                                float dashProgress = (StateTimer - windupDuration) / dashDuration;
                                if (dashProgress > 0.3f && dashProgress < 0.4f)
                                {
                                    for (int j = 0; j < 10; j++)
                                    {
                                        float a = MathHelper.TwoPi * j / 10f;
                                        Vector2 shockVel = a.ToRotationVector2() * Main.rand.NextFloat(6f, 12f);
                                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center + (dashTargetDir * 30f), shockVel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 3, 1f, Main.myPlayer, 0f, 0f);
                                    }
                                }
                            }

                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perpRight * projSpeed, ModContent.ProjectileType<UnknownEntityBolt>(), boltDamage, 1f, Main.myPlayer, 0f, 0f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perpLeft * projSpeed, ModContent.ProjectileType<UnknownEntityBolt>(), boltDamage, 1f, Main.myPlayer, 0f, 0f);
                        }
                        SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.4f }, NPC.Center);
                    }
                }
            }
            else if (StateTimer > windupDuration + dashDuration)
            {
                NPC.velocity *= 0.90f;
                NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.1f);

                if (StateTimer > windupDuration + dashDuration + recoveryDuration)
                {
                    for (int i = 0; i < 15; i++)
                    {
                        Vector2 vel = Main.rand.NextVector2Circular(6f, 6f);
                        Dust ring = Dust.NewDustPerfect(NPC.Center, DustID.BlueTorch, vel, 0, Color.Cyan, 1.2f);
                        ring.noGravity = true;
                    }

                    dashCount++;
                    if (dashCount < 3)
                    {
                        StateTimer = 1;
                        SubTimer = 0;
                        startPos = NPC.Center;
                        dashTargetDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                    }
                    else
                    {
                        SoundEngine.PlaySound(SoundID.DD2_BetsyDeath with { Volume = 1.5f }, NPC.Center);
                        ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 22f);

                        for (int i = 0; i < 70; i++)
                        {
                            Vector2 vel = Main.rand.NextVector2Circular(16f, 16f);
                            Dust boom = Dust.NewDustPerfect(NPC.Center, DustID.RainbowTorch, vel, 0, Color.HotPink, 3.0f);
                            boom.noGravity = true;
                        }

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            for (int i = 0; i < 20; i++)
                            {
                                float angle = MathHelper.TwoPi * i / 20f;
                                Vector2 finisherVel = angle.ToRotationVector2() * 10f + Main.rand.NextVector2Circular(2f, 2f);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, finisherVel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 2, 1f, Main.myPlayer, 0f, 3f);
                            }
                        }

                        dashCount = 0;
                        StateTimer = 0;
                        SubTimer = 0;
                        State = AIState.Chase;
                        NPC.netUpdate = true;
                    }
                }
            }
        }

        private Color GetMainColor()
        {
            if (isDesperation) return Color.White;
            if (isPhase2) return Color.Magenta;
            return Color.Cyan;
        }

        // ==================== WORMHOLE DASH ====================
        private void ExecuteWormholeDash(Player target)
        {
            int windupDuration = 28;
            int dashDuration = 22;
            int fadeDuration = 14;
            int totalCycleTime = windupDuration + dashDuration + fadeDuration;

            if (StateTimer == 1)
            {
                NPC.alpha = 0;
                startPos = NPC.Center;

                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                portalExitPos = target.Center + angle.ToRotationVector2() * 650f;

                dashTargetDir = (target.Center - portalExitPos).SafeNormalize(Vector2.UnitX);

                SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 0f);
            }

            if (StateTimer < windupDuration)
            {
                float progress = StateTimer / (float)windupDuration;
                float elasticFactor = EaseInElastic(progress);

                NPC.velocity *= 0.8f;
                Vector2 pullBackPos = startPos - (dashTargetDir * 140f);
                NPC.Center = Vector2.Lerp(startPos, pullBackPos, elasticFactor);

                float targetAngle = dashTargetDir.ToRotation() + (NPC.spriteDirection == -1 ? MathHelper.Pi : 0);
                NPC.rotation = MathHelper.Lerp(NPC.rotation, targetAngle * 0.35f, 0.2f);

                for (int i = 0; i < 5; i++)
                {
                    Vector2 exitDustVel = Main.rand.NextVector2Circular(45, 45);
                    int dustID = (i % 2 == 0) ? DustID.Electric : (i % 3 == 0 ? DustID.PinkTorch : DustID.Shadowflame);

                    Dust dExit = Dust.NewDustPerfect(portalExitPos + exitDustVel, dustID, -exitDustVel * 0.12f);
                    dExit.noGravity = true;
                    dExit.scale = Main.rand.NextFloat(1.3f, 1.8f);
                }
            }
            else if (StateTimer == windupDuration)
            {
                NPC.Center = portalExitPos;
                NPC.velocity = dashTargetDir * 58f;

                SoundEngine.PlaySound(SoundID.Item84, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);

                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 0f);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // Ring Bolts
                    int ringCount = 12;
                    float spreadAngle = MathHelper.TwoPi / ringCount;
                    for (int i = 0; i < ringCount; i++)
                    {
                        Vector2 projVel = (spreadAngle * i).ToRotationVector2() * 8.5f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, projVel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 3, 1f, Main.myPlayer, 0f, 1f);
                    }

                    // Tembakkan Side Death Rays (Noise Laser) Kiri & Kanan dari Portal Keluar
                    Vector2 perpRight = dashTargetDir.RotatedBy(MathHelper.PiOver2);
                    Vector2 perpLeft = dashTargetDir.RotatedBy(-MathHelper.PiOver2);

                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perpRight, ModContent.ProjectileType<UnknownEntityDeathRay>(), NPC.damage / 3, 0f, Main.myPlayer, 0f, 0f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perpLeft, ModContent.ProjectileType<UnknownEntityDeathRay>(), NPC.damage / 3, 0f, Main.myPlayer, 0f, 0f);
                }

                for (int i = 0; i < 40; i++)
                {
                    float angle = MathHelper.TwoPi * i / 40f;
                    Vector2 ringVel = angle.ToRotationVector2() * Main.rand.NextFloat(10f, 22f);

                    Color chromaColor = Main.hslToRgb((i / 40f + Main.GlobalTimeWrappedHourly) % 1f, 1f, 0.75f);
                    Dust d = Dust.NewDustPerfect(NPC.Center, DustID.RainbowTorch, ringVel, 0, chromaColor, 2.2f);
                    d.noGravity = true;
                }
            }
            else if (StateTimer > windupDuration && StateTimer <= windupDuration + dashDuration)
            {
                NPC.velocity *= 0.98f;

                int sparkDust = Main.rand.NextBool() ? DustID.Electric : DustID.PinkTorch;
                Dust d = Dust.NewDustPerfect(NPC.Center, sparkDust, -NPC.velocity * 0.25f + Main.rand.NextVector2Circular(3, 3), 0, default, 1.6f);
                d.noGravity = true;
            }
            else if (StateTimer > windupDuration + dashDuration && StateTimer < totalCycleTime)
            {
                NPC.velocity *= 0.88f;
                float fadeProgress = (StateTimer - (windupDuration + dashDuration)) / (float)fadeDuration;
                NPC.alpha = (int)MathHelper.Lerp(0, 255, fadeProgress);
            }
            else if (StateTimer >= totalCycleTime)
            {
                NPC.alpha = 0;
                SubTimer++;

                if (SubTimer >= 2)
                {
                    StateTimer = 0;
                    SubTimer = 0;
                    State = AIState.Chase;
                }
                else
                {
                    StateTimer = 0;
                }
            }
        }

        // ==================== PROJECTILE RING ====================
        private void ExecuteProjectileRing()
        {
            int chargeDuration = 35;
            int barrageDuration = 70;
            int climaxTick = 105;
            int totalDuration = 135;

            if (StateTimer < chargeDuration)
            {
                NPC.velocity *= 0.88f;
                NPC.velocity.Y -= 0.15f;
                NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.15f);

                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 0f);

                if (StateTimer == 1)
                {
                    SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.3f, Volume = 0.8f }, NPC.Center);
                }

                for (int i = 0; i < 4; i++)
                {
                    Vector2 spawnOffset = Main.rand.NextVector2CircularEdge(200f, 200f);
                    Vector2 dustVel = -spawnOffset * 0.08f;
                    Color chromaColor = Main.hslToRgb((StateTimer * 0.03f + i * 0.25f) % 1f, 1f, 0.75f);

                    Dust d = Dust.NewDustPerfect(NPC.Center + spawnOffset, DustID.RainbowTorch, dustVel, 0, chromaColor, 1.8f);
                    d.noGravity = true;
                }
            }
            else if (StateTimer >= chargeDuration && StateTimer < climaxTick)
            {
                NPC.velocity *= 0.92f;
                NPC.position.Y += (float)Math.Sin(StateTimer * 0.2f) * 0.6f;

                float barrageProgress = (StateTimer - chargeDuration) / (float)barrageDuration;

                if (StateTimer % 9 == 0)
                {
                    SoundEngine.PlaySound(SoundID.Item12 with { Volume = 0.6f }, NPC.Center);
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 0f);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int count = 8;
                        float angleStep = MathHelper.TwoPi / count;
                        float rotOffset = StateTimer * 0.09f;

                        for (int i = 0; i < count; i++)
                        {
                            Vector2 vel = (angleStep * i + rotOffset).ToRotationVector2() * (9f + (float)Math.Sin(barrageProgress * MathHelper.Pi) * 3f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 4, 1f, Main.myPlayer, 0f, 2f);
                        }
                    }
                }

                if (StateTimer % 15 == 3)
                {
                    SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.7f }, NPC.Center);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int count = 6;
                        float angleStep = MathHelper.TwoPi / count;
                        float rotOffset = -StateTimer * 0.07f;

                        for (int i = 0; i < count; i++)
                        {
                            Vector2 vel = (angleStep * i + rotOffset).ToRotationVector2() * 7.5f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 3, 1f, Main.myPlayer, 0f, 2f);
                        }
                    }
                }
            }
            else if (StateTimer == climaxTick)
            {
                SoundEngine.PlaySound(SoundID.Item62, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item100, NPC.Center);
                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);

                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 0f);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int starburstCount = 16;
                    float angleStep = MathHelper.TwoPi / starburstCount;
                    for (int i = 0; i < starburstCount; i++)
                    {
                        Vector2 vel = (angleStep * i).ToRotationVector2() * 12f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 3, 1f, Main.myPlayer, 0f, 3f);
                    }

                    Player target = Main.player[NPC.target];
                    Vector2 targetDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitY);
                    for (int i = -1; i <= 1; i++)
                    {
                        Vector2 beamVel = targetDir.RotatedBy(i * 0.18f) * 16f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, beamVel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 3, 1f, Main.myPlayer, 0f, 3f);
                    }
                }

                for (int i = 0; i < 48; i++)
                {
                    float angle = MathHelper.TwoPi * i / 48f;
                    Vector2 ringVel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 25f);
                    Color chromaColor = Main.hslToRgb((i / 48f + Main.GlobalTimeWrappedHourly) % 1f, 1f, 0.8f);

                    Dust d = Dust.NewDustPerfect(NPC.Center, DustID.RainbowTorch, ringVel, 0, chromaColor, 2.5f);
                    d.noGravity = true;
                }
            }
            else if (StateTimer > climaxTick)
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

        // ==================== TELEPORT ====================
        private void ExecuteTeleport(Player target)
        {
            int fadeTime = 15;
            int arrivalBurstTime = 12;

            if (StateTimer <= fadeTime)
            {
                NPC.velocity *= 0.8f;
                NPC.alpha = (int)MathHelper.Lerp(0, 255, StateTimer / (float)fadeTime);

                if (StateTimer == 1)
                {
                    SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                }

                if (StateTimer % 3 == 0 && Main.rand.NextBool(2))
                {
                    Vector2 burstPos = NPC.Center + Main.rand.NextVector2Circular(40f, 40f);
                    Dust d = Dust.NewDustPerfect(burstPos, DustID.Electric, Main.rand.NextVector2Circular(4f, 4f), 0, Color.Magenta, 1.2f);
                    d.noGravity = true;
                }
            }
            else if (StateTimer == fadeTime + 1)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float distance = Main.rand.NextFloat(180f, 380f);
                Vector2 teleportOffset = angle.ToRotationVector2() * distance;
                teleportOffset.Y -= 100f;

                NPC.Center = target.Center + teleportOffset;
                NPC.velocity = Vector2.Zero;

                SoundEngine.PlaySound(SoundID.Item100, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 0.7f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 6f);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int burstCount = 14;
                    float angleStep = MathHelper.TwoPi / burstCount;

                    for (int i = 0; i < burstCount; i++)
                    {
                        Vector2 vel = (angleStep * i + Main.rand.NextFloat(-0.08f, 0.08f)).ToRotationVector2() * Main.rand.NextFloat(7f, 14f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 4, 0f, Main.myPlayer, 0f, Main.rand.Next(0, 2));
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        Vector2 dirToPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                        float spread = (i - 1.5f) * 0.3f;
                        Vector2 vel = dirToPlayer.RotatedBy(spread) * 12f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 3, 0f, Main.myPlayer, 0f, 1f);
                    }
                }

                for (int i = 0; i < 50; i++)
                {
                    float angle2 = MathHelper.TwoPi * i / 50f;
                    Vector2 vel = angle2.ToRotationVector2() * Main.rand.NextFloat(4f, 20f);
                    Color burstColor = i % 2 == 0 ? Color.Cyan : Color.Magenta;
                    Dust d = Dust.NewDustPerfect(NPC.Center, DustID.RainbowTorch, vel, 0, burstColor, 2f);
                    d.noGravity = true;
                }

                if (bloomCircleTex?.Value != null) { }
            }
            else if (StateTimer <= fadeTime * 2 + 1 + arrivalBurstTime)
            {
                float progress = (StateTimer - fadeTime) / (float)(fadeTime + arrivalBurstTime);
                NPC.alpha = (int)MathHelper.Lerp(255, 0, Math.Min(progress * 1.5f, 1f));

                if (Main.rand.NextBool(3))
                {
                    Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(30f, 30f), DustID.Electric, Main.rand.NextVector2Circular(2f, 2f), 0, Color.Cyan, 0.8f);
                    d.noGravity = true;
                }
            }
            else
            {
                NPC.alpha = 0;
                StateTimer = 0;
                State = AIState.Chase;
            }
        }

        // ==================== PRISM MIRAGE ====================
        private void ExecutePrismMirage(Player target)
        {
            int windupDuration = 45;
            int dashDuration = 18;
            int recoveryDuration = 30;
            int climaxTick = windupDuration + (dashDuration / 2);
            int totalDuration = windupDuration + dashDuration + recoveryDuration;

            if (StateTimer == 1)
            {
                NPC.alpha = 0;
                startPos = target.Center;

                float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                dashTargetDir = baseAngle.ToRotationVector2();

                portalExitPos = startPos + baseAngle.ToRotationVector2() * 650f;
                NPC.Center = portalExitPos;
                NPC.velocity = Vector2.Zero;

                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.2f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item117 with { Volume = 0.8f }, startPos);
                ScreenShakeSystem.StartShakeAtPoint(startPos, 0f);
            }

            if (StateTimer < windupDuration)
            {
                NPC.velocity *= 0.8f;
                float progress = StateTimer / (float)windupDuration;

                for (int i = 0; i < 4; i++)
                {
                    float angle = dashTargetDir.ToRotation() + (i * MathHelper.PiOver2);
                    Vector2 cloneSpawnPos = startPos + angle.ToRotationVector2() * 650f;

                    if (i == 0)
                    {
                        float angleToCenter = (startPos - NPC.Center).ToRotation();
                        NPC.rotation = MathHelper.Lerp(NPC.rotation, angleToCenter, 0.25f);
                    }

                    Vector2 offset = Main.rand.NextVector2CircularEdge(120f, 120f);
                    Vector2 pullVel = -offset * 0.12f;
                    Dust d = Dust.NewDustPerfect(cloneSpawnPos + offset, DustID.RainbowTorch, pullVel, 0, Main.hslToRgb((progress + i * 0.25f) % 1f, 1f, 0.8f), 1.6f);
                    d.noGravity = true;
                }

                Vector2 centerDustOffset = Main.rand.NextVector2CircularEdge(180f * (1f - progress), 180f * (1f - progress));
                Dust dCenter = Dust.NewDustPerfect(startPos + centerDustOffset, DustID.Electric, -centerDustOffset * 0.1f, 0, default, 1.4f);
                dCenter.noGravity = true;
            }
            else if (StateTimer == windupDuration)
            {
                SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.2f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.3f }, NPC.Center);

                Vector2 dirToCenter = (startPos - NPC.Center).SafeNormalize(Vector2.UnitX);
                NPC.velocity = dirToCenter * 58f;

                ScreenShakeSystem.StartShakeAtPoint(startPos, 0f);
            }
            else if (StateTimer > windupDuration && StateTimer <= windupDuration + dashDuration)
            {
                NPC.velocity *= 0.985f;

                Dust d = Dust.NewDustPerfect(NPC.Center, DustID.Electric, -NPC.velocity * 0.2f, 0, default, 2f);
                d.noGravity = true;

                if (StateTimer == climaxTick)
                {
                    SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.2f, Pitch = -0.2f }, startPos);
                    SoundEngine.PlaySound(SoundID.Item100 with { Volume = 1.1f }, startPos);
                    SoundEngine.PlaySound(SoundID.Item122, startPos);

                    ScreenShakeSystem.StartShakeAtPoint(startPos, 0f);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        int projCount = 16;
                        float angleStep = MathHelper.TwoPi / projCount;
                        for (int i = 0; i < projCount; i++)
                        {
                            Vector2 projVel = (angleStep * i + dashTargetDir.ToRotation()).ToRotationVector2() * 11f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), startPos, projVel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage * 2 / 5, 1f, Main.myPlayer, 0f, 2f);
                        }

                        for (int i = 0; i < 4; i++)
                        {
                            float diagAngle = dashTargetDir.ToRotation() + MathHelper.PiOver4 + (i * MathHelper.PiOver2);
                            Vector2 beamVel = diagAngle.ToRotationVector2() * 15f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), startPos, beamVel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 2, 1f, Main.myPlayer, 0f, 2f);
                        }
                    }

                    for (int i = 0; i < 60; i++)
                    {
                        float angle = MathHelper.TwoPi * i / 60f;
                        Vector2 ringVel = angle.ToRotationVector2() * Main.rand.NextFloat(8f, 32f);
                        Color chromaColor = Main.hslToRgb((i / 60f + Main.GlobalTimeWrappedHourly) % 1f, 1f, 0.85f);

                        Dust dExplode = Dust.NewDustPerfect(startPos, DustID.RainbowTorch, ringVel, 0, chromaColor, 2.8f);
                        dExplode.noGravity = true;
                    }
                }
            }
            else if (StateTimer > windupDuration + dashDuration)
            {
                NPC.velocity *= 0.88f;
                NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.12f);

                if (StateTimer >= totalDuration)
                {
                    StateTimer = 0;
                    SubTimer = 0;
                    State = AIState.Chase;
                }
            }
        }

        // ==================== PHANTOM GRID ====================
        private void ExecutePhantomGrid(Player target)
        {
            int windupDuration = 55;
            int dashDuration = 20;
            int slashExplodeTick = windupDuration + dashDuration;
            int recoveryDuration = 30;
            int totalDuration = slashExplodeTick + recoveryDuration;

            if (StateTimer == 1)
            {
                startPos = target.Center;
                NPC.alpha = 255;
                NPC.velocity = Vector2.Zero;

                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.2f }, startPos);
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.4f, Volume = 1f }, startPos);
                ScreenShakeSystem.StartShakeAtPoint(startPos, 4f);
            }

            if (StateTimer < windupDuration)
            {
                float progress = StateTimer / (float)windupDuration;

                for (int i = 0; i < 2; i++)
                {
                    float lineAngle = MathHelper.PiOver4 + (i * MathHelper.PiOver2);
                    Vector2 lineDir = lineAngle.ToRotationVector2();

                    if (Main.rand.NextBool(2))
                    {
                        float randomDist = Main.rand.NextFloat(-750f, 750f);
                        Vector2 dustPos = startPos + lineDir * randomDist;
                        Vector2 dustVel = -lineDir * Main.rand.NextFloat(2f, 8f);

                        Dust d = Dust.NewDustPerfect(dustPos, DustID.Electric, dustVel, 0, Color.Cyan, 1.2f);
                        d.noGravity = true;
                    }
                }

                for (int i = 0; i < 3; i++)
                {
                    Vector2 offset = Main.rand.NextVector2CircularEdge(300f * (1f - progress), 300f * (1f - progress));
                    Dust d = Dust.NewDustPerfect(startPos + offset, DustID.RainbowTorch, -offset * 0.15f, 0, Color.DeepPink, 1.8f);
                    d.noGravity = true;
                }
            }
            else if (StateTimer == windupDuration)
            {
                SoundEngine.PlaySound(SoundID.Item74 with { Volume = 1.2f, Pitch = 0.2f }, startPos);
                SoundEngine.PlaySound(SoundID.Item117 with { Volume = 1.2f, Pitch = -0.1f }, startPos);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.5f }, startPos);
                ScreenShakeSystem.StartShakeAtPoint(startPos, 12f);

                for (int i = 0; i < 2; i++)
                {
                    float lineAngle = MathHelper.PiOver4 + (i * MathHelper.PiOver2);
                    Vector2 lineDir = lineAngle.ToRotationVector2();

                    for (float dist = -800f; dist <= 800f; dist += 25f)
                    {
                        Vector2 spawnPos = startPos + lineDir * dist;
                        Vector2 perpendicularVel = lineDir.RotatedBy(MathHelper.PiOver2) * Main.rand.NextFloat(-6f, 6f);

                        Dust d = Dust.NewDustPerfect(spawnPos, DustID.RainbowTorch, perpendicularVel, 0, Color.Turquoise, 2f);
                        d.noGravity = true;

                        Dust d2 = Dust.NewDustPerfect(spawnPos, DustID.Electric, perpendicularVel * 1.5f, 0, default, 1.4f);
                        d2.noGravity = true;
                    }
                }

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    for (int i = 0; i < 4; i++)
                    {
                        float slashAngle = MathHelper.PiOver4 + (i * MathHelper.PiOver2);
                        Vector2 slashDir = slashAngle.ToRotationVector2();

                        for (int j = -3; j <= 3; j++)
                        {
                            Vector2 spawnPos = startPos + slashDir * (j * 120f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, slashDir * 18f, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 3, 1f, Main.myPlayer, 0f, 0f);
                        }
                    }
                }
            }
            else if (StateTimer == slashExplodeTick)
            {
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.3f }, startPos);
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f }, startPos);
                ScreenShakeSystem.StartShakeAtPoint(startPos, 15f);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int projCount = 16;
                    float angleStep = MathHelper.TwoPi / projCount;
                    for (int i = 0; i < projCount; i++)
                    {
                        Vector2 vel = (angleStep * i).ToRotationVector2() * 12f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), startPos, vel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage * 2 / 5, 1f, Main.myPlayer, 0f, 0f);
                    }
                }

                for (int i = 0; i < 80; i++)
                {
                    float angle = MathHelper.TwoPi * i / 80f;
                    Vector2 ringVel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 36f);
                    Color chromaColor = Main.hslToRgb((i / 80f + Main.GlobalTimeWrappedHourly) % 1f, 1f, 0.85f);

                    Dust d = Dust.NewDustPerfect(startPos, DustID.RainbowTorch, ringVel, 0, chromaColor, 3f);
                    d.noGravity = true;
                }
            }
            else if (StateTimer >= totalDuration)
            {
                NPC.Center = startPos + new Vector2(0f, -220f);
                NPC.alpha = 0;
                NPC.velocity = Vector2.Zero;

                StateTimer = 0;
                SubTimer = 0;
                State = AIState.Chase;
            }
        }

        // ==================== VOID COLLAPSE ====================
        private void ExecuteVoidCollapse(Player target)
        {
            int windupDuration = 30;
            int pullDuration = 110;
            int collapseTick = windupDuration + pullDuration;
            int recoveryDuration = 25;
            int totalDuration = collapseTick + recoveryDuration;

            if (StateTimer == 1)
            {
                startPos = target.Center;
                SubTimer = 0;

                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.6f, Volume = 0.9f }, startPos);
                SoundEngine.PlaySound(SoundID.Item68 with { Pitch = -0.4f, Volume = 0.6f }, startPos);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 0f);
            }

            Vector2 hoverTarget = startPos + new Vector2(0f, -260f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverTarget - NPC.Center) * 0.06f, 0.1f);
            NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.08f);

            if (StateTimer < windupDuration)
            {
                float progress = StateTimer / (float)windupDuration;
                float ringRadius = MathHelper.Lerp(650f, 500f, progress);

                for (int i = 0; i < 6; i++)
                {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 dustPos = startPos + angle.ToRotationVector2() * ringRadius;
                    Vector2 dustVel = (startPos - dustPos).SafeNormalize(Vector2.Zero) * 1.5f;

                    Dust d = Dust.NewDustPerfect(dustPos, DustID.PurpleTorch, dustVel, 0, default, 1.3f * progress);
                    d.noGravity = true;
                }
            }
            else if (StateTimer == windupDuration)
            {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.2f, Volume = 1.1f }, startPos);
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.6f, Volume = 0.7f }, startPos);
                ScreenShakeSystem.StartShakeAtPoint(startPos, 8f);
            }
            else if (StateTimer > windupDuration && StateTimer < collapseTick)
            {
                int activeTimer = (int)(StateTimer - windupDuration);
                float pullProgress = activeTimer / (float)pullDuration;

                float distToWell = Vector2.Distance(target.Center, startPos);
                if (distToWell > 40f)
                {
                    Vector2 pullDir = (startPos - target.Center).SafeNormalize(Vector2.Zero);
                    float pullStrength = MathHelper.Lerp(0.05f, 0.32f, pullProgress);
                    target.velocity += pullDir * pullStrength;
                }

                if (Main.rand.NextBool(2))
                {
                    float spiralAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    float spiralRadius = Main.rand.NextFloat(60f, 550f);
                    Vector2 spiralPos = startPos + spiralAngle.ToRotationVector2() * spiralRadius;
                    Vector2 spiralVel = (startPos - spiralPos).SafeNormalize(Vector2.Zero) * MathHelper.Lerp(1f, 4f, pullProgress);

                    Color voidColor = Color.Lerp(Color.MediumPurple, Color.Black, 0.3f);
                    Dust d = Dust.NewDustPerfect(spiralPos, DustID.Shadowflame, spiralVel, 0, voidColor, 1.1f);
                    d.noGravity = true;
                }

                int fireInterval = 9;
                if (activeTimer % fireInterval == 0)
                {
                    float ringRadius = MathHelper.Lerp(480f, 70f, pullProgress);
                    float tangentSpeed = MathHelper.Lerp(4.5f, 11f, pullProgress);
                    float ringAngleOffset = activeTimer * 0.05f;

                    int ringCount = 7;
                    SoundEngine.PlaySound(SoundID.Item12 with { Pitch = 0.1f + pullProgress * 0.3f, Volume = 0.5f }, startPos);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        for (int i = 0; i < ringCount; i++)
                        {
                            float angle = ringAngleOffset + (MathHelper.TwoPi / ringCount) * i;
                            Vector2 spawnPos = startPos + angle.ToRotationVector2() * ringRadius;
                            Vector2 tangentDir = angle.ToRotationVector2().RotatedBy(MathHelper.PiOver2);
                            Vector2 shootVel = tangentDir * tangentSpeed;

                            Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, shootVel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 3, 0f, Main.myPlayer, 0f, 3f);
                        }
                    }
                }
            }
            else if (StateTimer == collapseTick)
            {
                SoundEngine.PlaySound(SoundID.Item62 with { Volume = 1.3f, Pitch = -0.3f }, startPos);
                SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.1f }, startPos);
                ScreenShakeSystem.StartShakeAtPoint(startPos, 14f);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int burstCount = 20;
                    for (int i = 0; i < burstCount; i++)
                    {
                        float angle = MathHelper.TwoPi * i / burstCount;
                        Vector2 burstVel = angle.ToRotationVector2() * 13f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), startPos, burstVel, ModContent.ProjectileType<UnknownEntityBolt>(), NPC.damage / 3, 0f, Main.myPlayer, 0f, 3f);
                    }
                }

                for (int i = 0; i < 70; i++)
                {
                    float angle = MathHelper.TwoPi * i / 70f;
                    Vector2 ringVel = angle.ToRotationVector2() * Main.rand.NextFloat(6f, 34f);
                    Color burstColor = Color.Lerp(Color.MediumPurple, Color.Cyan, i / 70f);

                    Dust d = Dust.NewDustPerfect(startPos, DustID.RainbowTorch, ringVel, 0, burstColor, 2.6f);
                    d.noGravity = true;
                }
            }
            else if (StateTimer > collapseTick)
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
    }
}