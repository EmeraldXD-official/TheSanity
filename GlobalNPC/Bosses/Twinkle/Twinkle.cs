using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules; // Ditambahkan untuk sistem drop
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;
using TheSanity.Items.Placeable;

namespace TheSanity.GlobalNPC.Bosses.Twinkle
{
    [AutoloadBossHead]
    public class Twinkle : ModNPC
    {
        private enum States
        {
            SpawnAnimation,
            Cooldown, 
            Attack_Dash,
            Attack_StarCircle,
            Attack_Pentagram,
            Attack_HomingVolley,
            Attack_LockDash,
            Phase2_ThreeShot,
            Phase2_ChasingStorm,
            Attack_ShotgunStars,
            LastStand_Suck,
            LastStand_Burst,
            LastStand_Finale
        }

        private States CurrentState { get; set; } = States.SpawnAnimation;

        private int globalTimer = 0;
        private int attackTimer = 0;
        private int suckedCount = 0;
        private List<Vector2> pentagramPoints = new List<Vector2>();
        private int pentagramIndex = 0;
        
        private Vector2 lastSpawnPosition = Vector2.Zero;
        private int lockDashCount = 0;
        private int lockDashMax = 0;
        private int chaseStormCount = 0;
        private int chaseStormMax = 0;

        private States nextAttackState;
        private int cooldownDuration = 0;

        private int shotgunLoopCount = 0;
        private int shotgunLoopMax = 0;
        private int tpCount = 0;
        private int tpMax = 0;
        private int tpTimer = 0;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Twinkle/TwinkleStars";

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = 1;
            NPCID.Sets.TrailCacheLength[NPC.type] = 8;
            NPCID.Sets.TrailingMode[NPC.type] = 3;
        }

        public override void SetDefaults()
        {
            NPC.width = 64;
            NPC.height = 64;
            
            // 🪙 BALANCE PRE-HARDMODE (Master mode otomatis mengalikan x3 menjadi 72 Contact Damage)
            NPC.damage = 24; 
            NPC.defense = 22;
            NPC.lifeMax = 5273; 
            
            NPC.HitSound = SoundID.NPCHit5;
            NPC.DeathSound = SoundID.NPCDeath55;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.boss = true;
            NPC.lavaImmune = true;
            NPC.aiStyle = -1;

            // 🪙 INTEGRASI MUSIK CUSTOM KAMU
            Music = MusicLoader.GetMusicSlot(Mod, "Music/TwinkleTwinkleTheme"); 
        }

        // =================================================================
        // SISTEM DROP LOOT (Treasure Bag & Relic)
        // =================================================================
        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            // Drop Treasure Bag secara adil per orang di dunia Expert / Master Mode
            npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<TwinkleBag>()));

            // Drop trofi pajangan Relic emas khusus saat dikalahkan di Master Mode
            npcLoot.Add(ItemDropRule.MasterModeDropOnAllPlayers(ModContent.ItemType<TwinkleRelic>()));
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            Player player = Main.player[NPC.target];

            if (player.dead || !player.active)
            {
                NPC.velocity.Y -= 0.5f; 
                NPC.rotation += 0.05f; // Tetap berputar sedikit saat pergi
                NPC.EncourageDespawn(10);
                return;
            }

            if (Main.dayTime)
            {
                for (int i = 0; i < 50; i++)
                {
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GemTopaz, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f));
                }
                NPC.active = false;
                return;
            }

            globalTimer++;
            attackTimer++;

            float hpRatio = (float)NPC.life / NPC.lifeMax;
            bool isPhase2 = hpRatio <= 0.50f && hpRatio > 0.10f;
            
            if (hpRatio <= 0.10f && CurrentState < States.LastStand_Suck)
            {
                CurrentState = States.LastStand_Suck;
                attackTimer = 0;
            }

            switch (CurrentState)
            {
                case States.SpawnAnimation:
                    ExecuteSpawnAnimation(player);
                    break;

                case States.Cooldown:
                    ExecuteCooldown(player);
                    break;

                case States.Attack_Dash:
                    ExecutePhase1Dash(player, isPhase2 ? 1.8f : 1.0f, isPhase2);
                    break;

                case States.Attack_StarCircle:
                    ExecuteStarCircle(player, isPhase2);
                    break;

                case States.Attack_Pentagram:
                    ExecutePentagram(player, isPhase2 ? 40f : 22f, isPhase2);
                    break;

                case States.Attack_HomingVolley:
                    ExecuteHomingVolley(player, isPhase2);
                    break;

                case States.Attack_LockDash:
                    ExecuteLockDash(player, isPhase2);
                    break;

                case States.Phase2_ThreeShot:
                    ExecuteThreeShot(player);
                    break;

                case States.Phase2_ChasingStorm:
                    ExecuteChasingStorm(player);
                    break;

                case States.Attack_ShotgunStars:
                    ExecuteShotgunStars(player, isPhase2);
                    break;

                case States.LastStand_Suck:
                    ExecuteLastStandSuck(player);
                    break;

                case States.LastStand_Burst:
                    ExecuteLastStandBurst();
                    break;

                case States.LastStand_Finale:
                    ExecuteLastStandFinale(player);
                    break;
            }
        }

        private void ChooseNextAttack(bool phase2)
        {
            attackTimer = 0;
            NPC.netUpdate = true;

            lockDashCount = 0;
            chaseStormCount = 0;

            List<States> attackPool = new List<States>
            {
                States.Attack_Dash,
                States.Attack_StarCircle,
                States.Attack_Pentagram,
                States.Attack_HomingVolley,
                States.Attack_ShotgunStars 
            };

            if (phase2)
            {
                attackPool.Add(States.Phase2_ThreeShot);
                attackPool.Add(States.Phase2_ChasingStorm); 
            }
            else
            {
                attackPool.Add(States.Attack_LockDash); 
            }

            States chosen;
            int safetyCounter = 0;
            do
            {
                chosen = attackPool[Main.rand.Next(attackPool.Count)];
                safetyCounter++;
            } while (chosen == nextAttackState && safetyCounter < 10);

            nextAttackState = chosen;
            cooldownDuration = Main.rand.Next(60, 121); // Jeda 1-2 detik
            CurrentState = States.Cooldown;
        }

        private void ExecuteCooldown(Player player)
        {
            NPC.velocity.X *= 0.86f; 
            
            // MODIFIKASI: Idle animation bergerak naik-turun secara halus menggunakan rumus Sinus
            NPC.velocity.Y = MathF.Sin(globalTimer * 0.09f) * 1.2f;

            // MODIFIKASI: Tidak lagi mengunci pandangan ke player, melainkan berputar santai saat diam
            NPC.rotation += 0.015f;

            if (attackTimer >= cooldownDuration)
            {
                CurrentState = nextAttackState;
                attackTimer = 0;
                NPC.netUpdate = true;
            }
        }

        private void ResetPentagramPoints(Vector2 center, bool phase2)
        {
            pentagramPoints.Clear();

            if (!phase2)
            {
                float r = 350f;
                pentagramPoints.Add(center - new Vector2(0, r));
                pentagramPoints.Add(center + new Vector2(r * 0.85f, r * 0.5f));
                pentagramPoints.Add(center + new Vector2(-r * 0.75f, -r * 0.25f));
                pentagramPoints.Add(center + new Vector2(r * 0.75f, -r * 0.25f));
                pentagramPoints.Add(center + new Vector2(-r * 0.85f, r * 0.5f));
            }
            else
            {
                float[] radii = { 160f, 320f, 480f };
                foreach (float r in radii)
                {
                    pentagramPoints.Add(center - new Vector2(0, r));
                    pentagramPoints.Add(center + new Vector2(r * 0.85f, r * 0.5f));
                    pentagramPoints.Add(center + new Vector2(-r * 0.75f, -r * 0.25f));
                    pentagramPoints.Add(center + new Vector2(r * 0.75f, -r * 0.25f));
                    pentagramPoints.Add(center + new Vector2(-r * 0.85f, r * 0.5f));
                }
            }
        }

        #region CORE ATTACKS SYSTEM
        private void ExecuteSpawnAnimation(Player player)
        {
            NPC.dontTakeDamage = true;
            if (globalTimer == 1)
            {
                NPC.Center = player.Center - new Vector2(0, 480);
                NPC.velocity = Vector2.Zero;
                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
            }

            // Muka menghadap ke bawah menatap player saat animasi spawn awal
            NPC.rotation = (player.Center - NPC.Center).ToRotation() + MathHelper.PiOver2;

            if (globalTimer % 6 == 0) 
            {
                Vector2 spawnPos = player.Center + new Vector2(Main.rand.NextFloat(200, 800), Main.rand.NextFloat(-800, -600));
                Vector2 velocity = new Vector2(-8f, 12f);

                // 🪙 PERBAIKAN: Menggunakan custom projectile milikmu sendiri
                int proj = Terraria.Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, velocity, ModContent.ProjectileType<TwinkleHostileStar>(), NPC.damage / 3, 1f, Main.myPlayer);
                if (proj != Main.maxProjectiles)
                {
                    Main.projectile[proj].hostile = true;
                    Main.projectile[proj].friendly = false;
                    Main.projectile[proj].tileCollide = false;
                    Main.projectile[proj].GetGlobalProjectile<TwinkleGlobalProj>().IsSpawnAnimStar = true;
                }
            }

            if (globalTimer >= 300) 
            {
                NPC.dontTakeDamage = false;
                ChooseNextAttack(false);
            }
        }

        private void ExecutePhase1Dash(Player player, float speedMult, bool phase2)
        {
            if (attackTimer == 1)
            {
                Vector2 moveDirection = player.Center - NPC.Center;
                moveDirection.Normalize();
                NPC.velocity = moveDirection * (14f * speedMult);
                SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
            }

            // Muka menghadap penuh ke arah jalur lesatan dash-nya
            if (NPC.velocity != Vector2.Zero)
            {
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;
            }

            if (attackTimer >= 45)
            {
                NPC.velocity *= 0.9f;
                if (attackTimer >= 60)
                {
                    ChooseNextAttack(phase2);
                }
            }
        }

        private void ExecuteStarCircle(Player player, bool phase2)
        {
            if (attackTimer == 1) lastSpawnPosition = NPC.Center;

            float radius = 450f;
            float speed = phase2 ? 0.05f : 0.025f; 
            float currentAngle = globalTimer * speed;
            Vector2 desirePos = player.Center + new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * radius;
            NPC.Center = Vector2.Lerp(NPC.Center, desirePos, phase2 ? 0.2f : 0.1f);

            // MODIFIKASI: Boss berputar-putar (muter-muter) kencang alih-alih selalu mengunci arah pandang ke player
            NPC.rotation += 0.16f;

            float distanceThreshold = phase2 ? 35f : 50f; 
            if (Vector2.Distance(NPC.Center, lastSpawnPosition) >= distanceThreshold)
            {
                Vector2 projVel = player.Center - NPC.Center;
                projVel.Normalize();
                projVel *= phase2 ? 12f : 7.5f; 

                // Balanced damage peluru agar aman di Master mode
                Terraria.Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, projVel, ModContent.ProjectileType<TwinkleHostileStar>(), (int)(NPC.damage * 0.5f), 0f, Main.myPlayer);
                SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                lastSpawnPosition = NPC.Center;
            }

            if (attackTimer >= 240)
            {
                ChooseNextAttack(phase2);
            }
        }

        private void ExecutePentagram(Player player, float speed, bool phase2)
        {
            if (attackTimer == 1)
            {
                pentagramIndex = 0;
                ResetPentagramPoints(player.Center, phase2);
                NPC.Center = pentagramPoints[0];
                SoundEngine.PlaySound(SoundID.Item117, NPC.Center); 
            }

            if (pentagramIndex < pentagramPoints.Count)
            {
                Vector2 target = pentagramPoints[pentagramIndex];
                Vector2 move = target - NPC.Center;
                float dist = move.Length();
                
                if (dist > 25f)
                {
                    move.Normalize();
                    NPC.velocity = move * speed;
                    // Muka menghadap ke arah titik sudut pentagram yang dituju saat ini
                    NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;
                }
                else
                {
                    int spawnStarsCount = 1;
                    if (phase2)
                    {
                        if (pentagramIndex >= 5 && pentagramIndex < 10) spawnStarsCount = 2; 
                        else if (pentagramIndex >= 10) spawnStarsCount = 3;                  
                    }

                    for (int i = 0; i < spawnStarsCount; i++)
                    {
                        Terraria.Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<TwinkleHostileStar>(), (int)(NPC.damage * 0.5f), 0f, Main.myPlayer, -2);
                    }
                    
                    SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                    pentagramIndex++;
                }
            }
            else
            {
                Vector2 move = pentagramPoints[0] - NPC.Center;
                if (move.Length() > 25f)
                {
                    move.Normalize();
                    NPC.velocity = move * speed;
                    NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;
                }
                else
                {
                    NPC.velocity = Vector2.Zero;
                    NPC.rotation = (player.Center - NPC.Center).ToRotation() + MathHelper.PiOver2; // Menatap player saat peluncuran ritual
                    TwinkleGlobalProj.TriggerPentagramLaunch(player.Center);
                    SoundEngine.PlaySound(SoundID.Item105, NPC.Center); 
                    ChooseNextAttack(phase2);
                }
            }
        }

        private void ExecuteHomingVolley(Player player, bool phase2)
        {
            NPC.velocity *= 0.85f; 
            
            // Muka atas mengunci penuh arah pandang player selagi memberondong peluru homing tunggal
            NPC.rotation = (player.Center - NPC.Center).ToRotation() + MathHelper.PiOver2;

            int fireRate = 7; 
            int duration = phase2 ? 140 : 90; 

            if (attackTimer >= 20 && attackTimer <= duration && attackTimer % fireRate == 0)
            {
                Vector2 baseDir = player.Center - NPC.Center;
                baseDir.Normalize();
                baseDir *= phase2 ? 11f : 7.5f;
                baseDir = baseDir.RotatedByRandom(MathHelper.ToRadians(6)); 

                // Balanced damage peluru homing tunggal
                Terraria.Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, baseDir, ModContent.ProjectileType<TwinkleCustomHomingStar>(), (int)(NPC.damage * 0.45f), 0f, Main.myPlayer);
                SoundEngine.PlaySound(SoundID.Item9, NPC.Center); 
            }

            if (attackTimer >= duration + 30)
            {
                ChooseNextAttack(phase2);
            }
        }

        private void ExecuteLockDash(Player player, bool phase2)
        {
            if (attackTimer == 1 && lockDashCount == 0)
            {
                lockDashMax = Main.rand.Next(3, 6); 
            }

            if (attackTimer < 35)
            {
                Vector2 lockMove = player.Center - NPC.Center;
                lockMove.Normalize();
                NPC.velocity = Vector2.Lerp(NPC.velocity, lockMove * 5f, 0.07f);
                
                // Fase bidik: Muka atas menatap tajam ke posisi Player sebelum melesat
                NPC.rotation = lockMove.ToRotation() + MathHelper.PiOver2;
            }
            else if (attackTimer == 35)
            {
                Vector2 dashVel = player.Center - NPC.Center;
                dashVel.Normalize();
                NPC.velocity = dashVel * 18f;
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2; // Muka searah dash

                float baseRotation = dashVel.ToRotation();
                int starsCount = 3;
                float spread = MathHelper.ToRadians(20);

                for (int i = 0; i < starsCount; i++)
                {
                    float angle = baseRotation + MathHelper.Lerp(-spread, spread, (float)i / (starsCount - 1));
                    Vector2 starVel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 6.5f;
                    Terraria.Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, starVel, ModContent.ProjectileType<TwinkleHostileStar>(), (int)(NPC.damage * 0.5f), 0f, Main.myPlayer);
                }
                SoundEngine.PlaySound(SoundID.Item74, NPC.Center);
            }
            else if (attackTimer > 35 && attackTimer < 70)
            {
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;
            }

            if (attackTimer >= 70)
            {
                NPC.velocity *= 0.85f;
                if (attackTimer >= 85)
                {
                    lockDashCount++;
                    if (lockDashCount < lockDashMax)
                    {
                        attackTimer = 1; 
                        NPC.netUpdate = true;
                    }
                    else
                    {
                        ChooseNextAttack(phase2);
                    }
                }
            }
        }

        private void ExecuteThreeShot(Player player)
        {
            NPC.velocity = Vector2.Zero; 
            
            // Muka atas mengunci arah player saat melakukan triple shoot lurus
            NPC.rotation = (player.Center - NPC.Center).ToRotation() + MathHelper.PiOver2;

            if (attackTimer % 14 == 0)
            {
                Vector2 shootDir = player.Center - NPC.Center;
                shootDir.Normalize();

                // Balanced damage peluru lurus
                int projDmg = (int)(NPC.damage * 0.5f);

                Terraria.Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, shootDir * 11f, ModContent.ProjectileType<TwinkleHostileStar>(), projDmg, 0f, Main.myPlayer);

                Vector2 leftWingVel = shootDir.RotatedBy(MathHelper.ToRadians(-35)) * 8.5f;
                Terraria.Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, leftWingVel, ModContent.ProjectileType<TwinkleHostileStar>(), projDmg, 0f, Main.myPlayer);

                Vector2 rightWingVel = shootDir.RotatedBy(MathHelper.ToRadians(35)) * 8.5f;
                Terraria.Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, rightWingVel, ModContent.ProjectileType<TwinkleHostileStar>(), projDmg, 0f, Main.myPlayer);

                SoundEngine.PlaySound(SoundID.Item72, NPC.Center);
            }

            if (attackTimer >= 240) 
            {
                ChooseNextAttack(true);
            }
        }

        private void ExecuteChasingStorm(Player player)
        {
            if (attackTimer == 1 && chaseStormCount == 0)
            {
                chaseStormMax = Main.rand.Next(3, 6); 
            }

            if (attackTimer < 35)
            {
                Vector2 lockMove = player.Center - NPC.Center;
                lockMove.Normalize();
                NPC.velocity = Vector2.Lerp(NPC.velocity, lockMove * 7f, 0.08f);
                
                // Fase bidik badai: Muka menatap player
                NPC.rotation = lockMove.ToRotation() + MathHelper.PiOver2;
            }
            else if (attackTimer == 35)
            {
                Vector2 dashVel = player.Center - NPC.Center;
                dashVel.Normalize();
                NPC.velocity = dashVel * 24f; 
                lastSpawnPosition = NPC.Center;
                SoundEngine.PlaySound(SoundID.Item119, NPC.Center); 
            }
            
            // Menjaga arah muka lurus sesuai arah lintasan dash badai bintangnya
            if (attackTimer >= 35 && attackTimer < 75)
            {
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;

                if (Vector2.Distance(NPC.Center, lastSpawnPosition) >= 80f)
                {
                    float angleStep = MathHelper.TwoPi / 5f;
                    for (int i = 0; i < 5; i++)
                    {
                        float currentAngle = i * angleStep;
                        Vector2 starVel = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * 4f;
                        
                        Terraria.Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, starVel, ModContent.ProjectileType<TwinkleCustomHomingStar>(), (int)(NPC.damage * 0.45f), 0f, Main.myPlayer);
                    }
                    SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
                    lastSpawnPosition = NPC.Center;
                }
            }

            if (attackTimer >= 75)
            {
                NPC.velocity *= 0.85f;
                if (attackTimer >= 90)
                {
                    chaseStormCount++;
                    if (chaseStormCount < chaseStormMax)
                    {
                        attackTimer = 1; 
                        NPC.netUpdate = true;
                    }
                    else
                    {
                        ChooseNextAttack(true);
                    }
                }
            }
        }

        private void ExecuteShotgunStars(Player player, bool phase2)
        {
            if (attackTimer == 1)
            {
                shotgunLoopCount = 0;
                shotgunLoopMax = Main.rand.Next(3, 6); 
                tpCount = 0;
                tpMax = Main.rand.Next(5, 8);         
                tpTimer = 0;
                NPC.velocity = Vector2.Zero;
            }

            tpTimer++;

            if (tpCount < tpMax)
            {
                NPC.velocity = Vector2.Zero;
                
                // Muka selalu mengunci arah player saat melakukan rangkaian teleportasi berantai
                NPC.rotation = (player.Center - NPC.Center).ToRotation() + MathHelper.PiOver2;

                if (tpTimer >= 14) 
                {
                    for (int i = 0; i < 15; i++)
                    {
                        Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GemTopaz, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));
                    }

                    float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                    float distance = Main.rand.NextFloat(260f, 420f);
                    NPC.Center = player.Center + new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * distance;
                    
                    for (int i = 0; i < 15; i++)
                    {
                        Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f));
                    }

                    SoundEngine.PlaySound(SoundID.Item8, NPC.Center); 
                    tpCount++;
                    tpTimer = 0;
                    NPC.netUpdate = true;
                }
            }
            else
            {
                NPC.velocity *= 0.8f;
                NPC.rotation = (player.Center - NPC.Center).ToRotation() + MathHelper.PiOver2; // Menatap player sebelum menembak

                if (tpTimer == 18) 
                {
                    int totalStars = phase2 ? 10 : 5; 
                    float spread = MathHelper.ToRadians(phase2 ? 50 : 30);

                    Vector2 targetDir = player.Center - NPC.Center;
                    targetDir.Normalize();
                    targetDir *= phase2 ? 12f : 9f;

                    for (int i = 0; i < totalStars; i++)
                    {
                        float angleOffset = MathHelper.Lerp(-spread / 2f, spread / 2f, (float)i / (totalStars - 1));
                        Vector2 finalVel = targetDir.RotatedBy(angleOffset);

                        // Balanced damage peluru shotgun stars
                        Terraria.Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, finalVel, ModContent.ProjectileType<TwinkleHostileStar>(), (int)(NPC.damage * 0.5f), 0f, Main.myPlayer);
                    }
                    SoundEngine.PlaySound(SoundID.Item38, NPC.Center); 
                }

                if (tpTimer >= 48)
                {
                    shotgunLoopCount++;
                    if (shotgunLoopCount < shotgunLoopMax)
                    {
                        tpCount = 0;
                        tpMax = Main.rand.Next(5, 8);
                        tpTimer = 0;
                        NPC.netUpdate = true;
                    }
                    else
                    {
                        ChooseNextAttack(phase2);
                    }
                }
            }
        }

        private void ExecuteLastStandSuck(Player player)
        {
            NPC.dontTakeDamage = true;
            NPC.velocity = Vector2.Zero;

            if (attackTimer == 1)
            {
                NPC.Center = player.Center - new Vector2(0, 480);
            }

            // Tetap menghadap ke arah player di bawahnya saat menyedot energi bintang
            NPC.rotation = (player.Center - NPC.Center).ToRotation() + MathHelper.PiOver2;

            if (attackTimer % 2 == 0 && suckedCount < 200)
            {
                int[] types = { ProjectileID.ManaCloakStar, ProjectileID.BeeCloakStar, ProjectileID.StarVeilStar, ProjectileID.StarCloakStar, ProjectileID.SuperStar, ProjectileID.FallingStar };
                int chosenType = Main.rand.Next(types);

                float randomAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                Vector2 spawnLoc = player.Center + new Vector2((float)Math.Cos(randomAngle), (float)Math.Sin(randomAngle)) * 1200f;
                
                Vector2 velocityToBos = NPC.Center - spawnLoc;
                velocityToBos.Normalize();
                velocityToBos *= 15f;

                int p = Terraria.Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnLoc, velocityToBos, chosenType, 0, 0f, Main.myPlayer);
                if (p != Main.maxProjectiles) Main.projectile[p].tileCollide = false;
            }

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.friendly && p.getRect().Intersects(NPC.getRect()))
                {
                    p.Kill();
                    suckedCount++;
                    SoundEngine.PlaySound(SoundID.MaxMana, NPC.Center);
                }
            }

            if (suckedCount >= 200 || attackTimer >= 400)
            {
                CurrentState = States.LastStand_Burst;
                attackTimer = 0;
            }
        }

        private void ExecuteLastStandBurst()
        {
            float step = MathHelper.TwoPi / 400f;
            for (int i = 0; i < 400; i++)
            {
                float angle = i * step;
                Vector2 vel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * Main.rand.NextFloat(5f, 14f);
                Terraria.Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<TwinkleRainbowStar>(), (int)(NPC.damage * 0.6f), 0f, Main.myPlayer);
            }
            SoundEngine.PlaySound(SoundID.Item14, NPC.Center); 

            CurrentState = States.LastStand_Finale;
            attackTimer = 0;
        }

        private void ExecuteLastStandFinale(Player player)
        {
            NPC.velocity = Vector2.Zero;

            if (Main.rand.NextBool(2))
            {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GemTopaz, Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-6f, 6f), 100, default, 1.5f);
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.GoldFlame, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default, 2f);
            }

            if (NPC.life > 1)
            {
                NPC.life -= 8;
                if (NPC.life < 1) NPC.life = 1;
            }

            if (attackTimer < 240)
            {
                // Putaran kekacauan ritual sebelum dash terakhir pecah
                NPC.rotation += 0.05f * attackTimer; 
            }
            else
            {
                NPC.dontTakeDamage = false;
                
                // 🪙 SANGAT SAKIT: Melakukan Overwrite damage bos menjadi sangat fatal! (350 x 3 = 1050 Damage di Master Mode)
                NPC.damage = 350; 
                
                Vector2 fatalMove = player.Center - NPC.Center;
                fatalMove.Normalize();
                NPC.velocity = fatalMove * 35f;

                // Muka atas menatap lurus menghujam ke arah rute dash kematian fatal player
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;

                if (attackTimer >= 300) attackTimer = 235;
            }
        }
        #endregion

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Texture2D glowMask = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Twinkle/TwinkleStarsGlow").Value;
            
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            // 1. Menggambar Jejak Bayangan Kuning Bawaan (Trail Cache)
            for (int i = 0; i < NPC.oldPos.Length; i++)
            {
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size * 0.5f - screenPos;
                Color trailColor = Color.Yellow * ((NPC.oldPos.Length - i) / (float)NPC.oldPos.Length) * 0.6f;
                spriteBatch.Draw(texture, drawPos, null, trailColor, NPC.rotation, drawOrigin, NPC.scale, SpriteEffects.None, 0f);
            }

            Vector2 mainPos = NPC.Center - screenPos;

            // MODIFIKASI: Menambahkan mekanik perputaran bayangan orbit Merah & Biru (Kiri ke Kanan)
            float speedPutaran = Main.GlobalTimeWrappedHourly * 4.5f; // Mengatur kecepatan orbit
            float radiusOffset = 15f; // Jarak jangkauan keluar bayangan dari pusat boss

            // Rumus posisi sirkular melingkar (Merah di sisi kanan awal, Biru di sisi kiri berlawanan 180 derajat / Pi)
            Vector2 offsetMerah = new Vector2(MathF.Cos(speedPutaran), MathF.Sin(speedPutaran)) * radiusOffset;
            Vector2 offsetBiru = new Vector2(MathF.Cos(speedPutaran + MathHelper.Pi), MathF.Sin(speedPutaran + MathHelper.Pi)) * radiusOffset;

            // Gambar bayangan Merah (Transparan 40%)
            spriteBatch.Draw(texture, mainPos + offsetMerah, null, Color.Red * 0.4f, NPC.rotation, drawOrigin, NPC.scale, SpriteEffects.None, 0f);
            
            // Gambar bayangan Biru (Transparan 40%)
            spriteBatch.Draw(texture, mainPos + offsetBiru, null, Color.Blue * 0.4f, NPC.rotation, drawOrigin, NPC.scale, SpriteEffects.None, 0f);


            // 2. Menggambar Sprite Utama Bos
            spriteBatch.Draw(texture, mainPos, null, drawColor, NPC.rotation, drawOrigin, NPC.scale, SpriteEffects.None, 0f);

            // 3. Menggambar Efek Glow Mask Menyala Kuning
            Color glowColor = Color.Yellow * 1.5f;
            spriteBatch.Draw(glowMask, mainPos, null, glowColor, NPC.rotation, drawOrigin, NPC.scale, SpriteEffects.None, 0f);

            return false;
        }
    }

    public class TwinkleCustomHomingStar : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false; 
            Projectile.penetrate = -1;
            Projectile.timeLeft = 260;
        }

        public override void AI()
        {
            Projectile.rotation += 0.25f;
            Lighting.AddLight(Projectile.Center, 0.5f, 0.8f, 1f); 

            Player player = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            if (player != null && player.active && !player.dead)
            {
                Vector2 targetVelocity = player.Center - Projectile.Center;
                targetVelocity.Normalize();
                targetVelocity *= 10.5f; 

                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetVelocity, 0.045f);
            }

            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GemTopaz, 0f, 0f, 100, default, 1f);
                dust.noGravity = true;
                dust.velocity *= 0.2f;
            }
        }
    }
}