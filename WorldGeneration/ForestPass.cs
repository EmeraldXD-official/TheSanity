using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace TheSanity.WorldGeneration
{
	/// <summary>
	/// Forest: nambahin 3-5 layer Wall GrassUnsafe (ID 63) di antara Grass block dan udara,
	/// PLUS 5 layer tambahan ke ATAS permukaan (ke arah kanopi), berjejer sepanjang keseluruhan Forest.
	/// </summary>
	public static class ForestPass
	{
		// ID 63 vanilla = WallID.GrassUnsafe ("Grass Wall" natural, yang biasa muncul di gua dangkal Forest)
		private static readonly ushort[] ForestWalls = { WallID.GrassUnsafe };

		public static void Generate(UnifiedRandom rand)
		{
			for (int x = GenHelpers.WorldPadding; x < Main.maxTilesX - GenHelpers.WorldPadding; x++)
			{
				int surfaceY = GenHelpers.FindSurfaceY(x);
				if (surfaceY < 0)
					continue;

				Tile tile = Main.tile[x, surfaceY];

				// Cuma target Grass murni (bukan Jungle/Corrupt/Crimson/Hallow grass, itu punya TileID sendiri-sendiri)
				if (tile.TileType != TileID.Grass)
					continue;

				// 3-5 layer ke bawah (seperti biasa) + 5 layer tambahan ke ATAS permukaan
				GenHelpers.ApplyWallBand(x, surfaceY, 3, 5, ForestWalls, rand, extraUpwardLayers: 5);
			}
		}
	}
}
