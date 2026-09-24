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
            // FIX: the premise of the old comment was backwards - aiTimer already increments once
            // per tick, unconditionally, in WhoAmI.cs AI() right before the switch that dispatches
            // here. The aiTimer++ that used to sit here double-counted it, running the whole
            // windup/dash/shard timeline at 2x its intended speed.

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
                // BALANCE ("sakit banget"): dash-through + arc slash (below) itungannya combo 2-hit -
                // dulu 90/130 disini + 110/150 di slash = worst case 200/280 (44%/62% dari HP
                // referensi 450) kalau dua2nya kena. Diturunin biar totalnya ~27-35%, sama budget
                // yang dipakai combo lain (lihat Riposte Cascade/Dimensional Pierce).
                NPC.damage = isPhase2 ? 75 : 55; // contact damage while the dash itself is live
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
                // Pairs with the dash damage above - total worst-case (both hits) now 55+65=120
                // (26.7%) phase1, 75+85=160 (35.6%) phase2.
                NPC.damage = isPhase2 ? 85 : 65;

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
            // Trimmed alongside the dash+slash rebalance above - this shard burst is a 3rd potential
            // hit source on the same Abyssal Cleave cast (outward-spread, so usually only 0-1 shard
            // realistically lands), kept modest so an unlucky "hit by everything" cast still stays
            // survivable rather than stacking on top of the already-rebalanced dash+slash total.
            int dmg = isPhase2 ? 34 : 24;
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
        // REDESIGN: the boss summons a single clone of itself. The clone takes up a post well out to
        // the player's LEFT, while the real body crosses over to the player's RIGHT - a pincer setup.
        // Once both are in position, the clone and the real body alternate throwing a giant glowing
        // copy of the mimicked melee weapon at the player, taking turns, for 10 throws total (clone
        // opens the exchange, then real body, then clone again, ...). Each thrown weapon spins
        // continuously in flight (puppeted rotation, tracked below) instead of holding a fixed angle.
        // The clone itself never attacks independently - the REAL body's Handle method below drives
        // both throws, using the clone only as a puppeted stand-in position/visual (see
        // isSovereignGuardClone in WhoAmI.cs and RunSovereignGuardCloneAI below), so there's only ever
        // one "brain" running this pattern.
        // ============================================================================================
        private int sovereignGuardCloneWho = -1; // NPC.whoAmI of the summoned clone, or -1 if none/dead
        private int sovereignGuardThrowsCompleted = 0;
        private readonly List<int> sovereignGuardThrownProjectiles = new List<int>(); // tracked purely to puppet their spin each tick
        private const int SovereignGuardThrowCount = 10;
        private const float SovereignCloneOffsetX = 480f; // clone's post: well out to the player's left ("agak jauh")
        private const float SovereignRealOffsetX = 300f;  // real body's post: mirrored to the player's right
        private const int SovereignPositionDuration = 40; // ticks spent gliding into the left/right pincer
        private const float SovereignSpinSpeed = 0.5f; // radians/tick each thrown weapon spins while flying

        private void ResetOrbitingBladeRingState()
        {
            DismissSovereignGuardCloneIfAny();
            sovereignGuardCloneWho = -1;
            sovereignGuardThrowsCompleted = 0;
            sovereignGuardThrownProjectiles.Clear();
        }

        private void DismissSovereignGuardCloneIfAny()
        {
            if (sovereignGuardCloneWho >= 0 && sovereignGuardCloneWho < Main.maxNPCs)
            {
                NPC cloneNpc = Main.npc[sovereignGuardCloneWho];
                if (cloneNpc.active && cloneNpc.ModNPC is WhoAmI clone && clone.isSovereignGuardClone)
                    clone.DismissSovereignGuardCloneQuietly();
            }
            sovereignGuardCloneWho = -1;
        }

        private void HandleOrbitingBladeRing(Player target)
        {
            // aiTimer already increments once per tick, unconditionally, in WhoAmI.cs AI() right
            // before the switch that dispatches here - do NOT add a second aiTimer++ in this method
            // (see the header comment on this file / the fix notes elsewhere in this file for what
            // that class of bug does: it makes the `aiTimer == 1` entry check below never fire, so
            // the clone would never actually get spawned).
            int throwInterval = isPhase2 ? 14 : 20;

            NPC.damage = 0;

            if (aiTimer == 1)
            {
                Vector2 clonePos = target.Center + new Vector2(-SovereignCloneOffsetX, 0f);
                int npcIndex = NPC.NewNPC(NPC.GetSource_FromAI(), (int)clonePos.X, (int)clonePos.Y, ModContent.NPCType<WhoAmI>());
                if (npcIndex >= 0 && npcIndex < Main.maxNPCs && Main.npc[npcIndex].ModNPC is WhoAmI clone)
                {
                    clone.isSovereignGuardClone = true;
                    clone.sovereignGuardCloneOwner = NPC.whoAmI;
                    clone.dummyPlayer = dummyPlayer; // share the same proxy visuals - see WhoAmI.cs header comment
                    clone.aiState = STATE_IDLE;
                    clone.aiTimer = 0;

                    // Unhittable/harmless "prop" stand-in, same convention as the Mirror Mirage decoys -
                    // this clone is here to sell the pincer visual and hold a throw position, not to be
                    // a second real target.
                    Main.npc[npcIndex].dontTakeDamage = true;
                    Main.npc[npcIndex].life = 1;
                    Main.npc[npcIndex].lifeMax = 1;
                    Main.npc[npcIndex].damage = 0;
                    Main.npc[npcIndex].netUpdate = true;

                    sovereignGuardCloneWho = Main.npc[npcIndex].whoAmI;
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, NPC.Center);
                NPC.netUpdate = true;
            }

            // Safety net: if the clone never spawned (e.g. NewNPC ran out of slots) or got cleaned up
            // some other way mid-attack, don't stall in this state forever waiting for a partner that
            // no longer exists.
            bool cloneAlive = sovereignGuardCloneWho >= 0 && sovereignGuardCloneWho < Main.maxNPCs
                && Main.npc[sovereignGuardCloneWho].active && Main.npc[sovereignGuardCloneWho].ModNPC is WhoAmI cloneRef && cloneRef.isSovereignGuardClone;
            if (!cloneAlive)
            {
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = 20;
                NPC.netUpdate = true;
                return;
            }

            // Real body glides to its RIGHT-side post - no static idle while the clone settles in
            // (SAT SET rule 3).
            Vector2 realGoal = target.Center + new Vector2(SovereignRealOffsetX, 0f);
            EaseVelocityTowards((realGoal - NPC.Center) * 0.3f, Math.Min(aiTimer / (float)SovereignPositionDuration, 1f), EasingCurves.Sine, EasingType.InOut, 0.35f);

            // Keep every thrown weapon spinning in flight for as long as it stays active, regardless
            // of which phase of the attack we're currently in (a throw from earlier can still be
            // mid-flight while the next one is being lined up).
            for (int i = sovereignGuardThrownProjectiles.Count - 1; i >= 0; i--)
            {
                int idx = sovereignGuardThrownProjectiles[i];
                if (idx < 0 || idx >= Main.maxProjectiles || !Main.projectile[idx].active)
                {
                    sovereignGuardThrownProjectiles.RemoveAt(i);
                    continue;
                }
                Main.projectile[idx].rotation += SovereignSpinSpeed;
            }

            if (aiTimer < SovereignPositionDuration)
                return; // still taking up the pincer positions

            // --- ALTERNATING THROWS: clone opens (even count), real body follows (odd count), 10 total ---
            int throwTick = aiTimer - SovereignPositionDuration;
            if (sovereignGuardThrowsCompleted < SovereignGuardThrowCount && throwTick % throwInterval == 0)
            {
                bool cloneThrowsThisTime = sovereignGuardThrowsCompleted % 2 == 0;
                Vector2 throwerPos = cloneThrowsThisTime ? Main.npc[sovereignGuardCloneWho].Center : NPC.Center;
                ThrowSovereignGuardWeapon(throwerPos, target);
                sovereignGuardThrowsCompleted++;
                NPC.netUpdate = true;
            }

            if (sovereignGuardThrowsCompleted >= SovereignGuardThrowCount && throwTick > throwInterval + 15)
            {
                DismissSovereignGuardCloneIfAny();
                aiState = STATE_IDLE;
                aiTimer = 0;
                patternCooldown = isPhase2 ? 20 : 35;
                NPC.netUpdate = true;
            }
        }

        // Fires one giant glowing copy of the mimicked melee weapon from fromPos toward the player -
        // reuses GetWeaponProjectileType(activeWeapon) the same way the old ring-blade prop did, just
        // as a straightforward fire-and-forget thrown shot instead of a hand-puppeted orbiting prop.
        // The index is handed off to sovereignGuardThrownProjectiles so HandleOrbitingBladeRing can
        // keep spinning it every tick while it's in flight ("sambil muter-muter").
        private void ThrowSovereignGuardWeapon(Vector2 fromPos, Player target)
        {
            int dmg = isPhase2 ? 70 : 50;
            float speed = isPhase2 ? 15f : 11f;
            float scale = isPhase2 ? 2.3f : 2f;

            Vector2 dir = target.Center - fromPos;
            if (dir != Vector2.Zero) dir.Normalize(); else dir = new Vector2(NPC.direction, 0f);

            int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), fromPos, dir * speed, GetWeaponProjectileType(activeWeapon), dmg, 0f, proxySlot);
            if (p >= 0 && p < Main.maxProjectiles)
            {
                Projectile proj = Main.projectile[p];
                proj.hostile = true;
                proj.friendly = false;
                proj.tileCollide = false;
                proj.aiStyle = 0;
                proj.penetrate = -1;
                proj.timeLeft = 60;
                proj.scale = scale;
                proj.rotation = dir.ToRotation(); // starting angle - HandleOrbitingBladeRing spins it from here every tick
                sovereignGuardThrownProjectiles.Add(p);
            }

            for (int i = 0; i < 8; i++)
                LuminanceUtilities.SpawnParticle(fromPos, dir * 2f, new Color(210, 230, 255), 16, 1f, ParticleType.Spark);
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, fromPos);
            ScreenShakeSystem.StartShakeAtPoint(fromPos, 5f, 0.15f);
        }

        // ---------------------------------------------------------------------------------------
        // CLONE-SIDE LOGIC (runs on the Sovereign Guard clone instance only - see the
        // isSovereignGuardClone short-circuit at the top of AI() in WhoAmI.cs)
        // ---------------------------------------------------------------------------------------
        private int sovereignGuardCloneLifetime = 0;
        private bool sovereignGuardCloneDismissed = false;

        private void RunSovereignGuardCloneAI()
        {
            // Holds its flank and just bobs slightly - "no static idle" - while the real body drives
            // the alternating throws. All of the actual attack logic lives on the real boss instance
            // in HandleOrbitingBladeRing above; this clone never acts on its own.
            NPC.velocity *= 0.9f;
            NPC.damage = 0;
            sovereignGuardCloneLifetime++;

            Vector2 bob = GetSatSetBobOffset(1.3f, 6f);
            NPC.Center += bob * 0.05f;

            if (Main.rand.NextBool(3))
                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(1, 1), new Color(210, 230, 255), 10, 0.7f, ParticleType.Spark);

            // Safety net: if the owner boss vanishes/desyncs without cleaning up, or the pattern has
            // simply ended, the clone self-dismisses after a generous timeout instead of lingering.
            bool ownerStillRunningPattern = sovereignGuardCloneOwner >= 0 && sovereignGuardCloneOwner < Main.maxNPCs && Main.npc[sovereignGuardCloneOwner].active
                && Main.npc[sovereignGuardCloneOwner].ModNPC is WhoAmI owner && owner.aiState == STATE_ORBITING_BLADE_RING;
            if (!ownerStillRunningPattern || sovereignGuardCloneLifetime > 400)
                DismissSovereignGuardCloneQuietly();
        }

        // Removes this clone without any death effect (orderly cleanup, not a "kill").
        public void DismissSovereignGuardCloneQuietly()
        {
            if (sovereignGuardCloneDismissed) return;
            sovereignGuardCloneDismissed = true;

            for (int i = 0; i < 10; i++)
                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(2, 2), new Color(210, 230, 255) * 0.5f, 14, 0.8f, ParticleType.Spark);

            NPC.life = 0;
            NPC.active = false;
            NPC.netUpdate = true;
        }

        // Called from OnKill() (see the isSovereignGuardClone branch in WhoAmI.cs). The clone is
        // normally unhittable (CanBeHitByProjectile/CanBeHitByItem in WhoAmI.cs both return false for
        // it), so this is a belt-and-suspenders safety net only, mirroring OnMirageDecoyKilled.
        private void OnSovereignGuardCloneKilled()
        {
            if (sovereignGuardCloneDismissed) return;
            sovereignGuardCloneDismissed = true;

            ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 10f, 0.3f);
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Shatter, NPC.Center);

            int shardCount = 8;
            for (int i = 0; i < shardCount; i++)
            {
                float angle = MathHelper.TwoPi / shardCount * i + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 vel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 7f;
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ProjectileID.PurpleLaser, 16, 0f, proxySlot);
                if (p >= 0 && p < Main.maxProjectiles) { Main.projectile[p].hostile = true; Main.projectile[p].friendly = false; ClampBossProjectileLifetime(p); }
            }
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
            // FIX: the premise of the old comment was backwards - aiTimer already increments once
            // per tick, unconditionally, in WhoAmI.cs AI() right before the switch that dispatches
            // here. The aiTimer++ that used to sit here double-counted it, running the blink/lunge
            // timing at 2x its intended speed.
            NPC.damage = 0;
            float triangleRadius = isPhase2 ? 420f : 340f;
            int blinkInterval = isPhase2 ? 11 : 15;
            int lungeDelayAfterLastBlink = isPhase2 ? 16 : 22;
            int lungeStagger = isPhase2 ? 6 : 9;
            float lungeSpeed = isPhase2 ? 40f : 30f;
            // BALANCE ("sakit banget"): 3 TERPISAH projectile lunge, masing2 dulu 120/160 dmg - kalau
            // ketiganya kena itu 360 (80% dari HP referensi 450) phase1, atau 480 (107%!, lebih dari
            // full HP) phase2 - bisa literally one-shot dari HP penuh. Diturunin biar worst-case
            // ketiga-tiganya kena ada di ~30-35% HP referensi (3x45=135=30% phase1, 3x52=156=~35%
            // phase2), konsisten sama budget combo yang sama dipakai di Riposte Cascade/Abyssal
            // Cleave/Mirror Waltz.
            int lungeDamage = isPhase2 ? 52 : 45;

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