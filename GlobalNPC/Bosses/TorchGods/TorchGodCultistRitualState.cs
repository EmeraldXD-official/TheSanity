using Microsoft.Xna.Framework;

namespace TheSanity.GlobalNPC.Bosses.TorchGods.Projectiles
{
	/// <summary>
	/// State kecil yang di-share antara TorchGodCultistRitualProjectile (yang
	/// NULIS nilainya tiap tick selagi dia hidup/keliatan) dan
	/// TorchGodFireballProjectile (yang MEMBACA nilainya buat tau apakah
	/// dirinya harus "mati" karena masuk ke zona aman ritual - lihat pattern
	/// TorchGodCultistRainPattern buat penjelasan lengkap mekaniknya).
	///
	/// Static & simple SENGAJA - cuma ada 1 TorchGod / 1 ritual aktif dalam
	/// satu waktu di dunia ini, jadi gak perlu instance-per-boss yang ribet
	/// (pola yang sama kayak field static torchGodBorder di
	/// TorchGodArenaGlobalNPC).
	/// </summary>
	public static class TorchGodCultistRitualState
	{
		public static bool IsActive;
		public static Vector2 Center;
		public static float Radius;
	}
}
