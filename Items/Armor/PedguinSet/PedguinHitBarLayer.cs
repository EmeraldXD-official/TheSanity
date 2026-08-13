using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.PedguinSet
{
	public class PedguinHitBarLayer : PlayerDrawLayer
	{
		public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Head);

		protected override void Draw(ref PlayerDrawSet drawInfo)
		{
			// CEK SHADOW PASS: Jika sedang menggambar bayangan (afterimage player), lewati layer ini
			if (drawInfo.shadow != 0f)
				return;

			Player drawPlayer = drawInfo.drawPlayer;
			PedguinPlayer modPlayer = drawPlayer.GetModPlayer<PedguinPlayer>();

			if (!modPlayer.hasAbsoluteZeroSet || modPlayer.hitCharge <= 0)
				return;

			Vector2 pos = drawInfo.Position - Main.screenPosition + new Vector2(drawPlayer.width / 2f - 20f, -18f);
			pos = new Vector2((int)pos.X, (int)pos.Y);

			int barWidth = 40;
			int barHeight = 6;
			float progress = (float)modPlayer.hitCharge / PedguinPlayer.MaxHitCharge;
			int fillWidth = (int)(barWidth * progress);

			Texture2D pixel = TextureAssets.MagicPixel.Value;
			bool isReady = modPlayer.hitCharge >= PedguinPlayer.MaxHitCharge;

			// 1. OUTLINE BINGKAI
			Color frameColor = isReady ? new Color(100, 240, 255) * 0.9f : Color.Black * 0.85f;
			drawInfo.DrawDataCache.Add(new DrawData(
				pixel,
				new Rectangle((int)pos.X - 2, (int)pos.Y - 2, barWidth + 4, barHeight + 4),
				frameColor
			));

			// 2. BAR BACKGROUND
			drawInfo.DrawDataCache.Add(new DrawData(
				pixel,
				new Rectangle((int)pos.X, (int)pos.Y, barWidth, barHeight),
				new Color(15, 25, 40) * 0.85f
			));

			// 3. ISIAN BAR
			Color barColor;
			if (isReady)
			{
				float pulse = (float)(Math.Sin(Main.GameUpdateCount * 0.25) * 0.5 + 0.5);
				barColor = Color.Lerp(new Color(120, 255, 255), Color.White, pulse);
			}
			else
			{
				barColor = Color.Lerp(new Color(50, 150, 255), new Color(130, 245, 255), progress);
			}

			drawInfo.DrawDataCache.Add(new DrawData(
				pixel,
				new Rectangle((int)pos.X, (int)pos.Y, fillWidth, barHeight),
				barColor
			));

			// 4. SHADER GLOW OVERLAY (Efek berpijar saat Ready)
			if (isReady)
			{
				Color glowOverlay = new Color(100, 255, 255, 0) * 0.5f;
				drawInfo.DrawDataCache.Add(new DrawData(
					pixel,
					new Rectangle((int)pos.X - 4, (int)pos.Y - 4, barWidth + 8, barHeight + 8),
					glowOverlay
				));
			}

			// 5. GARIS SEKAT (DIVIDER 8 CHUNK)
			int segmentCount = PedguinPlayer.MaxHitCharge;
			float segmentStep = (float)barWidth / segmentCount;

			for (int i = 1; i < segmentCount; i++)
			{
				int dividerX = (int)(pos.X + i * segmentStep);
				drawInfo.DrawDataCache.Add(new DrawData(
					pixel,
					new Rectangle(dividerX, (int)pos.Y, 1, barHeight),
					Color.Black * 0.5f
				));
			}
		}
	}
}