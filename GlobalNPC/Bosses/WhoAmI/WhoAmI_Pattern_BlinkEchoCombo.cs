using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // BLINK & ECHO COMBO — STATE_BLINK_ECHO_COMBO — WeaponArchetype.TrueMelee / ProjMelee
    // ================================================================================================
    // A rhythm-based melee assault:
    //   A) TELEGRAPH (ticks 0-14)     - a sharp line flashes from the boss to a point BEHIND the
    //                                   player (relative to their predicted facing), warning exactly
    //                                   where the blink is going to land.
    //   B) BLINK + ECHO (tick 15)     - instant reposition (ExecuteSpeedCanceledTeleport / ApplySnapDash
    //                                   for the final short hop) leaving 3 fading echo ghosts sampled
    //                                   along the path.
    //   C) SWEEPING SLASH (16-26)    - a fast arc melee hitbox. While this window is open, isParrying
    //                                   is armed so a player hit during the active frames triggers the
    //                                   existing STATE_COUNTER_ATTACK riposte (see WhoAmI.cs).
    //   D) RECOVER (27-34)           - high-damping brake to a dead stop, then back to STATE_IDLE.
    //
    // MOVEMENT: zig-zag micro-steering between the telegraph and the blink via EaseVelocityTowards,
    // snap-dash for the final approach, and a 0.15f brake on landing - all per the sat-set rules.
    //
    // ─── INTEGRATION CHECKLIST ─────────────────────────────────────────────────────────────────────
    //   1. WhoAmI.cs constants block: STATE_BLINK_ECHO_COMBO = 10  ✔ done
    //   2. WhoAmI.cs AI() switch: case STATE_BLINK_ECHO_COMBO -> HandleBlinkEchoCombo(player)  ✔ done
    //   3. WhoAmI_Patterns.cs: TrueMelee & ProjMelee validPatterns gain index 4, ExecuteTrueMeleePattern
    //      / ExecuteProjMeleePattern gain `case 4:` that enters this state.  ✔ done (see that file)
    // ================================================================================================
    public partial class WhoAmI
    {
        private Vector2 blinkEchoTelegraphPoint = Vector2.Zero;
        private Vector2 blinkEchoLandingPoint = Vector2.Zero;
        private bool blinkEchoHasBlinked = false;

        private const int BlinkEchoTelegraphDuration = 14;
        private const int BlinkEchoSlashStart = 16;
        private const int BlinkEchoSlashEnd = 26;
        private const int BlinkEchoRecoverEnd = 34;
        private const float BlinkEchoDashSpeed = 46f; // snap-dash speed for the final short hop into range

        private void HandleBlinkEchoCombo(Player target)
        {
            aiTimer++;
            NPC.damage = 0;

            // ---- A) TELEGRAPH ----
            if (aiTimer <= BlinkEchoTelegraphDuration)
            {
                if (aiTimer == 1)
                {
                    // Land BEHIND the player relative to where they're currently moving/facing, so the
                    // slash naturally reads as an ambush rather than a frontal charge.
                    Vector2 facing = target.velocity.Length() > 1f ? Vector2.Normalize(target.velocity) : new Vector2(target.direction, 0f);
                    blinkEchoLandingPoint = target.Center + facing * 90f;
                    blinkEchoTelegraphPoint = target.Center - facing * 60f;
                    blinkEchoHasBlinked = false;
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                }

                // Zig-zag micro-steering toward the telegraph anchor - never a straight, static line.
                Vector2 zigzag = blinkEchoTelegraphPoint + new Vector2((float)Math.Sin(aiTimer * 0.9f) * 40f, 0f);
                Vector2 desired = (zigzag - NPC.Center) * 0.3f;
                EaseVelocityTowards(desired, aiTimer / (float)BlinkEchoTelegraphDuration, EasingCurves.Quadratic, EasingType.In);

                // Telegraph line flashing from the boss to the landing point.
                if (aiTimer % 2 == 0)
                {
                    Vector2 dir = blinkEchoLandingPoint - NPC.Center;
                    float len = dir.Length();
                    if (len > 0f)
                    {
                        dir.Normalize();
                        for (float d = 0f; d < len; d += 24f)
                            LuminanceUtilities.SpawnParticle(NPC.Center + dir * d, Vector2.Zero, Color.OrangeRed * 0.8f, 10, 0.7f, ParticleType.Spark);
                    }
                }

                if (aiTimer == BlinkEchoTelegraphDuration)
                    ScreenShakeSystem.StartShakeAtPoint(blinkEchoLandingPoint, 8f, 0.25f);

                return;
            }

            // ---- B) BLINK + ECHO ----
            if (!blinkEchoHasBlinked)
            {
                blinkEchoHasBlinked = true;

                // Long hop uses the speed-canceled teleport (leaves echo clones along the way); a short
                // hop instead gets a literal snap-dash so it still feels instantaneous either way.
                if (!ExecuteSpeedCanceledTeleport(blinkEchoLandingPoint))
                {
                    ApplySnapDash(blinkEchoLandingPoint - NPC.Center, BlinkEchoDashSpeed);
                    NPC.Center += NPC.velocity; // consume the snap immediately - this IS the blink frame
                }

                // 3 fading echo ghosts along the traveled path, independent of the teleport's own trail.
                for (int i = 1; i <= 3; i++)
                {
                    Vector2 echoPos = Vector2.Lerp(blinkEchoTelegraphPoint, blinkEchoLandingPoint, i / 4f);
                    LuminanceUtilities.SpawnParticle(echoPos, Vector2.Zero, Color.MediumPurple * (1f - i * 0.2f), 22, 1.1f, ParticleType.Spark);
                }

                NPC.direction = (target.Center.X < NPC.Center.X) ? -1 : 1;
                NPC.netUpdate = true;
            }

            // ---- C) SWEEPING SLASH ----
            if (aiTimer >= BlinkEchoSlashStart && aiTimer <= BlinkEchoSlashEnd)
            {
                if (aiTimer == BlinkEchoSlashStart)
                {
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
                    isParrying = true; // active frames - a player hit here triggers the existing counter/riposte
                    bossWeaponSwingTimer = bossWeaponSwingMax;
                }

                NPC.damage = isPhase2 ? 60 : 40;

                float sweep = (aiTimer - BlinkEchoSlashStart) / (float)(BlinkEchoSlashEnd - BlinkEchoSlashStart);
                float arcAngle = MathHelper.Lerp(-MathHelper.PiOver4, MathHelper.PiOver4, sweep) * NPC.direction;
                Vector2 tipOffset = new Vector2((float)Math.Cos(arcAngle), (float)Math.Sin(arcAngle)) * 60f;
                LuminanceUtilities.SpawnParticle(NPC.Center + tipOffset, Vector2.Zero, Color.White, 8, 0.6f, ParticleType.Spark);

                // Sharp braking during the strike itself - the boss "plants" through the swing.
                ApplyBrakingImpulse(0.5f);

                if (aiTimer == BlinkEchoSlashEnd)
                {
                    isParrying = false;
                    NPC.damage = 0;
                }
                return;
            }

            // ---- D) RECOVER ----
            ApplyBrakingImpulse(0.15f); // instant stop-and-go inertia per the sat-set brief
            if (aiTimer >= BlinkEchoRecoverEnd)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 60 : 90;
                NPC.netUpdate = true;
            }
        }
    }
}