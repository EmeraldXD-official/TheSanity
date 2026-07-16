using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // GRAVITY WELL & ARCANE TORRENT — STATE_GRAVITY_WELL_TORRENT — WeaponArchetype.Magic
    // ================================================================================================
    // Space-manipulation attack:
    //   A) POSITION (ticks 0-25)   - boss floats to a spot diagonally ABOVE the player, bobbing the
    //                                 whole time (never static).
    //   B) RIFT CHANNEL (26-140)   - a spatial rift opens roughly at the player's position at the
    //                                 moment the channel starts; it exerts a continuous, moderate pull
    //                                 on the player's velocity for the rest of the state. While it's
    //                                 open the boss rains down curved, spiraling magic bolts
    //                                 (MathF.Cos-offset paths) from its hands.
    //   C) COLLAPSE (141-160)     - the rift snaps shut with a final burst, boss brakes hard, recovers.
    //
    // The pull is intentionally "moderate" (see RiftPullStrength) - a fight against it, not an
    // inescapable stunlock - since the brief calls for "a tight, tense movement dance", not a guaranteed
    // hit.
    //
    // ─── INTEGRATION CHECKLIST ─────────────────────────────────────────────────────────────────────
    //   1. WhoAmI.cs constants: STATE_GRAVITY_WELL_TORRENT = 12  ✔ done
    //   2. WhoAmI.cs AI() switch: case STATE_GRAVITY_WELL_TORRENT -> HandleGravityWellTorrent(player) ✔
    //   3. WhoAmI_Patterns.cs: Magic validPatterns gains index 5 (index 4 is already taken by the
    //      existing Phantom Mirage Cascade / STATE_MAGIC_SPIRAL_RIFT), ExecuteMagicPattern gains
    //      `case 5:` that enters this state.  ✔ done (see that file)
    // ================================================================================================
    public partial class WhoAmI
    {
        private Vector2 gravityWellRiftCenter = Vector2.Zero;
        private Vector2 gravityWellFloatSpot = Vector2.Zero;
        private int gravityWellBoltsFired = 0;
        private float gravityWellSpiralAngle = 0f;

        private const int GravityWellPositionEnd = 25;
        private const int GravityWellChannelEnd = 140;
        private const int GravityWellRecoverEnd = 160;
        private const float RiftPullStrength = 0.16f; // added toward rift center each tick, clamped below
        private const float RiftPullMaxSpeed = 6.5f;
        private const float RiftRadius = 500f; // beyond this the pull doesn't reach

        private void HandleGravityWellTorrent(Player target)
        {
            aiTimer++;
            NPC.damage = 0;

            if (aiTimer == 1)
            {
                float side = (NPC.Center.X < target.Center.X) ? -1f : 1f;
                gravityWellFloatSpot = target.Center + new Vector2(side * 260f, -220f);
                gravityWellBoltsFired = 0;
                gravityWellSpiralAngle = 0f;
            }

            // ---- A) POSITION ----
            if (aiTimer <= GravityWellPositionEnd)
            {
                Vector2 bobbed = gravityWellFloatSpot + GetSatSetBobOffset(1.2f, 18f);
                EaseVelocityTowards((bobbed - NPC.Center) * 0.35f, aiTimer / (float)GravityWellPositionEnd, EasingCurves.Sine, EasingType.InOut);

                if (aiTimer == GravityWellPositionEnd)
                {
                    gravityWellRiftCenter = target.Center;
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, gravityWellRiftCenter);
                    ScreenShakeSystem.StartShakeAtPoint(gravityWellRiftCenter, 10f, 0.3f);
                }
                return;
            }

            // Boss keeps a gentle bob at its float spot for the whole channel - "no static idle".
            Vector2 hoverTarget = gravityWellFloatSpot + GetSatSetBobOffset(1f, 14f);
            EaseVelocityTowards((hoverTarget - NPC.Center) * 0.25f, 1f, EasingCurves.Sine, EasingType.InOut, 0.6f);

            // ---- B) RIFT CHANNEL ----
            if (aiTimer <= GravityWellChannelEnd)
            {
                // Rift visual - inward-spiraling particles converging on the rift center.
                if (Main.rand.NextBool(2))
                {
                    float ringAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 ringPos = gravityWellRiftCenter + new Vector2((float)Math.Cos(ringAngle), (float)Math.Sin(ringAngle)) * RiftRadius * 0.9f;
                    Vector2 inward = gravityWellRiftCenter - ringPos;
                    if (inward != Vector2.Zero) inward.Normalize();
                    LuminanceUtilities.SpawnParticle(ringPos, inward * 3f, Color.DarkViolet, 20, 1.1f, ParticleType.Spark);
                }

                // Continuous, moderate pull on the player - clamped so it's resistible, not a stunlock.
                float distToRift = Vector2.Distance(target.Center, gravityWellRiftCenter);
                if (distToRift > 4f && distToRift < RiftRadius)
                {
                    Vector2 pull = gravityWellRiftCenter - target.Center;
                    pull.Normalize();
                    float falloff = 1f - (distToRift / RiftRadius); // stronger near the center
                    target.velocity += pull * RiftPullStrength * (0.5f + falloff);
                    if (target.velocity.Length() > RiftPullMaxSpeed)
                    {
                        Vector2 v = target.velocity;
                        v.Normalize();
                        target.velocity = v * RiftPullMaxSpeed;
                    }
                }

                // Curved, spiraling magic bolts fired from the boss's "hands" (offset either side of center).
                if ((aiTimer - GravityWellPositionEnd) % 20 == 0 && gravityWellBoltsFired < 8)
                {
                    gravityWellBoltsFired++;
                    gravityWellSpiralAngle += 0.7f;

                    for (int hand = -1; hand <= 1; hand += 2)
                    {
                        Vector2 spawnPos = NPC.Center + new Vector2(hand * 20f, 10f);
                        Vector2 baseDir = gravityWellRiftCenter - spawnPos;
                        if (baseDir != Vector2.Zero) baseDir.Normalize();

                        // MathF.Cos-based curl on the initial direction so the shot leaves on a spiral arc.
                        float curl = (float)Math.Cos(gravityWellSpiralAngle) * 0.5f * hand;
                        Vector2 curledDir = Vector2.Transform(baseDir, Matrix.CreateRotationZ(curl));

                        int dmg = isPhase2 ? 30 : 20;
                        int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, curledDir * (isPhase2 ? 9f : 7f), ProjectileID.PurpleLaser, dmg, 0f, proxySlot);
                        if (p >= 0 && p < Main.maxProjectiles)
                        {
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                            // ai[0] carries a spiral-curl rate that WhoAmIProjectileGuard style code could
                            // read to keep curving post-launch; left at 0 here (straight-line curl-on-exit)
                            // to avoid coupling this file to the projectile guard's internals.
                        }
                    }

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item72, NPC.Center);
                }
                return;
            }

            // ---- C) COLLAPSE ----
            if (aiTimer == GravityWellChannelEnd + 1)
            {
                ScreenShakeSystem.StartShakeAtPoint(gravityWellRiftCenter, 16f, 0.4f);
                for (int i = 0; i < 25; i++)
                    LuminanceUtilities.SpawnParticle(gravityWellRiftCenter, Main.rand.NextVector2Circular(6, 6), Color.White, 25, 1.3f, ParticleType.Spark);
            }

            ApplyBrakingImpulse(0.2f);
            if (aiTimer >= GravityWellRecoverEnd)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 80 : 110;
                NPC.netUpdate = true;
            }
        }
    }
}