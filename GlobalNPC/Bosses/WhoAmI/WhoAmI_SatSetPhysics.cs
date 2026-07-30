using Microsoft.Xna.Framework;
using System;
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
            if (aiState == STATE_DASH_ATTACK || aiState == STATE_DODGE || aiState == STATE_PREDICTIVE_DODGE || aiState == STATE_BLINK_ECHO_COMBO
                || aiState == STATE_WHIP_LASH_CAGE || aiState == STATE_BOOMERANG_CROSSFIRE || aiState == STATE_MIRROR_MIRAGE)
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
    }
}