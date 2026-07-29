using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class SanityGlobalTile : GlobalTile
    {
        public override void KillTile(int i, int j, int type, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (!fail && !effectOnly)
            {
                if (type == TileID.ShadowOrbs)
                {
                    // 🔥 SOLUSI: Ditambahkan "Terraria." di depannya agar tidak bentrok dengan nama folder/namespace Tile kamu
                    Terraria.Tile tile = Main.tile[i, j];
                    
                    /* Shadow Orb & Crimson Heart itu 2x2. 
                        tile.TileFrameX % 36 == 0 artinya ini kolom kiri.
                        tile.TileFrameY % 36 == 0 artinya ini baris atas.
                        Dengan cek ini, kode hanya akan jalan 1 KALI (di pojok kiri atas orb).
                    */
                    if (tile.TileFrameX % 36 == 0 && tile.TileFrameY % 36 == 0)
                    {
                        bool isCrimson = tile.TileFrameX >= 36;
                        Vector2 spawnPos = new Vector2(i * 16, j * 16);

                        if (!isCrimson)
                        {
                            // --- SHADOW ORB ---
                            int eaterAmount = Main.rand.Next(1, 4); 
                            for (int k = 0; k < eaterAmount; k++) {
                                NPC.NewNPC(null, (int)spawnPos.X, (int)spawnPos.Y, NPCID.LittleEater);
                            }

                            int devourerAmount = Main.rand.Next(1, 4);
                            for (int k = 0; k < devourerAmount; k++) {
                                NPC.NewNPC(null, (int)spawnPos.X, (int)spawnPos.Y, NPCID.DevourerHead);
                            }
                        }
                        else
                        {
                            // --- CRIMSON HEART ---
                            int crawlerAmount = Main.rand.Next(1, 4);
                            for (int k = 0; k < crawlerAmount; k++) {
                                NPC.NewNPC(null, (int)spawnPos.X, (int)spawnPos.Y, NPCID.BloodCrawlerWall);
                            }

                            int crimeraAmount = Main.rand.Next(1, 4);
                            for (int k = 0; k < crimeraAmount; k++) {
                                NPC.NewNPC(null, (int)spawnPos.X, (int)spawnPos.Y, NPCID.LittleCrimera);
                            }
                        }
                    }
                }
            }
        }
    }
}