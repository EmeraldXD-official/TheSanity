using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    public partial class PlutoHead
    {
        private void ExecuteTrickDashPattern(Player player) {
            int stage = (int)NPC.ai[1]; 
            int timer = (int)NPC.ai[2]; 

            if (stage == 0) { 
                NPC.velocity *= 0.82f; 
                Vector2 targetDir = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero); 
                if (targetDir != Vector2.Zero) { 
                    NPC.rotation = NPC.rotation.AngleLerp(targetDir.ToRotation(), 0.15f); 
                }

                timer++; 
                if (timer >= 30) { 
                    NPC.ai[1] = 1f; 
                    NPC.ai[2] = 0f; 

                    Vector2 baseDir = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero); 
                    Vector2 sideOffsetDir = baseDir.RotatedBy(Main.rand.NextBool() ? MathHelper.PiOver2 : -MathHelper.PiOver2); 
                    Vector2 trickTargetPos = player.Center + sideOffsetDir * 450f; 

                    Vector2 dashDir = (trickTargetPos - NPC.Center).SafeNormalize(Vector2.Zero); 
                    float distanceToTarget = Vector2.Distance(NPC.Center, trickTargetPos); 
                    
                    float totalDashDistance = distanceToTarget + 3200f; 
                    float dashSpeed = 44f; 

                    NPC.velocity = dashDir * dashSpeed; 
                    NPC.rotation = dashDir.ToRotation(); 
                    dashDuration = totalDashDistance / dashSpeed; 

                    if (dashDuration > 150f) dashDuration = 150f; 

                    int soundNum = Main.rand.Next(1, 3); 
                    SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{soundNum}"), NPC.Center); 

                    triggeredProjThisDash = false; 
                    projSequenceActive = false; 
                    projWaveCount = 0; 
                    projSegmentIndex = 0; 
                    projDelayTimer = 0; 

                    NPC.ai[3]++; 
                    NPC.netUpdate = true; 
                }
                else {
                    NPC.ai[2] = timer; 
                }
            }
            else if (stage == 1) { 
                NPC.rotation = NPC.velocity.ToRotation(); 

                if (!triggeredProjThisDash && Vector2.Distance(NPC.Center, player.Center) <= 800f) { 
                    triggeredProjThisDash = true; 
                    projSequenceActive = true; 
                    projWaveCount = 0; 
                    projSegmentIndex = 0; 
                    projDelayTimer = 0; 
                }

                if (projSequenceActive) { 
                    projDelayTimer++; 
                    if (projDelayTimer >= 2) { 
                        projDelayTimer = 0; 

                        if (Main.netMode != NetmodeID.MultiplayerClient) { 
                            for (int i = 0; i < Main.maxNPCs; i++) { 
                                NPC pot = Main.npc[i]; 
                                if (pot.active && pot.ai[3] == NPC.whoAmI && 
                                   (pot.type == ModContent.NPCType<PlutoBody>() || pot.type == ModContent.NPCType<PlutoTail>()) && 
                                   (int)pot.ai[0] == projSegmentIndex) { 
                                    
                                    Vector2 projVel = (player.Center - pot.Center).SafeNormalize(Vector2.Zero) * 9.5f; 
                                    
                                    Projectile.NewProjectile(
                                        NPC.GetSource_FromAI(), 
                                        pot.Center, 
                                        projVel, 
                                        ModContent.ProjectileType<RedMiniNuke>(), 
                                        NPC.damage / 3, 
                                        0f, 
                                        Main.myPlayer 
                                    );
                                    break; 
                                }
                            }
                        }

                        projSegmentIndex++; 
                        if (projSegmentIndex >= 12) { 
                            projSegmentIndex = 0; 
                            projWaveCount++; 
                            if (projWaveCount >= 3) { 
                                projSequenceActive = false; 
                            }
                        }
                    }
                }

                timer++; 
                if (timer >= (int)dashDuration) { 
                    NPC.ai[2] = 0f; 
                    projSequenceActive = false; 
                    
                    if ((int)NPC.ai[3] >= maxDashes) { 
                        NPC.ai[0] = 0f; 
                        NPC.ai[1] = 0f; 
                        NPC.ai[3] = 0f; 
                    } else {
                        NPC.ai[1] = 0f; 
                    }
                    NPC.netUpdate = true; 
                }
                else {
                    NPC.ai[2] = timer; 
                }
            }
        }
    }
}