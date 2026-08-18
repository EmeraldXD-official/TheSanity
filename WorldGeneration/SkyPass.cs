using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace TheSanity.WorldGeneration
{
	/// <summary>
	/// Sky: nambahin gerombolan awan (Cloud tile) yang tersebar acak di langit atas,
	/// bentuknya dibikin bulat/lonjong biar ga keliatan kotak-kotak kaku.
	/// </summary>
	public static class SkyPass
	{
		public static void Generate(UnifiedRandom rand)
		{
			int minY = 50;
			int maxY = (int)(Main.worldSurface * 0.28); // area langit atas, di atas kebanyakan pepohonan

			// jumlah awan disesuaikan lebar world biar konsisten di small/medium/large world
			int cloudCount = Main.maxTilesX / 70;

			for (int c = 0; c < cloudCount; c++)
			{
				int cx = rand.Next(GenHelpers.WorldPadding, Main.maxTilesX - GenHelpers.WorldPadding);
				int cy = rand.Next(minY, maxY);
				CreateCloudBlob(cx, cy, rand);
			}
		}

		/// <summary>
		/// Satu gumpalan awan berbentuk elips acak, disusun dari beberapa tile Cloud.
		/// </summary>
		private static void CreateCloudBlob(int cx, int cy, UnifiedRandom rand)
		{
			int radiusX = rand.Next(6, 14);
			int radiusY = rand.Next(2, 5);

			for (int dx = -radiusX; dx <= radiusX; dx++)
			{
				for (int dy = -radiusY; dy <= radiusY; dy++)
				{
					// rumus elips, dikasih sedikit random noise di pinggir biar ga mulus sempurna (kesan alami)
					double edge = (dx * dx) / (double)(radiusX * radiusX) + (dy * dy) / (double)(radiusY * radiusY);
					if (edge > 1.0 || (edge > 0.75 && rand.Next(100) < 40))
						continue;

					int x = cx + dx;
					int y = cy + dy;
					if (!GenHelpers.InBounds(x, y) || Main.tile[x, y].HasTile)
						continue;

					WorldGen.PlaceTile(x, y, TileID.Cloud, mute: true, forced: true);
				}
			}
		}
	}
}
