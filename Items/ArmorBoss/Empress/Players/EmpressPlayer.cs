using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using TheSanity.Items.ArmorBoss.Empress.Projectiles;

namespace TheSanity.Items.ArmorBoss.Empress.Players
{
    public class EmpressPlayer : ModPlayer
    {
        public bool empressSet;

        private const int RangeInTiles = 80;
        private const int StrikeCooldownTicks = 240; // 4 seconds at 60 tps, between bursts
        private const float StrikeBaseDamage = 48f;

        private const int StaggerTicks = 8; // ~0.13s between each dagger appearing
        private const float SpawnOffsetRadius = 90f; // daggers appear around each target, not at the player

        // Safety cap only -- not a design limit. Without one, a huge event crowd could queue
        // hundreds of daggers in one burst and tank performance. Raise it if you want more.
        private const int MaxTargetsPerBurst = 20;

        // --- Sun Dance: a continuous passive loop while wearing the full set, not tied to
        // enemies being nearby -- it just keeps firing on its own cooldown the whole time the
        // set bonus is active.
        //
        // Modeled directly on the real Empress of Light's Sun Dance: she doesn't throw
        // anything -- she hovers in place and 6 rays of light appear around her, each fixed
        // in place (no spinning). 3 sets appear one after another, each set's rays offset a
        // bit further clockwise than the last, so the whole pattern steps to a new
        // arrangement rather than smoothly turning. Same shape here, just looping continuously
        // instead of being one attack in a fixed boss-attack rotation.
        private const int SunDanceCooldownTicks = 120; // 2s at 60 tps, matches the real attack's post-sequence wait
        private const float SunDanceBaseDamage = 30f;

        private const int SunDanceWaveCount = 3; // 3 sets of rays, same as the real attack
        private const int SunDanceRaysNormal = 6; // 6 rays per set normally...
        private const int SunDanceRaysExpert = 8; // ...8 in Expert/Master, mirroring the boss's enraged count
        private const int SunDanceWaveIntervalTicks = 15; // gap between each of the 3 sets
        private static readonly float SunDanceWaveAngleOffset = MathHelper.ToRadians(20f); // each set starts a bit further clockwise than the last

        private int strikeCooldown;
        private int sunDanceCooldown;
        private int sunDanceWavesRemaining;
        private int sunDanceWaveTimer;
        private float sunDanceWaveBaseAngle;

        // Mid-burst state: queue of NPC whoAmIs still waiting for their dagger, and the
        // countdown to the next one. One dagger is queued per enemy found in range, so the
        // queue naturally grows with however many enemies are around.
        private readonly List<int> pendingTargets = new List<int>();
        private int daggerSpawnTimer;

        public override void ResetEffects()
        {
            empressSet = false;
        }

        // empressSet is now set from EmpressMask.UpdateArmorSet(Player) instead —
        // that hook lives on ModItem, not ModPlayer.

        public override void PostUpdateEquips()
        {
            if (!empressSet)
            {
                strikeCooldown = 0;
                sunDanceCooldown = 0;
                sunDanceWavesRemaining = 0;
                sunDanceWaveTimer = 0;
                pendingTargets.Clear();
                return;
            }

            // Sun Dance runs on its own independent cooldown/check every tick, regardless of
            // where the lance/dagger burst is in its own cycle -- it's a reflex to something
            // getting close, not part of that state machine.
            UpdateSunDance();

            // Mid-burst: keep popping one target off the queue on the stagger timer instead
            // of touching the cooldown or rescanning for enemies.
            if (pendingTargets.Count > 0)
            {
                daggerSpawnTimer--;
                if (daggerSpawnTimer <= 0)
                {
                    int nextTarget = pendingTargets[0];
                    pendingTargets.RemoveAt(0);
                    SpawnDaggerAt(nextTarget);
                    daggerSpawnTimer = StaggerTicks;
                }
                return;
            }

            if (strikeCooldown > 0)
            {
                strikeCooldown--;
                return;
            }

            List<NPC> targets = FindNearbyEnemies(RangeInTiles * 16f, MaxTargetsPerBurst);
            if (targets.Count == 0)
            {
                return;
            }

            // Start a new burst: one dagger queued per enemy found, first one fires immediately.
            foreach (NPC npc in targets)
            {
                pendingTargets.Add(npc.whoAmI);
            }
            daggerSpawnTimer = 0;
            strikeCooldown = StrikeCooldownTicks;
        }

        private void UpdateSunDance()
        {
            // Mid-sequence: a Sun Dance was already triggered and is still firing off its
            // remaining sets. This runs on its own little timer regardless of the cooldown
            // below, same as the real attack casting 3 sets in a row before going quiet.
            if (sunDanceWavesRemaining > 0)
            {
                sunDanceWaveTimer--;
                if (sunDanceWaveTimer <= 0)
                {
                    SpawnSunDanceWave(sunDanceWaveBaseAngle);
                    sunDanceWaveBaseAngle += SunDanceWaveAngleOffset;
                    sunDanceWavesRemaining--;
                    sunDanceWaveTimer = SunDanceWaveIntervalTicks;
                }
                return;
            }

            if (sunDanceCooldown > 0)
            {
                sunDanceCooldown--;
                return;
            }

            // No proximity check anymore -- this is a passive loop that just keeps going the
            // whole time the set bonus is active, not a reaction to something getting close.
            // Start the sequence: first set fires next tick, then two more follow behind it,
            // each rotated a bit further clockwise, just like the real attack.
            sunDanceWavesRemaining = SunDanceWaveCount;
            sunDanceWaveTimer = 0;
            sunDanceWaveBaseAngle = -MathHelper.PiOver2; // first ray points "up", rays fan out clockwise from there
            sunDanceCooldown = SunDanceCooldownTicks;
        }

        private void SpawnSunDanceWave(float baseAngle)
        {
            if (Player.whoAmI != Main.myPlayer)
            {
                return;
            }

            // Centered exactly on the player -- this is the armor's own passive glow around
            // the wearer, not a boss hovering near a separate target, so no offset here.
            Vector2 anchor = Player.Center;
            int damage = (int)(SunDanceBaseDamage * Player.GetDamage(DamageClass.Summon).Multiplicative);

            // Expert/Master mirrors the boss's enraged pattern: 8 rays instead of 6.
            int rayCount = Main.expertMode ? SunDanceRaysExpert : SunDanceRaysNormal;

            for (int i = 0; i < rayCount; i++)
            {
                float angle = baseAngle + MathHelper.TwoPi * i / rayCount;

                // velocity is Vector2.Zero and stays that way -- these rays appear at the
                // anchor and rotate in place, they are never launched anywhere.
                Projectile.NewProjectile(
                    Player.GetSource_FromThis(),
                    anchor,
                    Vector2.Zero,
                    ModContent.ProjectileType<SunDanceRay>(),
                    damage,
                    1f,
                    Player.whoAmI,
                    ai0: angle
                );
            }
        }

        private void SpawnDaggerAt(int targetWhoAmI)
        {
            if (Player.whoAmI != Main.myPlayer)
            {
                return;
            }

            if (targetWhoAmI < 0 || targetWhoAmI >= Main.maxNPCs)
            {
                return;
            }

            NPC target = Main.npc[targetWhoAmI];
            if (!target.active)
            {
                return; // this particular enemy died or despawned before its turn -- just skip it
            }

            // Appear near the enemy rather than at the player, like the real Terraprisma's blades.
            Vector2 spawnPos = target.Center + Main.rand.NextVector2CircularEdge(SpawnOffsetRadius, SpawnOffsetRadius);
            int damage = (int)(StrikeBaseDamage * Player.GetDamage(DamageClass.Summon).Multiplicative);

            Projectile.NewProjectile(
                Player.GetSource_FromThis(),
                spawnPos,
                Vector2.Zero, // TerraprismaDaggerStrike locks its own aim during its telegraph, then dashes
                ModContent.ProjectileType<TerraprismaDaggerStrike>(),
                damage,
                1f,
                Player.whoAmI,
                ai0: targetWhoAmI
            );
        }

        private List<NPC> FindNearbyEnemies(float maxDistance, int maxCount)
        {
            var results = new List<NPC>();
            float maxDistSq = maxDistance * maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.friendly || npc.dontTakeDamage || npc.immortal || !npc.chaseable)
                {
                    continue;
                }
                if (npc.lifeMax <= 5 && npc.life <= 0) // skip already-dying/critter edge cases
                {
                    continue;
                }

                float distSq = Vector2.DistanceSquared(Player.Center, npc.Center);
                if (distSq <= maxDistSq)
                {
                    results.Add(npc);
                }

                if (results.Count >= maxCount)
                {
                    break;
                }
            }

            return results;
        }
    }
}