using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using Terraria.Audio;
using Terraria.GameContent;
using TheSanity.Items.OreBar.Regilia;
namespace TheSanity.Items.FastasyBlade.UltraSword
{
	// =======================================================
	// 1. KELAS ITEM UTAMA (ULTRA SWORD)
	// =======================================================
	public class UltraSword : ModItem
	{
		private const int ComboResetDelay = 120; // Reset kombo jika diam 2 detik

		private enum ComboStage
		{
			UpSlash1 = 0,    // Atas -> Bawah
			DownSlash1 = 1,  // Bawah -> Atas (Tembak Laser)
			UpSlash2 = 2,    // Atas -> Bawah
			DownSlash2 = 3,  // Bawah -> Atas (Tembak Laser)
			AutoFinisher = 4 // Panggil Pedang Turret Terbang
		}

		private ComboStage stage = ComboStage.UpSlash1;
		private int comboResetTimer = 0;

		public override void SetDefaults()
		{
			Item.damage = 78;
			Item.DamageType = DamageClass.Melee;
			Item.width = 50;
			Item.height = 50;
			Item.useTime = 18;
			Item.useAnimation = 18;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.knockBack = 4.5f;
			Item.value = Item.buyPrice(0, 15, 0, 0);
			Item.rare = ItemRarityID.Pink;
			Item.UseSound = SoundID.Item9;
			Item.autoReuse = true;

			Item.shoot = ModContent.ProjectileType<AutoShootingUltraSword>();
			Item.shootSpeed = 16f;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "UltraSwordCombo1", "Executes a 5-stage Astral Combo:"));
			tooltips.Add(new TooltipLine(Mod, "UltraSwordCombo2", "[c/FF69B4:Cosmic Slash] ➔ [c/BA55D3:Prism Beam] ➔ [c/FF69B4:Cosmic Slash] ➔ [c/BA55D3:Prism Beam] ➔ [c/FF1493:Phantom Satellite]"));
			tooltips.Add(new TooltipLine(Mod, "UltraSwordCombo3", "[c/FF1493:Finisher:] Summons a floating phantom blade that acts as a turret, rapid-firing lasers at nearby foes"));
		}

		public override bool CanUseItem(Player player)
		{
			// Mencegah penggunaan jika pedang turret terbang masih aktif
			return player.ownedProjectileCounts[ModContent.ProjectileType<AutoShootingUltraSword>()] <= 0;
		}

		public override void HoldItem(Player player)
		{
			if (stage != ComboStage.UpSlash1)
			{
				comboResetTimer++;
				if (comboResetTimer > ComboResetDelay)
				{
					ResetCombo();
				}
			}
		}

		private void ResetCombo()
		{
			stage = ComboStage.UpSlash1;
			comboResetTimer = 0;
		}

		// =======================================================
		// CUSTOM SWING (TERKUNCI DI TANGAN PLAYER)
		// =======================================================
		public override void UseStyle(Player player, Rectangle heldItemFrame)
		{
			if (player.itemAnimation > 0)
			{
				float progress = 1f - ((float)player.itemAnimation / player.itemAnimationMax);
				bool topToBottom = (stage == ComboStage.UpSlash1 || stage == ComboStage.UpSlash2);

				float startAngle = topToBottom ? -MathHelper.ToRadians(110f) : MathHelper.ToRadians(60f);
				float endAngle = topToBottom ? MathHelper.ToRadians(60f) : -MathHelper.ToRadians(110f);

				float currentAngle = MathHelper.Lerp(startAngle, endAngle, progress);
				player.itemRotation = currentAngle * player.direction;

				Vector2 handOffset;
				if (progress < 0.35f)
				{
					handOffset = topToBottom ? new Vector2(player.direction * -2f, -8f) : new Vector2(player.direction * 6f, 6f);
				}
				else if (progress < 0.70f)
				{
					handOffset = new Vector2(player.direction * 6f, -2f);
				}
				else
				{
					handOffset = topToBottom ? new Vector2(player.direction * 6f, 6f) : new Vector2(player.direction * -2f, -8f);
				}

				player.itemLocation = player.MountedCenter + handOffset;
			}
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			comboResetTimer = 0;

			switch (stage)
			{
				case ComboStage.UpSlash1:
					stage = ComboStage.DownSlash1;
					break;

				case ComboStage.DownSlash1:
					FirePrismBeams(player, source, position, velocity, damage, knockback);
					stage = ComboStage.UpSlash2;
					break;

				case ComboStage.UpSlash2:
					stage = ComboStage.DownSlash2;
					break;

				case ComboStage.DownSlash2:
					FirePrismBeams(player, source, position, velocity, damage, knockback);
					stage = ComboStage.AutoFinisher;
					break;

				case ComboStage.AutoFinisher:
				default:
					// Panggil Pedang Turret Melayang
					Projectile.NewProjectile(source, position, Vector2.Zero, ModContent.ProjectileType<AutoShootingUltraSword>(), (int)(damage * 0.9f), knockback, player.whoAmI);

					SoundEngine.PlaySound(SoundID.Item68, position);
					for (int i = 0; i < 20; i++)
					{
						Vector2 speed = Main.rand.NextVector2Circular(6f, 6f);
						Dust.NewDust(position, Item.width, Item.height, DustID.PinkFairy, speed.X, speed.Y, 0, default, 1.5f);
					}

					ResetCombo();
					break;
			}

			return false;
		}

		private void FirePrismBeams(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback)
		{
			SoundEngine.PlaySound(SoundID.Item72, position);
			const int beamCount = 3;
			const float spreadDegrees = 30f;

			for (int i = 0; i < beamCount; i++)
			{
				float t = beamCount == 1 ? 0.5f : i / (float)(beamCount - 1);
				float angle = MathHelper.ToRadians(MathHelper.Lerp(-spreadDegrees / 2f, spreadDegrees / 2f, t));
				Vector2 beamVelocity = velocity.RotatedBy(angle) * 1.1f;

				Projectile.NewProjectile(source, position, beamVelocity, ModContent.ProjectileType<CosmicLaser>(), (int)(damage * 0.6f), knockback * 0.5f, player.whoAmI);
			}
		}

		public override void AddRecipes()
		{
			CreateRecipe()
            .AddIngredient(2880, 1)
            .AddIngredient(1198, 12)
            .AddIngredient(381, 25)
            .AddIngredient<ReligiaBar>(15)
            .AddTile(TileID.MythrilAnvil)
            .Register();
		}
	}

	// =======================================================
	// 2. PROYEKTIL PEDANG TURRET TERBANG (PHANTOM SATELLITE)
	// =======================================================
	public class AutoShootingUltraSword : ModProjectile
	{
		private int shootTimer = 0;
		private const int ShootCooldown = 12; // Menembak setiap 12 frame (~5 peluru per detik)

		public override string Texture => "TheSanity/Items/FastasyBlade/UltraSword/UltraSword";

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 6;
			ProjectileID.Sets.TrailingMode[Type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 36;
			Projectile.height = 36;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 360;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
			Projectile.scale = 1.3f;
			Projectile.netImportant = true;
		}

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];

			if (!player.active || player.dead)
			{
				Projectile.Kill();
				return;
			}

			// Posisi melayang di atas bahu belakang player
			Vector2 hoverPosition = player.Center + new Vector2(-player.direction * 45f, -60f);
			
			// Movement mulus menggunakan Lerp
			Projectile.Center = Vector2.Lerp(Projectile.Center, hoverPosition, 0.12f);

			NPC target = FindClosestNPC(850f);

			if (target != null)
			{
				// Arahkan ujung pedang ke musuh (+ 45 derajat karena sprite miring)
				Vector2 directionToTarget = target.Center - Projectile.Center;
				float targetRotation = directionToTarget.ToRotation() + MathHelper.PiOver4;
				Projectile.rotation = Utils.AngleLerp(Projectile.rotation, targetRotation, 0.2f);

				// Logika Menembak
				shootTimer++;
				if (shootTimer >= ShootCooldown)
				{
					shootTimer = 0;
					FireLaserAtTarget(target);
				}
			}
			else
			{
				// Jika tidak ada musuh, mengarah santai ke depan player
				float idleRotation = (player.direction == 1 ? 0f : MathHelper.Pi) + MathHelper.PiOver4;
				Projectile.rotation = Utils.AngleLerp(Projectile.rotation, idleRotation, 0.1f);
			}

			// Efek Cahaya & Partikel Kosmik
			Lighting.AddLight(Projectile.Center, 0.9f, 0.3f, 0.8f);

			if (Main.rand.NextBool(3))
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy, 0f, 0f, 100, default, 1.1f);
				dust.noGravity = true;
				dust.velocity *= 0.3f;
			}
		}

		private void FireLaserAtTarget(NPC target)
		{
			if (Projectile.owner == Main.myPlayer)
			{
				// Titik muncul tembakan di ujung pedang
				Vector2 shootOrigin = Projectile.Center + (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 20f;
				Vector2 shootVelocity = (target.Center - shootOrigin).SafeNormalize(Vector2.UnitX) * 18f;

				Projectile.NewProjectile(
					Projectile.GetSource_FromThis(),
					shootOrigin,
					shootVelocity,
					ModContent.ProjectileType<CosmicLaser>(),
					Projectile.damage,
					Projectile.knockBack * 0.5f,
					Projectile.owner
				);

				SoundEngine.PlaySound(SoundID.Item12, Projectile.Center); // Efek suara laser
			}
		}

		private NPC FindClosestNPC(float maxDistance)
		{
			NPC closest = null;
			float closestDistance = maxDistance;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				if (npc.CanBeChasedBy(Projectile))
				{
					float distance = Vector2.Distance(Projectile.Center, npc.Center);
					if (distance < closestDistance)
					{
						closestDistance = distance;
						closest = npc;
					}
				}
			}

			return closest;
		}

		// VISUAL SHADER GLOW / BLOOM ADDITIVE
		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D texture = TextureAssets.Projectile[Type].Value;
			Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.ZoomMatrix);

			for (int k = Projectile.oldPos.Length - 1; k > 0; k--)
			{
				Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + drawOrigin;
				float progress = (float)k / Projectile.oldPos.Length;
				
				Color auraColor = Color.Lerp(Color.HotPink, Color.DeepSkyBlue, progress) * ((1f - progress) * 0.6f);

				Main.EntitySpriteDraw(texture, drawPos, null, auraColor, Projectile.oldRot[k], drawOrigin, Projectile.scale * 1.2f, SpriteEffects.None, 0);
			}
			
			Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.Magenta * 0.5f, Projectile.rotation, drawOrigin, Projectile.scale * 1.1f, SpriteEffects.None, 0);

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.ZoomMatrix);

			return true;
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item105, Projectile.position);
			for (int i = 0; i < 15; i++)
			{
				Vector2 speed = Main.rand.NextVector2Circular(5f, 5f);
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy, speed.X, speed.Y, 0, default, 1.4f);
				dust.noGravity = true;
			}
		}
	}

	// =======================================================
	// 3. PROYEKTIL LASER KOSMIK (COSMIC LASER)
	// =======================================================
	public class CosmicLaser : ModProjectile
	{
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.PurpleLaser;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 6;
			ProjectileID.Sets.TrailingMode[Type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 10;
			Projectile.height = 10;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = 2; // Menembus 2 musuh
			Projectile.timeLeft = 180;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
			Projectile.extraUpdates = 1; // Bergerak sangat cepat
		}

		public override void AI()
		{
			Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

			Lighting.AddLight(Projectile.Center, 0.8f, 0.2f, 0.8f);

			if (Main.rand.NextBool(2))
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy, 0f, 0f, 100, default, 1.0f);
				dust.noGravity = true;
				dust.velocity *= 0.1f;
			}
		}

		// VISUAL SHADER GLOW / BLOOM ADDITIVE
		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D texture = TextureAssets.Projectile[Type].Value;
			Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.ZoomMatrix);

			for (int k = Projectile.oldPos.Length - 1; k > 0; k--)
			{
				Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + drawOrigin;
				float progress = (float)k / Projectile.oldPos.Length;
				
				Color glowColor = Color.Lerp(Color.White, Color.Magenta, progress) * (1f - progress);

				Main.EntitySpriteDraw(texture, drawPos, null, glowColor, Projectile.oldRot[k], drawOrigin, Projectile.scale * 1.5f, SpriteEffects.None, 0);
			}

			Main.spriteBatch.End();
			Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.ZoomMatrix);

			return true;
		}

		public override void OnKill(int timeLeft)
		{
			for (int i = 0; i < 6; i++)
			{
				Vector2 speed = Main.rand.NextVector2Circular(3f, 3f);
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PinkFairy, speed.X, speed.Y);
				dust.noGravity = true;
			}
		}
	}
}