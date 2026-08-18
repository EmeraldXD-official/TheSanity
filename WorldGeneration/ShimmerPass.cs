using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace TheSanity.WorldGeneration
{
	/// <summary>
	/// Shimmer: kolam shimmer vanilla biasanya kecil & polos. Di sini kita cari kolam shimmer
	/// yang udah di-generate vanilla, perbesar jadi danau yang jauh lebih luas, dan hias
	/// area sekelilingnya pakai Pearlstone biar kerasa kayak "zona sakral" tersendiri.
	/// </summary>
	public static class ShimmerPass
	{
		public static void Generate(UnifiedRandom rand)
		{
			if (!TryFindShimmerPool(out int foundX, out int foundY))
				return; // jaga-jaga kalau world ini kebetulan ga generate shimmer pool

			EnlargePool(foundX, foundY, rand);
			DecorateAroundPool(foundX, foundY, rand);
		}

		/// <summary>
		/// Nyari titik liquid Shimmer pertama yang ketemu di dunia (step 4 tile biar cepat).
		/// </summary>
		private static bool TryFindShimmerPool(out int foundX, out int foundY)
		{
			for (int x = GenHelpers.WorldPadding; x < Main.maxTilesX - GenHelpers.WorldPadding; x += 4)
			{
				for (int y = 50; y < Main.maxTilesY - 300; y += 4)
				{
					Tile tile = Main.tile[x, y];
					if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Shimmer)
					{
						foundX = x;
						foundY = y;
						return true;
					}
				}
			}

			foundX = -1;
			foundY = -1;
			return false;
		}

		/// <summary>
		/// Gali & isi liquid Shimmer dalam bentuk elips besar (lebih lebar daripada tinggi,
		/// biar kesan "danau", bukan sumur bundar).
		/// </summary>
		private static void EnlargePool(int cx, int cy, UnifiedRandom rand)
		{
			int radius = rand.Next(28, 42);

			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius / 2; dy <= radius / 2; dy++)
				{
					if (dx * dx + dy * dy * 4 > radius * radius)
						continue;

					int x = cx + dx, y = cy + dy;
					if (!GenHelpers.InBounds(x, y))
						continue;

					WorldGen.KillTile(x, y, false, false, true);
					Tile tile = Main.tile[x, y];
					tile.LiquidAmount = 255;
					tile.LiquidType = LiquidID.Shimmer;
				}
			}
		}

		/// <summary>
		/// Gantikan sebagian tile Stone di sekeliling danau jadi Pearlstone + wall khusus,
		/// biar area sekitar shimmer keliatan beda & "sakral", bukan cuma gua batu biasa.
		/// </summary>
		private static void DecorateAroundPool(int cx, int cy, UnifiedRandom rand)
		{
			int decorRadius = rand.Next(48, 65);

			for (int dx = -decorRadius; dx <= decorRadius; dx++)
			{
				for (int dy = -decorRadius / 2; dy <= decorRadius / 2; dy++)
				{
					int x = cx + dx, y = cy + dy;
					if (!GenHelpers.InBounds(x, y))
						continue;

					Tile tile = Main.tile[x, y];
					if (!tile.HasTile || tile.LiquidAmount > 0)
						continue;

					if (tile.TileType == TileID.Stone && rand.Next(100) < 25)
					{
						WorldGen.PlaceTile(x, y, TileID.Pearlstone, mute: true, forced: true);
						tile.WallType = WallID.PearlstoneBrickUnsafe;
					}
				}
			}
		}
	}
}
