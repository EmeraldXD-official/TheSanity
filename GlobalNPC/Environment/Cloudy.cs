using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.Audio;
using Terraria.DataStructures;

namespace TheSanity.NPCs
{
    // =========================================================================
    // 1. KODE UTAMA ENTIAS SPACE RAIN CLOUD NPC (SMART TARGETING - 20 BLOCKS RADIUS)
    // =========================================================================
    public class SpaceRainCloudNPC : ModNPC
    {
        private int lifetimeTimer = 0;
        private int maxLifetime = 0;
        
        private int lightningCooldownTimer = 0;   
        private int chosenCooldown = 0;           
        private bool isChargingLightning = false; 
        
        private bool isFreezingPostLightning = false; 
        private int postLightningFreezeTimer = 0;     

        private int hookedLifetimeTimer = 0;
        private int landingWindowTimer = 0;

        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.RainCloudRaining;

        public override void SetStaticDefaults()
        {
            NPCID.Sets.ImmuneToAllBuffs[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 54;            
            NPC.height = 24;           
            NPC.damage = 0;            
            NPC.defense = 0;
            NPC.lifeMax = 100;
            NPC.dontTakeDamage = true; 
            NPC.noGravity = true;      
            NPC.noTileCollide = false; 
            NPC.aiStyle = -1;          
            NPC.value = 0f;            
        }

        public override bool CheckActive()
        {
            return false;
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            if (NPC.CountNPCS(Type) >= 150) 
                return 0f;

            if (spawnInfo.Player.ZoneSkyHeight || spawnInfo.Player.ZoneOverworldHeight)
                return 900.0f;
            
            return 0f;
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            Player targetPlayer = Main.player[NPC.target];

            // --- DETEKSI DAN PENYESUAIAN TINGGI TERBANG SAAT SPAWN DI SURFACE ---
            if (NPC.localAI[0] == 0f)
            {
                NPC.localAI[0] = 1f; 
                if (targetPlayer.ZoneOverworldHeight && !targetPlayer.ZoneSkyHeight)
                {
                    NPC.position.Y -= Main.rand.Next(600, 901); 
                    NPC.netUpdate = true; 
                }
            }

            if (!targetPlayer.active || targetPlayer.dead)
                NPC.ai[3] = 1f;

            if (NPC.ai[3] == 1f)
            {
                NPC.alpha += 4; 
                if (NPC.alpha >= 255)
                {
                    NPC.active = false; 
                    return;
                }
            }

            if (NPC.ai[3] != 1f)
            {
                if (maxLifetime == 0)
                    maxLifetime = Main.rand.Next(1200, 2101);

                lifetimeTimer++;
                if (lifetimeTimer >= maxLifetime)
                    NPC.ai[3] = 1f;
            }

            // --- MEKANIK KONTROL CUACA HUJAN (ANTI-NYAMBER SAAT CERAH) ---
            if (!Main.raining)
            {
                lightningCooldownTimer = 0;
                isChargingLightning = false;
                isFreezingPostLightning = false;
                postLightningFreezeTimer = 0;
            }
            else
            {
                if (chosenCooldown == 0)
                    chosenCooldown = Main.rand.Next(600, 901);

                if (NPC.ai[3] != 1f && !isFreezingPostLightning)
                    lightningCooldownTimer++;

                if (lightningCooldownTimer >= (chosenCooldown - 60) && lightningCooldownTimer < chosenCooldown)
                    isChargingLightning = true;

                if (lightningCooldownTimer >= chosenCooldown)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 finalTargetCenter = NPC.Center;
                        bool foundValidTarget = false;
                        float highestTargetScore = -1f;

                        float scanRangeX = 20f * 16f; 
                        float scanRangeY = 1000f;     

                        // Pindai Player
                        for (int i = 0; i < Main.maxPlayers; i++)
                        {
                            Player p = Main.player[i];
                            if (p.active && !p.dead && !p.ghost && p.Center.Y > NPC.Center.Y)
                            {
                                float xDist = Math.Abs(p.Center.X - NPC.Center.X);
                                float yDist = p.Center.Y - NPC.Center.Y;
                                if (xDist <= scanRangeX && yDist <= scanRangeY)
                                {
                                    float currentScore = 2000f - yDist;
                                    if (p.HasBuff(BuffID.Wet))
                                        currentScore += 6000f;
                                    if (currentScore > highestTargetScore)
                                    {
                                        highestTargetScore = currentScore;
                                        finalTargetCenter = p.Center;
                                        foundValidTarget = true;
                                    }
                                }
                            }
                        }

                        // Pindai NPC lain
                        for (int i = 0; i < Main.maxNPCs; i++)
                        {
                            NPC n = Main.npc[i];
                            if (n.active && n.whoAmI != NPC.whoAmI && n.type != NPC.type && n.Center.Y > NPC.Center.Y)
                            {
                                float xDist = Math.Abs(n.Center.X - NPC.Center.X);
                                float yDist = n.Center.Y - NPC.Center.Y;
                                if (xDist <= scanRangeX && yDist <= scanRangeY)
                                {
                                    float currentScore = 2000f - yDist;
                                    if (n.HasBuff(BuffID.Wet))
                                        currentScore += 6000f;
                                    if (currentScore > highestTargetScore)
                                    {
                                        highestTargetScore = currentScore;
                                        finalTargetCenter = n.Center;
                                        foundValidTarget = true;
                                    }
                                }
                            }
                        }

                        Vector2 lightningVelocity = new Vector2(0f, 14f);
                        if (foundValidTarget)
                        {
                            Vector2 shootDirection = finalTargetCenter - NPC.Center;
                            shootDirection.Normalize();
                            lightningVelocity = shootDirection * 14f;
                        }
                        
                        int pProj = Projectile.NewProjectile(
                            NPC.GetSource_FromAI(), 
                            NPC.Center, 
                            lightningVelocity, 
                            ProjectileID.VortexLightning, 
                            45,                   
                            0f, 
                            Main.myPlayer, 
                            lightningVelocity.ToRotation() 
                        );
                        
                        if (pProj < Main.maxProjectiles)
                        {
                            Main.projectile[pProj].alpha = 255;      
                            Main.projectile[pProj].hostile = true;   
                            Main.projectile[pProj].friendly = false; 
                            Main.projectile[pProj].netUpdate = true;
                        }
                    }

                    SoundEngine.PlaySound(SoundID.Thunder, NPC.Center);

                    isChargingLightning = false;
                    isFreezingPostLightning = true;
                    postLightningFreezeTimer = 54; 
                    lightningCooldownTimer = 0;
                }

                if (isFreezingPostLightning)
                {
                    postLightningFreezeTimer--;
                    if (postLightningFreezeTimer <= 0)
                    {
                        isFreezingPostLightning = false;
                        chosenCooldown = Main.rand.Next(600, 901);
                    }
                }
            }

            // --- GAYA FISIK BADAN UTAMA (SOLID BLOCK / ROCK PILLAR COLLISION STYLE) ---
            bool playerOnTop = false;
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player player = Main.player[i];
                if (player.active && !player.dead)
                {
                    // Cek apakah player sedang di-hook oleh awan ini
                    bool isThisPlayerHooked = false;
                    for (int j = 0; j < Main.maxProjectiles; j++)
                    {
                        Projectile proj = Main.projectile[j];
                        if (proj.active && Main.projHook[proj.type] && proj.owner == i)
                        {
                            CloudyProjectileTracker tracker = proj.GetGlobalProjectile<CloudyProjectileTracker>();
                            if (tracker.hookedNpcIndex == NPC.whoAmI)
                            {
                                isThisPlayerHooked = true;
                                break;
                            }
                        }
                    }
                    if (isThisPlayerHooked) continue;

                    if (player.Hitbox.Intersects(NPC.Hitbox))
                    {
                        float overlapX = Math.Min(player.position.X + player.width, NPC.position.X + NPC.width) - Math.Max(player.position.X, NPC.position.X);
                        float overlapY = Math.Min(player.position.Y + player.height, NPC.position.Y + NPC.height) - Math.Max(player.position.Y, NPC.position.Y);

                        if (overlapX < overlapY)
                        {
                            if (player.Center.X < NPC.Center.X)
                                player.position.X -= overlapX;
                            else
                                player.position.X += overlapX;
                            player.velocity.X = 0;
                        }
                        else
                        {
                            if (player.Center.Y < NPC.Center.Y)
                            {
                                float kecepatanHantaman = player.velocity.Y;
                                playerOnTop = true;
                                player.position.Y -= overlapY;
                                player.velocity.Y = 0;
                                player.fallStart = (int)(player.position.Y / 16f);
                                player.GetModPlayer<CloudyPlayerBlackout>().standingOnCloud = true;

                                if (player.mount.Active)
                                    player.mount.ResetFlightTime(player.velocity.X);

                                if (landingWindowTimer == 0)
                                    landingWindowTimer = 30;

                                float batasKecepatanHancur = player.mount.Active ? 7.0f : 11.0f;
                                if (kecepatanHantaman >= batasKecepatanHancur && landingWindowTimer > 0)
                                    NPC.ai[3] = 1f;
                            }
                            else
                            {
                                player.position.Y += overlapY;
                                if (player.velocity.Y < 0)
                                    player.velocity.Y = 0;
                            }
                        }
                    }
                }
            }

            if (landingWindowTimer > 0)
                landingWindowTimer--;

            // =====================================================================
            // --- MEKANIK GRAPPLING HOOK (DENGAN TRACKER & POSTAI UNTUK UPDATE POSISI) ---
            // =====================================================================
            bool adaHookMenempel = false;
            for (int j = 0; j < Main.maxProjectiles; j++)
            {
                Projectile proj = Main.projectile[j];
                if (proj.active && Main.projHook[proj.type])
                {
                    Player playerHook = Main.player[proj.owner];
                    CloudyProjectileTracker tracker = proj.GetGlobalProjectile<CloudyProjectileTracker>();

                    // Jika hook belum menempel dan mengenai awan, kunci
                    if (tracker.hookedNpcIndex == -1 && proj.ai[0] == 0f && proj.Hitbox.Intersects(NPC.Hitbox))
                    {
                        tracker.hookedNpcIndex = NPC.whoAmI;
                        proj.ai[0] = 2f;
                        proj.Center = NPC.Center;
                        proj.netUpdate = true;
                    }

                    // Jika hook menempel pada awan ini
                    if (tracker.hookedNpcIndex == NPC.whoAmI)
                    {
                        adaHookMenempel = true;

                        if (playerHook.active && !playerHook.dead)
                        {
                            float jarakKeAwan = Vector2.Distance(playerHook.Center, NPC.Center);
                            if (jarakKeAwan < 54f)
                            {
                                // Kunci posisi di bawah awan
                                playerHook.position.X = NPC.Center.X - playerHook.width / 2f;
                                playerHook.position.Y = NPC.position.Y + NPC.height + 12f;
                                playerHook.velocity = Vector2.Zero;
                            }
                            else
                            {
                                // Tarik ke arah awan
                                Vector2 arahTarik = NPC.Center - playerHook.Center;
                                arahTarik.Normalize();
                                float kecepatanTarik = 14f;
                                playerHook.velocity = arahTarik * kecepatanTarik;
                                playerHook.fallStart = (int)(playerHook.position.Y / 16f);
                            }
                        }
                    }
                }
            }

            if (adaHookMenempel)
            {
                hookedLifetimeTimer++;
                if (hookedLifetimeTimer >= 300)
                    NPC.ai[3] = 1f;
            }
            else
            {
                hookedLifetimeTimer = 0;
            }

            // --- LOGIKA GERAKAN DINAMIS + ANGIN ---
            if (NPC.ai[1] == 0f)
                NPC.ai[1] = Main.rand.NextBool() ? 1f : -1f;

            if (NPC.collideX)
            {
                NPC.ai[1] *= -1f;
                NPC.ai[3] = 1f;
            }

            float baseSpeed = 1.8f;
            float windPushEffect = Main.windSpeedCurrent * 3.0f;
            float calculatedSpeedX = (NPC.ai[1] * baseSpeed) + windPushEffect;

            if (NPC.ai[1] > 0f && calculatedSpeedX < 0.25f) calculatedSpeedX = 0.25f;
            if (NPC.ai[1] < 0f && calculatedSpeedX > -0.25f) calculatedSpeedX = -0.25f;

            NPC.velocity.X = calculatedSpeedX;

            if (isChargingLightning || isFreezingPostLightning)
                NPC.velocity = Vector2.Zero;
            else if (playerOnTop)
                NPC.velocity = Vector2.Zero;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.RainCloudRaining].Value;
            int totalFrames = Main.projFrames[ProjectileID.RainCloudRaining];
            if (totalFrames <= 0) totalFrames = 4;
            
            int frameHeight = texture.Height / totalFrames;
            int animatedFrame = (int)(Main.GameUpdateCount / 7) % totalFrames;
            Rectangle sourceRectangle = new Rectangle(0, animatedFrame * frameHeight, texture.Width, frameHeight);
            Vector2 textureOrigin = sourceRectangle.Size() * 0.5f;

            Color cloudColor = drawColor * (1f - (NPC.alpha / 255f));
            if (Main.raining && Main.cloudAlpha >= 0.5f)
                cloudColor = cloudColor * 0.40f;

            if (isChargingLightning && NPC.ai[3] != 1f)
            {
                Color glowColor = Color.Cyan * 1f * (1f - (NPC.alpha / 255f));
                Vector2[] outlineOffsets = new Vector2[]
                {
                    new Vector2(-3, 0), new Vector2(3, 0), new Vector2(0, -3), new Vector2(0, 3),
                    new Vector2(-2, -2), new Vector2(2, -2), new Vector2(-2, 2), new Vector2(2, 2)
                };
                foreach (Vector2 offset in outlineOffsets)
                {
                    spriteBatch.Draw(texture, NPC.Center + offset - screenPos, sourceRectangle, glowColor, NPC.rotation, textureOrigin, NPC.scale, SpriteEffects.None, 0f);
                }
            }

            spriteBatch.Draw(texture, NPC.Center - screenPos, sourceRectangle, cloudColor, NPC.rotation, textureOrigin, NPC.scale, SpriteEffects.None, 0f);
            return false;
        }
    }

    // =========================================================================
    // 2. KODE KONTROL GLOBAL PROJECTILE (SISTEM IDENTITAS INDUK & HOOK TRACKER)
    // =========================================================================
    public class CloudyProjectileTracker : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public bool isFromCustomCloud = false;
        public bool isCustomCloudSpark = false;
        private bool hasSpawnedSparks = false;

        // Indeks NPC awan tempat hook menempel (-1 = tidak menempel)
        public int hookedNpcIndex = -1;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (projectile.type == ProjectileID.VortexLightning && source is EntitySource_Parent parentSource && parentSource.Entity is NPC npc && npc.type == ModContent.NPCType<SpaceRainCloudNPC>())
            {
                isFromCustomCloud = true;
            }
            
            if (projectile.type == ProjectileID.Spark && source is EntitySource_Parent sparkParent && sparkParent.Entity is Projectile parentProj)
            {
                if (parentProj.TryGetGlobalProjectile(out CloudyProjectileTracker parentTracker) && parentTracker.isFromCustomCloud)
                {
                    isCustomCloudSpark = true;
                }
            }
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target)
        {
            if ((isFromCustomCloud || isCustomCloudSpark))
            {
                if (target.type == ModContent.NPCType<SpaceRainCloudNPC>())
                    return false;
                return true;
            }
            return base.CanHitNPC(projectile, target);
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo hurtInfo)
        {
            if (isFromCustomCloud || isCustomCloudSpark)
            {
                if (isFromCustomCloud)
                    target.AddBuff(BuffID.Electrified, 180);
                
                target.GetModPlayer<CloudyPlayerBlackout>().blackoutTimer = 180;
            }
        }

        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (isFromCustomCloud || isCustomCloudSpark)
            {
                if (isFromCustomCloud)
                    target.AddBuff(BuffID.Electrified, 180);

                if (target.TryGetGlobalNPC(out CloudyNPCBlackout npcBlackout))
                    npcBlackout.blackoutTimer = 180;
            }
        }

        public override bool OnTileCollide(Projectile projectile, Vector2 oldVelocity)
        {
            if (isFromCustomCloud)
                TriggerSparkSpout(projectile);
            return base.OnTileCollide(projectile, oldVelocity);
        }

        public override void PostAI(Projectile projectile)
        {
            // --- PEMBARUAN POSISI HOOK DAN KONDISI PELEPASAN ---
            if (Main.projHook[projectile.type] && hookedNpcIndex != -1)
            {
                if (hookedNpcIndex >= 0 && hookedNpcIndex < Main.maxNPCs)
                {
                    NPC npc = Main.npc[hookedNpcIndex];
                    if (npc.active && npc.type == ModContent.NPCType<SpaceRainCloudNPC>() && npc.ai[3] != 1f)
                    {
                        Player playerHook = Main.player[projectile.owner];
                        // Lepas jika tombol lompat, mount, atau player mati/tidak aktif
                        if (playerHook.controlJump || playerHook.mount.Active || !playerHook.active || playerHook.dead)
                        {
                            hookedNpcIndex = -1;
                            projectile.ai[0] = 1f;
                            projectile.netUpdate = true;
                            return;
                        }
                        // Perbarui posisi hook ke tengah awan
                        projectile.Center = npc.Center;
                        projectile.ai[0] = 2f;
                        projectile.netUpdate = true;
                    }
                    else
                    {
                        // Awan tidak aktif/mati, lepaskan
                        hookedNpcIndex = -1;
                        projectile.ai[0] = 1f;
                        projectile.netUpdate = true;
                    }
                }
                else
                {
                    hookedNpcIndex = -1;
                    projectile.ai[0] = 1f;
                    projectile.netUpdate = true;
                }
            }

            // --- TRIGGER SPARK SAAT PETIR MENYENTUH TANAH ---
            if (isFromCustomCloud && !hasSpawnedSparks && (projectile.velocity.Y == 0f || Collision.SolidCollision(projectile.position, projectile.width, projectile.height)))
            {
                TriggerSparkSpout(projectile);
            }
        }

        private void TriggerSparkSpout(Projectile projectile)
        {
            if (hasSpawnedSparks) return;
            hasSpawnedSparks = true;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int spawnCount = Main.rand.Next(4, 7);
                for (int i = 0; i < spawnCount; i++)
                {
                    Vector2 launchVelocity = new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-6f, -3f));
                    int sparkDamage = projectile.damage / 2;

                    int sparkProj = Projectile.NewProjectile(
                        projectile.GetSource_FromThis(),
                        projectile.Center,
                        launchVelocity,
                        ProjectileID.Spark,
                        sparkDamage,
                        0f,
                        Main.myPlayer
                    );

                    if (sparkProj < Main.maxProjectiles)
                    {
                        Main.projectile[sparkProj].hostile = true;
                        Main.projectile[sparkProj].friendly = true;
                        Main.projectile[sparkProj].timeLeft = 120;
                        Main.projectile[sparkProj].netUpdate = true;
                    }
                }
            }
        }
    }

    // =========================================================================
    // 3. KODE MOD PLAYER (SISTEM CORAK HITAM GOSONG PLAYER - ALL LAYERS)
    // =========================================================================
    public class CloudyPlayerBlackout : ModPlayer
    {
        public int blackoutTimer = 0;
        public bool standingOnCloud = false;

        public override void ResetEffects()
        {
            standingOnCloud = false;
        }

        public override void PostUpdateMiscEffects()
        {
            if (blackoutTimer > 0)
                blackoutTimer--;
        }

        public override void PreUpdateMovement()
        {
            if (standingOnCloud)
            {
                Player.velocity.Y = 0f;
                Player.fallStart = (int)(Player.position.Y / 16f);
            }
        }

        public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
        {
            if (blackoutTimer > 0)
            {
                float intensity = 1f;
                if (blackoutTimer < 30)
                    intensity = blackoutTimer / 30f;

                for (int i = 0; i < drawInfo.DrawDataCache.Count; i++)
                {
                    DrawData data = drawInfo.DrawDataCache[i];
                    data.color = Color.Lerp(data.color, Color.Black, intensity);
                    drawInfo.DrawDataCache[i] = data;
                }
            }
        }
    }

    // =========================================================================
    // 4. KODE GLOBAL NPC (SISTEM CORAK HITAM GOSONG ALL NPC VIA PRE-DRAW MANUAL TINT)
    // =========================================================================
    public class CloudyNPCBlackout : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        public int blackoutTimer = 0;

        public override void PostAI(NPC npc)
        {
            if (blackoutTimer > 0)
                blackoutTimer--;
        }

        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            if (blackoutTimer > 0)
            {
                float intensity = 1f;
                if (blackoutTimer < 30)
                    intensity = blackoutTimer / 30f;

                Texture2D texture = TextureAssets.Npc[npc.type].Value;
                Vector2 drawOrigin = npc.frame.Size() / 2f;

                SpriteEffects effects = SpriteEffects.None;
                if (npc.spriteDirection == 1)
                    effects = SpriteEffects.FlipHorizontally;

                Vector2 drawPos = npc.Center - screenPos;
                drawPos.Y += npc.gfxOffY;

                Color finalColor = Color.Lerp(drawColor, Color.Black, intensity);

                spriteBatch.Draw(
                    texture,
                    drawPos,
                    npc.frame,
                    npc.GetAlpha(finalColor),
                    npc.rotation,
                    drawOrigin,
                    npc.scale,
                    effects,
                    0f
                );

                return false;
            }
            return true;
        }
    }

    // =========================================================================
    // 5. KODE GLOBAL NPC (LOCK ABSOLUT SPAWN FREQUENCY - ANTI MODIFIER BIOME/BUFF)
    // =========================================================================
    public class SpaceRainCloudSpawnBypass : global::Terraria.ModLoader.GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            if (Main.raining && (player.ZoneOverworldHeight || player.ZoneSkyHeight))
            {
                spawnRate = 140;
                maxSpawns = 6;
            }
        }
    }
}