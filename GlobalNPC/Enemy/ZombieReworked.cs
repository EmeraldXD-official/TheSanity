using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace TheSanity
{
    public class ZombieAuraAI : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private static readonly HashSet<int> ZombieIDs = new HashSet<int>
        {
            -55, -54, -45, -44, -37, -36, -35, -34, -33, -32, -31, -30, -29, -28, -27, -26,
            3, 132, 161, 186, 187, 188, 189, 200, 223, 254, 255, 319, 320, 321, 331, 332,
            430, 431, 432, 433, 434, 435, 436, 489, 586, 590, 591, 632
        };

        public override void PostAI(NPC npc)
        {
            if (ZombieIDs.Contains(npc.type))
            {
                // Radius aura 10 block (160 pixels)
                float auraRadius = 160f;

                // --- VISUAL AURA COKLAT TEBAL (EFEK TERSEDOT - ANTI TEMBUS BLOCK) ---
                for (int i = 0; i < 4; i++)
                {
                    Vector2 spawnPos = npc.Center + Main.rand.NextVector2CircularEdge(auraRadius, auraRadius);

                    // LOKASI CEK COLLISION PARTIKEL: Memastikan titik spawn partikel tidak terhalang block dari pusat zombie
                    if (Collision.CanHitLine(npc.Center, 1, 1, spawnPos, 1, 1))
                    {
                        Vector2 velocity = Vector2.Normalize(npc.Center - spawnPos) * Main.rand.NextFloat(1.5f, 4f); 

                        // DustID.Dirt (0) untuk partikel tanah
                        Dust d = Dust.NewDustPerfect(spawnPos, DustID.Dirt, velocity, 100, default, Main.rand.NextFloat(1f, 1.5f));
                        d.noGravity = true; 
                        d.scale = Main.rand.NextFloat(1.2f, 1.8f);
                    }
                }

                // --- LOGIKA DEBUFF (VENOM & CONFUSION - ANTI TEMBUS BLOCK) ---
                foreach (Player player in Main.player)
                {
                    if (player.active && !player.dead && !player.ghost)
                    {
                        float distance = Vector2.Distance(player.Center, npc.Center);

                        if (distance <= auraRadius)
                        {
                            // LOKASI CEK COLLISION PLAYER: Garis lurus dari pusat zombie ke pusat player harus bebas dari block solid
                            if (Collision.CanHitLine(npc.Center, 1, 1, player.Center, 1, 1))
                            {
                                // 180 ticks = 3 detik
                                // Selama di dalam aura, durasi akan terus di-reset ke 180 (3 detik)
                                player.AddBuff(68, 2);   // Venom
                                player.AddBuff(120, 180); // Confusion
                            }
                        }
                    }
                }
            }
        }
    }
}