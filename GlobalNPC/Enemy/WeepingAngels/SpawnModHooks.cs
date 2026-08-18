using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Content.NPCs.WeepingAngel
{
    // =====================================================================
    // Custom spawn system buat Weeping Angel.
    //
    // Kenapa custom (bukan lewat NPCSpawnInfo/EditSpawnPool biasa)?
    // Karena requirement-nya sangat spesifik posisi (butuh 2x3 tile kosong,
    // lantai rata, & jarak minimum ke statue lain) yang ga bisa diatur lewat
    // vanilla spawn pool. Jadi kita bikin "spawner" sendiri yang jalan tiap
    // beberapa tick, nyari titik valid di sekitar player yang lagi di
    // Underground/Cavern.
    // =====================================================================
    public class SpawnModHooks : ModSystem
    {
        // Nyimpen posisi semua Weeping Angel yang lagi hidup di dunia (tile coords)
        private static readonly List<Point> ActiveAngelTilePositions = new List<Point>();

        private const int BaseMinDistanceTiles = 1000;
        private const int ReducedMinDistanceTiles = 500; // kalau player pegang item 52
        private const int AngelStatueItemId = 52; // "Angel Statue"

        // Kesempatan cek spawn per player per tick (jangan kegedean biar ga lag)
        private const float BaseSpawnCheckChance = 1f / 600f;      // ~tiap 10 detik/player
        private const float BoostedSpawnCheckChanceMult = 1.20f;   // +20% kalau bawa item 52

        public static void RegisterAngel(NPC npc)
        {
            ActiveAngelTilePositions.Add(npc.Center.ToTileCoordinates());
        }

        public static void UnregisterAngel(NPC npc)
        {
            Point p = npc.Center.ToTileCoordinates();
            // Hapus entry terdekat (toleransi kecil karena posisi bisa geser dikit)
            for (int i = ActiveAngelTilePositions.Count - 1; i >= 0; i--)
            {
                if (Vector2.Distance(ActiveAngelTilePositions[i].ToVector2(), p.ToVector2()) < 4)
                {
                    ActiveAngelTilePositions.RemoveAt(i);
                    break;
                }
            }
        }

        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return; // spawn logic cuma server/singleplayer

            foreach (Player player in Main.player)
            {
                if (!player.active || player.dead) continue;
                if (!(player.ZoneDirtLayerHeight || player.ZoneRockLayerHeight)) continue;

                bool hasAngelStatueItem = PlayerHasItem(player, AngelStatueItemId);
                float chance = BaseSpawnCheckChance * (hasAngelStatueItem ? BoostedSpawnCheckChanceMult : 1f);

                if (Main.rand.NextFloat() > chance) continue;

                int minDistanceTiles = hasAngelStatueItem ? ReducedMinDistanceTiles : BaseMinDistanceTiles;
                TrySpawnNear(player, minDistanceTiles);
            }
        }

        private static bool PlayerHasItem(Player player, int itemType)
        {
            for (int i = 0; i < player.inventory.Length; i++)
            {
                if (player.inventory[i].type == itemType && player.inventory[i].stack > 0) return true;
            }
            return false;
        }

        private static void TrySpawnNear(Player player, int minDistanceTiles)
        {
            // Pilih titik acak offscreen di sekitar player buat dicoba.
            int tileX = (int)(player.Center.X / 16f) + Main.rand.Next(-60, 61);
            int tileY = (int)(player.Center.Y / 16f) + Main.rand.Next(-40, 41);

            if (!WorldGen.InWorld(tileX, tileY, 10)) return;

            // Cek jarak minimum ke statue lain
            foreach (var existing in ActiveAngelTilePositions)
            {
                float distTiles = Vector2.Distance(existing.ToVector2(), new Vector2(tileX, tileY));
                if (distTiles < minDistanceTiles) return;
            }

            if (!IsAreaValidForStatue(tileX, tileY)) return;

            FlattenFloor(tileX, tileY);

            Vector2 worldPos = new Vector2(tileX * 16f, tileY * 16f - WeepingAngel.StatueTilesTall * 16f + 16f);
            int npcIndex = NPC.NewNPC(new Terraria.DataStructures.EntitySource_SpawnNPC(), (int)worldPos.X, (int)worldPos.Y, ModContent.NPCType<WeepingAngel>());
        }

        /// <summary>
        /// Area 2x3 kosong (tembus/walkable) di atas, dengan lantai solid rata
        /// di bawahnya.
        /// </summary>
        private static bool IsAreaValidForStatue(int baseX, int baseY)
        {
            // baseY dianggap baris lantai. Statue butuh 3 tile kosong DI ATAS lantai,
            // dan 2 tile lebar.
            for (int dx = 0; dx < WeepingAngel.StatueTilesWide; dx++)
            {
                for (int dy = 1; dy <= WeepingAngel.StatueTilesTall; dy++)
                {
                    Tile t = Main.tile[baseX + dx, baseY - dy];
                    if (t == null || t.HasTile && Main.tileSolid[t.TileType])
                    {
                        return false; // kepentok tile solid, ga ada ruang
                    }
                }

                Tile floorTile = Main.tile[baseX + dx, baseY];
                if (floorTile == null || !floorTile.HasTile || !Main.tileSolid[floorTile.TileType])
                {
                    return false; // lantai ga solid/nggak nutup penuh
                }
            }
            return true;
        }

        /// <summary>
        /// Kalau lantai di bawah statue ada yang miring (hammered/slope), kita
        /// paksa balikin jadi rata (slope = none) biar statue nempel presisi.
        /// </summary>
        private static void FlattenFloor(int baseX, int baseY)
        {
            for (int dx = 0; dx < WeepingAngel.StatueTilesWide; dx++)
            {
                int x = baseX + dx;
                Tile t = Main.tile[x, baseY];
                if (t != null && t.HasTile && (t.Slope != Terraria.ID.SlopeType.Solid || t.IsHalfBlock))
                {
                    WorldGen.SlopeTile(x, baseY, 0);
                    t.IsHalfBlock = false;
                    Terraria.NetMessage.SendTileSquare(-1, x, baseY, 1);
                }
            }
        }
    }
}
