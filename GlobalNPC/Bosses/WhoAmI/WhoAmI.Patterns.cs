using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    public partial class WhoAmI
    {
        // ======================== PATTERN SELECTION ========================
        private void SelectAndExecuteArchetypePattern(Player target)
        {
            float dist = Vector2.Distance(NPC.Center, target.Center);
            bool isClose = dist < 350f;
            bool isFar = dist > 700f;
            bool playerAggressive = playerAggressionScore > 30f;

            bool hasProjectile = WeaponHasProjectile(activeWeapon);

            int preferredPattern = 0;
            List<int> validPatterns = new List<int>();

            switch (currentArchetype)
            {
                case WeaponArchetype.TrueMelee:
                    // index 4 = Blink & Echo Combo (STATE_BLINK_ECHO_COMBO)
                    // index 5 = Abyssal Cleave & Fractured Space (STATE_ABYSSAL_CLEAVE)
                    // index 6 = Orbiting Blade Ring / Sovereign Guard (STATE_ORBITING_BLADE_RING)
                    // index 7 = Dimensional Pierce / Flash Strike (STATE_DIMENSIONAL_PIERCE)
                    // index 8 = Warped Mirror Waltz (STATE_MELEE_MIRROR_WALTZ)
                    // index 9 = Fractured Persona Onslaught (STATE_MELEE_FRACTURED_ONSLAUGHT)
                    // index 10 = Riposte Cascade (STATE_MELEE_RIPOSTE_CASCADE)
                    validPatterns = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
                    break;
                case WeaponArchetype.ProjMelee:
                    // index 4 = Blink & Echo Combo (STATE_BLINK_ECHO_COMBO)
                    // index 5-7 = same melee archetype trio as TrueMelee (see above) - these read
                    // the player's weapon type for the mimicked slash/blade art but don't require
                    // a projectile, so they stay available even when !hasProjectile.
                    // index 8-10 = second melee trio (see above) - same convention, also available
                    // without a projectile (Mirror Waltz/Fractured Onslaught/Riposte Cascade all use
                    // contact slashes + puppeted blade props, not a fire-and-forget projectile).
                    validPatterns = hasProjectile ? new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 } : new List<int> { 1, 5, 6, 7, 8, 9, 10 };
                    break;
                case WeaponArchetype.Ranged:
                    // index 4 = Orbiting Grid Lock (STATE_ORBIT_GRID_LOCK)
                    // index 5 = Vector Laser Grid System (STATE_VECTOR_LASER_GRID)
                    // index 6 = Homing Cluster Comet (STATE_HOMING_CLUSTER_COMET)
                    // index 7 = Singularity Overdrive (STATE_SINGULARITY_OVERDRIVE)
                    // index 8 = Parallax Volley (STATE_RANGED_PARALLAX_VOLLEY)
                    // index 9 = Mirror Ricochet (STATE_RANGED_MIRROR_RICOCHET)
                    // index 10 = Starfall Convergence (STATE_RANGED_STARFALL_CONVERGENCE)
                    // All need a real ranged projectile to mimic, same as the rest of this pool.
                    validPatterns = hasProjectile ? new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 } : new List<int> { 3 };
                    break;
                case WeaponArchetype.Magic:
                    // index 4 = existing Phantom Mirage Cascade (STATE_MAGIC_SPIRAL_RIFT)
                    // index 5 = new Gravity Well & Arcane Torrent (STATE_GRAVITY_WELL_TORRENT)
                    // index 6 = Aureola Signet Rain (STATE_AUREOLA_SIGNET_RAIN)
                    // index 7 = Double Helix Sweep (STATE_DOUBLE_HELIX_SWEEP)
                    // index 8 = Quantum Glitch Phasing (STATE_QUANTUM_GLITCH_PHASING)
                    // index 9 = Fracture Bloom (STATE_MAGIC_FRACTURE_BLOOM)
                    // index 10 = Umbral Duality (STATE_MAGIC_UMBRAL_DUALITY)
                    // index 11 = Paradox Mirror Volley (STATE_MAGIC_PARADOX_MIRROR)
                    validPatterns = hasProjectile ? new List<int> { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 } : new List<int> { 3 };
                    break;
                case WeaponArchetype.Summon:
                    // index 4 = new Spectral Rift Swarm (STATE_SUMMON_RIFT_SWARM)
                    // index 5 = Wraith Convergence (STATE_SUMMON_WRAITH_CONVERGENCE)
                    // index 6 = Soul Tether Bind (STATE_SUMMON_SOUL_TETHER)
                    // index 7 = Spectral Carousel (STATE_SUMMON_SPECTRAL_CAROUSEL)
                    validPatterns = hasProjectile ? new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 } : new List<int> { 1 };
                    break;
                case WeaponArchetype.Whip:
                    // index 4 = new Lash Cage (STATE_WHIP_LASH_CAGE)
                    // index 5 = Serpent's Coil (STATE_WHIP_SERPENTS_COIL)
                    // index 6 = Cracked Fan Lash (STATE_WHIP_FAN_LASH)
                    // index 7 = Puppeteer's Snap (STATE_WHIP_PUPPETEER_SNAP)
                    validPatterns = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
                    break;
                case WeaponArchetype.Yoyo:
                    // index 4 = new Tether Storm (STATE_YOYO_TETHER_STORM)
                    // index 5 = Pendulum Reckoning (STATE_YOYO_PENDULUM_RECKONING)
                    // index 6 = Binary Orbit Snare (STATE_YOYO_BINARY_SNARE)
                    // index 7 = Cascade Unravel (STATE_YOYO_CASCADE_UNRAVEL)
                    validPatterns = hasProjectile ? new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 } : new List<int> { 3 };
                    break;
                case WeaponArchetype.Boomerang:
                    // index 4 = new Crossfire (STATE_BOOMERANG_CROSSFIRE)
                    // index 5 = Windmill Barrage (STATE_BOOMERANG_WINDMILL_BARRAGE)
                    // index 6 = Ricochet Triangle (STATE_BOOMERANG_RICOCHET_TRIANGLE)
                    // index 7 = Curving Return Barrage (STATE_BOOMERANG_CURVING_RETURN)
                    validPatterns = hasProjectile ? new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 } : new List<int> { 0 };
                    break;
                default:
                    validPatterns = new List<int> { 0 };
                    break;
            }

            if (validPatterns.Count == 0)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = 10;
                NPC.netUpdate = true;
                return;
            }

            if (isClose && (currentArchetype == WeaponArchetype.TrueMelee || currentArchetype == WeaponArchetype.ProjMelee))
                preferredPattern = GetDeterministicRandom(0, 2);
            else if (isFar && (currentArchetype == WeaponArchetype.Ranged || currentArchetype == WeaponArchetype.Magic))
                preferredPattern = GetDeterministicRandom(0, 2);
            else if (currentArchetype == WeaponArchetype.Whip)
                preferredPattern = isClose ? GetDeterministicRandom(0, 2) : GetDeterministicRandom(2, 4);
            else
                preferredPattern = validPatterns[GetDeterministicRandom(0, validPatterns.Count)];

            // FIX ("projectile dari senjatanya ilang pas ganti pattern"): this function gets called
            // again on every tick that patternCooldown <= 0 (see the STATE_IDLE case in WhoAmI.cs),
            // which for the "inline" patterns below (index 0-3 - the ones that stay parked in
            // STATE_IDLE for their whole run instead of switching to a dedicated state, sometimes
            // up to 150 ticks) is true for almost their entire runtime, not just once at the start.
            // Without also checking archetypePatternTimer (which stays > 0 for the full committed
            // duration of the current pattern "session"), this roll could fire mid-barrage/mid-combo
            // and yank the boss into STATE_PARRY_STANCE, abandoning whatever projectile/combo it had
            // just fired - same root cause as the Mirror Mirage/Mirror Lance fix in WhoAmI.cs and the
            // weapon-carousel fix in WhoAmI_Helpers.cs (see those comments for the full explanation).
            if (archetypePatternTimer <= 0 && playerAggressive && isClose && parryCooldownTimer == 0 && GetDeterministicRandom(0, 100) < 35)
            {
                aiState = STATE_PARRY_STANCE;
                aiTimer = 0;
                patternCooldown = 40;
                parryCooldownTimer = isPhase2 ? 750 : 900;
                NPC.netUpdate = true;
                return;
            }

            if (archetypePatternTimer <= 0)
            {
                // Let a pattern repeat back-to-back for a few "sessions" - reads like the boss
                // committing to a combo - instead of forcing a different index every single time.
                // archetypePatternStreak counts how many sessions in a row the CURRENT index has
                // run; once it hits archetypePatternStreakLimit (rolled fresh to 3 or 4 whenever the
                // pattern actually changes), only THEN do we force a reroll away from it. Below that
                // limit, preferredPattern is allowed through as-is, same-index-as-before included.
                int newPattern = preferredPattern;
                bool mustSwitch = archetypePatternStreak >= archetypePatternStreakLimit && validPatterns.Count > 1;

                if (mustSwitch)
                {
                    int attempts = 0;
                    while (newPattern == archetypePatternIndex && attempts < 5)
                    {
                        newPattern = validPatterns[GetDeterministicRandom(0, validPatterns.Count)];
                        attempts++;
                    }
                }

                if (newPattern == archetypePatternIndex)
                {
                    archetypePatternStreak++;
                }
                else
                {
                    archetypePatternIndex = newPattern;
                    archetypePatternStreak = 1;
                    archetypePatternStreakLimit = GetDeterministicRandom(3, 5); // rolls 3 or 4
                }

                archetypePatternTimer = isPhase2 ? 100 : 150;
                patternCooldown = isPhase2 ? 25 : 45;
                // FIX ("pattern lawas suka keluar aneh/instan-abis"): the "legacy" inline patterns
                // (the ones that never switch aiState away from STATE_IDLE - e.g. pattern index 2/3
                // on nearly every archetype below, sometimes 0/1/2 too) time themselves purely off
                // aiTimer (aiTimer==0 teleport-in checks, aiTimer % N burst cadence, aiTimer > M exit)
                // as if it were "ticks since THIS pattern started". It isn't: aiTimer is a single
                // shared counter that only gets reset to 0 by a dedicated state's own entry code (see
                // e.g. HandleBlinkEchoCombo) or when returning to STATE_IDLE - NOT when this function
                // picks a new archetypePatternIndex while staying in STATE_IDLE the whole time.
                // archetypePatternTimer keeps counting down in the background even while a totally
                // unrelated dedicated-state pattern is running (WhoAmI.cs ticks it unconditionally),
                // so by the time a fresh index lands here aiTimer is almost always already large and
                // unrelated to this pattern's own timeline - its `aiTimer == 0` teleport-in openers
                // silently never fire (skipping straight into the mid-pattern branches instead), its
                // `aiTimer % N` bursts land on whatever phase aiTimer happens to be at instead of a
                // clean cadence, and its `aiTimer > exitTick` cutoff can trip almost immediately,
                // ending the "attack" after a couple of frames. Resetting aiTimer here - once, exactly
                // when a genuinely new pattern index is chosen - gives every archetype's Execute*Pattern
                // switch the same clean "aiTimer starts at 0 this tick" guarantee the newer dedicated-
                // state patterns already get from their own `aiState != STATE_X` entry checks (which
                // still set aiTimer = 0 themselves right after this, redundantly but harmlessly).
                aiTimer = 0;
                NPC.netUpdate = true;
            }

            switch (currentArchetype)
            {
                case WeaponArchetype.TrueMelee:
                    ExecuteTrueMeleePattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.ProjMelee:
                    ExecuteProjMeleePattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.Ranged:
                    ExecuteRangedPattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.Magic:
                    ExecuteMagicPattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.Summon:
                    ExecuteSummonPattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.Whip:
                    ExecuteWhipPattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.Yoyo:
                    ExecuteYoyoPattern(target, archetypePatternIndex);
                    break;
                case WeaponArchetype.Boomerang:
                    ExecuteBoomerangPattern(target, archetypePatternIndex);
                    break;
                default:
                    aiState = STATE_RANGED_BARRAGE;
                    aiTimer = 0;
                    break;
            }
        }

        // ---------- TrueMelee ----------
        private void ExecuteTrueMeleePattern(Player target, int pattern)
        {
            if (isPhase2)
            {
                // Phase 2 patterns: lebih agresif, lebih banyak slash, dash, dan efek
                switch (pattern)
                {
                    case 0:
                        // Kombo cepat 5 pukulan dengan lompatan
                        if (aiState != STATE_MELEE_COMBO)
                        {
                            aiState = STATE_MELEE_COMBO;
                            aiTimer = 0;
                            meleeComboStep = 0;
                            NPC.velocity *= 0.3f; // soft-stop, bukan snap ke nol - biar transisi dari gerakan sebelumnya kerasa nyambung
                            for (int i = 0; i < 20; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), Color.OrangeRed, 25, 1.5f, ParticleType.Spark);
                            NPC.netUpdate = true;
                        }
                        break;
                    case 1:
                        // Dash ganda + kombo
                        if (aiState != STATE_DASH_ATTACK && dashAttackCooldownTimer == 0)
                        {
                            aiState = STATE_DASH_ATTACK;
                            aiTimer = 0;
                            for (int i = 0; i < 30; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(8, 8), Color.Cyan, 30, 2f, ParticleType.Spark);
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                            NPC.netUpdate = true;
                        }
                        else if (aiState == STATE_IDLE)
                        {
                            aiState = STATE_MELEE_COMBO;
                            aiTimer = 0;
                            meleeComboStep = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 2:
                        // Putaran 8 slash + ledakan besar
                        if (aiTimer % 15 == 0 && aiTimer < 120)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                float ang = i * (MathHelper.Pi / 4f) + aiTimer * 0.03f;
                                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                                SpawnMeleeSlash(target, dir.ToRotation());
                                LuminanceUtilities.SpawnParticle(NPC.Center + dir * 80f, dir * 3f, Color.Gold, 20, 1.2f, ParticleType.Spark);
                                if (Main.rand.NextBool(2))
                                    LuminanceUtilities.SpawnParticle(NPC.Center + dir * 50f, dir.RotatedBy(1.5f) * 2f, Color.OrangeRed, 25, 1.5f, ParticleType.Spark);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
                        }
                        if (aiTimer > 140) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        // Teleport + dash ganda + ledakan
                        if (aiTimer == 0)
                        {
                            ExecuteGlitchTeleport(target);
                            aiTimer = 1;
                            for (int i = 0; i < 30; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(6, 6), Color.Magenta, 35, 1.5f, ParticleType.Spark);
                        }
                        else if (aiTimer < 25)
                        {
                            Vector2 toTarget = target.Center - NPC.Center;
                            if (toTarget != Vector2.Zero) toTarget.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 20f, 0.2f);
                            if (aiTimer % 8 == 0) SpawnMeleeSlash(target, 0f);
                        }
                        else if (aiTimer == 25)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            NPC.velocity = dir * 30f;
                            for (int i = 0; i < 20; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), Color.Cyan, 25, 2f, ParticleType.Spark);
                            SpawnMeleeSlash(target, 0f);
                            SpawnMeleeSlash(target, 0.8f);
                            SpawnMeleeSlash(target, -0.8f);
                            SpawnMeleeSlash(target, 1.6f);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        else if (aiTimer > 35)
                        {
                            aiState = STATE_IDLE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 4:
                        // NEW: Blink & Echo Combo - see WhoAmI_Pattern_BlinkEchoCombo.cs
                        if (aiState != STATE_BLINK_ECHO_COMBO)
                        {
                            aiState = STATE_BLINK_ECHO_COMBO;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW: Abyssal Cleave & Fractured Space - see WhoAmI_Pattern_MeleeArchetypeExtras.cs
                        if (aiState != STATE_ABYSSAL_CLEAVE)
                        {
                            aiState = STATE_ABYSSAL_CLEAVE;
                            aiTimer = 0;
                            ResetAbyssalCleaveState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW: Orbiting Blade Ring (Sovereign Guard) - see WhoAmI_Pattern_MeleeArchetypeExtras.cs
                        if (aiState != STATE_ORBITING_BLADE_RING)
                        {
                            aiState = STATE_ORBITING_BLADE_RING;
                            aiTimer = 0;
                            ResetOrbitingBladeRingState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW: Dimensional Pierce / Flash Strike - see WhoAmI_Pattern_MeleeArchetypeExtras.cs
                        if (aiState != STATE_DIMENSIONAL_PIERCE)
                        {
                            aiState = STATE_DIMENSIONAL_PIERCE;
                            aiTimer = 0;
                            ResetDimensionalPierceState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 8:
                        // NEW WAVE 2: Warped Mirror Waltz - see WhoAmI_Pattern_MeleeArchetypeExtras2.cs
                        if (aiState != STATE_MELEE_MIRROR_WALTZ)
                        {
                            aiState = STATE_MELEE_MIRROR_WALTZ;
                            aiTimer = 0;
                            ResetMeleeMirrorWaltzState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 9:
                        // NEW WAVE 2: Fractured Persona Onslaught - see WhoAmI_Pattern_MeleeArchetypeExtras2.cs
                        if (aiState != STATE_MELEE_FRACTURED_ONSLAUGHT)
                        {
                            aiState = STATE_MELEE_FRACTURED_ONSLAUGHT;
                            aiTimer = 0;
                            ResetMeleeFracturedOnslaughtState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 10:
                        // NEW WAVE 2: Riposte Cascade - see WhoAmI_Pattern_MeleeArchetypeExtras2.cs
                        if (aiState != STATE_MELEE_RIPOSTE_CASCADE)
                        {
                            aiState = STATE_MELEE_RIPOSTE_CASCADE;
                            aiTimer = 0;
                            ResetMeleeRiposteCascadeState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
            else
            {
                // Phase 1 patterns (existing)
                switch (pattern)
                {
                    case 0:
                        if (aiState != STATE_MELEE_COMBO)
                        {
                            aiState = STATE_MELEE_COMBO;
                            aiTimer = 0;
                            meleeComboStep = 0;
                            NPC.velocity *= 0.3f; // soft-stop, bukan snap ke nol - biar transisi dari gerakan sebelumnya kerasa nyambung
                            for (int i = 0; i < 10; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(3, 3), Color.Orange, 20, 1.2f, ParticleType.Spark);
                            NPC.netUpdate = true;
                        }
                        break;
                    case 1:
                        if (aiState != STATE_DASH_ATTACK && dashAttackCooldownTimer == 0)
                        {
                            aiState = STATE_DASH_ATTACK;
                            aiTimer = 0;
                            for (int i = 0; i < 18; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), Color.Cyan, 28, 1.6f, ParticleType.Spark);
                            for (int i = 0; i < 8; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(2, 2), Color.OrangeRed, 22, 1.1f, ParticleType.Spark);
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                            NPC.netUpdate = true;
                        }
                        else if (aiState == STATE_IDLE)
                        {
                            aiState = STATE_MELEE_COMBO;
                            aiTimer = 0;
                            meleeComboStep = 0;
                            NPC.velocity *= 0.3f; // soft-stop, bukan snap ke nol - biar transisi dari gerakan sebelumnya kerasa nyambung
                            NPC.netUpdate = true;
                        }
                        break;
                    case 2:
                        if (aiTimer % 20 == 0 && aiTimer < 100)
                        {
                            for (int i = 0; i < 6; i++)
                            {
                                float ang = i * (MathHelper.Pi / 3f) + aiTimer * 0.02f;
                                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                                SpawnMeleeSlash(target, dir.ToRotation());
                                LuminanceUtilities.SpawnParticle(NPC.Center + dir * 60f, dir * 2f, Color.Gold, 15, 0.8f, ParticleType.Spark);
                                if (Main.rand.NextBool(3))
                                    LuminanceUtilities.SpawnParticle(NPC.Center + dir * 40f, dir.RotatedBy(1.2f) * 1.5f, Color.Orange, 18, 1.1f, ParticleType.Spark);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0)
                        {
                            ExecuteGlitchTeleport(target);
                            aiTimer = 1;
                            for (int i = 0; i < 20; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), Color.Magenta, 30, 1.2f, ParticleType.Spark);
                        }
                        else if (aiTimer < 30)
                        {
                            Vector2 toTarget = target.Center - NPC.Center;
                            if (toTarget != Vector2.Zero) toTarget.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 15f, 0.15f);
                            if (aiTimer % 10 == 0) SpawnMeleeSlash(target, 0f);
                        }
                        else if (aiTimer == 30)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            NPC.velocity = dir * 25f;
                            for (int i = 0; i < 10; i++)
                                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(4, 4), Color.Cyan, 20, 1.5f, ParticleType.Spark);
                            SpawnMeleeSlash(target, 0f);
                            SpawnMeleeSlash(target, 0.8f);
                            SpawnMeleeSlash(target, -0.8f);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        else if (aiTimer > 40)
                        {
                            aiState = STATE_IDLE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 4:
                        // NEW: Blink & Echo Combo - see WhoAmI_Pattern_BlinkEchoCombo.cs
                        if (aiState != STATE_BLINK_ECHO_COMBO)
                        {
                            aiState = STATE_BLINK_ECHO_COMBO;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW: Abyssal Cleave & Fractured Space - see WhoAmI_Pattern_MeleeArchetypeExtras.cs
                        if (aiState != STATE_ABYSSAL_CLEAVE)
                        {
                            aiState = STATE_ABYSSAL_CLEAVE;
                            aiTimer = 0;
                            ResetAbyssalCleaveState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW: Orbiting Blade Ring (Sovereign Guard) - see WhoAmI_Pattern_MeleeArchetypeExtras.cs
                        if (aiState != STATE_ORBITING_BLADE_RING)
                        {
                            aiState = STATE_ORBITING_BLADE_RING;
                            aiTimer = 0;
                            ResetOrbitingBladeRingState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW: Dimensional Pierce / Flash Strike - see WhoAmI_Pattern_MeleeArchetypeExtras.cs
                        if (aiState != STATE_DIMENSIONAL_PIERCE)
                        {
                            aiState = STATE_DIMENSIONAL_PIERCE;
                            aiTimer = 0;
                            ResetDimensionalPierceState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 8:
                        // NEW WAVE 2: Warped Mirror Waltz - see WhoAmI_Pattern_MeleeArchetypeExtras2.cs
                        if (aiState != STATE_MELEE_MIRROR_WALTZ)
                        {
                            aiState = STATE_MELEE_MIRROR_WALTZ;
                            aiTimer = 0;
                            ResetMeleeMirrorWaltzState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 9:
                        // NEW WAVE 2: Fractured Persona Onslaught - see WhoAmI_Pattern_MeleeArchetypeExtras2.cs
                        if (aiState != STATE_MELEE_FRACTURED_ONSLAUGHT)
                        {
                            aiState = STATE_MELEE_FRACTURED_ONSLAUGHT;
                            aiTimer = 0;
                            ResetMeleeFracturedOnslaughtState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 10:
                        // NEW WAVE 2: Riposte Cascade - see WhoAmI_Pattern_MeleeArchetypeExtras2.cs
                        if (aiState != STATE_MELEE_RIPOSTE_CASCADE)
                        {
                            aiState = STATE_MELEE_RIPOSTE_CASCADE;
                            aiTimer = 0;
                            ResetMeleeRiposteCascadeState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
        }

        // ---------- ProjMelee ----------
        private void ExecuteProjMeleePattern(Player target, int pattern)
        {
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 15 == 0 && aiTimer < 80)
                        {
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer == 50)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            NPC.velocity = dir * 20f;
                            aiState = STATE_DODGE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer % 10 == 0 && aiTimer < 80)
                        {
                            for (int i = 0; i < 12; i++)
                            {
                                float ang = i * (MathHelper.Pi / 6f) + aiTimer * 0.02f;
                                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                                if (hasProjectile)
                                    FireAttackProjectile(target);
                                else
                                    SpawnMeleeSlash(target, ang);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer < 25)
                        {
                            if (aiTimer % 5 == 0) SpawnMeleeSlash(target, 0f);
                        }
                        else if (aiTimer == 25)
                        {
                            if (hasProjectile)
                            {
                                for (int i = 0; i < 4; i++)
                                    FireAttackProjectile(target);
                            }
                            else
                                SpawnMeleeSlash(target, 0f);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 45) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer < 50)
                        {
                            Vector2 away = NPC.Center - target.Center;
                            if (away != Vector2.Zero) away.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, away * 12f, 0.12f);
                            if (hasProjectile && aiTimer % 8 == 0) FireAttackProjectile(target);
                            else if (aiTimer % 8 == 0) SpawnMeleeSlash(target, 0f);
                        }
                        else
                        {
                            aiState = STATE_IDLE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 4:
                        // NEW: Blink & Echo Combo - see WhoAmI_Pattern_BlinkEchoCombo.cs
                        if (aiState != STATE_BLINK_ECHO_COMBO)
                        {
                            aiState = STATE_BLINK_ECHO_COMBO;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW: Abyssal Cleave & Fractured Space - see WhoAmI_Pattern_MeleeArchetypeExtras.cs
                        if (aiState != STATE_ABYSSAL_CLEAVE)
                        {
                            aiState = STATE_ABYSSAL_CLEAVE;
                            aiTimer = 0;
                            ResetAbyssalCleaveState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW: Orbiting Blade Ring (Sovereign Guard) - see WhoAmI_Pattern_MeleeArchetypeExtras.cs
                        if (aiState != STATE_ORBITING_BLADE_RING)
                        {
                            aiState = STATE_ORBITING_BLADE_RING;
                            aiTimer = 0;
                            ResetOrbitingBladeRingState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW: Dimensional Pierce / Flash Strike - see WhoAmI_Pattern_MeleeArchetypeExtras.cs
                        if (aiState != STATE_DIMENSIONAL_PIERCE)
                        {
                            aiState = STATE_DIMENSIONAL_PIERCE;
                            aiTimer = 0;
                            ResetDimensionalPierceState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 8:
                        // NEW WAVE 2: Warped Mirror Waltz - see WhoAmI_Pattern_MeleeArchetypeExtras2.cs
                        if (aiState != STATE_MELEE_MIRROR_WALTZ)
                        {
                            aiState = STATE_MELEE_MIRROR_WALTZ;
                            aiTimer = 0;
                            ResetMeleeMirrorWaltzState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 9:
                        // NEW WAVE 2: Fractured Persona Onslaught - see WhoAmI_Pattern_MeleeArchetypeExtras2.cs
                        if (aiState != STATE_MELEE_FRACTURED_ONSLAUGHT)
                        {
                            aiState = STATE_MELEE_FRACTURED_ONSLAUGHT;
                            aiTimer = 0;
                            ResetMeleeFracturedOnslaughtState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 10:
                        // NEW WAVE 2: Riposte Cascade - see WhoAmI_Pattern_MeleeArchetypeExtras2.cs
                        if (aiState != STATE_MELEE_RIPOSTE_CASCADE)
                        {
                            aiState = STATE_MELEE_RIPOSTE_CASCADE;
                            aiTimer = 0;
                            ResetMeleeRiposteCascadeState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
            else
            {
                // Phase 1 (existing ProjMelee)
                switch (pattern)
                {
                    case 0:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 25 == 0 && aiTimer < 100)
                        {
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer == 60)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            NPC.velocity = dir * 15f;
                            aiState = STATE_DODGE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer % 15 == 0 && aiTimer < 90)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                float ang = i * (MathHelper.Pi / 4f) + aiTimer * 0.01f;
                                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
                                if (hasProjectile)
                                    FireAttackProjectile(target);
                                else
                                    SpawnMeleeSlash(target, ang);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer < 30)
                        {
                            if (aiTimer % 10 == 0) SpawnMeleeSlash(target, 0f);
                        }
                        else if (aiTimer == 30)
                        {
                            if (hasProjectile)
                            {
                                FireAttackProjectile(target);
                                FireAttackProjectile(target);
                            }
                            else
                                SpawnMeleeSlash(target, 0f);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 50) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer < 60)
                        {
                            Vector2 away = NPC.Center - target.Center;
                            if (away != Vector2.Zero) away.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, away * 8f, 0.08f);
                            if (hasProjectile && aiTimer % 12 == 0) FireAttackProjectile(target);
                            else if (aiTimer % 12 == 0) SpawnMeleeSlash(target, 0f);
                        }
                        else
                        {
                            aiState = STATE_IDLE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 4:
                        // NEW: Blink & Echo Combo - see WhoAmI_Pattern_BlinkEchoCombo.cs
                        if (aiState != STATE_BLINK_ECHO_COMBO)
                        {
                            aiState = STATE_BLINK_ECHO_COMBO;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW: Abyssal Cleave & Fractured Space - see WhoAmI_Pattern_MeleeArchetypeExtras.cs
                        if (aiState != STATE_ABYSSAL_CLEAVE)
                        {
                            aiState = STATE_ABYSSAL_CLEAVE;
                            aiTimer = 0;
                            ResetAbyssalCleaveState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW: Orbiting Blade Ring (Sovereign Guard) - see WhoAmI_Pattern_MeleeArchetypeExtras.cs
                        if (aiState != STATE_ORBITING_BLADE_RING)
                        {
                            aiState = STATE_ORBITING_BLADE_RING;
                            aiTimer = 0;
                            ResetOrbitingBladeRingState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW: Dimensional Pierce / Flash Strike - see WhoAmI_Pattern_MeleeArchetypeExtras.cs
                        if (aiState != STATE_DIMENSIONAL_PIERCE)
                        {
                            aiState = STATE_DIMENSIONAL_PIERCE;
                            aiTimer = 0;
                            ResetDimensionalPierceState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 8:
                        // NEW WAVE 2: Warped Mirror Waltz - see WhoAmI_Pattern_MeleeArchetypeExtras2.cs
                        if (aiState != STATE_MELEE_MIRROR_WALTZ)
                        {
                            aiState = STATE_MELEE_MIRROR_WALTZ;
                            aiTimer = 0;
                            ResetMeleeMirrorWaltzState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 9:
                        // NEW WAVE 2: Fractured Persona Onslaught - see WhoAmI_Pattern_MeleeArchetypeExtras2.cs
                        if (aiState != STATE_MELEE_FRACTURED_ONSLAUGHT)
                        {
                            aiState = STATE_MELEE_FRACTURED_ONSLAUGHT;
                            aiTimer = 0;
                            ResetMeleeFracturedOnslaughtState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 10:
                        // NEW WAVE 2: Riposte Cascade - see WhoAmI_Pattern_MeleeArchetypeExtras2.cs
                        if (aiState != STATE_MELEE_RIPOSTE_CASCADE)
                        {
                            aiState = STATE_MELEE_RIPOSTE_CASCADE;
                            aiTimer = 0;
                            ResetMeleeRiposteCascadeState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
        }

        // ---------- Ranged ----------
        private void ExecuteRangedPattern(Player target, int pattern)
        {
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            // FIX: pattern index 3 is the exact fallback SelectAndExecuteArchetypePattern picks when
            // !hasProjectile (validPatterns collapses to just {3} - see that method), and it's safe to
            // run without a "real" item.shoot because it only calls FireAttackProjectile, which already
            // has its own bullet-type fallback when weapon.shoot <= 0. The old blanket guard below
            // returned to STATE_IDLE before pattern 3 ever got a chance to run, so ANY ammo-based ranged
            // weapon (most vanilla bows/guns never set item.shoot at all - they fire via ammo) made
            // this whole archetype dead-end back to idle every single tick, which is why the boss would
            // often just circle the player and only start attacking again once something else (a
            // carousel weapon swap, a dodge, etc.) happened to change the situation. Exempting pattern 3
            // here mirrors the same fix already in place in ExecuteMagicPattern (`pattern != 3`).
            if (!hasProjectile && pattern != 3) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (aiTimer % 5 == 0 && aiTimer < 50)
                        {
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 70) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer < 100)
                        {
                            float angle = aiTimer * 0.05f;
                            Vector2 orbit = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 400f;
                            Vector2 targetPosition = target.Center + orbit;
                            Vector2 delta = targetPosition - NPC.Center;
                            if (delta.Length() > 10f)
                                NPC.velocity = Vector2.Lerp(NPC.velocity, Vector2.Normalize(delta) * 12f, 0.15f);
                            if (aiTimer % 10 == 0) FireAttackProjectile(target);
                        }
                        else
                        {
                            aiState = STATE_IDLE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 2:
                        if (aiTimer % 8 == 0 && aiTimer < 70)
                        {
                            for (int i = 0; i < 7; i++)
                            {
                                float spread = MathHelper.ToRadians(GetDeterministicRandom(-40, 40));
                                Vector2 dir = (target.Center - NPC.Center).RotatedBy(spread);
                                if (dir != Vector2.Zero) dir.Normalize();
                                FireAttackProjectile(target);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 90) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0) ExecuteGlitchTeleport(target);
                        else if (aiTimer == 20)
                        {
                            Vector2 aim = target.Center - NPC.Center;
                            if (aim != Vector2.Zero) aim.Normalize();
                            for (int i = 0; i < 5; i++) FireAttackProjectile(target);
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item40, NPC.Center);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 4:
                        // NEW: Orbiting Grid Lock - see WhoAmI_Pattern_OrbitingGridLock.cs
                        if (aiState != STATE_ORBIT_GRID_LOCK)
                        {
                            aiState = STATE_ORBIT_GRID_LOCK;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW: Vector Laser Grid System - see WhoAmI_Pattern_RangedArchetypeExtras.cs
                        if (aiState != STATE_VECTOR_LASER_GRID)
                        {
                            aiState = STATE_VECTOR_LASER_GRID;
                            aiTimer = 0;
                            ResetVectorLaserGridState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW: Homing Cluster Comet - see WhoAmI_Pattern_RangedArchetypeExtras.cs
                        if (aiState != STATE_HOMING_CLUSTER_COMET)
                        {
                            aiState = STATE_HOMING_CLUSTER_COMET;
                            aiTimer = 0;
                            ResetHomingClusterCometState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW: Singularity Overdrive - see WhoAmI_Pattern_RangedArchetypeExtras.cs
                        if (aiState != STATE_SINGULARITY_OVERDRIVE)
                        {
                            aiState = STATE_SINGULARITY_OVERDRIVE;
                            aiTimer = 0;
                            ResetSingularityOverdriveState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 8:
                        // NEW WAVE 2: Parallax Volley - see WhoAmI_Pattern_RangedArchetypeExtras2.cs
                        if (aiState != STATE_RANGED_PARALLAX_VOLLEY)
                        {
                            aiState = STATE_RANGED_PARALLAX_VOLLEY;
                            aiTimer = 0;
                            ResetRangedParallaxVolleyState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 9:
                        // NEW WAVE 2: Mirror Ricochet - see WhoAmI_Pattern_RangedArchetypeExtras2.cs
                        if (aiState != STATE_RANGED_MIRROR_RICOCHET)
                        {
                            aiState = STATE_RANGED_MIRROR_RICOCHET;
                            aiTimer = 0;
                            ResetRangedMirrorRicochetState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 10:
                        // NEW WAVE 2: Starfall Convergence - see WhoAmI_Pattern_RangedArchetypeExtras2.cs
                        if (aiState != STATE_RANGED_STARFALL_CONVERGENCE)
                        {
                            aiState = STATE_RANGED_STARFALL_CONVERGENCE;
                            aiTimer = 0;
                            ResetRangedStarfallConvergenceState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
            else
            {
                // Phase 1 Ranged (existing)
                switch (pattern)
                {
                    case 0:
                        if (aiTimer % 8 == 0 && aiTimer < 60)
                        {
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 80) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer < 120)
                        {
                            float angle = aiTimer * 0.03f;
                            Vector2 orbit = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 300f;
                            Vector2 targetPosition = target.Center + orbit;
                            Vector2 delta = targetPosition - NPC.Center;
                            if (delta.Length() > 10f)
                                NPC.velocity = Vector2.Lerp(NPC.velocity, Vector2.Normalize(delta) * 8f, 0.1f);
                            if (aiTimer % 15 == 0) FireAttackProjectile(target);
                        }
                        else
                        {
                            aiState = STATE_IDLE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 2:
                        if (aiTimer % 12 == 0 && aiTimer < 80)
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                float spread = MathHelper.ToRadians(GetDeterministicRandom(-30, 30));
                                Vector2 dir = (target.Center - NPC.Center).RotatedBy(spread);
                                if (dir != Vector2.Zero) dir.Normalize();
                                FireAttackProjectile(target);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0) ExecuteGlitchTeleport(target);
                        else if (aiTimer == 30)
                        {
                            Vector2 aim = target.Center - NPC.Center;
                            if (aim != Vector2.Zero) aim.Normalize();
                            FireAttackProjectile(target);
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item40, NPC.Center);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 50) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 4:
                        // NEW: Orbiting Grid Lock - see WhoAmI_Pattern_OrbitingGridLock.cs
                        if (aiState != STATE_ORBIT_GRID_LOCK)
                        {
                            aiState = STATE_ORBIT_GRID_LOCK;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW: Vector Laser Grid System - see WhoAmI_Pattern_RangedArchetypeExtras.cs
                        if (aiState != STATE_VECTOR_LASER_GRID)
                        {
                            aiState = STATE_VECTOR_LASER_GRID;
                            aiTimer = 0;
                            ResetVectorLaserGridState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW: Homing Cluster Comet - see WhoAmI_Pattern_RangedArchetypeExtras.cs
                        if (aiState != STATE_HOMING_CLUSTER_COMET)
                        {
                            aiState = STATE_HOMING_CLUSTER_COMET;
                            aiTimer = 0;
                            ResetHomingClusterCometState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW: Singularity Overdrive - see WhoAmI_Pattern_RangedArchetypeExtras.cs
                        if (aiState != STATE_SINGULARITY_OVERDRIVE)
                        {
                            aiState = STATE_SINGULARITY_OVERDRIVE;
                            aiTimer = 0;
                            ResetSingularityOverdriveState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 8:
                        // NEW WAVE 2: Parallax Volley - see WhoAmI_Pattern_RangedArchetypeExtras2.cs
                        if (aiState != STATE_RANGED_PARALLAX_VOLLEY)
                        {
                            aiState = STATE_RANGED_PARALLAX_VOLLEY;
                            aiTimer = 0;
                            ResetRangedParallaxVolleyState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 9:
                        // NEW WAVE 2: Mirror Ricochet - see WhoAmI_Pattern_RangedArchetypeExtras2.cs
                        if (aiState != STATE_RANGED_MIRROR_RICOCHET)
                        {
                            aiState = STATE_RANGED_MIRROR_RICOCHET;
                            aiTimer = 0;
                            ResetRangedMirrorRicochetState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 10:
                        // NEW WAVE 2: Starfall Convergence - see WhoAmI_Pattern_RangedArchetypeExtras2.cs
                        if (aiState != STATE_RANGED_STARFALL_CONVERGENCE)
                        {
                            aiState = STATE_RANGED_STARFALL_CONVERGENCE;
                            aiTimer = 0;
                            ResetRangedStarfallConvergenceState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
        }

        // ---------- Magic ----------
        private void ExecuteMagicPattern(Player target, int pattern)
        {
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile && pattern != 3) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (!isCurrentlyChanneling)
                        {
                            isCurrentlyChanneling = true;
                            for (int i = 0; i < 3; i++) FireAttackProjectile(target);
                            aiTimer = 0;
                        }
                        else
                        {
                            // FIX ("projectile dari senjata boss kadang semi-homing padahal senjata
                            // aslinya nggak homing"): step sebelumnya (yang nyelamatin dari crash
                            // "Index was outside the bounds of the array") emang udah bener soal ai[]
                            // - tapi solusinya OVER-CORRECTED: nge-Lerp velocity SEMUA proyektil aktif
                            // (p.type == activeWeapon.shoot) ke arah target TIAP TICK selama channel
                            // berlangsung (sampai 60-90 tick) itu SENDIRI adalah homing buatan yang
                            // dipaksakan ke SEMUA senjata magic tanpa peduli senjata aslinya di vanilla
                            // homing atau nggak. Water Bolt/Golden Shower/Crystal Storm/Diamond Staff
                            // dsb yang di tangan player jalan LURUS TOTAL jadi keliatan "nempel ngejar"
                            // player kalau boss lagi channeling weapon itu - persis laporan bug ini,
                            // dan "kadang" karena cuma kejadian pas pattern 0 (channeling) yang kepilih
                            // dari beberapa pattern magic yang ada.
                            //
                            // Fix: jangan re-steer proyektil yang UDAH ditembak sama sekali - biarin
                            // dia terbang sesuai AI aslinya (lurus buat senjata non-homing, atau
                            // homing sendiri kalau memang vanilla-nya homing - itu urusan AI vanilla
                            // proyektilnya, bukan urusan kita maksa dari sini). "Channeling" sekarang
                            // cukup nembak ULANG shot baru tiap beberapa tick yang di-aim ke posisi
                            // target SAAT itu - itu udah cukup buat kesan "boss lagi nembakin target
                            // terus-terusan sambil channel", tanpa proyektil manapun berbelok
                            // supranatural di tengah jalan.
                            if (aiTimer % 12 == 0 && aiTimer < 84)
                                FireAttackProjectile(target);
                            if (aiTimer > 90) { isCurrentlyChanneling = false; aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        }
                        break;
                    case 1:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 20 == 0 && aiTimer < 100)
                        {
                            Vector2 pos = target.Center + new Vector2(Main.rand.Next(-600, 600), -600);
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, pos);
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 15 == 0 && aiTimer < 90)
                        {
                            float ang = aiTimer * 0.06f;
                            for (int i = 0; i < 6; i++)
                            {
                                float a = ang + i * (MathHelper.Pi / 3f);
                                Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                                FireAttackProjectile(target);
                            }
                        }
                        if (aiTimer > 110) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0) ExecuteGlitchTeleport(target);
                        else if (aiTimer == 15)
                        {
                            for (int i = 0; i < 30; i++)
                            {
                                Vector2 dir = Main.rand.NextVector2Circular(1f, 1f);
                                if (hasProjectile)
                                    FireAttackProjectile(target);
                                else
                                    SpawnMeleeSlash(target, dir.ToRotation());
                            }
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        }
                        if (aiTimer > 35) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 4:
                        // Phase 2 Phantom Mirage Cascade: enter the state and let HandleMagicSpiralRift run.
                        if (aiState != STATE_MAGIC_SPIRAL_RIFT)
                        {
                            aiState = STATE_MAGIC_SPIRAL_RIFT;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW: Gravity Well & Arcane Torrent - see WhoAmI_Pattern_GravityWellTorrent.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_GRAVITY_WELL_TORRENT)
                        {
                            aiState = STATE_GRAVITY_WELL_TORRENT;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW: Aureola Signet Rain - see WhoAmI_Pattern_MagicArchetypeExtras.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_AUREOLA_SIGNET_RAIN)
                        {
                            aiState = STATE_AUREOLA_SIGNET_RAIN;
                            aiTimer = 0;
                            ResetAureolaSignetRainState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW: Double Helix Sweep - see WhoAmI_Pattern_MagicArchetypeExtras.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_DOUBLE_HELIX_SWEEP)
                        {
                            aiState = STATE_DOUBLE_HELIX_SWEEP;
                            aiTimer = 0;
                            ResetDoubleHelixSweepState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 8:
                        // NEW: Quantum Glitch Phasing - see WhoAmI_Pattern_MagicArchetypeExtras.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_QUANTUM_GLITCH_PHASING)
                        {
                            aiState = STATE_QUANTUM_GLITCH_PHASING;
                            aiTimer = 0;
                            ResetQuantumGlitchPhasingState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 9:
                        // NEW WAVE 2: Fracture Bloom - see WhoAmI_Pattern_MagicArchetypeExtras2.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_MAGIC_FRACTURE_BLOOM)
                        {
                            aiState = STATE_MAGIC_FRACTURE_BLOOM;
                            aiTimer = 0;
                            ResetMagicFractureBloomState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 10:
                        // NEW WAVE 2: Umbral Duality - see WhoAmI_Pattern_MagicArchetypeExtras2.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_MAGIC_UMBRAL_DUALITY)
                        {
                            aiState = STATE_MAGIC_UMBRAL_DUALITY;
                            aiTimer = 0;
                            ResetMagicUmbralDualityState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 11:
                        // NEW WAVE 2: Paradox Mirror Volley - see WhoAmI_Pattern_MagicArchetypeExtras2.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_MAGIC_PARADOX_MIRROR)
                        {
                            aiState = STATE_MAGIC_PARADOX_MIRROR;
                            aiTimer = 0;
                            ResetMagicParadoxMirrorState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
            else
            {
                // Phase 1 Magic (existing)
                switch (pattern)
                {
                    case 0:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (!isCurrentlyChanneling)
                        {
                            isCurrentlyChanneling = true;
                            FireAttackProjectile(target);
                            aiTimer = 0;
                        }
                        else
                        {
                            // Same fix as the Phase 2 case 0 branch above (see the full explanation
                            // there): re-steering every already-fired projectile of this weapon type
                            // toward the target every tick made ALL magic weapons look semi-homing
                            // while channeling, even ones that are dead straight in vanilla. Don't
                            // touch already-spawned projectiles at all - just keep firing fresh aimed
                            // shots at the target's current position while channeling is active.
                            if (aiTimer % 15 == 0 && aiTimer < 105)
                                FireAttackProjectile(target);
                            if (aiTimer > 90) { isCurrentlyChanneling = false; aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        }
                        break;
                    case 1:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 30 == 0 && aiTimer < 120)
                        {
                            Vector2 pos = target.Center + new Vector2(Main.rand.Next(-400, 400), -400);
                            FireAttackProjectile(target);
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, pos);
                        }
                        if (aiTimer > 150) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 20 == 0 && aiTimer < 100)
                        {
                            float ang = aiTimer * 0.04f;
                            for (int i = 0; i < 4; i++)
                            {
                                float a = ang + i * (MathHelper.Pi / 2f);
                                Vector2 dir = new Vector2((float)Math.Cos(a), (float)Math.Sin(a));
                                FireAttackProjectile(target);
                            }
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0) ExecuteGlitchTeleport(target);
                        else if (aiTimer == 20)
                        {
                            for (int i = 0; i < 20; i++)
                            {
                                Vector2 dir = Main.rand.NextVector2Circular(1f, 1f);
                                if (hasProjectile)
                                    FireAttackProjectile(target);
                                else
                                    SpawnMeleeSlash(target, dir.ToRotation());
                            }
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 4:
                        // Phase 1 Phantom Mirage Cascade: identical trigger — HandleMagicSpiralRift reads
                        // isPhase2 internally to scale the shot count, teleport distance, and fan width.
                        if (aiState != STATE_MAGIC_SPIRAL_RIFT)
                        {
                            aiState = STATE_MAGIC_SPIRAL_RIFT;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW: Gravity Well & Arcane Torrent - see WhoAmI_Pattern_GravityWellTorrent.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_GRAVITY_WELL_TORRENT)
                        {
                            aiState = STATE_GRAVITY_WELL_TORRENT;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW: Aureola Signet Rain - see WhoAmI_Pattern_MagicArchetypeExtras.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_AUREOLA_SIGNET_RAIN)
                        {
                            aiState = STATE_AUREOLA_SIGNET_RAIN;
                            aiTimer = 0;
                            ResetAureolaSignetRainState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW: Double Helix Sweep - see WhoAmI_Pattern_MagicArchetypeExtras.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_DOUBLE_HELIX_SWEEP)
                        {
                            aiState = STATE_DOUBLE_HELIX_SWEEP;
                            aiTimer = 0;
                            ResetDoubleHelixSweepState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 8:
                        // NEW: Quantum Glitch Phasing - see WhoAmI_Pattern_MagicArchetypeExtras.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_QUANTUM_GLITCH_PHASING)
                        {
                            aiState = STATE_QUANTUM_GLITCH_PHASING;
                            aiTimer = 0;
                            ResetQuantumGlitchPhasingState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 9:
                        // NEW WAVE 2: Fracture Bloom - see WhoAmI_Pattern_MagicArchetypeExtras2.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_MAGIC_FRACTURE_BLOOM)
                        {
                            aiState = STATE_MAGIC_FRACTURE_BLOOM;
                            aiTimer = 0;
                            ResetMagicFractureBloomState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 10:
                        // NEW WAVE 2: Umbral Duality - see WhoAmI_Pattern_MagicArchetypeExtras2.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_MAGIC_UMBRAL_DUALITY)
                        {
                            aiState = STATE_MAGIC_UMBRAL_DUALITY;
                            aiTimer = 0;
                            ResetMagicUmbralDualityState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 11:
                        // NEW WAVE 2: Paradox Mirror Volley - see WhoAmI_Pattern_MagicArchetypeExtras2.cs
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiState != STATE_MAGIC_PARADOX_MIRROR)
                        {
                            aiState = STATE_MAGIC_PARADOX_MIRROR;
                            aiTimer = 0;
                            ResetMagicParadoxMirrorState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
        }

        // ---------- Summon ----------
        private void ExecuteSummonPattern(Player target, int pattern)
        {
            bool hasProjectile = WeaponHasProjectile(activeWeapon);

            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 20 == 0 && aiTimer < 100)
                        {
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer == 0) NPC.alpha = 255;
                        else if (aiTimer == 40) NPC.alpha = 0;
                        else if (aiTimer == 60)
                        {
                            if (hasProjectile)
                            {
                                for (int i = 0; i < 4; i++)
                                    FireAttackProjectile(target);
                            }
                            else
                                SpawnMeleeSlash(target, 0f);
                        }
                        if (aiTimer > 80) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 6 == 0 && aiTimer < 70)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            Vector2 perp = new Vector2(-dir.Y, dir.X) * Main.rand.NextFloat(-1f, 1f) * 80f;
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 90) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 20)
                        {
                            for (int i = 0; i < 12; i++)
                            {
                                Vector2 dir = Main.rand.NextVector2Circular(1f, 1f);
                                if (hasProjectile)
                                    FireAttackProjectile(target);
                                else
                                    SpawnMeleeSlash(target, dir.ToRotation());
                            }
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 4:
                        // NEW: Spectral Rift Swarm - see WhoAmI_Pattern_ArchetypeExtras.cs
                        if (aiState != STATE_SUMMON_RIFT_SWARM)
                        {
                            aiState = STATE_SUMMON_RIFT_SWARM;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW WAVE 2: Wraith Convergence - see WhoAmI_Pattern_SummonWhipExtras2.cs
                        if (aiState != STATE_SUMMON_WRAITH_CONVERGENCE)
                        {
                            aiState = STATE_SUMMON_WRAITH_CONVERGENCE;
                            aiTimer = 0;
                            ResetSummonWraithConvergenceState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW WAVE 2: Soul Tether Bind - see WhoAmI_Pattern_SummonWhipExtras2.cs
                        if (aiState != STATE_SUMMON_SOUL_TETHER)
                        {
                            aiState = STATE_SUMMON_SOUL_TETHER;
                            aiTimer = 0;
                            ResetSummonSoulTetherState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW WAVE 2: Spectral Carousel - see WhoAmI_Pattern_SummonWhipExtras2.cs
                        if (aiState != STATE_SUMMON_SPECTRAL_CAROUSEL)
                        {
                            aiState = STATE_SUMMON_SPECTRAL_CAROUSEL;
                            aiTimer = 0;
                            ResetSummonSpectralCarouselState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
            else
            {
                // Phase 1 Summon (existing)
                switch (pattern)
                {
                    case 0:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 30 == 0 && aiTimer < 120)
                        {
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 150) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer == 0) NPC.alpha = 255;
                        else if (aiTimer == 60) NPC.alpha = 0;
                        else if (aiTimer == 80)
                        {
                            if (hasProjectile)
                            {
                                FireAttackProjectile(target);
                                FireAttackProjectile(target);
                            }
                            else
                                SpawnMeleeSlash(target, 0f);
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }
                        if (aiTimer % 10 == 0 && aiTimer < 80)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            Vector2 perp = new Vector2(-dir.Y, dir.X) * Main.rand.NextFloat(-1f, 1f) * 50f;
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 30)
                        {
                            for (int i = 0; i < 6; i++)
                            {
                                Vector2 dir = Main.rand.NextVector2Circular(1f, 1f);
                                if (hasProjectile)
                                    FireAttackProjectile(target);
                                else
                                    SpawnMeleeSlash(target, dir.ToRotation());
                            }
                            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                        }
                        if (aiTimer > 50) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 4:
                        // NEW: Spectral Rift Swarm - see WhoAmI_Pattern_ArchetypeExtras.cs
                        if (aiState != STATE_SUMMON_RIFT_SWARM)
                        {
                            aiState = STATE_SUMMON_RIFT_SWARM;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW WAVE 2: Wraith Convergence - see WhoAmI_Pattern_SummonWhipExtras2.cs
                        if (aiState != STATE_SUMMON_WRAITH_CONVERGENCE)
                        {
                            aiState = STATE_SUMMON_WRAITH_CONVERGENCE;
                            aiTimer = 0;
                            ResetSummonWraithConvergenceState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW WAVE 2: Soul Tether Bind - see WhoAmI_Pattern_SummonWhipExtras2.cs
                        if (aiState != STATE_SUMMON_SOUL_TETHER)
                        {
                            aiState = STATE_SUMMON_SOUL_TETHER;
                            aiTimer = 0;
                            ResetSummonSoulTetherState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW WAVE 2: Spectral Carousel - see WhoAmI_Pattern_SummonWhipExtras2.cs
                        if (aiState != STATE_SUMMON_SPECTRAL_CAROUSEL)
                        {
                            aiState = STATE_SUMMON_SPECTRAL_CAROUSEL;
                            aiTimer = 0;
                            ResetSummonSpectralCarouselState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
        }

        // ---------- Whip ----------
        private void ExecuteWhipPattern(Player target, int pattern)
        {
            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (aiTimer % 10 == 0 && aiTimer < 70)
                        {
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 90) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer % 15 == 0 && aiTimer < 80)
                        {
                            for (int i = 0; i < 5; i++)
                            {
                                float ang = aiTimer * 0.08f + i * 1.256f;
                                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 120f;
                                FireAttackProjectile(target);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer < 25)
                        {
                            Vector2 toTarget = target.Center - NPC.Center;
                            if (toTarget != Vector2.Zero) toTarget.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 18f, 0.15f);
                        }
                        else if (aiTimer == 25)
                        {
                            for (int i = 0; i < 4; i++)
                                FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 45) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0)
                        {
                            for (int i = 0; i < 3; i++)
                                FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 35) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 4:
                        // NEW: Lash Cage - see WhoAmI_Pattern_ArchetypeExtras.cs
                        if (aiState != STATE_WHIP_LASH_CAGE)
                        {
                            aiState = STATE_WHIP_LASH_CAGE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW WAVE 2: Serpent's Coil - see WhoAmI_Pattern_SummonWhipExtras2.cs
                        if (aiState != STATE_WHIP_SERPENTS_COIL)
                        {
                            aiState = STATE_WHIP_SERPENTS_COIL;
                            aiTimer = 0;
                            ResetWhipSerpentsCoilState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW WAVE 2: Cracked Fan Lash - see WhoAmI_Pattern_SummonWhipExtras2.cs
                        if (aiState != STATE_WHIP_FAN_LASH)
                        {
                            aiState = STATE_WHIP_FAN_LASH;
                            aiTimer = 0;
                            ResetWhipFanLashState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW WAVE 2: Puppeteer's Snap - see WhoAmI_Pattern_SummonWhipExtras2.cs
                        if (aiState != STATE_WHIP_PUPPETEER_SNAP)
                        {
                            aiState = STATE_WHIP_PUPPETEER_SNAP;
                            aiTimer = 0;
                            ResetWhipPuppeteerSnapState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
            else
            {
                // Phase 1 Whip (existing)
                switch (pattern)
                {
                    case 0:
                        if (aiTimer % 15 == 0 && aiTimer < 90)
                        {
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 110) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer % 20 == 0 && aiTimer < 100)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                float ang = aiTimer * 0.05f + i * 2.094f;
                                Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 80f;
                                FireAttackProjectile(target);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer < 30)
                        {
                            Vector2 toTarget = target.Center - NPC.Center;
                            if (toTarget != Vector2.Zero) toTarget.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 12f, 0.1f);
                        }
                        else if (aiTimer == 30)
                        {
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 50) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer == 0)
                        {
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 4:
                        // NEW: Lash Cage - see WhoAmI_Pattern_ArchetypeExtras.cs
                        if (aiState != STATE_WHIP_LASH_CAGE)
                        {
                            aiState = STATE_WHIP_LASH_CAGE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW WAVE 2: Serpent's Coil - see WhoAmI_Pattern_SummonWhipExtras2.cs
                        if (aiState != STATE_WHIP_SERPENTS_COIL)
                        {
                            aiState = STATE_WHIP_SERPENTS_COIL;
                            aiTimer = 0;
                            ResetWhipSerpentsCoilState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW WAVE 2: Cracked Fan Lash - see WhoAmI_Pattern_SummonWhipExtras2.cs
                        if (aiState != STATE_WHIP_FAN_LASH)
                        {
                            aiState = STATE_WHIP_FAN_LASH;
                            aiTimer = 0;
                            ResetWhipFanLashState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW WAVE 2: Puppeteer's Snap - see WhoAmI_Pattern_SummonWhipExtras2.cs
                        if (aiState != STATE_WHIP_PUPPETEER_SNAP)
                        {
                            aiState = STATE_WHIP_PUPPETEER_SNAP;
                            aiTimer = 0;
                            ResetWhipPuppeteerSnapState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
        }

        // ---------- Yoyo ----------
        private void ExecuteYoyoPattern(Player target, int pattern)
        {
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            // FIX: same issue and same fix as ExecuteRangedPattern above - pattern 3 is the fallback
            // SelectAndExecuteArchetypePattern deliberately picks when !hasProjectile (validPatterns
            // collapses to {3}), and it only calls FireAttackProjectile (which has its own fallback), so
            // it must be exempted from this guard instead of being blocked by it.
            if (!hasProjectile && pattern != 3) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (aiTimer % 6 == 0 && aiTimer < 80)
                        {
                            float ang = aiTimer * 0.08f;
                            Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 150f;
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer == 0)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            for (int i = 0; i < 3; i++) FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 35) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer == 0)
                        {
                            for (int i = 0; i < 5; i++)
                                FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 25) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer % 20 == 0 && aiTimer < 70)
                        {
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 90) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 4:
                        // NEW: Tether Storm - see WhoAmI_Pattern_ArchetypeExtras.cs
                        if (aiState != STATE_YOYO_TETHER_STORM)
                        {
                            aiState = STATE_YOYO_TETHER_STORM;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW WAVE 2: Pendulum Reckoning - see WhoAmI_Pattern_YoyoBoomerangExtras2.cs
                        if (aiState != STATE_YOYO_PENDULUM_RECKONING)
                        {
                            aiState = STATE_YOYO_PENDULUM_RECKONING;
                            aiTimer = 0;
                            ResetYoyoPendulumReckoningState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW WAVE 2: Binary Orbit Snare - see WhoAmI_Pattern_YoyoBoomerangExtras2.cs
                        if (aiState != STATE_YOYO_BINARY_SNARE)
                        {
                            aiState = STATE_YOYO_BINARY_SNARE;
                            aiTimer = 0;
                            ResetYoyoBinarySnareState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW WAVE 2: Cascade Unravel - see WhoAmI_Pattern_YoyoBoomerangExtras2.cs
                        if (aiState != STATE_YOYO_CASCADE_UNRAVEL)
                        {
                            aiState = STATE_YOYO_CASCADE_UNRAVEL;
                            aiTimer = 0;
                            ResetYoyoCascadeUnravelState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
            else
            {
                // Phase 1 Yoyo (existing)
                switch (pattern)
                {
                    case 0:
                        if (aiTimer % 10 == 0 && aiTimer < 100)
                        {
                            float ang = aiTimer * 0.05f;
                            Vector2 dir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * 100f;
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 120) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer == 0)
                        {
                            Vector2 dir = target.Center - NPC.Center;
                            if (dir != Vector2.Zero) dir.Normalize();
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer == 0)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                FireAttackProjectile(target);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 30) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer % 25 == 0 && aiTimer < 80)
                        {
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 100) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 4:
                        // NEW: Tether Storm - see WhoAmI_Pattern_ArchetypeExtras.cs
                        if (aiState != STATE_YOYO_TETHER_STORM)
                        {
                            aiState = STATE_YOYO_TETHER_STORM;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW WAVE 2: Pendulum Reckoning - see WhoAmI_Pattern_YoyoBoomerangExtras2.cs
                        if (aiState != STATE_YOYO_PENDULUM_RECKONING)
                        {
                            aiState = STATE_YOYO_PENDULUM_RECKONING;
                            aiTimer = 0;
                            ResetYoyoPendulumReckoningState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW WAVE 2: Binary Orbit Snare - see WhoAmI_Pattern_YoyoBoomerangExtras2.cs
                        if (aiState != STATE_YOYO_BINARY_SNARE)
                        {
                            aiState = STATE_YOYO_BINARY_SNARE;
                            aiTimer = 0;
                            ResetYoyoBinarySnareState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW WAVE 2: Cascade Unravel - see WhoAmI_Pattern_YoyoBoomerangExtras2.cs
                        if (aiState != STATE_YOYO_CASCADE_UNRAVEL)
                        {
                            aiState = STATE_YOYO_CASCADE_UNRAVEL;
                            aiTimer = 0;
                            ResetYoyoCascadeUnravelState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
        }

        // ---------- Boomerang ----------
        private void ExecuteBoomerangPattern(Player target, int pattern)
        {
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            // FIX: same issue as ExecuteRangedPattern/ExecuteYoyoPattern above, but here the fallback
            // SelectAndExecuteArchetypePattern picks for !hasProjectile is pattern index 0 (validPatterns
            // collapses to {0} for Boomerang), so that's the index exempted here.
            if (!hasProjectile && pattern != 0) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            if (isPhase2)
            {
                switch (pattern)
                {
                    case 0:
                        if (aiTimer == 0)
                        {
                            for (int i = 0; i < 3; i++)
                                FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 25) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer == 0)
                        {
                            for (int i = 0; i < 5; i++)
                                FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 25) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer == 0)
                        {
                            FireAttackProjectile(target);
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 35) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer < 15)
                        {
                            Vector2 toTarget = target.Center - NPC.Center;
                            if (toTarget != Vector2.Zero) toTarget.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 18f, 0.15f);
                        }
                        else if (aiTimer == 15)
                        {
                            for (int i = 0; i < 4; i++)
                                FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 35) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 4:
                        // NEW: Crossfire - see WhoAmI_Pattern_ArchetypeExtras.cs
                        if (aiState != STATE_BOOMERANG_CROSSFIRE)
                        {
                            aiState = STATE_BOOMERANG_CROSSFIRE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW WAVE 2: Windmill Barrage - see WhoAmI_Pattern_YoyoBoomerangExtras2.cs
                        if (aiState != STATE_BOOMERANG_WINDMILL_BARRAGE)
                        {
                            aiState = STATE_BOOMERANG_WINDMILL_BARRAGE;
                            aiTimer = 0;
                            ResetBoomerangWindmillBarrageState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW WAVE 2: Ricochet Triangle - see WhoAmI_Pattern_YoyoBoomerangExtras2.cs
                        if (aiState != STATE_BOOMERANG_RICOCHET_TRIANGLE)
                        {
                            aiState = STATE_BOOMERANG_RICOCHET_TRIANGLE;
                            aiTimer = 0;
                            ResetBoomerangRicochetTriangleState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW WAVE 2: Curving Return Barrage - see WhoAmI_Pattern_YoyoBoomerangExtras2.cs
                        if (aiState != STATE_BOOMERANG_CURVING_RETURN)
                        {
                            aiState = STATE_BOOMERANG_CURVING_RETURN;
                            aiTimer = 0;
                            ResetBoomerangCurvingReturnState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
            else
            {
                // Phase 1 Boomerang (existing)
                switch (pattern)
                {
                    case 0:
                        if (aiTimer == 0)
                        {
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 30) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 1:
                        if (aiTimer == 0)
                        {
                            for (int i = 0; i < 3; i++)
                            {
                                FireAttackProjectile(target);
                            }
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 30) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 2:
                        if (aiTimer == 0)
                        {
                            FireAttackProjectile(target);
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 3:
                        if (aiTimer < 20)
                        {
                            Vector2 toTarget = target.Center - NPC.Center;
                            if (toTarget != Vector2.Zero) toTarget.Normalize();
                            NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * 12f, 0.1f);
                        }
                        else if (aiTimer == 20)
                        {
                            FireAttackProjectile(target);
                            bossWeaponSwingTimer = bossWeaponSwingMax;
                        }
                        if (aiTimer > 40) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; }
                        break;
                    case 4:
                        // NEW: Crossfire - see WhoAmI_Pattern_ArchetypeExtras.cs
                        if (aiState != STATE_BOOMERANG_CROSSFIRE)
                        {
                            aiState = STATE_BOOMERANG_CROSSFIRE;
                            aiTimer = 0;
                            NPC.netUpdate = true;
                        }
                        break;
                    case 5:
                        // NEW WAVE 2: Windmill Barrage - see WhoAmI_Pattern_YoyoBoomerangExtras2.cs
                        if (aiState != STATE_BOOMERANG_WINDMILL_BARRAGE)
                        {
                            aiState = STATE_BOOMERANG_WINDMILL_BARRAGE;
                            aiTimer = 0;
                            ResetBoomerangWindmillBarrageState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 6:
                        // NEW WAVE 2: Ricochet Triangle - see WhoAmI_Pattern_YoyoBoomerangExtras2.cs
                        if (aiState != STATE_BOOMERANG_RICOCHET_TRIANGLE)
                        {
                            aiState = STATE_BOOMERANG_RICOCHET_TRIANGLE;
                            aiTimer = 0;
                            ResetBoomerangRicochetTriangleState();
                            NPC.netUpdate = true;
                        }
                        break;
                    case 7:
                        // NEW WAVE 2: Curving Return Barrage - see WhoAmI_Pattern_YoyoBoomerangExtras2.cs
                        if (aiState != STATE_BOOMERANG_CURVING_RETURN)
                        {
                            aiState = STATE_BOOMERANG_CURVING_RETURN;
                            aiTimer = 0;
                            ResetBoomerangCurvingReturnState();
                            NPC.netUpdate = true;
                        }
                        break;
                }
            }
        }
    }
}