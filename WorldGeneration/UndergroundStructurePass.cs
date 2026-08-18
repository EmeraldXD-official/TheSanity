using System;
using Terraria;
using Terraria.ID;
using Terraria.Utilities;

namespace TheSanity.WorldGeneration
{
	/// <summary>
	/// Nyebarin struktur-struktur besar di seluruh lapisan underground (bukan cuma satu titik),
	/// biar eksplorasi bawah tanah kerasa "ada isinya" ala Remnant, bukan cuma gua kosong + ore.
	/// Setiap titik kandidat dicek dulu ke ProtectionCheck supaya ga nabrak Dungeon, Jungle Temple,
	/// spawn, atau Underworld. 6 varian struktur biar eksplorasi ga ngebosenin & kerasa lebih "premium":
	/// Vaulted Chamber, Ruined Tower, Crystal Cavern, Mineshaft Tunnel, Ancient Library, dan
	/// Collapsed Colonnade (reruntuhan pilar terbuka).
	/// </summary>
	public static class UndergroundStructurePass
	{
		public static void Generate(UnifiedRandom rand)
		{
			int minY = (int)Main.rockLayer + 40;
			int maxY = Main.maxTilesY - 300;

			// jumlah struktur nyesuain lebar world (world Large dapet lebih banyak).
			// Sekarang lebih rapat dari sebelumnya karena variannya udah lebih banyak & beragam.
			int targetCount = Main.maxTilesX / 180;
			int maxAttempts = targetCount * 10; // batasi biar ga infinite loop kalau banyak spot ke-block

			int placed = 0;
			int attempts = 0;

			while (placed < targetCount && attempts < maxAttempts)
			{
				attempts++;

				int x = rand.Next(GenHelpers.WorldPadding + 60, Main.maxTilesX - GenHelpers.WorldPadding - 60);
				int y = rand.Next(minY, maxY);

				if (ProtectionCheck.IsProtected(x, y, 45))
					continue;

				switch (rand.Next(6))
				{
					case 0:
						BuildVaultedChamber(x, y, rand);
						break;
					case 1:
						BuildRuinedTower(x, y, rand);
						break;
					case 2:
						BuildCrystalCavern(x, y, rand);
						break;
					case 3:
						BuildMineshaftTunnel(x, y, rand);
						break;
					case 4:
						BuildAncientLibrary(x, y, rand);
						break;
					default:
						BuildCollapsedColonnade(x, y, rand);
						break;
				}

				placed++;
			}
		}

		// ================= TIPE 1: RUANGAN BUNDAR DENGAN PILAR =================

		private static void BuildVaultedChamber(int cx, int cy, UnifiedRandom rand)
		{
			int radius = rand.Next(9, 14);

			// gali ruangan bundar
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					if (dx * dx + dy * dy > radius * radius)
						continue;

					int x = cx + dx, y = cy + dy;
					if (!GenHelpers.InBounds(x, y))
						continue;

					WorldGen.KillTile(x, y, false, false, true);
					Main.tile[x, y].WallType = WallID.GrayBrick;
				}
			}

			// lantai
			int floorY = cy + radius - 1;
			for (int dx = -radius + 1; dx <= radius - 1; dx++)
			{
				int x = cx + dx;
				if (GenHelpers.InBounds(x, floorY))
					WorldGen.PlaceTile(x, floorY, TileID.GrayBrick, mute: true, forced: true);
			}

			// 2 pilar kiri-kanan, sekarang dikasih obor biar ruangannya keliatan "hidup"
			int pillarOffset = radius - 3;
			int[] pillarXs = { cx - pillarOffset, cx + pillarOffset };
			foreach (int px in pillarXs)
			{
				for (int y = cy - radius + 2; y < floorY; y++)
				{
					if (GenHelpers.InBounds(px, y))
						WorldGen.PlaceTile(px, y, TileID.GrayBrick, mute: true, forced: true);
				}

				int torchY = floorY - 2;
				if (GenHelpers.InBounds(px, torchY) && !Main.tile[px, torchY].HasTile)
					WorldGen.PlaceTile(px, torchY, TileID.Torches, mute: true, forced: true);
			}

			FillChestWithLoot(cx, floorY - 1, rand);
		}

		// ================= TIPE 2: MENARA RERUNTUHAN VERTIKAL =================

		private static void BuildRuinedTower(int cx, int topY, UnifiedRandom rand)
		{
			int width = rand.Next(5, 8);
			int height = rand.Next(35, 70);
			int half = width / 2;

			for (int dy = 0; dy < height; dy++)
			{
				int y = topY + dy;

				for (int dx = -half; dx <= half; dx++)
				{
					int x = cx + dx;
					if (!GenHelpers.InBounds(x, y))
						continue;

					bool isEdge = Math.Abs(dx) == half;
					if (isEdge)
					{
						WorldGen.PlaceTile(x, y, TileID.GrayBrick, mute: true, forced: true);
					}
					else
					{
						WorldGen.KillTile(x, y, false, false, true);
						Main.tile[x, y].WallType = WallID.GrayBrick;

						// cobweb nyangkut di sela-sela interior, kesan menara tua ditinggalkan lama
						if (rand.Next(150) < 2)
							WorldGen.PlaceTile(x, y, TileID.Cobweb, mute: true, forced: true);
					}
				}

				// platform tiap 6 tile biar bisa dipanjat/dieksplor
				if (dy % 6 == 0 && dy > 0)
				{
					for (int dx = -half + 1; dx <= half - 1; dx++)
					{
						int x = cx + dx;
						if (GenHelpers.InBounds(x, y))
							WorldGen.PlaceTile(x, y, TileID.Platforms, mute: true, forced: true);
					}
				}
			}

			FillChestWithLoot(cx, topY + height - 3, rand);
		}

		// ================= TIPE 3: GUA KRISTAL =================

		private static void BuildCrystalCavern(int cx, int cy, UnifiedRandom rand)
		{
			int radius = rand.Next(10, 16);

			// gali gua berbentuk blob (ga bundar sempurna)
			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					double jitter = rand.Next(-20, 21) * 0.01;
					if (dx * dx + dy * dy > radius * radius * (1 + jitter))
						continue;

					int x = cx + dx, y = cy + dy;
					if (!GenHelpers.InBounds(x, y))
						continue;

					WorldGen.KillTile(x, y, false, false, true);
				}
			}

			// taburi kristal & gem di dinding gua
			ushort[] gemTiles =
			{
				TileID.Amethyst, TileID.Topaz, TileID.Sapphire,
				TileID.Emerald, TileID.Ruby, TileID.Diamond,
			};

			for (int dx = -radius; dx <= radius; dx++)
			{
				for (int dy = -radius; dy <= radius; dy++)
				{
					int x = cx + dx, y = cy + dy;
					if (!GenHelpers.InBounds(x, y) || Main.tile[x, y].HasTile)
						continue;

					if (!HasSolidNeighbor(x, y))
						continue;

					if (rand.Next(100) < 6)
						WorldGen.PlaceTile(x, y, TileID.Crystals, mute: true, forced: true);
					else if (rand.Next(150) < 3)
						WorldGen.PlaceTile(x, y, gemTiles[rand.Next(gemTiles.Length)], mute: true, forced: true);
				}
			}
		}

		private static bool HasSolidNeighbor(int x, int y)
		{
			int[] dxs = { 1, -1, 0, 0 };
			int[] dys = { 0, 0, 1, -1 };

			for (int i = 0; i < 4; i++)
			{
				int nx = x + dxs[i], ny = y + dys[i];
				if (GenHelpers.InBounds(nx, ny) && Main.tile[nx, ny].HasTile)
					return true;
			}
			return false;
		}

		// ================= TIPE 4: TEROWONGAN TAMBANG TUA (MINESHAFT) =================

		/// <summary>
		/// Terowongan horizontal panjang dengan tiang penyangga kayu + palang atas (headframe)
		/// ala tambang tua, lantai papan, obor nempel di tiang, sarang laba-laba di beberapa titik,
		/// dan chest di ujung terowongan. Jalurnya sengaja naik-turun dikit biar ga lurus kaku.
		/// </summary>
		private static void BuildMineshaftTunnel(int cx, int cy, UnifiedRandom rand)
		{
			int length = rand.Next(45, 95);
			int direction = rand.Next(2) == 0 ? -1 : 1;
			const int tunnelHeight = 4;

			int curY = cy;
			bool chestPlaced = false;

			for (int i = 0; i < length; i++)
			{
				int x = cx + direction * i;
				if (!GenHelpers.InBounds(x, curY))
					break;

				// jaga-jaga: kalau jalurnya udah mepet ke struktur terlindungi, stop lebih awal
				if (i % 10 == 0 && ProtectionCheck.IsProtected(x, curY, 20))
					break;

				// jalur naik-turun dikit biar ga lurus sempurna kayak digambar penggaris
				if (rand.Next(100) < 18)
					curY += rand.Next(-1, 2);

				for (int dy = 0; dy < tunnelHeight; dy++)
				{
					int y = curY + dy;
					if (GenHelpers.InBounds(x, y))
						WorldGen.KillTile(x, y, false, false, true);
				}

				int floorY = curY + tunnelHeight;
				if (GenHelpers.InBounds(x, floorY))
					WorldGen.PlaceTile(x, floorY, TileID.WoodBlock, mute: true, forced: true);

				// tiang penyangga kayu tiap ~6 tile, gaya khas terowongan tambang tua
				if (i % 6 == 0)
				{
					for (int dy = 0; dy < tunnelHeight; dy++)
					{
						int y = curY + dy;
						if (GenHelpers.InBounds(x, y))
							WorldGen.PlaceTile(x, y, TileID.WoodBlock, mute: true, forced: true);
					}

					for (int hx = -1; hx <= 1; hx++)
					{
						int px = x + hx;
						if (GenHelpers.InBounds(px, curY - 1))
							WorldGen.PlaceTile(px, curY - 1, TileID.WoodBlock, mute: true, forced: true);
					}

					if (GenHelpers.InBounds(x, curY + 1) && !Main.tile[x, curY + 1].HasTile)
						WorldGen.PlaceTile(x, curY + 1, TileID.Torches, mute: true, forced: true);
				}

				// cobweb nyangkut di beberapa titik langit-langit, kesan tambang lama & terbengkalai
				if (rand.Next(100) < 5 && GenHelpers.InBounds(x, curY) && !Main.tile[x, curY].HasTile)
					WorldGen.PlaceTile(x, curY, TileID.Cobweb, mute: true, forced: true);

				if (!chestPlaced && i > length - 5)
				{
					FillChestWithLoot(x, floorY - 1, rand);
					chestPlaced = true;
				}
			}
		}

		// ================= TIPE 5: PERPUSTAKAAN KUNO =================

		/// <summary>
		/// Ruangan persegi berdinding GrayBrick, dijejer rak buku di kedua sisi dinding,
		/// ada meja-kursi baca di tengah, dan obor di dinding atas. Kesan reruntuhan
		/// perpustakaan kuno yang terkubur, bukan cuma ruangan kosong.
		/// </summary>
		private static void BuildAncientLibrary(int cx, int cy, UnifiedRandom rand)
		{
			int width = rand.Next(14, 22);
			int height = rand.Next(8, 12);
			int left = cx - width / 2;
			int top = cy - height / 2;

			for (int dx = 0; dx < width; dx++)
			{
				for (int dy = 0; dy < height; dy++)
				{
					int x = left + dx, y = top + dy;
					if (!GenHelpers.InBounds(x, y))
						continue;

					bool isWall = dx == 0 || dx == width - 1 || dy == 0 || dy == height - 1;
					if (isWall)
					{
						WorldGen.PlaceTile(x, y, TileID.GrayBrick, mute: true, forced: true);
					}
					else
					{
						WorldGen.KillTile(x, y, false, false, true);
						Main.tile[x, y].WallType = WallID.GrayBrick;
					}
				}
			}

			int floorY = top + height - 1;

			// rak buku berjejer nempel di dinding kiri & kanan
			for (int dy = 1; dy < height - 1; dy++)
			{
				int yRow = top + dy;

				int xLeft = left + 1;
				if (GenHelpers.InBounds(xLeft, yRow) && !Main.tile[xLeft, yRow].HasTile && rand.Next(100) < 75)
					WorldGen.PlaceTile(xLeft, yRow, TileID.Bookcases, mute: true, forced: true);

				int xRight = left + width - 2;
				if (GenHelpers.InBounds(xRight, yRow) && !Main.tile[xRight, yRow].HasTile && rand.Next(100) < 75)
					WorldGen.PlaceTile(xRight, yRow, TileID.Bookcases, mute: true, forced: true);
			}

			// meja & kursi baca di tengah ruangan
			int tableX = cx - 1;
			if (GenHelpers.InBounds(tableX, floorY - 1))
				WorldGen.PlaceTile(tableX, floorY - 1, TileID.Tables, mute: true, forced: true);
			if (GenHelpers.InBounds(tableX + 2, floorY - 1))
				WorldGen.PlaceTile(tableX + 2, floorY - 1, TileID.Chairs, mute: true, forced: true);

			// obor penerangan di dinding atas
			for (int dx = 2; dx < width - 2; dx += 4)
			{
				int x = left + dx;
				if (GenHelpers.InBounds(x, top + 1) && !Main.tile[x, top + 1].HasTile)
					WorldGen.PlaceTile(x, top + 1, TileID.Torches, mute: true, forced: true);
			}

			FillChestWithLoot(cx, floorY - 1, rand);
		}

		// ================= TIPE 6: RERUNTUHAN PILAR TERBUKA (COLLAPSED COLONNADE) =================

		/// <summary>
		/// Area terbuka lebar dengan lantai batu bata rata dan deretan pilar yang sebagian
		/// udah patah/pendek (tinggi acak per pilar), plus puing berserakan & cobweb -
		/// kesan reruntuhan kuil/kuil kuno yang runtuh sebagian, bukan ruangan tertutup rapi.
		/// </summary>
		private static void BuildCollapsedColonnade(int cx, int cy, UnifiedRandom rand)
		{
			int width = rand.Next(30, 50);
			int floorY = cy;
			int halfWidth = width / 2;

			// jaga-jaga: cek ujung kiri & kanan area juga, biar ga numpuk ke struktur terlindungi
			if (ProtectionCheck.IsProtected(cx - halfWidth, floorY, 20) || ProtectionCheck.IsProtected(cx + halfWidth, floorY, 20))
				return;

			for (int dx = -halfWidth; dx <= halfWidth; dx++)
			{
				int x = cx + dx;
				for (int dy = -14; dy <= 0; dy++)
				{
					int y = floorY + dy;
					if (GenHelpers.InBounds(x, y))
						WorldGen.KillTile(x, y, false, false, true);
				}

				if (GenHelpers.InBounds(x, floorY))
					WorldGen.PlaceTile(x, floorY, TileID.GrayBrick, mute: true, forced: true);
			}

			// pilar-pilar patah dengan tinggi acak (sebagian utuh, sebagian cuma sisa pendek)
			int pillarSpacing = rand.Next(5, 8);
			for (int dx = -halfWidth + 2; dx <= halfWidth - 2; dx += pillarSpacing)
			{
				int x = cx + dx;
				int pillarHeight = rand.Next(2, 13); // variatif, sengaja ada yang "patah" pendek

				for (int dy = 0; dy < pillarHeight; dy++)
				{
					int y = floorY - 1 - dy;
					if (GenHelpers.InBounds(x, y))
						WorldGen.PlaceTile(x, y, TileID.GrayBrick, mute: true, forced: true);
				}

				// puing batu berserakan di kaki pilar
				if (rand.Next(100) < 60)
				{
					int rx = x + rand.Next(-1, 2);
					if (GenHelpers.InBounds(rx, floorY - 1) && !Main.tile[rx, floorY - 1].HasTile)
						WorldGen.PlaceTile(rx, floorY - 1, TileID.GrayBrick, mute: true, forced: true);
				}
			}

			// cobweb & sisa reruntuhan menyebar sebagai detail "udah lama ditinggalkan"
			for (int dx = -halfWidth; dx <= halfWidth; dx++)
			{
				int x = cx + dx;
				if (rand.Next(100) < 4 && GenHelpers.InBounds(x, floorY - 4) && !Main.tile[x, floorY - 4].HasTile)
					WorldGen.PlaceTile(x, floorY - 4, TileID.Cobweb, mute: true, forced: true);
			}

			FillChestWithLoot(cx, floorY - 1, rand);
		}

		// ================= LOOT =================

		/// <summary>
		/// Taruh chest berisi loot dasar (koin, torch, potion) di titik target.
		/// Style chest random supaya visualnya bervariasi (kayu, emas, dll).
		/// </summary>
		private static void FillChestWithLoot(int x, int y, UnifiedRandom rand)
		{
			int chestIndex = WorldGen.PlaceChest(x, y, (ushort)TileID.Containers, true, rand.Next(0, 18));
			if (chestIndex == -1)
				return;

			Chest chest = Main.chest[chestIndex];
			chest.item[0].SetDefaults(ItemID.GoldCoin);
			chest.item[0].stack = rand.Next(5, 30);

			chest.item[1].SetDefaults(ItemID.Torch);
			chest.item[1].stack = rand.Next(10, 30);

			if (rand.Next(100) < 40)
			{
				chest.item[2].SetDefaults(ItemID.LesserHealingPotion);
				chest.item[2].stack = rand.Next(1, 4);
			}

			if (rand.Next(100) < 25)
			{
				chest.item[3].SetDefaults(ItemID.SilverCoin);
				chest.item[3].stack = rand.Next(10, 99);
			}
		}
	}
}
