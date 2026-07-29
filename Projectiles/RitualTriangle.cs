using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class RitualTriangle : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/RitualTriangle";

        private Vector2 cachedAltarPos = Vector2.Zero;
        private bool shouldDraw = false; 

        private int warningTimer = 0;
        private bool isPulling = false;
        private float rotationTimer = 0f;

        public override void SetDefaults()
        {
            Projectile.width = 32;       
            Projectile.height = 32;
            Projectile.hostile = false;  
            Projectile.friendly = false; 
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 10;    
            Projectile.hide = true; 
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overWiresUI, List<int> overWiresUI2)
        {
            behindNPCs.Add(index);
        }

        public override void AI()
        {
            Projectile.timeLeft = 10;
            rotationTimer += 0.05f; 

            int golemHeadIndex = NPC.FindFirstNPC(NPCID.GolemHead);
            int golemHeadFreeIndex = NPC.FindFirstNPC(NPCID.GolemHeadFree);
            bool trackingGolem = (golemHeadIndex != -1 || golemHeadFreeIndex != -1);

            if (trackingGolem)
            {
                NPC boss = golemHeadIndex != -1 ? Main.npc[golemHeadIndex] : Main.npc[golemHeadFreeIndex];
                bool isHeadFreeAlive = (golemHeadIndex == -1 && golemHeadFreeIndex != -1);

                int targetIndex = (boss.target >= 0 && boss.target < Main.maxPlayers) ? boss.target : Main.myPlayer;
                Player player = Main.player[targetIndex];

                if (player.active && !player.dead)
                {
                    shouldDraw = true;
                    Projectile.Center = player.Center;

                    Vector2 altarPos = FindLihzahrdAltar();
                    Vector2 altarCenter = altarPos != Vector2.Zero ? altarPos + new Vector2(24, 24) : boss.Center;

                    bool isTooFarUp = (altarCenter.Y - player.Center.Y) > (67 * 16f);

                    if (isHeadFreeAlive)
                    {
                        isPulling = false;
                        warningTimer = 0;
                    }
                    else
                    {
                        if (isTooFarUp)
                        {
                            if (warningTimer < 180) warningTimer++;
                            
                            if (warningTimer >= 180 && !isPulling)
                            {
                                isPulling = true;
                                player.AddBuff(BuffID.Shimmer, 60);
                            }
                        }
                        else
                        {
                            if (!isPulling && warningTimer > 0) warningTimer--;
                        }
                    }

                    if (isPulling && !isHeadFreeAlive)
                    {
                        Vector2 vectorToGolem = boss.Center - player.Center;
                        float distanceToGolem = vectorToGolem.Length();

                        if (distanceToGolem <= 10 * 16f) 
                        {
                            isPulling = false;
                            warningTimer = 0; 
                        }
                        else
                        {
                            float pullSpeed = 16f; 
                            Vector2 moveDirection = vectorToGolem.SafeNormalize(Vector2.UnitY);
                            
                            player.position += moveDirection * pullSpeed; 
                            player.velocity = Vector2.Zero;              
                            player.fallStart = (int)(player.position.Y / 16f);
                        }
                    }
                }
                else
                {
                    shouldDraw = false;
                }
            }
            else
            {
                isPulling = false;
                warningTimer = 0;
                Projectile.Center = Main.LocalPlayer.Center;
                shouldDraw = false; 
            }
        }

        private Vector2 FindLihzahrdAltar()
        {
            if (cachedAltarPos != Vector2.Zero) 
                return cachedAltarPos;

            Player player = Main.LocalPlayer;
            int pX = (int)(player.Center.X / 16);
            int pY = (int)(player.Center.Y / 16);

            for (int x = pX - 150; x < pX + 150; x++)
            {
                for (int y = pY - 150; y < pY + 150; y++)
                {
                    if (x > 0 && x < Main.maxTilesX && y > 0 && y < Main.maxTilesY)
                    {
                        if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.LihzahrdAltar)
                        {
                            cachedAltarPos = new Vector2(x * 16, y * 16);
                            return cachedAltarPos;
                        }
                    }
                }
            }
            return Vector2.Zero;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (!shouldDraw) return false;

            int golemHeadIndex = NPC.FindFirstNPC(NPCID.GolemHead);
            int golemHeadFreeIndex = NPC.FindFirstNPC(NPCID.GolemHeadFree);

            Vector2 chainTargetPos = Vector2.Zero;
            bool isHeadFreeAlive = false;

            if (golemHeadIndex != -1)
            {
                chainTargetPos = Main.npc[golemHeadIndex].Center;
            }
            else if (golemHeadFreeIndex != -1)
            {
                chainTargetPos = Main.npc[golemHeadFreeIndex].Center;
                isHeadFreeAlive = true;
            }

            // RENDERING RANTAI VANILLA (DIAM STATIS & WARNA DEFAULT ORIGINAL)
            if ((isPulling || isHeadFreeAlive) && chainTargetPos != Vector2.Zero)
            {
                Texture2D chainTexture = TextureAssets.Chain22.Value; 
                Vector2 chainCurrent = Projectile.Center; 
                Vector2 remainingVector = chainTargetPos - chainCurrent;
                
                float rotation = remainingVector.ToRotation() - MathHelper.PiOver2;

                while (remainingVector.Length() > 16f && !float.IsNaN(remainingVector.Length()))
                {
                    remainingVector.Normalize();
                    remainingVector *= 16f; 
                    chainCurrent += remainingVector;
                    remainingVector = chainTargetPos - chainCurrent;

                    Color chainVanillaColor = Lighting.GetColor((int)(chainCurrent.X / 16f), (int)(chainCurrent.Y / 16f));

                    Main.EntitySpriteDraw(chainTexture, chainCurrent - Main.screenPosition, null, 
                        chainVanillaColor, rotation, chainTexture.Size() / 2f, 1f, SpriteEffects.None, 0);
                }
            }

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // =========================================================================
            // LAYER 1: RENDERING LINGKARAN LUAR (CULTIST RITUAL - VANILLA ASSET)
            // Selalu digambar selama Boss Golem aktif tracking player
            // =========================================================================
            Texture2D outerTexture = TextureAssets.Projectile[ProjectileID.CultistRitual].Value;
            Vector2 outerOrigin = outerTexture.Size() / 2f; 
            
            float outerRotation = rotationTimer * 0.3f; 

            // [COLOR LOCATION]: WARNA RING RITUAL LUAR
            Color outerColor = Color.LightGray * Projectile.Opacity * 0.8f; 

            // [SCALE LOCATION]: UKURAN LINGKARAN CULTIST RITUAL LUAR (Pakai settingan kustom kamu)
            float outerScale = 0.45f; 

            Main.EntitySpriteDraw(
                outerTexture, 
                drawPos, 
                null, 
                outerColor, 
                outerRotation, 
                outerOrigin, 
                outerScale, 
                SpriteEffects.None, 
                0
            );

            // =========================================================================
            // LAYER 2: RENDERING LINGKARAN DALAM (MOON - CUSTOM MOD ASSET)
            // KONDISI KHUSUS: Hanya digambar ketika GolemHeadFree terdeteksi hidup!
            // =========================================================================
            if (isHeadFreeAlive)
            {
                Texture2D innerTexture = ModContent.Request<Texture2D>("TheSanity/Projectiles/Moon").Value;
                Vector2 innerOrigin = innerTexture.Size() / 2f; 
                
                float innerRotation = -rotationTimer * 1.2f; 

                // [COLOR LOCATION]: WARNA MOON DALAM (Default .png)
                Color innerColor = Color.White * Projectile.Opacity; 

                // [SCALE LOCATION]: UKURAN MOON DALAM (Pakai settingan kustom kamu)
                float innerScale = 2.1f; 

                Main.EntitySpriteDraw(
                    innerTexture, 
                    drawPos, 
                    null, 
                    innerColor, 
                    innerRotation, 
                    innerOrigin, 
                    innerScale, 
                    SpriteEffects.None, 
                    0
                );
            }

            return false; 
        }
    }
}