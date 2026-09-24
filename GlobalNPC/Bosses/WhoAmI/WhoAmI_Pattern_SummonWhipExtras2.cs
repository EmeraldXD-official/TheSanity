using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // SUMMON + WHIP TRIO 2 — 6 new attacks total (3 per class), wired in as indices 5/6/7 on both
    // WeaponArchetype.Summon and WeaponArchetype.Whip in WhoAmI_Patterns.cs. Same file-bundling
    // convention as WhoAmI_Pattern_ArchetypeExtras.cs (which bundles the first Summon/Whip/Yoyo/
    // Boomerang bonus pattern together).
    //
    // Summon casts are represented the same way HandleSummonRiftSwarm already does it: firing the
    // mimicked weapon's own summon-cast projectile via FireAttackProjectile and then reading/steering
    // whatever ends up flagged `p.owner == proxySlot && p.minion` in Main.projectile - no custom
    // minion NPC spawning needed, and ongoing per-minion AI stays with WhoAmIProjectileGuard exactly
    // like the existing pattern already relies on.
    // ================================================================================================
    public partial class WhoAmI
    {
        // ============================================================================================
        // ATTACK 1: "WRAITH CONVERGENCE" — STATE_SUMMON_WRAITH_CONVERGENCE
        // Casts scatter minions out into a wide ring around the player, then that ring spends the
        // rest of the attack slowly CONSTRICTING - every currently-active boss minion gets nudged
        // toward a shrinking circle each tick - instead of Rift Swarm's single "call them all in now".
        // ============================================================================================
        private const int WraithCastEnd = 45;
        private const int WraithConstrictEnd = 130;
        private int wraithCastsFired = 0;

        private void ResetSummonWraithConvergenceState()
        {
            wraithCastsFired = 0;
        }

        private void HandleSummonWraithConvergence(Player target)
        {
            NPC.damage = 0;
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            Vector2 hover = target.Center + new Vector2(0f, -240f) + GetSatSetBobOffset(1f, 20f);
            EaseVelocityTowards((hover - NPC.Center) * 0.06f, 1f, EasingCurves.Sine, EasingType.InOut, 0.4f);

            if (aiTimer <= WraithCastEnd)
            {
                int castEvery = isPhase2 ? 8 : 11;
                if (aiTimer % castEvery == 0)
                {
                    float fanAngle = MathHelper.Lerp(-0.9f, 0.9f, wraithCastsFired / (isPhase2 ? 6f : 4f));
                    FireAttackProjectile(target, fanAngle);
                    wraithCastsFired++;
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                }
                return;
            }

            float progress = MathHelper.Clamp((aiTimer - WraithCastEnd) / (float)(WraithConstrictEnd - WraithCastEnd), 0f, 1f);
            float ringRadius = MathHelper.Lerp(isPhase2 ? 420f : 360f, isPhase2 ? 70f : 110f, EaseProgress(progress, EasingCurves.Quadratic, EasingType.In));

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != proxySlot || !p.minion) continue;
                Vector2 rel = p.Center - target.Center;
                float ang = rel == Vector2.Zero ? Main.rand.NextFloat(MathHelper.TwoPi) : rel.ToRotation();
                Vector2 desired = target.Center + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * ringRadius;
                p.velocity = Vector2.Lerp(p.velocity, (desired - p.Center) * 0.09f, 0.12f);
            }

            if (progress >= 1f && aiTimer == WraithConstrictEnd)
            {
                ScreenShakeSystem.StartShakeAtPoint(target.Center, 6f, 0.22f);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, target.Center);
                for (int i = 0; i < 16; i++)
                    LuminanceUtilities.SpawnParticle(target.Center + Main.rand.NextVector2Circular(90, 90), Vector2.Zero, new Color(170, 255, 190), 18, 1f, ParticleType.Spark);
            }

            if (aiTimer > WraithConstrictEnd + 20)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 65 : 95;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 2: "SOUL TETHER BIND" — STATE_SUMMON_SOUL_TETHER
        // A small handful of minions each get their own independent tether-pull on the player - not
        // one central well like Gravity Well Torrent, several simultaneous, slightly-out-of-sync pulls
        // - with periodic synchronized "snaps" where every tether yanks taut at once.
        // ============================================================================================
        private const int TetherBindDuration = 150;
        private int tetherNextSnapTick = 55;

        private void ResetSummonSoulTetherState()
        {
            tetherNextSnapTick = 55;
        }

        private void HandleSummonSoulTether(Player target)
        {
            NPC.damage = 0;
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            Vector2 hover = target.Center + new Vector2(NPC.direction * -260f, -100f) + GetSatSetBobOffset(1.2f, 18f);
            EaseVelocityTowards((hover - NPC.Center) * 0.05f, 1f, EasingCurves.Sine, EasingType.InOut, 0.4f);

            if (aiTimer == 1 || aiTimer == 20 || aiTimer == 40)
            {
                FireAttackProjectile(target, Main.rand.NextFloat(-0.5f, 0.5f));
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
            }

            bool snapping = tetherNextSnapTick > 0 && aiTimer == tetherNextSnapTick;
            int tethered = 0;
            for (int i = 0; i < Main.maxProjectiles && tethered < 3; i++)
            {
                Projectile p = Main.projectile[i];
                if (!p.active || p.owner != proxySlot || !p.minion) continue;
                tethered++;

                if (aiTimer % 4 == 0)
                {
                    for (int seg = 0; seg <= 6; seg++)
                        LuminanceUtilities.SpawnParticle(Vector2.Lerp(p.Center, target.Center, seg / 6f), Vector2.Zero, new Color(150, 220, 255) * 0.4f, 10, 0.6f, ParticleType.Spark);
                }

                float dist = Vector2.Distance(p.Center, target.Center);
                Vector2 pull = p.Center - target.Center;
                if (pull == Vector2.Zero) continue;
                pull.Normalize();
                float leashStrength = snapping ? 3.2f : MathHelper.Clamp(dist / 900f, 0.1f, 0.9f) * (isPhase2 ? 0.16f : 0.11f);
                target.velocity += pull * leashStrength;
            }

            // FIX ("player ngebug gegara boss"): up to 3 tethered minions can each add a 3.2-strength
            // "snap" pull to target.velocity in the SAME tick, and that could repeat 2-3 times over
            // this state's 150-tick duration with nothing ever capping the total in between - stacking
            // snaps could push the player's velocity well past anything their own tile collision could
            // resolve cleanly (the actual "glitch"). Clamped once per tick, after every minion's pull
            // has been added, so a single snap still reads as a firm yank but repeated snaps can't
            // compound into a runaway speed. See ClampPlayerPullVelocity in WhoAmI_SatSetPhysics.cs.
            ClampPlayerPullVelocity(target, 13f);

            if (snapping)
            {
                ScreenShakeSystem.StartShakeAtPoint(target.Center, 4f, 0.15f);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item37, target.Center);
                tetherNextSnapTick = aiTimer + (isPhase2 ? 40 : 55);
                if (tetherNextSnapTick > TetherBindDuration) tetherNextSnapTick = -1;
            }

            if (aiTimer > TetherBindDuration)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 60 : 90;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 3: "SPECTRAL CAROUSEL" — STATE_SUMMON_SPECTRAL_CAROUSEL
        // A ring of spectral glyph-turrets (visual only, not real minions) spins around the player,
        // each one firing the instant it crosses a fixed "muzzle" angle - like numbers passing 12
        // o'clock - and the whole ring's spin speed ramps up over the attack for a building crescendo.
        // ============================================================================================
        private const int CarouselDuration = 140;
        private bool[] carouselFired = new bool[8];
        private int carouselGlyphCount = 6;
        private float ringAngleAccumulator = 0f;

        private void ResetSummonSpectralCarouselState()
        {
            carouselGlyphCount = isPhase2 ? 8 : 6;
            for (int i = 0; i < carouselFired.Length; i++) carouselFired[i] = false;
            ringAngleAccumulator = 0f;
        }

        private void HandleSummonSpectralCarousel(Player target)
        {
            NPC.damage = 0;
            bool hasProjectile = WeaponHasProjectile(activeWeapon);
            if (!hasProjectile) { aiState = STATE_IDLE; aiTimer = 0; NPC.netUpdate = true; return; }

            Vector2 hover = target.Center + new Vector2(0f, -70f);
            NPC.Center = Vector2.Lerp(NPC.Center, hover, 0.03f);

            float progress = MathHelper.Clamp(aiTimer / (float)CarouselDuration, 0f, 1f);
            float spinSpeed = MathHelper.Lerp(isPhase2 ? 0.05f : 0.035f, isPhase2 ? 0.16f : 0.11f, progress * progress);
            float ringAngle = ringAngleAccumulator;
            ringAngleAccumulator += spinSpeed;
            float radius = isPhase2 ? 230f : 195f;

            for (int i = 0; i < carouselGlyphCount; i++)
            {
                float baseAng = MathHelper.TwoPi * i / carouselGlyphCount;
                float thisAngle = ringAngle + baseAng;
                Vector2 glyphPos = target.Center + new Vector2((float)Math.Cos(thisAngle), (float)Math.Sin(thisAngle)) * radius;

                Color glyphColor = new Color(255, 190, 90);
                LuminanceUtilities.SpawnParticle(glyphPos, Vector2.Zero, glyphColor * 0.7f, 10, 0.75f, ParticleType.Spark);

                float normalizedAngle = MathHelper.WrapAngle(thisAngle);
                bool atMuzzle = Math.Abs(MathHelper.WrapAngle(normalizedAngle - -MathHelper.PiOver2)) < spinSpeed * 1.5f;
                if (!atMuzzle)
                {
                    carouselFired[i] = false;
                }
                else if (!carouselFired[i] && aiTimer > 10)
                {
                    carouselFired[i] = true;
                    Vector2 dir = target.Center - glyphPos;
                    if (dir != Vector2.Zero) dir.Normalize(); else dir = -Vector2.UnitY;
                    int projType = GetWeaponProjectileType(activeWeapon);
                    int dmg = isPhase2 ? 40 : 28;
                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), glyphPos, dir * (isPhase2 ? 7.5f : 5.5f), projType, dmg, 0f, proxySlot);
                    if (p >= 0 && p < Main.maxProjectiles)
                    {
                        Main.projectile[p].hostile = true;
                        Main.projectile[p].friendly = false;
                        Main.projectile[p].tileCollide = false;
                        Main.projectile[p].timeLeft = 90;
                    }
                    for (int b = 0; b < 8; b++)
                        LuminanceUtilities.SpawnParticle(glyphPos, dir * 2f, glyphColor, 14, 0.9f, ParticleType.Spark);
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item43, glyphPos);
                }
            }

            if (aiTimer > CarouselDuration)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 62 : 92;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 1 (WHIP): "SERPENT'S COIL" — STATE_WHIP_SERPENTS_COIL
        // The copied whip doesn't crack straight out - it visibly coils in on itself (spiraling angle,
        // shrinking radius) before snapping fully taut for the real strike. Repeats from a fresh,
        // randomized stand-off point each beat instead of Lash Cage's fixed 3-compass-point circuit.
        // ============================================================================================
        private const int CoilTightenTicks = 22;
        private int coilBeatsDone = 0;
        private int coilBeatsTotal = 2;
        private Vector2 coilAnchor = Vector2.Zero;

        private void ResetWhipSerpentsCoilState()
        {
            coilBeatsDone = 0;
            coilBeatsTotal = isPhase2 ? 3 : 2;
            coilAnchor = NPC.Center;
        }

        private void HandleWhipSerpentsCoil(Player target)
        {
            NPC.damage = 0;
            int beatCycle = CoilTightenTicks + 16;
            int localTick = aiTimer - coilBeatsDone * beatCycle;

            if (coilBeatsDone >= coilBeatsTotal)
            {
                ApplyBrakingImpulse(0.18f);
                if (aiTimer > coilBeatsTotal * beatCycle + 16)
                {
                    aiState = STATE_IDLE;
                    aiTimer = 0;
                    patternCooldown = isPhase2 ? 55 : 85;
                    NPC.netUpdate = true;
                }
                return;
            }

            if (localTick == 1)
            {
                float ang = Main.rand.NextFloat(MathHelper.TwoPi);
                coilAnchor = target.Center + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * Main.rand.NextFloat(200f, 280f);
            }

            EaseVelocityTowards((coilAnchor - NPC.Center) * 0.3f, MathHelper.Clamp(localTick / 12f, 0f, 1f), EasingCurves.Sine, EasingType.InOut);

            if (localTick <= CoilTightenTicks)
            {
                float coilT = localTick / (float)CoilTightenTicks;
                float coilAngle = coilT * MathHelper.TwoPi * 2.5f;
                float coilRadius = MathHelper.Lerp(70f, 4f, coilT);
                Vector2 coilPos = NPC.Center + new Vector2((float)Math.Cos(coilAngle), (float)Math.Sin(coilAngle)) * coilRadius;
                LuminanceUtilities.SpawnParticle(coilPos, Vector2.Zero, new Color(120, 255, 90), 12, 0.8f, ParticleType.Spark);
            }
            else if (localTick == CoilTightenTicks + 1)
            {
                NPC.direction = (target.Center.X < NPC.Center.X) ? -1 : 1;
                FireAttackProjectile(target);
                bossWeaponSwingTimer = bossWeaponSwingMax;
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 5f, 0.18f);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                for (int i = 0; i < 14; i++)
                    LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(4, 4), new Color(120, 255, 90), 18, 1.1f, ParticleType.Spark);
                coilBeatsDone++;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 2 (WHIP): "CRACKED FAN LASH" — STATE_WHIP_FAN_LASH
        // Boss plants itself at one anchor and cracks the whip in a fast, wide sweeping fan across the
        // player instead of relocating - breadth instead of Lash Cage's positioning.
        // ============================================================================================
        private int fanLashCracksDone = 0;
        private int fanLashCrackTotal = 5;

        private void ResetWhipFanLashState()
        {
            fanLashCracksDone = 0;
            fanLashCrackTotal = isPhase2 ? 7 : 5;
        }

        private void HandleWhipFanLash(Player target)
        {
            NPC.damage = 0;

            if (aiTimer <= 14)
            {
                Vector2 anchor = target.Center + new Vector2(NPC.direction * -260f, -60f);
                EaseVelocityTowards((anchor - NPC.Center) * 0.35f, aiTimer / 14f, EasingCurves.Sine, EasingType.InOut);
                return;
            }

            ApplyBrakingImpulse(0.3f);
            const int crackInterval = 5;
            if (fanLashCracksDone < fanLashCrackTotal && (aiTimer - 15) % crackInterval == 0)
            {
                float fanT = fanLashCrackTotal <= 1 ? 0.5f : fanLashCracksDone / (float)(fanLashCrackTotal - 1);
                float angleOffset = MathHelper.Lerp(-0.75f, 0.75f, fanT);
                NPC.direction = (target.Center.X < NPC.Center.X) ? -1 : 1;
                FireAttackProjectile(target, angleOffset);
                bossWeaponSwingTimer = bossWeaponSwingMax;
                Vector2 dir = Vector2.Transform(target.Center - NPC.Center, Matrix.CreateRotationZ(angleOffset));
                if (dir != Vector2.Zero) dir.Normalize();
                for (int i = 0; i < 8; i++)
                    LuminanceUtilities.SpawnParticle(NPC.Center + dir * 26f, dir, new Color(255, 110, 60), 14, 0.85f, ParticleType.Spark);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1.WithPitchOffset(fanLashCracksDone * 0.03f), NPC.Center);
                fanLashCracksDone++;
                NPC.netUpdate = true;
            }

            if (fanLashCracksDone >= fanLashCrackTotal && aiTimer > 15 + fanLashCrackTotal * crackInterval + 16)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 55 : 85;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 3 (WHIP): "PUPPETEER'S SNAP" — STATE_WHIP_PUPPETEER_SNAP
        // A genuine follow-the-leader chain of lag points sweeps an S-curve across the arena - each
        // link eases toward where the link ahead of it WAS last tick, like a real whip's body - and
        // periodically the tip cracks with a real hit where it currently sits.
        // ============================================================================================
        private const int SnapChainLength = 6;
        private Vector2[] snapChainPoints = new Vector2[SnapChainLength];
        private int snapNextCrackTick = 14;

        private void ResetWhipPuppeteerSnapState()
        {
            for (int i = 0; i < SnapChainLength; i++) snapChainPoints[i] = NPC.Center;
            snapNextCrackTick = 14;
        }

        private void HandleWhipPuppeteerSnap(Player target)
        {
            NPC.damage = 0;
            NPC.velocity *= 0.9f;
            NPC.Center += GetSatSetBobOffset(1.2f, 10f) * 0.04f;

            float sweep = (float)Math.Sin(aiTimer * (isPhase2 ? 0.05f : 0.038f)) * (isPhase2 ? 300f : 250f);
            float bob = (float)Math.Sin(aiTimer * 0.09f) * 60f;
            Vector2 leaderTarget = target.Center + new Vector2(sweep, -180f + bob);

            Vector2[] previous = new Vector2[SnapChainLength];
            Array.Copy(snapChainPoints, previous, SnapChainLength);

            snapChainPoints[0] = Vector2.Lerp(previous[0], leaderTarget, 0.22f);
            for (int i = 1; i < SnapChainLength; i++)
                snapChainPoints[i] = Vector2.Lerp(previous[i], previous[i - 1], 0.34f);

            for (int i = 0; i < SnapChainLength; i++)
            {
                float linkT = i / (float)(SnapChainLength - 1);
                LuminanceUtilities.SpawnParticle(snapChainPoints[i], Vector2.Zero, new Color(200, 60, 180) * MathHelper.Lerp(0.9f, 0.4f, linkT), 12, MathHelper.Lerp(1f, 0.5f, linkT), ParticleType.Spark);
            }

            int crackInterval = isPhase2 ? 12 : 15;
            if (aiTimer >= snapNextCrackTick)
            {
                Vector2 tip = snapChainPoints[SnapChainLength - 1];
                Vector2 dir = target.Center - tip;
                if (dir != Vector2.Zero) dir.Normalize(); else dir = -Vector2.UnitY;
                int dmg = isPhase2 ? 58 : 42;
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), tip, dir * (isPhase2 ? 9f : 7f), GetWeaponProjectileType(activeWeapon), dmg, 0f, proxySlot);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                    Main.projectile[p].tileCollide = false;
                    Main.projectile[p].timeLeft = 60;
                }
                for (int b = 0; b < 12; b++)
                    LuminanceUtilities.SpawnParticle(tip, Main.rand.NextVector2Circular(4, 4), new Color(220, 90, 200), 16, 1f, ParticleType.Spark);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, tip);
                snapNextCrackTick = aiTimer + crackInterval;
                NPC.netUpdate = true;
            }

            int totalDuration = isPhase2 ? 130 : 105;
            if (aiTimer > totalDuration)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 58 : 88;
                NPC.netUpdate = true;
            }
        }
    }
}