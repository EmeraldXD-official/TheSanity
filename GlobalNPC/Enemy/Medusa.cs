using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class MedusaRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void PostAI(NPC npc)
        {
            // Cek apakah NPC ini adalah Medusa
            if (npc.type != NPCID.Medusa) return;

            // --- 1. LOGIKA AURA (5 BLOCK = 80 PIXEL) ---
            float auraRange = 5f * 16f; 
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.active && !target.dead)
            {
                float distance = Vector2.Distance(npc.Center, target.Center);
                if (distance <= auraRange)
                {
                    // Berikan debuff Stoned (156)
                    // Kita beri durasi sangat singkat (2 frame) agar terus ter-refresh selama di dalam aura
                    target.AddBuff(156, 2);
                }
            }

            // --- 2. VISUAL PARTIKEL (ABU-ABU TEBAL & TERSEDOT) ---
            // Kita spawn partikel setiap frame agar terlihat padat/tebal
            for (int i = 0; i < 3; i++) 
            {
                // Spawn partikel di posisi acak dalam lingkaran aura
                Vector2 spawnOffset = Main.rand.NextVector2Circular(auraRange, auraRange);
                Vector2 spawnPos = npc.Center + spawnOffset;

                // Dust ID 54 atau 1 adalah warna abu-abu batu
                Dust dust = Dust.NewDustPerfect(spawnPos, DustID.Stone, Vector2.Zero, 100, Color.Gray, 1.5f);
                dust.noGravity = true;

                // Efek Tersedot: Hitung arah dari partikel menuju pusat Medusa
                Vector2 suctionDir = npc.Center - dust.position;
                suctionDir.Normalize();
                dust.velocity = suctionDir * 3f; // Kecepatan sedotan
            }
        }
    }
}