using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class FireImpRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void PostAI(NPC npc)
        {
            // --- LOGIKA FIRE IMP (ID 24) ---
            if (npc.type == 24) 
            {
                // AURA API TEBAL: Menggunakan DustID.Torch (6) atau DustID.HeatRay untuk warna orange/merah
                for (int i = 0; i < 5; i++)
                {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    Vector2 spawnPos = npc.Center + angle.ToRotationVector2() * 140f;
                    
                    // Kecepatan sedot ke arah badan Fire Imp
                    Vector2 velocity = Vector2.Normalize(npc.Center - spawnPos) * Main.rand.NextFloat(2f, 5f);

                    // DustID.Torch (6) memberikan efek api orange yang terang
                    Dust d = Dust.NewDustPerfect(spawnPos, DustID.Torch, velocity, 100, default, 1.8f);
                    d.noGravity = true;
                }

                // Efek Radius Aura (140 unit)
                Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
                if (target.active && !target.dead && npc.Distance(target.Center) < 140f)
                {
                    target.AddBuff(67, 60); // Debuff 67 (Burning/On Fire!)
                }
            }

            // --- LOGIKA PELURU FIREBALL (NPC ID 25) ---
            if (npc.type == 25) 
            {
                npc.dontTakeDamage = true; // Kebal, tidak bisa ditangkis
                npc.chaseable = false; 

                // Homing Timer 10 Detik (600 frame)
                npc.ai[1]++; 
                if (npc.ai[1] < 600) 
                {
                    Player target = Main.player[Player.FindClosest(npc.Center, 1, 1)];
                    if (target != null && target.active && !target.dead)
                    {
                        float speed = 4.5f; // Fireball biasanya agak cepat
                        Vector2 desiredVelocity = Vector2.Normalize(target.Center - npc.Center) * speed;
                        
                        // Homing lerp (belok halus)
                        npc.velocity = Vector2.Lerp(npc.velocity, desiredVelocity, 0.05f);
                    }
                }
                else
                {
                    // Hancur otomatis setelah 10 detik
                    npc.life = 0;
                    npc.active = false;
                }

                // Trail Api Tebal
                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Torch, 0, 0, 100, default, 1.5f);
                    d.noGravity = true;
                    d.velocity *= 0.5f;
                }
            }
        }

        // Tidak perlu ModifyHitPlayer untuk debuff karena Fireball vanilla sudah kasih "On Fire!"
    }
}