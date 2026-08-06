using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.TorchGods.Projectiles;

namespace TheSanity.GlobalNPC.Bosses.TorchGods.Patterns
{
	/// <summary>
	/// Pattern "predictive laser sweep": begitu di-Activate(), langsung
	/// nge-spawn SATU TorchGodPredictiveLaserProjectile yang ngunci arah
	/// prediksi ke posisi player TERDEKAT saat itu (SEKALI doang - gak
	/// ngikutin player lagi abis itu, biar fair buat di-telegraph).
	///
	/// Sisa logic (telegraph 1 detik -> jadi beam -> muter 180 derajat)
	/// semuanya ditangani DI DALAM proyektilnya sendiri (lihat
	/// TorchGodPredictiveLaserProjectile) - class ini cuma nentuin KAPAN &
	/// KE ARAH MANA proyektilnya di-spawn, dan ngasih tau caller kapan
	/// durasi pattern ini abis (buat lanjut ke pattern berikutnya).
	///
	/// Cara pakai (dari ModNPC pemilik boss):
	///   private readonly TorchGodLaserSweepPattern laserSweep = new();
	///
	///   // trigger sekali:
	///   laserSweep.Activate(NPC);
	///
	///   PostAI() => bool justFinished = laserSweep.Update();
	/// </summary>
	public class TorchGodLaserSweepPattern
	{
		// HARUS disamain sama PredictTicks + SweepTicks + buffer di
		// TorchGodPredictiveLaserProjectile, biar pattern ini gak nganggep
		// "udah kelar" padahal proyektilnya masih aktif (atau sebaliknya).
		private const int PredictTicks = 60;
		private const int SweepTicks = 60;
		private const int TotalDurationTicks = PredictTicks + SweepTicks + 10;

		private const int LaserDamage = 45;

		private int timer;

		public bool IsActive { get; private set; }

		/// <summary>
		/// Mulai attack ini: kunci arah prediksi ke player terdekat SAAT INI
		/// JUGA, terus spawn proyektil laser-nya.
		/// </summary>
		public void Activate(NPC npc)
		{
			IsActive = true;
			timer = 0;

			Player target = Main.player[Player.FindClosest(npc.Center, 1, 1)];
			float predictAngle = (target != null && target.active)
				? (target.Center - npc.Center).ToRotation()
				: 0f;

			// Cuma server/singleplayer yang boleh spawn projectile baru.
			if (Main.netMode != NetmodeID.MultiplayerClient)
			{
				Projectile.NewProjectile(
					npc.GetSource_FromAI(),
					npc.Center,
					Vector2.Zero,
					ModContent.ProjectileType<TorchGodPredictiveLaserProjectile>(),
					LaserDamage,
					0f,
					Main.myPlayer,
					predictAngle, // -> Projectile.ai[0] (sudut prediksi yang dikunci)
					0f            // -> Projectile.ai[1] (umur proyektil, mulai dari 0)
				);
			}
		}

		/// <summary>
		/// Panggil tiap tick (biasanya dari PostAI). Return TRUE persis di
		/// tick pas durasi pattern ini abis - dipakai caller buat trigger
		/// pattern berikutnya.
		/// </summary>
		public bool Update()
		{
			if (!IsActive)
				return false;

			timer++;

			if (timer >= TotalDurationTicks)
			{
				IsActive = false;
				return true;
			}

			return false;
		}
	}
}
