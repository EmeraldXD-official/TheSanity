using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ================================================================================================
    // MIRROR MIRAGE / SHELL GAME — STATE_MIRROR_MIRAGE — Any archetype / Desperation
    // ================================================================================================
    // An illusion-based guessing game:
    //   A) SPLIT + ORBIT (ticks 0-44)   - the boss "splits" into 3 visually identical mirages
    //                                     (itself + 2 decoys, spawned as flagged clone NPC instances -
    //                                     see isMirageDecoy in WhoAmI.cs) that orbit the player rapidly.
    //   B) LOCK-IN (tick 45)            - the 3 mirages lock into Left / Right / Top slots around the
    //                                     player and begin a synchronous heavy-weapon charge windup,
    //                                     with screen shakes escalating the tension.
    //   C) THE TRAP (46-150)            - only ONE mirage is real. Decoys are now fully UNHITTABLE (see
    //                                     CanBeHitByProjectile/CanBeHitByItem in WhoAmI.cs) - attacking a
    //                                     decoy just whiffs through it harmlessly, no burst, no feedback,
    //                                     because it was never really there. Hitting the REAL boss
    //                                     (ModifyHitByProjectile/Item hooks in WhoAmI.cs call
    //                                     BreakMirageChannel) staggers it and ends the state early,
    //                                     exposing it to clean damage. OnMirageDecoyKilled is kept around
    //                                     only as an inert safety net (see its own comment below) - it is
    //                                     no longer reachable through normal combat.
    //   D) RELEASE (if never broken)    - if the full channel completes untouched, the real boss
    //                                     unleashes the charged strike as a short area burst instead.
    //   E) CLEANUP                      - any surviving decoys are dismissed quietly.
    //
    // Decoys are spawned as additional instances of THIS SAME NPC type rather than fake visuals, so they
    // are fully, correctly hittable/renderable for free - see isMirageDecoy / dummyPlayer sharing below.
    //
    // ─── INTEGRATION CHECKLIST ─────────────────────────────────────────────────────────────────────
    //   1. WhoAmI.cs fields: isMirageDecoy, mirageOwnerWhoAmI, mirageDecoySlots, mirageLayoutPositions,
    //      mirageRealPositionIndex, mirageChannelBroken, mirrorMirageCooldownTimer, FindRealBossIndex()
    //      ✔ done
    //   2. WhoAmI.cs constants: STATE_MIRROR_MIRAGE = 13, STATE_MIRAGE_DECOY_HOLD = 14  ✔ done
    //   3. WhoAmI.cs AI(): decoy short-circuit at top of AI() -> RunMirageDecoyAI()  ✔ done
    //      WhoAmI.cs AI() switch: case STATE_MIRROR_MIRAGE -> HandleMirrorMirage(player)  ✔ done
    //      WhoAmI.cs STATE_IDLE case: TryStartMirrorMirage(player) rolled independently of archetype ✔
    //   4. WhoAmI.cs CheckDead / OnKill / ModifyHitByProjectile / ModifyHitByItem: decoy + break-channel
    //      guards  ✔ done
    // ================================================================================================
    public partial class WhoAmI
    {
        private const int MirageOrbitDuration = 44;
        private const int MirageLockTick = 45;
        private const int MirageChannelEnd = 150;
        private const float MirageLayoutRadius = 240f;
        private const int MirrorMirageBaseCooldown = 900; // ~15s between attempts, rolled from STATE_IDLE

        // ---------------------------------------------------------------------------------------
        // ENTRY POINT (called from STATE_IDLE, archetype-agnostic)
        // ---------------------------------------------------------------------------------------
        private bool TryStartMirrorMirage(Player target)
        {
            // Never double-trigger, and never during the desperation sequence (that has its own cutscene).
            if (aiState == STATE_MIRROR_MIRAGE || aiState == STATE_DESPERATION_CUTSCENE) return false;
            if (mirageDecoySlots[0] != -1 || mirageDecoySlots[1] != -1) return false;

            // More frequent in phase 2 - a 1-in-3 roll on an already-throttled cooldown.
            if (!Main.rand.NextBool(isPhase2 ? 2 : 3))
            {
                mirrorMirageCooldownTimer = 120; // quick re-roll, not a full cooldown wasted on a miss
                return false;
            }

            aiState = STATE_MIRROR_MIRAGE;
            aiTimer = 0;
            mirageChannelBroken = false;
            mirageRealPositionIndex = Main.rand.Next(3); // 0=Left, 1=Right, 2=Top
            NPC.netUpdate = true;
            return true;
        }

        // ---------------------------------------------------------------------------------------
        // MAIN HANDLER (runs on the REAL boss instance only)
        // ---------------------------------------------------------------------------------------
        private void HandleMirrorMirage(Player target)
        {
            aiTimer++;
            NPC.damage = 0;

            // If a hit already broke the channel this tick, resolve immediately regardless of phase.
            if (mirageChannelBroken)
            {
                ResolveMirrorMirage(staggered: true);
                return;
            }

            // ---- A) SPLIT + ORBIT ----
            if (aiTimer <= MirageOrbitDuration)
            {
                if (aiTimer == 1)
                {
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, target.Center);
                    for (int i = 0; i < 20; i++)
                        LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(4, 4), Color.White, 20, 1.1f, ParticleType.Spark);
                }

                float orbitAngle = aiTimer * 0.35f;
                Vector2 orbitPos = GetOrbitalCrawlPosition(target.Center, 180f, orbitAngle);
                EaseVelocityTowards((orbitPos - NPC.Center) * 0.5f, aiTimer / (float)MirageOrbitDuration, EasingCurves.Circ, EasingType.In);

                if (Main.rand.NextBool(2))
                    LuminanceUtilities.SpawnParticle(NPC.Center, -NPC.velocity * 0.2f, Color.MediumPurple, 16, 1f, ParticleType.Spark);

                if (aiTimer == MirageOrbitDuration)
                {
                    ComputeMirageLayout(target);
                    SpawnMirageDecoys(target);
                    ScreenShakeSystem.StartShakeAtPoint(target.Center, 14f, 0.4f);
                }
                return;
            }

            // ---- B/C) LOCK-IN + CHANNEL WINDUP ----
            Vector2 lockedPos = mirageLayoutPositions[mirageRealPositionIndex];
            EaseVelocityTowards((lockedPos - NPC.Center) * 0.6f, 1f, EasingCurves.Cubic, EasingType.Out);

            float channelProgress = (aiTimer - MirageLockTick) / (float)(MirageChannelEnd - MirageLockTick);
            if (channelProgress >= 0f)
            {
                // Escalating screen shake as the synchronous charge builds tension.
                if (aiTimer % 20 == 0)
                    ScreenShakeSystem.StartShakeAtPoint(target.Center, 4f + channelProgress * 10f, 0.2f);

                if (Main.rand.NextBool(2))
                    LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(2, 2), Color.Gold, 14, 0.8f + channelProgress, ParticleType.Spark);
            }

            if (aiTimer >= MirageChannelEnd)
            {
                ResolveMirrorMirage(staggered: false);
            }
        }

        private void ComputeMirageLayout(Player target)
        {
            // Left, Right, Top - fixed compass slots so the "guess" is spatially unambiguous.
            mirageLayoutPositions[0] = target.Center + new Vector2(-MirageLayoutRadius, 0f);
            mirageLayoutPositions[1] = target.Center + new Vector2(MirageLayoutRadius, 0f);
            mirageLayoutPositions[2] = target.Center + new Vector2(0f, -MirageLayoutRadius);
        }

        private void SpawnMirageDecoys(Player target)
        {
            int slot = 0;
            for (int i = 0; i < 3; i++)
            {
                if (i == mirageRealPositionIndex) continue;

                int npcIndex = NPC.NewNPC(NPC.GetSource_FromAI(), (int)mirageLayoutPositions[i].X, (int)mirageLayoutPositions[i].Y, ModContent.NPCType<WhoAmI>());
                if (npcIndex < 0 || npcIndex >= Main.maxNPCs) continue;

                NPC decoyNpc = Main.npc[npcIndex];
                if (decoyNpc.ModNPC is WhoAmI decoy)
                {
                    decoy.isMirageDecoy = true;
                    decoy.mirageOwnerWhoAmI = NPC.whoAmI;
                    decoy.dummyPlayer = dummyPlayer; // share the same proxy visuals - see file header
                    decoy.aiState = STATE_MIRAGE_DECOY_HOLD;
                    decoy.aiTimer = 0;
                    // FIX: decoys are an illusion to be picked apart by EYE, not a target you're allowed
                    // to damage - dontTakeDamage=true here (belt-and-suspenders alongside the
                    // CanBeHitByProjectile/CanBeHitByItem overrides in WhoAmI.cs, which are what actually
                    // stop the hit from registering at all). life/lifeMax left at 1 only so nothing else
                    // that inspects NPC.life on a boss-type NPC misbehaves; they're not meant to reach 0
                    // through combat anymore.
                    decoyNpc.dontTakeDamage = true;
                    decoyNpc.life = 1;
                    decoyNpc.lifeMax = 1;
                    decoyNpc.damage = 0; // decoys never deal contact damage - they're a trap, not a threat
                    decoyNpc.netUpdate = true;

                    if (slot < mirageDecoySlots.Length) mirageDecoySlots[slot] = decoyNpc.whoAmI;
                    slot++;
                }
            }
        }

        // Ends the state either by stagger (real boss was hit - "exposing to clean damage") or by a
        // natural release (channel completed untouched - the boss gets to unleash the charged strike).
        private void ResolveMirrorMirage(bool staggered)
        {
            DismissRemainingDecoys();

            if (staggered)
            {
                NPC.velocity *= 0.5f; // per the brief - stagger, don't fully freeze
                CombatText.NewText(NPC.getRect(), Color.Gold, "Found me!", true);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath6, NPC.Center);
            }
            else
            {
                // Charged strike releases as a short omnidirectional shard burst instead of a guaranteed
                // single-target hit, so it's dodgeable rather than a free hit for "guessing wrong".
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 20f, 0.5f);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                int shardCount = isPhase2 ? 14 : 10;
                for (int i = 0; i < shardCount; i++)
                {
                    float angle = MathHelper.TwoPi / shardCount * i;
                    Vector2 vel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 9f;
                    int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ProjectileID.PurpleLaser, isPhase2 ? 28 : 18, 0f, proxySlot);
                    if (p >= 0 && p < Main.maxProjectiles) { Main.projectile[p].hostile = true; Main.projectile[p].friendly = false; }
                }
            }

            aiState = STATE_IDLE;
            aiTimer = 0;
            mirageChannelBroken = false;
            patternCooldown = isPhase2 ? 70 : 100;
            mirrorMirageCooldownTimer = MirrorMirageBaseCooldown;
            NPC.netUpdate = true;
        }

        private void DismissRemainingDecoys()
        {
            for (int i = 0; i < mirageDecoySlots.Length; i++)
            {
                int who = mirageDecoySlots[i];
                if (who >= 0 && who < Main.maxNPCs)
                {
                    NPC decoyNpc = Main.npc[who];
                    if (decoyNpc.active && decoyNpc.ModNPC is WhoAmI decoy && decoy.isMirageDecoy)
                        decoy.DismissMirageDecoyQuietly();
                }
                mirageDecoySlots[i] = -1;
            }
        }

        // Called by the real boss when a hit lands on IT while mirage is active - see
        // ModifyHitByProjectile/ModifyHitByItem in WhoAmI.cs.
        private void BreakMirageChannel()
        {
            mirageChannelBroken = true;
        }

        // ---------------------------------------------------------------------------------------
        // DECOY-SIDE LOGIC (runs on decoy instances only - see the isMirageDecoy short-circuit at
        // the top of AI() in WhoAmI.cs)
        // ---------------------------------------------------------------------------------------
        private int mirageDecoyLifetime = 0;
        private bool mirageDecoyDismissed = false;

        private void RunMirageDecoyAI()
        {
            // Decoys hold their assigned slot and just bob slightly - "no static idle" - while they
            // wait to be hit (or dismissed by the owner when the state resolves).
            NPC.velocity *= 0.9f;
            NPC.damage = 0;
            mirageDecoyLifetime++;

            Vector2 bob = GetSatSetBobOffset(1.3f, 6f);
            NPC.Center += bob * 0.05f;

            if (Main.rand.NextBool(3))
                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(1, 1), Color.Gold, 10, 0.7f, ParticleType.Spark);

            // Safety net: if the owner boss vanishes/desyncs without cleaning up (disconnect, mod
            // reload, etc.), the decoy self-dismisses after a generous timeout instead of lingering.
            bool ownerStillAlive = mirageOwnerWhoAmI >= 0 && mirageOwnerWhoAmI < Main.maxNPCs && Main.npc[mirageOwnerWhoAmI].active
                && Main.npc[mirageOwnerWhoAmI].ModNPC is WhoAmI owner && owner.aiState == STATE_MIRROR_MIRAGE;
            if (!ownerStillAlive || mirageDecoyLifetime > MirageChannelEnd + 40)
                DismissMirageDecoyQuietly();
        }

        // Removes this decoy WITHOUT the shard-burst death effect (used for orderly cleanup rather
        // than "the player found and popped it").
        public void DismissMirageDecoyQuietly()
        {
            if (mirageDecoyDismissed) return;
            mirageDecoyDismissed = true;

            for (int i = 0; i < 10; i++)
                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(2, 2), Color.MediumPurple * 0.5f, 14, 0.8f, ParticleType.Spark);

            NPC.life = 0;
            NPC.active = false;
            NPC.netUpdate = true;
        }

        // Called from OnKill() (see the isMirageDecoy branch in WhoAmI.cs). Historically this fired
        // when a player struck a decoy down; decoys are now unhittable (CanBeHitByProjectile/
        // CanBeHitByItem in WhoAmI.cs both return false for them), so this path shouldn't be reachable
        // through normal combat anymore. Left in place as a harmless safety net only.
        private void OnMirageDecoyKilled()
        {
            if (mirageDecoyDismissed) return; // already being cleaned up quietly - don't double-burst
            mirageDecoyDismissed = true;

            ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 12f, 0.35f);
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Shatter, NPC.Center);

            int shardCount = 8;
            for (int i = 0; i < shardCount; i++)
            {
                float angle = MathHelper.TwoPi / shardCount * i + Main.rand.NextFloat(-0.2f, 0.2f);
                Vector2 vel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 7f;
                int p = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ProjectileID.PurpleLaser, 16, 0f, proxySlot);
                if (p >= 0 && p < Main.maxProjectiles) { Main.projectile[p].hostile = true; Main.projectile[p].friendly = false; }
            }

            for (int i = 0; i < 25; i++)
                LuminanceUtilities.SpawnParticle(NPC.Center, Main.rand.NextVector2Circular(5, 5), Color.MediumPurple, 22, 1.2f, ParticleType.Spark);
        }
    }
}