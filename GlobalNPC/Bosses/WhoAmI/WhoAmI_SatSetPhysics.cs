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
    // "SAT SET" PHYSICS OVERHAUL — shared movement primitives used by every new attack pattern
    // (WhoAmI_Pattern_BlinkEchoCombo.cs, _OrbitingGridLock.cs, _GravityWellTorrent.cs, _MirrorMirage.cs)
    // and lightly wired into the existing idle movement (see ExecuteSmoothMovement in WhoAmI_Helpers.cs).
    // ================================================================================================
    // Rules implemented here, matching the design brief 1:1:
    //   1. PREDICTIVE INTERCEPTION - GetPredictiveInterceptPoint()
    //   2. SNAP DASHES & HIGH-DAMPING BRAKES - ApplySnapDash() / ApplyBrakingImpulse()
    //   3. NO STATIC IDLE - GetSatSetBobOffset() (micro bobbing / tight orbital crawl)
    //   4. SPEED-CANCELED TELEPORTS (>800 units) - ExecuteSpeedCanceledTeleport()
    //   5. PROJECTILE SIDE-STEPPING - HandleProjectileSideStep()
    //   6. MINIMUM CASTING DISTANCE - EnforceMinimumCastingDistance()
    //   7. NO STATIC IDLE SAFETY NET (whole-fight) - EnforceNoStaticIdle()
    // ================================================================================================
    public partial class WhoAmI
    {
        // ---------------------------------------------------------------------------------------
        // 1) PREDICTIVE INTERCEPTION
        // ---------------------------------------------------------------------------------------
        // Reads the player's CURRENT velocity and projects it forward by `leadTicks` frames, so any
        // caller steering toward this point is aiming where the player is GOING, not where they ARE.
        private Vector2 GetPredictiveInterceptPoint(Player target, float leadTicks = 20f)
        {
            return target.Center + target.velocity * leadTicks;
        }

        // ---------------------------------------------------------------------------------------
        // 2) SNAP DASHES & HIGH-DAMPING BRAKES
        // ---------------------------------------------------------------------------------------
        // Instantly sets velocity to full dash speed in the given direction (1-frame ramp-up, no
        // easing) - used at the START of a dash/blink/lunge sub-phase.
        private void ApplySnapDash(Vector2 direction, float dashSpeed)
        {
            if (direction != Vector2.Zero) direction.Normalize();
            NPC.velocity = direction * dashSpeed;
            NPC.netUpdate = true;
        }

        // Slams velocity down toward zero (default 0.15x, per the brief) - used at the END of a dash
        // or immediately after an attack connects, to simulate sudden stop-and-go inertia.
        private void ApplyBrakingImpulse(float dampingFactor = 0.15f)
        {
            NPC.velocity *= dampingFactor;
        }

        // ---------------------------------------------------------------------------------------
        // 3) NO STATIC IDLE
        // ---------------------------------------------------------------------------------------
        // Smooth figure-eight-ish micro movement to layer on top of an otherwise-fixed telegraph
        // position, so the boss never reads as "frozen" during windups.
        private Vector2 GetSatSetBobOffset(float speed = 1f, float amplitude = 14f)
        {
            float t = Main.GlobalTimeWrappedHourly * speed;
            return new Vector2((float)Math.Sin(t * 2.3f) * amplitude, (float)Math.Cos(t * 1.7f) * amplitude * 0.6f);
        }

        // A tight orbital crawl around an arbitrary anchor (player, telegraph point, etc.) - used by
        // Orbiting Grid Lock and Gravity Well while they're "parked" doing something else (channeling,
        // firing) so they still visibly drift instead of standing dead still.
        private Vector2 GetOrbitalCrawlPosition(Vector2 anchor, float radius, float angleRadians)
        {
            return anchor + new Vector2((float)Math.Cos(angleRadians), (float)Math.Sin(angleRadians)) * radius;
        }

        // ---------------------------------------------------------------------------------------
        // 4) SPEED-CANCELED TELEPORTS (long range only)
        // ---------------------------------------------------------------------------------------
        // Cooldown is separate from the existing ExecuteGlitchTeleport's teleportCooldownTimer so this
        // doesn't compete with / get starved by the older long-range-escape teleport logic.
        private int satSetBlinkCooldownTimer = 0;
        private const float SatSetBlinkDistanceThreshold = 800f;

        // If the boss is farther than SatSetBlinkDistanceThreshold from `destination`, instantly blink
        // there instead of flying the distance, leaving a short trail of fading silhouette clones
        // sampled from NPC.oldPos so the motion still reads clearly on screen.
        private bool ExecuteSpeedCanceledTeleport(Vector2 destination)
        {
            if (satSetBlinkCooldownTimer > 0) return false;
            if (Vector2.Distance(NPC.Center, destination) < SatSetBlinkDistanceThreshold) return false;

            // Sample the trail BEFORE moving, so the clones mark the path the boss "would have" taken.
            for (int i = 0; i < NPC.oldPos.Length; i += 2)
            {
                Vector2 clonePos = Vector2.Lerp(NPC.oldPos[i], destination, 0.5f);
                LuminanceUtilities.SpawnParticle(clonePos + NPC.Size * 0.5f, Vector2.Zero, Color.MediumPurple * 0.6f, 18, 1.4f, ParticleType.Spark);
            }

            NPC.Center = destination;
            NPC.velocity = Vector2.Zero;
            satSetBlinkCooldownTimer = 12; // short - this is a repositioning tool, not an escape cooldown
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item92, NPC.Center);
            for (int i = 0; i < 14; i++)
                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(4, 4), Color.White, 20, 1f, ParticleType.Spark);
            NPC.netUpdate = true;
            return true;
        }

        private void TickSatSetTimers()
        {
            if (satSetBlinkCooldownTimer > 0) satSetBlinkCooldownTimer--;
        }

        // ---------------------------------------------------------------------------------------
        // 6) MINIMUM CASTING DISTANCE ("jangan nempel pas nge-cast/nembak")
        // ---------------------------------------------------------------------------------------
        // Every ranged/magic pattern already steers toward a hover/orbit spot well outside this
        // radius (180-420px - see GravityWellTorrent, OrbitingGridLock, AureolaSignetRain, etc.),
        // but that steering is just a soft EaseVelocityTowards goal, not a hard constraint - if the
        // player rushes the boss while it's mid-cast, nothing was pushing back, so the boss could end
        // up standing right on top of the player during what's supposed to be a ranged/magic attack.
        // This is a safety-net repel: only kicks in below MinCastingDistance, which is comfortably
        // closer than every pattern's own intended stand-off distance, so it never fights normal
        // pattern positioning - it only ever engages when something (usually the player closing the
        // gap) has pushed the boss inside that floor.
        private const float MinCastingDistance = 150f;
        private const float MinCastingDistancePhase2 = 230f; // phase 2 asks for extra breathing room, per the wider ExecuteSmoothMovement standoff (see WhoAmI_Helpers.cs)
        private const float CastingDistancePushStrength = 3.2f;
        private const float CastingDistancePushStrengthPhase2 = 4.5f; // pushes off harder too, so the floor actually holds against the faster phase-2 chase speed

        // States where the boss is meant to be casting/shooting from range, not brawling in melee -
        // melee-archetype states (BlinkEchoCombo, MeleeCombo, DashAttack, WhipLashCage, AbyssalCleave,
        // etc.) are deliberately excluded since closing distance IS the point of those attacks.
        private static readonly HashSet<int> RangedOrMagicCastStates = new HashSet<int>
        {
            STATE_RANGED_BARRAGE,
            STATE_MAGIC_SPIRAL_RIFT,
            STATE_GRAVITY_WELL_TORRENT,
            STATE_ORBIT_GRID_LOCK,
            STATE_AUREOLA_SIGNET_RAIN,
            STATE_DOUBLE_HELIX_SWEEP,
            STATE_QUANTUM_GLITCH_PHASING,
            STATE_VECTOR_LASER_GRID,
            STATE_HOMING_CLUSTER_COMET,
            STATE_SINGULARITY_OVERDRIVE,
            // Second-wave Ranged/Magic trio (WhoAmI_Pattern_RangedArchetypeExtras2.cs /
            // WhoAmI_Pattern_MagicArchetypeExtras2.cs) - same reasoning as the rest of this set,
            // these are all cast-from-range patterns that shouldn't end up resolving point-blank.
            STATE_RANGED_PARALLAX_VOLLEY,
            STATE_RANGED_MIRROR_RICOCHET,
            STATE_RANGED_STARFALL_CONVERGENCE,
            STATE_MAGIC_FRACTURE_BLOOM,
            STATE_MAGIC_UMBRAL_DUALITY,
            STATE_MAGIC_PARADOX_MIRROR,
        };

        private void EnforceMinimumCastingDistance(Player target)
        {
            if (!RangedOrMagicCastStates.Contains(aiState)) return;

            float minDist = isPhase2 ? MinCastingDistancePhase2 : MinCastingDistance;
            float pushStrength = isPhase2 ? CastingDistancePushStrengthPhase2 : CastingDistancePushStrength;

            float dist = Vector2.Distance(NPC.Center, target.Center);
            if (dist >= minDist || dist < 1f) return;

            Vector2 away = NPC.Center - target.Center;
            away.Normalize();

            // Additive nudge, not a hard snap - stacks with whatever EaseVelocityTowards the pattern
            // is already doing that tick, so it reads as "getting pushed off" rather than teleporting.
            NPC.velocity += away * pushStrength;
            NPC.netUpdate = true;
        }

        // ---------------------------------------------------------------------------------------
        // 5) PROJECTILE SIDE-STEPPING
        // ---------------------------------------------------------------------------------------
        // Distinct from the existing HandleReactiveDodging (which triggers a full STATE_DODGE /
        // STATE_PREDICTIVE_DODGE state change at 180px). This is a lighter-weight in-place micro-dash
        // that only nudges velocity sideways and optionally shaves time off the current attack timer
        // to let a counter-attack come out sooner - it does NOT change aiState, so it layers safely
        // underneath whatever the boss is currently doing.
        private int sideStepCooldownTimer = 0;
        private const float SideStepTriggerRange = 250f;
        private const float SideStepSpeed = 9f;

        private void HandleProjectileSideStep(Player target)
        {
            if (sideStepCooldownTimer > 0) { sideStepCooldownTimer--; return; }

            // Don't fight with states that already own the boss's velocity completely this tick.
            // AUDIT FIX: this list only ever named a handful of the older patterns - every pattern
            // added since (GravityWellTorrent, OrbitingGridLock, MirrorLanceRupture, AbyssalCleave,
            // OrbitingBladeRing, DimensionalPierce, VectorLaserGrid, HomingClusterComet,
            // SingularityOverdrive, AureolaSignetRain, DoubleHelixSweep, QuantumGlitchPhasing,
            // SummonRiftSwarm, YoyoTetherStorm) also drives NPC.velocity directly every tick and was
            // missing here, so a nearby projectile could still shove the boss sideways AND fast-forward
            // its aiTimer (see below) out from under whatever timing that pattern was relying on. Only
            // STATE_IDLE genuinely has no one else owning velocity this tick, so gate on that instead
            // of maintaining a hand-picked list that new patterns keep falling through.
            if (aiState != STATE_IDLE)
                return;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile proj = Main.projectile[i];
                if (!proj.active || !proj.friendly || proj.hostile || proj.damage <= 0) continue;

                float dist = Vector2.Distance(NPC.Center, proj.Center);
                if (dist >= SideStepTriggerRange) continue;

                Vector2 toBoss = NPC.Center - proj.Center;
                if (Vector2.Dot(proj.velocity, toBoss) <= 0) continue; // only care about incoming, not passing-by

                Vector2 perpendicular = new Vector2(-proj.velocity.Y, proj.velocity.X);
                if (perpendicular != Vector2.Zero) perpendicular.Normalize();
                if (Vector2.Dot(perpendicular, toBoss) < 0) perpendicular = -perpendicular; // step away from the line, not into it

                NPC.velocity += perpendicular * SideStepSpeed;
                sideStepCooldownTimer = 18;

                // Shave time off whatever windup/attack timer is currently running so a counter can
                // come out sooner - only meaningful for states that count UP toward a threshold (all
                // the new pattern states do); harmless no-op otherwise since aiTimer just advances faster.
                if (aiTimer > 6) aiTimer += 6;

                LuminanceUtilities.SpawnParticle(NPC.Center, perpendicular * 3f, Color.Silver, 14, 0.9f, ParticleType.Spark);
                NPC.netUpdate = true;
                break;
            }
        }

        // ---------------------------------------------------------------------------------------
        // 7) NO STATIC IDLE - WHOLE-FIGHT SAFETY NET ("ga kaku" pass)
        // ---------------------------------------------------------------------------------------
        // Rule 3 above (GetSatSetBobOffset) is opt-in - every NEW pattern added since the sat-set
        // overhaul calls it deliberately. The much OLDER "inline" pattern bodies in
        // WhoAmI_Patterns.cs (index 0-3 on every archetype - ExecuteTrueMeleePattern's spin-slash
        // burst, ExecuteRangedPattern's stand-and-fire barrage, ExecuteMagicPattern's channel, etc.)
        // predate that rule entirely and, for several of their "stand still and burst out an attack"
        // cases, never touch NPC.velocity at all for 60-140 ticks straight - the boss just parks
        // wherever it happened to stop and reads as a frozen statue mid-fight. That's the single
        // biggest source of the "kaku" (stiff) feeling across a full run, way more than any one
        // pattern's own movement code, simply because those inline patterns are picked constantly
        // (they're 4 of the ~8-11 weighted choices for every archetype).
        //
        // Rather than hand-editing every one of those ~30 stationary cases individually (each one
        // is a small, easy-to-miss omission spread across a 2000+ line file), this is a blanket
        // safety net: it only ever engages once NPC.velocity has ALREADY decayed to a near-stop, and
        // it only ever ADDS a gentle, continuously-rotating drift on top - it never overrides or
        // fights a pattern that's already actively steering (BlinkEchoCombo's telegraph zig-zag,
        // GravityWellTorrent's hover, the ExecuteSmoothMovement idle chase, etc. all keep
        // NPC.velocity comfortably above the threshold on their own, so this never even triggers
        // for them). Excluded states are ones where a hard stop is either already handled by their
        // own dedicated movement (DODGE / PREDICTIVE_DODGE / DASH_ATTACK) or where standing dead
        // still is the deliberate readable tell (PARRY_STANCE - the wind-up itself IS the telegraph).
        private float noStaticIdleDriftAngle = 0f;
        private const float NoStaticIdleSpeedThreshold = 0.5f; // below this, the boss counts as "frozen"
        private const float NoStaticIdleNudgeStrength = 1.15f;

        private void EnforceNoStaticIdle()
        {
            if (aiState == STATE_DODGE || aiState == STATE_PREDICTIVE_DODGE ||
                aiState == STATE_DASH_ATTACK || aiState == STATE_PARRY_STANCE)
                return;

            if (NPC.velocity.Length() >= NoStaticIdleSpeedThreshold) return;

            // Slow, continuously-advancing rotation rather than a fixed direction, so a boss that's
            // frozen for a long stretch drifts in a lazy circle instead of just crawling off in a
            // straight line - reads as "breathing"/alive rather than as a bugged slide.
            noStaticIdleDriftAngle += 0.045f;
            Vector2 drift = new Vector2(
                (float)Math.Cos(noStaticIdleDriftAngle),
                (float)Math.Sin(noStaticIdleDriftAngle * 1.3f) * 0.6f) * NoStaticIdleNudgeStrength;

            NPC.velocity += drift * 0.1f;

            // Faint ambient wisp so the nudge also reads visually, not just as a physics tweak -
            // sparse (1-in-6) and small so it never competes with a pattern's own VFX.
            if (Main.rand.NextBool(6))
                LuminanceUtilities.SpawnParticle(NPC.Center + Main.rand.NextVector2Circular(18f, 18f), drift * 0.3f, Color.MediumPurple * 0.5f, 16, 0.6f, ParticleType.Spark);
        }

        // ---------------------------------------------------------------------------------------
        // 8) PLAYER PULL VELOCITY CLAMP ("player-nya yang ngebug gegara boss")
        // ---------------------------------------------------------------------------------------
        // Any pattern that pulls the player toward a point (rift, orb, tether...) does it by
        // adding to `target.velocity` every tick. GravityWellTorrent (this file's own sibling
        // pattern) already clamps its own pull inline via RiftPullMaxSpeed - but
        // HandleSingularityOverdrive (WhoAmI_Pattern_RangedArchetypeExtras.cs) pulls with NO
        // falloff and NO cap for 100-130 ticks straight, and HandleSummonSoulTether
        // (WhoAmI_Pattern_SummonWhipExtras2.cs) adds a hard 3.2-strength "snap" pull from up to 3
        // tethered minions at once, several times per cast, ALSO uncapped. Both just keep adding
        // to target.velocity tick after tick with nothing ever pulling it back down, so it's easy
        // for the player's own velocity to compound into the tens-of-pixels-per-tick range over a
        // couple of seconds - and since the PLAYER (unlike this boss) has normal tile collision,
        // slamming into a wall at that speed is exactly what shows up as "ngebug": clipping,
        // violent bounce-back, getting stuck vibrating in a corner, or rubber-banding. This is a
        // player-mounted velocity clamp for exactly that class of pull - call it once, right after
        // any `target.velocity += pull * ...` line, so the pull still reads as a firm yank but can
        // never runaway into something the player's own collision has to violently resolve.
        private void ClampPlayerPullVelocity(Player target, float maxSpeed)
        {
            if (target.velocity.Length() > maxSpeed)
                target.velocity = target.velocity.SafeNormalize(Vector2.Zero) * maxSpeed;
        }
    }
}