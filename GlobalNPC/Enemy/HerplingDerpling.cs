using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class CrimsonInsectRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public bool isScaleSet = false;

        public override bool PreAI(NPC npc)
        {
            // --- 1. FILTER ID (174: Derpling, 177: Herpling) ---
            bool isCrimsonInsect = npc.type == 174 || npc.type == 177;
            if (!isCrimsonInsect) return true;

            // --- 2. RESIZE SPRITE DAN HITBOX JADI 2 BLOCK ---
            if (!isScaleSet)
            {
                isScaleSet = true;
                npc.scale = 0.5f;
                npc.width = 32;
                npc.height = 32;
                npc.netUpdate = true;
            }

            // --- FIX ERROR: Pengecekan Target yang Aman ---
            // Kita cek dulu apakah targetnya valid sebelum diakses
            if (npc.target < 0 || npc.target >= Main.maxPlayers) return true;
            
            Player target = Main.player[npc.target];
            
            // Sekarang aman untuk mengecek active/dead karena kita sudah yakin npc.target valid
            if (!target.active || target.dead) return true;

            // --- 3. LOGIKA AURA PENYEDOT (RADIUS 10 BLOCK) ---
            float distance = Vector2.Distance(npc.Center, target.Center);
            float auraRadius = 160f; 

            if (distance < auraRadius)
            {
                target.AddBuff(BuffID.Slow, 120); // Menggunakan BuffID.Slow agar lebih jelas

                Vector2 pullDirection = (npc.Center - target.Center).SafeNormalize(Vector2.Zero);
                target.velocity += pullDirection * 0.25f;

                // --- 4. VISUAL EFEK TERSEDOT ---
                if (Main.rand.NextBool(4)) 
                {
                    Vector2 dustSpawnPos = npc.Center + Main.rand.NextVector2CircularEdge(distance, distance);
                    Vector2 dustVelocity = (npc.Center - dustSpawnPos).SafeNormalize(Vector2.Zero) * 4f;

                    Dust d = Dust.NewDustDirect(dustSpawnPos, 0, 0, DustID.Crimson, 0f, 0f, 100, default, 1.1f);
                    d.noGravity = true;
                    d.velocity = dustVelocity;
                }
            }

            return true;
        }
    }
}