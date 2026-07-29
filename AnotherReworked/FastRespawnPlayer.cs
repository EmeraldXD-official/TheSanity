using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalPlayers
{
    public class FastRespawnPlayer : ModPlayer
    {
        public override void UpdateDead()
        {
            // Deteksi apakah ada Boss yang sedang hidup di dunia
            bool isBossAlive = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                
                // npc.boss mengecek boss normal, 
                // EaterOfWorldsHead ditambahkan manual karena secara teknis part cacing kadang tidak dihitung boss murni oleh game
                if (npc.active && (npc.boss || npc.type == Terraria.ID.NPCID.EaterofWorldsHead))
                {
                    isBossAlive = true;
                    break; // Langsung hentikan pencarian jika sudah ketemu 1 boss
                }
            }

            // =========================================================================
            // [GUIDE & BALANCING LOKASI: RESPAWN TIMER CAP]
            // Catatan: Waktu di Terraria menggunakan 'Tick'. 60 Tick = 1 Detik.
            // =========================================================================
            
            int bossRespawnCap = 600; // 10 Detik
            int normalRespawnCap = 120; // 2 Detik

            if (isBossAlive)
            {
                // Jika sedang melawan Boss dan sisa waktu respawn lebih dari 10 detik, pangkas menjadi 10 detik
                if (Player.respawnTimer > bossRespawnCap)
                {
                    Player.respawnTimer = bossRespawnCap;
                }
            }
            else
            {
                // Jika tidak ada Boss dan sisa waktu respawn lebih dari 2 detik, pangkas menjadi 2 detik
                if (Player.respawnTimer > normalRespawnCap)
                {
                    Player.respawnTimer = normalRespawnCap;
                }
            }
        }
    }
}