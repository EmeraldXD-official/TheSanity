using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Acc.HaloOfSanctity
{
	// =======================================================
	// 1. MOD ITEM
	// =======================================================
	public class HaloOfSanctity : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 28;
			Item.height = 14;
			Item.accessory = true;
			Item.value = Item.sellPrice(0, 5, 0, 0);
			Item.rare = ItemRarityID.LightRed;
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			// --- STAT EFFECTS ---
			player.lifeRegen += 4;
			
			// Applies the Featherfall buff dynamically while equipped
			player.AddBuff(BuffID.Featherfall, 2);
			
			player.noFallDmg = true;
			player.buffImmune[BuffID.Bleeding] = true;
			player.buffImmune[BuffID.Confused] = true;

			// --- GOLDEN LIGHT AROUND PLAYER ---
			Vector2 haloPos = player.Center;
			haloPos.Y -= 28f + player.headPosition.Y;
			Lighting.AddLight(haloPos, 0.9f, 0.8f, 0.4f);

			if (!hideVisual)
			{
				player.GetModPlayer<HaloOfSanctityPlayer>().showHalo = true;

				// --- GOLDEN DUST SPARKLE PARTICLES ---
				if (Main.rand.NextBool(4))
				{
					Dust dust = Dust.NewDustPerfect(
						haloPos + new Vector2(Main.rand.NextFloat(-12f, 12f), Main.rand.NextFloat(-4f, 4f)),
						DustID.GoldFlame,
						new Vector2(0, Main.rand.NextFloat(0.2f, 0.6f)),
						100,
						default,
						0.8f
					);
					dust.noGravity = true;
				}
			}
		}

		public override void UpdateVanity(Player player)
		{
			player.GetModPlayer<HaloOfSanctityPlayer>().showHalo = true;

			Vector2 haloPos = player.Center;
			haloPos.Y -= 28f + player.headPosition.Y;
			Lighting.AddLight(haloPos, 0.9f, 0.8f, 0.4f);
		}

		// --- TOOLTIPS VIA CODE & PULSING COLOR ---
		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "FlavorText", "'A holy aura radiating heavenly warmth'"));
			tooltips.Add(new TooltipLine(Mod, "Stat1", "Significantly increases life regeneration"));
			tooltips.Add(new TooltipLine(Mod, "Stat2", "Grants the Featherfall buff and fall damage immunity"));
			tooltips.Add(new TooltipLine(Mod, "Stat3", "Grants immunity to Bleeding and Confused"));

			foreach (var line in tooltips)
			{
				float lerpVal = (float)(Math.Sin(Main.GlobalTimeWrappedHourly * 6f) + 1f) / 2f;
				line.OverrideColor = Color.Lerp(Color.Gold, Color.Yellow, lerpVal);
			}
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.AngelHalo, 1)
				.AddIngredient(ItemID.Feather, 5)
				.AddIngredient(ItemID.AdhesiveBandage, 1)
				.AddIngredient(ItemID.Nazar, 1)
				.AddTile(TileID.TinkerersWorkbench)
				.Register();
		}
	}

	// =======================================================
	// 2. MOD PLAYER
	// =======================================================
	public class HaloOfSanctityPlayer : ModPlayer
	{
		public bool showHalo;

		public override void ResetEffects()
		{
			showHalo = false;
		}
	}

	// =======================================================
	// 3. DRAW LAYER (PULSATING SCALE & GLOW EFFECT)
	// =======================================================
	public class SingleHaloDrawLayer : PlayerDrawLayer
	{
		public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.Head);

		public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
		{
			return drawInfo.drawPlayer.active 
				&& !drawInfo.drawPlayer.dead 
				&& !drawInfo.drawPlayer.invis 
				&& drawInfo.drawPlayer.GetModPlayer<HaloOfSanctityPlayer>().showHalo;
		}

		protected override void Draw(ref PlayerDrawSet drawInfo)
		{
			Player drawPlayer = drawInfo.drawPlayer;

			string texturePath = "TheSanity/Items/Acc/HaloOfSanctity/HaloOfSanctity_Draw";
			if (!ModContent.HasAsset(texturePath)) return;

			Texture2D texture = ModContent.Request<Texture2D>(texturePath).Value;

			Vector2 position = drawPlayer.Center - Main.screenPosition;
			position.Y -= 28f; 
			position.Y += drawPlayer.headPosition.Y; 

			Vector2 drawPos = new Vector2((int)position.X, (int)position.Y);
			Vector2 origin = new Vector2(texture.Width / 2f, texture.Height / 2f);

			// --- PULSATING SCALE EFFECT (GROWS AND SHRINKS SMOOTHLY) ---
			float scalePulse = (float)(Math.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.1f) + 1.0f;

			// 1. MAIN SPRITE
			Color drawColor = Lighting.GetColor((int)(drawPlayer.Center.X / 16f), (int)(drawPlayer.Center.Y / 16f));
			drawColor = drawPlayer.GetImmuneAlphaPure(drawColor, drawInfo.shadow);

			DrawData baseDraw = new DrawData(
				texture,
				drawPos,
				null,
				drawColor,
				drawPlayer.headRotation,
				origin,
				scalePulse,
				drawInfo.playerEffect,
				0
			);
			drawInfo.DrawDataCache.Add(baseDraw);

			// 2. GLOW EFFECT
			float pulse = (float)(Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.15f) + 0.85f;
			Color glowColor = new Color(255, 230, 150, 0) * pulse;
			glowColor = drawPlayer.GetImmuneAlphaPure(glowColor, drawInfo.shadow);

			DrawData glowDraw = new DrawData(
				texture,
				drawPos,
				null,
				glowColor,
				drawPlayer.headRotation,
				origin,
				scalePulse * 1.05f,
				drawInfo.playerEffect,
				0
			);
			drawInfo.DrawDataCache.Add(glowDraw);
		}
	}
}