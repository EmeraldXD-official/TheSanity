using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using System;

namespace TheSanity.GlobalNPC.Bosses.TorchGods.Projectiles
{
	/// <summary>
	/// Laser "predictive sweep" - REWRITE total render/collision-nya biar konsepnya
	/// nyontek 2 pattern Twins:
	///
	///  - Fase TELEGRAPH: gaya "Predik" RetBeam (TwinsRework.DrawAimLine) - garis
	///    TIPIS 2 lapis (core + outline) pakai MagicPixel polos (BUKAN sprite laser
	///    apapun), berkedip & makin terang mendekati saat beam-nya lepas.
	///  - Fase BEAM: gaya "Last Stand" Deathray (TwinsRework.DrawDeathrayCone) -
	///    beam berbentuk CORONG/V (tipis di deket badan boss, MELEBAR di ujung
	///    jauh), 2 lapis (glow lebar+redup di luar, core sempit+terang di dalam).
	///    Hitbox damage-nya juga ngikutin bentuk corong itu (lerp ketebalan
	///    berdasarkan posisi sepanjang garis), bukan ketebalan rata.
	///  - Kecepatan muternya EXPONENTIAL (cubic ease-in, t^3): lambat banget di
	///    awal, terus makin cepat mendekati akhir, sampai nyampe setengah
	///    lingkaran (180 derajat) dari sudut prediksi - pola yang sama kayak
	///    ramp-in Deathray pas ganti arah putaran di Last Stand.
	///
	/// SEMUA visual di sini pure MagicPixel (vanilla built-in texture polos) -
	/// gak butuh sprite/asset custom sama sekali, sama kayak cara Twins gambar
	/// aim line & Deathray-nya.
	/// </summary>
	public class TorchGodPredictiveLaserProjectile : ModProjectile
	{
		private const int PredictTicks = 60;   // 1 detik telegraph, GAK damage
		private const int SweepTicks = 60;     // durasi muter 180 derajat pas jadi beam beneran
		private const float SweepAngleRadians = MathHelper.Pi; // setengah lingkaran = 180 derajat

		// -1 = muter berlawanan arah jarum jam, 1 = searah jarum jam (dari
		// sudut prediksi awal). Ganti tanda ini kalau mau arah muternya kebalik.
		private const float SweepDirection = -1f;

		private const float LaserLength = 2000f; // sepanjang ini dari titik spawn

		// ---- Gaya "Predik" (telegraph) - nyontek persis DrawAimLine Twins ----
		private const int TelegraphCoreThickness = 8;
		private const int TelegraphOutlineThickness = 16;
		private static readonly Color TelegraphColor = new Color(255, 200, 60); // kuning-oranye, beda dari merah Twins biar gak "nyontek warna" juga

		// ---- Gaya "Last Stand" (beam corong/V) - nyontek persis DrawDeathrayCone Twins ----
		private const float ConeStartThickness = 12f;   // core, deket badan boss
		private const float ConeEndThickness = 100f;    // core, di ujung jauh
		private const float GlowStartThickness = 20f;   // glow luar, deket badan boss
		private const float GlowEndThickness = 160f;    // glow luar, di ujung jauh
		private static readonly Color ConeGlowColor = new Color(255, 140, 30);
		private static readonly Color ConeCoreColor = new Color(255, 210, 60);
		private const float GlowAlpha = 0.45f;
		private const int ConeSegments = 24;

		// ai[0] = sudut prediksi yang DIKUNCI pas spawn (radian).
		// ai[1] = umur proyektil dalam tick, dihitung manual di AI() (BUKAN pakai
		//         Projectile.timeLeft, itu cuma buat nentuin kapan di-Kill()).
		private float LockedAngle => Projectile.ai[0];
		private int Age => (int)Projectile.ai[1];

		private bool IsBeamPhase => Age >= PredictTicks;

		// Sudut AKTUAL laser tick ini. Fase beam: EXPONENTIAL (cubic ease-in) -
		// lambat di awal, cepat mendekati akhir, sampai nyampe SweepAngleRadians.
		private float CurrentAngle
		{
			get
			{
				if (!IsBeamPhase)
					return LockedAngle;

				float sweepT = MathHelper.Clamp((Age - PredictTicks) / (float)SweepTicks, 0f, 1f);
				float eased = sweepT * sweepT * sweepT; // cubic ease-in

				return LockedAngle + (SweepAngleRadians * SweepDirection * eased);
			}
		}

		// Texture placeholder - GAK PERNAH beneran ke-draw (PreDraw return false &
		// semuanya digambar manual pakai MagicPixel), tapi ModProjectile tetap
		// butuh path texture yang valid buat di-load.
		public override string Texture => "Terraria/Images/Projectile_1";

		public override void SetDefaults()
		{
			Projectile.width = 8;
			Projectile.height = 8;

			Projectile.hostile = true;
			Projectile.friendly = false;

			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.penetrate = -1;

			// Dikit buffer di atas total durasi telegraph+sweep.
			Projectile.timeLeft = PredictTicks + SweepTicks + 10;
		}

		public override void AI()
		{
			Projectile.ai[1] += 1f;
			Projectile.rotation = CurrentAngle;

			Color glowColor = IsBeamPhase ? new Color(1f, 0.5f, 0.1f) : new Color(0.6f, 0.5f, 0.2f);
			Lighting.AddLight(Projectile.Center, glowColor.ToVector3() * 0.8f);
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			// Fase telegraph: cuma visual, GAK ada damage sama sekali.
			if (!IsBeamPhase)
				return false;

			Vector2 start = Projectile.Center;
			Vector2 direction = CurrentAngle.ToRotationVector2();
			Vector2 end = start + direction * LaserLength;

			Vector2 targetCenter = targetHitbox.Center.ToVector2();
			float dist = DistancePointToSegment(targetCenter, start, end, out float t);

			// Ketebalan hit-nya ngikutin bentuk corong/V (lerp dari deket badan ke
			// ujung jauh) - PERSIS sama filosofi TickDeathrayDamageAndBolts Twins,
			// biar hitbox-nya kongruen sama yang keliatan (yang deket badan lebih
			// susah kena, yang jauh lebih gampang kena karena lebih lebar).
			float hitThicknessAtT = MathHelper.Lerp(ConeStartThickness, ConeEndThickness, t) * 0.5f;

			return dist <= hitThicknessAtT;
		}

		private static float DistancePointToSegment(Vector2 point, Vector2 segStart, Vector2 segEnd, out float t)
		{
			Vector2 seg = segEnd - segStart;
			float lengthSquared = seg.LengthSquared();
			if (lengthSquared <= 0.0001f)
			{
				t = 0f;
				return Vector2.Distance(point, segStart);
			}

			t = MathHelper.Clamp(Vector2.Dot(point - segStart, seg) / lengthSquared, 0f, 1f);
			Vector2 closest = segStart + seg * t;
			return Vector2.Distance(point, closest);
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Vector2 screenPos = Main.screenPosition;
			Vector2 direction = CurrentAngle.ToRotationVector2();

			if (!IsBeamPhase)
				DrawTelegraphLine(direction, screenPos);
			else
				DrawBeamCone(direction, screenPos);

			return false;
		}

		// ---- Gaya "Predik" - garis tipis 2 lapis (core+outline), berkedip & makin
		// terang mendekati saat beam-nya lepas. Nyontek persis DrawAimLine Twins. ----
		private void DrawTelegraphLine(Vector2 direction, Vector2 screenPos)
		{
			float progress = MathHelper.Clamp(Age / (float)PredictTicks, 0f, 1f);
			float flicker = 0.75f + 0.25f * MathF.Sin(Main.GameUpdateCount * 0.9f);
			float alpha = MathHelper.Lerp(0.35f, 1f, progress) * flicker;

			Vector2 start = Projectile.Center - screenPos;
			float rotation = direction.ToRotation();

			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Rectangle sourceRect = new Rectangle(0, 0, pixel.Width, pixel.Height);
			Vector2 origin = new Vector2(0f, pixel.Height * 0.5f);

			// Outline (lebih transparan, lebih tebal).
			Color outlineColor = TelegraphColor * (alpha * 0.35f);
			Rectangle outlineDest = new Rectangle((int)start.X, (int)start.Y, (int)LaserLength, TelegraphOutlineThickness);
			Main.spriteBatch.Draw(pixel, outlineDest, sourceRect, outlineColor, rotation, origin, SpriteEffects.None, 0f);

			// Core (solid, lebih tipis, di atas outline).
			Color coreColor = TelegraphColor * alpha;
			Rectangle coreDest = new Rectangle((int)start.X, (int)start.Y, (int)LaserLength, TelegraphCoreThickness);
			Main.spriteBatch.Draw(pixel, coreDest, sourceRect, coreColor, rotation, origin, SpriteEffects.None, 0f);
		}

		// ---- Gaya "Last Stand" - beam corong/V, 2 lapis (glow lebar+redup di luar,
		// core sempit+terang di dalam), digambar per-segmen biar ketebalannya bisa
		// nge-lerp dari startThickness ke endThickness. Nyontek persis
		// DrawDeathrayCone Twins. ----
		private void DrawBeamCone(Vector2 direction, Vector2 screenPos)
		{
			DrawCone(direction, screenPos, GlowStartThickness, GlowEndThickness, ConeGlowColor, GlowAlpha);
			DrawCone(direction, screenPos, ConeStartThickness, ConeEndThickness, ConeCoreColor, 1f);
		}

		private void DrawCone(Vector2 direction, Vector2 screenPos, float startThickness, float endThickness, Color color, float alpha)
		{
			Texture2D pixel = TextureAssets.MagicPixel.Value;
			Rectangle sourceRect = new Rectangle(0, 0, pixel.Width, pixel.Height);
			Vector2 origin = new Vector2(0f, pixel.Height * 0.5f);
			float rotation = direction.ToRotation();
			float segLength = LaserLength / ConeSegments;

			for (int i = 0; i < ConeSegments; i++)
			{
				float tMid = (i + 0.5f) / ConeSegments;
				float thickness = MathHelper.Lerp(startThickness, endThickness, tMid);

				Vector2 segWorldStart = Projectile.Center + direction * (segLength * i);
				Vector2 segScreenStart = segWorldStart - screenPos;

				// Sedikit overlap (+2px panjang) antar segmen biar gak ada celah tipis.
				Rectangle segDest = new Rectangle((int)segScreenStart.X, (int)segScreenStart.Y, (int)segLength + 2, (int)thickness);
				Main.spriteBatch.Draw(pixel, segDest, sourceRect, color * alpha, rotation, origin, SpriteEffects.None, 0f);
			}
		}
	}
}
