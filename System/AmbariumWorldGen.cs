using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent.Generation;
using Terraria.ID;
using Terraria.IO;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace TheSanity.Systems
{
    public class AmbariumWorldGen : ModSystem
    {
        // Flag penanda agar teks & ore hanya muncul 1x per Blood Moon
        private bool spawnedBloodMoonOre = false;

        // ====================================================
        // 1. WORLD GENERATION (SAAT MEMBUAT DUNIA BARU)
        // ====================================================
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int shiniesIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Shinies"));

            if (shiniesIndex != -1)
            {
                tasks.Insert(shiniesIndex + 1, new PassLegacy("Ambarium Ore Pass", GenerateAmbariumOreWorldGen));
            }
        }

        private void GenerateAmbariumOreWorldGen(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Generating Ambarium Ore...";

            int amount = (int)(Main.maxTilesX * Main.maxTilesY * 0.00015);

            for (int k = 0; k < amount; k++)
            {
                int x = WorldGen.genRand.Next(0, Main.maxTilesX);
                int y = WorldGen.genRand.Next((int)GenVars.worldSurfaceLow, Main.maxTilesY - 200);

                Tile tile = Main.tile[x, y];

                if (tile.HasTile && tile.TileType == TileID.Stone)
                {
                    WorldGen.TileRunner(
                        x, 
                        y, 
                        WorldGen.genRand.Next(7, 12), 
                        WorldGen.genRand.Next(6, 12), 
                        ModContent.TileType<Tiles.AmbariumOreTile>()
                    );
                }
            }
        }

        // ====================================================
        // 2. EVENT BLOOD MOON (SETIAP BLOOD MOON TERJADI)
        // ====================================================
        public override void PostUpdateWorld()
        {
            // Cek jika Blood Moon sedang aktif di malam hari
            if (Main.bloodMoon && !Main.dayTime)
            {
                if (!spawnedBloodMoonOre)
                {
                    spawnedBloodMoonOre = true;

                    // 1. Tampilkan teks "Some stones look suspicious" di chat box
                    Color messageColor = new Color(185, 90, 255); // Warna ungu Ambarium

                    if (Main.netMode == NetmodeID.SinglePlayer)
                    {
                        Main.NewText("Some stones look suspicious", messageColor);
                    }
                    else if (Main.netMode == NetmodeID.Server)
                    {
                        ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral("Some stones look suspicious"), messageColor);
                    }

                    // 2. Spawn Vein Ore tambahan di Underground/Cavern
                    GenerateAmbariumOreBloodMoon();
                }
            }
            else
            {
                // Reset penanda saat Blood Moon selesai / saat siang hari
                spawnedBloodMoonOre = false;
            }
        }

        // ⛏️ FUNGSI SPANWING ORE TAMBAHAN SAAT BLOOD MOON
        private void GenerateAmbariumOreBloodMoon()
        {
            int oreTileType = ModContent.TileType<Tiles.AmbariumOreTile>();
            if (oreTileType == 0) return;

            // Jumlah gumpalan/vein yang dimunculkan saat Blood Moon
            int amountOfVeins = (int)(25 * (Main.maxTilesX / 4200f)); 

            for (int k = 0; k < amountOfVeins; k++)
            {
                int x = WorldGen.genRand.Next(100, Main.maxTilesX - 100);
                int y = WorldGen.genRand.Next((int)GenVars.worldSurfaceLow, Main.maxTilesY - 200);

                Tile tile = Main.tile[x, y];
                if (tile.HasTile && (tile.TileType == TileID.Stone || tile.TileType == TileID.Dirt))
                {
                    WorldGen.TileRunner(
                        x, 
                        y, 
                        WorldGen.genRand.Next(5, 9), 
                        WorldGen.genRand.Next(5, 9), 
                        oreTileType
                    );
                }
            }
        }
    }
}