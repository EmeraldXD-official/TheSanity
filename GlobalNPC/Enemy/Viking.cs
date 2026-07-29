using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;
using TheSanity.Projectiles;

namespace TheSanity.GlobalNPC.Enemy
{
    public class VikingRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        public int throwTimer = 0; 

        public override bool PreAI(NPC npc)
        {
            if (npc.type != NPCID.UndeadViking && npc.type != NPCID.ArmoredViking) return true;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target != null && !target.dead && npc.HasValidTarget)
            {
                float distance = Vector2.Distance(npc.Center, target.Center);

                throwTimer++;
                if (throwTimer >= 240 && distance < 450f && Collision.CanHitLine(npc.position, npc.width, npc.height, target.position, target.width, target.height))
                {
                    throwTimer = 0; 

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 shootVelocity = (target.Center - npc.Center);
                        shootVelocity.Normalize();
                        shootVelocity *= 8.5f; 

                        // LOKASI ADJUSTMENT DAMAGE BALANCING:
                        // Di Master Mode, damage ini otomatis dikali 3 oleh engine Terraria.
                        // Kita set damage dasarnya ke 66, jadi 66 * 3 = 198 damage (Pas kisaran 200 paling sakit di Master Mode).
                        int damage = 40;

                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVelocity, ModContent.ProjectileType<VikingAx>(), damage, 3f, Main.myPlayer);
                    }

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, npc.Center);
                    npc.netUpdate = true;
                }
            }

            return true; 
        }

        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.ArmoredViking)
            {
                npc.knockBackResist = 0f; 
            }
        }

        public override void ModifyIncomingHit(NPC npc, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.ArmoredViking)
            {
                modifiers.FinalDamage *= 0.70f; 
            }
        }
    }
}