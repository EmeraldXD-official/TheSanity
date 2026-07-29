using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.DataStructures;
using Terraria.Chat; 
using Terraria.Localization; 
using StructureHelper.API;

namespace TheSanity.Structure
{
    public class TempleArenaSystem : ModSystem
    {
        public static bool generatedTempleArena;

        public override void ClearWorld()
        {
            generatedTempleArena = false;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (generatedTempleArena)
            {
                tag["generatedTempleArena"] = true;
            }
        }

        public override void LoadWorldData(TagCompound tag)
        {
            generatedTempleArena = tag.ContainsKey("generatedTempleArena");
        }

        public override void PostUpdateWorld()
        {
            if (NPC.downedPlantBoss && !generatedTempleArena)
            {
                bool success = GenerateTempleArena();

                if (success)
                {
                    generatedTempleArena = true; 

                    Color notificationColor = new Color(210, 105, 30); 
                    string message = "Temple has changed!";

                    if (Main.netMode == NetmodeID.SinglePlayer)
                    {
                        Main.NewText(message, notificationColor);
                    }
                    else if (Main.netMode == NetmodeID.Server)
                    {
                        ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(message), notificationColor);
                    }
                }
            }
        }

        private bool GenerateTempleArena()
        {
            int structureWidth = 191;
            int structureHeight = 148;

            int altarX = -1;
            int altarY = -1;
            bool foundAltar = false;

            // Cari lokasi Lihzahrd Altar
            for (int x = 10; x < Main.maxTilesX - 10; x++)
            {
                for (int y = 10; y < Main.maxTilesY - 10; y++)
                {
                    if (Main.tile[x, y].HasTile && Main.tile[x, y].TileType == TileID.LihzahrdAltar)
                    {
                        altarX = x;
                        altarY = y;
                        foundAltar = true;
                        break; 
                    }
                }
                if (foundAltar) break; 
            }

            if (!foundAltar)
            {
                return false; 
            }

            // Horizontal tetap di tengah Altar
            int placementX = altarX - (structureWidth / 2);
            
            // =========================================================================
            // PERBAIKAN: DIGESER KE ATAS 14 BALOK
            // =========================================================================
            // Dikurangi 14 agar titik awal paste (top-left) naik setinggi 14 block dari altar
            int placementY = altarY - 14; 

            // Validasi boundaries map Terraria
            if (placementX < 0) placementX = 0;
            if (placementY < 0) placementY = 0;
            if (placementX + structureWidth > Main.maxTilesX) placementX = Main.maxTilesX - structureWidth;
            if (placementY + structureHeight > Main.maxTilesY) placementY = Main.maxTilesY - structureHeight;

            Point16 placePoint = new Point16(placementX, placementY);
            string structurePath = "Structure/TampleArena"; 

            // Paste struktur via Structure Helper
            Generator.GenerateStructure(structurePath, placePoint, Mod);

            // Golem-room render safety fix (Mencegah glitch visual block/wall/actuator)
            for (int x = placementX - 5; x < placementX + structureWidth + 5; x++)
            {
                for (int y = placementY - 5; y < placementY + structureHeight + 5; y++)
                {
                    if (WorldGen.InWorld(x, y))
                    {
                        WorldGen.SquareTileFrame(x, y, true);
                        WorldGen.SquareWallFrame(x, y, true);
                    }
                }
            }

            return true; 
        }
    }
}