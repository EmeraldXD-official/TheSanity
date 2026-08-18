using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace TheSanity.WorldGeneration
{
	/// <summary>
	/// Jungle: sama kayak Forest (wall band 3-5 layer) tapi pakai JungleUnsafe,
	/// plus ditambah kesan rawa-rawa (kolam air dangkal) dan jurang-jurang curam.
	/// </summary>
	public static class JunglePass
	{
		private static readonly ushort[] JungleWalls = { WallID.JungleUnsafe };

		public static void Generate(UnifiedRandom rand)
		{
			for (int x = GenHelpers.WorldPadding; x < Main.maxTilesX - GenHelpers.WorldPadding; x++)
			{
				int surfaceY = GenHelpers.FindSurfaceY(x);
				if (surfaceY < 0)
					continue;

				Tile tile = Main.tile[x, surfaceY];
				if (tile.TileType != TileID.JungleGrass)
					continue;

				GenHelpers.ApplyWallBand(x, surfaceY, 3, 5, JungleWalls, rand);

				// ~8% chance mulai kolam rawa di titik ini
				if (rand.Next(100) < 8)
					CreateSwampPool(x, rand);

				// ~4% chance mulai jurang di titik ini
				if (rand.Next(100) < 4)
					CarveChasm(x, surfaceY, rand);
			}
		}

		/// <summary>
		/// Bikin genangan air dangkal di beberapa tile berturut-turut, kesan rawa becek.
		/// </summary>
		private static void CreateSwampPool(int startX, UnifiedRandom rand)
		{
			int width = rand.Next(5, 13);
			for (int x = startX; x < startX + width && x < Main.maxTilesX - GenHelpers.WorldPadding; x++)
			{
				int y = GenHelpers.FindSurfaceY(x);
				if (y < 0)
					continue;

				WorldGen.KillTile(x, y, false, false, true);
				Tile tile = Main.tile[x, y];
				tile.LiquidAmount = 200;
				tile.LiquidType = LiquidID.Water;
			}
		}

		/// <summary>
		/// Gali jurang sempit tapi dalam, dindingnya dikasih wall JungleUnsafe juga.
		/// </summary>
		private static void CarveChasm(int startX, int surfaceY, UnifiedRandom rand)
		{
			int width = rand.Next(2, 6);
			int depth = rand.Next(18, 45);

			for (int x = startX; x < startX + width && x < Main.maxTilesX - GenHelpers.WorldPadding; x++)
			{
				for (int y = surfaceY; y < surfaceY + depth && y < Main.maxTilesY; y++)
				{
					WorldGen.KillTile(x, y, false, false, true);
					Main.tile[x, y].WallType = WallID.JungleUnsafe;
				}
			}
		}
	}
}
