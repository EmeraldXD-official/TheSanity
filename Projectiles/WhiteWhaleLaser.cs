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

        // Properti dinamis untuk menentukan durasi bidik berdasarkan keadaan serangan unit pemiliknya
        public float GetTelegraphDuration() {
            int ownerIndex = (int)Projectile.ai[0];
            if (ownerIndex >= 0 && ownerIndex < Main.maxNPCs) {
                NPC owner = Main.npc[ownerIndex];
                if (owner.active) {
                    if (owner.type == ModContent.NPCType<global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss>() && owner.ai[0] == 3f && (global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss.P2Attacks)owner.ai[2] == global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle) {
                        return (owner.ai[3] > 210) ? 0f : 40f; 
                    }
                    if (owner.type == ModContent.NPCType<global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleClone>()) {
                        int parentIdx = (int)owner.ai[0];
                        if (parentIdx >= 0 && parentIdx < Main.maxNPCs) {
                            NPC parent = Main.npc[parentIdx];
                            if (parent.active && parent.type == ModContent.NPCType<global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss>() && (global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss.P2Attacks)parent.ai[2] == global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle) {
                                return (parent.ai[3] > 210) ? 0f : 40f;
                            }
                        }
                    }
                }
            }
            return 35f; 
        }

        // Properti dinamis untuk menentukan durasi total keaktifan laser secara presisi
        public float GetTotalDuration() {
            int ownerIndex = (int)Projectile.ai[0];
            if (ownerIndex >= 0 && ownerIndex < Main.maxNPCs) {
                NPC owner = Main.npc[ownerIndex];
                if (owner.active) {
                    if (owner.type == ModContent.NPCType<global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss>() && owner.ai[0] == 3f && (global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss.P2Attacks)owner.ai[2] == global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle) {
                        return 180f; 
                    }
                    if (owner.type == ModContent.NPCType<global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleClone>()) {
                        int parentIdx = (int)owner.ai[0];
                        if (parentIdx >= 0 && parentIdx < Main.maxNPCs) {
                            NPC parent = Main.npc[parentIdx];
                            if (parent.active && parent.type == ModContent.NPCType<global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss>() && (global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss.P2Attacks)parent.ai[2] == global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle) {
                                return 180f;
                            }
                        }
                    }
                }
            }
            return 35f + 45f; 
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
            float telegDur = GetTelegraphDuration();

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
                        if (npc.active && (npc.type == ModContent.NPCType<global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss>() || npc.type == ModContent.NPCType<global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleClone>())) {
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

            float telegraphScale = 0.2f; 
            float maxLaserScale = 1.3f; 
            
            if (Timer < telegDur) {
                Projectile.scale = telegraphScale;
            }
            else {
                float laserTimer = Timer - telegDur;
                float growDuration = 8f;   
                float shrinkDuration = 8f; 
                
                if (laserTimer < growDuration) {
                    Projectile.scale = MathHelper.Lerp(telegraphScale, maxLaserScale, laserTimer / growDuration);
                }
                else if (Timer > totalDur - shrinkDuration) {
                    Projectile.scale = MathHelper.Lerp(0f, maxLaserScale, (totalDur - Timer) / shrinkDuration);
                }
                else {
                    Projectile.scale = maxLaserScale;
                }
            }

            int ownerIndex = (int)Projectile.ai[0];
            if (ownerIndex >= 0 && ownerIndex < Main.maxNPCs) {
                NPC owner = Main.npc[ownerIndex];
                if (owner.active) {
                    Projectile.Center = owner.Center; 

                    bool isTrackingLaser = (owner.type == ModContent.NPCType<global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss>() && owner.ai[0] == 1f && owner.ai[2] == 1f);

                    if (Timer < telegDur) {
                        if (isTrackingLaser && owner.target >= 0 && owner.target < 255) {
                            Player playerTarget = Main.player[owner.target];
                            Projectile.rotation = (playerTarget.Center - Projectile.Center).ToRotation();
                        }
                    }

                    // KONDISI UTAMA SINKRONISASI ROTASI PERMANEN MENGIKUTI KECEPATAN ORBIT UNIT
                    if (owner.type == ModContent.NPCType<global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss>() && owner.ai[0] == 3f && (global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss.P2Attacks)owner.ai[2] == global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle) {
                        float pTimer = owner.ai[3];
                        if (pTimer >= 1 && pTimer <= 180) {
                            Projectile.rotation = (pTimer - 1f) * (MathHelper.TwoPi / 120f);
                        }
                        else if (pTimer > 210 && pTimer <= 390) {
                            Projectile.rotation = -(pTimer - 211f) * (MathHelper.TwoPi / 120f);
                        }
                    }
                    else if (owner.type == ModContent.NPCType<global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleClone>()) {
                        int parentIdx = (int)owner.ai[0];
                        if (parentIdx >= 0 && parentIdx < Main.maxNPCs) {
                            NPC parent = Main.npc[parentIdx];
                            if (parent.active && parent.type == ModContent.NPCType<global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss>() && (global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss.P2Attacks)parent.ai[2] == global::TheSanity.GlobalNPC.Bosses.WhiteWhale.WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle) {
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

            if (Timer >= telegDur && Main.rand.NextBool(2)) {
                Vector2 beamDir = Projectile.rotation.ToRotationVector2();
                Vector2 dustPos = Projectile.Center + beamDir * Main.rand.NextFloat(100f, CurrentLaserLength);
                Dust dust = Dust.NewDustDirect(dustPos, 0, 0, DustID.PinkTorch, 0f, 0f, 100, default, 1.5f);
                dust.noGravity = true;
                dust.velocity *= 0.5f;
            }
        }

        public override bool CanHitPlayer(Player target) {
            if (Timer < GetTelegraphDuration()) return false; 
            return true;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) {
            if (Timer < GetTelegraphDuration()) return false;

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
            float telegDur = GetTelegraphDuration();
            if (telegDur > 0f && Timer < telegDur) {
                float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 30f) * 0.2f + 0.5f;
                return pink * pulse * (Timer / telegDur);
            }
            
            float wavePhase = (Main.GlobalTimeWrappedHourly * 25f) - (distance * 0.015f);
            float lerpFactor = (float)(Math.Sin(wavePhase) + 1f) / 2f;
            
            return Color.Lerp(pink, white, lerpFactor) * Projectile.Opacity;
        }
    }
}