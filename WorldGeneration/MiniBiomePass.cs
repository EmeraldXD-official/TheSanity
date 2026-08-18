using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace TheSanity.WorldGeneration
{
	/// <summary>
	/// Mini/Micro Biome enhancement: Queen Bee hive, Granite, Marble, dan Hell (Underworld)
	/// diperbesar & diberi variasi tambahan setelah generation vanilla-nya selesai,
	/// biar kesannya lebih "ada isinya" ala Remnant Lite, bukan cuma kotak kecil doang.
	/// </summary>
	public static class MiniBiomePass
	{
		// ================= QUEEN BEE (Bee Hive) =================

		public static void EnhanceQueenBee(UnifiedRandom rand)
		{
			int top = (int)Main.worldSurface;
			int bottom = (int)Main.rockLayer + 250;

			for (int x = GenHelpers.WorldPadding; x < Main.maxTilesX - GenHelpers.WorldPadding; x++)
			{
				for (int y = top; y < bottom && y < Main.maxTilesY; y++)
				{
					Tile tile = Main.tile[x, y];
					if (!tile.HasTile || tile.TileType != TileID.Hive)
						continue;

					// jangan expand tiap tile hive, cukup beberapa titik acak biar ga super lambat & ga meledak ukurannya
					if (rand.Next(400) < 1)
						ExpandHive(x, y, rand);
				}
			}
		}

		private static void ExpandHive(int cx, int cy, UnifiedRandom rand)
		{
			int radius = rand.Next(6, 14);
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					if (dx * dx + dy * dy > radius * radius)
						continue;

					int x = cx + dx, y = cy + dy;
					if (!GenHelpers.InBounds(x, y))
						continue;

					Tile tile = Main.tile[x, y];
					if (!tile.HasTile && rand.Next(100) < 20)
					{
						WorldGen.PlaceTile(x, y, TileID.Hive, mute: true, forced: true);
						tile.WallType = WallID.HiveUnsafe;
					}

					// sisipan kolam madu kecil di pinggiran biar makin niat
					if (tile.HasTile && tile.TileType == TileID.Hive && rand.Next(100) < 3)
					{
						WorldGen.KillTile(x, y, false, false, true);
						tile.LiquidAmount = 200;
						tile.LiquidType = LiquidID.Honey;
					}
				}
			}
		}

		// ================= GRANITE =================

		public static void EnhanceGranite(UnifiedRandom rand)
		{
			ExpandMiniBiome(TileID.Granite, TileID.Granite, rand, chancePerTile: 500, minRadius: 8, maxRadius: 18);
		}

		// ================= MARBLE =================

		public static void EnhanceMarble(UnifiedRandom rand)
		{
			ExpandMiniBiome(TileID.Marble, TileID.Marble, rand, chancePerTile: 500, minRadius: 8, maxRadius: 18);
		}

		/// <summary>
		/// Fungsi umum buat "menggembungkan" mini biome underground (Granite/Marble) biar radiusnya
		/// lebih luas & bentuknya ga bulat sempurna kayak vanilla.
		/// </summary>
		private static void ExpandMiniBiome(ushort searchTileType, ushort placeTileType, UnifiedRandom rand, int chancePerTile, int minRadius, int maxRadius)
		{
			int top = (int)Main.rockLayer;
			int bottom = Main.maxTilesY - 250;

			for (int x = GenHelpers.WorldPadding; x < Main.maxTilesX - GenHelpers.WorldPadding; x++)
			{
				for (int y = top; y < bottom; y++)
				{
					Tile tile = Main.tile[x, y];
					if (!tile.HasTile || tile.TileType != searchTileType)
						continue;

					if (rand.Next(chancePerTile) != 0)
						continue;

					int radius = rand.Next(minRadius, maxRadius + 1);
					for (int dx = -radius; dx <= radius; dx++)
					{
						for (int dy = -radius; dy <= radius; dy++)
						{
							// noise kecil di jarak biar pinggirnya ga rapi bulat sempurna
							double jitter = rand.Next(-15, 16) * 0.01;
							if (dx * dx + dy * dy > radius * radius * (1 + jitter))
								continue;

							int px = x + dx, py = y + dy;
							if (!GenHelpers.InBounds(px, py))
								continue;

							Tile t = Main.tile[px, py];
							if (t.HasTile && t.TileType != TileID.Stone && t.TileType != searchTileType)
								continue; // jangan nimpa ore/struktur lain

							if (rand.Next(100) < 55)
								WorldGen.PlaceTile(px, py, placeTileType, mute: true, forced: true);
						}
					}
				}
			}
		}

		// ================= HELL (UNDERWORLD) =================

		public static void EnhanceHell(UnifiedRandom rand)
		{
			int hellTop = Main.maxTilesY - 250;
			int hellBottom = Main.maxTilesY - 40;

			for (int x = GenHelpers.WorldPadding; x < Main.maxTilesX - GenHelpers.WorldPadding; x++)
			{
				if (rand.Next(260) < 1)
				{
					int y = rand.Next(hellTop, hellBottom);
					CreateObsidianFormation(x, y, rand);
				}

				if (rand.Next(350) < 1)
				{
					int y = rand.Next(hellTop, hellBottom);
					CreateAshPile(x, y, rand);
				}
			}
		}

		private static void CreateObsidianFormation(int cx, int cy, UnifiedRandom rand)
		{
			int size = rand.Next(4, 11);
			for (int dx = -size; dx <= size; dx++)
			{
				for (int dy = -size; dy <= size; dy++)
				{
					if (dx * dx + dy * dy > size * size)
						continue;

					int x = cx + dx, y = cy + dy;
					if (!GenHelpers.InBounds(x, y))
						continue;

					WorldGen.PlaceTile(x, y, TileID.Obsidian, mute: true, forced: true);
				}
			}
		}

		private static void CreateAshPile(int cx, int cy, UnifiedRandom rand)
		{
			int width = rand.Next(6, 16);
			int height = rand.Next(3, 7);
			for (int dx = -width / 2; dx <= width / 2; dx++)
			{
				int colHeight = height - rand.Next(0, 3);
				for (int dy = 0; dy < colHeight; dy++)
				{
					int x = cx + dx, y = cy + dy;
					if (!GenHelpers.InBounds(x, y))
						continue;

					if (!Main.tile[x, y].HasTile)
						WorldGen.PlaceTile(x, y, TileID.Ash, mute: true, forced: true);
				}
			}
		}
	}
}
