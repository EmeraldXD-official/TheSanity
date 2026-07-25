using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    public partial class PlutoHead
    {
        private void ExecuteDashPattern(Player player) {
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

                    Vector2 dashDir = (player.Center - NPC.Center).SafeNormalize(Vector2.Zero); 
                    float distanceToPlayer = Vector2.Distance(NPC.Center, player.Center); 
                    
                    float totalDashDistance = distanceToPlayer + 3200f; 
                    float dashSpeed = 44f; 

                    NPC.velocity = dashDir * dashSpeed; 
                    NPC.rotation = dashDir.ToRotation(); 
                    dashDuration = totalDashDistance / dashSpeed; 

                    if (dashDuration > 150f) dashDuration = 150f; 

                    int soundNum = Main.rand.Next(1, 3); 
                    SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Dash{soundNum}"), NPC.Center); 

                    NPC.ai[3]++; 
                    NPC.netUpdate = true; 
                }
                else {
                    NPC.ai[2] = timer; 
                }
            }
            else if (stage == 1) { 
                NPC.rotation = NPC.velocity.ToRotation(); 
                timer++; 
                if (timer >= (int)dashDuration) { 
                    NPC.ai[2] = 0f; 
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