using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.TorchGods.Projectiles
{
	/// <summary>
	/// Fireball custom buat TorchGod: PAKE SPRITE VANILLA Fireball apa
	/// adanya (belum ada asset custom), tapi AI/behavior full custom:
	///  - Nembus block (Projectile.tileCollide = false).
	///  - Ignore gravity (AI() di bawah SENGAJA gak ada kode gravitasi sama
	///    sekali - peluru cuma gerak lurus konstan sesuai velocity awal,
	///    beda dari lemparan vanilla Fireball asli yang kena gravitasi).
	///  - Ninggalin afterimage trail yang MAKIN MENGECIL & MAKIN TRANSPARAN
	///    makin jauh dari peluru utama, pakai Projectile.oldPos bawaan.
	/// </summary>
	public class TorchGodFireballProjectile : ModProjectile
	{
		// Berapa banyak titik afterimage yang disimpen di trail (makin
		// banyak = trail makin panjang/mulus, tapi makin berat dikit).
		private const int TrailLength = 12;

		// Scale & opacity afterimage PALING JAUH (paling lama/oldest).
		// Yang paling deket peluru utama otomatis mendekati 1 (lihat PreDraw).
		private const float FarthestTrailScale = 0.15f;
		private const float FarthestTrailOpacity = 0f;
		private const float NearestTrailOpacity = 0.85f;

		private const int ProjectileLifetimeTicks = 300; // 5 detik - biar gak nembus block SELAMANYA

		// Sprite: pure vanilla Fireball, gak ada asset custom.
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Fireball;

		public override void SetStaticDefaults()
		{
			// Wajib biar Projectile.oldPos punya cukup slot buat trail
			// sepanjang TrailLength di atas (default vanilla cuma 5 slot).
			ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailLength;
			ProjectileID.Sets.TrailingMode[Projectile.type] = 2; // interpolasi rapi buat proyektil cepat
		}

		public override void SetDefaults()
		{
			Projectile.width = 18;
			Projectile.height = 18;

			Projectile.hostile = true;
			Projectile.friendly = false;

			Projectile.tileCollide = false; // TEMBUS BLOCK
			Projectile.ignoreWater = true;
			Projectile.timeLeft = ProjectileLifetimeTicks;
			Projectile.penetrate = -1;

			// SENGAJA gak ada penyetelan gravitasi apapun di sini ataupun
			// di AI() di bawah - itu artinya IGNORE GRAVITY. Kalau ada kode
			// yang nambahin velocity.Y tiap tick baru itu "kena gravitasi";
			// di sini peluru cuma gerak lurus konstan sesuai velocity awal.
		}

		public override void AI()
		{
			// Cuma nyesuain rotasi visual biar ngikutin arah gerak - posisi
			// sebenernya udah otomatis lurus konstan dari velocity, gak
			// disentuh di sini sama sekali.
			Projectile.rotation = Projectile.velocity.ToRotation();

			Lighting.AddLight(Projectile.Center, 0.9f, 0.5f, 0.1f);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
			Vector2 origin = tex.Size() / 2f;

			// Gambar afterimage dari yang PALING JAUH/LAMA dulu (index
			// terakhir di array oldPos) ke yang PALING DEKET/BARU, biar
			// yang deket peluru utama ke-draw belakangan (di atas yang jauh).
			for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
			{
				Vector2 oldPos = Projectile.oldPos[i];
				if (oldPos == Vector2.Zero)
					continue; // slot belum ke-isi (peluru baru aja spawn)

				// progress: 0 = paling jauh/paling lama, 1 = paling deket peluru utama.
				float progress = 1f - (i / (float)(Projectile.oldPos.Length - 1));

				float scale = MathHelper.Lerp(FarthestTrailScale, 1f, progress);
				float opacity = MathHelper.Lerp(FarthestTrailOpacity, NearestTrailOpacity, progress);

				Vector2 drawPos = oldPos + Projectile.Size * 0.5f - Main.screenPosition;
				Color trailColor = Color.Lerp(Color.OrangeRed, Color.Yellow, progress) * opacity;

				Main.spriteBatch.Draw(tex, drawPos, null, trailColor, Projectile.rotation, origin,
					Projectile.scale * scale, SpriteEffects.None, 0f);
			}

			// return true -> peluru utama (sprite asli, ukuran normal) tetap
			// digambar normal di atas semua trail ini.
			return true;
		}
	}
}
