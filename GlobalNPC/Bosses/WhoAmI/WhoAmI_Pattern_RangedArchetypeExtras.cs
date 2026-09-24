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
        // REDESIGN: instead of a laser-sight grid, the boss lobs a handful of sprite "bombs" out
        // around the player. Each bomb telegraphs briefly where it lands, then detonates into a
        // burst of projectiles fired from whichever ranged weapon is currently being mimicked.
        // ============================================================================================
        private readonly List<Vector2> laserGridBombPoints = new List<Vector2>();
        private readonly List<int> laserGridBombFuse = new List<int>(); // tick (relative to spawn) each bomb detonates on
        private bool laserGridBombsSpawned = false;
        private const int LaserGridBombCount = 5;
        private const int LaserGridBombTravelTicks = 30; // ticks the bomb spends arcing to its landing point before it's armed
        private const int LaserGridBombFuseDelay = 20;   // extra ticks after landing before it actually detonates

        private void ResetVectorLaserGridState()
        {
            laserGridBombPoints.Clear();
            laserGridBombFuse.Clear();
            laserGridBombsSpawned = false;
        }

        private void HandleVectorLaserGrid(Player target)
        {
            // aiTimer is already incremented once per tick, unconditionally, in WhoAmI.cs AI() (right
            // before the `switch (aiState)` that dispatches into this method) - do NOT increment it
            // again here (see the double-increment bug class documented on HandleHomingClusterComet
            // below for what that does: the `aiTimer == 1` entry check never fires and nothing spawns).
            int detonateInterval = isPhase2 ? 5 : 8; // stagger between each bomb's detonation

            NPC.damage = 0;
            // Real slow hover-strafe around the player the whole time, instead of standing still while
            // the bombs are out (SAT SET rule 3).
            float strafeAngle = Main.GlobalTimeWrappedHourly * (isPhase2 ? 1.1f : 0.8f);
            Vector2 hoverGoal = target.Center + new Vector2((float)Math.Cos(strafeAngle), (float)Math.Sin(strafeAngle) * 0.5f) * 300f;
            NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverGoal - NPC.Center) * 0.05f, 0.12f);

            if (!laserGridBombsSpawned && aiTimer == 1)
            {
                laserGridBombsSpawned = true;
                for (int i = 0; i < LaserGridBombCount; i++)
                {
                    Vector2 landing = target.Center + Main.rand.NextVector2CircularEdge(220f, 220f);
                    laserGridBombPoints.Add(landing);
                    laserGridBombFuse.Add(LaserGridBombTravelTicks + LaserGridBombFuseDelay + i * detonateInterval);
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                NPC.netUpdate = true;
            }

            // Bomb sprites arc in and sit armed (blinking) at their landing point until their fuse runs out.
            for (int i = 0; i < laserGridBombPoints.Count; i++)
            {
                int fuseTick = laserGridBombFuse[i];
                if (aiTimer >= fuseTick) continue; // already detonated below

                if (aiTimer < LaserGridBombTravelTicks)
                {
                    // Falling-in arc from the boss toward the landing point.
                    float t = aiTimer / (float)LaserGridBombTravelTicks;
                    Vector2 from = NPC.Center;
                    Vector2 pos = Vector2.Lerp(from, laserGridBombPoints[i], t) - new Vector2(0f, (float)Math.Sin(t * MathHelper.Pi) * 140f);
                    LuminanceUtilities.SpawnParticle(pos, Vector2.Zero, new Color(90, 230, 120), 10, 0.8f, ParticleType.Spark);
                }
                else
                {
                    // Armed and telegraphing at the landing point - blink faster as the fuse gets close.
                    int ticksLeft = fuseTick - aiTimer;
                    int blinkRate = ticksLeft < 15 ? 2 : 6;
                    if (aiTimer % blinkRate == 0)
                    {
                        for (int r = 0; r < 3; r++)
                        {
                            float a = MathHelper.TwoPi * r / 3f + Main.GlobalTimeWrappedHourly * 4f;
                            Vector2 p = laserGridBombPoints[i] + new Vector2((float)Math.Cos(a), (float)Math.Sin(a)) * 20f;
                            LuminanceUtilities.SpawnParticle(p, Vector2.Zero, Color.OrangeRed * 0.8f, 10, 0.7f, ParticleType.Spark);
                        }
                    }
                }
            }

            // Detonate any bomb whose fuse has just run out.
            for (int i = 0; i < laserGridBombPoints.Count; i++)
            {
                if (aiTimer != laserGridBombFuse[i]) continue;
                DetonateLaserGridBomb(laserGridBombPoints[i], target);
            }

            int lastFuse = laserGridBombFuse.Count > 0 ? laserGridBombFuse[laserGridBombFuse.Count - 1] : 0;
            if (aiTimer > lastFuse + 15)
            {
                laserGridBombPoints.Clear();
                laserGridBombFuse.Clear();
                laserGridBombsSpawned = false;
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 25 : 40;
                NPC.netUpdate = true;
            }
        }

        // Detonates one bomb: a burst of projectiles fired from whatever ranged weapon is currently
        // being mimicked ("projectile dari senjata ranger yang sedang dipakai"), fanned out in a ring
        // rather than all aimed at one point.
        private void DetonateLaserGridBomb(Vector2 point, Player target)
        {
            int shardCount = isPhase2 ? 10 : 7;
            int dmg = isPhase2 ? 42 : 30;
            float speed = isPhase2 ? 8.5f : 6.5f;
            int projType = ResolveMimickedProjectileType();

            for (int i = 0; i < shardCount; i++)
            {
                float ang = MathHelper.TwoPi * i / shardCount + Main.rand.NextFloat(-0.1f, 0.1f);
                Vector2 vel = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * speed;
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), point, vel, projType, dmg, 0f, proxySlot);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Projectile proj = Main.projectile[p];
                    proj.hostile = true;
                    proj.friendly = false;
                    proj.tileCollide = false;
                    proj.penetrate = 1;
                    proj.timeLeft = 70;
                    proj.scale = 1.1f;
                }
            }

            ScreenShakeSystem.StartShakeAtPoint(point, 6f, 0.2f);
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, point);
            for (int i = 0; i < 16; i++)
                LuminanceUtilities.SpawnParticle(point, Main.rand.NextVector2Circular(4, 4), new Color(90, 230, 120), 20, 1.1f, ParticleType.Spark);
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
            // NOTE: aiTimer already increments once per tick in WhoAmI.cs AI(), before the switch
            // that dispatches here - see the comment in HandleVectorLaserGrid above. This method
            // used to double-increment it (via a stray aiTimer++ that used to be right here), which
            // meant `aiTimer == 0` below was NEVER true on any tick this handler actually ran on.
            // That's a much worse bug here than in the other two attacks in this file: the comet
            // projectile (cometIndex) never got spawned, cometFractured never became true, and
            // EVERY exit path in this method is gated behind cometFractured - so the boss got stuck
            // in STATE_HOMING_CLUSTER_COMET permanently (just hovering/orbiting the player, doing
            // nothing) any time the pattern RNG picked this attack for a Ranged-classified weapon.
            // Removed the duplicate increment, and added the timeout below as a safety net so a
            // failed/skipped spawn (e.g. NewProjectile running out of slots) can never soft-lock the
            // fight like this again.
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

            if (cometIndex == -1 && aiTimer == 1)
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

            // SAFETY NET: if the comet still hasn't spawned a few ticks in (e.g. NewProjectile ran
            // out of free projectile slots), don't sit in this state forever waiting for a fracture
            // that can never happen - bail back to idle. This is what should have caught the
            // double-increment bug described above too, and keeps this pattern from ever being able
            // to soft-lock the fight again for any other reason that stops cometIndex from being set.
            if (cometIndex == -1 && aiTimer > 5)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = 15;
                NPC.netUpdate = true;
                return;
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
        //
        // FIX ("boss nembak kotak ungu gajelas pas pake senjata ranged"): this was missing the exact
        // placeholder guard that FireAttackProjectileAimed already has in WhoAmI_Helpers.cs. Almost
        // every ammo-based gun (S.D.M.G., Chain Gun, Minishark, etc.) stores ProjectileID.
        // PurificationPowder in Item.shoot as a meaningless placeholder - the real fired projectile
        // normally comes from Player.PickAmmo(), which never runs for this boss's dummy owner.
        // PurificationPowder IS a valid ID (> ProjectileID.None), so the old check let it straight
        // through - every pattern that calls this (Vector Laser Grid bombs, Homing Cluster Comet,
        // Ricochet Barrage, Starfall Convergence, plus the Magic-side extras) ended up spawning a
        // REAL, hostile Clentaminator paint-powder blob instead of a bullet whenever the boss was
        // mimicking an ammo-based gun. Layered under this pattern set's own tint VFX and often
        // scaled way up (Homing Cluster Comet's "giant condensed projectile" is 2.1x-2.6x scale) or
        // fired in a dense burst (Singularity Overdrive's 12-16 round ring, tinted dark purple), a
        // pile of those blobs reads exactly as the reported "unclear purple box" instead of a shot.
        private int ResolveMimickedProjectileType()
        {
            if (activeWeapon == null)
                return ProjectileID.EnchantedBeam;

            int projType = activeWeapon.shoot;
            if (projType <= ProjectileID.None || projType == ProjectileID.PurificationPowder)
                projType = activeWeapon.CountsAsClass(DamageClass.Ranged) ? ProjectileID.BulletHighVelocity : ProjectileID.EnchantedBeam;

            return projType;
        }

        // ============================================================================================
        // ATTACK 3: "SINGULARITY OVERDRIVE"
        // REDESIGN: the boss snap-dashes straight into the player, then detonates into a ring of
        // projectiles fired from whichever ranged weapon is currently being mimicked. This whole
        // dash-then-detonate beat repeats 3 times in a row before the boss backs off.
        // ============================================================================================
        private int singularityDashIndex = 0;
        private int singularityCycleStartTick = 0;
        private bool singularityDashLaunched = false;
        private bool singularityExploded = false;
        private Vector2 singularityDashTargetPoint = Vector2.Zero;
        private const int SingularityDashCount = 3;

        private void ResetSingularityOverdriveState()
        {
            singularityDashIndex = 0;
            singularityCycleStartTick = 0;
            singularityDashLaunched = false;
            singularityExploded = false;
            singularityDashTargetPoint = Vector2.Zero;
        }

        private void HandleSingularityOverdrive(Player target)
        {
            // aiTimer already increments once per tick in WhoAmI.cs AI(), before the switch that
            // dispatches here - see the comment in HandleVectorLaserGrid above. Do not add a second
            // increment here; every timing check below is relative to singularityCycleStartTick, which
            // is reset to the CURRENT aiTimer each time a new dash cycle starts (see the bottom of this
            // method) - the same "first real tick of a phase is always N+1, never N" convention used
            // throughout this pattern set.
            int windup = isPhase2 ? 10 : 14;
            int dashDuration = isPhase2 ? 12 : 16;
            float dashSpeed = isPhase2 ? 34f : 26f;
            int recoverAfterExplode = isPhase2 ? 12 : 16;
            int explosionCount = isPhase2 ? 16 : 12;
            int explosionDamage = isPhase2 ? 42 : 30;
            int dashContactDamage = isPhase2 ? 45 : 32;

            NPC.damage = 0;
            int localTick = aiTimer - singularityCycleStartTick;

            // --- WINDUP: telegraph the upcoming dash at the player's predicted position ---
            if (!singularityDashLaunched && localTick < windup)
            {
                if (localTick == 1)
                    singularityDashTargetPoint = GetPredictiveInterceptPoint(target, isPhase2 ? 20f : 14f);

                // Never fully static during the windup (SAT SET rule 3) - a slight brake + bob instead
                // of a hard freeze.
                NPC.velocity *= 0.85f;
                NPC.Center += GetSatSetBobOffset(1.5f, 8f) * 0.05f;

                if (localTick % 3 == 0)
                {
                    Vector2 toTarget = singularityDashTargetPoint - NPC.Center;
                    if (toTarget != Vector2.Zero) toTarget.Normalize();
                    LuminanceUtilities.SpawnParticle(NPC.Center + toTarget * 40f, toTarget * 2f, new Color(150, 40, 220), 16, 1f, ParticleType.Spark);
                }
                return;
            }

            // --- LAUNCH: snap-dash straight at the player's predicted position ---
            if (!singularityDashLaunched)
            {
                singularityDashLaunched = true;
                Vector2 dir = singularityDashTargetPoint - NPC.Center;
                if (dir != Vector2.Zero) dir.Normalize(); else dir = new Vector2(NPC.direction, 0f);
                ApplySnapDash(dir, dashSpeed);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item42, NPC.Center);
                for (int i = 0; i < 14; i++)
                    LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(4, 4), new Color(150, 40, 220), 22, 1.3f, ParticleType.Spark);
                NPC.netUpdate = true;
            }

            int dashTick = localTick - windup;
            if (dashTick >= 0 && dashTick < dashDuration)
            {
                NPC.damage = dashContactDamage; // contact damage while the dash itself is live
                if (dashTick % 2 == 0)
                    LuminanceUtilities.SpawnParticle(NPC.Center, -NPC.velocity * 0.1f, new Color(150, 40, 220), 24, 1.2f, ParticleType.Spark);
                return;
            }

            // --- EXPLODE: detonate into a ring of projectiles from the mimicked ranged weapon ---
            if (!singularityExploded)
            {
                singularityExploded = true;
                NPC.damage = 0;
                ApplyBrakingImpulse(0.4f);

                int projType = ResolveMimickedProjectileType();
                for (int i = 0; i < explosionCount; i++)
                {
                    float ang = MathHelper.TwoPi * i / explosionCount;
                    Vector2 vel = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * (isPhase2 ? 8.5f : 6.5f);
                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, projType, explosionDamage, 0f, proxySlot);
                    if (p >= 0 && p < Main.maxProjectiles)
                    {
                        Projectile proj = Main.projectile[p];
                        proj.hostile = true;
                        proj.friendly = false;
                        proj.tileCollide = false;
                        proj.penetrate = 1;
                        proj.timeLeft = 70;
                        proj.scale = 1.1f;
                    }
                }

                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 9f, 0.3f);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                for (int i = 0; i < 22; i++)
                    LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), new Color(150, 40, 220), 24, 1.3f, ParticleType.Spark);
                NPC.netUpdate = true;
            }

            // --- RECOVER, then either line up the next dash or finish after the 3rd ---
            ApplyBrakingImpulse(0.15f);
            if (localTick >= windup + dashDuration + recoverAfterExplode)
            {
                singularityDashIndex++;
                if (singularityDashIndex >= SingularityDashCount)
                {
                    aiState = STATE_IDLE;
                    aiTimer = 0;
                    patternCooldown = isPhase2 ? 25 : 40;
                    NPC.netUpdate = true;
                    return;
                }

                singularityCycleStartTick = aiTimer;
                singularityDashLaunched = false;
                singularityExploded = false;
                NPC.netUpdate = true;
            }
        }
    }
}