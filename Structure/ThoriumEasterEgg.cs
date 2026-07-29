using System;
using System.Collections.Generic;
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
    // SYSTEM UNTUK MENYIMPAN DATA WORLD (THORIUM EASTER EGG)
    // =========================================================================
    public class ThoriumEggSystem : ModSystem
    {
        public static bool generatedThoriumNote;

        public override void ClearWorld()
        {
            generatedThoriumNote = false;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (generatedThoriumNote)
            {
                tag["generatedThoriumNote"] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag)
        {
            generatedThoriumNote = tag.ContainsKey("generatedThoriumNote");
        }
    }

    // =========================================================================
    // TRIGGER SAAT KING SLIME MATI
    // =========================================================================
    public class KingSlimeStructureTrigger : global::Terraria.ModLoader.GlobalNPC
    {
        public override void OnKill(NPC npc)
        {
            if (npc.type == NPCID.KingSlime && !ThoriumEggSystem.generatedThoriumNote)
            {
                GenerateThoriumNotes();
                ThoriumEggSystem.generatedThoriumNote = true; 
            }
        }

        private void GenerateThoriumNotes()
        {
            int structureWidth = 34;
            int structureHeight = 40;
            
            // Pengaturan jumlah spawn dan jarak
            int structuresToSpawn = 3;
            int minimumDistance = 150; // Jarak minimal antar struktur (dalam jumlah blok)

            // List untuk menyimpan koordinat yang sudah berhasil di-spawn
            List<Point> placedLocations = new List<Point>();

            // Loop sebanyak jumlah struktur yang ingin di-spawn
            for (int i = 0; i < structuresToSpawn; i++)
            {
                int startX = 0;
                int startY = 0;
                bool foundValidSpot = false;

                // Loop pencarian spot (dinaikkan ke 2000 agar lebih mudah mencari 3 tempat)
                for (int attempts = 0; attempts < 2000; attempts++)
                {
                    startX = WorldGen.genRand.Next(Main.maxTilesX / 10, (Main.maxTilesX * 9) / 10);
                    startY = WorldGen.genRand.Next((int)Main.rockLayer + 50, Main.maxTilesY - 250);

                    while (!WorldGen.SolidTile(startX, startY) && startY < Main.maxTilesY - 200)
                    {
                        startY++;
                    }

                    ushort tileType = Main.tile[startX, startY].TileType;

                    // --- 1. FILTER BIOME ---
                    if (tileType == TileID.IceBlock || tileType == TileID.SnowBlock) continue;
                    if (tileType == TileID.Mud || tileType == TileID.JungleGrass) continue;
                    if (tileType == TileID.Sand || tileType == TileID.HardenedSand || tileType == TileID.Sandstone) continue;
                    if (tileType == TileID.BlueDungeonBrick || tileType == TileID.GreenDungeonBrick || tileType == TileID.PinkDungeonBrick) continue;
                    if (tileType == TileID.LihzahrdBrick) continue;

                    // --- 2. CEK JARAK DENGAN STRUKTUR SEBELUMNYA ---
                    bool isTooClose = false;
                    foreach (Point placedPoint in placedLocations)
                    {
                        // Menghitung selisih jarak X dan Y
                        if (Math.Abs(startX - placedPoint.X) < minimumDistance && 
                            Math.Abs(startY - placedPoint.Y) < minimumDistance)
                        {
                            isTooClose = true;
                            break;
                        }
                    }
                    // Jika terlalu dekat dengan struktur yang sudah ada, cari spot baru
                    if (isTooClose) continue;

                    // --- 3. FILTER CAIRAN TOTAL (AREA SCANNER) ---
                    bool areaHasLiquid = false;
                    int topLeftX = startX - (structureWidth / 2);
                    int topLeftY = startY - structureHeight;

                    for (int scanX = topLeftX; scanX < topLeftX + structureWidth; scanX++)
                    {
                        for (int scanY = topLeftY; scanY <= startY; scanY++)
                        {
                            if (WorldGen.InWorld(scanX, scanY))
                            {
                                if (Main.tile[scanX, scanY].LiquidAmount > 0)
                                {
                                    areaHasLiquid = true;
                                    break;
                                }
                            }
                        }
                        if (areaHasLiquid) break;
                    }

                    if (areaHasLiquid) continue;

                    // Jika lolos semua filter, tandai sebagai valid!
                    foundValidSpot = true;
                    break;
                }

                // Fallback jika apes banget tidak menemukan spot (sangat jarang terjadi)
                if (!foundValidSpot)
                {
                    // Di-offset berdasarkan iterasi (i * 200) agar kalau gagal, tetap tidak menumpuk
                    startX = (Main.maxTilesX / 2) + (i * 200); 
                    startY = (int)Main.rockLayer + 100;
                    while (!WorldGen.SolidTile(startX, startY)) { startY++; }
                }

                // =========================================================================
                // PENEMPATAN FINAL STRUKTUR
                // =========================================================================
                int yOffset = 2; 
                int placementY = startY - structureHeight + yOffset;
                int placementX = startX - (structureWidth / 2);

                Point16 placePoint = new Point16(placementX, placementY);
                string structurePath = "Structure/NoteOfThorium";

                // Generate struktur
                Generator.GenerateStructure(structurePath, placePoint, Mod);

                // =========================================================================
                // UPDATE FRAME KESELURUHAN (FIX RENDER CAT / PAINTING & WALL)
                // =========================================================================
                for (int x = placementX - 2; x < placementX + structureWidth + 2; x++)
                {
                    for (int y = placementY - 2; y < placementY + structureHeight + 2; y++)
                    {
                        if (WorldGen.InWorld(x, y))
                        {
                            WorldGen.SquareTileFrame(x, y, true); // Update Block & Cat
                            WorldGen.SquareWallFrame(x, y, true); // Update Wall & Cat
                        }
                    }
                }

                // Simpan koordinat yang berhasil di-spawn ke dalam List untuk dicek oleh loop berikutnya
                placedLocations.Add(new Point(startX, startY));
            }
        }
    }
}