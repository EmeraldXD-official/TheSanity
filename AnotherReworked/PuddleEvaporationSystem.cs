using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [WORLD SYSTEM]: PUDDLE EVAPORATION & GROUND ABSORPTION (SMART DETECTION)
    // =========================================================================
    public class PuddleEvaporationSystem : ModSystem
    {
        public override void PostUpdateWorld()
        {
            if (Main.raining) return;

            int tilesToScanPerFrame = 500; 

            for (int i = 0; i < tilesToScanPerFrame; i++)
            {
                int x = Main.rand.Next(300, Main.maxTilesX - 300);
                int y = Main.rand.Next(0, (int)Main.worldSurface);

                Tile tile = Main.tile[x, y];

                // HAPUS BATASAN < 255 AGAR BISA MENYEDOT FULL BLOCK
                if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Water)
                {
                    // 1. CEK CERDAS: Apakah ini benar-benar genangan becek?
                    if (!IsValidPuddle(x, y)) continue; // Jika ini danau/oasis, skip langsung!

                    Tile groundTile = Main.tile[x, y + 1];
                    bool isDesert = groundTile.HasTile && (groundTile.TileType == TileID.Sand || groundTile.TileType == TileID.HardenedSand || groundTile.TileType == TileID.Sandstone);

                    float drainChance = isDesert ? 0.80f : 0.50f;
                    byte drainAmount = 64; 

                    if (Main.rand.NextFloat() < drainChance)
                    {
                        if (tile.LiquidAmount <= drainAmount)
                        {
                            tile.LiquidAmount = 0; 
                        }
                        else
                        {
                            tile.LiquidAmount -= drainAmount; 
                        }

                        WorldGen.SquareTileFrame(x, y, true);
                        if (Main.netMode == NetmodeID.Server)
                        {
                            NetMessage.SendTileSquare(-1, x, y, 1);
                        }
                    }
                }
            }
        }

        // =========================================================================
        // [GUIDE & BALANCING LOKASI: KRITERIA DANAU VS GENANGAN BECEK]
        // =========================================================================
        private bool IsValidPuddle(int startX, int startY)
        {
            // 1. Cek Kedalaman: Jika blok di bawahnya juga air, berarti ini kolam dalam. BUKAN GENANGAN.
            if (Main.tile[startX, startY + 1].LiquidAmount > 0) return false;
            
            // 2. Cek Atap: Pastikan ini lapisan air paling atas (tidak ada air di atasnya)
            if (Main.tile[startX, startY - 1].LiquidAmount > 0) return false;

            int puddleWidth = 1;
            int maxCheckDistance = 6; // LOKASI BALANCING: Batas maksimal lebar air yang dianggap genangan

            // 3. Scan ke Kiri mencari tepi air
            for (int i = 1; i <= maxCheckDistance; i++)
            {
                Tile leftTile = Main.tile[startX - i, startY];
                if (leftTile.LiquidAmount > 0)
                {
                    // Jika air di sebelah kiri ternyata dalam, berarti ini pinggiran danau
                    if (Main.tile[startX - i, startY + 1].LiquidAmount > 0) return false;
                    puddleWidth++;
                }
                else break; // Nabrak tanah kering
            }

            // 4. Scan ke Kanan mencari tepi air
            for (int i = 1; i <= maxCheckDistance; i++)
            {
                Tile rightTile = Main.tile[startX + i, startY];
                if (rightTile.LiquidAmount > 0)
                {
                    // Jika air di sebelah kanan ternyata dalam, berarti ini pinggiran danau
                    if (Main.tile[startX + i, startY + 1].LiquidAmount > 0) return false;
                    puddleWidth++;
                }
                else break; // Nabrak tanah kering
            }

            // LOKASI BALANCING: Jika lebarnya lebih dari 6 blok menyambung, biarkan saja (dianggap Oasis/Kolam)
            if (puddleWidth > 6) return false;

            return true;
        }
    }
}