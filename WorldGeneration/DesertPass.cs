using System;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace TheSanity.WorldGeneration
{
	/// <summary>
	/// Desert: permukaan dibiarkan alami (ga digelombangin lagi, biar surface tetap konsisten
	/// sama vanilla), fokus dekorasinya di pilar-pilar batu pasir bergaya "hoodoo" (menyempit
	/// di tengah, agak melebar lagi di puncak - kesan erosi angin gurun bertahun-tahun),
	/// lengkap dengan puing rubble di kaki pilar & lubang-lubang keropos alami.
	/// </summary>
	public static class DesertPass
	{
		public static void Generate(UnifiedRandom rand)
		{
			for (int x = GenHelpers.WorldPadding; x < Main.maxTilesX - GenHelpers.WorldPadding; x++)
			{
				int surfaceY = GenHelpers.FindSurfaceY(x);
				if (surfaceY < 0)
					continue;

				Tile tile = Main.tile[x, surfaceY];
				if (tile.TileType != TileID.Sand)
					continue;

				// pilar random, jarang, biar ga penuh sesak
				if (rand.Next(220) < 3)
					CreatePillar(x, surfaceY, rand);
			}
		}

		/// <summary>
		/// Pilar batu pasir bergaya "hoodoo": lebar penuh di kaki, menyempit di badan,
		/// lalu agak melebar lagi di puncak (tudung), dengan lubang-lubang keropos kecil
		/// di badannya dan sisa puing berserakan di kakinya.
		/// </summary>
		private static void CreatePillar(int startX, int surfaceY, UnifiedRandom rand)
		{
			int height = rand.Next(15, 50);
			int width = rand.Next(3, 6);
			int half = width / 2;

			ScatterBaseRubble(startX, half, rand);

			for (int level = 0; level < height; level++)
			{
				int y = surfaceY - level - 1;
				if (y < 0)
					break;

				float t = level / (float)height; // 0 = kaki, 1 = puncak
				int colWidth = ComputeHoodooWidth(width, t, rand);

				for (int dx = -colWidth / 2; dx <= colWidth / 2; dx++)
				{
					int x = startX + dx;
					if (x < GenHelpers.WorldPadding || x >= Main.maxTilesX - GenHelpers.WorldPadding)
						continue;

					// erosi: di bagian badan (bukan kaki/puncak) sering ada lubang keropos kecil
					bool midSection = t > 0.25f && t < 0.85f;
					if (midSection && rand.Next(100) < 6)
						continue;

					WorldGen.PlaceTile(x, y, TileID.Sandstone, mute: true, forced: true);
					Main.tile[x, y].WallType = WallID.Sandstone;
				}
			}
		}

		/// <summary>
		/// Lebar kolom pilar per ketinggian relatif (t: 0 kaki, 1 puncak). Bentuknya menyempit
		/// di tengah lalu melebar lagi di ujung atas (silhouette hoodoo khas erosi gurun),
		/// plus jitter kecil biar tiap pilar ga simetris kaku satu sama lain.
		/// </summary>
		private static int ComputeHoodooWidth(int baseWidth, float t, UnifiedRandom rand)
		{
			double waist = Math.Sin(t * Math.PI) * 0.6;             // maksimum penyempitan di tengah badan
			double capBulge = t > 0.85f ? (t - 0.85f) * 4.0 : 0.0;   // tudung sedikit melebar di puncak

			int w = baseWidth - (int)(waist * 1.5) + (int)capBulge;
			w += rand.Next(-1, 2); // jitter -1..1
			return Math.Max(1, w);
		}

		/// <summary>
		/// Sisa puing batu pasir kecil-kecil berserakan di sekitar kaki pilar,
		/// kesan udah berdiri lama & sebagian tererosi jatuh ke tanah.
		/// </summary>
		private static void ScatterBaseRubble(int startX, int half, UnifiedRandom rand)
		{
			int rubbleCount = rand.Next(4, 10);
			for (int i = 0; i < rubbleCount; i++)
			{
				int x = startX + rand.Next(-half - 4, half + 5);
				if (x < GenHelpers.WorldPadding || x >= Main.maxTilesX - GenHelpers.WorldPadding)
					continue;

				int y = GenHelpers.FindSurfaceY(x);
				if (y < 0 || Main.tile[x, y - 1].HasTile)
					continue;

				WorldGen.PlaceTile(x, y - 1, TileID.Sandstone, mute: true, forced: true);
			}
		}
	}
}
