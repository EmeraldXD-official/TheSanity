using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // YOYO + BOOMERANG TRIO 2 — 6 new attacks total (3 per class), wired in as indices 5/6/7 on both
    // WeaponArchetype.Yoyo and WeaponArchetype.Boomerang in WhoAmI_Patterns.cs. Same bundling
    // convention as WhoAmI_Pattern_ArchetypeExtras.cs.
    // ================================================================================================
    public partial class WhoAmI
    {
        // ============================================================================================
        // ATTACK 1: "PENDULUM RECKONING" — STATE_YOYO_PENDULUM_RECKONING
        // The boss hangs from a fixed overhead pivot and genuinely swings like a pendulum - real
        // sideways arc motion, amplitude pumped a little higher each pass - throwing at the bottom of
        // every swing where it's moving fastest, then lets the final, biggest swing fly as a straight
        // smash instead of another throw.
        // ============================================================================================
        private Vector2 pendulumPivot = Vector2.Zero;
        private float pendulumAmplitude = 0.5f;
        private float pendulumPrevTheta = 0f;
        private int pendulumSwingsDone = 0;
        private int pendulumSwingsTotal = 3;
        private const float PendulumArmLength = 240f;
        private const float PendulumAngularSpeed = 0.055f;

        private void ResetYoyoPendulumReckoningState()
        {
            pendulumPivot = NPC.Center;
            pendulumAmplitude = 0.45f;
            pendulumPrevTheta = 0f;
            pendulumSwingsDone = 0;
            pendulumSwingsTotal = isPhase2 ? 4 : 3;
        }

        private void HandleYoyoPendulumReckoning(Player target)
        {
            NPC.damage = 0;
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (aiTimer == 1)
                pendulumPivot = target.Center + new Vector2(0f, -300f);
            else
                pendulumPivot = Vector2.Lerp(pendulumPivot, target.Center + new Vector2(0f, -300f), 0.01f);

            if (pendulumSwingsDone < pendulumSwingsTotal)
            {
                float theta = (float)Math.Sin(aiTimer * PendulumAngularSpeed) * pendulumAmplitude;
                Vector2 armOffset = new Vector2((float)Math.Sin(theta), 1f - (float)Math.Cos(theta)) * PendulumArmLength;
                NPC.Center = pendulumPivot + armOffset;

                // A zero-crossing (sign flip) is the bottom of the arc - fastest point, throw here.
                if (Math.Sign(theta) != 0 && Math.Sign(pendulumPrevTheta) != 0 && Math.Sign(theta) != Math.Sign(pendulumPrevTheta))
                {
                    NPC.direction = theta >= pendulumPrevTheta ? 1 : -1;
                    FireAttackProjectile(target);
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                    for (int i = 0; i < 10; i++)
                        LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(3, 3), new Color(140, 170, 220), 16, 0.9f, ParticleType.Spark);
                    pendulumSwingsDone++;
                    pendulumAmplitude = Math.Min(pendulumAmplitude + 0.22f, 1.35f);
                    NPC.netUpdate = true;
                }
                pendulumPrevTheta = theta;

                if (Main.rand.NextBool(3))
                    LuminanceUtilities.SpawnParticle(NPC.Center - armOffset * 0.05f, Vector2.Zero, new Color(140, 170, 220) * 0.4f, 10, 0.6f, ParticleType.Spark);
                return;
            }

            // Final, biggest swing releases as a straight smash rather than another throw.
            float finalTheta = (float)Math.Sin(aiTimer * PendulumAngularSpeed) * pendulumAmplitude;
            Vector2 finalOffset = new Vector2((float)Math.Sin(finalTheta), 1f - (float)Math.Cos(finalTheta)) * PendulumArmLength;
            NPC.Center = pendulumPivot + finalOffset;

            bool crossedNow = Math.Sign(finalTheta) != 0 && Math.Sign(pendulumPrevTheta) != 0 && Math.Sign(finalTheta) != Math.Sign(pendulumPrevTheta);
            pendulumPrevTheta = finalTheta;
            if (crossedNow)
            {
                Vector2 dir = target.Center - NPC.Center;
                if (dir != Vector2.Zero) dir.Normalize(); else dir = new Vector2(NPC.direction, 0f);
                NPC.velocity = dir * (isPhase2 ? 20f : 15f);
                // BALANCE ("sakit banget"): finisher smash ini dulu 85/120 (19%/27% dari HP referensi
                // 450) buat satu hit - diturunin ke tier "signature hit" yang sama dipakai buat
                // finisher/dash pattern lain (~13%/19%).
                NPC.damage = isPhase2 ? 85 : 60;
                bossWeaponSwingTimer = bossWeaponSwingMax;
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 8f, 0.28f);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, NPC.Center);
                for (int i = 0; i < 20; i++)
                    LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), new Color(180, 200, 240), 20, 1.3f, ParticleType.Spark);
                NPC.netUpdate = true;
                pendulumSwingsDone++; // guard so this branch only fires once
            }

            if (pendulumSwingsDone > pendulumSwingsTotal)
            {
                NPC.damage = 0;
                ApplyBrakingImpulse(0.15f);
                if (aiTimer > 40 + pendulumSwingsTotal * (int)(2 * Math.PI / PendulumAngularSpeed))
                {
                    aiState = STATE_IDLE;
                    aiTimer = 0;
                    patternCooldown = isPhase2 ? 60 : 90;
                    NPC.netUpdate = true;
                }
            }
        }

        // ============================================================================================
        // ATTACK 2: "BINARY ORBIT SNARE" — STATE_YOYO_BINARY_SNARE
        // Two yoyos swing around the boss itself in a tight binary orbit, their shared radius pulsing
        // out and back like a real yo-yo string paying out - each time they hit full extension a hit
        // snaps out from wherever they are, so the danger zone keeps rotating around the boss.
        // ============================================================================================
        private int binarySnareNextPulseTick = 20;

        private void ResetYoyoBinarySnareState()
        {
            binarySnareNextPulseTick = 20;
        }

        private void HandleYoyoBinarySnare(Player target)
        {
            NPC.damage = 0;
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            Vector2 hover = target.Center + new Vector2(0f, -160f);
            EaseVelocityTowards((hover - NPC.Center) * 0.03f, 1f, EasingCurves.Sine, EasingType.InOut, 0.4f);

            float spin = aiTimer * (isPhase2 ? 0.1f : 0.075f);
            float pulsePhase = aiTimer * (isPhase2 ? 0.09f : 0.065f);
            float radius = MathHelper.Lerp(60f, isPhase2 ? 210f : 175f, 0.5f + 0.5f * (float)Math.Sin(pulsePhase));
            Vector2 offset = new Vector2((float)Math.Cos(spin), (float)Math.Sin(spin)) * radius;
            Vector2 moteA = NPC.Center + offset;
            Vector2 moteB = NPC.Center - offset;

            LuminanceUtilities.SpawnParticle(moteA, Vector2.Zero, new Color(140, 170, 220), 12, 0.85f, ParticleType.Spark);
            LuminanceUtilities.SpawnParticle(moteB, Vector2.Zero, new Color(200, 220, 255), 12, 0.85f, ParticleType.Spark);
            if (aiTimer % 5 == 0)
            {
                LuminanceUtilities.SpawnParticle(Vector2.Lerp(NPC.Center, moteA, 0.5f), Vector2.Zero, new Color(140, 170, 220) * 0.35f, 8, 0.5f, ParticleType.Spark);
                LuminanceUtilities.SpawnParticle(Vector2.Lerp(NPC.Center, moteB, 0.5f), Vector2.Zero, new Color(200, 220, 255) * 0.35f, 8, 0.5f, ParticleType.Spark);
            }

            if (aiTimer >= binarySnareNextPulseTick && radius > (isPhase2 ? 190f : 155f))
            {
                foreach (Vector2 mote in new[] { moteA, moteB })
                {
                    Vector2 dir = target.Center - mote;
                    if (dir != Vector2.Zero) dir.Normalize(); else dir = -Vector2.UnitY;
                    int dmg = isPhase2 ? 48 : 34;
                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), mote, dir * (isPhase2 ? 7f : 5.2f), GetWeaponProjectileType(activeWeapon), dmg, 0f, proxySlot);
                    if (p >= 0 && p < Main.maxProjectiles)
                    {
                        Main.projectile[p].hostile = true;
                        Main.projectile[p].friendly = false;
                        Main.projectile[p].tileCollide = false;
                        Main.projectile[p].timeLeft = 70;
                    }
                    for (int i = 0; i < 8; i++)
                        LuminanceUtilities.SpawnParticle(mote, dir * 2f, new Color(180, 200, 240), 14, 0.9f, ParticleType.Spark);
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                binarySnareNextPulseTick = aiTimer + (isPhase2 ? 24 : 32);
                NPC.netUpdate = true;
            }

            int totalDuration = isPhase2 ? 140 : 110;
            if (aiTimer > totalDuration)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 58 : 88;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 3: "CASCADE UNRAVEL" — STATE_YOYO_CASCADE_UNRAVEL
        // The boss itself sweeps along a sine wave across the player's space, throwing at a steady
        // beat as it goes - since every throw is aimed live and fired from wherever the boss currently
        // is on that wave, the thrown yoyos end up tracing the same wave shape a beat behind the boss.
        // ============================================================================================
        private float cascadeUnravelPhaseOffset = 0f;
        private int cascadeUnravelNextThrowTick = 8;

        private void ResetYoyoCascadeUnravelState()
        {
            cascadeUnravelPhaseOffset = Main.rand.NextFloat(MathHelper.TwoPi);
            cascadeUnravelNextThrowTick = 8;
        }

        private void HandleYoyoCascadeUnravel(Player target)
        {
            NPC.damage = 0;
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            float wave = (float)Math.Sin(aiTimer * (isPhase2 ? 0.05f : 0.038f) + cascadeUnravelPhaseOffset);
            Vector2 sweepPos = target.Center + new Vector2(wave * (isPhase2 ? 360f : 300f), -190f + wave * 40f);
            EaseVelocityTowards((sweepPos - NPC.Center) * 0.12f, 1f, EasingCurves.Sine, EasingType.InOut, 0.5f);

            int throwInterval = isPhase2 ? 10 : 14;
            if (aiTimer >= cascadeUnravelNextThrowTick)
            {
                NPC.direction = (target.Center.X < NPC.Center.X) ? -1 : 1;
                FireAttackProjectile(target, Main.rand.NextFloat(-0.12f, 0.12f));
                bossWeaponSwingTimer = bossWeaponSwingMax;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                for (int i = 0; i < 8; i++)
                    LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(3, 3), new Color(100, 220, 255), 14, 0.85f, ParticleType.Spark);
                cascadeUnravelNextThrowTick = aiTimer + throwInterval;
                NPC.netUpdate = true;
            }

            int totalDuration = isPhase2 ? 150 : 120;
            if (aiTimer > totalDuration)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 58 : 88;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 1 (BOOMERANG): "WINDMILL BARRAGE" — STATE_BOOMERANG_WINDMILL_BARRAGE
        // Boss plants at one point and throws in a continuously spinning fan - not a single sweep like
        // Cracked Fan Lash's whip cousin, a full windmill that keeps spinning faster the longer it
        // runs - instead of Crossfire's relocate-and-throw positioning.
        // ============================================================================================
        private float windmillAngle = 0f;
        private int windmillNextThrowTick = 10;

        private void ResetBoomerangWindmillBarrageState()
        {
            windmillAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            windmillNextThrowTick = 10;
        }

        private void HandleBoomerangWindmillBarrage(Player target)
        {
            NPC.damage = 0;
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (aiTimer <= 16)
            {
                Vector2 anchor = target.Center + new Vector2(0f, -180f);
                EaseVelocityTowards((anchor - NPC.Center) * 0.35f, aiTimer / 16f, EasingCurves.Sine, EasingType.InOut);
            }
            else
            {
                ApplyBrakingImpulse(0.3f);
            }

            int totalDuration = isPhase2 ? 150 : 115;
            if (aiTimer > 16 && aiTimer <= totalDuration)
            {
                float progress = (aiTimer - 16) / (float)(totalDuration - 16);
                float spinSpeed = MathHelper.Lerp(isPhase2 ? 0.09f : 0.07f, isPhase2 ? 0.26f : 0.19f, progress * progress);
                windmillAngle += spinSpeed;

                int throwInterval = Math.Max(4, (int)MathHelper.Lerp(14, 5, progress));
                if (aiTimer >= windmillNextThrowTick)
                {
                    Vector2 dir = new Vector2((float)Math.Cos(windmillAngle), (float)Math.Sin(windmillAngle));
                    NPC.direction = dir.X >= 0 ? 1 : -1;
                    float angleOffset = dir.ToRotation() - (target.Center - NPC.Center).ToRotation();
                    FireAttackProjectile(target, angleOffset);
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item7, NPC.Center);
                    for (int i = 0; i < 6; i++)
                        LuminanceUtilities.SpawnParticle(NPC.Center + dir * 24f, dir * 2f, new Color(230, 180, 80), 12, 0.8f, ParticleType.Spark);
                    windmillNextThrowTick = aiTimer + throwInterval;
                    NPC.netUpdate = true;
                }
            }

            if (aiTimer > totalDuration + 16)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 55 : 85;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 2 (BOOMERANG): "RICOCHET TRIANGLE" — STATE_BOOMERANG_RICOCHET_TRIANGLE
        // One throw, three redirects - a relay of quick spawns marks a triangle around the player and
        // hands off leg to leg (same relay trick as Mirror Ricochet) before the final leg comes home
        // through the player, instead of two separate boomerangs crossing like Crossfire.
        // ============================================================================================
        private Vector2[] ricochetTriLegs = new Vector2[3];
        private int ricochetTriLegIndex = 0;
        private int ricochetTriNextLegTick = 14;

        private void ResetBoomerangRicochetTriangleState()
        {
            ricochetTriLegIndex = 0;
            ricochetTriNextLegTick = 14;
        }

        private void HandleBoomerangRicochetTriangle(Player target)
        {
            NPC.damage = 0;
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (aiTimer == 1)
            {
                float baseAngle = (target.Center - NPC.Center).ToRotation();
                for (int i = 0; i < 3; i++)
                {
                    float ang = baseAngle + MathHelper.TwoPi * (i + 0.5f) / 3f;
                    ricochetTriLegs[i] = target.Center + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 260f;
                }
            }

            if (ricochetTriLegIndex < 3 && aiTimer >= ricochetTriNextLegTick)
            {
                Vector2 origin = ricochetTriLegIndex == 0 ? NPC.Center : ricochetTriLegs[ricochetTriLegIndex - 1];
                Vector2 legTarget = ricochetTriLegs[ricochetTriLegIndex];
                bool isFinalLeg = ricochetTriLegIndex == 2;
                Vector2 aimAt = isFinalLeg ? target.Center : legTarget;

                Vector2 dir = aimAt - origin;
                if (dir != Vector2.Zero) dir.Normalize(); else dir = -Vector2.UnitY;
                int dmg = isPhase2 ? 55 : 40;
                float speed = isPhase2 ? 10f : 7.5f;
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), origin, dir * speed, GetWeaponProjectileType(activeWeapon), dmg, 0f, proxySlot);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                    Main.projectile[p].tileCollide = false;
                    Main.projectile[p].timeLeft = 50;
                }
                for (int i = 0; i < 12; i++)
                    LuminanceUtilities.SpawnParticle(origin, Main.rand.NextVector2Circular(4, 4), new Color(230, 235, 255), 16, 1f, ParticleType.Spark);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item8, origin);

                ricochetTriLegIndex++;
                ricochetTriNextLegTick = aiTimer + (isPhase2 ? 10 : 13);
                NPC.netUpdate = true;
            }

            if (ricochetTriLegIndex >= 3 && aiTimer > ricochetTriNextLegTick + 18)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 55 : 85;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 3 (BOOMERANG): "CURVING RETURN BARRAGE" — STATE_BOOMERANG_CURVING_RETURN
        // Thrown blades that genuinely curve out one way and home back the other, like an actual
        // boomerang flight arc rather than a straight there-and-back - each one threatens twice, once
        // outbound and once curving home. Uses the same self-managed tracked-projectile-list curl
        // technique as Fracture Bloom (WhoAmI_Pattern_MagicArchetypeExtras2.cs).
        // ============================================================================================
        private class CurvingBlade
        {
            public int Index;
            public float CurlSign;
            public bool Returning;
        }
        private readonly List<CurvingBlade> curvingBlades = new List<CurvingBlade>();
        private int curvingReturnNextThrowTick = 6;
        private int curvingReturnThrowsDone = 0;
        private int curvingReturnThrowsTotal = 3;

        private void ResetBoomerangCurvingReturnState()
        {
            foreach (var b in curvingBlades)
                if (b.Index >= 0 && b.Index < Main.maxProjectiles && Main.projectile[b.Index].active)
                    Main.projectile[b.Index].Kill();
            curvingBlades.Clear();
            curvingReturnNextThrowTick = 6;
            curvingReturnThrowsDone = 0;
            curvingReturnThrowsTotal = isPhase2 ? 4 : 3;
        }

        private void HandleBoomerangCurvingReturn(Player target)
        {
            NPC.damage = 0;
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            NPC.velocity *= 0.9f;
            NPC.Center += GetSatSetBobOffset(1.1f, 8f) * 0.05f;

            if (curvingReturnThrowsDone < curvingReturnThrowsTotal && aiTimer >= curvingReturnNextThrowTick)
            {
                Vector2 aimBase = target.Center - NPC.Center;
                if (aimBase == Vector2.Zero) aimBase = new Vector2(NPC.direction, 0f);
                aimBase.Normalize();
                float side = (curvingReturnThrowsDone % 2 == 0) ? 1f : -1f;
                Vector2 outboundDir = Vector2.Transform(aimBase, Matrix.CreateRotationZ(0.5f * side));

                int dmg = isPhase2 ? 46 : 32;
                float speed = isPhase2 ? 8.5f : 6.5f;
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, outboundDir * speed, GetWeaponProjectileType(activeWeapon), dmg, 0f, proxySlot);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                    Main.projectile[p].tileCollide = false;
                    Main.projectile[p].penetrate = 1;
                    Main.projectile[p].timeLeft = 100;
                    curvingBlades.Add(new CurvingBlade { Index = p, CurlSign = -side, Returning = false });
                }
                bossWeaponSwingTimer = bossWeaponSwingMax;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item7, NPC.Center);
                curvingReturnThrowsDone++;
                curvingReturnNextThrowTick = aiTimer + (isPhase2 ? 16 : 22);
                NPC.netUpdate = true;
            }

            float curlRate = isPhase2 ? 0.05f : 0.038f;
            float homeTurnRate = isPhase2 ? 0.075f : 0.05f;
            float homeSpeed = isPhase2 ? 9f : 7f;
            foreach (var blade in curvingBlades)
            {
                if (blade.Index < 0 || blade.Index >= Main.maxProjectiles || !Main.projectile[blade.Index].active) continue;
                Projectile proj = Main.projectile[blade.Index];

                if (!blade.Returning)
                {
                    proj.velocity = Vector2.Transform(proj.velocity, Matrix.CreateRotationZ(curlRate * blade.CurlSign));
                    proj.rotation += 0.3f * blade.CurlSign;
                    if (proj.timeLeft < 70) blade.Returning = true;
                }
                else
                {
                    Vector2 toTarget = target.Center - proj.Center;
                    if (toTarget != Vector2.Zero)
                    {
                        toTarget.Normalize();
                        proj.velocity = Vector2.Lerp(proj.velocity, toTarget * homeSpeed, homeTurnRate);
                    }
                    proj.rotation += 0.3f * blade.CurlSign;
                }

                if (aiTimer % 4 == 0)
                    LuminanceUtilities.SpawnParticle(proj.Center, Vector2.Zero, new Color(140, 120, 255) * 0.6f, 12, 0.7f, ParticleType.Spark);
            }

            if (curvingReturnThrowsDone >= curvingReturnThrowsTotal && aiTimer > curvingReturnNextThrowTick + 90)
            {
                curvingBlades.Clear();
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 55 : 85;
                NPC.netUpdate = true;
            }
        }
    }
}