using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class DarkCasterRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void PostAI(NPC npc)
        {
            // --- LOGIKA DARK CASTER (ID 32) ---
            if (npc.type == 32) 
            {
                // AURA AIR TEBAL: Menggunakan DustID.Water (33) atau DustID.MagicMirror (15)
                for (int i = 0; i < 5; i++)
                {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 spawnPos = npc.Center + angle.ToRotationVector2() * 140f;
                    
                    // Kecepatan sedot ke arah badan Dark Caster
                    Vector2 velocity = Vector2.Normalize(npc.Center - spawnPos) * Main.rand.NextFloat(2f, 5f);

                    // DustID.Water (33) atau bisa pakai 172 (biru cerah)
                    Dust d = Dust.NewDustPerfect(spawnPos, 172, velocity, 100, default, 1.6f);
                    d.noGravity = true;
                    d.noLight = false;
                }

                // Efek Radius Aura (140 unit)
                Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
                if (target.active && !target.dead && npc.Distance(target.Center) < 140f)
                {
                    target.AddBuff(44, 60); // Frostburn
                    target.AddBuff(33, 60); // Weak (Serangan player jadi lemah)
                }
            }

            // --- LOGIKA WATER SPHERE (NPC ID 33) ---
            if (npc.type == 33) 
            {
                npc.dontTakeDamage = true; // Kebal, ngga bisa di-block
                npc.chaseable = false; 

                // Homing Timer 10 Detik
                npc.ai[1]++; 
                if (npc.ai[1] < 600) 
                {
                    Player target = Main.player[Player.FindClosest(npc.Center, 1, 1)];
                    if (target != null && target.active && !target.dead)
                    {
                        float speed = 4f; 
                        Vector2 desiredVelocity = Vector2.Normalize(target.Center - npc.Center) * speed;
                        
                        // Homing lerp
                        npc.velocity = Vector2.Lerp(npc.velocity, desiredVelocity, 0.05f);
                    }
                }
                else
                {
                    npc.life = 0;
                    npc.active = false;
                }

                // Trail Air Biru Tebal
                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, 172, 0, 0, 100, default, 1.4f);
                    d.noGravity = true;
                }
            }
        }

        // Memberikan Frostburn (44) saat peluru (33) menabrak player
        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            if (npc.type == 33)
            {
                target.AddBuff(44, 180); // Frostburn selama 3 detik
            }
        }
    }
}