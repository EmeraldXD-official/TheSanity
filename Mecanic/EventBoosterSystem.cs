using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System.Collections.Generic;

namespace TheSanity
{
    // =========================================================================
    // 1. BAGIAN PLAYER: MEMBERIKAN DEBUFF SHADOW CANDLE SECARA TERUS-MENERUS
    // =========================================================================
    public class EventDebuffPlayer : ModPlayer
    {
        public override void PostUpdateBuffs()
        {
            // Cek apakah sedang terjadi event Blood Moon ATAU Solar Eclipse
            if (Main.bloodMoon || Main.eclipse)
            {
                // BuffID.ShadowCandle (ID: 353) - Mematikan efek kenyamanan rumah/NPC town
                // Diberikan durasi 2 frame (akan terus di-refresh setiap detik selama event aktif)
                Player.AddBuff(BuffID.ShadowCandle, 2);
            }
        }
    }

    // =========================================================================
    // 2. BAGIAN SPAWN RATE: MENINGKATKAN REFRESH & SPAWN RATE SEBANYAK 10X LIPAT
    // =========================================================================
    public class EventSpawnModifier : global::Terraria.ModLoader.GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            // Pastikan pengali hanya aktif saat Blood Moon atau Solar Eclipse berlangsung
            if (Main.bloodMoon || Main.eclipse)
            {
                // -------------------------------------------------------------------------
                // [SPAWN RATE BALANCING LOCATION]
                // -------------------------------------------------------------------------
                // spawnRate: Semakin KECIL angkanya, semakin CEPAT monster baru bermunculan (1 frame = instan)
                // maxSpawns: Semakin BESAR angkanya, semakin BANYAK monster yang bisa ada di layar sekaligus
                // -------------------------------------------------------------------------
                
                // Kurangi jeda kemunculan musuh menjadi 1/10 dari aslinya (Super Cepat!)
                spawnRate = (int)(spawnRate * 0.1f);
                if (spawnRate < 1) spawnRate = 1; // Batasan aman sistem Terraria agar tidak crash

                // Naikkan batas maksimum jumlah monster di layar menjadi 10x lipat
                maxSpawns *= 10;
            }
        }
    }
}