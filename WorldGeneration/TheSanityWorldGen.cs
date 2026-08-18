using System.Collections.Generic;
using Terraria;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.Utilities;
using Terraria.WorldBuilding;

namespace TheSanity.WorldGeneration
{
	/// <summary>
	/// Entry point world generation TheSanity.
	/// Nyisipin 1 GenPass tambahan ke antrian world gen vanilla, dijalankan SETELAH
	/// terrain, biome, dan micro-biome vanilla selesai dibuat -- jadi kita cuma
	/// "mempercantik" hasil generate vanilla, bukan gantiin dari nol (lebih aman & kompatibel).
	/// </summary>
	public class TheSanityWorldGen : ModSystem
	{
		public override void ModifyWorldGenTasks(List<GenPass> tasks, ref double totalWeight)
		{
			// "Micro Biomes" adalah pass vanilla yang generate Queen Bee hive, Granite, Marble, dll.
			// Kita taruh pass kita PERSIS setelah itu supaya semua biome dasar udah jadi duluan.
			int index = tasks.FindIndex(g => g.Name.Equals("Micro Biomes"));
			if (index == -1)
				index = tasks.Count - 2; // fallback: taruh mendekati akhir kalau nama pass berubah di update Terraria

			tasks.Insert(index + 1, new TheSanityBiomePass("TheSanity: Enhanced Biomes", 200f));
		}
	}

	public class TheSanityBiomePass : GenPass
	{
		public TheSanityBiomePass(string name, float loadWeight) : base(name, loadWeight) { }

		protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
		{
			UnifiedRandom rand = WorldGen.genRand;

			progress.Message = "Menghiasi Forest...";
			ForestPass.Generate(rand);
			progress.Set(0.12f);

			progress.Message = "Membentuk pegunungan Snow...";
			SnowPass.Generate(rand);
			progress.Set(0.28f);

			progress.Message = "Membentuk rawa & jurang Jungle...";
			JunglePass.Generate(rand);
			progress.Set(0.42f);

			progress.Message = "Membentuk pilar & bukit Desert...";
			DesertPass.Generate(rand);
			progress.Set(0.55f);

			progress.Message = "Menghiasi Crimson...";
			EvilBiomePass.GenerateCrimson(rand);
			progress.Set(0.65f);

			progress.Message = "Menghiasi Corruption...";
			EvilBiomePass.GenerateCorruption(rand);
			progress.Set(0.75f);

			progress.Message = "Menaburkan awan di langit...";
			SkyPass.Generate(rand);
			progress.Set(0.78f);

			progress.Message = "Memperbesar mini biome...";
			MiniBiomePass.EnhanceQueenBee(rand);
			MiniBiomePass.EnhanceGranite(rand);
			MiniBiomePass.EnhanceMarble(rand);
			MiniBiomePass.EnhanceHell(rand);
			progress.Set(0.87f);

			progress.Message = "Merevamp kolam Shimmer...";
			ShimmerPass.Generate(rand);
			progress.Set(0.92f);

			progress.Message = "Membangun reruntuhan bawah tanah...";
			UndergroundStructurePass.Generate(rand);
			progress.Set(1f);
		}
	}
}
