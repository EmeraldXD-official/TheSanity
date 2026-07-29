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
    // SYSTEM UNTUK MENYIMPAN DATA WORLD & TIMER SPAWN (GABUNGAN)
    // =========================================================================
    public class EasterEggSystem : ModSystem
    {
        public static bool generatedCalamityLantern;
        
        // Timer untuk jeda 3 detik. Nilai -1 berarti timer sedang tidak aktif.
        public static int calamityLanternTimer = -1; 

        public override void ClearWorld()
        {
            generatedCalamityLantern = false;
            calamityLanternTimer = -1;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (generatedCalamityLantern)
            {
                tag["generatedCalamityLantern"] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag)
        {
            generatedCalamityLantern = tag.ContainsKey("generatedCalamityLantern");
        }

        // =========================================================================
        // UPDATE DUNIA (Mengecek Status Trio Mech & Menjalankan Timer)
        // =========================================================================
        public override void PostUpdateWorld()
        {
            // 1. Cek apakah KETIGA Mech Boss sudah kalah (Destroyer, Twins, Skeletron Prime)
            // Dan pastikan struktur belum dibuat, serta timer belum menyala
            if (NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3 && 
                !generatedCalamityLantern && calamityLanternTimer == -1)
            {
                // Mulai hitung mundur! 3 detik = 180 frame/tick
                calamityLanternTimer = 180;
            }

            // 2. Kalau timer sedang berjalan (angka > 0), kurangi 1 setiap frame
            if (calamityLanternTimer > 0)
            {
                calamityLanternTimer--;
            }
            // 3. Kalau timer tepat menyentuh angka 0, eksekusi spawn strukturnya!
            else if (calamityLanternTimer == 0)
            {
                GenerateCalamityLantern();
                generatedCalamityLantern = true; // Kunci permanen agar tidak muncul lagi
                calamityLanternTimer = -1; // Matikan timer biar tidak looping
            }
        }

        // =========================================================================
        // FUNGSI GENERATE STRUKTUR
        // =========================================================================
        private void GenerateCalamityLantern()
        {
            int structureWidth = 55;
            int structureHeight = 42;

            int startX = 0;
            int startY = 0;
            bool foundValidSpot = false;

            // Smart Terrain Scanner
            for (int attempts = 0; attempts < 100; attempts++)
            {
                startX = WorldGen.genRand.Next(Main.maxTilesX / 4, (Main.maxTilesX * 3) / 4);
                startY = Main.UnderworldLayer + 20;

                while (!WorldGen.SolidTile(startX, startY) && startY < Main.maxTilesY - 20)
                {
                    startY++;
                }

                bool isSubmerged = false;
                for (int i = 1; i <= 5; i++)
                {
                    if (Main.tile[startX, startY - i].LiquidAmount > 0)
                    {
                        isSubmerged = true;
                        break;
                    }
                }

                if (!isSubmerged)
                {
                    foundValidSpot = true;
                    break;
                }
            }

            int yOffset = 2; 
            int placementY = startY - structureHeight + yOffset;
            int placementX = startX - (structureWidth / 2);

            Point16 placePoint = new Point16(placementX, placementY);
            string structurePath = "Structure/ForgottenCalamityLantern";

            Generator.GenerateStructure(structurePath, placePoint, Mod);

            // =========================================================================
            // UPDATE FRAME (Mencegah Glitch Cat/Painting)
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