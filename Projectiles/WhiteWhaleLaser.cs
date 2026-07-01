using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.WhiteWhale;

namespace TheSanity.Projectiles
{
    public class WhiteWhaleLaser : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/WhiteBeam";

        public const float MaxLaserLength = 2200f;   

        public float CurrentLaserLength {
            get => Projectile.localAI[0];
            set => Projectile.localAI[0] = value;
        }

        public float Timer {
            get => Projectile.ai[1];
            set => Projectile.ai[1] = value;
        }

        public float GetTelegraphDuration() {
            return 0f; 
        }

        public float GetTotalDuration() {
            int ownerIndex = (int)Projectile.ai[0];
            if (ownerIndex >= 0 && ownerIndex < Main.maxNPCs) {
                NPC owner = Main.npc[ownerIndex];
                if (owner.active) {
                    if (owner.type == ModContent.NPCType<WhiteWhaleBoss>() && owner.ai[0] == 1f && owner.ai[2] == 2f) {
                        return 100f; // Menyelaraskan durasi tembakan Fase 1 menjadi 100 frame
                    }
                    if (owner.type == ModContent.NPCType<WhiteWhaleBoss>() && owner.ai[0] == 3f && (WhiteWhaleBoss.P2Attacks)owner.ai[2] == WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle) {
                        return 180f; 
                    }
                    if (owner.type == ModContent.NPCType<WhiteWhaleClone>()) {
                        int parentIdx = (int)owner.ai[0];
                        if (parentIdx >= 0 && parentIdx < Main.maxNPCs) {
                            NPC parent = Main.npc[parentIdx];
                            if (parent.active && parent.type == ModContent.NPCType<WhiteWhaleBoss>() && (WhiteWhaleBoss.P2Attacks)parent.ai[2] == WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle) {
                                return 180f;
                            }
                        }
                    }
                }
            }
            return 100f; 
        }

        public override void SetDefaults() {
            Projectile.width = 26; 
            Projectile.height = 26;
            Projectile.hostile = true;     
            Projectile.friendly = false;   
            Projectile.penetrate = -1;     
            Projectile.tileCollide = false; 
        }

        public override void AI() {
            Timer++;
            float totalDur = GetTotalDuration();

            if (Timer >= totalDur) {
                Projectile.Kill();
                return;
            }

            if (Timer == 1) {
                if (Projectile.ai[0] == 0f || Projectile.ai[0] == -1f) {
                    int closestNPC = -1;
                    float closestDist = 250f; 
                    for (int i = 0; i < Main.maxNPCs; i++) {
                        NPC npc = Main.npc[i];
                        if (npc.active && (npc.type == ModContent.NPCType<WhiteWhaleBoss>() || npc.type == ModContent.NPCType<WhiteWhaleClone>())) {
                            float dist = Vector2.Distance(Projectile.Center, npc.Center);
                            if (dist < closestDist) {
                                closestDist = dist;
                                closestNPC = i;
                            }
                        }
                    }
                    Projectile.ai[0] = closestNPC; 
                }
                Projectile.rotation = Projectile.velocity.ToRotation(); 
            }

            float maxLaserScale = 1.3f; 
            if (Timer < 8f) {
                Projectile.scale = MathHelper.Lerp(0.2f, maxLaserScale, Timer / 8f);
            }
            else if (Timer > totalDur - 8f) {
                Projectile.scale = MathHelper.Lerp(0f, maxLaserScale, (totalDur - Timer) / 8f);
            }
            else {
                Projectile.scale = maxLaserScale;
            }

            int ownerIndex = (int)Projectile.ai[0];
            if (ownerIndex >= 0 && ownerIndex < Main.maxNPCs) {
                NPC owner = Main.npc[ownerIndex];
                if (owner.active) {
                    
                    // PERBAIKAN SEJATI: Menggabungkan koordinat fisik dan offset visual gambar paus
                    if (owner.type == ModContent.NPCType<WhiteWhaleBoss>() && owner.ai[0] == 1f && owner.ai[2] == 2f) {
                        Vector2 visualCenter = owner.Center + new Vector2(-owner.direction * 90f, -10f);
                        Projectile.Center = visualCenter + new Vector2(owner.direction * 105f, 30f);
                        Projectile.rotation = Projectile.velocity.ToRotation();
                    }
                    else if (owner.type == ModContent.NPCType<WhiteWhaleBoss>() && owner.ai[0] == 3f && (WhiteWhaleBoss.P2Attacks)owner.ai[2] == WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle) {
                        Vector2 visualCenter = owner.Center + new Vector2(-owner.direction * 90f, -10f);
                        Projectile.Center = visualCenter + new Vector2(owner.direction * 105f, 30f);
                    }
                    else {
                        Projectile.Center = owner.Center;
                    }

                    // Rotasi khusus orbit segitiga Fase 2
                    if (owner.type == ModContent.NPCType<WhiteWhaleBoss>() && owner.ai[0] == 3f && (WhiteWhaleBoss.P2Attacks)owner.ai[2] == WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle) {
                        float pTimer = owner.ai[3];
                        if (pTimer >= 1 && pTimer <= 180) {
                            Projectile.rotation = (pTimer - 1f) * (MathHelper.TwoPi / 120f);
                        }
                        else if (pTimer > 210 && pTimer <= 390) {
                            Projectile.rotation = -(pTimer - 211f) * (MathHelper.TwoPi / 120f);
                        }
                    }
                    else if (owner.type == ModContent.NPCType<WhiteWhaleClone>()) {
                        int parentIdx = (int)owner.ai[0];
                        if (parentIdx >= 0 && parentIdx < Main.maxNPCs) {
                            NPC parent = Main.npc[parentIdx];
                            if (parent.active && parent.type == ModContent.NPCType<WhiteWhaleBoss>() && (WhiteWhaleBoss.P2Attacks)parent.ai[2] == WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle) {
                                float pTimer = parent.ai[3];
                                int cloneType = (int)owner.ai[1];
                                float offsetAngle = (cloneType == 1) ? MathHelper.TwoPi / 3f : 2f * MathHelper.TwoPi / 3f;
                                
                                if (pTimer >= 1 && pTimer <= 180) {
                                    Projectile.rotation = (pTimer - 1f) * (MathHelper.TwoPi / 120f) + offsetAngle;
                                }
                                else if (pTimer > 210 && pTimer <= 390) {
                                    Projectile.rotation = -(pTimer - 211f) * (MathHelper.TwoPi / 120f) + offsetAngle;
                                }
                            }
                        }
                    }
                }
            }

            CurrentLaserLength = MaxLaserLength;

            if (Main.rand.NextBool(2)) {
                Vector2 beamDir = Projectile.rotation.ToRotationVector2();
                Vector2 dustPos = Projectile.Center + beamDir * Main.rand.NextFloat(100f, CurrentLaserLength);
                Dust dust = Dust.NewDustDirect(dustPos, 0, 0, DustID.PinkTorch, 0f, 0f, 100, default, 1.5f);
                dust.noGravity = true;
                dust.velocity *= 0.5f;
            }
        }

        public override bool CanHitPlayer(Player target) {
            return true;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            Vector2 beamDir = Projectile.rotation.ToRotationVector2();
            float samplePoint = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + (beamDir * CurrentLaserLength), Projectile.width * Projectile.scale, ref samplePoint);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            
            int segmentWidth = texture.Width / 3; 
            Rectangle startSource = new Rectangle(0, 0, segmentWidth, texture.Height);
            Rectangle middleSource = new Rectangle(segmentWidth, 0, segmentWidth, texture.Height);
            Rectangle endSource = new Rectangle(segmentWidth * 2, 0, segmentWidth, texture.Height);

            Vector2 beamDir = Projectile.rotation.ToRotationVector2();
            Vector2 drawOrigin = new Vector2(0, texture.Height / 2f); 

            Color laserPink = new Color(255, 60, 160);
            Color laserWhite = Color.White;

            float distanceCovered = 0f;

            Vector2 startPos = Projectile.Center - Main.screenPosition;
            Color startColor = GetDynamicColor(distanceCovered, laserPink, laserWhite);
            Main.EntitySpriteDraw(texture, startPos, startSource, startColor, Projectile.rotation, drawOrigin, new Vector2(1f, Projectile.scale), SpriteEffects.None, 0);
            distanceCovered += segmentWidth;

            float remainingBodyLength = CurrentLaserLength - (segmentWidth * 2f);
            while (distanceCovered < remainingBodyLength) {
                Vector2 drawPos = Projectile.Center + (beamDir * distanceCovered) - Main.screenPosition;
                Color bodyColor = GetDynamicColor(distanceCovered, laserPink, laserWhite);

                float step = segmentWidth;
                if (distanceCovered + step > remainingBodyLength) {
                    step = remainingBodyLength - distanceCovered;
                }

                Vector2 segmentScale = new Vector2(step / segmentWidth, Projectile.scale);
                Main.EntitySpriteDraw(texture, drawPos, middleSource, bodyColor, Projectile.rotation, drawOrigin, segmentScale, SpriteEffects.None, 0);

                distanceCovered += step;
            }

            Vector2 endPos = Projectile.Center + (beamDir * distanceCovered) - Main.screenPosition;
            Color endColor = GetDynamicColor(distanceCovered, laserPink, laserWhite);
            Main.EntitySpriteDraw(texture, endPos, endSource, endColor, Projectile.rotation, drawOrigin, new Vector2(1f, Projectile.scale), SpriteEffects.None, 0);

            return false; 
        }

        private Color GetDynamicColor(float distance, Color pink, Color white) {
            float wavePhase = (Main.GlobalTimeWrappedHourly * 25f) - (distance * 0.015f);
            float lerpFactor = (float)(Math.Sin(wavePhase) + 1f) / 2f;
            return Color.Lerp(pink, white, lerpFactor) * Projectile.Opacity;
        }
    }
}