using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    // 1. MODIFIKASI TORNADO (Spawn 1 Shark Saja)
    public class SandnadoRework : GlobalProjectile
    {
        public bool hasSpawnedShark = false;
        public override bool InstancePerEntity => true;

        public override void PostAI(Projectile projectile)
        {
            // ID 658 = SandnadoHostile
            if (projectile.type == 658)
            {
                // Munculkan hanya 1 Shark per tornado
                if (!hasSpawnedShark && projectile.timeLeft < 580) 
                {
                    hasSpawnedShark = true;

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // Pilih ID Shark secara acak
                        int[] sharkIDs = { 542, 543, 544, 545 };
                        int selectedID = Main.rand.Next(sharkIDs);

                        Vector2 spawnPos = projectile.Center;
                        int n = NPC.NewNPC(projectile.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, selectedID);
                        
                        if (Main.npc[n].active)
                        {
                            // Efek terlempar keluar dari tornado
                            Main.npc[n].velocity = new Vector2(Main.rand.NextFloat(-4f, 4f), -8f);
                            Main.npc[n].netUpdate = true;
                        }
                    }
                }
            }
        }
    }

    // 2. MODIFIKASI SAND ELEMENTAL (5 Proyektil per Detik)
    public class SandElementalAura : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        public int auraTimer = 0;

        public override void PostAI(NPC npc)
        {
            if (npc.type == 541) // Sand Elemental
            {
                auraTimer++;

                // LOKASI RATE: 60 frame / 12 = 5 kali dalam 1 detik (Setiap 12 frame keluar 1)
                if (auraTimer % 12 == 0)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        float radius = 160f; // 10 Blocks
                        Vector2 randomPos = npc.Center + Main.rand.NextVector2Circular(radius, radius);

                        // Spawn Proyektil 596
                        int p = Projectile.NewProjectile(npc.GetSource_FromAI(), randomPos, Vector2.Zero, 596, 35, 1f, Main.myPlayer);
                        
                        if (Main.projectile[p].active)
                        {
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                            
                            // Visual peringatan (Dust Ungu/Shadowflame)
                            for (int i = 0; i < 5; i++)
                            {
                                Dust d = Dust.NewDustDirect(randomPos, 0, 0, DustID.Shadowflame, 0, 0, 100, default, 1.2f);
                                d.noGravity = true;
                                d.velocity *= 1.5f;
                            }
                        }
                    }
                }
                
                // Reset timer tiap detik agar tidak menumpuk angkanya
                if (auraTimer >= 60) auraTimer = 0;
            }
        }
    }
}