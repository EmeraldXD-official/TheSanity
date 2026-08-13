using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TheSanity.Items.Acc.ShadowHeart
{
	public class ShadowHeartPlayer : ModPlayer
	{
		public bool hasShadowHeart;
		public bool showHitbox;

		public override void ResetEffects()
		{
			hasShadowHeart = false;
		}

		// Hitbox mengecil menjadi 10x10 presisi di titik tengah Player.Center
		public Rectangle GetReducedHitbox()
		{
			int width = 15;
			int height = 15; // Mengubah tinggi menjadi 10 pixel
			Vector2 center = Player.Center;
			return new Rectangle((int)center.X - width / 2, (int)center.Y - height / 2, width, height);
		}

		// Filter Serangan Proyektil Musuh
		public override bool CanBeHitByProjectile(Projectile proj)
		{
			if (hasShadowHeart)
			{
				if (!proj.Hitbox.Intersects(GetReducedHitbox()))
				{
					return false;
				}
			}
			return true;
		}

		// Filter Serangan Kontak NPC / Musuh
		public override bool CanBeHitByNPC(NPC npc, ref int cooldownSlot)
		{
			if (hasShadowHeart)
			{
				if (!npc.Hitbox.Intersects(GetReducedHitbox()))
				{
					return false;
				}
			}
			return true;
		}
	}

	// =========================================================
	// LAYER VISUAL HITBOX INDICATOR (AUTOMATICALLY RESIZED)
	// =========================================================
	public class ShadowHeartHitboxLayer : PlayerDrawLayer
	{
		public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Head);

		protected override void Draw(ref PlayerDrawSet drawInfo)
		{
			if (drawInfo.shadow != 0f) return;

			Player drawPlayer = drawInfo.drawPlayer;
			ShadowHeartPlayer modPlayer = drawPlayer.GetModPlayer<ShadowHeartPlayer>();

			if (!modPlayer.hasShadowHeart || !modPlayer.showHitbox)
				return;

			Texture2D pixel = TextureAssets.MagicPixel.Value;

			// Mengambil ukuran 10x10 secara otomatis
			Rectangle reducedHitbox = modPlayer.GetReducedHitbox();

			int x = (int)(reducedHitbox.X - Main.screenPosition.X);
			int y = (int)(reducedHitbox.Y - Main.screenPosition.Y);
			int w = reducedHitbox.Width;
			int h = reducedHitbox.Height;

			Color outlineColor = new Color(180, 80, 255, 220); // Warna Ungu Glow
			Color fillColor = new Color(140, 40, 220, 90);    // Isian Transparan

			// 1. Isian Hitbox
			drawInfo.DrawDataCache.Add(new DrawData(
				pixel,
				new Rectangle(x, y, w, h),
				fillColor
			));

			// 2. Garis Batas Outline
			drawInfo.DrawDataCache.Add(new DrawData(pixel, new Rectangle(x, y, w, 1), outlineColor));
			drawInfo.DrawDataCache.Add(new DrawData(pixel, new Rectangle(x, y + h - 1, w, 1), outlineColor));
			drawInfo.DrawDataCache.Add(new DrawData(pixel, new Rectangle(x, y, 1, h), outlineColor));
			drawInfo.DrawDataCache.Add(new DrawData(pixel, new Rectangle(x + w - 1, y, 1, h), outlineColor));

			// 3. Titik Pusat Core Hitbox (2x2 Pixel White Dot)
			int dotSize = 2;
			Vector2 centerScreen = drawPlayer.Center - Main.screenPosition;
			drawInfo.DrawDataCache.Add(new DrawData(
				pixel,
				new Rectangle((int)centerScreen.X - dotSize / 2, (int)centerScreen.Y - dotSize / 2, dotSize, dotSize),
				Color.White
			));
		}
	}
}