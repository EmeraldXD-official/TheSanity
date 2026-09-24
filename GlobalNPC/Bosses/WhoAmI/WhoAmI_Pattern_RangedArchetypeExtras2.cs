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
    // RANGED ARCHETYPE TRIO 2 — 3 more Ranged-exclusive attacks (STATE_RANGED_PARALLAX_VOLLEY /
    // STATE_RANGED_MIRROR_RICOCHET / STATE_RANGED_STARFALL_CONVERGENCE), wired in as indices 8/9/10
    // in WhoAmI_Patterns.cs, same convention as WhoAmI_Pattern_RangedArchetypeExtras.cs (indices 5-7).
    //
    // Throughline for this pass: the first ranged trio is built around static geometry (a fixed
    // laser grid, a single orbiting comet, one settled gravity orb). These three instead keep the
    // BOSS itself moving/relocating through the attack and use indirection (multi-vantage fire,
    // a bounce relay, staggered off-screen strikes) so the source of danger keeps shifting instead
    // of sitting in one place for the whole pattern.
    // ================================================================================================
    public partial class WhoAmI
    {
        // ============================================================================================
        // ATTACK 1: "PARALLAX VOLLEY" — STATE_RANGED_PARALLAX_VOLLEY
        // The boss hops between several vantage points around the player, firing immediately on
        // arrival at each from a fresh angle, then closes with a rapid-fire converged volley from
        // the final point. Rolls its own lightweight short-range blink (NOT ExecuteSpeedCanceledTeleport
        // - that helper enforces an 800px minimum hop distance meant for cross-arena repositioning,
        // which is longer than these vantage-to-vantage hops usually need to be).
        // ============================================================================================
        private Vector2[] parallaxVantage = new Vector2[4];
        private int parallaxLegCount = 3;
        private int parallaxLegIndex = 0;
        private int parallaxNextLegTick = 6;

        private void ResetRangedParallaxVolleyState()
        {
            parallaxLegCount = isPhase2 ? 4 : 3;
            parallaxLegIndex = 0;
            parallaxNextLegTick = 6;
        }

        private void ParallaxBlinkTo(Vector2 destination)
        {
            for (int i = 0; i < 8; i++)
                LuminanceUtilities.SpawnParticle(Vector2.Lerp(NPC.Center, destination, i / 8f), Vector2.Zero, new Color(80, 255, 180) * 0.5f, 12, 0.8f, ParticleType.Spark);
            NPC.Center = destination;
            NPC.velocity = Vector2.Zero;
            for (int i = 0; i < 10; i++)
                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(4, 4), new Color(180, 255, 220), 16, 1f, ParticleType.Spark);
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
        }

        private void HandleRangedParallaxVolley(Player target)
        {
            NPC.damage = 0;

            if (aiTimer == 1)
            {
                Vector2 anchor = GetPredictiveInterceptPoint(target, 10f);
                float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < parallaxLegCount; i++)
                {
                    float ang = baseAngle + MathHelper.TwoPi * i / parallaxLegCount + Main.rand.NextFloat(-0.4f, 0.4f);
                    float dist = Main.rand.NextFloat(300f, 460f);
                    parallaxVantage[i] = anchor + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * dist;
                }
            }

            if (parallaxLegIndex < parallaxLegCount)
            {
                if (aiTimer >= parallaxNextLegTick)
                {
                    ParallaxBlinkTo(parallaxVantage[parallaxLegIndex]);
                    Vector2 lead = GetPredictiveInterceptPoint(target, 14f) - NPC.Center;
                    if (lead != Vector2.Zero) lead.Normalize(); else lead = new Vector2(NPC.direction, 0f);
                    NPC.direction = lead.X >= 0 ? 1 : -1;
                    FireAttackProjectile(target, Main.rand.NextFloat(-0.08f, 0.08f));
                    bossWeaponSwingTimer = bossWeaponSwingMax;

                    parallaxLegIndex++;
                    int spacing = Math.Max(10, (isPhase2 ? 20 : 24) - parallaxLegIndex * 2);
                    parallaxNextLegTick = aiTimer + spacing;
                    NPC.netUpdate = true;
                }
                return;
            }

            // Converged volley from the final vantage point - the "all those angles were setting up
            // for THIS" payoff moment.
            int convergeStart = parallaxNextLegTick;
            int convergeShots = isPhase2 ? 5 : 3;
            int shotsFired = (aiTimer - convergeStart) / 5;
            if (aiTimer >= convergeStart && shotsFired < convergeShots && (aiTimer - convergeStart) % 5 == 0)
            {
                FireAttackProjectile(target, Main.rand.NextFloat(-0.05f, 0.05f));
                bossWeaponSwingTimer = bossWeaponSwingMax;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item10, NPC.Center);
            }

            if (aiTimer > convergeStart + convergeShots * 5 + 18)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 22 : 36;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 2: "MIRROR RICOCHET" — STATE_RANGED_MIRROR_RICOCHET
        // Shots don't fly straight at the player - each volley marks a "mirror point" well off the
        // direct line, sends a quick visual pulse out to it, then a real shot launches FROM that
        // point toward the player a beat later, so it arrives from a rebounded angle instead of head
        // on. Several of these fire on staggered timers so the rebound points keep changing side.
        // ============================================================================================
        private const int RicochetVolleyMax = 4;
        private Vector2[] ricochetBouncePoint = new Vector2[RicochetVolleyMax];
        private int[] ricochetTriggerTick = new int[RicochetVolleyMax];
        private bool[] ricochetArcShown = new bool[RicochetVolleyMax];
        private bool[] ricochetRelayFired = new bool[RicochetVolleyMax];
        private int ricochetVolleyCount = 3;
        private const int RicochetRelayDelay = 11;

        private void ResetRangedMirrorRicochetState()
        {
            ricochetVolleyCount = isPhase2 ? 4 : 3;
            for (int i = 0; i < RicochetVolleyMax; i++)
            {
                ricochetArcShown[i] = false;
                ricochetRelayFired[i] = false;
                ricochetTriggerTick[i] = 18 + i * (isPhase2 ? 16 : 20) + Main.rand.Next(-5, 6);
            }
        }

        private void HandleRangedMirrorRicochet(Player target)
        {
            NPC.damage = 0;
            EaseVelocityTowards((GetPredictiveInterceptPoint(target, 8f) - NPC.Center - Vector2.UnitY * 40f) * 0.04f, 1f, EasingCurves.Sine, EasingType.InOut, 0.4f);

            for (int i = 0; i < ricochetVolleyCount; i++)
            {
                if (!ricochetArcShown[i] && aiTimer >= ricochetTriggerTick[i])
                {
                    ricochetArcShown[i] = true;
                    Vector2 mid = Vector2.Lerp(NPC.Center, target.Center, 0.55f);
                    Vector2 toTarget = target.Center - NPC.Center;
                    Vector2 perp = toTarget == Vector2.Zero ? Vector2.UnitX : new Vector2(-toTarget.Y, toTarget.X);
                    perp.Normalize();
                    float side = (i % 2 == 0) ? 1f : -1f;
                    ricochetBouncePoint[i] = mid + perp * side * Main.rand.NextFloat(220f, 340f);

                    for (int p = 0; p <= 10; p++)
                    {
                        float t = p / 10f;
                        Vector2 arcPos = Vector2.Lerp(NPC.Center, ricochetBouncePoint[i], t) + perp * side * (float)Math.Sin(t * MathHelper.Pi) * 40f;
                        LuminanceUtilities.SpawnParticle(arcPos, Vector2.Zero, new Color(150, 255, 90) * 0.55f, 14, 0.8f, ParticleType.Spark);
                    }
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                    NPC.netUpdate = true;
                }

                if (ricochetArcShown[i] && !ricochetRelayFired[i] && aiTimer >= ricochetTriggerTick[i] + RicochetRelayDelay)
                {
                    ricochetRelayFired[i] = true;
                    Vector2 predicted = GetPredictiveInterceptPoint(target, 12f);
                    Vector2 dir = predicted - ricochetBouncePoint[i];
                    if (dir != Vector2.Zero) dir.Normalize(); else dir = -Vector2.UnitY;

                    int projType = ResolveMimickedProjectileType();
                    int dmg = isPhase2 ? 62 : 44;
                    float speed = isPhase2 ? 10.5f : 8f;
                    int p2 = Projectile.NewProjectile(NPC.GetSource_FromAI(), ricochetBouncePoint[i], dir * speed, projType, dmg, 0f, proxySlot);
                    if (p2 >= 0 && p2 < Main.maxProjectiles)
                    {
                        Main.projectile[p2].hostile = true;
                        Main.projectile[p2].friendly = false;
                        Main.projectile[p2].tileCollide = false;
                        Main.projectile[p2].penetrate = 1;
                        Main.projectile[p2].timeLeft = 120;
                    }
                    for (int b = 0; b < 14; b++)
                        LuminanceUtilities.SpawnParticle(ricochetBouncePoint[i], Main.rand.NextVector2Circular(4, 4), new Color(180, 255, 120), 18, 1.1f, ParticleType.Spark);
                    ScreenShakeSystem.StartShakeAtPoint(ricochetBouncePoint[i], 3f, 0.15f);
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item42, ricochetBouncePoint[i]);
                }
            }

            int lastTrigger = 0;
            for (int i = 0; i < ricochetVolleyCount; i++) lastTrigger = Math.Max(lastTrigger, ricochetTriggerTick[i]);
            if (aiTimer > lastTrigger + RicochetRelayDelay + 20)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 24 : 38;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 3: "STARFALL CONVERGENCE" — STATE_RANGED_STARFALL_CONVERGENCE
        // The boss marks several ground points around the player; each one telegraphs on its own
        // staggered timer, then a bolt drops on it from directly off-screen above. The last mark
        // always lands on the player's own position at the moment the whole pattern started, so
        // standing still through the earlier ones is never the safe option.
        // ============================================================================================
        private const int StarfallMax = 5;
        private Vector2[] starfallPoint = new Vector2[StarfallMax];
        private int[] starfallStrikeTick = new int[StarfallMax];
        private bool[] starfallStruck = new bool[StarfallMax];
        private int starfallCount = 3;

        private void ResetRangedStarfallConvergenceState()
        {
            starfallCount = isPhase2 ? 5 : 3;
            for (int i = 0; i < StarfallMax; i++) starfallStruck[i] = true; // cleared for live indices below
        }

        private void HandleRangedStarfallConvergence(Player target)
        {
            NPC.damage = 0;
            Vector2 hover = target.Center + new Vector2(0f, -260f) + GetSatSetBobOffset(1.1f, 18f);
            EaseVelocityTowards((hover - NPC.Center) * 0.05f, 1f, EasingCurves.Sine, EasingType.InOut, 0.5f);

            if (aiTimer == 1)
            {
                for (int i = 0; i < starfallCount; i++)
                {
                    starfallStruck[i] = false;
                    starfallStrikeTick[i] = 26 + i * (isPhase2 ? 13 : 17) + Main.rand.Next(-4, 5);
                    if (i < starfallCount - 1)
                        starfallPoint[i] = target.Center + new Vector2(Main.rand.NextFloat(-320f, 320f), 0f);
                }
                // Final mark always resolves onto the player's live position when it actually lands
                // (computed at strike time below), not a stale spot from the windup.
            }

            for (int i = 0; i < starfallCount; i++)
            {
                if (starfallStruck[i]) continue;
                int untilStrike = starfallStrikeTick[i] - aiTimer;

                if (untilStrike > 0 && untilStrike <= 24)
                {
                    Vector2 markPos = (i == starfallCount - 1) ? target.Center : starfallPoint[i];
                    if (aiTimer % 3 == 0)
                    {
                        for (float y = markPos.Y - 700f; y < markPos.Y; y += 55f)
                            LuminanceUtilities.SpawnParticle(new Vector2(markPos.X, y), new Vector2(0, 3f), new Color(255, 225, 150) * (0.3f + 0.4f * (1f - untilStrike / 24f)), 10, 0.7f, ParticleType.Spark);
                        LuminanceUtilities.SpawnParticle(markPos, Vector2.Zero, new Color(255, 235, 190) * 0.7f, 10, 1.2f, ParticleType.Spark);
                    }
                }

                if (aiTimer >= starfallStrikeTick[i])
                {
                    starfallStruck[i] = true;
                    Vector2 strikePos = (i == starfallCount - 1) ? target.Center : starfallPoint[i];
                    Vector2 spawnPos = strikePos + new Vector2(0f, -900f);
                    float fallSpeed = isPhase2 ? 15f : 11f;
                    int dmg = isPhase2 ? 78 : 56;
                    int projType = ResolveMimickedProjectileType();

                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, new Vector2(0f, fallSpeed), projType, dmg, 0f, proxySlot);
                    if (p >= 0 && p < Main.maxProjectiles)
                    {
                        Main.projectile[p].hostile = true;
                        Main.projectile[p].friendly = false;
                        Main.projectile[p].tileCollide = false;
                        Main.projectile[p].penetrate = 1;
                        Main.projectile[p].timeLeft = 120;
                        Main.projectile[p].rotation = MathHelper.PiOver2;
                    }
                    for (int b = 0; b < 16; b++)
                        LuminanceUtilities.SpawnParticle(strikePos, Main.rand.NextVector2Circular(3, 3), new Color(255, 225, 150), 20, 1.2f, ParticleType.Spark);
                    ScreenShakeSystem.StartShakeAtPoint(strikePos, i == starfallCount - 1 ? 8f : 4f, 0.2f);
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item20, strikePos);
                    NPC.netUpdate = true;
                }
            }

            int lastStrike = 0;
            for (int i = 0; i < starfallCount; i++) lastStrike = Math.Max(lastStrike, starfallStrikeTick[i]);
            if (aiTimer > lastStrike + 24)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 26 : 42;
                NPC.netUpdate = true;
            }
        }
    }
}
