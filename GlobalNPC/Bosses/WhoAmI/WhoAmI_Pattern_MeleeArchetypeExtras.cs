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
    // MELEE ARCHETYPE TRIO — 3 new TrueMelee/ProjMelee-exclusive attacks (STATE_ABYSSAL_CLEAVE /
    // STATE_ORBITING_BLADE_RING / STATE_DIMENSIONAL_PIERCE), wired into the existing weighted-random
    // pattern pool as indices 5/6/7 in WhoAmI_Patterns.cs. Each attack:
    //   - reads the player's predicted position via GetPredictiveInterceptPoint() (WhoAmI_SatSetPhysics.cs)
    //   - uses ApplySnapDash()/ApplyBrakingImpulse()/ExecuteSpeedCanceledTeleport()/GetOrbitalCrawlPosition()
    //     from the same file rather than reinventing movement primitives
    //   - spawns its hostile projectiles with owner == proxySlot, so they automatically pick up the
    //     full neon-outline / scrolling-noise / chromatic-trail / impact-shockwave treatment from
    //     WhoAmI_VFX_ProjectileShader.cs for free
    //   - gets an ambient per-state tint layer from WhoAmI_VFX_Attacks.cs (already wired there)
    //   - is meaningfully more aggressive in Phase 2 (more hits, tighter timing, wider prediction lead)
    // ================================================================================================
    public partial class WhoAmI
    {
        // ============================================================================================
        // ATTACK 1: "ABYSSAL CLEAVE & FRACTURED SPACE"
        // Predictive dash -> massive arc slash -> the dash path itself lingers as a noise-distorted
        // spatial tear, which explodes into micro-shards ~1 second later.
        // ============================================================================================
        private Vector2 abyssalDashStart = Vector2.Zero;
        private Vector2 abyssalDashEnd = Vector2.Zero;
        private bool abyssalDashLaunched = false;
        private bool abyssalArcSlashed = false;
        private bool abyssalShardsTriggered = false;
        private int abyssalShardTriggerTick = -1;

        private void ResetAbyssalCleaveState()
        {
            abyssalDashStart = NPC.Center;
            abyssalDashEnd = NPC.Center;
            abyssalDashLaunched = false;
            abyssalArcSlashed = false;
            abyssalShardsTriggered = false;
            abyssalShardTriggerTick = -1;
        }

        private void HandleAbyssalCleave(Player target)
        {
            int windup = isPhase2 ? 12 : 18;
            int dashDuration = isPhase2 ? 16 : 20;
            float dashSpeed = isPhase2 ? 30f : 22f;
            const int shardDelay = 60; // ~1 real-time second regardless of phase, per the design brief

            // --- WINDUP: telegraph the coming dash direction; never fully static (SAT SET rule 3) ---
            if (!abyssalDashLaunched && aiTimer < windup)
            {
                NPC.damage = 0;
                NPC.velocity *= 0.85f;
                Vector2 bob = GetSatSetBobOffset(1.4f, 6f);
                NPC.Center += bob * 0.05f;
                if (aiTimer % 4 == 0)
                {
                    Vector2 toTarget = GetPredictiveInterceptPoint(target, isPhase2 ? 26f : 18f) - NPC.Center;
                    if (toTarget != Vector2.Zero) toTarget.Normalize();
                    LuminanceUtilities.SpawnParticle(NPC.Center + toTarget * 50f, toTarget * 2f, new Color(120, 30, 200), 16, 1f, ParticleType.Spark);
                }
                return;
            }

            // --- LAUNCH: snap-dash toward the predictive intercept point ---
            if (!abyssalDashLaunched)
            {
                abyssalDashStart = NPC.Center;
                Vector2 intercept = GetPredictiveInterceptPoint(target, isPhase2 ? 30f : 22f);
                Vector2 dir = intercept - NPC.Center;
                if (dir != Vector2.Zero) dir.Normalize(); else dir = new Vector2(NPC.direction, 0f);
                ApplySnapDash(dir, dashSpeed);
                abyssalDashEnd = NPC.Center + dir * (dashDuration * dashSpeed);
                abyssalDashLaunched = true;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                for (int i = 0; i < 14; i++)
                    LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(4, 4), new Color(140, 40, 220), 22, 1.3f, ParticleType.Spark);
                NPC.netUpdate = true;
            }

            int dashTick = aiTimer - windup;
            if (dashTick >= 0 && dashTick < dashDuration)
            {
                NPC.damage = isPhase2 ? 130 : 90; // contact damage while the dash itself is live
                // "lingering, noise-distorted spatial tear" trail sampled along the dash path
                if (dashTick % 2 == 0)
                {
                    LuminanceUtilities.SpawnParticle(NPC.Center, -NPC.velocity * 0.1f, new Color(140, 40, 220), 30, 1.4f, ParticleType.Spark);
                    LuminanceUtilities.SpawnParticle(NPC.Center + Main.rand.NextVector2Circular(10, 10), Vector2.Zero, Color.Black * 0.6f, 26, 1.1f, ParticleType.Spark);
                }
                return;
            }

            // --- ARC SLASH at the end of the dash ---
            if (!abyssalArcSlashed)
            {
                abyssalArcSlashed = true;
                abyssalDashEnd = NPC.Center;
                ApplyBrakingImpulse(0.12f);
                NPC.damage = isPhase2 ? 150 : 110;

                int slashCount = isPhase2 ? 9 : 7;
                float arcWidth = MathHelper.ToRadians(isPhase2 ? 150f : 120f);
                Vector2 aim = target.Center - NPC.Center;
                if (aim == Vector2.Zero) aim = new Vector2(NPC.direction, 0f);
                for (int i = 0; i < slashCount; i++)
                {
                    float t = slashCount == 1 ? 0.5f : i / (float)(slashCount - 1);
                    float a = MathHelper.Lerp(-arcWidth * 0.5f, arcWidth * 0.5f, t);
                    SpawnMeleeSlash(target, a);
                }
                bossWeaponSwingTimer = bossWeaponSwingMax;
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, NPC.Center);
                abyssalShardTriggerTick = aiTimer + shardDelay;
                NPC.netUpdate = true;
                return;
            }

            // --- HOLD: the tear lingers while we wait to shard-burst it ---
            NPC.damage = 0;
            NPC.velocity *= 0.9f;
            // BUGFIX: pure damping converges to a full stop within a few ticks - small continuous
            // bob keeps SAT SET rule 3 satisfied during the ~60-tick wait for the shard burst.
            NPC.Center += GetSatSetBobOffset(1.2f, 10f) * 0.08f;
            if (aiTimer % 5 == 0)
            {
                Vector2 tearPoint = Vector2.Lerp(abyssalDashStart, abyssalDashEnd, Main.rand.NextFloat());
                LuminanceUtilities.SpawnParticle(tearPoint, Main.rand.NextVector2Circular(1.5f, 1.5f), new Color(150, 60, 230), 20, 0.9f, ParticleType.Spark);
            }

            // --- SHARD BURST: explodes into micro-shards ~1 second after the slash ---
            if (!abyssalShardsTriggered && abyssalShardTriggerTick >= 0 && aiTimer >= abyssalShardTriggerTick)
            {
                abyssalShardsTriggered = true;
                int tearPoints = isPhase2 ? 6 : 4;
                for (int p = 0; p < tearPoints; p++)
                {
                    float t = tearPoints == 1 ? 0.5f : p / (float)(tearPoints - 1);
                    Vector2 tearPoint = Vector2.Lerp(abyssalDashStart, abyssalDashEnd, t);
                    SpawnFracturedSpaceShardBurst(tearPoint);
                }
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 8f, 0.3f);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
            }

            if (abyssalShardTriggerTick >= 0 && aiTimer > abyssalShardTriggerTick + 20)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 25 : 40;
                NPC.netUpdate = true;
            }
        }

        // Small hostile shard projectiles bursting outward from a point along the tear line. Reuses
        // a plain vanilla sprite (ProjectileID.Bone) as an inert shard silhouette rather than
        // requiring a new custom asset - it's automatically recolored/reskinned by the shared
        // Lucille Karma projectile shader's neon+noise pass (WhoAmI_VFX_ProjectileShader.cs), the
        // same way SpawnMeleeSlash reuses ProjectileID.TerraBlade2Shot for the boss's own slashes.
        private void SpawnFracturedSpaceShardBurst(Vector2 point)
        {
            int shardCount = isPhase2 ? 6 : 4;
            int dmg = isPhase2 ? 60 : 45;
            for (int i = 0; i < shardCount; i++)
            {
                float ang = MathHelper.TwoPi * i / shardCount + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 vel = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * (isPhase2 ? 9f : 6.5f);
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), point, vel, ProjectileID.Bone, dmg, 0f, proxySlot);
                if (p >= 0 && p < Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                    Main.projectile[p].tileCollide = false;
                    Main.projectile[p].timeLeft = 40;
                    Main.projectile[p].scale = 0.9f;
                    Main.projectile[p].penetrate = 1;
                }
                LuminanceUtilities.SpawnParticle(point, vel * 0.4f, new Color(190, 120, 255), 20, 1f, ParticleType.Spark);
            }
        }

        // ============================================================================================
        // ATTACK 2: "ORBITING BLADE RING (SOVEREIGN GUARD)"
        // 6 giant glowing copies of the mimicked melee weapon are summoned in a ring, rotate rapidly
        // while the boss keeps drifting (no static idle), then fire off one-by-one toward the
        // player's predicted vector - each successive blade leading further than the last, fanning
        // the volley across the player's likely dodge path instead of all converging on one point.
        // ============================================================================================
        private readonly List<int> bladeRingProjectileIndices = new List<int>();
        private float bladeRingBaseAngle = 0f;
        private int bladeRingFireIndex = 0;
        private int bladeRingFireTimer = 0;
        private Vector2 bladeRingAnchor = Vector2.Zero;
        private const int BladeRingCount = 6;
        private const float BladeRingRadius = 150f;

        private void ResetOrbitingBladeRingState()
        {
            KillTrackedBladeRingProjectiles();
            bladeRingProjectileIndices.Clear();
            bladeRingBaseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
            bladeRingFireIndex = 0;
            bladeRingFireTimer = 0;
            bladeRingAnchor = NPC.Center;
        }

        private void KillTrackedBladeRingProjectiles()
        {
            foreach (int idx in bladeRingProjectileIndices)
                if (idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].active)
                    Main.projectile[idx].Kill();
        }

        private void HandleOrbitingBladeRing(Player target)
        {
            int channelDuration = isPhase2 ? 55 : 75;
            float rotSpeed = isPhase2 ? 0.05f : 0.03f;
            int fireInterval = isPhase2 ? 5 : 8;

            NPC.damage = 0;

            if (aiTimer == 0)
            {
                for (int i = 0; i < BladeRingCount; i++)
                {
                    float ang = bladeRingBaseAngle + MathHelper.TwoPi * i / BladeRingCount;
                    Vector2 pos = bladeRingAnchor + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * BladeRingRadius;
                    int idx = SpawnSovereignBlade(pos, ang, isPhase2 ? 2.5f : 2.2f, 0);
                    bladeRingProjectileIndices.Add(idx);
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, NPC.Center);
                NPC.netUpdate = true;
            }

            if (aiTimer < channelDuration)
            {
                // No static idle: the boss itself keeps a tight orbital crawl near the ring's anchor
                // while "parked" channeling, per WhoAmI_SatSetPhysics.cs rule 3.
                // BUGFIX: 30px radius at a slow 0.1 blend read as nearly static - widened + faster
                // blend so the "no static idle" drift is actually visible during the channel window.
                Vector2 crawl = GetOrbitalCrawlPosition(bladeRingAnchor, 70f, Main.GlobalTimeWrappedHourly * 1.2f);
                NPC.velocity = Vector2.Lerp(NPC.velocity, (crawl - NPC.Center) * 0.18f, 0.2f);

                for (int i = 0; i < bladeRingProjectileIndices.Count; i++)
                {
                    int idx = bladeRingProjectileIndices[i];
                    if (idx < 0 || idx >= Main.maxProjectiles || !Main.projectile[idx].active) continue;
                    float ang = bladeRingBaseAngle + MathHelper.TwoPi * i / BladeRingCount + rotSpeed * aiTimer;
                    Vector2 pos = bladeRingAnchor + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * BladeRingRadius;
                    Main.projectile[idx].velocity = pos - Main.projectile[idx].Center;
                    Main.projectile[idx].Center = pos;
                    Main.projectile[idx].rotation = ang + MathHelper.PiOver2;
                }
                return;
            }

            // --- FIRE SEQUENTIALLY toward the player's predicted vector ---
            bladeRingFireTimer++;
            if (bladeRingFireTimer >= fireInterval && bladeRingFireIndex < bladeRingProjectileIndices.Count)
            {
                bladeRingFireTimer = 0;
                int idx = bladeRingProjectileIndices[bladeRingFireIndex];
                if (idx >= 0 && idx < Main.maxProjectiles && Main.projectile[idx].active)
                {
                    // Multi-angle prediction: each successive blade leads the player further than
                    // the last, per the Phase 2 escalation brief ("multi-angle prediction").
                    float leadTicks = (isPhase2 ? 22f : 16f) + bladeRingFireIndex * (isPhase2 ? 4f : 3f);
                    Vector2 intercept = GetPredictiveInterceptPoint(target, leadTicks);
                    Vector2 dir = intercept - Main.projectile[idx].Center;
                    if (dir != Vector2.Zero) dir.Normalize(); else dir = new Vector2(NPC.direction, 0f);
                    float speed = isPhase2 ? 15f : 11f;
                    Main.projectile[idx].velocity = dir * speed;
                    Main.projectile[idx].rotation = dir.ToRotation();
                    Main.projectile[idx].timeLeft = 45;
                    Main.projectile[idx].damage = isPhase2 ? 95 : 65;
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, Main.projectile[idx].Center);
                    for (int p = 0; p < 6; p++)
                        LuminanceUtilities.SpawnParticle(Main.projectile[idx].Center, dir * 2f, new Color(210, 230, 255), 16, 1f, ParticleType.Spark);
                }
                bladeRingProjectileIndices[bladeRingFireIndex] = -1; // handed off to vanilla flight now, stop puppeting it
                bladeRingFireIndex++;
            }

            if (bladeRingFireIndex >= bladeRingProjectileIndices.Count && aiTimer > channelDuration + BladeRingCount * fireInterval + 20)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 20 : 35;
                bladeRingProjectileIndices.Clear();
                NPC.netUpdate = true;
            }
        }

        // Giant glowing melee-weapon copy used for the Sovereign Guard ring. Reuses
        // the boss's current weapon projectile type at a large scale (same reuse convention as
        // SpawnMeleeSlash), rather than a hardcoded proxy projectile, since this is a boss-owned "blade prop"
        // that gets puppeted by hand every tick, not a fire-and-forget mimicked shot (that's
        // FireAttackProjectile's job). damage == 0 while parked/channeling means it can't friendly-
        // -fire the player mid-orbit; HandleOrbitingBladeRing sets the real damage the instant it's
        // launched.
        private int SpawnSovereignBlade(Vector2 position, float rotation, float scale, int damage)
        {
            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), position, Vector2.Zero, GetWeaponProjectileType(activeWeapon), damage, 0f, proxySlot);
            if (p < 0 || p >= Main.maxProjectiles) return -1;

            Projectile proj = Main.projectile[p];
            proj.hostile = true;
            proj.friendly = false;
            proj.tileCollide = false;
            proj.penetrate = -1;
            proj.aiStyle = 0;
            proj.timeLeft = 999; // puppeted manually every tick while parked; overwritten the moment it fires
            proj.scale = scale;
            proj.rotation = rotation;
            proj.width = proj.height = (int)(80 * scale);
            proj.Center = position;

            for (int i = 0; i < 4; i++)
                LuminanceUtilities.SpawnParticle(position, Vector2.Zero, new Color(210, 230, 255), 14, 0.8f, ParticleType.Spark);

            return p;
        }

        // ============================================================================================
        // ATTACK 3: "DIMENSIONAL PIERCE / FLASH STRIKE"
        // The boss blinks rapidly to 3 points forming a triangle around the player. Each blink point
        // holds a static afterimage; once all 3 are placed, they lunge inward one after another at
        // extreme speed, dragging a chromatic trail (handled automatically by the shared projectile
        // shader once the lunge is a real moving projectile).
        // ============================================================================================
        private Vector2[] dimensionalBlinkPoints = new Vector2[3];
        private bool[] dimensionalLunged = new bool[3];
        private int dimensionalBlinkIndex = -1;
        private int dimensionalLungeReadyTick = -1;
        private const int DimensionalBlinkCount = 3;

        private void ResetDimensionalPierceState()
        {
            dimensionalBlinkPoints = new Vector2[DimensionalBlinkCount];
            dimensionalLunged = new bool[DimensionalBlinkCount];
            dimensionalBlinkIndex = -1;
            dimensionalLungeReadyTick = -1;
        }

        private void HandleDimensionalPierce(Player target)
        {
            NPC.damage = 0;
            float triangleRadius = isPhase2 ? 420f : 340f;
            int blinkInterval = isPhase2 ? 11 : 15;
            int lungeDelayAfterLastBlink = isPhase2 ? 16 : 22;
            int lungeStagger = isPhase2 ? 6 : 9;
            float lungeSpeed = isPhase2 ? 40f : 30f;
            int lungeDamage = isPhase2 ? 160 : 120;

            if (dimensionalBlinkIndex == -1)
            {
                // Lay out the triangle around the player's CURRENT position at the moment the
                // attack starts - the boss then blinks between these fixed points (matches the
                // brief: "blinks rapidly in a triangle pattern around the player").
                float baseAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                for (int i = 0; i < DimensionalBlinkCount; i++)
                {
                    float ang = baseAngle + MathHelper.TwoPi * i / DimensionalBlinkCount;
                    dimensionalBlinkPoints[i] = target.Center + new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang)) * triangleRadius;
                }
                dimensionalBlinkIndex = 0;
                NPC.netUpdate = true;
            }

            // --- BLINK to the next triangle point on schedule ---
            if (dimensionalBlinkIndex < DimensionalBlinkCount && aiTimer >= dimensionalBlinkIndex * blinkInterval)
            {
                NPC.Center = dimensionalBlinkPoints[dimensionalBlinkIndex];
                NPC.velocity = Vector2.Zero;
                for (int i = 0; i < 10; i++)
                    LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(3, 3), new Color(255, 80, 190), 18, 1.1f, ParticleType.Spark);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item92, NPC.Center);
                dimensionalLunged[dimensionalBlinkIndex] = false;
                dimensionalBlinkIndex++;
                NPC.netUpdate = true;

                if (dimensionalBlinkIndex >= DimensionalBlinkCount)
                    dimensionalLungeReadyTick = aiTimer + lungeDelayAfterLastBlink;
            }

            // Static afterimages hang at each visited point until their lunge fires.
            for (int i = 0; i < dimensionalBlinkIndex; i++)
            {
                if (dimensionalLunged[i]) continue;
                if (Main.rand.NextBool(4))
                    LuminanceUtilities.SpawnParticle(dimensionalBlinkPoints[i], Vector2.Zero, new Color(200, 60, 230) * 0.5f, 10, 0.9f, ParticleType.Spark);
            }

            // No static idle while parked at the final blink point waiting to lunge.
            if (dimensionalBlinkIndex >= DimensionalBlinkCount)
                NPC.velocity = GetSatSetBobOffset(1.8f, 3f) * 0.1f;

            // --- LUNGE: each afterimage dashes inward, staggered rather than all at once ---
            if (dimensionalLungeReadyTick >= 0 && aiTimer >= dimensionalLungeReadyTick)
            {
                for (int i = 0; i < DimensionalBlinkCount; i++)
                {
                    if (dimensionalLunged[i]) continue;
                    dimensionalLunged[i] = true;

                    Vector2 dir = target.Center - dimensionalBlinkPoints[i];
                    if (dir != Vector2.Zero) dir.Normalize(); else dir = new Vector2(NPC.direction, 0f);

                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), dimensionalBlinkPoints[i], dir * lungeSpeed, GetWeaponProjectileType(activeWeapon), lungeDamage, 0f, proxySlot);
                    if (p >= 0 && p < Main.maxProjectiles)
                    {
                        Main.projectile[p].hostile = true;
                        Main.projectile[p].friendly = false;
                        Main.projectile[p].tileCollide = false;
                        Main.projectile[p].aiStyle = 0;
                        Main.projectile[p].penetrate = -1;
                        Main.projectile[p].timeLeft = 30;
                        Main.projectile[p].scale = 1.6f;
                        Main.projectile[p].rotation = dir.ToRotation();
                    }
                    for (int j = 0; j < 8; j++)
                        LuminanceUtilities.SpawnParticle(dimensionalBlinkPoints[i], dir * 3f, new Color(255, 80, 190), 20, 1.2f, ParticleType.Spark);

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, dimensionalBlinkPoints[i]);
                    dimensionalLungeReadyTick = aiTimer + lungeStagger; // schedule the next afterimage's lunge
                    break; // one afterimage per call, staggered rather than simultaneous
                }
            }

            bool allLunged = dimensionalBlinkIndex >= DimensionalBlinkCount && dimensionalLunged[0] && dimensionalLunged[1] && dimensionalLunged[2];
            if (allLunged && aiTimer > dimensionalLungeReadyTick + 15)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 20 : 35;
                NPC.netUpdate = true;
            }
        }
    }
}