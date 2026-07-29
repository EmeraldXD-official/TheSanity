using System;
using System.Collections.Generic; 
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;
using TheSanity.Projectiles;

namespace TheSanity.GlobalNPCs
{
    public class GolemHeadOverride : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private Vector2 targetArenaCenter = Vector2.Zero;
        private bool hasFoundAltar = false;

        // SISTEM ALARM / SIKLUS KENDALI SERANGAN KEPALA
        private int globalTimer = 0;         
        public bool isLaserActive = false;   
        private int laserActiveTimer = 0;     

        // STATE MEKANIK FILLER EYE BEAM BERUNTUN
        private bool isFillerActive = false;
        private int fillerTimer = 0;
        private int fillerShotCount = 0;

        // STATE MEKANIK PHASE 2 FIST ATTACKS
        private bool isFistRingActive = false;
        private int fistRingTimer = 0;

        private bool isClapActive = false;
        private int clapTimer = 0;
        private float clapBaseAngle = 0f;

        // STATE MEKANIK CLAP BERUNTUN
        private bool isClapPhaseActive = false;
        private int clapPhaseToSpawn = 0;
        private int clapPhaseSpawned = 0;
        private int clapSpawnDelayTimer = 0; 

        // STATE MEKANIK BARU GOLEM STYNGER FATAL ATTACK (HP <= 75%)
        private bool isStyngerActive = false;
        private int styngerTimer = 0;
        private int styngerShotCount = 0;
        private int styngerTargetShots = 0;

        // SISTEM MANAJEMEN ANTRIAN SERANGAN ACAK BERURUTAN (FASE NORMAL)
        private int[] attackQueue = new int[6];
        private int currentQueueIndex = 0;
        private bool queueInitialized = false;
        private bool waitingForNextAttack = false;
        private int attackBreakTimer = 0; 

        // STATE MEKANIK RITUAL PENYEDOTAN & SEMBURAN BOLA API CULTIST
        private bool openedLaserDone = false;       
        private bool isAbsorbingFireballs = false;  
        private int absorptionTimer = 0;
        private bool isSpewingFireballs = false;   
        private int spewTimer = 0;
        private int spewWaveTimer = 0;              
        private int fireballsSpewedCount = 0;
        private int clonesSpewedCount = 0;
        private int preSpewDelayTimer = 0;          
        private int postSpewDelayTimer = 0;         

        // COUNTER HITUNGAN PENYEDOTAN PROYEKTIL SECARA REALTIME
        public int absorbedFireballs = 0;
        public int absorbedClones = 0;
        private int targetFireballsToSpew = 0;
        private int targetClonesToSpew = 0;

        // FIX STATE & COUNTER UNTUK MEKANIK WALL SLAM FIST AKURAT (HP <= 75%)
        private int laserCounterUnder75 = 0;       
        private bool isWallSlamActive = false;      

        // SAKELAR STATE GLOBAL
        private bool initialHealComplete = false; 
        private bool roared = false; 
        private bool spawnedHealChains = false;

        // FONDASI CINEMATIC DEATH
        private bool isDying = false;
        private int deathTimer = 0;
        private bool trulyDead = false; 

        // TIMING KUSTOM UNTUK KEPALA SAAT MENYENTUH TANAH
        private bool hasLanded = false;    
        private int landedTimer = 0;       

        // REWORK FIXED: Antrean DSP menggunakan 8 slot (2 Gelombang x 4 attack unik)
        private bool isDSPActive = false;       
        private bool dspInitialized = false;    
        private int[] dspAttackQueue = new int[8]; 
        private int dspQueueIndex = 0;          

        private void GenerateRandomAttackQueue()
        {
            int bodyIndex = NPC.FindFirstNPC(NPCID.Golem);
            bool lowHP = false;

            if (bodyIndex != -1 && Main.npc[bodyIndex].active)
            {
                if ((float)Main.npc[bodyIndex].life / Main.npc[bodyIndex].lifeMax <= 0.75f)
                {
                    lowHP = true;
                }
            }

            for (int i = 0; i < 6; i++)
            {
                attackQueue[i] = lowHP ? Main.rand.Next(0, 4) : Main.rand.Next(0, 3);
            }
            currentQueueIndex = 0;
            queueInitialized = true;
        }

        // FIX TRANSISI: Sekarang mereset status Wall Slam dengan bersih saat masuk DSP
        public void ResetAttackStatesForDSP()
        {
            isLaserActive = false;
            laserActiveTimer = 0;
            isFillerActive = false;
            fillerTimer = 0;
            fillerShotCount = 0;
            isFistRingActive = false;
            fistRingTimer = 0;
            isClapActive = false;
            clapTimer = 0;
            isClapPhaseActive = false;
            clapPhaseSpawned = 0;
            clapSpawnDelayTimer = 0;
            isStyngerActive = false;
            styngerTimer = 0;
            styngerShotCount = 0;
            openedLaserDone = false;
            isAbsorbingFireballs = false;
            isSpewingFireballs = false;
            preSpewDelayTimer = 0;
            postSpewDelayTimer = 0;
            absorbedFireballs = 0;
            absorbedClones = 0;
            waitingForNextAttack = false;
            attackBreakTimer = 0;

            isWallSlamActive = false;
            laserCounterUnder75 = 0;

            List<int> attacks = new List<int> { 0, 1, 2, 3, 0, 1, 2, 3 };
            
            for (int i = 0; i < attacks.Count; i++)
            {
                int temp = attacks[i];
                int randomIndex = Main.rand.Next(i, attacks.Count);
                attacks[i] = attacks[randomIndex];
                attacks[randomIndex] = temp;
            }

            for (int i = 0; i < 8; i++)
            {
                dspAttackQueue[i] = attacks[i];
            }
            dspQueueIndex = 0;
            dspInitialized = true;
        }

        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.Golem)
            {
                if (NPC.FindFirstNPC(NPCID.GolemHeadFree) == -1 && !isDying && !trulyDead)
                {
                    initialHealComplete = false;
                    roared = false;
                    spawnedHealChains = false; 
                    isDying = false;
                    deathTimer = 0;
                    isFistRingActive = false;
                    isClapActive = false;
                    isClapPhaseActive = false;
                    isStyngerActive = false;
                    clapPhaseSpawned = 0;
                    clapSpawnDelayTimer = 0;
                    openedLaserDone = false;
                    isAbsorbingFireballs = false;
                    isSpewingFireballs = false;
                    preSpewDelayTimer = 0;
                    postSpewDelayTimer = 0;
                    absorbedFireballs = 0;
                    absorbedClones = 0;
                    queueInitialized = false;
                    waitingForNextAttack = false;
                    laserCounterUnder75 = 0;
                    isWallSlamActive = false;
                    isDSPActive = false;
                    dspInitialized = false;
                }
            }

            if (npc.type == NPCID.GolemHead)
            {
                if (NPC.AnyNPCs(NPCID.GolemFistLeft) || NPC.AnyNPCs(NPCID.GolemFistRight))
                {
                    npc.dontTakeDamage = true;
                }
                else
                {
                    npc.dontTakeDamage = false; 
                }
            }

            if (npc.type == NPCID.Golem)
            {
                if (isDying)
                {
                    deathTimer++;
                    npc.velocity = Vector2.Zero; 
                    npc.dontTakeDamage = true;
                    npc.timeLeft = 3600; 

                    if (deathTimer >= 180 && !NPC.AnyNPCs(NPCID.GolemHeadFree))
                    {
                        trulyDead = true; 
                        npc.dontTakeDamage = false;
                        npc.StrikeInstantKill(); 
                    }
                    return false; 
                }

                int headFreeIndex = NPC.FindFirstNPC(NPCID.GolemHeadFree);
                if (headFreeIndex != -1)
                {
                    NPC headFree = Main.npc[headFreeIndex];

                    npc.alpha = 255; 
                    npc.Center = headFree.Center; 
                    npc.velocity = Vector2.Zero;
                    npc.noGravity = true;
                    npc.noTileCollide = true;

                    for (int i = 0; i < npc.buffImmune.Length; i++)
                    {
                        npc.buffImmune[i] = true;
                    }
                    for (int i = 0; i < NPC.maxBuffs; i++)
                    {
                        if (npc.buffTime[i] > 0)
                        {
                            npc.buffType[i] = 0;
                            npc.buffTime[i] = 0;
                        }
                    }

                    if (npc.lifeMax != 76499)
                    {
                        npc.lifeMax = 76499;
                    }

                    if (!initialHealComplete)
                    {
                        npc.dontTakeDamage = true; 

                        int healAmount = npc.lifeMax / 240; 
                        npc.life += healAmount;

                        if (npc.life >= npc.lifeMax)
                        {
                            npc.life = npc.lifeMax;
                            initialHealComplete = true; 
                        }

                        if (Main.netMode != NetmodeID.Server && Main.GameUpdateCount % 5 == 0)
                        {
                            npc.netUpdate = true;
                        }
                    }
                    else
                    {
                        if (isDSPActive)
                        {
                            npc.life = 1;
                            npc.dontTakeDamage = true;
                        }
                        else
                        {
                            npc.dontTakeDamage = false; 
                        }
                    }

                    if (!CheckAnyPlayerAlive()) { npc.active = false; }
                    else { npc.timeLeft = 3600; } 

                    return false; 
                }
            }

            if (npc.type == NPCID.GolemHeadFree)
            {
                if (!CheckAnyPlayerAlive())
                {
                    npc.active = false;
                    return false;
                }

                npc.TargetClosest(false);

                int bodyIndex = NPC.FindFirstNPC(NPCID.Golem);
                if (bodyIndex == -1 || !Main.npc[bodyIndex].active)
                {
                    npc.life = 0;
                    npc.HitEffect(); 
                    npc.active = false; 
                    return false;
                }

                npc.timeLeft = 3600; 

                var bodyGlobal = Main.npc[bodyIndex].GetGlobalNPC<GolemHeadOverride>();
                
                // ===== FIX VISUAL RANTAI DSP =====
                // Memastikan proyektil rantai tidak kehabisan timeLeft dan hilang mendadak saat fase DSP
                if (bodyGlobal.isDSPActive)
                {
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<GolemHealChain>())
                        {
                            Main.projectile[i].timeLeft = 300;
                        }
                    }
                }
                // =================================

                if (bodyGlobal.isDying)
                {
                    if (hasLanded)
                    {
                        npc.velocity = Vector2.Zero; 
                        landedTimer++;

                        if (landedTimer >= 120)
                        {
                            SoundEngine.PlaySound(new SoundStyle("Terraria/Sounds/Roar_2"), npc.Center);
                            npc.life = 0;
                            npc.HitEffect(); 
                            npc.active = false; 
                        }
                        return false; 
                    }

                    if ((npc.velocity.Y == 0f && npc.oldVelocity.Y > 0f) || npc.collideY)
                    {
                        hasLanded = true;
                        npc.velocity = Vector2.Zero;
                        npc.rotation = Main.rand.NextBool() ? MathHelper.ToRadians(45f) : MathHelper.ToRadians(-45f);
                        return false;
                    }

                    npc.dontTakeDamage = true;
                    npc.noGravity = false;     
                    npc.noTileCollide = false; 

                    npc.rotation += 0.08f;
                    npc.velocity.X = 0f;
                    npc.velocity.Y += 0.4f;
                    if (npc.velocity.Y > 14f) npc.velocity.Y = 14f;

                    if (Main.rand.NextBool(2))
                    {
                        int d = Dust.NewDust(npc.position, npc.width, npc.height, DustID.Lava, 0f, 0f, 100, default, 2f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity *= 2f;
                    }
                    return false; 
                }

                if (!hasFoundAltar)
                {
                    FindLihzahrdAltar(npc);
                    hasFoundAltar = true;
                }

                if (targetArenaCenter != Vector2.Zero)
                {
                    npc.Center = targetArenaCenter;
                    npc.velocity = Vector2.Zero; 
                }

                npc.noGravity = true;
                npc.noTileCollide = true;

                if (!bodyGlobal.initialHealComplete) 
                {
                    globalTimer = 0; 
                    isLaserActive = false;
                    laserActiveTimer = 0;

                    if (!spawnedHealChains && Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        spawnedHealChains = true;
                        for (int i = 0; i < 4; i++)
                        {
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                npc.Center,
                                Vector2.Zero,
                                ModContent.ProjectileType<GolemHealChain>(),
                                0, 
                                0f,
                                Main.myPlayer,
                                i,          
                                npc.whoAmI  
                            );
                        }
                    }
                    return false; 
                }
                else if (!bodyGlobal.roared) 
                {
                    bodyGlobal.roared = true; 
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center); 

                    openedLaserDone = false;
                    waitingForNextAttack = false;
                    globalTimer = 5000; 
                }

                bool hasLaserProjectiles = false;
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<DeathBeamProjectile>())
                    {
                        hasLaserProjectiles = true;
                        break;
                    }
                }

                bool isCurrentlyIdle = !isFistRingActive && !isFillerActive && !isClapActive && !isClapPhaseActive && !isStyngerActive && !isAbsorbingFireballs && !isSpewingFireballs && preSpewDelayTimer == 0 && postSpewDelayTimer == 0 && !isLaserActive && !hasLaserProjectiles;

                if (isCurrentlyIdle)
                {
                    if (!openedLaserDone)
                    {
                        openedLaserDone = true;
                        isLaserActive = true;
                        laserActiveTimer = 0;

                        SoundEngine.PlaySound(SoundID.Zombie103, npc.Center); 

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int laserDamage = 75; 
                            
                            bool isPatternX = Main.rand.NextBool(); 
                            float baseAngle = isPatternX ? MathHelper.ToRadians(45) : 0f;

                            float rotDir = Main.rand.NextBool() ? 1f : -1f;

                            for (int i = 0; i < 4; i++)
                            {
                                float spawnAngle = baseAngle + (i * MathHelper.PiOver2);
                                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(0, -12), Vector2.Zero, ModContent.ProjectileType<DeathBeamProjectile>(), laserDamage, 0f, Main.myPlayer, npc.whoAmI, 0f, rotDir);
                                if (proj != Main.maxProjectiles) { Main.projectile[proj].rotation = spawnAngle; }
                            }
                        }
                    }
                    else
                    {
                        if (bodyGlobal.isDSPActive)
                        {
                            if (!dspInitialized)
                            {
                                ResetAttackStatesForDSP();
                            }

                            if (waitingForNextAttack)
                            {
                                attackBreakTimer++;
                                if (attackBreakTimer >= 45) 
                                {
                                    attackBreakTimer = 0;
                                    waitingForNextAttack = false;

                                    if (dspQueueIndex < 8) 
                                    {
                                        int nextAttackType = dspAttackQueue[dspQueueIndex];
                                        dspQueueIndex++;

                                        if (nextAttackType == 0) 
                                        {
                                            isFistRingActive = true;
                                            fistRingTimer = 0;

                                            if (Main.netMode != NetmodeID.MultiplayerClient)
                                            {
                                                for (int i = 0; i < 20; i++)
                                                {
                                                    float angle = i * (MathHelper.TwoPi / 20f);
                                                    Vector2 spawnPos = npc.Center + angle.ToRotationVector2() * 140f;

                                                    int ringFistDamage = 24;
                                                    
                                                    Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, Vector2.Zero, ModContent.ProjectileType<GolemCustomFist>(), ringFistDamage, 0f, Main.myPlayer, 2, i);
                                                }
                                            }
                                            SoundEngine.PlaySound(SoundID.Zombie103, npc.Center);
                                        }
                                        else if (nextAttackType == 1) 
                                        {
                                            isFillerActive = true;
                                            fillerTimer = 0;
                                            fillerShotCount = 0;
                                        }
                                        else if (nextAttackType == 2) 
                                        {
                                            if (Main.netMode != NetmodeID.MultiplayerClient)
                                            {
                                                isClapPhaseActive = true;
                                                clapPhaseToSpawn = Main.rand.Next(5, 8); 
                                                clapPhaseSpawned = 0;
                                                clapSpawnDelayTimer = 0; 
                                                npc.netUpdate = true;
                                            }
                                        }
                                        else if (nextAttackType == 3)
                                        {
                                            isStyngerActive = true;
                                            styngerTimer = 0;
                                            styngerShotCount = 0;
                                            styngerTargetShots = Main.rand.Next(3, 6); 
                                            npc.netUpdate = true;
                                        }
                                    }
                                    else if (dspQueueIndex == 8) 
                                    {
                                        dspQueueIndex++; 
                                        
                                        isAbsorbingFireballs = true;
                                        absorptionTimer = 0;
                                        absorbedFireballs = 0;
                                        absorbedClones = 0;

                                        SoundEngine.PlaySound(SoundID.NPCDeath60, npc.Center); 

                                        if (Main.netMode != NetmodeID.MultiplayerClient)
                                        {
                                            int suctionFireballDamage = 13;
                                            
                                            int mainFireballToSpawn = 180; 
                                            for (int i = 0; i < mainFireballToSpawn; i++)
                                            {
                                                float angle = Main.rand.NextFloat() * MathHelper.TwoPi;
                                                float dist = Main.rand.NextFloat(1800f, 2600f);
                                                Vector2 spawnPos = npc.Center + angle.ToRotationVector2() * dist;

                                                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, Vector2.Zero, ProjectileID.CultistBossFireBall, suctionFireballDamage, 0f, Main.myPlayer);
                                                if (proj != Main.maxProjectiles)
                                                {
                                                    var modProj = Main.projectile[proj].GetGlobalProjectile<GolemFireballHandle>();
                                                    modProj.isGolemSuction = true;
                                                    modProj.targetNPCIndex = npc.whoAmI;
                                                    Main.projectile[proj].netUpdate = true;
                                                }
                                            }

                                            int cloneFireballToSpawn = 100; 
                                            for (int i = 0; i < cloneFireballToSpawn; i++)
                                            {
                                                float angle = Main.rand.NextFloat() * MathHelper.TwoPi;
                                                float dist = Main.rand.NextFloat(1800f, 2600f);
                                                Vector2 spawnPos = npc.Center + angle.ToRotationVector2() * dist;

                                                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, Vector2.Zero, ProjectileID.CultistBossFireBallClone, suctionFireballDamage, 0f, Main.myPlayer);
                                                if (proj != Main.maxProjectiles)
                                                {
                                                    var modProj = Main.projectile[proj].GetGlobalProjectile<GolemFireballHandle>();
                                                    modProj.isGolemSuction = true;
                                                    modProj.targetNPCIndex = npc.whoAmI;
                                                    Main.projectile[proj].netUpdate = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            else
                            {
                                waitingForNextAttack = true;
                                attackBreakTimer = 0;
                            }
                        }
                        else
                        {
                            if (!queueInitialized)
                            {
                                GenerateRandomAttackQueue();
                                waitingForNextAttack = true;
                                attackBreakTimer = 0;
                            }

                            if (waitingForNextAttack)
                            {
                                attackBreakTimer++;
                                if (attackBreakTimer >= 45) 
                                {
                                    attackBreakTimer = 0;
                                    waitingForNextAttack = false;

                                    if (currentQueueIndex < 6)
                                    {
                                        int nextAttackType = attackQueue[currentQueueIndex];
                                        currentQueueIndex++;

                                        if (nextAttackType == 0) 
                                        {
                                            isFistRingActive = true;
                                            fistRingTimer = 0;

                                            if (Main.netMode != NetmodeID.MultiplayerClient)
                                            {
                                                for (int i = 0; i < 20; i++)
                                                {
                                                    float angle = i * (MathHelper.TwoPi / 20f);
                                                    Vector2 spawnPos = npc.Center + angle.ToRotationVector2() * 140f;

                                                    int ringFistDamage = 24;
                                                    
                                                    Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, Vector2.Zero, ModContent.ProjectileType<GolemCustomFist>(), ringFistDamage, 0f, Main.myPlayer, 2, i);
                                                }
                                            }
                                            SoundEngine.PlaySound(SoundID.Zombie103, npc.Center);
                                        }
                                        else if (nextAttackType == 1) 
                                        {
                                            isFillerActive = true;
                                            fillerTimer = 0;
                                            fillerShotCount = 0;
                                        }
                                        else if (nextAttackType == 2) 
                                        {
                                            if (Main.netMode != NetmodeID.MultiplayerClient)
                                            {
                                                isClapPhaseActive = true;
                                                clapPhaseToSpawn = Main.rand.Next(5, 8); 
                                                clapPhaseSpawned = 0;
                                                clapSpawnDelayTimer = 0; 
                                                npc.netUpdate = true;
                                            }
                                        }
                                        else if (nextAttackType == 3)
                                        {
                                            isStyngerActive = true;
                                            styngerTimer = 0;
                                            styngerShotCount = 0;
                                            styngerTargetShots = Main.rand.Next(3, 6); 
                                            npc.netUpdate = true;
                                        }
                                    }
                                    else
                                    {
                                        queueInitialized = false; 

                                        isAbsorbingFireballs = true;
                                        absorptionTimer = 0;
                                        absorbedFireballs = 0;
                                        absorbedClones = 0;

                                        SoundEngine.PlaySound(SoundID.NPCDeath60, npc.Center); 

                                        if (Main.netMode != NetmodeID.MultiplayerClient)
                                        {
                                            int suctionFireballDamage = 16;
                                            
                                            int mainFireballToSpawn = 180; 
                                            for (int i = 0; i < mainFireballToSpawn; i++)
                                            {
                                                float angle = Main.rand.NextFloat() * MathHelper.TwoPi;
                                                float dist = Main.rand.NextFloat(1800f, 2600f);
                                                Vector2 spawnPos = npc.Center + angle.ToRotationVector2() * dist;

                                                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, Vector2.Zero, ProjectileID.CultistBossFireBall, suctionFireballDamage, 0f, Main.myPlayer);
                                                if (proj != Main.maxProjectiles)
                                                {
                                                    var modProj = Main.projectile[proj].GetGlobalProjectile<GolemFireballHandle>();
                                                    modProj.isGolemSuction = true;
                                                    modProj.targetNPCIndex = npc.whoAmI;
                                                    Main.projectile[proj].netUpdate = true;
                                                }
                                            }

                                            int cloneFireballToSpawn = 100; 
                                            for (int i = 0; i < cloneFireballToSpawn; i++)
                                            {
                                                float angle = Main.rand.NextFloat() * MathHelper.TwoPi;
                                                float dist = Main.rand.NextFloat(1800f, 2600f);
                                                Vector2 spawnPos = npc.Center + angle.ToRotationVector2() * dist;

                                                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, Vector2.Zero, ProjectileID.CultistBossFireBallClone, suctionFireballDamage, 0f, Main.myPlayer);
                                                if (proj != Main.maxProjectiles)
                                                {
                                                    var modProj = Main.projectile[proj].GetGlobalProjectile<GolemFireballHandle>();
                                                    modProj.isGolemSuction = true;
                                                    modProj.targetNPCIndex = npc.whoAmI;
                                                    Main.projectile[proj].netUpdate = true;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                else
                {
                    if (!isAbsorbingFireballs && !isSpewingFireballs && preSpewDelayTimer == 0 && postSpewDelayTimer == 0 && !isLaserActive)
                    {
                        waitingForNextAttack = true;
                    }
                }

                bool hasClapProjectiles = false;
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<GolemCustomFist>() && Main.projectile[i].ai[0] == 3)
                    {
                        hasClapProjectiles = true;
                        break;
                    }
                }
                if (hasClapProjectiles && !isClapActive) { isClapActive = true; clapTimer = 0; }
                if (!hasClapProjectiles && isClapActive) { isClapActive = false; }

                if (Main.netMode != NetmodeID.MultiplayerClient && isClapPhaseActive)
                {
                    if (!isClapActive && clapPhaseSpawned < clapPhaseToSpawn)
                    {
                        clapSpawnDelayTimer++;
                        
                        if (clapSpawnDelayTimer >= 45)
                        {
                            clapSpawnDelayTimer = 0;
                            Player player = Main.player[npc.target];

                            if (player.active && !player.dead)
                            {
                                clapBaseAngle = Main.rand.NextFloat() * MathHelper.TwoPi;
                                Vector2 offset = clapBaseAngle.ToRotationVector2() * 320f;

                                int clapDamage = 28;

                                Projectile.NewProjectile(npc.GetSource_FromAI(), player.Center + offset, Vector2.Zero, ModContent.ProjectileType<GolemCustomFist>(), clapDamage, 0f, Main.myPlayer, 3, 0); 
                                Projectile.NewProjectile(npc.GetSource_FromAI(), player.Center - offset, Vector2.Zero, ModContent.ProjectileType<GolemCustomFist>(), clapDamage, 0f, Main.myPlayer, 3, 1); 
                                
                                clapPhaseSpawned++;
                                npc.netUpdate = true;
                            }
                            else
                            {
                                isClapPhaseActive = false;
                            }
                        }
                    }
                    else if (!isClapActive && clapPhaseSpawned >= clapPhaseToSpawn)
                    {
                        isClapPhaseActive = false; 
                    }
                }

                if (isStyngerActive)
                {
                    styngerTimer++;
                    if (styngerTimer >= 60)
                    {
                        styngerTimer = 0;
                        Player player = Main.player[npc.target];

                        if (player.active && !player.dead)
                        {
                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                float styngerSpeed = 14f; 
                                
                                Vector2 velocity = (player.Center - npc.Center).SafeNormalize(Vector2.Zero) * styngerSpeed;
                                
                                int styngerDamage = 50; 

                                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velocity, ModContent.ProjectileType<GolemStynger>(), styngerDamage, 3f, Main.myPlayer);
                            }

                            SoundEngine.PlaySound(SoundID.Item61, npc.Center); 
                            styngerShotCount++;

                            if (styngerShotCount >= styngerTargetShots)
                            {
                                isStyngerActive = false;
                            }
                        }
                        else
                        {
                            isStyngerActive = false;
                        }
                    }
                }

                if (isClapActive)
                {
                    clapTimer++;
                    Player player = Main.player[npc.target];
                    Projectile fistA = null; Projectile fistB = null;

                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        Projectile p = Main.projectile[i];
                        if (p.active && p.type == ModContent.ProjectileType<GolemCustomFist>() && p.ai[0] == 3)
                        {
                            if (p.ai[1] == 0) fistA = p;
                            if (p.ai[1] == 1) fistB = p;
                        }
                    }

                    if (fistA != null && fistB != null)
                    {
                        if (clapTimer == 1 || clapBaseAngle == 0f)
                        {
                            clapBaseAngle = (fistA.Center - player.Center).ToRotation();
                        }

                        if (clapTimer <= 30)
                        {
                            Vector2 offset = clapBaseAngle.ToRotationVector2() * 300f;
                            fistA.Center = player.Center + offset;
                            fistB.Center = player.Center - offset;

                            fistA.velocity = Vector2.Zero;
                            fistB.velocity = Vector2.Zero;

                            fistA.rotation = (fistB.Center - fistA.Center).ToRotation() + MathHelper.PiOver2;
                            fistB.rotation = (fistA.Center - fistB.Center).ToRotation() + MathHelper.PiOver2; 
                        }
                        else 
                        {
                            if (fistA.velocity == Vector2.Zero && fistB.velocity == Vector2.Zero)
                            {
                                float smashSpeed = 26f; 
                                
                                fistA.velocity = (fistB.Center - fistA.Center).SafeNormalize(Vector2.Zero) * smashSpeed;
                                fistB.velocity = (fistA.Center - fistB.Center).SafeNormalize(Vector2.Zero) * smashSpeed;
                                
                                fistA.netUpdate = true; fistB.netUpdate = true;
                                SoundEngine.PlaySound(SoundID.Item43, player.Center); 
                            }

                            if (Vector2.Distance(fistA.Center, fistB.Center) < 26f)
                            {
                                Vector2 impactPoint = (fistA.Center + fistB.Center) / 2f;

                                if (Main.netMode != NetmodeID.MultiplayerClient)
                                {
                                    int shardCount = 8;
                                    for (int j = 0; j < shardCount; j++)
                                    {
                                        float shootAngle = j * (MathHelper.TwoPi / shardCount) + Main.rand.NextFloat(-0.2f, 0.2f);
                                        
                                        Vector2 shardVel = shootAngle.ToRotationVector2() * Main.rand.NextFloat(4f, 7f);
                                        
                                        int shardDamage = 10;

                                        Projectile.NewProjectile(npc.GetSource_FromAI(), impactPoint, shardVel, ModContent.ProjectileType<StoneShard>(), shardDamage, 1f, Main.myPlayer);
                                    }
                                }

                                SoundEngine.PlaySound(SoundID.Item62, impactPoint); 
                                for (int d = 0; d < 25; d++)
                                {
                                    int dustIdx = Dust.NewDust(impactPoint - new Vector2(15,15), 30, 30, DustID.Stone, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 0, default, 1.6f);
                                    Main.dust[d].noGravity = false;
                                }

                                fistA.Kill(); fistB.Kill();
                                isClapActive = false;
                            }
                        }
                    }
                    else
                    {
                        if (clapTimer > 120) isClapActive = false; 
                    }
                }

                if (isFistRingActive)
                {
                    fistRingTimer++;
                    Player player = Main.player[npc.target];

                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        Projectile p = Main.projectile[i];
                        if (p.active && p.type == ModContent.ProjectileType<GolemCustomFist>() && p.ai[0] == 2)
                        {
                            if (p.localAI[0] == 0)
                            {
                                float currentAngle = (fistRingTimer * 0.025f) + (p.ai[1] * (MathHelper.TwoPi / 20f)); 
                                p.Center = npc.Center + currentAngle.ToRotationVector2() * 140f;
                                p.velocity = Vector2.Zero;
                                p.rotation = (player.Center - p.Center).ToRotation() + MathHelper.PiOver2; 
                            }
                            else if (p.localAI[0] == 1)
                            {
                                p.rotation = p.velocity.ToRotation() + MathHelper.PiOver2; 
                            }
                        }
                    }

                    if (fistRingTimer % 7 == 0 && Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        List<int> validFistIndices = new List<int>();
                        int fistType = ModContent.ProjectileType<GolemCustomFist>();

                        for (int i = 0; i < Main.maxProjectiles; i++)
                        {
                            Projectile p = Main.projectile[i];
                            if (p.active && p.type == fistType && p.ai[0] == 2 && p.localAI[0] == 0) { validFistIndices.Add(i); }
                        }

                        if (validFistIndices.Count > 0)
                        {
                            int targetIdx = validFistIndices[Main.rand.Next(validFistIndices.Count)];
                            Projectile chosenFist = Main.projectile[targetIdx];

                            chosenFist.localAI[0] = 1; 
                            
                            float ringShootSpeed = 15.5f;
                            
                            chosenFist.velocity = (player.Center - chosenFist.Center).SafeNormalize(Vector2.Zero) * ringShootSpeed; 
                            chosenFist.netUpdate = true;

                            SoundEngine.PlaySound(SoundID.Item15, npc.Center);
                        }
                    }

                    if (fistRingTimer > 240)
                    {
                        isFistRingActive = false;
                    }
                }

                if (isFillerActive)
                {
                    fillerTimer++;
                    if (fillerTimer % 4 == 0)
                    {
                        Player player = Main.player[npc.target];

                        if (player.active && !player.dead)
                        {
                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                float shootSpeed = 6.5f;
                                
                                int damage = 30;

                                Vector2 leftEyePos = npc.Center + new Vector2(-16, -12);
                                Vector2 rightEyePos = npc.Center + new Vector2(16, -12);

                                Vector2 velocityLeft = (player.Center - leftEyePos).SafeNormalize(Vector2.Zero) * shootSpeed;
                                Vector2 velocityRight = (player.Center - rightEyePos).SafeNormalize(Vector2.Zero) * shootSpeed;

                                Projectile.NewProjectile(npc.GetSource_FromAI(), leftEyePos, velocityLeft, ProjectileID.EyeBeam, damage, 0f, Main.myPlayer);
                                Projectile.NewProjectile(npc.GetSource_FromAI(), rightEyePos, velocityRight, ProjectileID.EyeBeam, damage, 0f, Main.myPlayer);
                            }

                            SoundEngine.PlaySound(SoundID.Item12, npc.Center);
                            fillerShotCount++;

                            if (fillerShotCount >= 25) 
                            {
                                isFillerActive = false;
                            }
                        }
                        else
                        {
                            isFillerActive = false; 
                        }
                    }
                }

                if (isAbsorbingFireballs)
                {
                    absorptionTimer++;

                    if (Main.rand.NextBool(2))
                    {
                        float randAngle = Main.rand.NextFloat() * MathHelper.TwoPi;
                        Vector2 dPos = npc.Center + randAngle.ToRotationVector2() * Main.rand.NextFloat(60f, 180f);
                        Vector2 dVel = (npc.Center - dPos).SafeNormalize(Vector2.Zero) * 5f;
                        int d = Dust.NewDust(dPos, 0, 0, DustID.Lava, dVel.X, dVel.Y, 100, default, 1.3f);
                        Main.dust[d].noGravity = true;
                    }

                    bool anySuctionLeft = false;
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        if (Main.projectile[i].active && Main.projectile[i].GetGlobalProjectile<GolemFireballHandle>().isGolemSuction)
                        {
                            anySuctionLeft = true;
                            break;
                        }
                    }

                    if ((absorptionTimer >= 300 || !anySuctionLeft) && absorptionTimer > 40) 
                    {
                        isAbsorbingFireballs = false;
                        preSpewDelayTimer = 1; 
                        spewTimer = 0;
                        spewWaveTimer = 0;
                        fireballsSpewedCount = 0;
                        clonesSpewedCount = 0;
                        
                        targetFireballsToSpew = absorbedFireballs * 2;
                        targetClonesToSpew = absorbedClones * 2;

                        if (targetFireballsToSpew == 0 && targetClonesToSpew == 0)
                        {
                            targetFireballsToSpew = 360; 
                            targetClonesToSpew = 200;    
                        }
                    }
                }

                if (preSpewDelayTimer > 0)
                {
                    preSpewDelayTimer++;
                    if (Main.GameUpdateCount % 3 == 0)
                    {
                        int d = Dust.NewDust(npc.Center - new Vector2(10, 10), 20, 20, DustID.Lava, 0f, 0f, 100, default, 1.8f);
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity *= 1.5f;
                    }

                    if (preSpewDelayTimer >= 60) 
                    {
                        preSpewDelayTimer = 0;
                        isSpewingFireballs = true; 
                        SoundEngine.PlaySound(SoundID.Roar, npc.Center); 
                    }
                }

                if (isSpewingFireballs)
                {
                    spewTimer++;
                    spewWaveTimer++;

                    if (spewWaveTimer >= 8)
                    {
                        spewWaveTimer = 0;

                        if (fireballsSpewedCount < targetFireballsToSpew || clonesSpewedCount < targetClonesToSpew)
                        {
                            SoundEngine.PlaySound(SoundID.Item34, npc.Center); 
                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                int spewDamage = 25;
                                
                                for (int i = 0; i < 3; i++)
                                {
                                    if (fireballsSpewedCount < targetFireballsToSpew)
                                    {
                                        float angle = Main.rand.NextFloat() * MathHelper.TwoPi;
                                        
                                        Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4.5f, 9f);

                                        int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velocity, ProjectileID.CultistBossFireBall, spewDamage, 0f, Main.myPlayer);
                                        if (proj != Main.maxProjectiles)
                                        {
                                            var modProj = Main.projectile[proj].GetGlobalProjectile<GolemFireballHandle>();
                                            modProj.isGolemSpew = true;
                                            Main.projectile[proj].netUpdate = true;
                                        }
                                        fireballsSpewedCount++;
                                    }
                                }

                                for (int i = 0; i < 2; i++)
                                {
                                    if (clonesSpewedCount < targetClonesToSpew)
                                    {
                                        float angle = Main.rand.NextFloat() * MathHelper.TwoPi;
                                        
                                        Vector2 velocity = angle.ToRotationVector2() * Main.rand.NextFloat(4.5f, 9f);

                                        int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velocity, ProjectileID.CultistBossFireBallClone, spewDamage, 0f, Main.myPlayer);
                                        if (proj != Main.maxProjectiles)
                                        {
                                            var modProj = Main.projectile[proj].GetGlobalProjectile<GolemFireballHandle>();
                                            modProj.isGolemSpew = true;
                                            Main.projectile[proj].netUpdate = true;
                                        }
                                        clonesSpewedCount++;
                                    }
                                }
                            }
                        }
                    }

                    if (fireballsSpewedCount >= targetFireballsToSpew && clonesSpewedCount >= targetClonesToSpew)
                    {
                        isSpewingFireballs = false;
                        spewTimer = 0;
                        spewWaveTimer = 0;

                        postSpewDelayTimer = 1; 
                    }
                }

                if (postSpewDelayTimer > 0)
                {
                    postSpewDelayTimer++;
                    if (postSpewDelayTimer >= 45) 
                    {
                        postSpewDelayTimer = 0;
                        isLaserActive = true;
                        laserActiveTimer = 0;

                        SoundEngine.PlaySound(SoundID.Zombie103, npc.Center); 

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            int ultimateLaserDamage = 65;
                            
                            bool isPatternX = Main.rand.NextBool();
                            float baseAngle = isPatternX ? MathHelper.ToRadians(45) : 0f;

                            float rotDir = Main.rand.NextBool() ? 1f : -1f;

                            for (int i = 0; i < 4; i++)
                            {
                                float spawnAngle = baseAngle + (i * MathHelper.PiOver2);
                                int proj = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(0, -12), Vector2.Zero, ModContent.ProjectileType<DeathBeamProjectile>(), ultimateLaserDamage, 0f, Main.myPlayer, npc.whoAmI, 0f, rotDir);
                                if (proj != Main.maxProjectiles) { Main.projectile[proj].rotation = spawnAngle; }
                            }
                        }
                    }
                }

                if (isLaserActive)
                {
                    laserActiveTimer++;

                    if (laserActiveTimer == 60)
                    {
                        SoundEngine.PlaySound(SoundID.Zombie104, npc.Center);
                        SpawnEyeBeamRing(npc, 24); 
                    }

                    if (laserActiveTimer == 180)
                    {
                        SoundEngine.PlaySound(SoundID.Zombie104, npc.Center);
                        SpawnEyeBeamRing(npc, 24);
                    }

                    if (laserActiveTimer >= 360) 
                    {
                        isLaserActive = false;
                        laserActiveTimer = 0;

                        if (bodyGlobal.isDSPActive)
                        {
                            if (bodyIndex != -1 && Main.npc[bodyIndex].active)
                            {
                                laserCounterUnder75++;
                                if (!isWallSlamActive && laserCounterUnder75 >= 1)
                                {
                                    isWallSlamActive = true;
                                    laserCounterUnder75 = 0;

                                    if (Main.netMode != NetmodeID.MultiplayerClient)
                                    {
                                        int wallSlamDamage = 34; 
                                        
                                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, new Vector2(-8f, 0f), ModContent.ProjectileType<GolemWallSlamFist>(), wallSlamDamage, 0f, Main.myPlayer, -1f, 0f);
                                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, new Vector2(8f, 0f), ModContent.ProjectileType<GolemWallSlamFist>(), wallSlamDamage, 0f, Main.myPlayer, 1f, 0f);
                                    }
                                    SoundEngine.PlaySound(SoundID.Item62, npc.Center); 
                                }
                            }

                            if (dspInitialized && dspQueueIndex > 8) 
                            {
                                bodyGlobal.isDSPActive = false;
                                bodyGlobal.isDying = true;
                                bodyGlobal.deathTimer = 0;
                                
                                if (Main.netMode != NetmodeID.MultiplayerClient)
                                {
                                    Main.npc[bodyIndex].netUpdate = true;
                                    npc.netUpdate = true;
                                }

                                for (int i = 0; i < Main.maxProjectiles; i++)
                                {
                                    if (Main.projectile[i].active && (Main.projectile[i].type == ModContent.ProjectileType<GolemHealChain>() || Main.projectile[i].type == ModContent.ProjectileType<GolemWallSlamFist>()))
                                    {
                                        Main.projectile[i].Kill();
                                    }
                                }
                            }
                        }
                        else
                        {
                            if (bodyIndex != -1 && Main.npc[bodyIndex].active)
                            {
                                NPC bodyNpc = Main.npc[bodyIndex];

                                if ((float)bodyNpc.life / bodyNpc.lifeMax <= 0.75f)
                                {
                                    laserCounterUnder75++; 

                                    if (!isWallSlamActive && laserCounterUnder75 >= 2)
                                    {
                                        isWallSlamActive = true;
                                        laserCounterUnder75 = 0; 

                                        if (Main.netMode != NetmodeID.MultiplayerClient)
                                        {
                                            int wallSlamDamage = 34; 
                                            
                                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, new Vector2(-8f, 0f), ModContent.ProjectileType<GolemWallSlamFist>(), wallSlamDamage, 0f, Main.myPlayer, -1f, 0f);
                                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, new Vector2(8f, 0f), ModContent.ProjectileType<GolemWallSlamFist>(), wallSlamDamage, 0f, Main.myPlayer, 1f, 0f);
                                        }
                                        SoundEngine.PlaySound(SoundID.Item62, npc.Center); 
                                    }
                                    else if (isWallSlamActive && laserCounterUnder75 >= 2)
                                    {
                                        isWallSlamActive = false;
                                        laserCounterUnder75 = 0; 

                                        if (Main.netMode != NetmodeID.MultiplayerClient)
                                        {
                                            int fistType = ModContent.ProjectileType<GolemWallSlamFist>();
                                            for (int i = 0; i < Main.maxProjectiles; i++)
                                            {
                                                if (Main.projectile[i].active && Main.projectile[i].type == fistType)
                                                {
                                                    Main.projectile[i].Kill(); 
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                return false; 
            }
            return true;
        }

        public override bool CheckDead(NPC npc)
        {
            if (npc.type == NPCID.Golem)
            {
                var localGlobal = npc.GetGlobalNPC<GolemHeadOverride>();
                
                if (localGlobal.trulyDead) { return true; }

                if (!localGlobal.isDSPActive && !localGlobal.isDying)
                {
                    localGlobal.isDSPActive = true; 
                    npc.life = 1;                  
                    npc.dontTakeDamage = true;     
                    npc.netUpdate = true;

                    int headFreeIndex = NPC.FindFirstNPC(NPCID.GolemHeadFree);
                    if (headFreeIndex != -1)
                    {
                        Main.npc[headFreeIndex].GetGlobalNPC<GolemHeadOverride>().ResetAttackStatesForDSP();
                    }

                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<GolemWallSlamFist>())
                        {
                            Main.projectile[i].Kill();
                        }
                    }

                    SoundEngine.PlaySound(SoundID.Roar, npc.Center); 
                    return false; 
                }

                if (localGlobal.isDying)
                {
                    return false;
                }
            }
            return true;
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (npc.type == NPCID.Golem)
            {
                if (isDying) return false; 
            }

            if (npc.type == NPCID.GolemHeadFree)
            {
                int bodyIndex = NPC.FindFirstNPC(NPCID.Golem);
                if (bodyIndex != -1 && Main.npc[bodyIndex].active)
                {
                    var bodyGlobal = Main.npc[bodyIndex].GetGlobalNPC<GolemHeadOverride>();
                    if (bodyGlobal.isDying)
                    {
                        Texture2D texture = TextureAssets.Npc[npc.type].Value;
                        SpriteEffects effects = npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                        Vector2 origin = npc.frame.Size() / 2f;

                        spriteBatch.Draw(texture, npc.Center - screenPos, npc.frame, drawColor, npc.rotation, origin, npc.scale, effects, 0f);
                        return false; 
                    }
                }
            }
            return true; 
        }

        private bool CheckAnyPlayerAlive()
        {
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                if (Main.player[i].active && !Main.player[i].dead) return true; 
            }
            return false; 
        }

        private void SpawnEyeBeamRing(NPC npc, int projectileCount)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int eyeBeamDamage = 22; 
            
            float shootSpeed = 5.5f;
            
            float randomRotationOffset = Main.rand.NextFloat() * MathHelper.TwoPi;

            for (int i = 0; i < projectileCount; i++)
            {
                float finalAngle = randomRotationOffset + (i * (MathHelper.TwoPi / projectileCount));
                Vector2 velocity = finalAngle.ToRotationVector2() * shootSpeed;

                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center + new Vector2(0, -12), velocity, ProjectileID.EyeBeam, eyeBeamDamage, 0f, Main.myPlayer);
            }
        }

        private void FindLihzahrdAltar(NPC npc)
        {
            int altarX = -1; int altarY = -1; bool found = false;
            int currentTileX = (int)(npc.Center.X / 16);
            int currentTileY = (int)(npc.Center.Y / 16);
            int startX = Math.Max(10, currentTileX - 200); int startY = Math.Max(10, currentTileY - 200);
            
            int endX = Math.Min(Main.maxTilesX - 10, currentTileX + 200); 
            int endY = Math.Min(Main.maxTilesY - 10, currentTileY + 200);

            for (int x = startX; x < endX; x++)
            {
                for (int y = startY; y < endY; y++)
                {
                    if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.LihzahrdAltar)
                    {
                        altarX = x; altarY = y; found = true; break;
                    }
                }
                if (found) break;
            }

            if (found) targetArenaCenter = new Vector2(altarX * 16 + 24, (altarY - 45) * 16);
            else targetArenaCenter = npc.Center + new Vector2(0, -480);
        }

        public override void FindFrame(NPC npc, int frameHeight)
        {
            if (npc.type == NPCID.GolemHeadFree)
            {
                if (isLaserActive || isFillerActive || isFistRingActive || isClapActive || isClapPhaseActive || isStyngerActive || isAbsorbingFireballs || isSpewingFireballs || preSpewDelayTimer > 0 || postSpewDelayTimer > 0)
                {
                    npc.frame.Y = frameHeight; 
                }
                else
                {
                    npc.frame.Y = 0; 
                }
            }
        }
    }

    public class GolemFireballHandle : global::Terraria.ModLoader.GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool isGolemSuction = false; 
        public bool isGolemSpew = false;    
        public int targetNPCIndex = -1;

        private int suctionDelay = -1;
        private float currentPullSpeed = 0.5f; 

        public override bool PreAI(Projectile projectile)
        {
            if (isGolemSuction)
            {
                projectile.tileCollide = false; 
                projectile.hostile = false;     
                projectile.friendly = false;
                projectile.alpha = 135;          

                projectile.frameCounter++;
                if (projectile.frameCounter >= 4)
                {
                    projectile.frameCounter = 0;
                    projectile.frame++;
                    if (projectile.frame >= Main.projFrames[projectile.type])
                    {
                        projectile.frame = 0;
                    }
                }
                Lighting.AddLight(projectile.Center, 0.4f, 0.1f, 0.05f);

                int dustType = (projectile.type == ProjectileID.CultistBossFireBall) ? DustID.Torch : DustID.Shadowflame;
                if (Main.rand.NextBool(3))
                {
                    int d = Dust.NewDust(projectile.position, projectile.width, projectile.height, dustType, -projectile.velocity.X * 0.3f, -projectile.velocity.Y * 0.3f, 130, default, 1.2f);
                    Main.dust[d].noGravity = true;
                }

                if (targetNPCIndex != -1 && Main.npc[targetNPCIndex].active)
                {
                    Vector2 targetPos = Main.npc[targetNPCIndex].Center;
                    Vector2 dir = targetPos - projectile.Center;
                    float distance = dir.Length();

                    if (suctionDelay == -1)
                    {
                        suctionDelay = Main.rand.Next(0, 180); 
                    }

                    if (distance < 24f) 
                    {
                        if (Main.npc[targetNPCIndex].type == NPCID.GolemHeadFree)
                        {
                            var headGlobal = Main.npc[targetNPCIndex].GetGlobalNPC<GolemHeadOverride>();
                            
                            if (projectile.type == ProjectileID.CultistBossFireBall)
                            {
                                headGlobal.absorbedFireballs++;
                            }
                            else if (projectile.type == ProjectileID.CultistBossFireBallClone)
                            {
                                headGlobal.absorbedClones++;
                            }
                        }
                        
                        for (int k = 0; k < 5; k++)
                        {
                            int dustIdx = Dust.NewDust(projectile.Center, 0, 0, DustID.Lava, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f));
                            Main.dust[dustIdx].noGravity = true;
                        }
                        projectile.Kill(); 
                    }
                    else
                    {
                        if (suctionDelay > 0)
                        {
                            suctionDelay--;
                            float hoverSpeed = 0.5f;
                            projectile.velocity = dir.SafeNormalize(Vector2.Zero) * hoverSpeed;
                        }
                        else
                        {
                            if (currentPullSpeed < 6.0f) currentPullSpeed += 0.05f;
                            projectile.velocity = dir.SafeNormalize(Vector2.Zero) * currentPullSpeed;
                        }
                    }
                }
                else
                {
                    projectile.Kill();
                }

                projectile.rotation += 0.12f; 
                return false; 
            }

            if (isGolemSpew)
            {
                projectile.tileCollide = false; 
                projectile.hostile = true;      
                projectile.friendly = false;
                projectile.alpha = 0;          

                projectile.frameCounter++;
                if (projectile.frameCounter >= 4)
                {
                    projectile.frameCounter = 0;
                    projectile.frame++;
                    if (projectile.frame >= Main.projFrames[projectile.type])
                    {
                        projectile.frame = 0;
                    }
                }
                Lighting.AddLight(projectile.Center, 0.9f, 0.4f, 0.1f);

                if (Main.rand.NextBool(3))
                {
                    int d = Dust.NewDust(projectile.position, projectile.width, projectile.height, DustID.Torch, 0f, 0f, 100, default, 1.1f);
                    Main.dust[d].noGravity = true;
                }

                projectile.rotation = projectile.velocity.ToRotation() + MathHelper.PiOver2;
                return false; 
            }
            return true;
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            return true;
        }
    }
}