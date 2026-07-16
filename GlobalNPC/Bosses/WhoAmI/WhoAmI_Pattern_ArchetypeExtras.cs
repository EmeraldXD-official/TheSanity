using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // ARCHETYPE EXTRAS — signature patterns for Summon, Whip, Yoyo, and Boomerang
    // ================================================================================================
    // TrueMelee/ProjMelee got Blink & Echo Combo, Ranged got Orbiting Grid Lock, Magic got Gravity
    // Well Torrent - but Summon/Whip/Yoyo/Boomerang were left with only the original 4 shared patterns
    // (see WhoAmI_Patterns.cs case 0-3 for each). This file gives each of those 4 archetypes one
    // signature pattern of their own, wired the same way as the other new patterns (see the
    // integration checklist comments in WhoAmI_Pattern_BlinkEchoCombo.cs for the pattern this follows):
    //   1. WhoAmI.cs constants: STATE_SUMMON_RIFT_SWARM=15, STATE_WHIP_LASH_CAGE=16,
    //      STATE_YOYO_TETHER_STORM=17, STATE_BOOMERANG_CROSSFIRE=18  ✔ done
    //   2. WhoAmI.cs AI() switch: 4 new cases dispatching to the 4 Handle* methods below  ✔ done
    //   3. WhoAmI_Patterns.cs: each archetype's validPatterns gains index 4, each Execute*Pattern
    //      gains `case 4:` (both phase1 and phase2 branches) entering the matching state  ✔ done
    //   4. WhoAmI_VFX_Attacks.cs: GetAttackPatternColor gains a case for each new state  ✔ done
    //
    // All 4 use the shared "sat set" primitives from WhoAmI_SatSetPhysics.cs (no static idle, snap
    // dashes, braking impulses) and reuse FireAttackProjectile() so whatever weapon the boss currently
    // has copied from the player fires here too - these are movement/timing patterns layered on top
    // of the existing weapon-copy system, not new weapons of their own.
    // ================================================================================================
    public partial class WhoAmI
    {
        // ---------------------------------------------------------------------------------------
        // SUMMON — SPECTRAL RIFT SWARM — STATE_SUMMON_RIFT_SWARM
        // ---------------------------------------------------------------------------------------
        // A) RETREAT (0-20)     - boss falls back to a safe hover spot away from the player, opening
        //                         2 small rift motes at its flanks (visual only, no new textures).
        // B) SWARM (21-130)     - periodically fires the copied summon weapon (spawns more of the
        //                         player's minion, handled by the existing homing logic in
        //                         WhoAmIProjectileGuard.PostAI) while gently drifting side to side.
        // C) COMMANDED DIVE (131-150) - every currently active copied minion projectile owned by the
        //                         boss's proxy gets an instant velocity kick straight at the player -
        //                         a "call the swarm in" moment instead of them just trickling in.
        // D) RECOVER (151-170)
        private Vector2 summonSwarmHoverSpot = Vector2.Zero;
        private int summonSwarmBoltsFired = 0;
        private const int SummonSwarmRetreatEnd = 20;
        private const int SummonSwarmPhaseEnd = 130;
        private const int SummonSwarmDiveEnd = 150;
        private const int SummonSwarmRecoverEnd = 170;

        private void HandleSummonRiftSwarm(Player target)
        {
            aiTimer++;
            NPC.damage = 0;
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (aiTimer == 1)
            {
                float side = (NPC.Center.X < target.Center.X) ? -1f : 1f;
                summonSwarmHoverSpot = target.Center + new Vector2(side * 340f, -140f);
                summonSwarmBoltsFired = 0;
            }

            if (aiTimer <= SummonSwarmRetreatEnd)
            {
                Vector2 bobbed = summonSwarmHoverSpot + GetSatSetBobOffset(1f, 16f);
                EaseVelocityTowards((bobbed - NPC.Center) * 0.3f, aiTimer / (float)SummonSwarmRetreatEnd, EasingCurves.Sine, EasingType.InOut);
                if (Main.rand.NextBool(3))
                {
                    Vector2 rift = NPC.Center + new Vector2(Main.rand.NextFloat(-30f, 30f), Main.rand.NextFloat(-20f, 20f));
                    LuminanceUtilities.SpawnParticle(rift, Vector2.Zero, new Color(255, 200, 80), 16, 0.8f, ParticleType.Spark);
                }
                return;
            }

            Vector2 hover = summonSwarmHoverSpot + GetSatSetBobOffset(0.8f, 20f);
            EaseVelocityTowards((hover - NPC.Center) * 0.2f, 1f, EasingCurves.Sine, EasingType.InOut, 0.5f);

            if (aiTimer <= SummonSwarmPhaseEnd)
            {
                int callEvery = isPhase2 ? 18 : 24;
                if ((aiTimer - SummonSwarmRetreatEnd) % callEvery == 0)
                {
                    FireAttackProjectile(target);
                    summonSwarmBoltsFired++;
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                }
                return;
            }

            if (aiTimer == SummonSwarmPhaseEnd + 1)
            {
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 8f, 0.25f);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, NPC.Center);
            }

            if (aiTimer <= SummonSwarmDiveEnd)
            {
                // "Call the swarm in" - every copied minion projectile owned by the boss gets a
                // straight-line velocity kick toward the player, instead of relying purely on the
                // usual gradual homing lerp in WhoAmIProjectileGuard.PostAI.
                if (aiTimer == SummonSwarmPhaseEnd + 2)
                {
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        Projectile p = Main.projectile[i];
                        if (!p.active || p.owner != proxySlot || !p.minion) continue;
                        Vector2 dir = target.Center - p.Center;
                        if (dir != Vector2.Zero) dir.Normalize();
                        p.velocity = Vector2.Lerp(p.velocity, dir * 14f, 0.7f);
                    }
                }
                return;
            }

            ApplyBrakingImpulse(0.2f);
            if (aiTimer >= SummonSwarmRecoverEnd)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 70 : 100;
                NPC.netUpdate = true;
            }
        }

        // ---------------------------------------------------------------------------------------
        // WHIP — LASH CAGE — STATE_WHIP_LASH_CAGE
        // ---------------------------------------------------------------------------------------
        // Boss snap-dashes between 3 flank points around the player (Left, Right, Top - same compass
        // layout idea as Mirror Mirage) in sequence, cracking the copied whip at the player from each
        // stop, so the player has to keep re-orienting to where the next crack is coming from instead
        // of the boss just standing still and swinging.
        private Vector2[] lashCageStops = new Vector2[3];
        private int lashCageStopIndex = 0;
        private const float LashCageStopRadius = 240f;
        private const int LashCageTravelTicks = 18;
        private const int LashCageCrackHoldTicks = 10;

        private void HandleWhipLashCage(Player target)
        {
            aiTimer++;
            NPC.damage = 0;

            if (aiTimer == 1)
            {
                lashCageStopIndex = 0;
                Vector2 c = target.Center;
                lashCageStops[0] = c + new Vector2(-LashCageStopRadius, 0f);
                lashCageStops[1] = c + new Vector2(LashCageStopRadius, 0f);
                lashCageStops[2] = c + new Vector2(0f, -LashCageStopRadius);
            }

            int stopCycle = LashCageTravelTicks + LashCageCrackHoldTicks;
            int localTick = (aiTimer - 1) % stopCycle;
            int stopForThisCycle = ((aiTimer - 1) / stopCycle);

            if (stopForThisCycle != lashCageStopIndex && stopForThisCycle < lashCageStops.Length)
            {
                lashCageStopIndex = stopForThisCycle;
                NPC.netUpdate = true;
            }

            if (stopForThisCycle >= lashCageStops.Length)
            {
                ApplyBrakingImpulse(0.15f);
                if (aiTimer >= lashCageStops.Length * stopCycle + 20)
                {
                    aiState = STATE_IDLE;
                    aiTimer = 0;
                    patternCooldown = isPhase2 ? 60 : 90;
                    NPC.netUpdate = true;
                }
                return;
            }

            Vector2 stopPos = lashCageStops[stopForThisCycle] + GetSatSetBobOffset(1.4f, 10f);

            if (localTick < LashCageTravelTicks)
            {
                EaseVelocityTowards((stopPos - NPC.Center) * 0.45f, localTick / (float)LashCageTravelTicks, EasingCurves.Quadratic, EasingType.Out);
            }
            else
            {
                ApplyBrakingImpulse(0.4f);
                if (localTick == LashCageTravelTicks)
                {
                    NPC.direction = (target.Center.X < NPC.Center.X) ? -1 : 1;
                    FireAttackProjectile(target);
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                    for (int i = 0; i < 8; i++)
                    {
                        Vector2 dir = Main.rand.NextVector2Circular(1f, 1f);
                        LuminanceUtilities.SpawnParticle(NPC.Center + dir * 20f, dir * 2f, new Color(255, 90, 70), 14, 0.7f, ParticleType.Spark);
                    }
                }
            }
        }

        // ---------------------------------------------------------------------------------------
        // YOYO — TETHER STORM — STATE_YOYO_TETHER_STORM
        // ---------------------------------------------------------------------------------------
        // A) FAN THROW (0-40)   - throws several copied yoyos out in a quick rotating fan instead of
        //                         one at a time, so multiple orbit the boss at once (existing
        //                         UpdateBossYoyo in WhoAmIProjectileGuard already handles each one's
        //                         independent orbit/retract lifecycle - this just throws more of them
        //                         in a burst).
        // B) ORBITAL SWEEP (41-140) - boss itself does a rapid orbital sweep around the player while
        //                         its yoyos are still out, so the player has to dodge the boss's own
        //                         movement AND the orbiting yoyo hitboxes at the same time.
        // C) RECOVER (141-160)
        private float tetherStormOrbitAngle = 0f;
        private const int TetherStormFanEnd = 40;
        private const int TetherStormSweepEnd = 140;
        private const int TetherStormRecoverEnd = 160;
        private const float TetherStormOrbitRadius = 340f;

        private void HandleYoyoTetherStorm(Player target)
        {
            aiTimer++;
            NPC.damage = 0;
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (aiTimer == 1)
                tetherStormOrbitAngle = (NPC.Center - target.Center).ToRotation();

            if (aiTimer <= TetherStormFanEnd)
            {
                int throwEvery = isPhase2 ? 8 : 12;
                if (aiTimer % throwEvery == 0)
                {
                    FireAttackProjectile(target);
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                }
                // Bob in place while throwing - "no static idle".
                Vector2 bobbed = NPC.Center + GetSatSetBobOffset(1.2f, 12f) * 0.1f;
                EaseVelocityTowards((bobbed - NPC.Center) * 0.2f, 1f, EasingCurves.Sine, EasingType.InOut, 0.4f);
                return;
            }

            if (aiTimer <= TetherStormSweepEnd)
            {
                tetherStormOrbitAngle += isPhase2 ? 0.1f : 0.075f;
                Vector2 orbitPos = GetOrbitalCrawlPosition(target.Center, TetherStormOrbitRadius, tetherStormOrbitAngle);
                EaseVelocityTowards((orbitPos - NPC.Center) * 0.35f, (aiTimer - TetherStormFanEnd) / (float)(TetherStormSweepEnd - TetherStormFanEnd), EasingCurves.Sine, EasingType.InOut);

                if (NPC.velocity.Length() > 1f && Main.rand.NextBool(3))
                    LuminanceUtilities.SpawnParticle(NPC.Center, -NPC.velocity * 0.15f, new Color(200, 200, 255), 16, 0.8f, ParticleType.Spark);
                return;
            }

            ApplyBrakingImpulse(0.15f);
            if (aiTimer >= TetherStormRecoverEnd)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 70 : 100;
                NPC.netUpdate = true;
            }
        }

        // ---------------------------------------------------------------------------------------
        // BOOMERANG — CROSSFIRE — STATE_BOOMERANG_CROSSFIRE
        // ---------------------------------------------------------------------------------------
        // Boss snap-dashes to 2 flank points either side of the player in sequence, throwing a
        // boomerang aimed THROUGH the player from each flank so the two thrown paths cross roughly
        // where the player is standing - a brief "X" of incoming boomerangs instead of a single
        // straight-line throw.
        private Vector2[] crossfireFlanks = new Vector2[2];
        private int crossfireFlankIndex = 0;
        private const float CrossfireFlankRadius = 300f;
        private const int CrossfireTravelTicks = 16;
        private const int CrossfireThrowHoldTicks = 14;

        private void HandleBoomerangCrossfire(Player target)
        {
            aiTimer++;
            NPC.damage = 0;
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (aiTimer == 1)
            {
                Vector2 c = target.Center;
                crossfireFlanks[0] = c + new Vector2(-CrossfireFlankRadius, -80f);
                crossfireFlanks[1] = c + new Vector2(CrossfireFlankRadius, -80f);
                crossfireFlankIndex = 0;
            }

            int cycle = CrossfireTravelTicks + CrossfireThrowHoldTicks;
            int localTick = (aiTimer - 1) % cycle;
            int flankForThisCycle = (aiTimer - 1) / cycle;

            if (flankForThisCycle >= crossfireFlanks.Length)
            {
                ApplyBrakingImpulse(0.15f);
                if (aiTimer >= crossfireFlanks.Length * cycle + 15)
                {
                    aiState = STATE_IDLE;
                    aiTimer = 0;
                    patternCooldown = isPhase2 ? 55 : 85;
                    NPC.netUpdate = true;
                }
                return;
            }

            crossfireFlankIndex = flankForThisCycle;
            Vector2 flankPos = crossfireFlanks[flankForThisCycle] + GetSatSetBobOffset(1.3f, 10f);

            if (localTick < CrossfireTravelTicks)
            {
                EaseVelocityTowards((flankPos - NPC.Center) * 0.45f, localTick / (float)CrossfireTravelTicks, EasingCurves.Quadratic, EasingType.Out);
            }
            else
            {
                ApplyBrakingImpulse(0.4f);
                if (localTick == CrossfireTravelTicks)
                {
                    // FireAttackProjectile always aims at the player's current position internally, so
                    // throwing from alternating flanks (this stop, then the opposite one next cycle) is
                    // what creates the crossing "X" pattern - no need to override the aim vector here.
                    NPC.direction = (target.Center.X < NPC.Center.X) ? -1 : 1;
                    FireAttackProjectile(target);
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item7, NPC.Center);
                    for (int i = 0; i < 6; i++)
                    {
                        Vector2 dir = Main.rand.NextVector2Circular(1f, 1f);
                        LuminanceUtilities.SpawnParticle(NPC.Center + dir * 16f, dir * 2f, new Color(200, 200, 255), 12, 0.65f, ParticleType.Spark);
                    }
                }
            }
        }
    }
}
