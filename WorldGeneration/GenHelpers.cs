using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace TheSanity.WorldGeneration
{
	/// <summary>
	/// Kumpulan fungsi bantu yang dipakai berulang-ulang di semua Pass biome TheSanity.
	/// Taruh semua "helper" di sini biar tiap Pass file nggak duplikat logic yang sama.
	/// </summary>
	public static class GenHelpers
	{
		/// <summary>
		/// Batas kiri/kanan world yang "aman" untuk di-generate (skip border world biar ga error / ga ganggu ocean).
		/// </summary>
		public static int WorldPadding => 60;

		/// <summary>
		/// Cari tile Y pertama yang solid (padat) dari atas ke bawah di kolom x tertentu.
		/// Dipakai buat nentuin "permukaan" (surface) sebuah kolom, entah itu grass, sand, snow, dll.
		/// Return -1 kalau ga ketemu (harusnya jarang terjadi).
		/// </summary>
		public static int FindSurfaceY(int x, int startY = 80, int endY = -1)
		{
			if (endY < 0)
				endY = (int)Main.rockLayer + 100; // ga usah nyari sampe underground/cave, cukup sampe area rock layer atas

			for (int y = startY; y < endY && y < Main.maxTilesY; y++)
			{
				Tile tile = Main.tile[x, y];
				if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
					return y;
			}
			return -1;
		}

		/// <summary>
		/// Tempel wall di kolom x, mulai dari surfaceY sebanyak "layers" tile ke bawah,
		/// tapi cuma kalau slot wall-nya masih kosong (WallID.None) supaya ga nimpa wall gua alami / struktur lain.
		/// </summary>
		/// <param name="extraUpwardLayers">
		/// Opsional: berapa tile tambahan ke ATAS dari surfaceY yang juga ditempeli wall
		/// (dipakai buat Forest/Corruption/Crimson biar kanopinya kerasa lebih "tertutup").
		/// </param>
		public static void ApplyWallBand(int x, int surfaceY, int minLayers, int maxLayers, ushort[] wallOptions, UnifiedRandom rand, int extraUpwardLayers = 0)
		{
			int layers = rand.Next(minLayers, maxLayers + 1); // +1 karena UnifiedRandom.Next upper bound exclusive
			for (int i = 0; i < layers; i++)
			{
				int y = surfaceY + i;
				if (y >= Main.maxTilesY)
					break;

				Tile tile = Main.tile[x, y];
				if (tile.WallType == WallID.None)
				{
					tile.WallType = wallOptions[rand.Next(wallOptions.Length)];
				}
			}

			for (int i = 1; i <= extraUpwardLayers; i++)
			{
				int y = surfaceY - i;
				if (y < 0)
					break;

				Tile tile = Main.tile[x, y];
				if (tile.WallType == WallID.None)
				{
					tile.WallType = wallOptions[rand.Next(wallOptions.Length)];
				}
			}
		}

		public static bool InBounds(int x, int y)
		{
			return x >= 0 && x < Main.maxTilesX && y >= 0 && y < Main.maxTilesY;
		}
	}
}
