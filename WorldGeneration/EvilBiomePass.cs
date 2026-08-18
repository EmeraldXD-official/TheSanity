using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace TheSanity.WorldGeneration
{
	/// <summary>
	/// Crimson & Corruption: sama konsepnya kayak Forest (wall band 3-5 layer ke bawah
	/// + 5 layer ke atas permukaan), tapi wall-nya dipilih random dari 4 varian "Unsafe"
	/// biar teksturnya bervariasi, ga monoton satu jenis wall doang di sepanjang biome.
	/// </summary>
	public static class EvilBiomePass
	{
		private static readonly ushort[] CrimsonWalls =
		{
			WallID.CrimsonUnsafe1,
			WallID.CrimsonUnsafe2,
			WallID.CrimsonUnsafe3,
			WallID.CrimsonUnsafe4,
		};

		private static readonly ushort[] CorruptionWalls =
		{
			WallID.CorruptionUnsafe1,
			WallID.CorruptionUnsafe2,
			WallID.CorruptionUnsafe3,
			WallID.CorruptionUnsafe4,
		};

		public static void GenerateCrimson(UnifiedRandom rand)
		{
			for (int x = GenHelpers.WorldPadding; x < Main.maxTilesX - GenHelpers.WorldPadding; x++)
			{
				int surfaceY = GenHelpers.FindSurfaceY(x);
				if (surfaceY < 0)
					continue;

				Tile tile = Main.tile[x, surfaceY];
				if (tile.TileType != TileID.CrimsonGrass)
					continue;

				GenHelpers.ApplyWallBand(x, surfaceY, 3, 5, CrimsonWalls, rand, extraUpwardLayers: 5);
			}
		}

		public static void GenerateCorruption(UnifiedRandom rand)
		{
			for (int x = GenHelpers.WorldPadding; x < Main.maxTilesX - GenHelpers.WorldPadding; x++)
			{
				int surfaceY = GenHelpers.FindSurfaceY(x);
				if (surfaceY < 0)
					continue;

				Tile tile = Main.tile[x, surfaceY];
				if (tile.TileType != TileID.CorruptGrass)
					continue;

				GenHelpers.ApplyWallBand(x, surfaceY, 3, 5, CorruptionWalls, rand, extraUpwardLayers: 5);
			}
		}
	}
}
