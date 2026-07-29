using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Luminance.Core.Graphics;
using Luminance.Common.StateMachines;

namespace TheSanity.GlobalNPC.Bosses.EmpressRework
{
    public class EmpressAdvancedRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public enum AttackState
        {
            TeleportStrike,
            EverlastingBarrage,
            CloneAssault,
            MiniEmpressCombo,
            SunDanceAttack,
            PrismaticBladeDance,
            RainbowRampage,
            RealityTear,
            GardenOfLightBloom,
            HallowedMirrorImages,
            MiniFairyStarfall,
            FairyRoyaleFinale
        }

        public PushdownAutomata<EntityAIState<AttackState>, AttackState> StateMachine;

        // ==== Umum ====
        private int bossPhase = 1;
        private bool initializedStateMachine = false;

        // ==== TeleportStrike ====
        private int trackedProjectileIndex = -1;
        private int trackedProjectileIdentity = -1;
        private bool isInvisible = false;
        private int tpStrikeTimer = 0;
        private const int TeleportStrikeWindup = 90;
        private bool isPreparingStrike = false;
        private int strikePrepareTimer = 0;
        private int strikeChainIndex = 0;
        private int strikeChainTotal = 1;

        private int StrikeChainWindup(int linkIndex) => linkIndex == 0 ? TeleportStrikeWindup : Math.Max(35, TeleportStrikeWindup - 20 - linkIndex * 10);

        // ==== EverlastingBarrage ====
        private int everlastingTimer = 0;
        private int everlastingCount = 0;
        private int everlastingSide = 1;
        private const int WeaveSwitchEveryShots = 2;

        // ==== MiniEmpressCombo ====
        private int miniEmpressComboTimer = 0;
        private bool miniEmpressSpawned = false;
        private const int MiniEmpressSummonWindup = 65;
        private int miniEmpressWaveIndex = 0;
        private const int MiniEmpressWaveGap = 55;

        // ==== CloneAssault ====
        private int cloneAssaultTimer = 0;
        private bool cloneSpawned = false;
        private const int CloneAssaultWindup = 30;
        private const int CloneAssaultDuration = 200;
        private bool reinforcementSpawned = false;
        private bool finaleBurstFired = false;
        private const int ReinforcementTick = CloneAssaultDuration / 2;
        private const int FinaleBurstTick = CloneAssaultDuration - 30;

        // ==== SunDanceAttack ====
        private int sunDanceTimer = 0;
        private bool sunDanceSpawned = false;
        private const int SunDanceWindup = 20;
        private const int SunDanceStateDuration = 200;
        private bool sunDanceReinforcementSpawned = false;
        private const int SunDanceReinforcementTick = SunDanceStateDuration / 2;

        // ==== PrismaticBladeDance ====
        private int bladeDanceTimer = 0;
        private int bladeDanceWaveIndex = 0;
        private const int BladeDanceWaveInterval = 26;

        // ==== RainbowRampage ====
        private int rampageTimer = 0;
        private int rampageDashIndex = 0;
        private bool rampageIsDashing = false;
        private Vector2 rampageDashStart;
        private Vector2 rampageDashTarget;
        private int rampageDashTimer = 0;
        private const int RampageDashDuration = 18;
        private const int RampagePauseDuration = 22;
        private const int RampageTelegraphTime = 20;
        private bool rampageTelegraphActive = false;

        // ==== RealityTear (Laser Telegraph Updated) ====
        private int realityTearTimer = 0;
        private int realityTearBlinkIndex = 0;
        private Vector2 realityTearAimDir = Vector2.Zero;
        private bool realityTearTelegraphActive = false;
        private const int RealityTearTelegraphDuration = 14; 
        private const int RealityTearBlinkInterval = 34; // Diperpanjang agar ada jeda telegraph laser

        // ==== GardenOfLightBloom ====
        private int gardenTimer = 0;
        private int gardenWaveIndex = 0;
        private const int GardenWaveInterval = 45;

        // ==== HallowedMirrorImages ====
        private int mirrorTimer = 0;
        private int mirrorAnchorIndex = 0;
        private bool mirrorSpawned = false;
        private const int MirrorSwapInterval = 50;
        private const int MirrorDuration = 260;

        // ==== MiniFairyStarfall ====
        private int starlightTimer = 0;
        private int starlightWaveIndex = 0;
        private const int StarlightWaveInterval = 40;

        // ==== FairyRoyaleFinale ====
        private int finaleTimer = 0;
        private bool finaleSpawned = false;
        private const int FinaleDuration = 220;

        // ==== Afterimage trail ====
        private struct AfterimageSnapshot
        {
            public Vector2 Position;
            public Rectangle Frame;
            public float Rotation;
            public int Direction;
            public float Scale;
        }
        private readonly List<AfterimageSnapshot> afterimages = new List<AfterimageSnapshot>();
        private const int MaxAfterimages = 12;
        private float afterimageIntensity = 0.35f;

        private void UpdateAfterimageIntensity(AttackState state)
        {
            bool highMotion = state == AttackState.RainbowRampage
                || state == AttackState.RealityTear
                || state == AttackState.HallowedMirrorImages
                || (state == AttackState.TeleportStrike && isInvisible);
            afterimageIntensity = highMotion ? 0.9f : 0.35f;
        }

        private void CaptureAfterimage(NPC npc)
        {
            afterimages.Insert(0, new AfterimageSnapshot
            {
                Position = npc.Center,
                Frame = npc.frame,
                Rotation = npc.rotation,
                Direction = npc.spriteDirection,
                Scale = npc.scale
            });
            if (afterimages.Count > MaxAfterimages)
                afterimages.RemoveAt(afterimages.Count - 1);
        }

        public int CurrentPhase => bossPhase;

        public override bool AppliesToEntity(NPC npc, bool lateInstantiation)
        {
            return npc.type == NPCID.HallowBoss;
        }

        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.HallowBoss)
            {
                npcLoot.Add(Terraria.GameContent.ItemDropRules.ItemDropRule.Common(5005, 1, 1, 1));
            }
        }

        public override void SetDefaults(NPC npc)
        {
            npc.dontTakeDamage = false;
            npc.immortal = false;
            npc.chaseable = true;

            // Balance: Modifikasi damage multiplier menjadi 1.25x (sebelumnya 1.5x)
            npc.lifeMax = (int)(npc.lifeMax * 1);
            npc.life = npc.lifeMax;
            npc.damage = (int)(npc.damage * 1.25f);
            npc.defense = (int)(npc.defense + 12);
        }

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            modifiers.FinalDamage.Scale(0.65f);
        }

        public override void FindFrame(NPC npc, int frameHeight)
        {
            if (npc.type != NPCID.HallowBoss) return;

            double frameSpeed = 1.0;
            if (bossPhase == 3) frameSpeed = 1.4;

            npc.frameCounter += frameSpeed;
            if (npc.frameCounter >= 4.0)
            {
                npc.frameCounter = 0.0;
                npc.frame.Y += frameHeight;

                if (npc.frame.Y >= frameHeight * 22)
                {
                    npc.frame.Y = 0;
                }
            }
        }

        public override void AI(NPC npc)
        {
            npc.aiStyle = 0;
            npc.noGravity = true;
            npc.noTileCollide = true;
            npc.active = true;

            npc.alpha = 0;

            if (npc.ai[0] == 0) npc.ai[0] = 1;
            npc.localAI[0]++;

            if (npc.target < 0 || npc.target >= 255 || Main.player[npc.target].dead) npc.TargetClosest(true);
            Player player = Main.player[npc.target];

            float lifeRatio = (float)npc.life / npc.lifeMax;

            if (!initializedStateMachine)
            {
                StateMachine = new PushdownAutomata<EntityAIState<AttackState>, AttackState>(new EntityAIState<AttackState>(AttackState.TeleportStrike));
                initializedStateMachine = true;
            }

            StateMachine.PerformBehaviors();

            AttackState currentAttack = StateMachine.CurrentState.Identifier;

            if (bossPhase == 1 && lifeRatio < 0.65f)
            {
                bossPhase = 2;
                ResetPhaseVariables(npc);
                TriggerPhaseVisuals(npc);
            }
            else if (bossPhase == 2 && lifeRatio < 0.30f)
            {
                bossPhase = 3;
                ResetPhaseVariables(npc);
                TriggerPhaseVisuals(npc);
            }

            switch (currentAttack)
            {
                case AttackState.TeleportStrike:
                    ExecuteTeleportStrike(npc, player);
                    break;
                case AttackState.EverlastingBarrage:
                    ExecuteEverlastingAttack(npc, player);
                    break;
                case AttackState.CloneAssault:
                    ExecuteCloneAssault(npc, player);
                    break;
                case AttackState.MiniEmpressCombo:
                    ExecuteMiniEmpressCombo(npc, player);
                    break;
                case AttackState.SunDanceAttack:
                    ExecuteSunDanceAttack(npc, player);
                    break;
                case AttackState.PrismaticBladeDance:
                    ExecutePrismaticBladeDance(npc, player);
                    break;
                case AttackState.RainbowRampage:
                    ExecuteRainbowRampage(npc, player);
                    break;
                case AttackState.RealityTear:
                    ExecuteRealityTear(npc, player);
                    break;
                case AttackState.GardenOfLightBloom:
                    ExecuteGardenOfLightBloom(npc, player);
                    break;
                case AttackState.HallowedMirrorImages:
                    ExecuteHallowedMirrorImages(npc, player);
                    break;
                case AttackState.MiniFairyStarfall:
                    ExecuteMiniFairyStarfall(npc, player);
                    break;
                case AttackState.FairyRoyaleFinale:
                    ExecuteFairyRoyaleFinale(npc, player);
                    break;
            }

            UpdateAfterimageIntensity(currentAttack);
            CaptureAfterimage(npc);
        }

        private void ExecuteEverlastingAttack(NPC npc, Player player)
        {
            isInvisible = false;
            npc.alpha = 0;

            everlastingTimer++;

            if (everlastingTimer == 1)
            {
                everlastingSide = Main.rand.NextBool() ? 1 : -1;
            }

            int weaveCycle = everlastingCount / WeaveSwitchEveryShots;
            int weaveSide = (weaveCycle % 2 == 0) ? everlastingSide : -everlastingSide;
            float bob = (float)Math.Sin(everlastingTimer * 0.04f) * 40f;
            Vector2 targetPos = player.Center + new Vector2(weaveSide * 350f, -200f + bob);
            npc.velocity *= 0.93f;
            npc.velocity += (targetPos - npc.Center) * 0.05f;

            int shootInterval = Math.Max(20, 38 - (bossPhase - 1) * 7);
            int maxShots = 6 + (bossPhase - 1) * 2;
            int shotDamage = 24 + (bossPhase - 1) * 5;

            int timeToNextShot = shootInterval - ((everlastingTimer - 30) % shootInterval);

            if (everlastingTimer > 30 && everlastingCount < maxShots && timeToNextShot <= 10 && Main.netMode != NetmodeID.Server)
            {
                Dust d = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(20f, 20f), 2, 2, DustID.RainbowMk2, 0f, 0f, 100, default, 1.5f);
                d.noGravity = true;
                d.velocity *= 0.1f;
            }

            if (everlastingTimer > 30 && everlastingTimer % shootInterval == 0 && everlastingCount < maxShots)
            {
                Vector2 shootDir = Vector2.Normalize(player.Center - npc.Center);
                bool isPunctuationShot = (everlastingCount + 1) % 3 == 0;

                if (isPunctuationShot)
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        Vector2 dir = shootDir.RotatedBy(MathHelper.ToRadians(i * 16f));
                        int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 8.5f, ProjectileID.HallowBossLastingRainbow, (int)(shotDamage * 0.75f), 0f, Main.myPlayer);
                        if (proj != Main.maxProjectiles)
                        {
                            var mod = Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>();
                            mod.isBossEverlasting = true;
                            mod.attackPhase = bossPhase;
                        }
                    }
                    ScreenShakeSystem.StartShake(3f, MathHelper.TwoPi, null);
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item162, npc.Center);
                }
                else
                {
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootDir * 8f, ProjectileID.HallowBossLastingRainbow, shotDamage, 0f, Main.myPlayer);
                    if (proj != Main.maxProjectiles)
                    {
                        var mod = Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>();
                        mod.isBossEverlasting = true;
                        mod.attackPhase = bossPhase;
                    }

                    if (bossPhase >= 2)
                    {
                        Vector2 mirrorSpawn = player.Center + new Vector2(-weaveSide * 350f, -200f + bob);
                        Vector2 mirrorDir = Vector2.Normalize(player.Center - mirrorSpawn);
                        int mirrorProj = Projectile.NewProjectile(npc.GetSource_FromAI(), mirrorSpawn, mirrorDir * 8f, ProjectileID.HallowBossLastingRainbow, (int)(shotDamage * 0.8f), 0f, Main.myPlayer);
                        if (mirrorProj != Main.maxProjectiles)
                        {
                            var mod = Main.projectile[mirrorProj].GetGlobalProjectile<EmpressProjectileColorModifier>();
                            mod.isBossEverlasting = true;
                            mod.attackPhase = bossPhase;
                        }
                        if (Main.netMode != NetmodeID.Server)
                        {
                            for (int i = 0; i < 8; i++)
                            {
                                Dust d = Dust.NewDustDirect(mirrorSpawn, 2, 2, DustID.RainbowMk2, 0f, 0f, 100, default, 1.4f);
                                d.noGravity = true;
                                d.velocity = Main.rand.NextVector2Circular(2f, 2f);
                            }
                        }
                    }

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item160, npc.Center);
                }

                everlastingCount++;
            }

            if (everlastingCount >= maxShots && everlastingTimer > 300)
            {
                everlastingTimer = 0;
                everlastingCount = 0;
                SwitchToNextAttack();
            }
        }

        private void ExecuteMiniEmpressCombo(NPC npc, Player player)
        {
            npc.velocity *= 0.9f;
            miniEmpressComboTimer++;

            int totalWaves = bossPhase;
            int waveTick = miniEmpressWaveIndex * MiniEmpressWaveGap;

            if (miniEmpressWaveIndex < totalWaves && miniEmpressComboTimer == waveTick + 1)
            {
                SpawnMiniEmpresses(npc);
                miniEmpressWaveIndex++;

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(5f, MathHelper.TwoPi, null);

                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 25; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.RainbowMk2, 0f, 0f, 100, default, 2f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(6f, 6f);
                        d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.7f, 0.75f);
                    }
                }
            }

            int fullDuration = (totalWaves - 1) * MiniEmpressWaveGap + MiniEmpressSummonWindup + 20;

            if (miniEmpressComboTimer >= fullDuration)
            {
                miniEmpressComboTimer = 0;
                miniEmpressWaveIndex = 0;
                miniEmpressSpawned = false;
                SwitchToNextAttack();
            }
        }

        private void SpawnMiniEmpresses(NPC npc)
        {
            int minCount = 3;
            int maxCountExclusive = 5;
            int count = Main.rand.Next(minCount, maxCountExclusive);
            for (int i = 0; i < count; i++)
            {
                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, EmpressMiniEmpressModifier.MiniEmpressProjectileID, 0, 0f, Main.myPlayer);
                if (p != Main.maxProjectiles)
                {
                    var modifier = Main.projectile[p].GetGlobalProjectile<EmpressMiniEmpressModifier>();
                    modifier.isMiniEmpressSummon = true;
                    modifier.bossNPCIndex = npc.whoAmI;
                    modifier.summonIndex = i;
                    modifier.totalSummons = count;
                    modifier.attackPhase = bossPhase;
                }
            }
        }

        private void ExecuteTeleportStrike(NPC npc, Player player)
        {
            npc.alpha = isInvisible ? 255 : 0;

            if (strikeChainIndex == 0 && tpStrikeTimer == 0 && !isInvisible)
            {
                strikeChainTotal = bossPhase;
            }

            if (!isInvisible)
            {
                npc.velocity = Vector2.Zero;
                tpStrikeTimer++;

                int currentWindup = StrikeChainWindup(strikeChainIndex);
                if (tpStrikeTimer >= currentWindup)
                {
                    tpStrikeTimer = 0;
                    bool isFirstLink = strikeChainIndex == 0;

                    if (Main.netMode != NetmodeID.Server)
                    {
                        int heartSegments = isFirstLink ? 60 : 36;
                        float heartScale = isFirstLink ? 10f : 7f;
                        float hueShift = strikeChainIndex * 0.2f;
                        for (int i = 0; i < heartSegments; i++)
                        {
                            float t = (i / (float)heartSegments) * MathHelper.TwoPi;
                            float sinT = (float)Math.Sin(t);
                            float heartX = 16f * sinT * sinT * sinT;
                            float heartY = 13f * (float)Math.Cos(t) - 5f * (float)Math.Cos(2 * t) - 2f * (float)Math.Cos(3 * t) - (float)Math.Cos(4 * t);
                            Vector2 heartOffset = new Vector2(heartX, -heartY) * heartScale;
                            Vector2 dustVelocity = Vector2.Normalize(-heartOffset) * Main.rand.NextFloat(4f, 7f);
                            Dust dust = Dust.NewDustDirect(npc.Center + heartOffset, 0, 0, DustID.RainbowMk2, dustVelocity.X, dustVelocity.Y, 100, default, 1.5f);
                            dust.noGravity = true;
                            dust.color = Main.hslToRgb((Main.rand.NextFloat() + hueShift) % 1f, 0.6f, 0.8f);
                        }
                    }

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item160, npc.Center);

                    Vector2 shootDirection = Vector2.Normalize(player.Center - npc.Center);
                    float shootSpeed = 9f + strikeChainIndex * 1.5f;
                    int damage = 24 + (bossPhase - 1) * 4;

                    int streakCount = 5 + (bossPhase - 1) * 2;
                    float angleStep = 60f / (streakCount - 1);
                    int middleIndex = streakCount / 2;

                    int mainProjIndex = -1;
                    for (int i = 0; i < streakCount; i++)
                    {
                        float rotationAngle = MathHelper.ToRadians(-30f + (angleStep * i));
                        Vector2 finalDir = shootDirection.RotatedBy(rotationAngle);
                        int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, finalDir * shootSpeed, ProjectileID.HallowBossRainbowStreak, damage, 0f, Main.myPlayer);

                        if (proj != Main.maxProjectiles)
                        {
                            var mod = Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>();
                            mod.projectileIndexInSpread = i;
                            mod.attackPhase = bossPhase;
                        }

                        if (i == middleIndex && proj != Main.maxProjectiles) mainProjIndex = proj;
                    }

                    if (mainProjIndex != -1)
                    {
                        trackedProjectileIndex = mainProjIndex;
                        trackedProjectileIdentity = Main.projectile[mainProjIndex].identity;
                        isInvisible = true;
                        npc.dontTakeDamage = true;
                    }
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item165, npc.Center);
                }
            }
            else
            {
                bool foundProj = false;
                if (trackedProjectileIndex >= 0 && trackedProjectileIndex < Main.maxProjectiles)
                {
                    Projectile proj = Main.projectile[trackedProjectileIndex];
                    if (proj.active && proj.type == ProjectileID.HallowBossRainbowStreak && proj.identity == trackedProjectileIdentity)
                    {
                        foundProj = true;
                        ApplyChainHoming(proj, player);
                        if (proj.timeLeft <= 15) ExecuteStrikeExplosion(npc, proj);
                    }
                }

                if (!foundProj && trackedProjectileIdentity != -1)
                {
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        Projectile proj = Main.projectile[i];
                        if (proj.active && proj.type == ProjectileID.HallowBossRainbowStreak && proj.identity == trackedProjectileIdentity)
                        {
                            foundProj = true;
                            ApplyChainHoming(proj, player);
                            if (proj.timeLeft <= 15) ExecuteStrikeExplosion(npc, proj);
                            break;
                        }
                    }
                }

                if (!foundProj && trackedProjectileIdentity != -1)
                {
                    isInvisible = false;
                    npc.dontTakeDamage = false;
                    trackedProjectileIdentity = -1;
                    trackedProjectileIndex = -1;
                    npc.Center = player.Center - new Vector2(0, 250);
                    strikeChainIndex = 0;
                    strikeChainTotal = 1;
                    SwitchToNextAttack();
                }
            }
        }

        private void ApplyChainHoming(Projectile proj, Player player)
        {
            if (bossPhase < 3) return;
            Vector2 toPlayer = Vector2.Normalize(player.Center - proj.Center);
            Vector2 blended = Vector2.Normalize(Vector2.Lerp(Vector2.Normalize(proj.velocity), toPlayer, 0.02f));
            proj.velocity = blended * proj.velocity.Length();
        }

        private void ExecuteStrikeExplosion(NPC npc, Projectile proj)
        {
            npc.Center = proj.Center;
            ScreenShakeSystem.StartShake(8f, MathHelper.TwoPi, null);

            if (Main.netMode != NetmodeID.Server)
            {
                Vector2 spawnPos = npc.Center + Main.rand.NextVector2Circular(400f, 400f);
                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, Vector2.Zero, ModContent.ProjectileType<ShineFlareParticle>(), 0, 0, Main.myPlayer);
                Main.projectile[p].ai[0] = npc.Center.X;
                Main.projectile[p].ai[1] = npc.Center.Y;
            }

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item161, npc.Center);
            isPreparingStrike = true;
            strikePrepareTimer = 45;
            proj.Kill();

            int totalLances = 18 + (bossPhase - 1) * 5;
            int damage = 35 + (bossPhase - 1) * 6;
            float speedMultiplier = 12f + (bossPhase - 1) * 1.5f;

            for (int k = 0; k < totalLances; k++)
            {
                float baseAngle = MathHelper.ToRadians((360f / totalLances) * k);
                float finalPathAngle = baseAngle + MathHelper.PiOver2;
                Vector2 pathDir = Vector2.UnitX.RotatedBy(finalPathAngle);
                int lanceProj;
                if (k % 2 == 0)
                {
                    Vector2 spawnOut = npc.Center + (pathDir * 60f);
                    lanceProj = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnOut, pathDir * speedMultiplier, ProjectileID.FairyQueenLance, damage, 1f, Main.myPlayer, finalPathAngle);
                }
                else
                {
                    Vector2 spawnIn = npc.Center + (pathDir * 600f);
                    float reverseAngle = finalPathAngle + MathHelper.Pi;
                    lanceProj = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnIn, -pathDir * speedMultiplier, ProjectileID.FairyQueenLance, damage, 1f, Main.myPlayer, reverseAngle);
                }

                if (lanceProj != Main.maxProjectiles)
                {
                    var mod = Main.projectile[lanceProj].GetGlobalProjectile<EmpressProjectileColorModifier>();
                    mod.projectileIndexInSpread = k;
                    mod.attackPhase = bossPhase;
                }
            }
            isInvisible = false;
            npc.dontTakeDamage = false;
            trackedProjectileIdentity = -1;
            trackedProjectileIndex = -1;
            proj.Kill();

            strikeChainIndex++;
            if (strikeChainIndex < strikeChainTotal)
            {
                tpStrikeTimer = 0;
            }
            else
            {
                strikeChainIndex = 0;
                strikeChainTotal = 1;
                SwitchToNextAttack();
            }
        }

        private void ExecuteCloneAssault(NPC npc, Player player)
        {
            npc.velocity *= 0.9f;
            cloneAssaultTimer++;

            Vector2 hoverTarget = player.Center - new Vector2(0, 260f);
            npc.velocity += (hoverTarget - npc.Center) * 0.03f;

            if (!cloneSpawned && cloneAssaultTimer >= CloneAssaultWindup)
            {
                cloneSpawned = true;
                int cloneCount = bossPhase >= 2 ? 2 : 1;
                for (int i = 0; i < cloneCount; i++)
                {
                    float side = cloneCount == 1 ? (Main.rand.NextBool() ? 1f : -1f) : (i == 0 ? -1f : 1f);
                    SpawnAssaultClone(npc, side);
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(5f, MathHelper.TwoPi, null);
            }

            if (bossPhase >= 3 && !reinforcementSpawned && cloneAssaultTimer >= ReinforcementTick)
            {
                reinforcementSpawned = true;
                SpawnAssaultClone(npc, 0f);

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(6f, MathHelper.TwoPi, null);
                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.RainbowMk2, 0f, 0f, 100, default, 2f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(6f, 6f);
                    }
                }
            }

            if (cloneAssaultTimer > CloneAssaultWindup && cloneAssaultTimer % 35 == 0 && cloneAssaultTimer < FinaleBurstTick)
            {
                Vector2 shootDir = Vector2.Normalize(player.Center - npc.Center);
                int dmg = 20 + (bossPhase - 1) * 3;

                for (int i = -1; i <= 1; i++)
                {
                    Vector2 dir = shootDir.RotatedBy(MathHelper.ToRadians(i * 12f));
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 9f, ProjectileID.HallowBossLastingRainbow, dmg, 0f, Main.myPlayer);
                    if (proj != Main.maxProjectiles)
                    {
                        var mod = Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>();
                        mod.isBossEverlasting = bossPhase >= 3;
                        mod.attackPhase = bossPhase;
                    }
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, npc.Center);
            }

            int telegraphStart = FinaleBurstTick - 25;
            if (cloneAssaultTimer >= telegraphStart && cloneAssaultTimer < FinaleBurstTick && Main.netMode != NetmodeID.Server && Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(npc.Center + Main.rand.NextVector2Circular(50f, 50f), 2, 2, DustID.RainbowMk2, 0f, 0f, 100, default, 1.6f);
                d.noGravity = true;
                d.velocity *= 0.1f;
            }

            if (!finaleBurstFired && cloneAssaultTimer >= FinaleBurstTick)
            {
                finaleBurstFired = true;
                int novaCount = 10 + bossPhase * 2;
                int novaDamage = 18 + (bossPhase - 1) * 4;
                for (int i = 0; i < novaCount; i++)
                {
                    float angle = MathHelper.TwoPi * (i / (float)novaCount);
                    Vector2 dir = angle.ToRotationVector2();
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 8f, ProjectileID.HallowBossLastingRainbow, novaDamage, 0f, Main.myPlayer);
                    if (proj != Main.maxProjectiles)
                    {
                        var mod = Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>();
                        mod.isBossEverlasting = false;
                        mod.attackPhase = bossPhase;
                    }
                }
                ScreenShakeSystem.StartShake(8f, MathHelper.TwoPi, null);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item162, npc.Center);
            }

            if (cloneAssaultTimer >= CloneAssaultDuration)
            {
                cloneAssaultTimer = 0;
                cloneSpawned = false;
                reinforcementSpawned = false;
                finaleBurstFired = false;
                SwitchToNextAttack();
            }
        }

        private void SpawnAssaultClone(NPC npc, float side)
        {
            int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<EmpressLiteClone>(), 0, 0f, Main.myPlayer);
            if (p != Main.maxProjectiles)
            {
                var clone = Main.projectile[p].ModProjectile as EmpressLiteClone;
                if (clone != null)
                {
                    clone.bossNPCIndex = npc.whoAmI;
                    clone.attackPhase = bossPhase;
                    clone.sideOffset = side;
                }
            }
        }

        private void ExecuteSunDanceAttack(NPC npc, Player player)
        {
            npc.velocity *= 0.92f;
            sunDanceTimer++;

            Vector2 hoverTarget = player.Center - new Vector2(0, 300f);
            npc.velocity += (hoverTarget - npc.Center) * 0.04f;

            if (!sunDanceSpawned && sunDanceTimer >= SunDanceWindup)
            {
                sunDanceSpawned = true;
                SpawnSunDanceRing(npc, player, radius: 480f, angularSpeed: 0.0045f, isBig: true);

                if (bossPhase >= 2)
                {
                    SpawnSunDanceRing(npc, player, radius: 300f, angularSpeed: -0.007f, isBig: false);
                }

                SpawnMiniEmpresses(npc);

                int cloneProj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<EmpressLiteClone>(), 0, 0f, Main.myPlayer);
                if (cloneProj != Main.maxProjectiles)
                {
                    var clone = Main.projectile[cloneProj].ModProjectile as EmpressLiteClone;
                    if (clone != null)
                    {
                        clone.bossNPCIndex = npc.whoAmI;
                        clone.attackPhase = bossPhase;
                        clone.sideOffset = Main.rand.NextBool() ? 1f : -1f;
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(7f, MathHelper.TwoPi, null);
            }

            if (bossPhase >= 3 && !sunDanceReinforcementSpawned && sunDanceTimer >= SunDanceReinforcementTick)
            {
                sunDanceReinforcementSpawned = true;
                SpawnSunDanceRing(npc, player, radius: 190f, angularSpeed: 0.01f, isBig: false);

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(6f, MathHelper.TwoPi, null);
                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        Dust d = Dust.NewDustDirect(player.Center, 2, 2, DustID.RainbowMk2, 0f, 0f, 100, default, 1.8f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(4f, 4f);
                    }
                }
            }

            if (sunDanceTimer >= SunDanceStateDuration)
            {
                sunDanceTimer = 0;
                sunDanceSpawned = false;
                sunDanceReinforcementSpawned = false;
                SwitchToNextAttack();
            }
        }

        private void SpawnSunDanceRing(NPC npc, Player player, float radius, float angularSpeed, bool isBig)
        {
            int rayCount = (isBig ? 9 : 6) + (bossPhase - 1);
            int damage = (isBig ? 14 : 10) + (bossPhase - 1) * 3;

            for (int i = 0; i < rayCount; i++)
            {
                float startAngle = MathHelper.TwoPi * (i / (float)rayCount);
                int sunProj = Projectile.NewProjectile(npc.GetSource_FromAI(), player.Center, Vector2.Zero, ModContent.ProjectileType<MiniSunDanceRay>(), damage, 0f, Main.myPlayer);
                if (sunProj != Main.maxProjectiles)
                {
                    var ray = Main.projectile[sunProj].ModProjectile as MiniSunDanceRay;
                    if (ray != null)
                    {
                        ray.followPlayerIndex = player.whoAmI;
                        ray.orbitRadius = radius;
                        ray.orbitAngle = startAngle;
                        ray.angularSpeed = angularSpeed;
                        ray.rayDamage = damage;
                        ray.isBigVersion = isBig;
                    }
                }
            }
        }

        private void ExecutePrismaticBladeDance(NPC npc, Player player)
        {
            npc.velocity *= 0.93f;
            Vector2 hoverTarget = player.Center - new Vector2(0, 280f);
            npc.velocity += (hoverTarget - npc.Center) * 0.035f;

            bladeDanceTimer++;

            int totalWaves = 3 + (bossPhase - 1);

            const int telegraphTime = 14;
            int tickInWave = bladeDanceTimer % BladeDanceWaveInterval;
            if (bladeDanceWaveIndex < totalWaves && tickInWave >= BladeDanceWaveInterval - telegraphTime && Main.netMode != NetmodeID.Server)
            {
                float previewRadius = 90f + bladeDanceWaveIndex * 70f;
                int previewCount = 8 + bladeDanceWaveIndex * 3;
                float previewOffset = MathHelper.ToRadians(bladeDanceWaveIndex * 14f);
                float telegraphProgress = (tickInWave - (BladeDanceWaveInterval - telegraphTime)) / (float)telegraphTime;

                if (Main.rand.NextBool(2))
                {
                    int pick = Main.rand.Next(previewCount);
                    float previewAngle = MathHelper.TwoPi * (pick / (float)previewCount) + previewOffset;
                    Vector2 previewPos = npc.Center + previewAngle.ToRotationVector2() * previewRadius;
                    Dust d = Dust.NewDustDirect(previewPos, 1, 1, DustID.RainbowMk2, 0f, 0f, 150, default, MathHelper.Lerp(0.6f, 1.3f, telegraphProgress));
                    d.noGravity = true;
                    d.velocity *= 0.05f;
                }
            }

            if (bladeDanceTimer % BladeDanceWaveInterval == 0 && bladeDanceWaveIndex < totalWaves)
            {
                float waveRadius = 90f + bladeDanceWaveIndex * 70f;
                int lanceCount = 8 + bladeDanceWaveIndex * 3;
                int damage = 20 + (bossPhase - 1) * 4;
                float speed = 6f + bladeDanceWaveIndex * 0.8f;

                float angleOffset = MathHelper.ToRadians(bladeDanceWaveIndex * 14f);

                for (int i = 0; i < lanceCount; i++)
                {
                    float angle = MathHelper.TwoPi * (i / (float)lanceCount) + angleOffset;
                    Vector2 dir = angle.ToRotationVector2();
                    Vector2 spawnPos = npc.Center + dir * waveRadius;
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, dir * speed, ProjectileID.FairyQueenLance, damage, 1f, Main.myPlayer, angle);
                    if (proj != Main.maxProjectiles)
                    {
                        var mod = Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>();
                        mod.projectileIndexInSpread = i;
                        mod.attackPhase = bossPhase;
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(4f + bladeDanceWaveIndex, MathHelper.TwoPi, null);

                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 18; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.Center, 1, 1, DustID.RainbowMk2, 0f, 0f, 100, default, 1.8f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2CircularEdge(waveRadius * 0.02f + 2f, waveRadius * 0.02f + 2f);
                        d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.75f, 0.7f);
                    }
                }

                bladeDanceWaveIndex++;
            }

            if (bladeDanceWaveIndex >= totalWaves && bladeDanceTimer % BladeDanceWaveInterval == 0)
            {
                bladeDanceTimer = 0;
                bladeDanceWaveIndex = 0;
                SwitchToNextAttack();
            }
        }

       private void ExecuteRainbowRampage(NPC npc, Player player)
{
    int totalDashes = 3 + bossPhase;

    if (!rampageIsDashing)
    {
        rampageTimer++;
        npc.velocity *= 0.85f; // Memperlambat pergerakan saat ancang-ancang

        if (rampageDashIndex >= totalDashes)
        {
            rampageDashIndex = 0;
            rampageTimer = 0;
            rampageTelegraphActive = false;
            SwitchToNextAttack();
            return;
        }

        int telegraphStartTick = Math.Max(1, RampagePauseDuration - RampageTelegraphTime);

        // ==== 1. TELEPORT LANGSUNG KE TITIK START SAAT TELEGRAPH MULAI ====
        if (rampageTimer == telegraphStartTick)
        {
            Vector2 approachDir = Main.rand.NextVector2Unit();
            rampageDashStart = player.Center + approachDir * 500f;
            rampageDashTarget = player.Center - approachDir * 500f;

            // Teleport Empress ke awal jalur dash agar player tahu persis posisinya
            npc.Center = rampageDashStart;
            npc.velocity = Vector2.Zero;
            rampageTelegraphActive = true;

            // Efek Suara & Dust saat Teleport ke titik start
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item165, npc.Center);
            if (Main.netMode != NetmodeID.Server)
            {
                for (int i = 0; i < 16; i++)
                {
                    Dust d = Dust.NewDustDirect(npc.Center, 1, 1, DustID.RainbowMk2, 0f, 0f, 100, default, 1.6f);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(5f, 5f);
                    d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.8f, 0.7f);
                }
            }
        }

        // Efek percikan cahaya di sepanjang garis indikator
        if (rampageTelegraphActive && Main.netMode != NetmodeID.Server && Main.rand.NextBool(2))
        {
            Vector2 sparklePos = Vector2.Lerp(rampageDashStart, rampageDashTarget, Main.rand.NextFloat());
            Dust d = Dust.NewDustDirect(sparklePos, 1, 1, DustID.RainbowMk2, 0f, 0f, 150, default, 1f);
            d.noGravity = true;
            d.velocity *= 0.05f;
        }

        // ==== 2. MULAI DASH ====
        if (rampageTimer >= RampagePauseDuration)
        {
            rampageTimer = 0;
            rampageTelegraphActive = false;

            rampageIsDashing = true;
            rampageDashTimer = 0;

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, npc.Center);
            if (Main.netMode != NetmodeID.Server)
            {
                for (int i = 0; i < 12; i++)
                {
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.RainbowMk2, 0f, 0f, 100, default, 1.6f);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(4f, 4f);
                }
            }
        }
    }
    else
    {
        rampageDashTimer++;
        float progress = MathHelper.Clamp(rampageDashTimer / (float)RampageDashDuration, 0f, 1f);
        npc.Center = Vector2.Lerp(rampageDashStart, rampageDashTarget, progress);
        npc.velocity = (rampageDashTarget - rampageDashStart) / RampageDashDuration;

        if (Main.netMode != NetmodeID.Server && rampageDashTimer % 2 == 0)
        {
            Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.RainbowMk2, 0f, 0f, 150, default, 1.4f);
            d.noGravity = true;
            d.velocity = Vector2.Zero;
            d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.8f, 0.7f);
        }

        if (progress >= 1f)
        {
            rampageIsDashing = false;
            rampageDashIndex++;

            // ==== LANCE BURST 360 DERAJAT (6 LANCE, GAP 60 DERAJAT) ====
            int burstDamage = 22 + (bossPhase - 1) * 4;
            float lanceSpeed = 8f + (bossPhase - 1) * 1f;
            int lanceCount = 6;
            
            float startAngle = (rampageDashTarget - rampageDashStart).ToRotation();

            for (int i = 0; i < lanceCount; i++)
            {
                float angle = startAngle + MathHelper.ToRadians(60f * i);
                Vector2 dir = angle.ToRotationVector2();

                int proj = Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    npc.Center,
                    dir * lanceSpeed,
                    ProjectileID.FairyQueenLance,
                    burstDamage,
                    1f,
                    Main.myPlayer,
                    angle
                );

                if (proj != Main.maxProjectiles)
                {
                    var mod = Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>();
                    mod.projectileIndexInSpread = i;
                    mod.attackPhase = bossPhase;
                }
            }

            ScreenShakeSystem.StartShake(6f, MathHelper.TwoPi, null);
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item162, npc.Center);
        }
    }
}

        // ==== REALITY TEAR (PERBAIKAN LASER TELEGRAPH) ====
        private void ExecuteRealityTear(NPC npc, Player player)
        {
            realityTearTimer++;
            npc.velocity *= 0.85f;

            int totalBlinks = 3 + bossPhase; 
            int cycleTimer = realityTearTimer % RealityTearBlinkInterval;

            // 1. Blink & Kunci posisi arah mengincar player
            if (cycleTimer == 1 && realityTearBlinkIndex < totalBlinks)
            {
                float blinkDistance = MathHelper.Lerp(340f, 240f, (bossPhase - 1) / 2f);
                Vector2 blinkOffset = Main.rand.NextVector2CircularEdge(blinkDistance, blinkDistance);
                npc.Center = player.Center + blinkOffset;

                realityTearAimDir = Vector2.Normalize(player.Center - npc.Center);
                realityTearTelegraphActive = true;

                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.Center, 1, 1, DustID.RainbowMk2, 0f, 0f, 100, default, 1.8f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(6f, 6f);
                        d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.8f, 0.65f);
                    }
                }
                ScreenShakeSystem.StartShake(2.5f, MathHelper.TwoPi, null);
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item165, npc.Center);
            }

            // 2. Tracking halus arah laser selama jeda telegraph
            if (realityTearTelegraphActive && cycleTimer <= RealityTearTelegraphDuration)
            {
                Vector2 targetDir = Vector2.Normalize(player.Center - npc.Center);
                realityTearAimDir = Vector2.Normalize(Vector2.Lerp(realityTearAimDir, targetDir, 0.15f));
            }

            // 3. Tembakkan streak setelah telegraph laser selesai
            if (cycleTimer == RealityTearTelegraphDuration && realityTearBlinkIndex < totalBlinks)
            {
                realityTearTelegraphActive = false;

                int damage = 22 + (bossPhase - 1) * 4;
                int burstCount = 2 + bossPhase; 
                float spread = 24f;

                for (int i = 0; i < burstCount; i++)
                {
                    float t = burstCount == 1 ? 0f : (i / (float)(burstCount - 1)) - 0.5f;
                    Vector2 dir = realityTearAimDir.RotatedBy(MathHelper.ToRadians(t * spread));
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 11f, ProjectileID.HallowBossRainbowStreak, damage, 0f, Main.myPlayer);
                    if (proj != Main.maxProjectiles)
                    {
                        var mod = Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>();
                        mod.projectileIndexInSpread = i;
                        mod.attackPhase = bossPhase;
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item160, npc.Center);
                ScreenShakeSystem.StartShake(4f, MathHelper.TwoPi, null);
                realityTearBlinkIndex++;
            }

            if (realityTearBlinkIndex >= totalBlinks && cycleTimer == 0)
            {
                realityTearTimer = 0;
                realityTearBlinkIndex = 0;
                realityTearTelegraphActive = false;
                SwitchToNextAttack();
            }
        }

        private void ExecuteGardenOfLightBloom(NPC npc, Player player)
        {
            npc.velocity *= 0.94f;
            Vector2 hoverTarget = player.Center - new Vector2(0, 320f);
            npc.velocity += (hoverTarget - npc.Center) * 0.03f;

            gardenTimer++;

            int totalBlooms = 3 + (bossPhase - 1);

            if (gardenTimer % GardenWaveInterval == 0 && gardenWaveIndex < totalBlooms)
            {
                float angle = MathHelper.TwoPi * (gardenWaveIndex / (float)totalBlooms) + MathHelper.ToRadians(20f * bladeDanceWaveIndex);
                Vector2 bloomPos = player.Center + angle.ToRotationVector2() * 260f;

                int petalCount = 8 + (bossPhase - 1) * 2;
                int damage = 16 + (bossPhase - 1) * 3;

                for (int i = 0; i < petalCount; i++)
                {
                    float petalAngle = MathHelper.TwoPi * (i / (float)petalCount);
                    Vector2 dir = petalAngle.ToRotationVector2();
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), bloomPos, dir * 4.5f, ProjectileID.HallowBossLastingRainbow, damage, 0f, Main.myPlayer);
                    if (proj != Main.maxProjectiles)
                    {
                        var mod = Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>();
                        mod.isBossEverlasting = false;
                        mod.attackPhase = bossPhase;
                    }
                }

                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 16; i++)
                    {
                        Dust d = Dust.NewDustDirect(bloomPos, 1, 1, DustID.RainbowMk2, 0f, 0f, 100, default, 1.6f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(3f, 3f);
                        d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.7f, 0.75f);
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, bloomPos);
                ScreenShakeSystem.StartShake(3f, MathHelper.TwoPi, null);

                gardenWaveIndex++;
            }

            if (gardenWaveIndex >= totalBlooms && gardenTimer % GardenWaveInterval == 0)
            {
                gardenTimer = 0;
                gardenWaveIndex = 0;
                SwitchToNextAttack();
            }
        }

        private void ExecuteHallowedMirrorImages(NPC npc, Player player)
        {
            mirrorTimer++;

            Vector2[] anchors = new Vector2[3];
            for (int i = 0; i < 3; i++)
            {
                float angle = MathHelper.TwoPi * (i / 3f);
                anchors[i] = player.Center + angle.ToRotationVector2() * 340f;
            }

            if (!mirrorSpawned)
            {
                mirrorSpawned = true;
                mirrorAnchorIndex = Main.rand.Next(3);

                for (int i = 0; i < 3; i++)
                {
                    if (i == mirrorAnchorIndex) continue;
                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), anchors[i], Vector2.Zero, ModContent.ProjectileType<EmpressLiteClone>(), 0, 0f, Main.myPlayer);
                    if (p != Main.maxProjectiles)
                    {
                        var clone = Main.projectile[p].ModProjectile as EmpressLiteClone;
                        if (clone != null)
                        {
                            clone.bossNPCIndex = npc.whoAmI;
                            clone.attackPhase = bossPhase;
                            clone.sideOffset = i == 0 ? -1f : 1f;
                        }
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(5f, MathHelper.TwoPi, null);
            }

            npc.Center = anchors[mirrorAnchorIndex];
            npc.velocity = Vector2.Zero;

            if (mirrorTimer % MirrorSwapInterval == 0)
            {
                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 14; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.Center, 1, 1, DustID.RainbowMk2, 0f, 0f, 100, default, 1.7f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(5f, 5f);
                    }
                }

                mirrorAnchorIndex = Main.rand.Next(3);
                npc.Center = anchors[mirrorAnchorIndex];

                Vector2 shootDir = Vector2.Normalize(player.Center - npc.Center);
                int damage = 22 + (bossPhase - 1) * 4;
                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootDir * 9f, ProjectileID.HallowBossRainbowStreak, damage, 0f, Main.myPlayer);
                if (proj != Main.maxProjectiles)
                {
                    var mod = Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>();
                    mod.projectileIndexInSpread = mirrorAnchorIndex;
                    mod.attackPhase = bossPhase;
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item165, npc.Center);
            }

            if (mirrorTimer >= MirrorDuration)
            {
                mirrorTimer = 0;
                mirrorSpawned = false;
                SwitchToNextAttack();
            }
        }

        private void ExecuteMiniFairyStarfall(NPC npc, Player player)
        {
            npc.velocity *= 0.9f;
            Vector2 hoverTarget = player.Center - new Vector2(0, 420f);
            npc.velocity += (hoverTarget - npc.Center) * 0.03f;

            starlightTimer++;

            int totalWaves = 3 + bossPhase;

            if (starlightTimer % StarlightWaveInterval == 0 && starlightWaveIndex < totalWaves)
            {
                int fairyCount = 3 + (bossPhase - 1);
                float baseX = player.Center.X + Main.rand.NextFloat(-300f, 300f);
                float spacing = 120f;

                for (int i = 0; i < fairyCount; i++)
                {
                    float xPos = baseX + (i - fairyCount / 2f) * spacing + Main.rand.NextFloat(-25f, 25f);
                    Vector2 spawnPos = new Vector2(xPos, player.Center.Y - 850f);

                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, new Vector2(0f, 5f), EmpressMiniEmpressModifier.MiniEmpressProjectileID, 0, 0f, Main.myPlayer);
                    if (p != Main.maxProjectiles)
                    {
                        var modifier = Main.projectile[p].GetGlobalProjectile<EmpressMiniEmpressModifier>();
                        modifier.isMiniEmpressSummon = true;
                        modifier.bossNPCIndex = npc.whoAmI;
                        modifier.summonIndex = i;
                        modifier.totalSummons = fairyCount;
                        modifier.attackPhase = bossPhase;
                    }

                    if (Main.netMode != NetmodeID.Server)
                    {
                        for (int d2 = 0; d2 < 5; d2++)
                        {
                            Dust d = Dust.NewDustDirect(spawnPos - new Vector2(0f, d2 * 14f), 1, 1, DustID.RainbowMk2, 0f, 3.5f, 150, default, 1.4f - d2 * 0.15f);
                            d.noGravity = true;
                            d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.8f, 0.75f);
                        }
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, npc.Center);
                ScreenShakeSystem.StartShake(3.5f, MathHelper.TwoPi, null);

                starlightWaveIndex++;
            }

            if (starlightWaveIndex >= totalWaves && starlightTimer % StarlightWaveInterval == 0)
            {
                starlightTimer = 0;
                starlightWaveIndex = 0;
                SwitchToNextAttack();
            }
        }

        private void ExecuteFairyRoyaleFinale(NPC npc, Player player)
        {
            npc.velocity *= 0.9f;
            Vector2 hoverTarget = player.Center - new Vector2(0, 340f);
            npc.velocity += (hoverTarget - npc.Center) * 0.03f;

            finaleTimer++;

            if (!finaleSpawned && finaleTimer >= 30)
            {
                finaleSpawned = true;

                // Balance: Cukup 1 kali spawn mini empresses (tidak duplikat 2x lagi)
                SpawnMiniEmpresses(npc);
                SpawnSunDanceRing(npc, player, radius: 480f, angularSpeed: 0.0045f, isBig: true);
                SpawnSunDanceRing(npc, player, radius: 300f, angularSpeed: -0.008f, isBig: false);

                for (int i = 0; i < 2; i++)
                {
                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<EmpressLiteClone>(), 0, 0f, Main.myPlayer);
                    if (p != Main.maxProjectiles)
                    {
                        var clone = Main.projectile[p].ModProjectile as EmpressLiteClone;
                        if (clone != null)
                        {
                            clone.bossNPCIndex = npc.whoAmI;
                            clone.attackPhase = bossPhase;
                            clone.sideOffset = i == 0 ? -1f : 1f;
                        }
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                ScreenShakeSystem.StartShake(10f, MathHelper.TwoPi, null);

                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 60; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.RainbowMk2, 0f, 0f, 100, default, 2.6f);
                        d.noGravity = true;
                        d.velocity = Main.rand.NextVector2Circular(10f, 10f);
                        d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.85f, 0.7f);
                    }
                }
            }

            // Balance: Interval streak diturunkan ke 45 tick (sebelumnya 30)
            if (finaleTimer > 60 && finaleTimer % 45 == 0)
            {
                Vector2 shootDir = Vector2.Normalize(player.Center - npc.Center);
                int damage = 24 + (bossPhase - 1) * 5;
                for (int i = -2; i <= 2; i++)
                {
                    Vector2 dir = shootDir.RotatedBy(MathHelper.ToRadians(i * 9f));
                    int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, dir * 9.5f, ProjectileID.HallowBossRainbowStreak, damage, 0f, Main.myPlayer);
                    if (proj != Main.maxProjectiles)
                    {
                        var mod = Main.projectile[proj].GetGlobalProjectile<EmpressProjectileColorModifier>();
                        mod.projectileIndexInSpread = i + 2;
                        mod.attackPhase = bossPhase;
                    }
                }
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item160, npc.Center);
            }

            if (finaleTimer >= FinaleDuration)
            {
                finaleTimer = 0;
                finaleSpawned = false;
                SwitchToNextAttack();
            }
        }

        private static readonly AttackState[] Phase1Rotation =
        {
            AttackState.TeleportStrike, AttackState.SunDanceAttack, AttackState.EverlastingBarrage,
            AttackState.MiniEmpressCombo, AttackState.PrismaticBladeDance, AttackState.GardenOfLightBloom
        };

        private static readonly AttackState[] Phase2Rotation =
        {
            AttackState.TeleportStrike, AttackState.RainbowRampage, AttackState.SunDanceAttack,
            AttackState.CloneAssault, AttackState.EverlastingBarrage, AttackState.RealityTear,
            AttackState.MiniEmpressCombo, AttackState.HallowedMirrorImages, AttackState.PrismaticBladeDance,
            AttackState.GardenOfLightBloom
        };

        private static readonly AttackState[] Phase3Rotation =
        {
            AttackState.TeleportStrike, AttackState.RainbowRampage, AttackState.RealityTear,
            AttackState.CloneAssault, AttackState.MiniFairyStarfall, AttackState.SunDanceAttack,
            AttackState.HallowedMirrorImages, AttackState.EverlastingBarrage, AttackState.PrismaticBladeDance,
            AttackState.MiniEmpressCombo, AttackState.GardenOfLightBloom, AttackState.FairyRoyaleFinale
        };

        private int attackRotationIndex = -1;

        private void SwitchToNextAttack()
        {
            tpStrikeTimer = 0;
            AttackState[] rotation = bossPhase switch
            {
                3 => Phase3Rotation,
                2 => Phase2Rotation,
                _ => Phase1Rotation,
            };
            attackRotationIndex = (attackRotationIndex + 1) % rotation.Length;
            AttackState nextState = rotation[attackRotationIndex];

            StateMachine.StateStack.Push(new EntityAIState<AttackState>(nextState));
        }

        private void ResetPhaseVariables(NPC npc)
        {
            npc.velocity = Vector2.Zero;

            tpStrikeTimer = 0;
            strikeChainIndex = 0;
            strikeChainTotal = 1;
            everlastingTimer = 0;
            everlastingCount = 0;
            miniEmpressComboTimer = 0;
            miniEmpressSpawned = false;
            miniEmpressWaveIndex = 0;
            cloneAssaultTimer = 0;
            cloneSpawned = false;
            reinforcementSpawned = false;
            finaleBurstFired = false;
            sunDanceTimer = 0;
            sunDanceSpawned = false;
            sunDanceReinforcementSpawned = false;

            bladeDanceTimer = 0;
            bladeDanceWaveIndex = 0;

            rampageTimer = 0;
            rampageDashIndex = 0;
            rampageIsDashing = false;
            rampageDashTimer = 0;
            rampageTelegraphActive = false;

            realityTearTimer = 0;
            realityTearBlinkIndex = 0;
            realityTearTelegraphActive = false;

            gardenTimer = 0;
            gardenWaveIndex = 0;

            mirrorTimer = 0;
            mirrorAnchorIndex = 0;
            mirrorSpawned = false;

            starlightTimer = 0;
            starlightWaveIndex = 0;

            finaleTimer = 0;
            finaleSpawned = false;

            afterimages.Clear();
            afterimageIntensity = 0.35f;

            attackRotationIndex = -1;

            if (isInvisible)
            {
                isInvisible = false;
                npc.dontTakeDamage = false;
                trackedProjectileIdentity = -1;
                trackedProjectileIndex = -1;
            }
        }

        private void TriggerPhaseVisuals(NPC npc)
        {
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Roar, npc.Center);
            ScreenShakeSystem.StartShake(12f, MathHelper.TwoPi, null);
            if (Main.netMode != NetmodeID.Server)
            {
                for (int i = 0; i < 50; i++)
                {
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.RainbowMk2, 0f, 0f, 100, default, 2.5f);
                    d.noGravity = true;
                    d.velocity = Main.rand.NextVector2Circular(15f, 15f);
                    d.color = Main.hslToRgb(Main.rand.NextFloat(), 0.8f, 0.7f);
                }
            }
        }

        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (npc.type != NPCID.HallowBoss) return;

            var globalNPC = npc.GetGlobalNPC<EmpressAdvancedRework>();
            bool hasAfterimages = globalNPC.afterimages.Count > 0;
            bool hasDashTelegraph = globalNPC.rampageTelegraphActive;
            bool hasRealityTearTelegraph = globalNPC.realityTearTelegraphActive;

            if (!hasAfterimages && !hasDashTelegraph && !hasRealityTearTelegraph) return;

            Texture2D texture = TextureAssets.Npc[npc.type].Value;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);

            for (int i = globalNPC.afterimages.Count - 1; i >= 0; i--)
            {
                AfterimageSnapshot snap = globalNPC.afterimages[i];

                float ageProgress = i / (float)MaxAfterimages;
                float fade = (1f - ageProgress) * globalNPC.afterimageIntensity;
                if (fade <= 0.02f) continue;

                float hue = (Main.GlobalTimeWrappedHourly * 0.25f + i * 0.05f) % 1f;
                Color trailColor = Main.hslToRgb(hue, 0.85f, 0.65f) * fade;
                trailColor.A = 0;

                Vector2 origin = new Vector2(snap.Frame.Width / 2f, snap.Frame.Height / 2f);
                SpriteEffects effects = snap.Direction == -1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                spriteBatch.Draw(texture, snap.Position - screenPos, snap.Frame, trailColor, snap.Rotation, origin, snap.Scale, effects, 0f);
            }

            if (hasDashTelegraph)
            {
                globalNPC.DrawRampageTelegraph(spriteBatch, screenPos);
            }

            if (hasRealityTearTelegraph)
            {
                globalNPC.DrawRealityTearTelegraph(spriteBatch, npc, screenPos);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawRampageTelegraph(SpriteBatch spriteBatch, Vector2 screenPos)
{
    if (!rampageTelegraphActive) return;

    Texture2D lineTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/EmpressRework/BloomLine").Value;

    int telegraphStartTick = Math.Max(1, RampagePauseDuration - RampageTelegraphTime);
    float progress = MathHelper.Clamp((rampageTimer - telegraphStartTick) / (float)Math.Max(1, RampagePauseDuration - telegraphStartTick), 0f, 1f);

    float opacity = MathHelper.Lerp(0.4f, 1f, progress);

    Vector2 lineVector = rampageDashTarget - rampageDashStart;
    float lineLength = lineVector.Length();
    if (lineLength < 1f) return;

    float lineRotation = lineVector.ToRotation() + MathHelper.PiOver2;
    Vector2 lineOrigin = new Vector2(lineTex.Width / 2f, lineTex.Height / 2f);
    Vector2 lineCenter = (rampageDashStart + rampageDashTarget) / 2f - screenPos;

    float hue = (Main.GlobalTimeWrappedHourly * 0.35f) % 1f;
    
    // 1. Outer Rainbow Glow (Aura luar transparan)
    Color outerColor = Main.hslToRgb(hue, 0.8f, 0.7f) * opacity * 0.6f;
    outerColor.A = 0;
    Vector2 outerScale = new Vector2(MathHelper.Lerp(0.7f, 0.35f, progress), lineLength / lineTex.Height);
    spriteBatch.Draw(lineTex, lineCenter, null, outerColor, lineRotation, lineOrigin, outerScale, SpriteEffects.None, 0f);

    // 2. Inner Core Beam (Garis inti putih tajam di tengah)
    Color innerCoreColor = Color.White * opacity * 0.9f;
    innerCoreColor.A = 0;
    Vector2 innerScale = new Vector2(MathHelper.Lerp(0.25f, 0.1f, progress), lineLength / lineTex.Height);
    spriteBatch.Draw(lineTex, lineCenter, null, innerCoreColor, lineRotation, lineOrigin, innerScale, SpriteEffects.None, 0f);
}

        // Penanda Laser Dual-Layer untuk RealityTear
        private void DrawRealityTearTelegraph(SpriteBatch spriteBatch, NPC npc, Vector2 screenPos)
        {
            if (!realityTearTelegraphActive || realityTearAimDir == Vector2.Zero) return;

            Texture2D lineTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/EmpressRework/BloomLine").Value;

            int cycleTimer = realityTearTimer % RealityTearBlinkInterval;
            float progress = MathHelper.Clamp(cycleTimer / (float)RealityTearTelegraphDuration, 0f, 1f);

            float opacity = MathHelper.Lerp(0.4f, 1f, progress);
            float lineLength = 2200f;

            float lineRotation = realityTearAimDir.ToRotation() + MathHelper.PiOver2;
            Vector2 lineOrigin = new Vector2(lineTex.Width / 2f, lineTex.Height / 2f);

            Vector2 lineCenter = npc.Center + realityTearAimDir * (lineLength / 2f) - screenPos;

            float hue = (Main.GlobalTimeWrappedHourly * 0.4f) % 1f;
            Color outerColor = Main.hslToRgb(hue, 0.8f, 0.75f) * opacity * 0.6f;
            outerColor.A = 0;

            Color innerCoreColor = Color.White * opacity * 0.9f;
            innerCoreColor.A = 0;

            // Outer Glow Aura
            Vector2 outerScale = new Vector2(MathHelper.Lerp(0.8f, 0.3f, progress), lineLength / lineTex.Height);
            spriteBatch.Draw(lineTex, lineCenter, null, outerColor, lineRotation, lineOrigin, outerScale, SpriteEffects.None, 0f);

            // Inner Core Beam (Putih Terang)
            Vector2 innerScale = new Vector2(MathHelper.Lerp(0.3f, 0.1f, progress), lineLength / lineTex.Height);
            spriteBatch.Draw(lineTex, lineCenter, null, innerCoreColor, lineRotation, lineOrigin, innerScale, SpriteEffects.None, 0f);
        }
    }
}