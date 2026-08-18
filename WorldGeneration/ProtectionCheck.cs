using System;
using Terraria;
using Terraria.ID;

namespace TheSanity.WorldGeneration
{
	/// <summary>
	/// Dipakai sebelum nge-generate struktur besar di underground, buat mastiin kita ga nabrak
	/// Dungeon, Jungle Temple (Lihzahrd), area spawn, atau Underworld.
	/// Ini krusial: struktur custom yang numpuk/nabrak Dungeon bisa bikin loot vanilla ketimpa,
	/// atau bahkan bikin Old Man / entrance Dungeon ketutupan.
	/// </summary>
	public static class ProtectionCheck
	{
		// Kalau nemu salah satu tile ini di sekitar titik target, area itu dianggap "terlindungi".
		private static readonly int[] ProtectedTileTypes =
		{
			TileID.BlueDungeonBrick,
			TileID.GreenDungeonBrick,
			TileID.PinkDungeonBrick,
			TileID.Bookcases, // rak buku khas Dungeon
			TileID.LihzahrdBrick,
			TileID.LihzahrdAltar,
			TileID.WoodenSpikes, // sering nempel jalur trap Dungeon/Jungle Temple
		};

		/// <summary>
		/// True kalau titik (x, y) sebaiknya DIHINDARI untuk generate struktur baru.
		/// Cek 3 hal: (1) ada tile "penanda" struktur penting di sekitar radius,
		/// (2) terlalu dekat titik Dungeon/spawn vanilla, (3) sudah masuk area Underworld.
		/// </summary>
		public static bool IsProtected(int x, int y, int radius)
		{
			// 1) scan tile penanda struktur penting (step 2 tile biar ga terlalu berat di performa)
			for (int dx = -radius; dx <= radius; dx += 2)
			{
				for (int dy = -radius; dy <= radius; dy += 2)
				{
					int px = x + dx, py = y + dy;
					if (!GenHelpers.InBounds(px, py))
						continue;

					Tile tile = Main.tile[px, py];
					if (!tile.HasTile)
						continue;

					for (int i = 0; i < ProtectedTileTypes.Length; i++)
					{
						if (tile.TileType == ProtectedTileTypes[i])
							return true;
					}
				}
			}

			// 2) jarak ke titik Dungeon & spawn vanilla (Main.dungeonX/Y sudah diisi vanilla worldgen)
			if (Distance(x, y, Main.dungeonX, Main.dungeonY) < 180)
				return true;

			if (Distance(x, y, Main.spawnTileX, Main.spawnTileY) < 90)
				return true;

			// 3) area Underworld (biar ga tabrakan sama MiniBiomePass.EnhanceHell)
			if (y > Main.maxTilesY - 280)
				return true;

			return false;
		}

		private static double Distance(int x1, int y1, int x2, int y2)
		{
			double dx = x1 - x2;
			double dy = y1 - y2;
			return Math.Sqrt(dx * dx + dy * dy);
		}
	}
}
