using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class TimRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void PostAI(NPC npc)
        {
            // --- LOGIKA SI TIM (ID 45) ---
            if (npc.type == 45) 
            {
                // AURA TEBAL: Menggunakan loop lebih banyak (5x per frame) agar terlihat padat
                for (int i = 0; i < 5; i++)
                {
                    float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                    // Radius aura 150 unit
                    Vector2 spawnPos = npc.Center + angle.ToRotationVector2() * 150f;
                    
                    // Kecepatan partikel ditarik masuk ke pusat Tim
                    Vector2 velocity = Vector2.Normalize(npc.Center - spawnPos) * Main.rand.NextFloat(3f, 6f);

                    // DustID.Shadowflame (27) - Dibuat lebih besar (1.7f) agar aura terlihat tebal
                    Dust d = Dust.NewDustPerfect(spawnPos, DustID.Shadowflame, velocity, 100, default, 1.7f);
                    d.noGravity = true;
                    d.fadeIn = 0.5f; // Memberikan efek partikel "tumbuh" saat tersedot
                }

                // Danger Zone (Radius 150 unit)
                Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
                if (target.active && !target.dead && npc.Distance(target.Center) < 150f)
                {
                    target.AddBuff(153, 2); // Shadowflame
                    target.AddBuff(23, 30);  // Cursed
                }
            }

            // --- LOGIKA PELURU TIM (NPC ID 665) ---
            if (npc.type == 665) 
            {
                npc.dontTakeDamage = true; // Kebal
                npc.chaseable = false; 

                // Homing Timer 10 Detik (Lambat tapi menghantui)
                if (npc.ai[2] < 600) 
                {
                    npc.ai[2]++;
                    Player target = Main.player[Player.FindClosest(npc.Center, 1, 1)];
                    
                    if (target != null && target.active && !target.dead)
                    {
                        float speed = 3.5f; 
                        Vector2 desiredVelocity = Vector2.Normalize(target.Center - npc.Center) * speed;
                        npc.velocity = Vector2.Lerp(npc.velocity, desiredVelocity, 0.04f);
                    }
                }

                // Trail Peluru juga dipertebal
                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Shadowflame, 0, 0, 100, default, 1.5f);
                    d.noGravity = true;
                }
            }
        }

        // Menggunakan ModifyHitPlayer agar stabil di semua versi tML 1.4.4
        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            if (npc.type == 665)
            {
                target.AddBuff(153, 180); // Shadowflame 3 detik
            }
        }
    }
}