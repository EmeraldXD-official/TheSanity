using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.TorchGods.Projectiles;

namespace TheSanity.GlobalNPC.Bosses.TorchGods.Patterns
{
	/// <summary>
	/// Pattern "Cultist Ritual rain": TorchGod munculin lingkaran
	/// ProjectileID.CultistRitual (PURE VANILLA) persis di titik dia berdiri
	/// (tengah arena), terus HUJAN fireball (pakai TorchGodFireballProjectile
	/// yang sama kayak pattern spiral) jatuh dari atas ke seluruh arena.
	///
	/// CARA DODGE: berdiri DI DALAM lingkaran ritual itu (bareng TorchGod) -
	/// fireball hujan yang masuk ke radius ritual otomatis "kepadamkan"
	/// (Projectile.Kill() - lihat TorchGodFireballProjectile.AI() & cek
	/// TorchGodCultistRitualState), jadi area itu jadi satu-satunya tempat aman.
	///
	/// Urutan:
	///   1. Telegraph (ritual "timbul", GrowTicks/1 detik) - BELUM ada hujan.
	///   2. Raining   (ritual nahan penuh selama HoldDurationTicks yang
	///      di-random 4-5 detik) - hujan fireball turun terus tiap
	///      RainIntervalTicks.
	///   3. Ritual "mengecil" sendiri (ditangani proyektilnya sendiri,
	///      ShrinkTicks) - hujan udah berhenti di fase ini.
	///   4. Done begitu proyektil ritualnya beneran abis semua fase-nya.
	///
	/// Cara pakai (dari ModNPC pemilik boss):
	///   private readonly TorchGodCultistRainPattern cultistRain = new();
	///
	///   // trigger sekali:
	///   cultistRain.Activate(NPC);
	///
	///   PostAI() => bool justFinished = cultistRain.Update(NPC);
	/// </summary>
	public class TorchGodCultistRainPattern
	{
		// HARUS disamain sama GrowTicks/ShrinkTicks di
		// TorchGodCultistRitualProjectile, biar pattern ini tau persis kapan
		// fase-fase itu terjadi (buat nentuin kapan mulai/berhenti nge-rain).
		private const int GrowTicks = 60;   // 1 detik telegraph, BELUM ada hujan
		private const int ShrinkTicks = 60; // 1 detik ritual mengecil, hujan udah berhenti

		private const int MinHoldDurationTicks = 240;             // 4 detik hujan
		private const int MaxHoldDurationTicksInclusive = 300;    // 5 detik hujan

		private const int RainIntervalTicks = 10;   // 1 batch hujan tiap ~0.17 detik
		private const int RainDropsPerBatch = 3;
		private const float RainFallSpeed = 11f;
		private const int RainDamage = 25;

		// Area hujan (persegi) di sekitar titik tengah boss - kira-kira
		// disamain sama ArenaHalfSize di TorchGodArenaGlobalNPC (800px) biar
		// nutup seluruh arena, dikasih dikit buffer lebih kecil biar gak
		// mepet banget ke tembok border.
		private const float RainAreaHalfWidth = 750f;
		private const float RainSpawnHeightAboveCenter = 900f; // titik Y spawn hujan, di atas arena

		private int timer;
		private int holdDurationTicks;
		private Vector2 centerAnchor;

		public bool IsActive { get; private set; }

		/// <summary>
		/// Mulai attack ini: kunci titik tengah SAAT INI JUGA (TorchGod diem
		/// di tengah arena, jadi ini otomatis = tengah arena), random-in
		/// durasi hujan (4-5 detik), terus spawn proyektil ritual-nya.
		/// </summary>
		public void Activate(NPC npc)
		{
			IsActive = true;
			timer = 0;
			centerAnchor = npc.Center;
			holdDurationTicks = Main.rand.Next(MinHoldDurationTicks, MaxHoldDurationTicksInclusive + 1);

			if (Main.netMode != NetmodeID.MultiplayerClient)
			{
				Projectile.NewProjectile(
					npc.GetSource_FromAI(),
					npc.Center,
					Vector2.Zero,
					ModContent.ProjectileType<TorchGodCultistRitualProjectile>(),
					0,
					0f,
					Main.myPlayer,
					holdDurationTicks, // -> ai[0]
					0f                  // -> ai[1] (umur, mulai dari 0)
				);
			}
		}

		/// <summary>
		/// Panggil tiap tick (biasanya dari PostAI). Return TRUE persis di
		/// tick pas durasi total pattern ini abis (ritual udah timbul, hujan,
		/// dan mengecil semua) - dipakai caller buat trigger pattern berikutnya.
		/// </summary>
		public bool Update(NPC npc)
		{
			if (!IsActive)
				return false;

			timer++;

			// Hujan CUMA turun selama fase Holding si ritual (abis Grow,
			// sebelum mulai Shrink) - lihat urutan di komentar class di atas.
			bool isRainingPhase = timer > GrowTicks && timer <= GrowTicks + holdDurationTicks;
			if (isRainingPhase && timer % RainIntervalTicks == 0)
			{
				SpawnRainBatch(npc);
			}

			int totalDuration = GrowTicks + holdDurationTicks + ShrinkTicks + 10; // +buffer, samain sama proyektil ritual
			if (timer >= totalDuration)
			{
				IsActive = false;
				return true;
			}

			return false;
		}

		private void SpawnRainBatch(NPC npc)
		{
			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			for (int i = 0; i < RainDropsPerBatch; i++)
			{
				float offsetX = Main.rand.NextFloat(-RainAreaHalfWidth, RainAreaHalfWidth);
				Vector2 spawnPos = centerAnchor + new Vector2(offsetX, -RainSpawnHeightAboveCenter);

				Projectile.NewProjectile(
					npc.GetSource_FromAI(),
					spawnPos,
					Vector2.UnitY * RainFallSpeed,
					ModContent.ProjectileType<TorchGodFireballProjectile>(),
					RainDamage,
					2f,
					Main.myPlayer
				);
			}
		}
	}
}
