using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Enemy
{
    // Logic hancurin tile dalam radius, dipakai bareng sama CreeperEnemy (ledakan utama)
    // dan CreeperGoblinBombDamageLock (ledakan proyektil DD2GoblinBomb hasil muncratannya),
    // biar keduanya konsisten ngikutin progression pickaxe power yang sama.
    public static class CreeperExplosionUtils
    {
        // Tabel MinPick tile vanilla yang progression-gated (vanilla ga nyimpen ini di array publik per-tile,
        // jadi kita definisiin manual sesuai nilai asli dari wiki Pickaxe Power)
        private static readonly Dictionary<int, int> VanillaTileMinPick = new Dictionary<int, int>
        {
            { TileID.Meteorite, 50 },
            { TileID.Demonite, 55 },
            { TileID.Crimtane, 55 },
            { TileID.Hellstone, 65 },
            { TileID.Cobalt, 100 },
            { TileID.Palladium, 100 },
            { TileID.Mythril, 110 },
            { TileID.Orichalcum, 110 },
            { TileID.Adamantite, 150 },
            { TileID.Titanium, 150 },
            { TileID.Chlorophyte, 200 },
            { TileID.LihzahrdBrick, 210 },
            { TileID.LihzahrdAltar, 210 },
        };

        // Cari MinPick suatu tile: cek tabel vanilla manual dulu, kalau bukan vanilla / gak ketemu, cek ModTile-nya.
        public static int GetTileMinPick(int tileType)
        {
            if (VanillaTileMinPick.TryGetValue(tileType, out int vanillaMinPick))
                return vanillaMinPick;

            ModTile modTile = TileLoader.GetTile(tileType);
            return modTile?.MinPick ?? 0;
        }

        // Hancurkan semua tile solid dalam radius lingkaran di sekitar suatu titik.
        // Tile yang butuh pickaxe power lebih tinggi dari maxPickPower bakal di-skip sama sekali.
        // WAJIB dipanggil cuma di server/singleplayer (Main.netMode != NetmodeID.MultiplayerClient) oleh si pemanggil.
        public static void DestroyTilesInRadius(Vector2 center, float radiusPixels, int maxPickPower)
        {
            int radiusTileUnits = (int)(radiusPixels / 16f) + 1;
            Point centerTile = center.ToTileCoordinates();

            for (int x = centerTile.X - radiusTileUnits; x <= centerTile.X + radiusTileUnits; x++)
            {
                for (int y = centerTile.Y - radiusTileUnits; y <= centerTile.Y + radiusTileUnits; y++)
                {
                    if (!WorldGen.InWorld(x, y))
                        continue;

                    Vector2 tileCenterWorld = new Vector2(x * 16 + 8, y * 16 + 8);
                    if (Vector2.Distance(center, tileCenterWorld) > radiusPixels)
                        continue;

                    Tile tile = Main.tile[x, y];
                    if (tile == null || !tile.HasTile)
                        continue;

                    // Block butuh pickaxe power lebih tinggi dari yang "dikuasai" saat ini -> skip, ga bisa dihancurin
                    if (GetTileMinPick(tile.TileType) > maxPickPower)
                        continue;

                    // noItem: false -> tile drop item-nya sendiri secara otomatis, PERSIS kayak kena bom vanilla
                    WorldGen.KillTile(x, y, fail: false, effectOnly: false, noItem: false);

                    if (Main.netMode == NetmodeID.Server)
                    {
                        NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, x, y);
                    }
                }
            }
        }
    }
}
