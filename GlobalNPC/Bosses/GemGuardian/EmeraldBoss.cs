using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics.CameraModifiers;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;

namespace TheSanity.GlobalNPC.Bosses.GemGuardian
{
    [AutoloadBossHead]
    public class EmeraldBoss : ModNPC
    {
        private const int STATE_SUMMON_ANIMATION = 0;
        private const int STATE_CHOOSE = 1;
        private const int STATE_SPIN_IN_PLACE = 2;
        private const int STATE_DASH_NORMAL = 3;
        private const int STATE_ORBIT = 4;
        private const int STATE_DASH_RANDOM = 5;
        private const int STATE_TELEPORT_PREPARE = 6;
        private const int STATE_TELEPORT_DASH = 7;
        private const int STATE_SNEAKY_START = 8;
        private const int STATE_SNEAKY_DASH1 = 9;
        private const int STATE_SNEAKY_BLINK = 10;
        private const int STATE_SNEAKY_DASH2 = 11;
        private const int STATE_PREDICT_AIM = 12;
        private const int STATE_PREDICT_DASH = 13;
        private const int STATE_PREDICT_TP = 14;
        private const int STATE_SEED_BARRAGE = 15; 
        private const int STATE_DEATH = 16;

        public float AI_State { get => NPC.ai[0]; set => NPC.ai[0] = value; }
        public float AI_Timer { get => NPC.ai[1]; set => NPC.ai[1] = value; }
        public float AI_DashCount { get => NPC.ai[2]; set => NPC.ai[2] = value; }
        public float AI_MaxDashes { get => NPC.ai[3]; set => NPC.ai[3] = value; }

        private float orbitAngle = 0f;
        private bool IsPhase2 => NPC.life <= NPC.lifeMax * 0.5f;

        private int orbitComboCount = 0;
        private int maxOrbitCombos = 0;
        private int teleportCount = 0;
        private int maxTeleports = 0;
        private int teleportComboCount = 0;
        private int maxTeleportCombos = 0;

        private int predictComboCount = 0;
        private int maxPredictCombos = 0;
        private Vector2 predictedTarget = Vector2.Zero;
        private Vector2 predictDashVector = Vector2.Zero;
        private bool drawPredictLaser = false;

        private Vector2 lockedDashVelocity = Vector2.Zero; 

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.TrailingMode[Type] = 3; 
            NPCID.Sets.TrailCacheLength[Type] = 15; 

            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Slow] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Poisoned] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Venom] = true;
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void SetDefaults() {
            NPC.width = 64;
            NPC.height = 64;
            NPC.damage = 300; // Contact Damage dasar Boss tetap besar (300)
            NPC.defense = 46;
            NPC.lifeMax = 12000;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
            
            NPC.HitSound = SoundID.DD2_CrystalCartImpact; 
            NPC.DeathSound = SoundID.NPCDeath1; 
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) {
            if ((int)AI_State == STATE_SUMMON_ANIMATION) {
                return false;
            }
            return base.CanHitPlayer(target, ref cooldownSlot);
        }

        public override void AI() {
            if (NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active) {
                NPC.TargetClosest(true);
            }
            Player player = Main.player[NPC.target];

            bool isInUndergroundJungle = player.ZoneJungle && (player.Center.Y > Main.worldSurface * 16f);

            if (player.dead || !isInUndergroundJungle) {
                NPC.velocity.Y += 1.5f; 
                NPC.EncourageDespawn(10);
                return;
            }

            if (AI_State != STATE_DEATH) {
                int curseBuffType = ModContent.BuffType<ElementalCurse>(); 
                for (int i = 0; i < Main.maxPlayers; i++) {
                    Player p = Main.player[i];
                    if (p.active && !p.dead) {
                        p.AddBuff(curseBuffType, 2); 
                    }
                }
            }

            float speedMult = IsPhase2 ? 2.0f : 1.0f;

            // FIX: Pembagian otomatis agar peluru tidak overscale menyentuh 300+ di Expert/Master/Calamity
            int normalProjDamage = Main.masterMode ? 15 : (Main.expertMode ? 22 : 45);
            int sneakyProjDamage = Main.masterMode ? 18 : (Main.expertMode ? 25 : 50);
            int seedDamage = Main.masterMode ? 12 : (Main.expertMode ? 20 : 40);
            int thornDamage = Main.masterMode ? 18 : (Main.expertMode ? 25 : 50);

            switch ((int)AI_State) {
                case STATE_SUMMON_ANIMATION:
                    NPC.dontTakeDamage = true;

                    if (AI_Timer < 120f) {
                        if (AI_Timer == 0) {
                            NPC.Center = new Vector2(player.Center.X, player.Center.Y + 1000f);
                            NPC.alpha = 170; 

                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                Main.NewText("The ancient core awakens... The wrath of the Nature Guardian fills the air!", 0, 255, 60);
                            }
                        }

                        if (Main.rand.NextBool(3)) {
                            var shakeMod = new PunchCameraModifier(NPC.Center, new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-1.2f, 1.2f)), 6f, 6f, 10, 1200f, Mod.Name);
                            Main.instance.CameraModifiers.Add(shakeMod);
                        }
                        NPC.velocity = Vector2.Zero;
                        AI_Timer++;
                    }
                    else if (AI_Timer >= 120f && AI_Timer < 500f) {
                        NPC.velocity.X = (player.Center.X - NPC.Center.X) * 0.05f; 
                        NPC.velocity.Y = -22f; 

                        if (Main.rand.NextBool(2)) {
                            Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GemEmerald, 0f, -2f, 120, default, 1.3f);
                        }

                        if (NPC.Center.Y <= player.Center.Y - 400f) {
                            NPC.velocity = Vector2.Zero;
                            NPC.alpha = 0; 

                            SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
                            SpawnGemDustParticles();

                            for (int i = 0; i < 35; i++) {
                                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GemEmerald, Main.rand.NextFloat(-7f, 7f), Main.rand.NextFloat(-7f, 7f), 0, default, 1.8f);
                                d.noGravity = true;
                            }

                            var roarShake = new PunchCameraModifier(NPC.Center, new Vector2(0f, -18f), 20f, 15f, 40, 1500f, Mod.Name);
                            Main.instance.CameraModifiers.Add(roarShake);

                            AI_Timer = 500f; 
                            NPC.netUpdate = true;
                        }
                    }
                    else if (AI_Timer >= 500f) {
                        NPC.velocity = Vector2.Zero; 
                        AI_Timer++;

                        if (AI_Timer >= 545f) { 
                            NPC.dontTakeDamage = false; 
                            AI_State = STATE_CHOOSE;
                            AI_Timer = 0;
                            NPC.netUpdate = true;
                        }
                    }
                    break;

                case STATE_CHOOSE:
                    NPC.velocity *= 0.8f;
                    AI_Timer = 0;
                    NPC.damage = 300; 
                    NPC.alpha = 0;
                    NPC.dontTakeDamage = false;
                    drawPredictLaser = false;

                    int finalChoice;
                    if (IsPhase2) {
                        int[] phase2Options = { 0, 1, 2, 3, 4, 15 };
                        finalChoice = phase2Options[Main.rand.Next(phase2Options.Length)];
                    } else {
                        int[] phase1Options = { 0, 1, 2, 4, 15 };
                        finalChoice = phase1Options[Main.rand.Next(phase1Options.Length)];
                    }

                    if (finalChoice == 0) {
                        AI_DashCount = 0;
                        AI_MaxDashes = Main.rand.Next(7, 11);
                        AI_State = STATE_SPIN_IN_PLACE;
                    }
                    else if (finalChoice == 1) {
                        orbitComboCount = 0;
                        maxOrbitCombos = Main.rand.Next(5, 8);
                        AI_State = STATE_ORBIT;
                    }
                    else if (finalChoice == 2) {
                        teleportComboCount = 0;
                        maxTeleportCombos = IsPhase2 ? Main.rand.Next(9, 12) : Main.rand.Next(7, 9);
                        teleportCount = 0;
                        maxTeleports = IsPhase2 ? Main.rand.Next(6, 9) : Main.rand.Next(4, 7);
                        AI_State = STATE_TELEPORT_PREPARE;
                    }
                    else if (finalChoice == 3) {
                        AI_State = STATE_SNEAKY_START;
                    }
                    else if (finalChoice == 4) {
                        predictComboCount = 0;
                        maxPredictCombos = IsPhase2 ? Main.rand.Next(8, 11) : Main.rand.Next(5, 8);
                        AI_State = STATE_PREDICT_AIM;
                    }
                    else if (finalChoice == 15) {
                        AI_DashCount = 0; 
                        AI_State = STATE_SEED_BARRAGE;
                    }

                    NPC.netUpdate = true;
                    break;

                case STATE_SPIN_IN_PLACE:
                    NPC.velocity *= 0.85f; 
                    NPC.rotation += 0.35f * speedMult; 

                    AI_Timer += 1f * speedMult;
                    if (AI_Timer >= 60f) { 
                        SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                        
                        lockedDashVelocity = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * (IsPhase2 ? 24f : 14f);
                        NPC.velocity = lockedDashVelocity;

                        SpawnDashProjectiles(player);
                        AI_State = STATE_DASH_NORMAL;
                        AI_Timer = 0;
                    }
                    break;

                case STATE_DASH_NORMAL:
                    NPC.velocity = lockedDashVelocity; 
                    NPC.rotation = NPC.velocity.ToRotation() - MathHelper.PiOver2;

                    if (IsPhase2 && AI_Timer % 4 == 0) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // FIX: Menggunakan normalProjDamage yang sudah diturunkan nilainya
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<EmeraldBossProjectile>(), normalProjDamage, 1f, Main.myPlayer, 1f);
                        }
                    }

                    AI_Timer += 1f * speedMult;
                    if (AI_Timer >= 40f) { 
                        AI_DashCount++;
                        if (AI_DashCount >= AI_MaxDashes) {
                            AI_State = STATE_CHOOSE;
                        } else {
                            AI_State = STATE_SPIN_IN_PLACE;
                        }
                        AI_Timer = 0;
                    }
                    break;

                case STATE_ORBIT:
                    orbitAngle += 0.06f * speedMult; 
                    NPC.rotation += 0.2f * speedMult; 

                    Vector2 orbitTarget = player.Center + orbitAngle.ToRotationVector2() * 320f;
                    NPC.velocity = (orbitTarget - NPC.Center) * 0.1f; 

                    AI_Timer += 1f * speedMult;
                    if (AI_Timer >= 90f) { 
                        SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                        
                        lockedDashVelocity = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * (IsPhase2 ? 26f : 15f);
                        NPC.velocity = lockedDashVelocity;
                        
                        SpawnDashProjectiles(player);
                        AI_State = STATE_DASH_RANDOM;
                        AI_Timer = 0;
                    }
                    break;

                case STATE_DASH_RANDOM:
                    NPC.velocity = lockedDashVelocity; 
                    NPC.rotation = NPC.velocity.ToRotation() - MathHelper.PiOver2;

                    if (IsPhase2 && AI_Timer % 4 == 0) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // FIX: Menggunakan normalProjDamage
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<EmeraldBossProjectile>(), normalProjDamage, 1f, Main.myPlayer, 1f);
                        }
                    }

                    AI_Timer += 1f * speedMult;
                    if (AI_Timer >= 40f) {
                        orbitComboCount++;
                        if (orbitComboCount >= maxOrbitCombos) {
                            AI_State = STATE_CHOOSE;
                        } else {
                            AI_State = STATE_ORBIT;
                        }
                        AI_Timer = 0;
                    }
                    break;

                case STATE_TELEPORT_PREPARE:
                    NPC.velocity = Vector2.Zero; 
                    NPC.rotation += 0.45f * speedMult; 

                    float teleportDelay = IsPhase2 ? 10f : 20f; 

                    AI_Timer++;
                    if (AI_Timer >= teleportDelay) {
                        SpawnGemDustParticles(); 

                        float distance = Main.rand.NextFloat(280f, 420f);
                        Vector2 randomVector = Main.rand.NextFloat(0f, MathHelper.TwoPi).ToRotationVector2();
                        NPC.Center = player.Center + randomVector * distance;

                        SpawnGemDustParticles(); 
                        SoundEngine.PlaySound(SoundID.Item8, NPC.Center); 

                        teleportCount++;
                        AI_Timer = 0;

                        if (teleportCount >= maxTeleports) {
                            SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                            
                            lockedDashVelocity = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * (IsPhase2 ? 28f : 16f);
                            NPC.velocity = lockedDashVelocity;
                            
                            SpawnDashProjectiles(player);
                            AI_State = STATE_TELEPORT_DASH;
                        }
                        NPC.netUpdate = true;
                    }
                    break;

                case STATE_TELEPORT_DASH:
                    NPC.velocity = lockedDashVelocity; 
                    NPC.rotation = NPC.velocity.ToRotation() - MathHelper.PiOver2;

                    if (IsPhase2 && AI_Timer % 4 == 0) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // FIX: Menggunakan normalProjDamage
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<EmeraldBossProjectile>(), normalProjDamage, 1f, Main.myPlayer, 1f);
                        }
                    }

                    AI_Timer += 1f * speedMult;
                    if (AI_Timer >= 35f) { 
                        teleportComboCount++;
                        if (teleportComboCount >= maxTeleportCombos) {
                            AI_State = STATE_CHOOSE;
                        } else {
                            teleportCount = 0;
                            maxTeleports = IsPhase2 ? Main.rand.Next(6, 9) : Main.rand.Next(4, 7);
                            AI_State = STATE_TELEPORT_PREPARE;
                        }
                        AI_Timer = 0;
                    }
                    break;

                case STATE_SNEAKY_START:
                    NPC.velocity *= 0.8f;
                    NPC.rotation += 0.6f; 

                    AI_Timer++;
                    if (AI_Timer >= 45f) {
                        SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                        NPC.damage = 380; 
                        
                        lockedDashVelocity = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 32f; 
                        NPC.velocity = lockedDashVelocity;

                        AI_State = STATE_SNEAKY_DASH1;
                        AI_Timer = 0;
                    }
                    break;

                case STATE_SNEAKY_DASH1:
                    NPC.velocity = lockedDashVelocity; 
                    NPC.rotation = NPC.velocity.ToRotation() - MathHelper.PiOver2;

                    AI_Timer++;
                    if (AI_Timer >= 25f) { 
                        NPC.damage = 300; 
                        NPC.velocity = Vector2.Zero;
                        NPC.alpha = 255; 
                        NPC.dontTakeDamage = true;

                        float blinkDistance = Main.rand.NextFloat(320f, 440f);
                        Vector2 blinkOffset = Main.rand.NextFloat(0f, MathHelper.TwoPi).ToRotationVector2();
                        NPC.Center = player.Center + blinkOffset * blinkDistance;

                        AI_State = STATE_SNEAKY_BLINK;
                        AI_Timer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case STATE_SNEAKY_BLINK:
                    NPC.velocity = Vector2.Zero; 
                    
                    AI_Timer++; 
                    if (AI_Timer >= 40f) { 
                        NPC.alpha = 0;
                        NPC.dontTakeDamage = false;
                        NPC.damage = 450; 
                        
                        lockedDashVelocity = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 35f;
                        NPC.velocity = lockedDashVelocity;
                        SoundEngine.PlaySound(SoundID.Item74, NPC.Center);

                        AI_State = STATE_SNEAKY_DASH2;
                        AI_Timer = 0;
                    }
                    break;

                case STATE_SNEAKY_DASH2:
                    NPC.velocity = lockedDashVelocity; 
                    NPC.rotation = NPC.velocity.ToRotation() - MathHelper.PiOver2;

                    if (AI_Timer % 3 == 0) {
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // FIX: Menggunakan sneakyProjDamage dinamis
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<EmeraldBossProjectile>(), sneakyProjDamage, 1f, Main.myPlayer, 1f);
                        }
                    }

                    AI_Timer++;
                    if (AI_Timer >= 40f) {
                        NPC.damage = 300; 
                        AI_State = STATE_CHOOSE; 
                        AI_Timer = 0;
                    }
                    break;

                case STATE_PREDICT_AIM:
                    NPC.velocity = Vector2.Zero; 

                    float maxAimTime = IsPhase2 ? 24f : 45f; 
                    float laserHideTime = IsPhase2 ? 8f : 12f; 

                    AI_Timer++;
                    if (AI_Timer <= maxAimTime - laserHideTime) {
                        drawPredictLaser = true; 

                        float predictFactor = IsPhase2 ? 14f : 22f;
                        predictedTarget = player.Center + player.velocity * predictFactor;

                        NPC.rotation = (predictedTarget - NPC.Center).ToRotation() - MathHelper.PiOver2;
                    }
                    else {
                        drawPredictLaser = false; 

                        if (AI_Timer == (maxAimTime - laserHideTime) + 1f) {
                            predictDashVector = (predictedTarget - NPC.Center).SafeNormalize(Vector2.UnitY);
                        }
                    }

                    if (AI_Timer >= maxAimTime) {
                        SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
                        
                        lockedDashVelocity = predictDashVector * (IsPhase2 ? 48f : 38f);
                        NPC.velocity = lockedDashVelocity;

                        SpawnDashProjectiles(player); 
                        AI_State = STATE_PREDICT_DASH;
                        AI_Timer = 0;
                    }
                    break;

                case STATE_PREDICT_DASH:
                    NPC.velocity = lockedDashVelocity; 
                    NPC.rotation = NPC.velocity.ToRotation() - MathHelper.PiOver2;

                    if (IsPhase2 && AI_Timer % 2 == 0) { 
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            // FIX: Menggunakan normalProjDamage
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<EmeraldBossProjectile>(), normalProjDamage, 1f, Main.myPlayer, 1f);
                        }
                    }

                    AI_Timer++;
                    float maxDashDuration = IsPhase2 ? 25f : 35f; 
                    if (AI_Timer >= maxDashDuration) {
                        AI_State = STATE_PREDICT_TP; 
                        AI_Timer = 0;
                    }
                    break;

                case STATE_PREDICT_TP:
                    NPC.velocity = Vector2.Zero;
                    SpawnGemDustParticles(); 

                    float tpDist = Main.rand.NextFloat(300f, 460f);
                    Vector2 tpOffset = Main.rand.NextFloat(0f, MathHelper.TwoPi).ToRotationVector2();
                    NPC.Center = player.Center + tpOffset * tpDist;

                    SpawnGemDustParticles(); 
                    SoundEngine.PlaySound(SoundID.Item8, NPC.Center); 

                    predictComboCount++;
                    if (predictComboCount >= maxPredictCombos) {
                        AI_State = STATE_CHOOSE; 
                    } else {
                        AI_State = STATE_PREDICT_AIM; 
                    }
                    AI_Timer = 0;
                    NPC.netUpdate = true;
                    break;

                case STATE_SEED_BARRAGE:
                    NPC.velocity *= 0.82f; 
                    NPC.rotation += 0.28f * speedMult; 

                    int maxSeeds = IsPhase2 ? 20 : 12; 
                    int shootDelay = IsPhase2 ? 3 : 5;   

                    AI_Timer++;

                    if (AI_Timer % shootDelay == 0 && AI_DashCount < maxSeeds) {
                        SoundEngine.PlaySound(SoundID.Item17, NPC.Center); 
                        
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 shootDirection = NPC.rotation.ToRotationVector2(); 
                            // FIX: Menggunakan seedDamage dinamis
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, shootDirection * 10f, ProjectileID.PoisonSeedPlantera, seedDamage, 1f, Main.myPlayer);
                        }
                        
                        AI_DashCount++; 

                        if (IsPhase2 && AI_DashCount == maxSeeds) {
                            SoundEngine.PlaySound(SoundID.Item82, NPC.Center); 
                            
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                Vector2 thornUp = new Vector2(0f, -8.5f);        
                                Vector2 thornLeft = new Vector2(-6f, -6f);      
                                Vector2 thornRight = new Vector2(6f, -6f);      

                                // FIX: Menggunakan thornDamage dinamis
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, thornUp, ProjectileID.ThornBall, thornDamage, 1.5f, Main.myPlayer);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, thornLeft, ProjectileID.ThornBall, thornDamage, 1.5f, Main.myPlayer);
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, thornRight, ProjectileID.ThornBall, thornDamage, 1.5f, Main.myPlayer);
                            }
                        }
                    }

                    float totalBarrageTime = (maxSeeds * shootDelay) + 35f;
                    if (AI_Timer >= totalBarrageTime) {
                        AI_State = STATE_CHOOSE;
                        AI_Timer = 0;
                        AI_DashCount = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case STATE_DEATH:
                    NPC.velocity *= 0.90f; 
                    NPC.rotation += 0.5f;   
                    AI_Timer++;

                    if (Main.rand.NextBool(2)) {
                        Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GemEmerald, 0f, 0f, 100, default, 1.5f);
                    }

                    if (AI_Timer >= 120f) { 
                        ExplodeBoss();
                    }
                    break;
            }
        }

        private void SpawnDashProjectiles(Player player) {
            SoundEngine.PlaySound(SoundID.Item42, NPC.Center); 
            Vector2 baseVel = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitY) * 6f;

            // FIX: Menggunakan pembatasan agar tidak overscale di Expert/Master
            int normalProjDamage = Main.masterMode ? 15 : (Main.expertMode ? 22 : 45);

            float spreadAngle = MathHelper.ToRadians(20);
            for (int i = -1; i <= 1; i++) {
                Vector2 speed = baseVel.RotatedBy(spreadAngle * i);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, speed, ModContent.ProjectileType<EmeraldBossProjectile>(), normalProjDamage, 1f, Main.myPlayer, 0f); 
                }
            }
        }

        private void SpawnGemDustParticles() {
            for (int i = 0; i < 18; i++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GemEmerald, 0f, 0f, 100, default, 1.6f);
                dust.velocity *= 2.5f;
                dust.noGravity = true;
            }
        }

        public override bool CheckDead() {
            if (AI_State != STATE_DEATH) {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                AI_State = STATE_DEATH;
                AI_Timer = 0;
                NPC.netUpdate = true;
                return false; 
            }
            return true;
        }

        private void ExplodeBoss() {
            SoundEngine.PlaySound(SoundID.Item62, NPC.Center); 

            // FIX: Menggunakan pembatasan explode damage dinamis
            int explodeDamage = Main.masterMode ? 15 : (Main.expertMode ? 22 : 45);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                for (int i = 0; i < 10; i++) {
                    Vector2 vel = MathHelper.ToRadians(360f / 10f * i).ToRotationVector2() * 3f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<EmeraldBossProjectile>(), explodeDamage, 1f, Main.myPlayer, 1f); 
                }

                for (int i = 0; i < 40; i++) {
                    Vector2 vel = MathHelper.ToRadians(360f / 40f * i).ToRotationVector2() * Main.rand.NextFloat(5f, 9f);
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<EmeraldBossProjectile>(), explodeDamage, 1f, Main.myPlayer, 0f); 
                }
            }

            for (int d = 0; d < 80; d++) {
                Dust dust = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.GemEmerald, 0f, 0f, 100, default, 2f);
                dust.velocity *= 3f;
            }

            NPC.life = 0;
            NPC.HitEffect();
            NPC.active = false; 
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Vector2 drawOrigin = texture.Size() / 2f;
            SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Color glowColor = new Color(0, 255, 100, 255);

            if ((int)AI_State == STATE_PREDICT_AIM && drawPredictLaser) {
                Vector2 laserAimDir = predictedTarget - NPC.Center;
                float laserLen = laserAimDir.Length();
                if (laserLen < 2200f) laserLen = 2200f; 
                laserAimDir.Normalize();

                Color laserColor = new Color(0, 255, 60) * 0.18f; 
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, NPC.Center - screenPos, new Rectangle(0, 0, 1, 1), laserColor, laserAimDir.ToRotation(), new Vector2(0f, 0.5f), new Vector2(laserLen, 3f), SpriteEffects.None, 0f);
            }

            if ((int)AI_State == STATE_SNEAKY_BLINK) {
                float progress = AI_Timer / 40f; 
                float scaleEffect = MathHelper.Lerp(0.3f, 2.5f, progress); 
                Color blinkIndicatorColor = new Color(0, 255, 120, (int)(255 * (1f - progress))); 

                Vector2 drawBlinkPos = NPC.Center - screenPos;
                spriteBatch.Draw(texture, drawBlinkPos, null, blinkIndicatorColor, NPC.rotation + (progress * 4f), drawOrigin, scaleEffect, effects, 0f);
                spriteBatch.Draw(texture, drawBlinkPos, null, blinkIndicatorColor * 0.6f, -NPC.rotation - (progress * 2f), drawOrigin, scaleEffect * 1.3f, effects, 0f);
                
                return false; 
            }

            if (NPC.alpha >= 255) return false;

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;

                Vector2 trailDrawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos;
                Color trailColor = glowColor * ((NPC.oldPos.Length - i) / (float)NPC.oldPos.Length) * 0.38f;
                spriteBatch.Draw(texture, trailDrawPos, null, trailColor, NPC.rotation, drawOrigin, NPC.scale, effects, 0f);
            }

            float outlineThickness = 5f; 
            for (int i = 0; i < 8; i++) {
                Vector2 offset = new Vector2(outlineThickness, 0f).RotatedBy(MathHelper.PiOver4 * i);
                Vector2 outlineDrawPos = NPC.Center - screenPos + offset;
                spriteBatch.Draw(texture, outlineDrawPos, null, glowColor * 0.6f, NPC.rotation, drawOrigin, NPC.scale, effects, 0f);
            }

            Vector2 mainDrawPos = NPC.Center - screenPos;
            spriteBatch.Draw(texture, mainDrawPos, null, drawColor, NPC.rotation, drawOrigin, NPC.scale, effects, 0f);

            return false; 
        }
    }
}