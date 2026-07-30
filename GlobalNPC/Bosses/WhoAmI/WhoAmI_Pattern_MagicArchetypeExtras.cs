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
    // MAGIC ARCHETYPE TRIO — 3 new Magic-exclusive attacks (STATE_AUREOLA_SIGNET_RAIN /
    // STATE_DOUBLE_HELIX_SWEEP / STATE_QUANTUM_GLITCH_PHASING), wired into the existing
    // weighted-random pattern pool as indices 6/7/8 in WhoAmI_Patterns.cs (index 5 was already taken
    // by the pre-existing Gravity Well & Arcane Torrent). Same conventions as the Melee/Ranged trios:
    // hostile projectiles spawned with owner == proxySlot (auto-picks up the Lucille Karma shader
    // from WhoAmI_VFX_ProjectileShader.cs), ResolveMimickedProjectileType() (defined in
    // WhoAmI_Pattern_RangedArchetypeExtras.cs) used wherever the brief calls for something
    // "resembling the player's [weapon] projectile", and a real, continuous NPC.velocity applied to
    // the boss's own body from the very first tick of every Handle method - the Ranged trio initially
    // shipped without this and read as "frozen" whenever the player wasn't actively landing hits to
    // trigger a reactive dodge elsewhere (see WhoAmI_SatSetPhysics.cs rule 3), so this trio bakes the
    // fix in from the start instead of patching it in after the fact.
    // ================================================================================================
    public partial class WhoAmI
    {
        // ============================================================================================
        // ATTACK 1: "AUREOLA SIGNET RAIN"
        // Several mathematical magic circles ("signets") open above the arena, channel for a beat,
        // then each one rains down a dense torrent of spiraling magic projectiles.
        // ============================================================================================
        private readonly List<Vector2> signetPoints = new List<Vector2>();
        private bool signetChannelStarted = false;

        private void ResetAureolaSignetRainState()
        {
            signetPoints.Clear();
            signetChannelStarted = false;
        }

        private void HandleAureolaSignetRain(Player target)
        {
            int signetCount = isPhase2 ? 5 : 3;
            int channelDuration = isPhase2 ? 40 : 55;
            int rainDuration = isPhase2 ? 90 : 70;
            float spacing = isPhase2 ? 220f : 260f;

            NPC.damage = 0;

            // SAT SET rule 3: the boss weaves slowly beneath its own summoned circles the whole time
            // instead of parking under them - baked in from the first tick, not patched in later.
            float driftAngle = Main.GlobalTimeWrappedHourly * (isPhase2 ? 1.0f : 0.7f);
            Vector2 driftGoal = target.Center + new Vector2((float)Math.Cos(driftAngle) * 260f, -140f + (float)Math.Sin(driftAngle) * 60f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (driftGoal - NPC.Center) * 0.05f, 0.12f);

            if (signetPoints.Count == 0 && aiTimer == 0)
            {
                float baseY = target.Center.Y - 480f;
                for (int i = 0; i < signetCount; i++)
                {
                    float offset = (i - (signetCount - 1) / 2f) * spacing;
                    signetPoints.Add(new Vector2(target.Center.X + offset, baseY));
                }
                NPC.netUpdate = true;
            }

            if (aiTimer < channelDuration)
            {
                // Mathematical magic-circle telegraph: concentric counter-rotating rings of particles.
                if (aiTimer % 2 == 0)
                {
                    foreach (Vector2 signet in signetPoints)
                    {
                        float ringAngle = Main.GlobalTimeWrappedHourly * 3f;
                        for (int r = 0; r < 3; r++)
                        {
                            float radius = 26f + r * 14f;
                            float a = ringAngle * (r % 2 == 0 ? 1f : -1f) + r;
                            Vector2 p = signet + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * radius;
                            LuminanceUtilities.SpawnParticle(p, Vector2.Zero, new Color(150, 80, 240) * 0.6f, 12, 0.6f, ParticleType.Spark);
                        }
                    }
                }
                return;
            }

            if (!signetChannelStarted)
            {
                signetChannelStarted = true;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                foreach (Vector2 signet in signetPoints)
                    for (int i = 0; i < 12; i++)
                        LuminanceUtilities.SpawnParticle(signet, Main.rand.NextVector2Circular(3, 3), new Color(150, 80, 240), 20, 1.1f, ParticleType.Spark);
                NPC.netUpdate = true;
            }

            // --- RAIN: each signet periodically drops a spiraling cluster of projectiles ---
            int rainTick = aiTimer - channelDuration;
            if (rainTick >= 0 && rainTick < rainDuration)
            {
                int fireEvery = isPhase2 ? 6 : 9;
                if (rainTick % fireEvery == 0)
                {
                    int projType = ResolveMimickedProjectileType();
                    int dmg = isPhase2 ? 46 : 32;
                    foreach (Vector2 signet in signetPoints)
                    {
                        float spiralAngle = rainTick * 0.3f;
                        Vector2 spiralOffset = new Vector2((float)Math.Cos(spiralAngle), (float)Math.Sin(spiralAngle)) * 30f;
                        Vector2 spawnPos = signet + spiralOffset;
                        Vector2 vel = new Vector2((float)Math.Cos(spiralAngle) * 1.5f, isPhase2 ? 8.5f : 6.5f);

                        int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, vel, projType, dmg, 0f, proxySlot);
                        if (p >= 0 && p < Main.maxProjectiles)
                        {
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                            Main.projectile[p].tileCollide = false;
                            Main.projectile[p].penetrate = 1;
                            Main.projectile[p].timeLeft = 80;
                        }
                        LuminanceUtilities.SpawnParticle(spawnPos, vel * 0.3f, new Color(150, 80, 240), 16, 0.9f, ParticleType.Spark);
                    }
                }
            }

            if (aiTimer > channelDuration + rainDuration + 15)
            {
                signetPoints.Clear();
                signetChannelStarted = false;
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 25 : 45;
                NPC.netUpdate = true;
            }
        }

        // ============================================================================================
        // ATTACK 2: "DOUBLE HELIX SWEEP"
        // Two intertwined beams (puppeted point-hitboxes rather than one stretched sprite, same
        // convention as the ring-wave/blade-ring puppeteering elsewhere) trace a sine-wave helix
        // while sweeping the full width of the arena, forcing a "jump rope" dodge.
        // ============================================================================================
        private int helixArmAIndex = -1;
        private int helixArmBIndex = -1;
        private float helixSweepStartX;
        private float helixSweepEndX;

        private void ResetDoubleHelixSweepState()
        {
            if (helixArmAIndex >= 0 && helixArmAIndex < Main.maxProjectiles && Main.projectile[helixArmAIndex].active)
                Main.projectile[helixArmAIndex].Kill();
            if (helixArmBIndex >= 0 && helixArmBIndex < Main.maxProjectiles && Main.projectile[helixArmBIndex].active)
                Main.projectile[helixArmBIndex].Kill();
            helixArmAIndex = -1;
            helixArmBIndex = -1;
        }

        private void HandleDoubleHelixSweep(Player target)
        {
            int sweepDuration = isPhase2 ? 100 : 130;
            float halfWidth = isPhase2 ? 900f : 750f;
            float amplitude = isPhase2 ? 260f : 200f;
            float twists = isPhase2 ? 4f : 3f;
            int dmg = isPhase2 ? 50 : 36;

            NPC.damage = 0;

            if (helixArmAIndex == -1 && aiTimer == 0)
            {
                helixSweepStartX = target.Center.X - halfWidth;
                helixSweepEndX = target.Center.X + halfWidth;
                int projType = ResolveMimickedProjectileType();
                helixArmAIndex = SpawnHelixArmProjectile(projType, dmg);
                helixArmBIndex = SpawnHelixArmProjectile(projType, dmg);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                NPC.netUpdate = true;
            }

            // SAT SET rule 3: the boss hangs back and paces alongside its own beams rather than
            // standing still while they do all the work.
            Vector2 backGoal = target.Center + new Vector2(0f, -260f) + GetSatSetBobOffset(1.2f, 18f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (backGoal - NPC.Center) * 0.05f, 0.1f);

            float sweepT = MathHelper.Clamp(aiTimer / (float)sweepDuration, 0f, 1f);
            float sweepX = MathHelper.Lerp(helixSweepStartX, helixSweepEndX, sweepT);
            float helixPhase = sweepT * MathHelper.TwoPi * twists;
            float armAY = target.Center.Y + (float)Math.Sin(helixPhase) * amplitude;
            float armBY = target.Center.Y + (float)Math.Sin(helixPhase + MathHelper.Pi) * amplitude;

            UpdateHelixArm(helixArmAIndex, new Vector2(sweepX, armAY), new Color(150, 80, 240));
            UpdateHelixArm(helixArmBIndex, new Vector2(sweepX, armBY), new Color(230, 110, 230));

            if (aiTimer >= sweepDuration)
            {
                ResetDoubleHelixSweepState();
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 20 : 35;
                NPC.netUpdate = true;
            }
        }

        private int SpawnHelixArmProjectile(int projType, int dmg)
        {
            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, projType, dmg, 0f, proxySlot);
            if (p < 0 || p >= Main.maxProjectiles) return -1;

            Projectile proj = Main.projectile[p];
            proj.hostile = true;
            proj.friendly = false;
            proj.tileCollide = false;
            proj.penetrate = -1;
            proj.scale = 1.8f;
            proj.timeLeft = 999; // puppeted every tick for the whole sweep, overwritten by ResetDoubleHelixSweepState on exit

            return p;
        }

        private void UpdateHelixArm(int idx, Vector2 pos, Color trailColor)
        {
            if (idx < 0 || idx >= Main.maxProjectiles || !Main.projectile[idx].active) return;

            Projectile proj = Main.projectile[idx];
            proj.velocity = pos - proj.Center;
            proj.Center = pos;
            proj.rotation += 0.2f;

            if (Main.rand.NextBool(2))
                LuminanceUtilities.SpawnParticle(pos, Main.rand.NextVector2Circular(1.5f, 1.5f), trailColor, 16, 1f, ParticleType.Spark);
        }

        // ============================================================================================
        // ATTACK 3: "QUANTUM GLITCH PHASING"
        // A ring of orbs pulses between solid (damaging) and phased-out (harmless) on a fixed,
        // clearly-telegraphed rhythm; a well-timed player dash forces an early phase-out as a reward
        // on top of that base rhythm, without depending on any one specific dash item's internal flag.
        // ============================================================================================
        private readonly List<int> glitchOrbIndices = new List<int>();
        private readonly List<int> glitchOrbBaseDamage = new List<int>();
        private int glitchCycleTimer = 0;
        private bool glitchPhaseSolid = true;

        private void ResetQuantumGlitchPhasingState()
        {
            foreach (int idx in glitchOrbIndices)
                if (idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].active)
                    Main.projectile[idx].Kill();
            glitchOrbIndices.Clear();
            glitchOrbBaseDamage.Clear();
            glitchCycleTimer = 0;
            glitchPhaseSolid = true;
        }

        private void HandleQuantumGlitchPhasing(Player target)
        {
            int orbCount = isPhase2 ? 7 : 5;
            int activeDuration = isPhase2 ? 140 : 110;
            int cycleLength = isPhase2 ? 26 : 34; // ticks per solid/phased half-cycle
            const int telegraphWindow = 8; // ticks before a flip where orbs pulse-warn the player
            float ringRadius = isPhase2 ? 280f : 230f;
            int dmg = isPhase2 ? 44 : 32;

            NPC.damage = 0;

            // SAT SET rule 3: slow orbit around the ring instead of standing dead still at its center.
            Vector2 hoverGoal = target.Center + new Vector2((float)Math.Cos(Main.GlobalTimeWrappedHourly * 1.4f), (float)Math.Sin(Main.GlobalTimeWrappedHourly * 1.4f)) * (ringRadius + 120f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverGoal - NPC.Center) * 0.05f, 0.12f);

            if (glitchOrbIndices.Count == 0 && aiTimer == 0)
            {
                int projType = ResolveMimickedProjectileType();
                for (int i = 0; i < orbCount; i++)
                {
                    float ang = MathHelper.TwoPi * i / orbCount;
                    Vector2 pos = target.Center + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * ringRadius;
                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero, projType, dmg, 0f, proxySlot);
                    if (p >= 0 && p < Main.maxProjectiles)
                    {
                        Main.projectile[p].hostile = true;
                        Main.projectile[p].friendly = false;
                        Main.projectile[p].tileCollide = false;
                        Main.projectile[p].penetrate = -1;
                        Main.projectile[p].timeLeft = activeDuration + 10;
                        glitchOrbIndices.Add(p);
                        glitchOrbBaseDamage.Add(dmg);
                    }
                }
                glitchCycleTimer = cycleLength;
                glitchPhaseSolid = true;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                NPC.netUpdate = true;
            }

            // Orbs slowly drift around the ring so the pattern doesn't feel like static turrets.
            for (int i = 0; i < glitchOrbIndices.Count; i++)
            {
                int idx = glitchOrbIndices[i];
                if (idx < 0 || idx >= Main.maxProjectiles || !Main.projectile[idx].active) continue;
                float ang = MathHelper.TwoPi * i / glitchOrbIndices.Count + Main.GlobalTimeWrappedHourly * 0.5f;
                Vector2 pos = target.Center + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * ringRadius;
                Main.projectile[idx].velocity = pos - Main.projectile[idx].Center;
                Main.projectile[idx].Center = pos;
            }

            // Rough velocity-based heuristic rather than keying off any one specific dash item's
            // internal flag (Tabi / Master Ninja Gear / Shield of Cthulhu all set different fields) -
            // a fast enough dash forces an early phase-out as a reward on top of the base rhythm.
            bool playerIsDashing = target.velocity.Length() > target.moveSpeed * 1.8f;

            glitchCycleTimer--;
            if (glitchCycleTimer <= telegraphWindow && glitchCycleTimer > 0 && aiTimer % 3 == 0)
            {
                foreach (int idx in glitchOrbIndices)
                    if (idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].active)
                        LuminanceUtilities.SpawnParticle(Main.projectile[idx].Center, Vector2.Zero, glitchPhaseSolid ? new Color(150, 80, 240) : Color.White, 10, 1f, ParticleType.Spark);
            }

            if (glitchCycleTimer <= 0 || (playerIsDashing && glitchPhaseSolid))
            {
                glitchPhaseSolid = !glitchPhaseSolid;
                glitchCycleTimer = cycleLength;
                for (int i = 0; i < glitchOrbIndices.Count; i++)
                {
                    int idx = glitchOrbIndices[i];
                    if (idx < 0 || idx >= Main.maxProjectiles || !Main.projectile[idx].active) continue;
                    Main.projectile[idx].damage = glitchPhaseSolid ? glitchOrbBaseDamage[i] : 0;
                    for (int j = 0; j < 8; j++)
                        LuminanceUtilities.SpawnParticle(Main.projectile[idx].Center, Main.rand.NextVector2Circular(3, 3), glitchPhaseSolid ? new Color(150, 80, 240) : new Color(150, 80, 240) * 0.2f, 14, 1f, ParticleType.Spark);
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item92, NPC.Center);
            }

            if (aiTimer >= activeDuration)
            {
                ResetQuantumGlitchPhasingState();
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 25 : 40;
                NPC.netUpdate = true;
            }
        }
    }
}
