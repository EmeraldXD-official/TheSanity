using Microsoft.Xna.Framework;
using StructureHelper.API; 
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TheSanity.GlobalNPCs
{
    // =========================================================================
    // SYSTEM UNTUK MENYIMPAN DATA WORLD (MUTANT STRUCTURE)
    // =========================================================================
    public class MutantStructureSystem : ModSystem
    {
        public static bool generatedMutantLove;

        public override void ClearWorld()
        {
            generatedMutantLove = false;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (generatedMutantLove)
            {
                tag["generatedMutantLove"] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag)
        {
            generatedMutantLove = tag.ContainsKey("generatedMutantLove");
        }

        // =========================================================================
        // UPDATE DUNIA (Mengecek Status Golem)
        // =========================================================================
        public override void PostUpdateWorld()
        {
            // Cek apakah Golem sudah mati dan struktur belum di-generate
            if (NPC.downedGolemBoss && !generatedMutantLove)
            {
                GenerateMutantLove();
                generatedMutantLove = true; // Kunci permanen
            }
        }

        // =========================================================================
        // FUNGSI GENERATE STRUKTUR DI LANGIT
        // =========================================================================
        private void GenerateMutantLove()
        {
            int structureWidth = 33;
            int structureHeight = 30;

            int placementX = 0;
            int placementY = 0;
            bool foundValidSpot = false;

            // =========================================================================
            // [SKY SCANNER: MENCARI AREA UDARA KOSONG]
            // =========================================================================
            for (int attempts = 0; attempts < 5000; attempts++)
            {
                // X: Cari di area 10% sampai 90% peta (jangan terlalu di ujung)
                int startX = WorldGen.genRand.Next(Main.maxTilesX / 10, (Main.maxTilesX * 9) / 10);
                
                // Y: Cari di layer Space/Sky. Mulai dari Y=50 (batas atas agar tidak out-of-bounds) 
                // hingga 35% dari World Surface.
                int startY = WorldGen.genRand.Next(50, (int)(Main.worldSurface * 0.35));

                bool areaIsClear = true;

                // Buffer = 10 blok. Kita mengecek area yang LEBIH BESAR dari struktur
                // untuk memastikan tidak menempel/menabrak Sky Island atau struktur lain.
                int buffer = 10;
                int checkStartX = startX - buffer;
                int checkEndX = startX + structureWidth + buffer;
                int checkStartY = startY - buffer;
                int checkEndY = startY + structureHeight + buffer;

                for (int scanX = checkStartX; scanX < checkEndX; scanX++)
                {
                    for (int scanY = checkStartY; scanY < checkEndY; scanY++)
                    {
                        if (WorldGen.InWorld(scanX, scanY))
                        {
                            // Jika ada Tile (blok apapun) atau Wall (dinding apapun), berarti tidak kosong
                            if (Main.tile[scanX, scanY].HasTile || Main.tile[scanX, scanY].WallType > 0)
                            {
                                areaIsClear = false;
                                break;
                            }
                        }
                    }
                    if (!areaIsClear) break;
                }

                // Jika seluruh area + buffer kosong, lokasi ini valid!
                if (areaIsClear)
                {
                    placementX = startX;
                    placementY = startY;
                    foundValidSpot = true;
                    break;
                }
            }

            // Fallback: Jika benar-benar gagal menemukan tempat (sangat jarang terjadi)
            if (!foundValidSpot)
            {
                placementX = Main.maxTilesX / 2;
                placementY = 100; // Taruh paksa di tengah atas
            }

            // =========================================================================
            // PENEMPATAN FINAL STRUKTUR
            // =========================================================================
            Point16 placePoint = new Point16(placementX, placementY);
            string structurePath = "Structure/LoveOfMutant";

            Generator.GenerateStructure(structurePath, placePoint, Mod);

            // =========================================================================
            // UPDATE FRAME (Mencegah Glitch Cat/Painting pada Blok dan Dinding)
            // =========================================================================
            for (int x = placementX - 2; x < placementX + structureWidth + 2; x++)
            {
                for (int y = placementY - 2; y < placementY + structureHeight + 2; y++)
                {
                    if (WorldGen.InWorld(x, y))
                    {
                        WorldGen.SquareTileFrame(x, y, true);
                        WorldGen.SquareWallFrame(x, y, true);
                    }
                }
            }
        }
    }
}