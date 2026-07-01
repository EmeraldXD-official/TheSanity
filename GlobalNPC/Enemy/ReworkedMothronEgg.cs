using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.NPCs
{
    public class ReworkedMothronEgg : global::Terraria.ModLoader.GlobalNPC
    {
        // Hanya memproses dan mengawasi Mothron Egg saja di file ini
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.MothronEgg;
        }

        // =========================================================================
        // LOGIKA KEMATIAN: REWORK TETESAN SPAWN SAAT TELUR DI-KILL PLAYER
        // =========================================================================
        public override void OnKill(NPC npc)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // -------------------------------------------------------------------------
                // LOKASI BALANCING: PERSENTASE CHANCE GACHA & JUMLAH BAYI YANG SPAWN
                // -------------------------------------------------------------------------
                // babyChance: Peluang memicu 2-3 bayi (0.10f = 10% chance).
                // babyCount: Jumlah default adalah 1 bayi jika gacha gagal.
                // Main.rand.Next(2, 4): Memilih acak antara 2 sampai 3 bayi jika gacha berhasil.
                // -------------------------------------------------------------------------
                float babyChance = 0.10f; 
                int babyCount = 1;        

                if (Main.rand.NextFloat() < babyChance)
                {
                    babyCount = Main.rand.Next(2, 4); // Hasil acak: 2 atau 3 bayi
                }

                // Eksekusi pemanggilan Baby Mothron (MothronSpawn) ke koordinat hancurnya telur
                for (int i = 0; i < babyCount; i++)
                {
                    // Sedikit offset acak agar posisi spawn bayi tidak menumpuk kaku di satu titik pixel
                    Vector2 spawnOffset = Main.rand.NextVector2Circular(16f, 16f);
                    
                    NPC.NewNPC(
                        npc.GetSource_Death(), 
                        (int)(npc.Center.X + spawnOffset.X), 
                        (int)(npc.Center.Y + spawnOffset.Y), 
                        NPCID.MothronSpawn
                    );
                }

                // Efek suara lendir menetas / pecah saat bayi-bayi keluar
                SoundEngine.PlaySound(SoundID.NPCDeath13, npc.Center);
            }
        }
    }
}