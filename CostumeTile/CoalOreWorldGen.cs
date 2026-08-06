using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Generation;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using TheSanity.CostumeTile;

namespace TheSanity
{
    public class CoalOreWorldGen : ModSystem
    {
        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            // Mencari tahap generasi "Shinies" (tahap generator ore vanilla)
            int shiniesIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Shinies"));

            if (shiniesIndex != -1)
            {
                // Menambahkan tahapan spawn Coal Ore tepat setelah "Shinies"
                tasks.Insert(shiniesIndex + 1, new PassLegacy("Coal Ore Generation", GenerateCoalOre));
            }
        }

        private void GenerateCoalOre(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Generating Coal Ore...";

            // Density 0.00012 = 2x lipat lebih padat dari ore standar vanilla
            int amount = (int)(Main.maxTilesX * Main.maxTilesY * 0.00012);

            for (int k = 0; k < amount; k++)
            {
                // Menentukan posisi acak X dan Y (Underground sampai mendekati Underworld)
                int x = WorldGen.genRand.Next(0, Main.maxTilesX);
                int y = WorldGen.genRand.Next((int)Main.worldSurface, Main.maxTilesY - 200);

                // Strength & Steps diatur tinggi untuk membentuk chunk 5x5 hingga 15x15+
                double strength = WorldGen.genRand.Next(6, 14);
                int steps = WorldGen.genRand.Next(6, 16);

                // Urutan parameter TileRunner 1.4.4: 
                // (i, j, strength, steps, type, addTile, speedX, speedY, noMaterial, overWrite)
                WorldGen.TileRunner(
                    x, 
                    y, 
                    strength, 
                    steps, 
                    ModContent.TileType<CoalOre>(), 
                    false, 
                    0f, 
                    0f, 
                    false, 
                    true
                );
            }
        }
    }
}