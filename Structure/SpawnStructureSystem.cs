using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;
using StructureHelper.API;

namespace TheSanity.Structure
{
    public class SpawnStructureSystem : ModSystem
    {
        private class CustomGenPass : GenPass
        {
            private System.Action<GenerationProgress, GameConfiguration> _method;

            public CustomGenPass(string name, System.Action<GenerationProgress, GameConfiguration> method) : base(name, 1f)
            {
                _method = method;
            }

            protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
            {
                _method?.Invoke(progress, configuration);
            }
        }

        public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
        {
            int spawnIndex = tasks.FindIndex(genpass => genpass.Name.Equals("Spawn Point"));
            
            if (spawnIndex != -1)
            {
                tasks.Insert(spawnIndex + 1, new CustomGenPass("TheSanity Starter House", GenerateStarterHouse));
            }
        }

        private void GenerateStarterHouse(GenerationProgress progress, GameConfiguration configuration)
        {
            progress.Message = "Constructing Starter House...";

            int structureWidth = 38;
            int structureHeight = 51;

            int startX = Main.maxTilesX / 2 - (structureWidth / 2); 
            
            // =========================================================================
            // [GUIDE 1: DETEKSI PERMUKAAN (ADAPTASI WORLD GEN)]
            // =========================================================================
            int highestSolidY = Main.maxTilesY;
            
            for (int x = startX; x < startX + structureWidth; x++)
            {
                int y = (int)(Main.worldSurface * 0.4);
                while (y < Main.maxTilesY && !WorldGen.SolidTile(x, y))
                {
                    y++;
                }
                if (y < highestSolidY)
                {
                    highestSolidY = y;
                }
            }

            // =========================================================================
            // [GUIDE 2: KETINGGIAN RUMAH]
            // =========================================================================
            int angkatRumah = -37; 
            int finalY = highestSolidY + angkatRumah;

            // =========================================================================
            // [GUIDE 3: SELECTIVE CLEARING (HANYA POHON & RUMPUT)]
            // =========================================================================
            for (int x = startX; x < startX + structureWidth; x++)
            {
                for (int y = finalY; y < finalY + structureHeight; y++)
                {
                    if (WorldGen.InWorld(x, y) && Main.tile[x, y].HasTile)
                    {
                        ushort type = Main.tile[x, y].TileType;
                        if (TileID.Sets.IsATreeTrunk[type] || type == TileID.Trees || 
                            type == TileID.Plants || type == TileID.Plants2)
                        {
                            WorldGen.KillTile(x, y, noItem: true);
                        }
                    }
                }
            }

            // Generate Rumah dari File .shstruct
            Point16 position = new Point16(startX, finalY);
            Generator.GenerateStructure("Structure/StarterHouseReal2", position, Mod);

            // =========================================================================
            // [GUIDE 4: PONDASI OTOMATIS BAWAH RUMAH (FIXED UNTUK PLANTS)]
            // =========================================================================
            int dasarRumahY = finalY + structureHeight;
            for (int x = startX; x < startX + structureWidth; x++)
            {
                int foundationY = dasarRumahY;
                
                // FIXED: Cek apakah blok itu "Solid". Kalau cuma tanaman/rumput, dia akan tembus dan ditimpa.
                while (foundationY < Main.maxTilesY && !WorldGen.SolidTile(x, foundationY))
                {
                    // Hancurkan tanaman/rumput di posisi ini sebelum menaruh Dirt
                    if (Main.tile[x, foundationY].HasTile)
                    {
                        WorldGen.KillTile(x, foundationY, noItem: true);
                    }
                    
                    WorldGen.PlaceTile(x, foundationY, TileID.Dirt, mute: true, forced: true);
                    WorldGen.SquareTileFrame(x, foundationY, true); 
                    foundationY++;
                }
            }

            // =========================================================================
            // [UPDATE FRAME KESELURUHAN (Membantu render Block & Wall Paint)]
            // =========================================================================
            for (int x = startX - 2; x < startX + structureWidth + 2; x++)
            {
                for (int y = finalY - 2; y < finalY + structureHeight + 15; y++)
                {
                    if (WorldGen.InWorld(x, y))
                    {
                        WorldGen.SquareTileFrame(x, y, true); // Update Block
                        WorldGen.SquareWallFrame(x, y, true); // Update Wall (Tembok belakang)
                    }
                }
            }

            // =========================================================================
            // [GUIDE 5: POSISI SPAWN PLAYER]
            // =========================================================================
            int spawnOffsetX = 26; 
            int spawnOffsetY = 25; 

            Main.spawnTileX = startX + spawnOffsetX; 
            Main.spawnTileY = finalY + spawnOffsetY; 
        }
    }
}