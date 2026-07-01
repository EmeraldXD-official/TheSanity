using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class GoblinSorcererRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void PostAI(NPC npc)
        {
            // --- LOGIKA GOBLIN SORCERER (ID 29) ---
            if (npc.type == 29) 
            {
                // AURA TEBAL: Partikel Shadowflame yang tersedot masuk
                for (int i = 0; i < 5; i++)
                {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 spawnPos = npc.Center + angle.ToRotationVector2() * 140f;
                    
                    // Kecepatan sedot ke pusat goblin
                    Vector2 velocity = Vector2.Normalize(npc.Center - spawnPos) * Main.rand.NextFloat(2f, 5f);

                    Dust d = Dust.NewDustPerfect(spawnPos, DustID.Shadowflame, velocity, 100, default, 1.6f);
                    d.noGravity = true;
                    d.fadeIn = 0.4f;
                }

                // Efek Radius Aura (140 unit)
                Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
                if (target.active && !target.dead && npc.Distance(target.Center) < 140f)
                {
                    target.AddBuff(153, 60); // Shadowflame
                    target.AddBuff(35, 60);  // Darkness (Layar jadi gelap)
                }
            }

            // --- LOGIKA CHAOS BALL GOBLIN (NPC ID 30) ---
            if (npc.type == 30) 
            {
                npc.dontTakeDamage = true; // Tidak bisa dipukul/di-block
                npc.chaseable = false; 

                // Homing Timer 10 Detik (600 frame)
                // Kita pakai ai[1] karena ai[0] biasanya dipakai untuk AI dasar NPC ini
                npc.ai[1]++; 
                if (npc.ai[1] < 600) 
                {
                    Player target = Main.player[Player.FindClosest(npc.Center, 1, 1)];
                    if (target != null && target.active && !target.dead)
                    {
                        float speed = 4f; // Sedikit lebih cepat dari peluru Tim
                        Vector2 desiredVelocity = Vector2.Normalize(target.Center - npc.Center) * speed;
                        
                        // Homing halus
                        npc.velocity = Vector2.Lerp(npc.velocity, desiredVelocity, 0.05f);
                    }
                }
                else
                {
                    // Setelah 10 detik, peluru hancur otomatis agar tidak menumpuk
                    npc.life = 0;
                    npc.active = false;
                }

                // Trail partikel agar "Tebal"
                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Shadowflame, 0, 0, 100, default, 1.4f);
                    d.noGravity = true;
                    d.velocity *= 0.5f;
                }
            }
        }

        // Memberikan Shadowflame saat peluru (30) menabrak player
        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            if (npc.type == 30)
            {
                target.AddBuff(153, 180); // Shadowflame 3 detik
            }
        }
    }
}