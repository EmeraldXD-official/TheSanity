using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [WORLD SYSTEM]: PROGRESSIVE RAIN PUDDLE GENERATOR (DYNAMIC CHANCE 10% - 80%)
    // =========================================================================
    public class RainPuddleSystem : ModSystem
    {
        // Menyimpan akumulasi durasi hujan untuk meningkatkan kebecekan dunia
        public int rainIntensityCounter = 0;

        public override void PostUpdateWorld()
        {
            // RESET SYSTEM: Jika hujan sudah berhenti, kembalikan counter ke nol murni
            if (!Main.raining)
            {
                rainIntensityCounter = 0;
                return;
            }

            // -------------------------------------------------------------------------
            // SPEED INCREASE LOCATION: Mengatur kecepatan akumulasi air berdasarkan cuaca
            // -------------------------------------------------------------------------
            if (Main.maxRaining > 0.6f || Main.IsItStorming)
            {
                // JIKA BADAI/THUNDER: Kenaikan counter melesat 3x lebih cepat!
                rainIntensityCounter += 3;
            }
            else
            {
                // JIKA HUJAN BIASA: Kenaikan counter berjalan normal (+1 per frame)
                rainIntensityCounter++;
            }

            // -------------------------------------------------------------------------
            // CHANCE SCHEDULER LOCATION: Menentukan tingkat kebecekan (10% s/d 80%)
            // -------------------------------------------------------------------------
            bool shouldSpawnPuddle = false;

            if (rainIntensityCounter < 3600) // Detik 0 s/d Menit 1 Hujan
            {
                // AWAL HUJAN: Chance 10% (1 banding 10)
                shouldSpawnPuddle = Main.rand.NextBool(10);
            }
            else if (rainIntensityCounter >= 3600 && rainIntensityCounter < 7200) // Menit 1 s/d Menit 2
            {
                // MULAI BECEK: Chance 20% (1 banding 5)
                shouldSpawnPuddle = Main.rand.NextBool(5);
            }
            else if (rainIntensityCounter >= 7200 && rainIntensityCounter < 14400) // Menit 2 s/d Menit 4
            {
                // HUJAN DERAS: Chance 40% (2 banding 5)
                shouldSpawnPuddle = Main.rand.NextBool(2, 5);
            }
            else if (rainIntensityCounter >= 14400 && rainIntensityCounter < 21600) // Menit 4 s/d Menit 6
            {
                // BANJIR RINGAN: Chance 60% (3 banding 5)
                shouldSpawnPuddle = Main.rand.NextBool(3, 5);
            }
            else // Di atas Menit 6 (Atau cuma ~2 menit jika badai petir ganas terus-menerus)
            {
                // MENTOK BADAI: Chance 80% (4 banding 5)! Genangan langsung instan di mana-mana
                shouldSpawnPuddle = Main.rand.NextBool(4, 5);
            }

            // EKSEKUSI SUMMON JIKA CHANCE ACING TERPENUHI
            if (shouldSpawnPuddle)
            {
                // 1. Pilih koordinat X secara acak di seluruh lebar peta dunia
                int tileX = Main.rand.Next(20, Main.maxTilesX - 20);

                // 2. Scan dari langit paling atas (y = 0) lurus ke bawah
                int tileY = 0;
                while (tileY < Main.maxTilesY - 20 && !Main.tile[tileX, tileY].HasTile)
                {
                    tileY++;
                }

                // Posisi udara tepat di atas blok tanah/platform yang kehujanan
                int puddleY = tileY - 1;

                if (puddleY < 10 || puddleY >= Main.maxTilesY) return;

                Tile targetTile = Main.tile[tileX, puddleY];     // Tempat air ditaruh (udara)
                Tile groundTile = Main.tile[tileX, tileY];       // Alas di bawahnya (tanah/platform)

                // -------------------------------------------------------------------------
                // KRITERIA VALIDASI KETAT
                // -------------------------------------------------------------------------
                if (puddleY > Main.rockLayer) return;
                if (targetTile.WallType != 0) return;
                if (groundTile.TileType == TileID.SnowBlock || groundTile.TileType == TileID.IceBlock) return;

                bool isSolid = Main.tileSolid[groundTile.TileType] && !Main.tileSolidTop[groundTile.TileType];
                bool isPlatform = TileID.Sets.Platforms[groundTile.TileType];

                if ((isSolid || isPlatform) && !targetTile.HasTile)
                {
                    if (targetTile.LiquidAmount < 64)
                    {
                        targetTile.LiquidType = LiquidID.Water;
                        targetTile.LiquidAmount = 64; // Becek semata kaki

                        WorldGen.SquareTileFrame(tileX, puddleY, true);
                        Liquid.AddWater(tileX, puddleY); 

                        if (Main.netMode == NetmodeID.Server)
                        {
                            NetMessage.SendTileSquare(-1, tileX, puddleY, 1);
                        }
                    }
                }
            }
        }
    }
}