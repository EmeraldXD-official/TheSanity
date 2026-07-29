using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent; // DITAMBAHKAN: Diperlukan untuk memanggil TextureAssets vanilla
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class RingSunProjectile : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/RingSun";

        private Vector2 cachedAltarPos = Vector2.Zero;
        
        private bool shouldDraw = false; 
        
        // Penanda khusus untuk membedakan mode Altar (Default) dan mode Kepala Golem (Enhanced)
        private bool isTrackingHeadFree = false; 

        public override void SetDefaults()
        {
            Projectile.width = 2;       
            Projectile.height = 2;
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
            
            // =========================================================================
            // [SPEED LOCATION]: KECEPATAN ROTASI DASAR UTAMA
            // =========================================================================
            Projectile.rotation += 0.02f; 

            bool trackingGolem = false;

            // 1. KONDISI SAAT GOLEM HIDUP (Mengunci di Kepala Golem)
            int golemIndex = NPC.FindFirstNPC(NPCID.GolemHeadFree);
            if (golemIndex != -1)
            {
                NPC golem = Main.npc[golemIndex];
                if (golem.active)
                {
                    Projectile.Center = golem.Center + new Vector2(0, -12);
                    trackingGolem = true;
                    shouldDraw = true; 
                    isTrackingHeadFree = true; // Aktifkan mode bossfight kepala lepas
                }
            }
            else
            {
                isTrackingHeadFree = false; // Matikan jika kepalanya tidak ada
            }

            // 2. KONDISI TENANG / STANDBY DI ALTAR
            if (!trackingGolem)
            {
                Vector2 altarPos = FindLihzahrdAltar();
                if (altarPos != Vector2.Zero)
                {
                    Vector2 targetAltarCenter = altarPos + new Vector2(24, -35f);
                    float distanceToAltar = Vector2.Distance(Main.LocalPlayer.Center, targetAltarCenter);

                    if (distanceToAltar < 2000f) 
                    {
                        Projectile.Center = targetAltarCenter;
                        shouldDraw = true; 
                    }
                    else
                    {
                        Projectile.Center = Main.LocalPlayer.Center;
                        shouldDraw = false; 
                    }
                }
                else
                {
                    Projectile.Center = Main.LocalPlayer.Center;
                    shouldDraw = false;
                }
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

            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = texture.Size() / 2f;

            Color orangeGlow = Color.Orange * Projectile.Opacity;

            // JIKA SEDANG DI KEPALA GOLEM YANG LEPAS (Double Ring + Skala Lebih Besar)
            if (isTrackingHeadFree)
            {
                // =========================================================================
                // SUB-LAYER 1: CULTIST RITUAL VANILLA (Efek tambahan di belakang kepala Golem)
                // =========================================================================
                Texture2D ritualTexture = TextureAssets.Projectile[ProjectileID.CultistRitual].Value;
                Vector2 ritualOrigin = ritualTexture.Size() / 2f;
                
                // [SPEED LOCATION]: Dibuat berputar terbalik (-) dari putaran utama RingSun
                float ritualRotation = -Projectile.rotation * 0.8f; 
                
                // [SCALE LOCATION]: Skala Cultist Ritual luar pas di kepala Golem
                float ritualScale = 0.55f; 

                Main.EntitySpriteDraw(
                    ritualTexture,
                    drawPos,
                    null,
                    orangeGlow, // Menggunakan warna yang sama persis (Orange) sesuai request-mu
                    ritualRotation,
                    ritualOrigin,
                    ritualScale,
                    SpriteEffects.None,
                    0
                );

                // =========================================================================
                // SUB-LAYER 2: RING SUN UTAMA (Diperbesar sedikit saat fase lepas kepala)
                // =========================================================================
                // [SCALE LOCATION]: Skala RingSun saat bossfight (Dinaikkan dari default 1.0f ke 1.3f)
                float sunBossScale = 1.3f; 

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    null,
                    orangeGlow,
                    Projectile.rotation,
                    origin,
                    sunBossScale, // Ukuran lebih besar sedikit
                    SpriteEffects.None,
                    0
                );
            }
            else
            {
                // =========================================================================
                // KONDISI DEFAULT (Saat standby di Altar - Visual dibiarkan original murni)
                // =========================================================================
                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    null,
                    orangeGlow,
                    Projectile.rotation,
                    origin,
                    1.0f, // Tetap menggunakan skala bawaan pabrik
                    SpriteEffects.None,
                    0
                );
            }

            return false; 
        }
    }
}