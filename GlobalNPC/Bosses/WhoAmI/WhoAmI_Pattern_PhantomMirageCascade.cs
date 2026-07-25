using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

// ================================================================================================
// PHANTOM MIRAGE CASCADE — WeaponArchetype.Magic
// ================================================================================================
//
// Concept: Three-stage sequence that feels deliberate and cinematic.
//
//   A) LEMNISCATE ORBIT (ticks 1–44)
//      Boss traces a figure-8 path anchored on the player's position at state entry,
//      streaming a violet-to-white particle trail. Mid-orbit a sound spike + light shake
//      telegraphs what's coming. At tick 42 a bigger shake announces the lock-on.
//
//   B) PREDICTIVE SALVO (ticks 44–60)
//      At tick 44 the boss reads the player's current velocity and stamps "vortexAnchor"
//      ~28 ticks ahead. Orange warning particles converge on that predicted position.
//      At tick 52 a tight fan of projectiles fires toward the stamp, with a screen impact
//      shake and a short recoil kick that pushes the boss backward. At tick 60 it glitch-
//      teleports to the opposite diagonal flank to set up the final sweep.
//
//   C) SWEEPING ARC ECHO (ticks 61–135)
//      From the new flank, the boss weaves left-right perpendicular to its line-of-sight
//      while keeping an ideal range of ~480-550 px. Every 12 ticks it fires a rotating fan
//      burst whose base angle advances 28–35° each time (so successive bursts cover different
//      arcs — no static pattern). Phase 2 adds a second glitch-teleport mid-sweep and widens
//      the fan. A climax shake at tick 130 signals the end of the state.
//
// MOVEMENT RULES FOLLOWED:
//   - Sub-phase A uses parametric lemniscate + micro-bob → never static.
//   - All steering goes through EaseVelocityTowards (respects MaxSteeredVelocity clamp).
//   - Phase transitions use velocity *= 0.88f friction instead of instant zero.
//
// ─── INTEGRATION CHECKLIST (4 spots) — ALL APPLIED ────────────────────────────────────────────
//
//   1. WhoAmI.cs  → constant block (near STATE_PREDICTIVE_DODGE = 8):
//        ✔ DONE:  private const int STATE_MAGIC_SPIRAL_RIFT = 9;
//        (declared there, not in this file, to avoid a duplicate-member error across
//        this partial class's files)
//
//   2. WhoAmI.cs  → main AI() switch (after case STATE_PREDICTIVE_DODGE):
//        ✔ DONE:
//            case STATE_MAGIC_SPIRAL_RIFT:
//                HandleMagicSpiralRift(player);
//                break;
//
//   3. WhoAmI_Patterns.cs → SelectAndExecuteArchetypePattern → case WeaponArchetype.Magic:
//        ✔ DONE: validPatterns = hasProjectile ? new List<int> { 0, 1, 2, 3, 4 } : new List<int> { 3 };
//
//   4. WhoAmI_Patterns.cs → ExecuteMagicPattern → case 4 added in BOTH the Phase 2 and
//      Phase 1 switch blocks.
//        ✔ DONE
//
// ================================================================================================

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    public partial class WhoAmI
    {
        // NOTE: STATE_MAGIC_SPIRAL_RIFT is declared in WhoAmI.cs next to the other STATE_*
        // constants (see integration checklist above) — not redeclared here, since this class
        // is a `partial class` split across files and a second `private const` with the same
        // name would be a duplicate-member compile error (CS0102).

        // ── Sub-phase timestamps (all in ticks; aiTimer starts at 1 on first active frame) ───────
        private const int PmcOrbitEnd      = 44;   // last tick of lemniscate orbit
        private const int PmcPredictTick   = 44;   // mark predicted position + warning VFX
        private const int PmcStrikeTick    = 52;   // release predictive salvo
        private const int PmcTeleport1     = 60;   // glitch-teleport to opposite diagonal flank
        private const int PmcSweepEnd      = 135;  // final tick — exit to STATE_IDLE
        private const int PmcSweepInterval = 12;   // fan burst cadence during sweep

        // ─────────────────────────────────────────────────────────────────────────────────────────
        //  MAIN HANDLER — called from the AI() switch every tick while in STATE_MAGIC_SPIRAL_RIFT
        // ─────────────────────────────────────────────────────────────────────────────────────────
        private void HandleMagicSpiralRift(Player target)
        {
            // Safety exits — should never trigger mid-fight, but keeps things crash-free.
            if (target == null || !target.active || target.dead)
            {
                aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return;
            }
            if (!WeaponHasProjectile(activeWeapon))
            {
                aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return;
            }

            // ── SUB-PHASE A: LEMNISCATE ORBIT (ticks 1 – PmcOrbitEnd) ────────────────────────────
            //
            // On the very first tick (aiTimer == 1), capture the player's current position as the
            // orbit anchor. We intentionally do NOT update this each tick — as the player moves
            // during the orbit, the figure-8 "drifts" out of perfect center, giving the boss a
            // sense of mass and commitment rather than being a perfect magnet.

            if (aiTimer == 1)
            {
                flickerFlankStart = target.Center;

                // Seed the fan-rotation tracker with a deterministic-random offset so successive
                // invocations of this state don't always open their sweep from the same angle.
                patternOrbitAngle = MathHelper.ToRadians(GetDeterministicRandom(-40, 40));

                // Entry burst: brief violet flare that signals the start of the orbit.
                for (int i = 0; i < 28; i++)
                {
                    LuminanceUtilities.SpawnParticle(
                        NPC.Center,
                        Main.rand.NextVector2Circular(4.5f, 4.5f),
                        new Color(180, 80, 255),
                        38, 1.3f, ParticleType.Spark);
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, NPC.Center);
            }

            if (aiTimer <= PmcOrbitEnd)
            {
                // ── Lemniscate of Bernoulli (figure-8) ──────────────────────────────────────────
                // Parametric form:
                //   x(t) = R · cos(t) / (1 + sin²(t))
                //   y(t) = R · sin(t)·cos(t) / (1 + sin²(t))
                //
                // The angular speed (0.12 rad/tick) means the boss completes roughly one full
                // lemniscate loop over the 44-tick phase, which feels aggressive without being
                // so fast it becomes unreadable.
                float t        = aiTimer * 0.12f;
                float sinT     = (float)Math.Sin(t);
                float cosT     = (float)Math.Cos(t);
                float denom    = 1f + sinT * sinT;
                float orbitR   = isPhase2 ? 310f : 275f;

                Vector2 lemOffset = new Vector2(
                    orbitR * cosT / denom,
                    orbitR * sinT * cosT / denom);

                // Micro-bob layered on top: slow vertical float that is phase-shifted relative
                // to the lemniscate so the boss never hits a fully-static frame.
                float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.8f) * 13f;
                Vector2 goalPos = flickerFlankStart + lemOffset + new Vector2(0f, bob);

                // Smooth steering toward the parametric goal position.
                Vector2 desired = (goalPos - NPC.Center) * 0.32f;
                EaseVelocityTowards(desired,
                    (float)aiTimer / PmcOrbitEnd,
                    EasingCurves.Sine, EasingType.InOut);

                // Particle trail — color lerps violet→white as the orbit progresses.
                if (aiTimer % 3 == 0)
                {
                    float colorT = (float)aiTimer / PmcOrbitEnd;
                    Color trailColor = Color.Lerp(
                        new Color(100, 55, 215),
                        new Color(200, 160, 255),
                        (float)Math.Sin(aiTimer * 0.28f) * 0.5f + 0.5f);
                    LuminanceUtilities.SpawnParticle(
                        NPC.Center,
                        -NPC.velocity * 0.22f,
                        trailColor,
                        30, 1.1f, ParticleType.Spark);
                }

                // Tick 22: mid-orbit audio spike + subtle shake — first warning cue.
                if (aiTimer == 22)
                {
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item117, NPC.Center);
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 3.2f);
                }

                // Tick PmcOrbitEnd-2: escalated shake + combat text — the "lock-on" moment.
                if (aiTimer == PmcOrbitEnd - 2)
                {
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 5.5f);
                    CombatText.NewText(NPC.getRect(), new Color(255, 160, 40), "!", true);
                }

                // Tick PmcOrbitEnd / PmcPredictTick (same frame): stamp predicted position and
                // spawn converging warning particles toward the stamp.
                if (aiTimer == PmcPredictTick)
                {
                    // Project player's current velocity ~28 ticks forward.
                    Vector2 predicted = target.Center + target.velocity * 28f;
                    vortexAnchor = predicted;   // scratch field reused for VFX + aiming

                    // 16 orange particles drift outward from the boss, then converge: we spawn
                    // them with a velocity component toward 'predicted' so they visually "mark"
                    // the impact zone even though they're cosmetic only.
                    for (int i = 0; i < 16; i++)
                    {
                        Vector2 scatter     = Main.rand.NextVector2Circular(80f, 80f);
                        Vector2 scatterPos  = NPC.Center + scatter;
                        Vector2 toMark      = predicted - scatterPos;
                        float   toMarkLen   = toMark.Length();
                        Vector2 warnVel     = toMarkLen > 0f
                            ? (toMark / toMarkLen) * Main.rand.NextFloat(3.5f, 6.5f)
                            : Vector2.Zero;
                        LuminanceUtilities.SpawnParticle(
                            scatterPos, warnVel,
                            new Color(255, 130, 40),
                            42, 1.5f, ParticleType.Spark);
                    }
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                }

                return; // orbit phase still running; UpdateProxyPlayerVisuals etc. handled by AI()
            }

            // ── SUB-PHASE B: PREDICTIVE SALVO (ticks PmcOrbitEnd+1 – PmcTeleport1) ──────────────

            // Drift backward (away from player) during the brief charging window so the boss
            // never hangs motionless — gives the impression of winding up for the shot.
            if (aiTimer > PmcOrbitEnd && aiTimer < PmcStrikeTick)
            {
                Vector2 awayDir = NPC.Center - target.Center;
                if (awayDir != Vector2.Zero) awayDir.Normalize();
                // Gentle rearward drift at ~25% of normal movement speed.
                EaseVelocityTowards(awayDir * 5.5f, 0.28f, EasingCurves.Sine, EasingType.Out);

                // Simmer particles cluster around the boss to sell the "charging" feel.
                if (aiTimer % 4 == 0)
                {
                    LuminanceUtilities.SpawnParticle(
                        NPC.Center + Main.rand.NextVector2Circular(32f, 32f),
                        Vector2.Zero,
                        new Color(255, 210, 80),
                        24, 0.9f, ParticleType.Spark);
                }
            }

            // Tick PmcStrikeTick: release the predictive salvo.
            if (aiTimer == PmcStrikeTick)
            {
                Vector2 aimDir = vortexAnchor - NPC.Center;
                if (aimDir == Vector2.Zero) aimDir = target.Center - NPC.Center;
                if (aimDir != Vector2.Zero) aimDir.Normalize();

                int   shotCount  = isPhase2 ? 6 : 4;
                float halfSpread = isPhase2
                    ? MathHelper.ToRadians(22f)
                    : MathHelper.ToRadians(15f);

                int   projType = activeWeapon.shoot > 0 ? activeWeapon.shoot : ProjectileID.EnchantedBeam;
                int   dmg      = CalculateScaledDamage(activeWeapon);
                float spd      = activeWeapon.shootSpeed > 0 ? activeWeapon.shootSpeed : 11f;

                for (int i = 0; i < shotCount; i++)
                {
                    float frac = shotCount > 1 ? (float)i / (shotCount - 1) : 0f;
                    float spreadAngle = MathHelper.Lerp(-halfSpread, halfSpread, frac);
                    Vector2 vel = aimDir.RotatedBy(spreadAngle) * spd;

                    int p = Projectile.NewProjectile(
                        NPC.GetSource_FromAI(), NPC.Center, vel,
                        projType, dmg, 0f, proxySlot);

                    if (p >= 0 && p < Main.maxProjectiles)
                    {
                        Main.projectile[p].hostile  = true;
                        Main.projectile[p].friendly = false;
                        if (Main.projectile[p].timeLeft == 0 || Main.projectile[p].timeLeft > 600)
                            Main.projectile[p].timeLeft = 600;

                        // Muzzle-flash particles at each projectile origin.
                        for (int j = 0; j < 3; j++)
                        {
                            LuminanceUtilities.SpawnParticle(
                                NPC.Center,
                                vel * 0.18f + Main.rand.NextVector2Circular(1.8f, 1.8f),
                                new Color(255, 185, 60),
                                22, 1.0f, ParticleType.Spark);
                        }
                    }
                }

                if (activeWeapon.UseSound != null)
                    Terraria.Audio.SoundEngine.PlaySound(activeWeapon.UseSound, NPC.Center);
                bossWeaponSwingTimer = bossWeaponSwingMax;
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 7.5f);

                // Recoil kick — pushes boss opposite to the shot direction.
                NPC.velocity = -aimDir * 6f;
            }

            // Between strike and teleport: let the recoil momentum bleed out naturally.
            if (aiTimer > PmcStrikeTick && aiTimer < PmcTeleport1)
                NPC.velocity *= 0.84f;

            // Tick PmcTeleport1: glitch to the opposite diagonal flank.
            if (aiTimer == PmcTeleport1)
            {
                // Departure VFX at old position.
                for (int i = 0; i < 20; i++)
                {
                    LuminanceUtilities.SpawnParticle(
                        NPC.Center,
                        Main.rand.NextVector2Circular(5f, 5f),
                        new Color(100, 60, 220),
                        30, 1.2f, ParticleType.Spark);
                }

                // Compute destination: rotate the current "boss relative to player" vector by
                // 140–175° so the boss lands on the opposing diagonal flank. Small vertical bias
                // upward keeps it from spawning in the floor.
                Vector2 fromPlayer = NPC.Center - target.Center;
                if (fromPlayer == Vector2.Zero) fromPlayer = new Vector2(1f, -0.5f);
                fromPlayer.Normalize();

                float flipDeg  = GetDeterministicRandom(140, 175);
                Vector2 destDir = fromPlayer.RotatedBy(MathHelper.ToRadians(flipDeg));
                float   destDist = isPhase2 ? 330f : 400f;
                Vector2 dest    = target.Center + destDir * destDist + new Vector2(0f, -75f);

                // World-boundary clamp (identical margins to ExecuteGlitchTeleport).
                const float margin = 220f;
                dest.X = MathHelper.Clamp(dest.X, margin, Main.maxTilesX * 16f - margin);
                dest.Y = MathHelper.Clamp(dest.Y, margin, Main.maxTilesY * 16f - margin);

                NPC.Center   = dest;
                NPC.velocity = Vector2.Zero;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item92, NPC.Center);

                // Arrival VFX at new position.
                for (int i = 0; i < 20; i++)
                {
                    LuminanceUtilities.SpawnParticle(
                        NPC.Center,
                        Main.rand.NextVector2Circular(5f, 5f),
                        new Color(180, 80, 255),
                        30, 1.2f, ParticleType.Spark);
                }
                NPC.netUpdate = true;
            }

            // ── SUB-PHASE C: SWEEPING ARC ECHO (ticks PmcTeleport1+1 – PmcSweepEnd) ─────────────

            if (aiTimer > PmcTeleport1 && aiTimer < PmcSweepEnd)
            {
                // ── Lateral weave movement ─────────────────────────────────────────────────────
                // The boss orbits perpendicular to its line-of-sight at medium range, oscillating
                // left-right sinusoidally. This is NOT a fixed screen-axis weave — it's relative
                // to the instantaneous boss→player vector, so it stays readable from any camera angle.

                Vector2 toTarget = target.Center - NPC.Center;
                float   dist     = toTarget.Length();
                Vector2 fwd      = dist > 0f ? toTarget / dist : new Vector2(NPC.direction, 0f);
                Vector2 perp     = new Vector2(-fwd.Y, fwd.X); // 90° lateral of forward

                float idealDist  = isPhase2 ? 480f : 550f;
                float weaveAmp   = isPhase2 ? 265f  : 215f;
                float weaveFreq  = isPhase2 ? 0.20f : 0.15f;

                // Goal = stay idealDist behind player on the forward axis, sweep laterally.
                Vector2 weaveGoal = target.Center
                    - fwd  * idealDist
                    + perp * (float)Math.Sin(aiTimer * weaveFreq) * weaveAmp;

                // Micro-bob layered in (anti-stiffness, same technique as sub-phase A).
                weaveGoal.Y += (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.6f) * 16f;

                float sweepT     = (float)(aiTimer - PmcTeleport1) / (PmcSweepEnd - PmcTeleport1);
                float weaveBlend = EaseProgress(sweepT, EasingCurves.Quadratic, EasingType.InOut)
                                   * 0.11f + 0.03f;
                EaseVelocityTowards((weaveGoal - NPC.Center) * weaveBlend, sweepT,
                    EasingCurves.Sine, EasingType.InOut);

                // Light trail during sweep (thinner than orbit trail).
                if (aiTimer % 6 == 0)
                {
                    LuminanceUtilities.SpawnParticle(
                        NPC.Center,
                        -NPC.velocity * 0.14f,
                        new Color(130, 80, 200),
                        22, 0.88f, ParticleType.Spark);
                }

                // ── Rotating fan burst ─────────────────────────────────────────────────────────
                // Every PmcSweepInterval ticks, fire a fan of magic projectiles. The base angle
                // of the fan (patternOrbitAngle) advances by 28–35° each burst, so six bursts
                // rotate the coverage by ~180°. No two consecutive bursts cover the same arc.
                int sweepElapsed = aiTimer - PmcTeleport1;
                if (sweepElapsed > 0 && sweepElapsed % PmcSweepInterval == 0)
                {
                    int   fanCount    = isPhase2 ? 5 : 3;
                    float halfAngle   = isPhase2
                        ? MathHelper.ToRadians(40f)
                        : MathHelper.ToRadians(28f);

                    // Fan base direction points at the player; then the spread is layered on top
                    // of the rotating patternOrbitAngle to shift the whole fan each burst.
                    Vector2 fanBase = target.Center - NPC.Center;
                    if (fanBase != Vector2.Zero) fanBase.Normalize();

                    int   projType = activeWeapon.shoot > 0 ? activeWeapon.shoot : ProjectileID.EnchantedBeam;
                    int   dmg      = CalculateScaledDamage(activeWeapon);
                    float spd      = activeWeapon.shootSpeed > 0 ? activeWeapon.shootSpeed : 11f;

                    for (int i = 0; i < fanCount; i++)
                    {
                        float frac  = fanCount > 1 ? (float)i / (fanCount - 1) : 0f;
                        float angle = patternOrbitAngle + MathHelper.Lerp(-halfAngle, halfAngle, frac);
                        Vector2 vel = fanBase.RotatedBy(angle) * spd;

                        int p = Projectile.NewProjectile(
                            NPC.GetSource_FromAI(), NPC.Center, vel,
                            projType, dmg, 0f, proxySlot);

                        if (p >= 0 && p < Main.maxProjectiles)
                        {
                            Main.projectile[p].hostile  = true;
                            Main.projectile[p].friendly = false;
                            if (Main.projectile[p].timeLeft == 0 || Main.projectile[p].timeLeft > 600)
                                Main.projectile[p].timeLeft = 600;

                            LuminanceUtilities.SpawnParticle(
                                NPC.Center,
                                vel * 0.14f,
                                new Color(165, 100, 255),
                                20, 0.85f, ParticleType.Spark);
                        }
                    }

                    if (activeWeapon.UseSound != null)
                        Terraria.Audio.SoundEngine.PlaySound(activeWeapon.UseSound, NPC.Center);
                    bossWeaponSwingTimer = bossWeaponSwingMax;

                    // Advance the fan rotation for the next burst.
                    patternOrbitAngle += MathHelper.ToRadians(isPhase2 ? 35f : 28f);
                }

                // Phase 2 only: second glitch-teleport roughly halfway through the sweep to
                // reposition aggressively and reset the fan rotation seed.
                if (isPhase2)
                {
                    int midSweep = PmcTeleport1 + (PmcSweepEnd - PmcTeleport1) / 2;
                    if (aiTimer == midSweep)
                    {
                        // Departure burst.
                        for (int i = 0; i < 16; i++)
                        {
                            LuminanceUtilities.SpawnParticle(
                                NPC.Center,
                                Main.rand.NextVector2Circular(4f, 4f),
                                new Color(100, 60, 220),
                                28, 1.1f, ParticleType.Spark);
                        }

                        // New destination: opposite side again, slightly closer this time.
                        Vector2 fp2 = NPC.Center - target.Center;
                        if (fp2 == Vector2.Zero) fp2 = new Vector2(-1f, -0.5f);
                        fp2.Normalize();
                        Vector2 tp2Dest = target.Center
                            + fp2.RotatedBy(MathHelper.ToRadians(GetDeterministicRandom(130, 160))) * 300f
                            + new Vector2(0f, -60f);

                        const float m2 = 220f;
                        tp2Dest.X = MathHelper.Clamp(tp2Dest.X, m2, Main.maxTilesX * 16f - m2);
                        tp2Dest.Y = MathHelper.Clamp(tp2Dest.Y, m2, Main.maxTilesY * 16f - m2);

                        NPC.Center   = tp2Dest;
                        NPC.velocity = Vector2.Zero;
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item92, NPC.Center);

                        for (int i = 0; i < 16; i++)
                        {
                            LuminanceUtilities.SpawnParticle(
                                NPC.Center,
                                Main.rand.NextVector2Circular(4f, 4f),
                                new Color(180, 80, 255),
                                28, 1.1f, ParticleType.Spark);
                        }

                        // Fresh rotation seed for the second half of the sweep.
                        patternOrbitAngle = MathHelper.ToRadians(GetDeterministicRandom(-25, 25));
                        NPC.netUpdate = true;
                    }
                }

                // Climax shake a few ticks before the state ends.
                if (aiTimer == PmcSweepEnd - 5)
                    ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 5f);
            }

            // ── FRICTION EXIT ───────────────────────────────────────────────────────────────────
            // Bleed off remaining velocity smoothly before handing control back to STATE_IDLE.
            if (aiTimer >= PmcSweepEnd)
            {
                NPC.velocity *= 0.88f;
                aiState = STATE_IDLE;
                aiTimer = 0;
                NPC.netUpdate = true;
            }
        }
    }
}