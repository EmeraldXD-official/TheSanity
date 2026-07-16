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
    // RANGED ARCHETYPE TRIO — 3 new Ranged-exclusive attacks (STATE_VECTOR_LASER_GRID /
    // STATE_HOMING_CLUSTER_COMET / STATE_SINGULARITY_OVERDRIVE), wired into the existing
    // weighted-random pattern pool as indices 5/6/7 in WhoAmI_Patterns.cs. Same conventions as
    // WhoAmI_Pattern_MeleeArchetypeExtras.cs: SatSet movement primitives where relevant, hostile
    // projectiles spawned with owner == proxySlot (auto-picks up the Lucille Karma shader from
    // WhoAmI_VFX_ProjectileShader.cs), and a meaningfully harder Phase 2.
    //
    // All 3 require WeaponHasProjectile(activeWeapon) - ExecuteRangedPattern already early-outs to
    // STATE_IDLE for a Ranged-classified weapon with no real projectile before any pattern (0-7)
    // ever runs, so the Handle methods below don't need to re-check that themselves.
    // ================================================================================================
    public partial class WhoAmI
    {
        // ============================================================================================
        // ATTACK 1: "VECTOR LASER GRID SYSTEM"
        // Telegraphed laser-sight lines sweep into a grid over the player; after a 45-tick delay the
        // grid solidifies into full damaging beams.
        // ============================================================================================
        private readonly List<(Vector2 start, Vector2 end)> laserGridLines = new List<(Vector2, Vector2)>();
        private bool laserGridSolidified = false;
        private const int LaserGridTelegraphTicks = 45; // literal "45-tick delay" from the design brief

        private void ResetVectorLaserGridState()
        {
            laserGridLines.Clear();
            laserGridSolidified = false;
        }

        private void HandleVectorLaserGrid(Player target)
        {
            int solidifyDuration = isPhase2 ? 26 : 20;
            int lineCount = isPhase2 ? 7 : 5;
            float spacing = isPhase2 ? 90f : 110f;
            const float lineHalfLength = 900f;

            NPC.damage = 0;
            // BUGFIX: this used to be `NPC.velocity *= 0.9f` + a 0.04x bob offset, which converges to
            // basically zero within a few ticks - the boss stood dead still for the whole 45+ tick
            // telegraph (SAT SET rule 3 violation), which is also why the attack was hard to notice.
            // Real slow hover-strafe around the player instead, so it stays clearly "alive" the whole time.
            float strafeAngle = Main.GlobalTimeWrappedHourly * (isPhase2 ? 1.1f : 0.8f);
            Vector2 hoverGoal = target.Center + new Vector2((float)Math.Cos(strafeAngle), (float)Math.Sin(strafeAngle) * 0.5f) * 300f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverGoal - NPC.Center) * 0.05f, 0.12f);

            if (laserGridLines.Count == 0 && aiTimer == 0)
            {
                // A sweeping grid: several lines parallel to the aim direction, spaced across the
                // player, plus one perpendicular cross-line through their position.
                Vector2 aim = target.Center - NPC.Center;
                if (aim == Vector2.Zero) aim = new Vector2(NPC.direction, 0f);
                aim.Normalize();
                Vector2 perp = new Vector2(-aim.Y, aim.X);

                for (int i = 0; i < lineCount; i++)
                {
                    float offset = (i - (lineCount - 1) / 2f) * spacing;
                    Vector2 center = target.Center + perp * offset;
                    laserGridLines.Add((center - aim * lineHalfLength, center + aim * lineHalfLength));
                }
                laserGridLines.Add((target.Center - perp * lineHalfLength, target.Center + perp * lineHalfLength));
                NPC.netUpdate = true;
            }

            if (aiTimer < LaserGridTelegraphTicks)
            {
                // Telegraphed laser sights - dim scanning particles walking along each line so the
                // grid shape reads clearly before it solidifies.
                if (aiTimer % 3 == 0)
                {
                    foreach (var line in laserGridLines)
                    {
                        Vector2 p = Vector2.Lerp(line.start, line.end, Main.rand.NextFloat());
                        LuminanceUtilities.SpawnParticle(p, Vector2.Zero, new Color(90, 230, 120) * 0.5f, 12, 0.6f, ParticleType.Spark);
                    }
                }
                return;
            }

            if (!laserGridSolidified)
            {
                laserGridSolidified = true;
                int dmg = isPhase2 ? 85 : 60;
                int segments = isPhase2 ? 14 : 10;
                foreach (var line in laserGridLines)
                    SpawnLaserSegmentLine(line.start, line.end, segments, dmg, solidifyDuration);

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(target.Center, 5f, 0.2f);
                NPC.netUpdate = true;
            }

            if (aiTimer > LaserGridTelegraphTicks + solidifyDuration + 10)
            {
                laserGridLines.Clear();
                laserGridSolidified = false;
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 25 : 40;
                NPC.netUpdate = true;
            }
        }

        // A "blinding, noise-textured" beam approximated as a line of overlapping short-lived
        // hostile hitboxes (same discrete-hitbox-along-a-line convention SpawnMeleeSlash/the arc
        // slash in the melee trio already use) rather than one stretched sprite - keeps it fully
        // compatible with the shared neon/noise projectile shader without needing per-segment
        // custom scaling logic.
        private void SpawnLaserSegmentLine(Vector2 start, Vector2 end, int segments, int dmg, int lifeTicks)
        {
            Vector2 dir = end - start;
            float rot = dir.ToRotation();
            for (int i = 0; i <= segments; i++)
            {
                Vector2 pos = Vector2.Lerp(start, end, i / (float)segments);
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), pos, Vector2.Zero, ProjectileID.EnchantedBeam, dmg, 0f, proxySlot);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Projectile proj = Main.projectile[p];
                    proj.hostile = true;
                    proj.friendly = false;
                    proj.tileCollide = false;
                    proj.aiStyle = 0;
                    proj.timeLeft = lifeTicks;
                    proj.rotation = rot;
                    proj.scale = 1.6f;
                    proj.penetrate = -1;
                }
                if (i % 2 == 0)
                    LuminanceUtilities.SpawnParticle(pos, Vector2.Zero, new Color(90, 230, 120), 14, 0.8f, ParticleType.Spark);
            }
        }

        // ============================================================================================
        // ATTACK 2: "HOMING CLUSTER COMET"
        // A single giant condensed projectile orbits/curves in toward the player, then fractures
        // into dozens of smart-homing micro-pellets.
        // ============================================================================================
        private int cometIndex = -1;
        private bool cometFractured = false;
        private readonly List<int> cometPelletIndices = new List<int>();

        private void ResetHomingClusterCometState()
        {
            if (cometIndex >= 0 && cometIndex < Main.maxProjectiles && Main.projectile[cometIndex].active)
                Main.projectile[cometIndex].Kill();
            foreach (int idx in cometPelletIndices)
                if (idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].active)
                    Main.projectile[idx].Kill();
            cometPelletIndices.Clear();
            cometIndex = -1;
            cometFractured = false;
        }

        private void HandleHomingClusterComet(Player target)
        {
            int fractureTick = isPhase2 ? 45 : 60;
            int pelletCount = isPhase2 ? 28 : 18;
            int homingWindow = isPhase2 ? 55 : 40; // ticks after fracture the pellets keep actively homing

            NPC.damage = 0;

            // BUGFIX: this pattern used to only ever move the comet/pellets and never touched the
            // boss's own NPC.velocity, so the boss stood dead still for its entire ~2s duration
            // (SAT SET rule 3 violation) - the only visible motion came from the reactive dodge
            // elsewhere kicking in when the player actually landed a hit, which read as "frozen
            // unless attacked". Continuous hover-orbit around the player fixes that.
            float hoverAngle = Main.GlobalTimeWrappedHourly * (isPhase2 ? 1.6f : 1.1f);
            Vector2 hoverPoint = target.Center + new Vector2((float)Math.Cos(hoverAngle), (float)Math.Sin(hoverAngle)) * 340f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPoint - NPC.Center) * 0.06f, 0.15f);

            if (cometIndex == -1 && aiTimer == 0)
            {
                int projType = ResolveMimickedProjectileType();
                Vector2 aim = target.Center - NPC.Center;
                if (aim != Vector2.Zero) aim.Normalize(); else aim = new Vector2(NPC.direction, 0f);
                int dmg = isPhase2 ? 70 : 50;
                float speed = isPhase2 ? 9f : 6.5f;

                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aim * speed, projType, dmg, 0f, proxySlot);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    cometIndex = p;
                    Projectile proj = Main.projectile[p];
                    proj.hostile = true;
                    proj.friendly = false;
                    proj.tileCollide = false;
                    proj.penetrate = -1;
                    proj.scale = isPhase2 ? 2.6f : 2.1f;
                    proj.timeLeft = fractureTick + 5;
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item42, NPC.Center);
                NPC.netUpdate = true;
            }

            // --- pre-fracture: steer the comet in an organic curving orbit toward the player ---
            if (!cometFractured && cometIndex >= 0 && cometIndex < Main.maxProjectiles && Main.projectile[cometIndex].active)
            {
                Projectile comet = Main.projectile[cometIndex];
                float orbitAngle = aiTimer * (isPhase2 ? 0.11f : 0.07f);
                float orbitRadius = MathHelper.Lerp(260f, 20f, MathHelper.Clamp(aiTimer / (float)fractureTick, 0f, 1f));
                Vector2 orbitPoint = target.Center + new Vector2((float)Math.Cos(orbitAngle), (float)Math.Sin(orbitAngle)) * orbitRadius;
                Vector2 desired = orbitPoint - comet.Center;
                if (desired != Vector2.Zero) desired.Normalize();
                float speed = isPhase2 ? 11f : 8f;
                comet.velocity = Vector2.Lerp(comet.velocity, desired * speed, 0.08f);
                comet.rotation += 0.15f;

                if (aiTimer % 3 == 0)
                    LuminanceUtilities.SpawnParticle(comet.Center, -comet.velocity * 0.1f, new Color(60, 255, 190), 20, 1.1f, ParticleType.Spark);
            }

            bool cometDied = cometIndex >= 0 && (cometIndex >= Main.maxProjectiles || !Main.projectile[cometIndex].active);
            if (!cometFractured && cometIndex >= 0 && (aiTimer >= fractureTick || cometDied))
            {
                cometFractured = true;
                Vector2 burstCenter = (!cometDied && cometIndex < Main.maxProjectiles) ? Main.projectile[cometIndex].Center : NPC.Center;
                if (!cometDied) Main.projectile[cometIndex].Kill();

                int dmg = isPhase2 ? 34 : 24;
                int projType = ResolveMimickedProjectileType();
                for (int i = 0; i < pelletCount; i++)
                {
                    float ang = MathHelper.TwoPi * i / pelletCount + Main.rand.NextFloat(-0.15f, 0.15f);
                    Vector2 vel = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * Main.rand.NextFloat(4f, 7f);
                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), burstCenter, vel, projType, dmg, 0f, proxySlot);
                    if (p >= 0 && p < Main.maxProjectiles)
                    {
                        Projectile proj = Main.projectile[p];
                        proj.hostile = true;
                        proj.friendly = false;
                        proj.tileCollide = false;
                        proj.penetrate = 1;
                        proj.scale = 0.85f;
                        proj.timeLeft = 90;
                        cometPelletIndices.Add(p);
                    }
                }
                ScreenShakeSystem.StartShakeAtPoint(burstCenter, 7f, 0.3f);
                for (int i = 0; i < 20; i++)
                    LuminanceUtilities.SpawnParticle(burstCenter, Main.rand.NextVector2Circular(5, 5), new Color(90, 230, 120), 22, 1.2f, ParticleType.Spark);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, burstCenter);
                NPC.netUpdate = true;
            }

            // --- post-fracture: pellets actively home for a window, then coast on their own ---
            if (cometFractured && aiTimer < fractureTick + homingWindow)
            {
                float turnRate = isPhase2 ? 0.10f : 0.06f;
                float pelletSpeed = isPhase2 ? 9.5f : 7f;
                foreach (int idx in cometPelletIndices)
                {
                    if (idx < 0 || idx >= Main.maxProjectiles || !Main.projectile[idx].active) continue;
                    Projectile pellet = Main.projectile[idx];
                    Vector2 toTarget = target.Center - pellet.Center;
                    if (toTarget == Vector2.Zero) continue;
                    toTarget.Normalize();
                    pellet.velocity = Vector2.Lerp(pellet.velocity, toTarget * pelletSpeed, turnRate);
                    pellet.rotation = pellet.velocity.ToRotation();
                }
            }

            if (cometFractured && aiTimer > fractureTick + 25)
            {
                cometPelletIndices.Clear(); // let already-fired pellets fly on to their natural timeLeft expiry
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 20 : 35;
                NPC.netUpdate = true;
            }
        }

        // Prefers the mimicked weapon's own projectile type ("resembling the player's ranged
        // projectile", per the brief) and falls back to a generic energy bolt if the weapon has no
        // valid shoot type for some reason.
        private int ResolveMimickedProjectileType()
        {
            return activeWeapon != null && activeWeapon.shoot > ProjectileID.None ? activeWeapon.shoot : ProjectileID.EnchantedBeam;
        }

        // ============================================================================================
        // ATTACK 3: "SINGULARITY OVERDRIVE"
        // An unstable gravitational orb settles near the player, continuously pulling them in while
        // periodically emitting expanding, neon-outlined energy rings the player has to jump/dash
        // over.
        // ============================================================================================
        private class SingularityRingWave
        {
            public readonly List<int> ProjectileIndices = new List<int>();
            public int SpawnTick;
            public float MaxRadius;
            public Vector2 Anchor;
        }

        private int singularityOrbIndex = -1;
        private bool singularitySettled = false;
        private int singularityNextRingTick = -1;
        private readonly List<SingularityRingWave> singularityRingWaves = new List<SingularityRingWave>();
        private const int SingularityRingLifetime = 40;

        private void ResetSingularityOverdriveState()
        {
            if (singularityOrbIndex >= 0 && singularityOrbIndex < Main.maxProjectiles && Main.projectile[singularityOrbIndex].active)
                Main.projectile[singularityOrbIndex].Kill();
            foreach (var wave in singularityRingWaves)
                foreach (int idx in wave.ProjectileIndices)
                    if (idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].active)
                        Main.projectile[idx].Kill();
            singularityRingWaves.Clear();
            singularityOrbIndex = -1;
            singularitySettled = false;
            singularityNextRingTick = -1;
        }

        private void HandleSingularityOverdrive(Player target)
        {
            const int travelTicks = 20;
            int activeDuration = isPhase2 ? 130 : 100;
            int ringInterval = isPhase2 ? 30 : 42;
            float pullStrength = isPhase2 ? 0.22f : 0.15f;
            float maxRingRadius = isPhase2 ? 340f : 280f;
            int ringDamage = isPhase2 ? 55 : 40;
            const int ringSegments = 16;

            NPC.damage = 0;

            // BUGFIX: same SAT SET rule 3 gap as the comet attack above - this pattern never moved
            // the boss's own body, so it stood dead still while the orb pulled/rang the whole time.
            // Loose orbital crawl around the orb once it exists fixes that.
            if (singularityOrbIndex >= 0 && singularityOrbIndex < Main.maxProjectiles && Main.projectile[singularityOrbIndex].active)
            {
                Vector2 crawl = GetOrbitalCrawlPosition(Main.projectile[singularityOrbIndex].Center, 190f, Main.GlobalTimeWrappedHourly * 1.3f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (crawl - NPC.Center) * 0.07f, 0.15f);
            }

            if (singularityOrbIndex == -1 && aiTimer == 0)
            {
                Vector2 dest = target.Center + Main.rand.NextVector2CircularEdge(60f, 60f);
                Vector2 aim = dest - NPC.Center;
                if (aim != Vector2.Zero) aim.Normalize(); else aim = new Vector2(NPC.direction, 0f);

                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, aim * 14f, ProjectileID.EnchantedBeam, 0, 0f, proxySlot);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    singularityOrbIndex = p;
                    Projectile proj = Main.projectile[p];
                    proj.hostile = true;
                    proj.friendly = false;
                    proj.tileCollide = false;
                    proj.penetrate = -1;
                    proj.scale = isPhase2 ? 2.4f : 2f;
                    proj.timeLeft = travelTicks + activeDuration + 20;
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item42, NPC.Center);
                NPC.netUpdate = true;
            }

            if (singularityOrbIndex < 0 || singularityOrbIndex >= Main.maxProjectiles || !Main.projectile[singularityOrbIndex].active)
            {
                ResetSingularityOverdriveState();
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = 20;
                NPC.netUpdate = true;
                return;
            }
            Projectile orb = Main.projectile[singularityOrbIndex];

            if (!singularitySettled)
            {
                if (aiTimer >= travelTicks)
                {
                    singularitySettled = true;
                    orb.velocity = Vector2.Zero;
                    singularityNextRingTick = aiTimer; // first ring fires immediately on settle
                    for (int i = 0; i < 20; i++)
                        LuminanceUtilities.SpawnParticle(orb.Center, Main.rand.NextVector2Circular(4, 4), new Color(90, 20, 140), 24, 1.4f, ParticleType.Spark);
                    ScreenShakeSystem.StartShakeAtPoint(orb.Center, 6f, 0.25f);
                }
                return;
            }

            // --- ACTIVE: pull the player in, periodically emit expanding energy rings ---
            Vector2 toOrb = orb.Center - target.Center;
            float dist = toOrb.Length();
            if (dist > 4f)
                target.velocity += toOrb / dist * pullStrength;

            if (Main.rand.NextBool(3))
            {
                Vector2 inward = -toOrb.SafeNormalize(Vector2.Zero);
                LuminanceUtilities.SpawnParticle(orb.Center + Main.rand.NextVector2Circular(30, 30), inward * 1.5f, new Color(140, 40, 200), 16, 1f, ParticleType.Spark);
            }

            if (aiTimer >= singularityNextRingTick && aiTimer < travelTicks + activeDuration)
            {
                SpawnSingularityRingWave(orb.Center, maxRingRadius, ringSegments, ringDamage);
                singularityNextRingTick = aiTimer + ringInterval;
            }

            UpdateSingularityRingWaves();

            if (aiTimer >= travelTicks + activeDuration)
            {
                ResetSingularityOverdriveState();
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 25 : 40;
                NPC.netUpdate = true;
            }
        }

        private void SpawnSingularityRingWave(Vector2 anchor, float maxRadius, int segments, int dmg)
        {
            var wave = new SingularityRingWave { SpawnTick = aiTimer, MaxRadius = maxRadius, Anchor = anchor };
            for (int i = 0; i < segments; i++)
            {
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), anchor, Vector2.Zero, ProjectileID.EnchantedBeam, dmg, 0f, proxySlot);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Projectile proj = Main.projectile[p];
                    proj.hostile = true;
                    proj.friendly = false;
                    proj.tileCollide = false;
                    proj.aiStyle = 0;
                    proj.penetrate = -1;
                    proj.timeLeft = SingularityRingLifetime + 5;
                    proj.scale = 0.8f;
                    wave.ProjectileIndices.Add(p);
                }
            }
            singularityRingWaves.Add(wave);
        }

        // Puppets each ring wave's projectiles outward from their anchor over SingularityRingLifetime
        // ticks (same manual-tracked-indices convention as the melee trio's Sovereign Guard blade
        // ring) so the ring reads as one continuous expanding shockwave rather than static points.
        private void UpdateSingularityRingWaves()
        {
            for (int w = singularityRingWaves.Count - 1; w >= 0; w--)
            {
                SingularityRingWave wave = singularityRingWaves[w];
                int age = aiTimer - wave.SpawnTick;
                float t = MathHelper.Clamp(age / (float)SingularityRingLifetime, 0f, 1f);
                float radius = MathHelper.Lerp(0f, wave.MaxRadius, t);
                bool anyAlive = false;

                for (int i = 0; i < wave.ProjectileIndices.Count; i++)
                {
                    int idx = wave.ProjectileIndices[i];
                    if (idx < 0 || idx >= Main.maxProjectiles || !Main.projectile[idx].active) continue;
                    anyAlive = true;

                    float ang = MathHelper.TwoPi * i / wave.ProjectileIndices.Count;
                    Vector2 pos = wave.Anchor + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * radius;
                    Main.projectile[idx].Center = pos;

                    if (i % 3 == 0)
                        LuminanceUtilities.SpawnParticle(pos, Vector2.Zero, new Color(140, 40, 220), 10, 0.8f, ParticleType.Spark);

                    if (t >= 1f) Main.projectile[idx].Kill();
                }

                if (!anyAlive) singularityRingWaves.RemoveAt(w);
            }
        }
    }
}