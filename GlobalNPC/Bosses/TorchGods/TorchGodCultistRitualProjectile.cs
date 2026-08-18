using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.TorchGods.Projectiles
{
	/// <summary>
	/// Visual "Cultist Ritual" (ProjectileID.CultistRitual, PURE VANILLA,
	/// gak ada asset custom) yang dipasang persis di titik TorchGod berdiri,
	/// buat pattern TorchGodCultistRainPattern.
	///
	/// Fase (semua dikontrol lewat Age, dihitung manual sendiri di sini -
	/// BUKAN pakai Projectile.timeLeft buat itungan fase, itu cuma dipakai
	/// sebagai jaga-jaga auto-Kill):
	///   1. Growing   (GrowTicks tick pertama)   - scale 0 -> 1 ("timbul").
	///   2. Holding   (HoldDurationTicks, di-set dari pattern class lewat
	///      ai[0] pas spawn) - scale tetap 1, ini fase "hujan api" berlangsung.
	///   3. Shrinking (ShrinkTicks tick terakhir) - scale 1 -> 0, abis itu Kill().
	///
	/// SELAMA scale > 0 (fase Growing/Holding/Shrinking), proyektil ini nulis
	/// posisi & radius efektifnya (BaseRadius * scale) ke
	/// TorchGodCultistRitualState - itu yang dibaca TorchGodFireballProjectile
	/// buat nentuin apakah fireball hujan yang lagi jatuh harus "kepadamkan"
	/// begitu masuk area ini (zona aman).
	/// </summary>
	public class TorchGodCultistRitualProjectile : ModProjectile
	{
		private const int GrowTicks = 60;   // 1 detik "timbul"
		private const int ShrinkTicks = 60; // 1 detik "mengecil" pas mau ilang

		private const float BaseRadius = 160f; // radius zona aman pas scale = 1 (penuh)

		// ai[0] = HoldDurationTicks (di-set dari TorchGodCultistRainPattern pas spawn).
		// ai[1] = umur proyektil dalam tick, dihitung manual di AI().
		private int HoldDurationTicks => (int)Projectile.ai[0];
		private int Age => (int)Projectile.ai[1];

		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CultistRitual;

		public override void SetDefaults()
		{
			Projectile.width = 64;
			Projectile.height = 64;

			Projectile.hostile = false;
			Projectile.friendly = false; // murni visual + penanda zona aman, gak nge-damage siapa pun

			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;
		}

		public override void AI()
		{
			Projectile.ai[1] += 1f;

			// timeLeft di-REFRESH manual tiap tick (bukan di-set sekali angka
			// pasti di SetDefaults) - soalnya HoldDurationTicks baru KETAHUAN
			// abis ai[0] ke-set dari pattern class, dan itu kejadian SETELAH
			// SetDefaults() jalan. Trik umum: selalu kecil & di-refresh tiap
			// tick, proyektilnya gak akan pernah timeout duluan selama AI()
			// masih jalan normal.
			Projectile.timeLeft = 2;

			float scale = CalculateScale();
			Projectile.scale = scale;

			// Pelan-pelan muter, biar keliatan "hidup" bukan gambar statis.
			Projectile.rotation += 0.01f;

			bool isVisible = scale > 0.02f;
			TorchGodCultistRitualState.IsActive = isVisible;
			TorchGodCultistRitualState.Center = Projectile.Center;
			TorchGodCultistRitualState.Radius = BaseRadius * scale;

			if (Age >= GrowTicks + HoldDurationTicks + ShrinkTicks)
			{
				TorchGodCultistRitualState.IsActive = false;
				Projectile.Kill();
			}
		}

		public override void OnKill(int timeLeft)
		{
			// Jaga-jaga tambahan - begitu proyektil ini beneran mati (lewat
			// jalur mana pun), pastiin zona aman ke-nonaktifin, jangan sampai
			// nyangkut IsActive = true padahal ritualnya udah gak ada.
			TorchGodCultistRitualState.IsActive = false;
		}

		private float CalculateScale()
		{
			if (Age < GrowTicks)
				return MathHelper.Clamp(Age / (float)GrowTicks, 0f, 1f);

			int holdEnd = GrowTicks + HoldDurationTicks;
			if (Age < holdEnd)
				return 1f;

			int shrinkProgressTicks = Age - holdEnd;
			return 1f - MathHelper.Clamp(shrinkProgressTicks / (float)ShrinkTicks, 0f, 1f);
		}
	}
}
