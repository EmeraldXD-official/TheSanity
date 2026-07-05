using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules; 
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items; 
using TheSanity.Players;

namespace TheSanity.GlobalNPC.Bosses.WhiteWhale
{
    [AutoloadBossHead]
    public class WhiteWhaleBoss : ModNPC
    {
        private enum BossState {
            SpawnAnimation,
            Phase1,
            Phase2_Transition,
            Phase2_Active
        }

        public enum P2Attacks {
            Dash_Letter_X,
            Dash_Letter_Y,
            Dash_Letter_A,
            Dash_Letter_Z,
            Dash_Letter_T, 
            Dash_Letter_N, 
            Dash_Letter_H, 
            RotatingLaserTriangle,
            PredictiveSequentialDash
        }

        private enum P1Attacks {
            Dash3X,
            SequentialNebulaBlaze,
            LaserRotation180,
            SlamPlayer2X
        }

        private BossState State {
            get => (BossState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }
        private float GlobalTimer {
            get => NPC.ai[1];
            set => NPC.ai[1] = value;
        }
        public float AttackState {
            get => NPC.ai[2];
            set => NPC.ai[2] = value;
        }
        public float AttackTimer {
            get => NPC.ai[3];
            set => NPC.ai[3] = value;
        }
        
        private int dashCount = 0;
        private bool spawnedClones = false;
        private float dashTelegraphRotation = 0f; 
        private int laserIndex = -1; 
        
        public Vector2 rotatingCenter = Vector2.Zero; 

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 3; 
            NPCID.Sets.MPAllowedEnemies[NPC.type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(NPC.type);

            NPCID.Sets.TrailCacheLength[NPC.type] = 6; 
            NPCID.Sets.TrailingMode[NPC.type] = 3;     
        }

        public override void SetDefaults() {
            NPC.width = 200;
            NPC.height = 120;
            NPC.damage = 120;
            NPC.defense = 90;
            NPC.lifeMax = 87000;
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f; 
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath10;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<WhiteWhaleBag>()));
            npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<WhaleWhiteRelic>()));
        }

        public override void FindFrame(int frameHeight) {
            bool useFrameThree = false;

            if (State == BossState.Phase1 && (P1Attacks)AttackState == P1Attacks.Dash3X && NPC.velocity.Length() > 5f) {
                useFrameThree = true; 
            }
            else if (State == BossState.Phase1 && (P1Attacks)AttackState == P1Attacks.SlamPlayer2X && (AttackTimer % 90) > 45 && (AttackTimer % 90) < 80) {
                useFrameThree = true;
            }
            else if (State == BossState.Phase2_Active && (P2Attacks)AttackState <= P2Attacks.Dash_Letter_H && NPC.velocity.Length() > 8f) {
                useFrameThree = true;
            }
            else if (State == BossState.Phase2_Active && (P2Attacks)AttackState == P2Attacks.PredictiveSequentialDash && NPC.velocity.Length() > 8f) {
                useFrameThree = true;
            }
            
            if (State == BossState.Phase1 && (P1Attacks)AttackState == P1Attacks.LaserRotation180 && AttackTimer > 40 && AttackTimer < 140) {
                useFrameThree = true;
            }
            else if (State == BossState.Phase2_Active && (P2Attacks)AttackState == P2Attacks.RotatingLaserTriangle) {
                useFrameThree = true;
            }

            if (useFrameThree) {
                NPC.frame.Y = frameHeight * 2; 
                NPC.frameCounter = 0; 
            }
            else {
                NPC.frameCounter++;
                if (NPC.frameCounter >= 10) { 
                    NPC.frameCounter = 0;
                    NPC.frame.Y += frameHeight; 

                    if (NPC.frame.Y >= frameHeight * 2) {
                        NPC.frame.Y = 0;
                    }
                }
            }

            NPC.spriteDirection = -NPC.direction; 
        }

        public override void AI() {
            if (NPC.target < 0 || NPC.target == 255 || Main.player[NPC.target].dead || !Main.player[NPC.target].active) {
                NPC.TargetClosest();
            }
            Player target = Main.player[NPC.target];

            if (target.dead || !target.active) {
                NPC.velocity.Y -= 0.5f; 
                NPC.EncourageDespawn(10); 
                return; 
            }

            if (Main.dayTime) {
                NPC.velocity.Y -= 0.5f; 
                NPC.EncourageDespawn(10); 
                return; 
            }

            // Kunci arah agar mulut tidak berpindah tempat secara berlawanan di tengah menembak laser
            bool lockDirection = (State == BossState.Phase1 && (P1Attacks)AttackState == P1Attacks.LaserRotation180 && AttackTimer >= 40 && AttackTimer <= 140);
            if (!lockDirection) {
                NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;
            }

            switch (State) {
                case BossState.SpawnAnimation:
                    UpdateSpawnAnimation(target);
                    break;

                case BossState.Phase1:
                    UpdatePhase1(target);
                    break;

                case BossState.Phase2_Transition:
                    UpdatePhase2Transition(target);
                    break;

                case BossState.Phase2_Active:
                    UpdatePhase2(target);
                    break;
            }

            float maxDistance = 120f * 16f; 
            if (!Main.dayTime && !target.dead && target.active && Vector2.Distance(NPC.Center, target.Center) > maxDistance) {
                NPC.Center = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.Zero) * maxDistance;
                NPC.velocity *= 0.5f; 
            }

            if (!Main.dedServ && State != BossState.SpawnAnimation) {
                // Jangan spawn dust kalau boss sedang mode siluet
                if (NPC.localAI[3] == 0f) {
                    for (int i = 0; i < 4; i++) {
                        float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                        Vector2 haloOffset = angle.ToRotationVector2() * new Vector2(110f, 65f) + new Vector2(NPC.spriteDirection * 90f, -10f); 
                        
                        int dustType = Main.rand.NextBool() ? DustID.PinkTorch : DustID.WhiteTorch;
                        int d = Dust.NewDust(NPC.Center + haloOffset, 0, 0, dustType, 0f, 0f, 60, default, Main.rand.NextFloat(1.4f, 2.2f));
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity = NPC.velocity * 0.4f + Main.rand.NextVector2Circular(1f, 1f); 
                    }

                    bool isDashing = (State == BossState.Phase1 && (P1Attacks)AttackState == P1Attacks.Dash3X && NPC.velocity.Length() > 18f) ||
                                     (State == BossState.Phase2_Active && (P2Attacks)AttackState <= P2Attacks.Dash_Letter_H && NPC.velocity.Length() > 18f) ||
                                     (State == BossState.Phase2_Active && (P2Attacks)AttackState == P2Attacks.PredictiveSequentialDash && NPC.velocity.Length() > 18f);

                    if (isDashing) {
                        for (int i = 0; i < 16; i++) {
                            int dustType = Main.rand.NextBool() ? DustID.PinkTorch : DustID.WhiteTorch;
                            Vector2 backOffset = -NPC.velocity.SafeNormalize(Vector2.Zero) * 90f;
                            Vector2 spawnPos = NPC.Center + backOffset + new Vector2(NPC.spriteDirection * 90f, -10f) + Main.rand.NextVector2Circular(50f, 50f);
                            
                            int d = Dust.NewDust(spawnPos, 0, 0, dustType, 0f, 0f, 40, default, Main.rand.NextFloat(1.8f, 2.6f));
                            Main.dust[d].noGravity = true;
                            Main.dust[d].velocity = -NPC.velocity * Main.rand.NextFloat(0.2f, 0.4f) + Main.rand.NextVector2Circular(4f, 4f);
                        }
                    }
                }
            }
        }

        private void UpdateSpawnAnimation(Player target) {
            GlobalTimer++;
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.95f; 

            if (GlobalTimer == 1) {
                NPC.Center = target.Center + new Vector2(0, -500); 
                NPC.netUpdate = true; 
            }

            SpawnFog(target, 4, -200, 700); 

            if (target.whoAmI == Main.myPlayer && target.TryGetModPlayer<WhaleCursePlayer>(out var whalePlayer)) {
                if (GlobalTimer < 1800) { 
                    whalePlayer.trackBossIndex = NPC.whoAmI; 
                } else {
                    whalePlayer.trackBossIndex = -1; 
                }
            }

            if (GlobalTimer == 60) {
                Main.NewText("<Moby Dick> ...", 175, 75, 255);
                CombatText.NewText(NPC.getRect(), new Color(175, 75, 255), "...", true);
            }
            else if (GlobalTimer == 300) {
                Main.NewText("<Moby Dick> I am the creature shrouded in mist... the pure manifestation of the Sin of Gluttony.", 175, 75, 255);
                CombatText.NewText(NPC.getRect(), new Color(175, 75, 255), "Sin of Gluttony...", true);
            }
            else if (GlobalTimer == 600) {
                Main.NewText("<Moby Dick> This fog does not merely kill; it completely erases the very remnants of your existence.", 175, 75, 255);
                CombatText.NewText(NPC.getRect(), new Color(175, 75, 255), "Erasing existence!", true);
            }
            else if (GlobalTimer == 900) {
                Main.NewText("<Moby Dick> Do you truly believe you possess the strength to defeat me, mere mortal?", 175, 75, 255);
                CombatText.NewText(NPC.getRect(), new Color(175, 75, 255), "Defeat me?", true);
            }
            else if (GlobalTimer == 1200) {
                Main.NewText("<Moby Dick> Or will your soul shatter... and all the world's memories of you be entirely devoured?", 175, 75, 255);
                CombatText.NewText(NPC.getRect(), new Color(175, 75, 255), "Memories devoured!", true);
            }
            else if (GlobalTimer == 1500) {
                Main.NewText("<Moby Dick> Let us test your resolve before your memories vanish into the hollow fog!", 175, 75, 255);
                CombatText.NewText(NPC.getRect(), new Color(255, 25, 25), "FEEL THIS HUNGER!", true);
            }

            if (GlobalTimer >= 1800) {
                State = BossState.Phase1;
                GlobalTimer = 0;
                AttackTimer = 0;
                NPC.dontTakeDamage = false;
                NPC.netUpdate = true;
            }
        }

        private void UpdatePhase1(Player target) {
            AttackTimer++;

            if (NPC.life < NPC.lifeMax * 0.5f) {
                State = BossState.Phase2_Transition;
                GlobalTimer = 0;
                AttackTimer = 0;
                AttackState = 0;
                return;
            }

            SpawnFog(target, 2, -100, 400);

            switch ((P1Attacks)AttackState) {
                case P1Attacks.Dash3X:
                    int cycleTimer = (int)AttackTimer % 90; 

                    if (cycleTimer >= 0 && cycleTimer <= 40) {
                        Vector2 readyPos = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY * -1) * 800f;
                        Vector2 approachVelocity = (readyPos - NPC.Center).SafeNormalize(Vector2.Zero) * 16f;
                        
                        NPC.velocity = Vector2.Lerp(NPC.velocity, approachVelocity, 0.1f);
                        dashTelegraphRotation = (target.Center - NPC.Center).ToRotation(); 
                    }
                    else if (cycleTimer == 41) {
                        if (dashCount < 3) {
                            NPC.velocity = dashTelegraphRotation.ToRotationVector2() * 48f; 
                            dashCount++;
                            SoundEngine.PlaySound(new SoundStyle("TheSanity/Music/WhaleDash"), NPC.position);
                        }
                    }
                    else if (cycleTimer >= 75) { 
                        NPC.velocity *= 0.75f; 
                    }

                    if (dashCount >= 3 && AttackTimer > 270) {
                        dashCount = 0;
                        AttackState = (float)P1Attacks.SequentialNebulaBlaze;
                        AttackTimer = 0;
                    }
                    break;

                case P1Attacks.SequentialNebulaBlaze:
                    Vector2 hoverPos = target.Center + new Vector2(NPC.direction * -350f, -200f);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (hoverPos - NPC.Center) * 0.08f, 0.1f);

                    if (AttackTimer == 30 || AttackTimer == 60 || AttackTimer == 90) {
                        SoundEngine.PlaySound(SoundID.Item9, NPC.position);
                        if (Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 shootVel = (target.Center - NPC.Center).SafeNormalize(Vector2.Zero) * 9.5f;
                            
                            Projectile.NewProjectile(
                                NPC.GetSource_FromAI(),
                                NPC.Center,
                                shootVel,
                                ModContent.ProjectileType<Projectiles.NebulaBlazeHostile>(),
                                31, 
                                0f,
                                Main.myPlayer
                            );
                        }
                    }

                    if (AttackTimer > 130) {
                        AttackState = (float)P1Attacks.LaserRotation180;
                        AttackTimer = 0;
                        laserIndex = -1; 
                    }
                    break;

                case P1Attacks.LaserRotation180:
                    Vector2 laserCenterPos = target.Center + new Vector2(0f, -300f);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, (laserCenterPos - NPC.Center) * 0.05f, 0.1f);
                    
                    Vector2 mouthPosition = NPC.Center + new Vector2(-NPC.direction * 90f, -10f) + new Vector2(NPC.direction * 105f, 30f);

                    if (AttackTimer < 40) {
                        dashTelegraphRotation = (target.Center - mouthPosition).ToRotation();
                    }
                    else if (AttackTimer >= 40 && AttackTimer <= 140) {
                        float angleToPlayer = (target.Center - mouthPosition).ToRotation();
                        dashTelegraphRotation = MathHelper.WrapAngle(dashTelegraphRotation).AngleLerp(angleToPlayer, 0.045f);

                        if (AttackTimer == 40) {
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                Vector2 beamVel = dashTelegraphRotation.ToRotationVector2() * 8.5f;
                                laserIndex = Projectile.NewProjectile(
                                    NPC.GetSource_FromAI(), 
                                    mouthPosition, 
                                    beamVel, 
                                    ModContent.ProjectileType<Projectiles.WhiteWhaleLaser>(), 
                                    100, 
                                    0f, 
                                    Main.myPlayer
                                );
                            }
                        }

                        if (laserIndex >= 0 && laserIndex < Main.maxProjectiles) {
                            Projectile laser = Main.projectile[laserIndex];
                            if (laser.active && laser.type == ModContent.ProjectileType<Projectiles.WhiteWhaleLaser>()) {
                                laser.Center = mouthPosition;
                                laser.velocity = dashTelegraphRotation.ToRotationVector2() * 8.5f;
                                laser.netUpdate = true;
                            }
                        }
                    }

                    if (AttackTimer > 140) {
                        if (laserIndex >= 0 && laserIndex < Main.maxProjectiles) {
                            if (Main.projectile[laserIndex].active && Main.projectile[laserIndex].type == ModContent.ProjectileType<Projectiles.WhiteWhaleLaser>()) {
                                Main.projectile[laserIndex].Kill();
                            }
                        }
                        AttackState = (float)P1Attacks.SlamPlayer2X;
                        AttackTimer = 0;
                        dashCount = 0;
                    }
                    break;

                case P1Attacks.SlamPlayer2X:
                    int slamCycle = (int)AttackTimer % 90;

                    if (slamCycle <= 45) {
                        Vector2 slamReadyPos = target.Center + new Vector2(0f, -550f); 
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (slamReadyPos - NPC.Center) * 0.25f, 0.2f);
                        dashTelegraphRotation = MathHelper.PiOver2; 
                    }
                    else if (slamCycle == 46) {
                        NPC.velocity = new Vector2(0f, 38f); 
                        SoundEngine.PlaySound(SoundID.Roar, NPC.position);
                    }
                    else if (slamCycle > 46 && slamCycle <= 80) {
                        if (slamCycle == 75) {
                            NPC.velocity *= 0.1f; 
                        }
                    }

                    if (slamCycle == 89) {
                        dashCount++;
                    }

                    if (dashCount >= 2 && AttackTimer > 175) {
                        dashCount = 0;
                        AttackState = (float)P1Attacks.Dash3X; 
                        AttackTimer = 0;
                    }
                    break;
            }
        }

        private void UpdatePhase2Transition(Player target) {
            AttackTimer++;
            NPC.velocity *= 0.9f; 
            NPC.dontTakeDamage = true; 

            if (AttackTimer == 1) {
                SoundEngine.PlaySound(SoundID.Roar, NPC.position);
            }

            SpawnFog(target, 5, -200, 500);

            if (AttackTimer >= 180) {
                State = BossState.Phase2_Active; 
                GlobalTimer = 0;
                AttackTimer = 0;
                AttackState = (float)P2Attacks.Dash_Letter_X; 
                NPC.dontTakeDamage = false; 
                NPC.netUpdate = true; 
            }
        }

        private void ChooseNextP2Attack() {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int nextAttack;
                int totalAttacks = Enum.GetValues(typeof(P2Attacks)).Length;
                do {
                    nextAttack = Main.rand.Next(0, totalAttacks);
                } while (nextAttack == (int)AttackState); 

                AttackState = (float)nextAttack;
            }
            NPC.velocity *= 0.2f; 
            AttackTimer = 0;
            NPC.netUpdate = true;
        }

        private void UpdatePhase2(Player target) {
            AttackTimer++;
            SpawnFog(target, 6, -300, 500);

            // LOGIKA SILUET INVISIBLE & INVINCIBLE PHASE 2
            bool isRepositioning = false;
            P2Attacks p2Attack = (P2Attacks)AttackState;
            
            // Phase saat bersiap membentuk huruf
            if (p2Attack >= P2Attacks.Dash_Letter_X && p2Attack <= P2Attacks.Dash_Letter_H && AttackTimer <= 50) {
                isRepositioning = true;
            }
            // Phase saat balik arah putaran (Triangle)
            else if (p2Attack == P2Attacks.RotatingLaserTriangle && AttackTimer > 180 && AttackTimer <= 210) {
                isRepositioning = true;
            }

            if (isRepositioning) {
                NPC.localAI[3] = 1f;       // Flag untuk menyalakan efek siluet
                NPC.dontTakeDamage = true; // Tak bisa diserang
                NPC.damage = 0;            // Tidak menabrak/damage player
            } else {
                NPC.localAI[3] = 0f;       // Matikan siluet
                NPC.dontTakeDamage = false;
                NPC.damage = 50; 
            }

            if (!spawnedClones) {
                spawnedClones = true;
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int clone1Index = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X - 500, (int)NPC.Center.Y - 100, ModContent.NPCType<WhiteWhaleClone>());
                    if (clone1Index >= 0 && clone1Index < Main.maxNPCs) {
                        if (Main.npc[clone1Index].ModNPC is WhiteWhaleClone clone1) {
                            clone1.ParentIndex = NPC.whoAmI;
                            clone1.CloneType = 1;
                        }
                        Main.npc[clone1Index].netUpdate = true;
                    }

                    int clone2Index = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X + 500, (int)NPC.Center.Y - 100, ModContent.NPCType<WhiteWhaleClone>());
                    if (clone2Index >= 0 && clone2Index < Main.maxNPCs) {
                        if (Main.npc[clone2Index].ModNPC is WhiteWhaleClone clone2) {
                            clone2.ParentIndex = NPC.whoAmI;
                            clone2.CloneType = 2;
                        }
                        Main.npc[clone2Index].netUpdate = true;
                    }
                }
            }

            switch ((P2Attacks)AttackState) {
                case P2Attacks.Dash_Letter_X:
                    if (AttackTimer <= 50) {
                        Vector2 readyPos = target.Center + new Vector2(-700f, -700f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        dashTelegraphRotation = (target.Center + new Vector2(700f, 700f) - NPC.Center).ToRotation();
                    }
                    else if (AttackTimer == 51) {
                        NPC.velocity = dashTelegraphRotation.ToRotationVector2() * 52f;
                        SoundEngine.PlaySound(new SoundStyle("TheSanity/Music/WhaleDash"), NPC.position);
                    }
                    else if (AttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }

                    if (AttackTimer > 95) {
                        ChooseNextP2Attack();
                    }
                    break;

                case P2Attacks.Dash_Letter_Y:
                    if (AttackTimer <= 50) {
                        Vector2 readyPos = target.Center + new Vector2(-600f, -600f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        dashTelegraphRotation = (target.Center - NPC.Center).ToRotation();
                    }
                    else if (AttackTimer == 51) {
                        NPC.velocity = dashTelegraphRotation.ToRotationVector2() * 48f;
                        SoundEngine.PlaySound(new SoundStyle("TheSanity/Music/WhaleDash"), NPC.position);
                    }
                    else if (AttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }

                    if (AttackTimer > 95) {
                        ChooseNextP2Attack();
                    }
                    break;

                case P2Attacks.Dash_Letter_A:
                    if (AttackTimer <= 50) {
                        Vector2 readyPos = target.Center + new Vector2(-600f, 100f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        dashTelegraphRotation = 0f; 
                    }
                    else if (AttackTimer == 51) {
                        NPC.velocity = Vector2.UnitX * 50f;
                        SoundEngine.PlaySound(new SoundStyle("TheSanity/Music/WhaleDash"), NPC.position);
                    }
                    else if (AttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }

                    if (AttackTimer > 95) {
                        ChooseNextP2Attack();
                    }
                    break;

                case P2Attacks.Dash_Letter_Z:
                    if (AttackTimer <= 50) {
                        Vector2 readyPos = target.Center + new Vector2(-600f, -400f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        dashTelegraphRotation = 0f; 
                    }
                    else if (AttackTimer == 51) {
                        NPC.velocity = Vector2.UnitX * 50f;
                        SoundEngine.PlaySound(new SoundStyle("TheSanity/Music/WhaleDash"), NPC.position);
                    }
                    else if (AttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }

                    if (AttackTimer > 95) {
                        ChooseNextP2Attack();
                    }
                    break;

                case P2Attacks.Dash_Letter_T: 
                    if (AttackTimer <= 50) {
                        Vector2 readyPos = target.Center + new Vector2(-600f, -400f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        dashTelegraphRotation = 0f; 
                    }
                    else if (AttackTimer == 51) {
                        NPC.velocity = Vector2.UnitX * 50f;
                        SoundEngine.PlaySound(new SoundStyle("TheSanity/Music/WhaleDash"), NPC.position);
                    }
                    else if (AttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }

                    if (AttackTimer > 95) {
                        ChooseNextP2Attack();
                    }
                    break;

                case P2Attacks.Dash_Letter_N: 
                    if (AttackTimer <= 50) {
                        Vector2 readyPos = target.Center + new Vector2(-400f, 500f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        dashTelegraphRotation = -MathHelper.PiOver2; 
                    }
                    else if (AttackTimer == 51) {
                        NPC.velocity = -Vector2.UnitY * 50f;
                        SoundEngine.PlaySound(new SoundStyle("TheSanity/Music/WhaleDash"), NPC.position);
                    }
                    else if (AttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }

                    if (AttackTimer > 95) {
                        ChooseNextP2Attack();
                    }
                    break;

                case P2Attacks.Dash_Letter_H: 
                    if (AttackTimer <= 50) {
                        Vector2 readyPos = target.Center + new Vector2(-400f, -500f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        dashTelegraphRotation = MathHelper.PiOver2; 
                    }
                    else if (AttackTimer == 51) {
                        NPC.velocity = Vector2.UnitY * 50f;
                        SoundEngine.PlaySound(new SoundStyle("TheSanity/Music/WhaleDash"), NPC.position);
                    }
                    else if (AttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }

                    if (AttackTimer > 95) {
                        ChooseNextP2Attack();
                    }
                    break;

                case P2Attacks.RotatingLaserTriangle:
                    if (AttackTimer == 1) {
                        rotatingCenter = target.Center; 
                    }

                    float radius = 500f; 
                    Vector2 mouthPosP2 = NPC.Center + new Vector2(-NPC.direction * 90f, -10f) + new Vector2(NPC.direction * 105f, 30f);
                    
                    if (AttackTimer >= 1 && AttackTimer <= 180) {
                        float angle = (AttackTimer - 1f) * (MathHelper.TwoPi / 120f); 
                        Vector2 targetOrbitPos = rotatingCenter + angle.ToRotationVector2() * radius; 
                        NPC.velocity = (targetOrbitPos - NPC.Center) * 0.25f;

                        if (AttackTimer > 40 && AttackTimer % 15 == 0) {
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                Vector2 laserVel = angle.ToRotationVector2() * 8f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), mouthPosP2, laserVel, ModContent.ProjectileType<Projectiles.WhiteWhaleLaser>(), 160, 0f, Main.myPlayer);
                            }
                        }
                    }
                    else if (AttackTimer > 180 && AttackTimer <= 210) {
                        NPC.velocity *= 0.8f;
                    }
                    else if (AttackTimer > 210 && AttackTimer <= 390) {
                        float angle = -(AttackTimer - 211f) * (MathHelper.TwoPi / 120f);
                        Vector2 targetOrbitPos = rotatingCenter + angle.ToRotationVector2() * radius;
                        NPC.velocity = (targetOrbitPos - NPC.Center) * 0.25f;

                        if (AttackTimer % 15 == 0) {
                            if (Main.netMode != NetmodeID.MultiplayerClient) {
                                Vector2 laserVel = angle.ToRotationVector2() * 8f;
                                Projectile.NewProjectile(NPC.GetSource_FromAI(), mouthPosP2, laserVel, ModContent.ProjectileType<Projectiles.WhiteWhaleLaser>(), 100, 0f, Main.myPlayer);
                            }
                        }
                    }

                    if (AttackTimer > 410) {
                        for (int i = 0; i < Main.maxProjectiles; i++) {
                            if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<Projectiles.WhiteWhaleLaser>()) {
                                Main.projectile[i].Kill();
                            }
                        }
                        rotatingCenter = Vector2.Zero; 
                        ChooseNextP2Attack();
                    }
                    break;

                case P2Attacks.PredictiveSequentialDash:
                    if (AttackTimer >= 1 && AttackTimer <= 45) {
                        Vector2 readyPos = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY * -1) * 800f;
                        Vector2 approachVelocity = (readyPos - NPC.Center).SafeNormalize(Vector2.Zero) * 18f;
                        NPC.velocity = Vector2.Lerp(NPC.velocity, approachVelocity, 0.1f);

                        Vector2 predictedPos = target.Center + target.velocity * 25f;
                        dashTelegraphRotation = (predictedPos - NPC.Center).ToRotation();
                    }
                    else if (AttackTimer == 46) {
                        NPC.velocity = dashTelegraphRotation.ToRotationVector2() * 52f; 
                        SoundEngine.PlaySound(new SoundStyle("TheSanity/Music/WhaleDash"), NPC.position);
                    }
                    else if (AttackTimer >= 71 && AttackTimer <= 80) {
                        NPC.velocity *= 0.75f; 
                    }
                    
                    if (AttackTimer > 240) {
                        ChooseNextP2Attack();
                    }
                    break;
            }
        }

        private void SpawnFog(Player target, int intensity, int minY, int maxY) {
            Vector2 fogCenter = (State == BossState.SpawnAnimation) ? NPC.Center : target.Center;

            for (int i = 0; i < intensity; i++) {
                Vector2 spawnPos = fogCenter + new Vector2(Main.rand.Next(-1400, 1400), Main.rand.Next(minY, maxY));
                
                if (Main.netMode != NetmodeID.Server) {
                    int graveyardCloudID = Main.rand.Next(1087, 1094); 
                    Gore fogGore = Gore.NewGoreDirect(NPC.GetSource_FromAI(), spawnPos, Vector2.Zero, graveyardCloudID, Main.rand.NextFloat(0.9f, 1.3f));
                    fogGore.velocity *= 0.1f; 
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Vector2 origin = NPC.frame.Size() / 2f;
            
            float visualOffsetY = -10f; 
            float visualOffsetX = NPC.spriteDirection * 90f; 
            Vector2 visualOffset = new Vector2(visualOffsetX, visualOffsetY);

            if (Main.netMode != NetmodeID.Server) {
                Texture2D blankTexture = TextureAssets.MagicPixel.Value;
                int vignetteThickness = 250; 
                
                for (int i = 0; i < vignetteThickness; i += 5) {
                    float opacity = (1f - (i / (float)vignetteThickness)) * 0.8f; 
                    Color edgeColor = Color.Black * opacity;

                    spriteBatch.Draw(blankTexture, new Rectangle((int)screenPos.X, (int)screenPos.Y + i, Main.screenWidth, 5), edgeColor);
                    spriteBatch.Draw(blankTexture, new Rectangle((int)screenPos.X, (int)screenPos.Y + Main.screenHeight - i - 5, Main.screenWidth, 5), edgeColor);
                    spriteBatch.Draw(blankTexture, new Rectangle((int)screenPos.X + i, (int)screenPos.Y, 5, Main.screenHeight), edgeColor);
                    spriteBatch.Draw(blankTexture, new Rectangle((int)screenPos.X + Main.screenWidth - i - 5, (int)screenPos.Y, 5, Main.screenHeight), edgeColor);
                }
            }

            if (State == BossState.SpawnAnimation && GlobalTimer < 1800) { 
                Texture2D texture = TextureAssets.Npc[NPC.type].Value;
                Vector2 drawPos = NPC.Center - screenPos + visualOffset;
                SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
                spriteBatch.Draw(texture, drawPos, NPC.frame, Color.Black * 0.9f, NPC.rotation, origin, 1.0f, effects, 0f);
                return false; 
            }

            Texture2D magicPixel = TextureAssets.MagicPixel.Value;

            if (State == BossState.Phase1) {
                if ((P1Attacks)AttackState == P1Attacks.Dash3X && dashCount < 3) {
                    float cycleTimer = AttackTimer % 90;
                    if (cycleTimer >= 0 && cycleTimer <= 40) {
                        float opacity = cycleTimer / 40f; 
                        spriteBatch.Draw(magicPixel, NPC.Center - screenPos, new Rectangle(0, 0, 1, 1), Color.Red * opacity * 0.6f, dashTelegraphRotation, new Vector2(0, 0.5f), new Vector2(2400f, 90f), SpriteEffects.None, 0f);
                    }
                }
                else if ((P1Attacks)AttackState == P1Attacks.SlamPlayer2X) {
                    float cycleTimer = AttackTimer % 90;
                    if (cycleTimer <= 45) {
                        float opacity = cycleTimer / 45f;
                        spriteBatch.Draw(magicPixel, NPC.Center - screenPos, new Rectangle(0, 0, 1, 1), Color.Red * opacity * 0.7f, MathHelper.PiOver2, new Vector2(0, 0.5f), new Vector2(2400f, 130f), SpriteEffects.None, 0f);
                    }
                }
            }
            else if (State == BossState.Phase2_Active) {
                if ((P2Attacks)AttackState <= P2Attacks.Dash_Letter_H && AttackTimer <= 50) {
                    float opacity = AttackTimer / 50f;
                    spriteBatch.Draw(magicPixel, NPC.Center - screenPos, new Rectangle(0, 0, 1, 1), Color.Red * opacity * 0.6f, dashTelegraphRotation, new Vector2(0, 0.5f), new Vector2(2400f, 80f), SpriteEffects.None, 0f);
                }
                else if ((P2Attacks)AttackState == P2Attacks.PredictiveSequentialDash && AttackTimer >= 1 && AttackTimer <= 45) {
                    float opacity = AttackTimer / 45f;
                    spriteBatch.Draw(magicPixel, NPC.Center - screenPos, new Rectangle(0, 0, 1, 1), Color.Orange * opacity * 0.7f, dashTelegraphRotation, new Vector2(0, 0.5f), new Vector2(2400f, 80f), SpriteEffects.None, 0f);
                }
            }

            Texture2D mainTexture = TextureAssets.Npc[NPC.type].Value;
            SpriteEffects mainEffects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            // Ubah warna menjadi hitam agak transparan jika flag siluet menyala
            Color alphaColor = (NPC.localAI[3] == 1f) ? Color.Black * 0.7f : (drawColor * NPC.Opacity); 

            for (int i = 1; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;

                Color shadowColor = alphaColor * ((NPC.oldPos.Length - i) / (float)NPC.oldPos.Length) * 0.35f;
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos + visualOffset;
                float oldRot = NPC.oldRot[i];

                spriteBatch.Draw(mainTexture, drawPos, NPC.frame, shadowColor, oldRot, origin, 1.0f, mainEffects, 0f);
            }

            Vector2 mainDrawPos = NPC.Center - screenPos + visualOffset;
            spriteBatch.Draw(mainTexture, mainDrawPos, NPC.frame, alphaColor, NPC.rotation, origin, 1.0f, mainEffects, 0f);

            return false; 
        }
    }
}