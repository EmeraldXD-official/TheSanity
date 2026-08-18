using System;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace TheSanity.WorldGeneration
{
	/// <summary>
	/// Snow: bikin surface-nya jauh lebih bervariasi & menjulang, kesan "pegunungan tinggi",
	/// tapi transisi antar kolom di-clamp slope-nya biar ga ada tebing tegak lurus dadakan,
	/// dan smoothing-nya berbentuk lereng talus miring (bukan nge-fill 1 kolom vertikal lurus)
	/// biar keliatan kayak longsoran batu alami, bukan tembok buatan.
	/// </summary>
	public static class SnowPass
	{
		private const int MaxSlopePerTile = 3;

		public static void Generate(UnifiedRandom rand)
		{
			double seedA = rand.Next(1000);
			double seedB = rand.Next(1000);
			double seedC = rand.Next(1000);

			int minX = GenHelpers.WorldPadding;
			int maxX = Main.maxTilesX - GenHelpers.WorldPadding;
			int count = maxX - minX;

			int[] surfaceYs = new int[count];
			int[] rawHeight = new int[count];

			// tahap 1: hitung noise mentah dulu per kolom (belum ditempel)
			for (int x = minX; x < maxX; x++)
			{
				int idx = x - minX;
				int surfaceY = GenHelpers.FindSurfaceY(x);
				surfaceYs[idx] = surfaceY;

				if (surfaceY < 0)
				{
					rawHeight[idx] = -1;
					continue;
				}

				Tile tile = Main.tile[x, surfaceY];
				if (tile.TileType != TileID.SnowBlock && tile.TileType != TileID.IceBlock)
				{
					rawHeight[idx] = -1;
					continue;
				}

				// Kombinasi beberapa sine wave = noise sederhana biar hasilnya ga simetris & terkesan alami.
				double noise =
					Math.Sin((x + seedA) * 0.006) * 26 +   // punggung gunung besar
					Math.Sin((x + seedB) * 0.02) * 10 +    // bukit sedang
					Math.Sin((x + seedC) * 0.06) * 4;      // detail kecil

				rawHeight[idx] = (int)Math.Max(0, noise);
			}

			// tahap 2: clamp slope antar kolom bertetangga (dua arah) biar transisinya bertahap,
			// ga ada lompatan tinggi ekstrem yang bikin dinding tegak lurus dadakan
			int[] clamped = (int[])rawHeight.Clone();

			for (int i = 1; i < count; i++)
			{
				if (clamped[i] < 0 || clamped[i - 1] < 0)
					continue;

				int diff = clamped[i] - clamped[i - 1];
				if (diff > MaxSlopePerTile)
					clamped[i] = clamped[i - 1] + MaxSlopePerTile;
				else if (diff < -MaxSlopePerTile)
					clamped[i] = clamped[i - 1] - MaxSlopePerTile;
			}
			for (int i = count - 2; i >= 0; i--)
			{
				if (clamped[i] < 0 || clamped[i + 1] < 0)
					continue;

				int diff = clamped[i] - clamped[i + 1];
				if (diff > MaxSlopePerTile)
					clamped[i] = clamped[i + 1] + MaxSlopePerTile;
				else if (diff < -MaxSlopePerTile)
					clamped[i] = clamped[i + 1] - MaxSlopePerTile;
			}

			// tahap 3: tempel tile sesuai height yang udah landai, plus sedikit erosi di dekat puncak
			// biar puncaknya keropos & ga rata kayak dipotong penggaris
			for (int x = minX; x < maxX; x++)
			{
				int idx = x - minX;
				int surfaceY = surfaceYs[idx];
				int extraHeight = clamped[idx];
				if (surfaceY < 0 || extraHeight <= 0)
					continue;

				for (int i = 1; i <= extraHeight; i++)
				{
					int y = surfaceY - i;
					if (y < 0)
						break;

					bool nearPeak = i > extraHeight - 3;
					if (nearPeak && rand.Next(100) < 22)
						continue;

					if (!Main.tile[x, y].HasTile)
						WorldGen.PlaceTile(x, y, TileID.SnowBlock, mute: true, forced: true);
				}
			}

			SmoothTalus(minX, maxX, rand);
		}

		/// <summary>
		/// Kalau masih ada perbedaan tinggi tajam antar kolom bertetangga (biasanya dari surface
		/// vanilla asli, bukan dari noise kita yang udah di-clamp), isi bertahap membentuk lereng
		/// talus miring menyebar ke beberapa kolom kiri-kanan, BUKAN nge-fill lurus satu kolom
		/// vertikal, biar keliatan kayak reruntuhan batu longsor alami, bukan tembok siku-siku.
		/// </summary>
		private static void SmoothTalus(int minX, int maxX, UnifiedRandom rand)
		{
			for (int x = minX + 1; x < maxX - 1; x++)
			{
				int hL = GenHelpers.FindSurfaceY(x - 1);
				int hC = GenHelpers.FindSurfaceY(x);
				int hR = GenHelpers.FindSurfaceY(x + 1);
				if (hL < 0 || hC < 0 || hR < 0)
					continue;

				// hC lebih kecil = lebih tinggi (Y makin ke bawah makin besar)
				if (hC < hL - 4 && hC < hR - 4)
				{
					int target = (hL + hR) / 2 - 2;
					int span = target - hC;
					if (span <= 0)
						continue;

					int rampWidth = Math.Min(5, span);
					for (int rx = -rampWidth; rx <= rampWidth; rx++)
					{
						int cx = x + rx;
						if (cx < minX || cx >= maxX)
							continue;

						// makin jauh dari pusat lubang, makin sedikit yang diisi -> bentuk landai
						int localTarget = hC + (int)((float)Math.Abs(rx) / rampWidth * span);
						int colSurface = GenHelpers.FindSurfaceY(cx);
						if (colSurface < 0)
							continue;

						for (int y = colSurface; y < localTarget; y++)
						{
							if (y < 0 || y >= Main.maxTilesY)
								continue;

							if (!Main.tile[cx, y].HasTile && rand.Next(100) < 85)
								WorldGen.PlaceTile(cx, y, TileID.SnowBlock, mute: true, forced: true);
						}
					}
				}
			}
		}
	}
}
