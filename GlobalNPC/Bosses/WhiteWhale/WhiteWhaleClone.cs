using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio; 

namespace TheSanity.GlobalNPC.Bosses.WhiteWhale
{
    public class WhiteWhaleClone : ModNPC
    {
        public int ParentIndex {
            get => (int)NPC.ai[0];
            set => NPC.ai[0] = value;
        }
        public int CloneType {
            get => (int)NPC.ai[1]; 
            set => NPC.ai[1] = value;
        }
        private float telegraphRotation = 0f;

        public override void SetStaticDefaults() {
            Main.npcFrameCount[NPC.type] = 3; 
            NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(NPC.type, value); 

            NPCID.Sets.TrailCacheLength[NPC.type] = 5;
            NPCID.Sets.TrailingMode[NPC.type] = 3;
        }

        public override void SetDefaults() {
            NPC.width = 276; 
            NPC.height = 95;
            NPC.damage = 100;
            NPC.defense = 40;
            NPC.lifeMax = 10000; 
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath10;
            NPC.dontTakeDamage = true; // Clone kebal dari awal
        }

        public override void FindFrame(int frameHeight) {
            NPC.spriteDirection = -NPC.direction;
            bool useFrameThree = false;

            if (ParentIndex >= 0 && ParentIndex < Main.maxNPCs) {
                NPC parent = Main.npc[ParentIndex];
                if (parent.active && parent.type == ModContent.NPCType<WhiteWhaleBoss>()) {
                    float pState = parent.ai[2];
                    float pTimer = parent.ai[3];
                    
                    if (pState <= (float)WhiteWhaleBoss.P2Attacks.Dash_Letter_H && pTimer > 50f && NPC.velocity.Length() > 8f) {
                        useFrameThree = true;
                    }
                    else if (pState == (float)WhiteWhaleBoss.P2Attacks.PredictiveSequentialDash && NPC.velocity.Length() > 8f) {
                        useFrameThree = true;
                    }
                    
                    if ((WhiteWhaleBoss.P2Attacks)pState == WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle) {
                        useFrameThree = true;
                    }
                }
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
        }

        public override void AI() {
            NPC parent = Main.npc[ParentIndex];
            if (!parent.active || parent.type != ModContent.NPCType<WhiteWhaleBoss>()) {
                NPC.active = false; 
                return;
            }

            NPC.timeLeft = 3600;
            Player target = Main.player[parent.target];
            
            WhiteWhaleBoss.P2Attacks parentAttackState = (WhiteWhaleBoss.P2Attacks)parent.ai[2];
            float parentAttackTimer = parent.ai[3];

            // LOGIKA SILUET & INVINCIBLE CLONE
            bool isRepositioning = false;
            
            if (parentAttackState >= WhiteWhaleBoss.P2Attacks.Dash_Letter_X && parentAttackState <= WhiteWhaleBoss.P2Attacks.Dash_Letter_H && parentAttackTimer <= 50) {
                isRepositioning = true;
            }
            else if (parentAttackState == WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle && parentAttackTimer > 180 && parentAttackTimer <= 210) {
                isRepositioning = true;
            }

            if (isRepositioning) {
                NPC.localAI[3] = 1f; // Flag siluet
                NPC.damage = 0;
            } else {
                NPC.localAI[3] = 0f; // Matikan siluet
                NPC.damage = 50;
            }

            NPC.direction = NPC.Center.X < target.Center.X ? 1 : -1;

            switch (parentAttackState) {
                case WhiteWhaleBoss.P2Attacks.Dash_Letter_X:
                    if (parentAttackTimer <= 50) {
                        Vector2 readyPos = (CloneType == 1) ? target.Center + new Vector2(700f, -700f) : target.Center + new Vector2(-700f, 0f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        telegraphRotation = (CloneType == 1) ? (target.Center + new Vector2(-700f, 700f) - NPC.Center).ToRotation() : 0f;
                    }
                    else if (parentAttackTimer == 51) {
                        NPC.velocity = telegraphRotation.ToRotationVector2() * 52f;
                    }
                    else if (parentAttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }
                    break;

                case WhiteWhaleBoss.P2Attacks.Dash_Letter_Y:
                    if (parentAttackTimer <= 50) {
                        Vector2 readyPos = (CloneType == 1) ? target.Center + new Vector2(650f, -375f) : target.Center + new Vector2(0f, 750f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        telegraphRotation = (CloneType == 1) ? MathHelper.ToRadians(150f) : -MathHelper.PiOver2;
                    }
                    else if (parentAttackTimer == 51) {
                        NPC.velocity = telegraphRotation.ToRotationVector2() * 52f; 
                    }
                    else if (parentAttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }
                    break;

                case WhiteWhaleBoss.P2Attacks.Dash_Letter_A:
                    if (parentAttackTimer <= 50) {
                        Vector2 readyPos = target.Center + new Vector2(0f, -600f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        Vector2 dashTarget = (CloneType == 1) ? target.Center + new Vector2(-500f, 400f) : target.Center + new Vector2(500f, 400f);
                        telegraphRotation = (dashTarget - NPC.Center).ToRotation();
                    }
                    else if (parentAttackTimer == 51) {
                        NPC.velocity = telegraphRotation.ToRotationVector2() * 48f;
                    }
                    else if (parentAttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }
                    break;

                case WhiteWhaleBoss.P2Attacks.Dash_Letter_Z:
                    if (parentAttackTimer <= 50) {
                        Vector2 readyPos = (CloneType == 1) ? target.Center + new Vector2(600f, -400f) : target.Center + new Vector2(-600f, 400f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        telegraphRotation = (CloneType == 1) ? (target.Center + new Vector2(-600f, 400f) - NPC.Center).ToRotation() : 0f;
                    }
                    else if (parentAttackTimer == 51) {
                        NPC.velocity = telegraphRotation.ToRotationVector2() * 50f;
                    }
                    else if (parentAttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }
                    break;

                case WhiteWhaleBoss.P2Attacks.Dash_Letter_T:
                    if (parentAttackTimer <= 50) {
                        Vector2 readyPos = (CloneType == 1) ? target.Center + new Vector2(600f, -400f) : target.Center + new Vector2(0f, -500f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        telegraphRotation = (CloneType == 1) ? MathHelper.Pi : MathHelper.PiOver2;
                    }
                    else if (parentAttackTimer == 51) {
                        NPC.velocity = telegraphRotation.ToRotationVector2() * 50f;
                    }
                    else if (parentAttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }
                    break;

                case WhiteWhaleBoss.P2Attacks.Dash_Letter_N:
                    if (parentAttackTimer <= 50) {
                        Vector2 readyPos = (CloneType == 1) ? target.Center + new Vector2(400f, -500f) : target.Center + new Vector2(-400f, -500f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        
                        if (CloneType == 1) {
                            telegraphRotation = MathHelper.PiOver2;
                        } else {
                            Vector2 dashTarget = target.Center + new Vector2(400f, 500f);
                            telegraphRotation = (dashTarget - NPC.Center).ToRotation();
                        }
                    }
                    else if (parentAttackTimer == 51) {
                        NPC.velocity = telegraphRotation.ToRotationVector2() * 52f;
                    }
                    else if (parentAttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }
                    break;

                case WhiteWhaleBoss.P2Attacks.Dash_Letter_H:
                    if (parentAttackTimer <= 50) {
                        Vector2 readyPos = (CloneType == 1) ? target.Center + new Vector2(400f, 500f) : target.Center + new Vector2(-500f, 0f);
                        NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.15f, 0.1f);
                        telegraphRotation = (CloneType == 1) ? -MathHelper.PiOver2 : 0f;
                    }
                    else if (parentAttackTimer == 51) {
                        NPC.velocity = telegraphRotation.ToRotationVector2() * 50f;
                    }
                    else if (parentAttackTimer >= 75) {
                        NPC.velocity *= 0.82f;
                    }
                    break;

                case WhiteWhaleBoss.P2Attacks.RotatingLaserTriangle:
                    float radius = 500f;
                    float offsetAngle = (CloneType == 1) ? MathHelper.TwoPi / 3f : 2f * MathHelper.TwoPi / 3f;

                    Vector2 centerPoint = target.Center;
                    if (parent.ModNPC is WhiteWhaleBoss bossInstance && bossInstance.rotatingCenter != Vector2.Zero) {
                        centerPoint = bossInstance.rotatingCenter;
                    }

                    Vector2 mouthPos = NPC.Center + new Vector2(NPC.direction * 110f, 15f);

                    if (parentAttackTimer >= 1 && parentAttackTimer <= 180) {
                        float angle = (parentAttackTimer - 1f) * (MathHelper.TwoPi / 120f) + offsetAngle;
                        Vector2 targetOrbitPos = centerPoint + angle.ToRotationVector2() * radius;
                        NPC.velocity = (targetOrbitPos - NPC.Center) * 0.25f;

                        if (parentAttackTimer > 40 && parentAttackTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 laserVel = angle.ToRotationVector2() * 8f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), mouthPos, laserVel, ModContent.ProjectileType<Projectiles.WhiteWhaleLaser>(), 100, 0f, Main.myPlayer);
                        }
                    }
                    else if (parentAttackTimer > 180 && parentAttackTimer <= 210) {
                        NPC.velocity *= 0.8f;
                    }
                    else if (parentAttackTimer > 210 && parentAttackTimer <= 390) {
                        float angle = -(parentAttackTimer - 211f) * (MathHelper.TwoPi / 120f) + offsetAngle;
                        Vector2 targetOrbitPos = centerPoint + angle.ToRotationVector2() * radius;
                        NPC.velocity = (targetOrbitPos - NPC.Center) * 0.25f;

                        if (parentAttackTimer % 15 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                            Vector2 laserVel = angle.ToRotationVector2() * 8f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), mouthPos, laserVel, ModContent.ProjectileType<Projectiles.WhiteWhaleLaser>(), 100, 0f, Main.myPlayer);
                        }
                    }
                    break;

                case WhiteWhaleBoss.P2Attacks.PredictiveSequentialDash:
                    if (CloneType == 1) {
                        if (parentAttackTimer >= 81 && parentAttackTimer <= 125) {
                            Vector2 readyPos = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY * -1) * 800f;
                            NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.1f, 0.1f);
                            telegraphRotation = (target.Center + target.velocity * 25f - NPC.Center).ToRotation();
                        }
                        else if (parentAttackTimer == 126) {
                            NPC.velocity = telegraphRotation.ToRotationVector2() * 52f;
                            SoundEngine.PlaySound(new SoundStyle("TheSanity/Music/WhaleDash"), NPC.position);
                        }
                        else if (parentAttackTimer >= 151 && parentAttackTimer <= 160) {
                            NPC.velocity *= 0.75f;
                        }
                    }

                    if (CloneType == 2) {
                        if (parentAttackTimer >= 161 && parentAttackTimer <= 205) {
                            Vector2 readyPos = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.UnitY * -1) * 800f;
                            NPC.velocity = Vector2.Lerp(NPC.velocity, (readyPos - NPC.Center) * 0.1f, 0.1f);
                            telegraphRotation = (target.Center + target.velocity * 25f - NPC.Center).ToRotation();
                        }
                        else if (parentAttackTimer == 206) {
                            NPC.velocity = telegraphRotation.ToRotationVector2() * 52f;
                            SoundEngine.PlaySound(new SoundStyle("TheSanity/Music/WhaleDash"), NPC.position);
                        }
                        else if (parentAttackTimer >= 231 && parentAttackTimer <= 240) {
                            NPC.velocity *= 0.75f;
                        }
                    }
                    break;
            }

            float maxDistance = 120f * 16f; 
            if (Vector2.Distance(NPC.Center, target.Center) > maxDistance) {
                NPC.Center = target.Center + (NPC.Center - target.Center).SafeNormalize(Vector2.Zero) * maxDistance;
                NPC.velocity *= 0.5f;
            }

            if (!Main.dedServ) {
                // Tahan memunculkan debu kalau sedang mode siluet
                if (NPC.localAI[3] == 0f) {
                    for (int i = 0; i < 4; i++) {
                        float angle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
                        Vector2 haloOffset = angle.ToRotationVector2() * new Vector2(145f, 52f) + new Vector2(NPC.spriteDirection * 90f, -10f); 
                        
                        int dustType = Main.rand.NextBool() ? DustID.PinkTorch : DustID.WhiteTorch;
                        int d = Dust.NewDust(NPC.Center + haloOffset, 0, 0, dustType, 0f, 0f, 60, default, Main.rand.NextFloat(1.4f, 2.2f));
                        Main.dust[d].noGravity = true;
                        Main.dust[d].velocity = NPC.velocity * 0.4f + Main.rand.NextVector2Circular(1f, 1f);
                    }

                    if (NPC.velocity.Length() > 20f) {
                        for (int i = 0; i < 16; i++) {
                            int dustType = Main.rand.NextBool() ? DustID.PinkTorch : DustID.WhiteTorch;
                            Vector2 backOffset = -NPC.velocity.SafeNormalize(Vector2.Zero) * 120f;
                            Vector2 spawnPos = NPC.Center + backOffset + new Vector2(NPC.spriteDirection * 90f, -10f) + Main.rand.NextVector2Circular(50f, 50f);
                            
                            int d = Dust.NewDust(spawnPos, 0, 0, dustType, 0f, 0f, 40, default, Main.rand.NextFloat(1.8f, 2.6f));
                            Main.dust[d].noGravity = true;
                            Main.dust[d].velocity = -NPC.velocity * Main.rand.NextFloat(0.2f, 0.4f) + Main.rand.NextVector2Circular(4f, 4f);
                        }
                    }
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            float visualOffsetY = -10f; 
            float visualOffsetX = NPC.spriteDirection * 90f;
            Vector2 visualOffset = new Vector2(visualOffsetX, visualOffsetY);

            if (ParentIndex >= 0 && ParentIndex < Main.maxNPCs) {
                NPC parent = Main.npc[ParentIndex];
                Texture2D blankTexture = TextureAssets.MagicPixel.Value;

                if (parent.active && parent.type == ModContent.NPCType<WhiteWhaleBoss>()) {
                    WhiteWhaleBoss.P2Attacks parentState = (WhiteWhaleBoss.P2Attacks)parent.ai[2];
                    float parentAttackTimer = parent.ai[3];
                    
                    if (parentState <= WhiteWhaleBoss.P2Attacks.Dash_Letter_H && parentAttackTimer <= 50) {
                        float opacity = parentAttackTimer / 50f;
                        spriteBatch.Draw(blankTexture, NPC.Center - screenPos, new Rectangle(0, 0, 1, 1), Color.Red * opacity * 0.5f, telegraphRotation, new Vector2(0, 0.5f), new Vector2(2400f, 80f), SpriteEffects.None, 0f);
                    }
                    else if (parentState == WhiteWhaleBoss.P2Attacks.PredictiveSequentialDash) {
                        if (CloneType == 1 && parentAttackTimer >= 81 && parentAttackTimer <= 125) {
                            float opacity = (parentAttackTimer - 81f) / 44f;
                            spriteBatch.Draw(blankTexture, NPC.Center - screenPos, new Rectangle(0, 0, 1, 1), Color.Orange * opacity * 0.7f, telegraphRotation, new Vector2(0, 0.5f), new Vector2(2400f, 80f), SpriteEffects.None, 0f);
                        }
                        else if (CloneType == 2 && parentAttackTimer >= 161 && parentAttackTimer <= 205) {
                            float opacity = (parentAttackTimer - 161f) / 44f;
                            spriteBatch.Draw(blankTexture, NPC.Center - screenPos, new Rectangle(0, 0, 1, 1), Color.Orange * opacity * 0.7f, telegraphRotation, new Vector2(0, 0.5f), new Vector2(2400f, 80f), SpriteEffects.None, 0f);
                        }
                    }
                }
            }

            Texture2D cloneTexture = TextureAssets.Npc[NPC.type].Value;
            Vector2 origin = NPC.frame.Size() / 2f;
            SpriteEffects cloneEffects = NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Color alphaColor = (NPC.localAI[3] == 1f) ? Color.Black * 0.7f : (drawColor * NPC.Opacity);

            for (int i = 1; i < NPC.oldPos.Length; i++) {
                if (NPC.oldPos[i] == Vector2.Zero) continue;

                Color shadowColor = alphaColor * ((NPC.oldPos.Length - i) / (float)NPC.oldPos.Length) * 0.35f;
                Vector2 drawPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos + visualOffset;
                float oldRot = NPC.oldRot[i];

                spriteBatch.Draw(cloneTexture, drawPos, NPC.frame, shadowColor, oldRot, origin, 1.0f, cloneEffects, 0f);
            }

            Vector2 mainDrawPos = NPC.Center - screenPos + visualOffset;
            spriteBatch.Draw(cloneTexture, mainDrawPos, NPC.frame, alphaColor, NPC.rotation, origin, 1.0f, cloneEffects, 0f);

            return false; 
        }
    }
}