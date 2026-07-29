using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.Audio;

namespace TheSanity.Projectiles
{
    public class GolemHealChain : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Bullet;

        private Vector2 targetBlockPos = Vector2.Zero;
        private Vector2 spawnDirection = Vector2.Zero; 
        private bool initialScanDone = false;
        private float deployProgress = 0f;
        private int soundCooldown = 0;

        private float[] segmentSway = new float[120];
        private float[] segmentVelocity = new float[120];
        private int totalSegments = 0;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = -1;
            Projectile.hostile = false;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
            Projectile.hide = true; 
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> overPlayers, List<int> overWiresUI)
        {
            behindNPCs.Add(index);
        }

        public override void AI()
        {
            int parentIndex = (int)Projectile.ai[1];
            
            // FIX: Diperbarui agar mendukung GolemHeadFree, GolemHead, ataupun Golem Body (Tubuh)
            if (parentIndex < 0 || parentIndex >= Main.maxNPCs || !Main.npc[parentIndex].active || 
                (Main.npc[parentIndex].type != NPCID.GolemHeadFree && 
                 Main.npc[parentIndex].type != NPCID.GolemHead && 
                 Main.npc[parentIndex].type != NPCID.Golem))
            {
                Projectile.Kill();
                return;
            }

            NPC head = Main.npc[parentIndex];
            Projectile.Center = head.Center;
            Projectile.timeLeft = 10;

            if (!initialScanDone)
            {
                initialScanDone = true;
                float angle = 0f;
                switch ((int)Projectile.ai[0])
                {
                    case 0: angle = MathHelper.ToRadians(225); break;
                    case 1: angle = MathHelper.ToRadians(315); break;
                    case 2: angle = MathHelper.ToRadians(135); break;
                    case 3: angle = MathHelper.ToRadians(45);  break;
                }
                spawnDirection = angle.ToRotationVector2();
                Vector2 currentCheckPos = head.Center;
                for (int i = 0; i < 100; i++)
                {
                    currentCheckPos += spawnDirection * 16f;
                    if (Collision.SolidCollision(currentCheckPos - new Vector2(8), 16, 16))
                    {
                        targetBlockPos = currentCheckPos;
                        break;
                    }
                }
                if (targetBlockPos == Vector2.Zero) targetBlockPos = head.Center + spawnDirection * 1200f;
                float totalDistance = Vector2.Distance(head.Center, targetBlockPos);
                totalSegments = Math.Min((int)(totalDistance / 14f), 119);
            }

            if (deployProgress < 1f)
            {
                deployProgress += 0.04f;
                if (deployProgress > 1f) deployProgress = 1f;
                soundCooldown++;
                if (soundCooldown % 4 == 0) SoundEngine.PlaySound(SoundID.Tink, head.Center);
            }

            // --- FISIKA RANTAI ---
            Vector2 currentEndPos = Vector2.Lerp(head.Center, targetBlockPos, deployProgress);
            Vector2 lineVector = currentEndPos - head.Center;
            float lineLength = lineVector.Length();

            if (lineLength > 1f && totalSegments > 0)
            {
                Vector2 normalizedLine = lineVector / lineLength;
                Vector2 perpendicularVector = new Vector2(-normalizedLine.Y, normalizedLine.X);

                for (int i = 0; i < totalSegments; i++)
                {
                    float segmentRatio = (float)i / totalSegments;
                    Vector2 baseSegmentPos = head.Center + lineVector * segmentRatio;

                    for (int p = 0; p < Main.maxPlayers; p++)
                    {
                        Player player = Main.player[p];
                        if (player.active && !player.dead && Vector2.Distance(player.Center, baseSegmentPos) < 24f)
                        {
                            float pushForce = Vector2.Dot(player.velocity, perpendicularVector);
                            if (Math.Abs(pushForce) < 1f) pushForce = player.direction * 4f;
                            segmentVelocity[i] += pushForce * 0.5f; 
                        }
                    }

                    segmentVelocity[i] -= segmentSway[i] * 0.05f; 
                    segmentVelocity[i] *= 0.90f; 
                    segmentSway[i] += segmentVelocity[i];
                }

                for (int i = 0; i < 3; i++) 
                {
                    for (int j = 1; j < totalSegments - 1; j++) 
                    {
                        segmentSway[j] = (segmentSway[j - 1] + segmentSway[j] + segmentSway[j + 1]) / 3f;
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int parentIndex = (int)Projectile.ai[1];
            if (parentIndex == -1 || !Main.npc[parentIndex].active) return false;
            
            NPC head = Main.npc[parentIndex];
            Texture2D chainTexture = TextureAssets.Chain21.Value;
            
            Vector2 actualTarget = targetBlockPos + (spawnDirection * 32f); 
            Vector2 currentEndPos = Vector2.Lerp(head.Center, actualTarget, deployProgress);
            
            Vector2 directionVector = currentEndPos - head.Center;
            float maxDistance = directionVector.Length();
            if (maxDistance <= 0f || totalSegments <= 0) return false;

            Vector2 directionNormalized = directionVector / maxDistance;
            Vector2 perpendicularVector = new Vector2(-directionNormalized.Y, directionNormalized.X);
            float rotation = directionNormalized.ToRotation() + MathHelper.PiOver2;

            Vector2 currentDrawPos = head.Center;
            float stepSize = maxDistance / totalSegments;

            for (int i = 0; i < totalSegments; i++)
            {
                Vector2 dynamicDrawPos = currentDrawPos + perpendicularVector * segmentSway[i];
                Color segmentColor = Lighting.GetColor((int)(dynamicDrawPos.X / 16f), (int)(dynamicDrawPos.Y / 16f));

                Main.EntitySpriteDraw(chainTexture, dynamicDrawPos - Main.screenPosition, null, Projectile.GetAlpha(segmentColor), rotation, new Vector2(chainTexture.Width * 0.5f, chainTexture.Height * 0.5f), 1f, SpriteEffects.None, 0);
                
                currentDrawPos += directionNormalized * stepSize;
            }
            return false;
        }
    }
}