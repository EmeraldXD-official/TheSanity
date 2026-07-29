using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria.Audio; 

namespace TheSanity.GlobalNPC.Enemy
{
    public class ChameleonSlime : ModNPC
    {
        // ---- Sistem Pencurian Multi-Item (10 Slot Inventory) ----
        private int[] stolenItemTypes = new int[10];
        private int[] stolenItemStacks = new int[10];
        private int stolenItemCount = 0;
        private bool isFleeing = false;
        private int fleeTimer = 0;

        // ---- Custom AI Engine & State Machine ----
        private int jumpTimer = 0;        
        private bool isGrounded = false;  
        private float currentJumpX = 0f;  
        private float currentJumpY = 0f;  

        // ---- Sistem Radar Goa Buntu & Peluncur Ketapel ----
        private bool retreatingFromCave = false; 
        private int caveRetreatDir = 0;          
        private bool preparingMegaJump = false;  
        private int megaJumpDir = 0;             
        private bool megaJumpFlight = false;     

        // ---- Sistem Pengunci Arah Tebing Raksasa ----
        private int wallTurnDirection = 0;       

        // ---- Sistem Deteksi Stuck Terlokalisasi ----
        private float lastPositionX = 0f;
        private int stuckTimer = 0;
        private int forcedDirection = 0;

        // ---- Warna & Opacity saat ini ----
        public Color currentColor = Color.White;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = 1; 
            NPCID.Sets.ImmuneToAllBuffs[Type] = true;
        }

        public override void SetDefaults()
        {
            NPC.width = 48; // Entity 3 Block murni (Mencegah amblas ke lubang kecil)
            NPC.height = 40;
            
            NPC.damage = 20;
            NPC.defense = 0;
            NPC.lifeMax = 86;
            NPC.HitSound = null; 
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.value = 100f;
            NPC.knockBackResist = 0.5f;
            NPC.aiStyle = -1;                  
            NPC.noTileCollide = false; 
            NPC.lavaImmune = true;
            NPC.rarity = 1;			
        }

        public override bool CheckActive()
        {
            return stolenItemCount == 0;
        }

        public override void AI()
        {
            // -------------------------------------------------------------------------
            // [LOKASI PANDUAN BALANCING]: KECEPATAN, DAYA LOMPAT, & BATAS DINDING STRUKTUR
            // -------------------------------------------------------------------------
            float horizontalSpeedNormal = 3.5f;   
            float horizontalSpeedFlee = 7.0f;     
            float baseGravity = 0.35f;            
            float maxFallSpeedNormal = 12f;       
            
            int normalJumpCooldown = 25;          
            int fleeJumpCooldown = 8;             
            
            float minJumpHeight = -7.0f;          
            float maxJumpHeight = -22.0f;         
            
            int takeStanceDelay = 35;             
            int scanMinDistance = 18;             
            int scanMaxDistance = 28;             
            int scanMaxHeight = 40;               
            int miscalculateChance = 6;           
            
            int maxClimbableWallBlocks = 50;      
            // -------------------------------------------------------------------------

            float gravityForce = baseGravity;
            float maxFallSpeed = maxFallSpeedNormal;

            Player target = Main.player[NPC.target];
            NPC.TargetClosest(true);
            bool targetValid = target.active && !target.dead;

            int biome = GetCurrentBiome(target);
            bool isUnderground = NPC.position.Y > Main.worldSurface * 16;
            bool useNightColor = !Main.dayTime || isUnderground;
            
            Color targetColor = GetCamouflageColor(biome, useNightColor);
            currentColor = currentColor == Color.White ? targetColor : Color.Lerp(currentColor, targetColor, 0.05f);

            NPC.alpha = 0; 
            NPC.color = currentColor;

            isGrounded = NPC.velocity.Y == 0f || Collision.SolidCollision(NPC.BottomLeft, NPC.width, 2);
            if (isGrounded)
            {
                megaJumpFlight = false; 
            }
            
            bool wedgedInHole = NPC.collideX && Collision.SolidCollision(NPC.TopLeft, NPC.width, NPC.height);

            // ---- [SOLUSI CS0841]: DEKLARASI moveDirection DIPINDAHKAN KE SINI ----
            int moveDirection = (NPC.Center.X < target.Center.X) ? 1 : -1;
            if (isFleeing) moveDirection *= -1;

            if (wallTurnDirection != 0)
            {
                moveDirection = wallTurnDirection;
            }

            // ---- SISTEM DETEKSI DAN ANTI-KEJEPIT LUBANG 1X1 JAGGED WALL ----
            if (NPC.collideX)
            {
                if (Math.Abs(NPC.position.X - lastPositionX) < 0.5f)
                    stuckTimer++;
            }
            else
            {
                if (stuckTimer > 0) stuckTimer--;
            }
            lastPositionX = NPC.position.X;

            // Sekarang moveDirection di bawah ini sudah aman dibaca oleh compiler!
            if (stuckTimer >= 30)
            {
                stuckTimer = 0;
                if (!wedgedInHole)
                {
                    NPC.velocity.Y = -10.5f;              
                    NPC.velocity.X = -moveDirection * 4.5f; 
                    jumpTimer = 20;                        
                    forcedDirection = -moveDirection;
                }
            }

            // ---- RADAR VIRTUAL: GOA BUNTU & LUBANG ----
            int npcTileX = (int)(NPC.Center.X / 16);
            int npcTileY = (int)(NPC.Bottom.Y / 16);
            bool hasCeilingAbove = false;

            for (int yOffset = 3; yOffset <= 13; yOffset++) 
            {
                int checkY = npcTileY - yOffset;
                if (checkY >= 0 && checkY < Main.maxTilesY)
                {
                    Tile tile = Main.tile[npcTileX, checkY];
                    if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    {
                        hasCeilingAbove = true;
                        break;
                    }
                }
            }

            bool wallAheadInCave = false;
            if (hasCeilingAbove)
            {
                for (int xOffset = 1; xOffset <= 6; xOffset++) 
                {
                    int checkX = npcTileX + (moveDirection * xOffset);
                    if (checkX >= 0 && checkX < Main.maxTilesX)
                    {
                        for (int yOffset = 0; yOffset < 4; yOffset++) 
                        {
                            int checkY = npcTileY - yOffset;
                            if (checkY >= 0 && checkY < Main.maxTilesY)
                            {
                                Tile tile = Main.tile[checkX, checkY];
                                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                                {
                                    wallAheadInCave = true;
                                    break;
                                }
                            }
                        }
                    }
                    if (wallAheadInCave) break;
                }
            }

            if (isGrounded && hasCeilingAbove && (NPC.collideX || wallAheadInCave) && !retreatingFromCave && !preparingMegaJump)
            {
                retreatingFromCave = true;
                caveRetreatDir = -moveDirection; 
                wallTurnDirection = 0; 
            }

            if (retreatingFromCave && isGrounded)
            {
                if (!hasCeilingAbove) 
                {
                    retreatingFromCave = false;
                    preparingMegaJump = true;
                    megaJumpDir = -caveRetreatDir; 
                    jumpTimer = takeStanceDelay;   
                }
            }

            if (retreatingFromCave)
            {
                moveDirection = caveRetreatDir;
            }
            else if (preparingMegaJump)
            {
                moveDirection = megaJumpDir;
            }

            if (forcedDirection != 0)
            {
                moveDirection = forcedDirection;
                forcedDirection = 0; 
            }

            // ---- MEKANIK LOMPATAN UDARA: GLIDING VS FAST SLAM ----
            if (!isGrounded && NPC.wet == false && !megaJumpFlight)
            {
                bool wantToSlam = isFleeing && NPC.velocity.Y > 1.5f && !Collision.SolidCollision(NPC.BottomLeft + new Vector2(0, 24), NPC.width, 48);
                bool wantToGlide = Math.Abs(NPC.velocity.X) > 2.0f && NPC.velocity.Y > 0.5f && !wantToSlam;

                if (wantToSlam)
                {
                    gravityForce = 0.95f;    
                    maxFallSpeed = 22f;      
                    NPC.velocity.X *= 0.92f; 
                }
                else if (wantToGlide)
                {
                    gravityForce = 0.12f;    
                    maxFallSpeed = 4.0f;     
                    NPC.velocity.X += moveDirection * 0.15f;
                    float maxAirSpeed = isFleeing ? horizontalSpeedFlee * 1.1f : horizontalSpeedNormal * 1.1f;
                    NPC.velocity.X = Math.Clamp(NPC.velocity.X, -maxAirSpeed, maxAirSpeed);
                }
            }

            if (NPC.wet)
            {
                megaJumpFlight = false;
                bool playerChasingInWater = targetValid && target.wet && Vector2.Distance(NPC.Center, target.Center) < 400f;
                if (playerChasingInWater)
                {
                    NPC.velocity.Y += gravityForce * 0.5f; 
                    if (NPC.velocity.Y > 4f) NPC.velocity.Y = 4f;
                }
                else
                {
                    if (NPC.velocity.Y > -2f) NPC.velocity.Y -= 0.3f;
                }
            }

            // ---- STATE MESIN LOMPATAN ----
            if (isGrounded)
            {
                NPC.velocity.X = 0f;

                if (wedgedInHole)
                {
                    jumpTimer = 15; 
                }
                else if (jumpTimer > 0)
                {
                    jumpTimer--;
                }
                else
                {
                    if (targetValid)
                    {
                        float speedX = isFleeing ? horizontalSpeedFlee : horizontalSpeedNormal;

                        // [STATE A]: FIX DETEKSI PRESIFIKASI & PUSH MEGA JUMP KETAPEL RAKSASA
                        if (preparingMegaJump)
                        {
                            int targetTileX = npcTileX + (megaJumpDir * 22);
                            int targetTileY = npcTileY - 18;
                            bool foundHigherGround = false;

                            for (int xOffset = scanMinDistance; xOffset <= scanMaxDistance; xOffset++)
                            {
                                int checkX = npcTileX + (megaJumpDir * xOffset);
                                if (checkX >= 0 && checkX < Main.maxTilesX)
                                {
                                    for (int yOffset = scanMaxHeight; yOffset >= 2; yOffset--)
                                    {
                                        int checkY = npcTileY - yOffset;
                                        if (checkY >= 0 && checkY < Main.maxTilesY)
                                        {
                                            Tile tile = Main.tile[checkX, checkY];
                                            if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType] && checkY < npcTileY)
                                            {
                                                if (!Main.tile[checkX, checkY - 1].HasTile && !Main.tile[checkX, checkY - 2].HasTile && !Main.tile[checkX, checkY - 3].HasTile)
                                                {
                                                    targetTileX = checkX;
                                                    targetTileY = checkY - 2; 
                                                    foundHigherGround = true;
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                                if (foundHigherGround) break; 
                            }

                            float diffX = (targetTileX - npcTileX) * 16f;
                            float diffY = (targetTileY - npcTileY) * 16f; 

                            float apexHeight = diffY - 320f; 
                            if (apexHeight > -480f) apexHeight = -480f; 

                            float requiredVy = -(float)Math.Sqrt(2f * baseGravity * Math.Abs(apexHeight));
                            float timeToApex = -requiredVy / baseGravity;
                            float timeToTarget = (float)Math.Sqrt(2f * Math.Abs(apexHeight - diffY) / baseGravity);
                            float totalTimeFrames = timeToApex + timeToTarget;
                            float requiredVx = diffX / (totalTimeFrames > 0 ? totalTimeFrames : 1f);

                            if (Main.rand.NextBool(miscalculateChance))
                            {
                                requiredVx *= Main.rand.NextFloat(0.90f, 1.12f);
                            }

                            currentJumpX = requiredVx;
                            currentJumpY = requiredVy * 1.25f; 

                            preparingMegaJump = false;
                            megaJumpFlight = true; 
                            jumpTimer = 50; 
                        }
                        // [STATE B]: LOMPATAN MUNDUR BERTAHAP
                        else if (retreatingFromCave)
                        {
                            currentJumpX = moveDirection * (speedX * 0.75f); 
                            currentJumpY = -5.5f; 
                            jumpTimer = 14;
                        }
                        // [STATE C]: LOMPATAN ADAPTIF LERENG DAN LIVING TREE (ANTI JAGGED WALL 1X1)
                        else
                        {
                            currentJumpX = moveDirection * speedX;
                            int scanDistance = isFleeing ? 5 : 3; 
                            int highestObstacle = 0;

                            for (int xOffset = 1; xOffset <= scanDistance; xOffset++)
                            {
                                int checkX = npcTileX + (moveDirection * xOffset);
                                if (checkX >= 0 && checkX < Main.maxTilesX)
                                {
                                    for (int yOffset = 0; yOffset < 55; yOffset++)
                                    {
                                        int checkY = npcTileY - yOffset;
                                        if (checkY >= 0 && checkY < Main.maxTilesY)
                                        {
                                            Tile tile = Main.tile[checkX, checkY];
                                            if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                                            {
                                                if (yOffset > highestObstacle)
                                                {
                                                    highestObstacle = yOffset;
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (highestObstacle > maxClimbableWallBlocks)
                            {
                                wallTurnDirection = -moveDirection;
                                moveDirection = wallTurnDirection;
                                currentJumpX = moveDirection * speedX;
                                
                                highestObstacle = 0;
                                for (int xOffset = 1; xOffset <= scanDistance; xOffset++)
                                {
                                    int checkX = npcTileX + (moveDirection * xOffset);
                                    if (checkX >= 0 && checkX < Main.maxTilesX)
                                    {
                                        for (int yOffset = 0; yOffset < 55; yOffset++)
                                        {
                                            int checkY = npcTileY - yOffset;
                                            if (checkY >= 0 && checkY < Main.maxTilesY)
                                            {
                                                Tile tile = Main.tile[checkX, checkY];
                                                if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                                                {
                                                    if (yOffset > highestObstacle) highestObstacle = yOffset;
                                                }
                                            }
                                        }
                                    }
                                }
                            }

                            if (highestObstacle > 0 && highestObstacle <= maxClimbableWallBlocks)
                            {
                                currentJumpY = minJumpHeight - (highestObstacle * 0.85f) - 5.0f; 
                                currentJumpX += moveDirection * 2.2f; 
                            }
                            else
                            {
                                currentJumpY = minJumpHeight - Main.rand.NextFloat(1.0f, 2.5f);
                            }
                        }

                        if (currentJumpY < maxJumpHeight && !megaJumpFlight && !preparingMegaJump) currentJumpY = maxJumpHeight;

                        NPC.velocity.X = currentJumpX;
                        NPC.velocity.Y = currentJumpY;
                        
                        if (!retreatingFromCave && !megaJumpFlight)
                        {
                            jumpTimer = isFleeing ? fleeJumpCooldown : normalJumpCooldown;
                        }

                        PlayCustomJumpSound();
                        
                        if (isFleeing)
                        {
                            SpawnSpikes(); 

                            if (NPC.life < NPC.lifeMax)
                            {
                                NPC.life += 10;
                                if (NPC.life > NPC.lifeMax) NPC.life = NPC.lifeMax;
                                NPC.HealEffect(10); 
                            }
                        }
                    }
                }
            }
            else if (!wedgedInHole)
            {
                if (!megaJumpFlight && (NPC.velocity.X == 0f || Math.Abs(NPC.velocity.X) < Math.Abs(currentJumpX) * 0.4f))
                {
                    NPC.velocity.X = currentJumpX * 0.85f;
                }
            }

            if (isFleeing)
            {
                fleeTimer++;
                if (fleeTimer > 3600) 
                {
                    isFleeing = false;
                    fleeTimer = 0;
                }
            }

            NPC.velocity.Y += gravityForce;
            if (NPC.velocity.Y > maxFallSpeed)
                NPC.velocity.Y = maxFallSpeed;

            if (NPC.position.Y > Main.maxTilesY * 16 - 100)
                NPC.active = false;
        }

        private void PlayCustomJumpSound()
        {
            int randomStyle = Main.rand.Next(4); 
            string selectedSound = randomStyle switch
            {
                0 => "Chameleon",
                1 => "Chameleon1",
                2 => "Chameleon2",
                3 => "Chameleon3",
                _ => "Chameleon"
            };
            SoundEngine.PlaySound(new SoundStyle($"TheSanity/Sounds/Chameleon/{selectedSound}"), NPC.Center);
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            if (NPC.life > 0) 
            {
                string selectedHitSound = Main.rand.NextBool() ? "ChameleonHit" : "ChameleonHit1";
                SoundEngine.PlaySound(new SoundStyle($"TheSanity/Sounds/Chameleon/{selectedHitSound}"), NPC.Center);
            }
        }

        private int GetCurrentBiome(Player player)
        {
            if (NPC.position.Y > Main.UnderworldLayer * 16) return 7; 
            if (NPC.position.Y < Main.worldSurface * 0.4f) return 8;  

            if (player.active && !player.dead)
            {
                if (player.ZoneGlowshroom) return 9; 

                if (player.ZoneCrimson) return 4;
                if (player.ZoneCorrupt) return 5;
                if (player.ZoneJungle) return 3;
                if (player.ZoneDesert) return 2;
                if (player.ZoneSnow) return 1;
                if (player.ZoneBeach) return 6; 
            }
            return 0; 
        }

        private Color GetCamouflageColor(int biome, bool useNightColor)
        {
            switch (biome)
            {
                case 9: return new Color(0, 160, 255); 
                case 7: return new Color(240, 50, 20); 
                case 8: return useNightColor ? new Color(120, 120, 120) : new Color(255, 255, 255); 
                case 6: return NPC.wet ? (useNightColor ? new Color(12, 25, 102) : new Color(25, 76, 204)) : (useNightColor ? new Color(180, 100, 25) : new Color(230, 204, 51));
                case 1: return useNightColor ? new Color(160, 160, 160) : new Color(230, 230, 230);
                case 2: return useNightColor ? new Color(180, 100, 25) : new Color(230, 204, 51);
                case 3: return useNightColor ? new Color(102, 51, 0) : new Color(25, 128, 25);
                case 4: return useNightColor ? new Color(102, 0, 0) : new Color(204, 25, 25);
                case 5: return useNightColor ? new Color(51, 0, 76) : new Color(128, 25, 153);
                case 0:
                default: return useNightColor ? new Color(40, 40, 40) : new Color(76, 204, 51);
            }
        }

        private int GetBiomeDebuff(int biome)
        {
            switch (biome)
            {
                case 9: return BuffID.Confused; 
                case 0: return BuffID.OgreSpit;
                case 1: return BuffID.Frostburn;
                case 2: return BuffID.OnFire;
                case 3: return BuffID.Poisoned;
                case 4: return BuffID.Ichor;
                case 5: return BuffID.CursedInferno;
                case 6: return BuffID.Cursed;
                case 7: return BuffID.Burning;
                case 8: return BuffID.VortexDebuff;
                default: return BuffID.OgreSpit;
            }
        }

        private void SpawnSpikes()
        {
            int spikeType = ModContent.ProjectileType<Projectiles.ChameleonSpike>();
            int count = Main.rand.Next(4, 7); 
            int spikeDamage = 25; 

            for (int i = 0; i < count; i++)
            {
                Vector2 velocity = new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-9f, -5f));

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, velocity, spikeType, spikeDamage, 1f, Main.myPlayer);

                    if (Main.projectile[proj].ModProjectile is Projectiles.ChameleonSpike spike)
                    {
                        spike.projectileColor = currentColor;
                        spike.parentAlpha = (byte)(255 * (Main.raining ? 0.15f : 0.30f));
                    }
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo)
        {
            if (stolenItemCount < 10)
            {
                Item heldItem = target.inventory[target.selectedItem];
                if (heldItem != null && !heldItem.IsAir)
                {
                    stolenItemTypes[stolenItemCount] = heldItem.type;
                    stolenItemStacks[stolenItemCount] = heldItem.stack;
                    stolenItemCount++;

                    heldItem.TurnToAir(); 

                    isFleeing = true;
                    fleeTimer = 0;
                    wallTurnDirection = 0; 

                    if (Main.netMode != NetmodeID.SinglePlayer)
                        NetMessage.SendData(MessageID.ChatText, -1, -1, Terraria.Localization.NetworkText.FromLiteral("Slime Stole Your Item!"));
                    else
                        Main.NewText("Slime Stole Your Item!", Color.Orange);
                }
            }

            int biome = GetCurrentBiome(target);
            int debuff = GetBiomeDebuff(biome);
            target.AddBuff(debuff, Main.raining ? 600 : 300);

            if (Main.raining)
            {
                target.AddBuff(BuffID.Electrified, 300);
            }
        }

        public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (projectile.type == ProjectileID.FallingStar || projectile.type == ProjectileID.VortexVortexLightning)
            {
                modifiers.FinalDamage *= 0; 
                modifiers.Knockback *= 0f;  
            }
        }

        public override void OnHitByProjectile(Projectile projectile, NPC.HitInfo hit, int damageDone)
        {
            if (projectile.type == ProjectileID.FallingStar || projectile.type == ProjectileID.VortexVortexLightning)
            {
                NPC.life += damageDone; 
                if (NPC.life > NPC.lifeMax) NPC.life = NPC.lifeMax;
                projectile.Kill();      
            }
        }

        public override void OnKill()
        {
            for (int i = 0; i < stolenItemCount; i++)
            {
                if (stolenItemTypes[i] > 0 && stolenItemStacks[i] > 0)
                {
                    Item.NewItem(NPC.GetSource_Loot(), NPC.Center, stolenItemTypes[i], stolenItemStacks[i]);
                }
            }

            Item.NewItem(NPC.GetSource_Loot(), NPC.Center, ItemID.Gel, Main.rand.Next(30, 51));
            Item.NewItem(NPC.GetSource_Loot(), NPC.Center, ItemID.GoldCoin, Main.rand.Next(5, 8));
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = Terraria.GameContent.TextureAssets.Npc[NPC.type].Value;
            Rectangle frame = NPC.frame;
            if (frame.Width == 0 || frame.Height == 0)
            {
                frame = new Rectangle(0, 0, texture.Width, texture.Height / Main.npcFrameCount[NPC.type]);
            }

            float baseOpacity = Main.raining ? 0.15f : 0.30f;
            Color drawColorCustom = currentColor * baseOpacity;

            float scaleX = 1f;
            float scaleY = 1f;

            if (NPC.velocity.Y < 0) 
            {
                scaleY = 1f + Math.Abs(NPC.velocity.Y) * 0.025f;
                scaleX = 1f - Math.Abs(NPC.velocity.Y) * 0.012f;
            }
            else if (NPC.velocity.Y > 0) 
            {
                scaleX = 1f + NPC.velocity.Y * 0.025f;
                scaleY = 1f - NPC.velocity.Y * 0.012f;
            }

            scaleX = Math.Clamp(scaleX, 0.7f, 1.4f);
            scaleY = Math.Clamp(scaleY, 0.7f, 1.4f);

            Vector2 origin = frame.Size() / 2f;
            Vector2 position = NPC.Center - screenPos;

            if (stolenItemCount > 0)
            {
                for (int i = 0; i < stolenItemCount; i++)
                {
                    int itemType = stolenItemTypes[i];
                    if (itemType > 0)
                    {
                        Main.instance.LoadItem(itemType); 
                        Texture2D itemTexture = Terraria.GameContent.TextureAssets.Item[itemType].Value;
                        Rectangle itemFrame = new Rectangle(0, 0, itemTexture.Width, itemTexture.Height);
                        Vector2 itemOrigin = itemFrame.Size() / 2f;

                        float radians = (float)(i * (Math.PI * 2 / 10)) + (Main.GameUpdateCount * 0.02f);
                        Vector2 innerOffset = new Vector2((float)Math.Cos(radians), (float)Math.Sin(radians)) * 7f;
                        Vector2 itemDrawPos = position + innerOffset;

                        float itemRotation = (float)Math.Sin(Main.GameUpdateCount * 0.05f + i) * 0.15f;
                        float pulseScale = 0.55f + (float)Math.Sin(Main.GameUpdateCount * 0.08f + i) * 0.05f;
                        Color itemColor = Color.White * baseOpacity; 

                        spriteBatch.Draw(itemTexture, itemDrawPos, itemFrame, itemColor, itemRotation, itemOrigin, new Vector2(scaleX, scaleY) * pulseScale, SpriteEffects.None, 0f);
                    }
                }
            }

            spriteBatch.Draw(texture, position, frame, drawColorCustom, NPC.rotation, origin, new Vector2(scaleX, scaleY), SpriteEffects.None, 0f);

            return false;
        }
    }

    public class ChameleonSlimeSpawn : global::Terraria.ModLoader.GlobalNPC
    {
        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            if (!NPC.downedBoss1)
                return;

            if (spawnInfo.Player.ZoneDungeon)
                return;

            if (spawnInfo.PlayerSafe) 
                return;

            pool.Add(ModContent.NPCType<ChameleonSlime>(), 0.015f);
        }
    }
}