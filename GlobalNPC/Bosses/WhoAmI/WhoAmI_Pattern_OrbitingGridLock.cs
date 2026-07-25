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
    // ORBITING GRID LOCK — STATE_ORBIT_GRID_LOCK — WeaponArchetype.Ranged
    // ================================================================================================
    // A zoning hazard that forces precise movement:
    //   A) FAST ORBIT (ticks 0-59)     - boss circles the player aggressively at medium range while
    //                                    three "holographic clone" marker points lock into a triangle
    //                                    layout around the player (no new textures - vanilla particles
    //                                    scaled/rotated stand in for the clone silhouettes).
    //   B) GRID LOCK (tick 60)          - the three markers connect with laser lines (drawn as dense
    //                                    particle streams every frame), forming a triangular cage.
    //   C) BARRAGE (61-140)            - the boss, now OUTSIDE the triangle, fires a high-velocity
    //                                    projectile barrage inward, forcing the player to dodge inside
    //                                    the confined grid instead of just running away.
    //   D) RECOVER (141-160)
    //
    // ─── INTEGRATION CHECKLIST ─────────────────────────────────────────────────────────────────────
    //   1. WhoAmI.cs constants: STATE_ORBIT_GRID_LOCK = 11  ✔ done
    //   2. WhoAmI.cs AI() switch: case STATE_ORBIT_GRID_LOCK -> HandleOrbitingGridLock(player)  ✔ done
    //   3. WhoAmI_Patterns.cs: Ranged validPatterns gains index 4, ExecuteRangedPattern gains
    //      `case 4:` that enters this state.  ✔ done (see that file)
    // ================================================================================================
    public partial class WhoAmI
    {
        private Vector2[] gridLockMarkers = new Vector2[3];
        private Vector2 gridLockOrbitAnchor = Vector2.Zero;
        private float gridLockOrbitAngle = 0f;
        private int gridLockBarrageShotsFired = 0;

        private const int GridLockOrbitDuration = 60;
        private const int GridLockLockTick = 60;
        private const int GridLockBarrageEnd = 140;
        private const int GridLockRecoverEnd = 160;
        private const float GridLockOrbitRadius = 520f;
        private const float GridLockTriangleRadius = 260f; // how far the 3 markers sit from the player

        private void HandleOrbitingGridLock(Player target)
        {
            aiTimer++;
            NPC.damage = 0;

            if (aiTimer == 1)
            {
                gridLockOrbitAnchor = target.Center;
                gridLockOrbitAngle = (NPC.Center - target.Center).ToRotation();
                gridLockBarrageShotsFired = 0;

                // Triangle layout, one vertex pointing "up" (toward the boss's current side).
                for (int i = 0; i < 3; i++)
                {
                    float angle = MathHelper.PiOver2 * -1f + i * (MathHelper.TwoPi / 3f);
                    gridLockMarkers[i] = target.Center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * GridLockTriangleRadius;
                }
            }

            // ---- A) FAST ORBIT ----
            if (aiTimer <= GridLockOrbitDuration)
            {
                // Anchor follows the player slowly so the orbit doesn't detach if they walk away, but
                // the orbit itself is fast and wide, per the brief.
                gridLockOrbitAnchor = Vector2.Lerp(gridLockOrbitAnchor, target.Center, 0.05f);
                gridLockOrbitAngle += isPhase2 ? 0.09f : 0.065f;

                Vector2 orbitPos = GetOrbitalCrawlPosition(gridLockOrbitAnchor, GridLockOrbitRadius, gridLockOrbitAngle);
                EaseVelocityTowards((orbitPos - NPC.Center) * 0.4f, aiTimer / (float)GridLockOrbitDuration, EasingCurves.Sine, EasingType.InOut);

                if (NPC.velocity.Length() > 1f && Main.rand.NextBool(2))
                    LuminanceUtilities.SpawnParticle(NPC.Center, -NPC.velocity * 0.15f, Color.Cyan, 20, 1f, ParticleType.Spark);

                // Holographic clone markers materialize progressively over the orbit window.
                float lockProgress = aiTimer / (float)GridLockOrbitDuration;
                int markersShown = (int)MathHelper.Clamp(lockProgress * 3f + 1f, 1, 3);
                for (int i = 0; i < markersShown; i++)
                {
                    // Rotate/scale a simple particle cluster to fake a "holographic weapon clone" silhouette
                    // without any new art assets, per the requirement to reuse vanilla-safe visuals only.
                    float spin = Main.GlobalTimeWrappedHourly * 3f + i;
                    for (int r = 0; r < 4; r++)
                    {
                        float a = spin + r * MathHelper.PiOver2;
                        Vector2 p = gridLockMarkers[i] + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * 18f;
                        LuminanceUtilities.SpawnParticle(p, Vector2.Zero, Color.DeepSkyBlue * 0.85f, 8, 0.6f, ParticleType.Spark);
                    }
                }

                if (aiTimer == GridLockOrbitDuration)
                    ScreenShakeSystem.StartShakeAtPoint(target.Center, 10f, 0.3f);

                return;
            }

            // ---- B) GRID LOCK (laser lines connecting the 3 markers) ----
            // Drawn continuously through the barrage phase too, so the cage stays visible the whole time.
            if (aiTimer >= GridLockLockTick && aiTimer <= GridLockBarrageEnd)
            {
                DrawGridLockLine(gridLockMarkers[0], gridLockMarkers[1]);
                DrawGridLockLine(gridLockMarkers[1], gridLockMarkers[2]);
                DrawGridLockLine(gridLockMarkers[2], gridLockMarkers[0]);
            }

            // ---- C) BARRAGE (boss parked just outside the triangle, firing inward) ----
            if (aiTimer > GridLockLockTick && aiTimer <= GridLockBarrageEnd)
            {
                Vector2 outsidePos = target.Center + Vector2.Normalize(NPC.Center - target.Center == Vector2.Zero ? -Vector2.UnitY : NPC.Center - target.Center) * (GridLockTriangleRadius + 300f);
                outsidePos += GetSatSetBobOffset(1.5f, 20f); // never fully static while parked

                EaseVelocityTowards((outsidePos - NPC.Center) * 0.3f, 1f, EasingCurves.Sine, EasingType.InOut);

                if ((aiTimer - GridLockLockTick) % 14 == 0 && gridLockBarrageShotsFired < 12)
                {
                    gridLockBarrageShotsFired++;
                    Vector2 shootDir = target.Center - NPC.Center;
                    if (shootDir != Vector2.Zero) shootDir.Normalize();

                    int dmg = isPhase2 ? 34 : 22;
                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, shootDir * (isPhase2 ? 15f : 12f), ProjectileID.PurpleLaser, dmg, 0f, proxySlot);
                    if (p >= 0 && p < Main.maxProjectiles) { Main.projectile[p].hostile = true; Main.projectile[p].friendly = false; }

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item12, NPC.Center);
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 6f, 0.15f);
                }
                return;
            }

            // ---- D) RECOVER ----
            ApplyBrakingImpulse(0.2f);
            if (aiTimer >= GridLockRecoverEnd)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 70 : 100;
                NPC.netUpdate = true;
            }
        }

        private void DrawGridLockLine(Vector2 from, Vector2 to)
        {
            float len = Vector2.Distance(from, to);
            Vector2 dir = (to - from);
            if (dir == Vector2.Zero) return;
            dir.Normalize();

            for (float d = 0f; d < len; d += 30f)
            {
                if (Main.rand.NextBool(2))
                    LuminanceUtilities.SpawnParticle(from + dir * d, Vector2.Zero, Color.Cyan * 0.6f, 6, 0.5f, ParticleType.Spark);
            }
        }
    }
}