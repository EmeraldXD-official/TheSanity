using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class KrakenCthulunado : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Cthulunado;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.Cthulunado];
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            
            Projectile.alpha = 255;
            Projectile.timeLeft = 600; // 10 Detik
        }

        public override void AI()
        {
            float stackIndex = Projectile.ai[2]; // Index 0 s/d 19

            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            float textureWidth = texture.Width;
            float frameHeight = texture.Height / (float)Main.projFrames[Projectile.type];

            // 1. HITUNG SKALA LEBAR (3 Blok di dasar, melebar bertahap hingga tumpukan 20)
            float targetWidthInPixels = (3f + (stackIndex * 0.8f)) * 16f; 
            float currentScale = targetWidthInPixels / textureWidth;
            
            Projectile.scale = currentScale;
            Projectile.width = (int)targetWidthInPixels;
            Projectile.height = (int)(frameHeight * currentScale);

            if (Projectile.localAI[1] == 0)
            {
                Projectile.localAI[1] = 1;

                // Urutan hilang (Despawn): Bawah ke atas
                Projectile.timeLeft = 600 - (int)((19 - stackIndex) * 3);
            }

            // 2. HITUNG POSISI AKUMULATIF (SEAMLESS STACKING)
            float cumulativeY = 0f;
            float h0 = frameHeight * ((3f + 0f) * 16f / textureWidth);
            cumulativeY += h0 * 0.35f;

            for (int i = 1; i <= stackIndex; i++)
            {
                float prevHeight = frameHeight * ((3f + ((i - 1) * 0.8f)) * 16f / textureWidth);
                float currHeight = frameHeight * ((3f + (i * 0.8f)) * 16f / textureWidth);
                
                float spacing = (prevHeight * 0.5f) + (currHeight * 0.5f) - (prevHeight * 0.28f);
                cumulativeY += spacing;
            }

            Projectile.localAI[0]++;
            float timer = Projectile.localAI[0];
            Vector2 basePos = new Vector2(Projectile.ai[0], Projectile.ai[1]);

            // TORNADO TEGAP LURUS
            Projectile.Center = new Vector2(basePos.X, basePos.Y - cumulativeY);

            // 3. LOGIKA SPAWN DELAY & CLEANUP AFTER-IMAGE
            int spawnDelay = (int)stackIndex * 3;

            if (timer < spawnDelay)
            {
                Projectile.friendly = false;
                Projectile.alpha = 255;
                return; 
            }

            if (Projectile.alpha == 255)
            {
                Projectile.alpha = 0;
                for (int k = 0; k < Projectile.oldPos.Length; k++)
                {
                    Projectile.oldPos[k] = Projectile.position;
                }
            }

            Projectile.friendly = true;

            // 4. LOGIKA SEDOTAN SAT-SET 360 DERAJAT (HANYA DIJALANKAN TUMPUKAN DASAR STACK 0)
            if (stackIndex == 0 && Projectile.owner == Main.myPlayer)
            {
                HandleOmniDirectionalSuction(basePos, frameHeight, textureWidth);
            }

            // 5. ANIMASI SPRITE SHEET
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
            }

            // 6. PENCAHAYAAN NEON
            float wave = (MathF.Sin((timer * 0.15f) - (stackIndex * 0.4f)) + 1f) * 0.5f;
            Color neonGlow = Color.Lerp(Color.DeepSkyBlue, Color.LightCyan, wave);
            Lighting.AddLight(Projectile.Center, neonGlow.R / 255f, neonGlow.G / 255f, neonGlow.B / 255f);
        }

        private void HandleOmniDirectionalSuction(Vector2 basePos, float frameHeight, float textureWidth)
        {
            // Kalkulasi Total Tinggi Pilar Tornado
            float totalHeight = 0f;
            float h0 = frameHeight * ((3f + 0f) * 16f / textureWidth);
            totalHeight += h0 * 0.35f;

            for (int i = 1; i <= 19; i++)
            {
                float prevHeight = frameHeight * ((3f + ((i - 1) * 0.8f)) * 16f / textureWidth);
                float currHeight = frameHeight * ((3f + (i * 0.8f)) * 16f / textureWidth);
                float spacing = (prevHeight * 0.5f) + (currHeight * 0.5f) - (prevHeight * 0.28f);
                totalHeight += spacing;
            }

            float bottomY = basePos.Y;
            float topY = basePos.Y - totalHeight;
            float suctionRadius = 50f * 16f; // Jangkauan 50 Block

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];

                if (npc.CanBeChasedBy())
                {
                    // === EKSKLUSI BOSS & SEGMEN BOSS ===
                    // Cek apakah NPC ini boss utama ATAU segmen milik boss (seperti Eater of Worlds / Destroyer)
                    bool isBoss = npc.boss || (npc.realLife >= 0 && Main.npc[npc.realLife].boss);
                    if (isBoss) continue;

                    // Cari titik terdekat pada segmen garis vertikal tornado
                    float clampedY = MathHelper.Clamp(npc.Center.Y, topY, bottomY);
                    Vector2 closestPoint = new Vector2(basePos.X, clampedY);

                    float dist = Vector2.Distance(npc.Center, closestPoint);

                    if (dist < suctionRadius)
                    {
                        float diffX = basePos.X - npc.Center.X;
                        float distFactor = 1f - (dist / suctionRadius);
                        
                        // Kecepatan Tarikan Kilat (15f s/d 35f)
                        float pullSpeed = MathHelper.Lerp(15f, 35f, distFactor);

                        bool isOutsideX = Math.Abs(diffX) > 40f;
                        bool isAboveTornado = npc.Center.Y < topY - 15f;
                        bool isBelowTornado = npc.Center.Y > bottomY + 15f;

                        // A. JIKA MUSUH DI LUAR AREA PUSAT (SISI KANAN/KIRI, DI ATAS PUNCAK, ATAU DI BAWAH DASAR)
                        if (isOutsideX || isAboveTornado || isBelowTornado)
                        {
                            // Tarik Horizontal Kencang
                            if (isOutsideX)
                            {
                                npc.velocity.X = MathHelper.Lerp(npc.velocity.X, Math.Sign(diffX) * pullSpeed, 0.35f);
                            }

                            // Tarik Vertikal Kencang (Sat-Set)
                            if (isAboveTornado)
                            {
                                npc.velocity.Y = MathHelper.Lerp(npc.velocity.Y, pullSpeed, 0.4f);
                            }
                            else if (isBelowTornado)
                            {
                                npc.velocity.Y = MathHelper.Lerp(npc.velocity.Y, -pullSpeed, 0.4f);
                            }
                        }
                        // B. JIKA SUDAH MASUK TEPAT DI DALAM PUSAT TORNADO
                        else
                        {
                            // 1 dari 35 chance per tick: LEMPAR MUSUH SEJAUH 60-70 BLOK
                            if (Main.rand.NextBool(35))
                            {
                                float throwDirection = Main.rand.NextBool() ? 1f : -1f;
                                npc.velocity = new Vector2(throwDirection * 30f, -7f);
                            }
                            // SISANYA: KOCOK KANAN-KIRI
                            else
                            {
                                float shake = MathF.Sin((float)Main.timeForVisualEffects * 0.9f) * 9f;
                                npc.velocity.X = shake;
                                npc.velocity.Y = -1.2f;
                            }
                        }
                    }
                }
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Wet, 240);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (Projectile.alpha >= 255) return false;

            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle frameRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width * 0.5f, frameHeight * 0.5f);

            float stackIndex = Projectile.ai[2];
            float timer = Projectile.localAI[0];

            float wave = (MathF.Sin((timer * 0.15f) - (stackIndex * 0.4f)) + 1f) * 0.5f;
            Color neonColor = Color.Lerp(new Color(0, 191, 255), new Color(175, 238, 239), wave);

            // DRAW AFTER-IMAGE TRAIL
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float alpha = (1f - (i / (float)Projectile.oldPos.Length)) * 0.45f;

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    frameRect,
                    neonColor * alpha,
                    Projectile.oldRot[i],
                    origin,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                );
            }

            // DRAW MAIN SPRITE
            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(
                texture,
                mainPos,
                frameRect,
                neonColor * 0.95f,
                Projectile.rotation,
                origin,
                Projectile.scale, 
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}