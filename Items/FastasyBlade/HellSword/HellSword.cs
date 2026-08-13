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

namespace TheSanity.Items.FastasyBlade.HellSword
{
	// =======================================================
	// 1. KELAS ITEM UTAMA (HELLSWORD)
	// =======================================================
	public class HellSword : ModItem
	{
		private const int ComboResetDelay = 120; // Reset kombo jika diam 2 detik

		private enum ComboStage
		{
			UpSlash1 = 0,    // Atas -> Bawah
			DownSlash1 = 1,  // Bawah -> Atas
			UpSlash2 = 2,    // Atas -> Bawah
			DownSlash2 = 3,  // Bawah -> Atas
			AutoFinisher = 4 // Dipanggil, lalu reset ke UpSlash1
		}

		private ComboStage stage = ComboStage.UpSlash1;
		private int comboResetTimer = 0;

		public override void SetDefaults()
		{
			Item.damage = 55;
			Item.DamageType = DamageClass.Melee;
			Item.width = 48;
			Item.height = 48;
			Item.useTime = 20;
			Item.useAnimation = 20;
			Item.useStyle = ItemUseStyleID.Swing; // Menggunakan Swing style bawaan
			Item.knockBack = 5f;
			Item.value = Item.buyPrice(0, 10, 0, 0);
			Item.rare = ItemRarityID.Orange;
			Item.UseSound = SoundID.Item1;
			Item.autoReuse = true;

			Item.shoot = ModContent.ProjectileType<AutoAttackingHellSword>();
			Item.shootSpeed = 20f;
		}
        public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.TheHorsemansBlade, 1)
            .AddIngredient(ItemID.LivingFireBlock, 20)
            .AddIngredient(391, 12)
            .AddIngredient(ItemID.HellstoneBar, 25)
            .AddIngredient<ReligiaBar>(15)
            .AddTile(TileID.MythrilAnvil)
            .Register();

            
    }

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "HellSwordCombo1", "Executes a 5-stage infernal combo:"));
			tooltips.Add(new TooltipLine(Mod, "HellSwordCombo2", "[c/FFA500:Slash] ➔ [c/FF4500:Flame Slash] ➔ [c/FFA500:Slash] ➔ [c/FF4500:Flame Slash] ➔ [c/FF2200:Spectral Finisher]"));
			tooltips.Add(new TooltipLine(Mod, "HellSwordCombo3", "Downward slashes unleash homing hellfire embers"));
			tooltips.Add(new TooltipLine(Mod, "HellSwordCombo4", "[c/FF4500:Finisher:] Summons a phantom blade that autonomously hunts foes for 5 seconds"));
			tooltips.Add(new TooltipLine(Mod, "HellSwordCombo5", "Finisher hits spawn orbiting soulfire, launching toward your cursor when the blade dissipates"));
		}

		public override bool CanUseItem(Player player)
		{
			// Mencegah swing jika pedang otomatis finisher masih aktif
			return player.ownedProjectileCounts[ModContent.ProjectileType<AutoAttackingHellSword>()] <= 0;
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
			stage = ComboStage.UpSlash1; // Selalu reset ke ayunan Atas ke Bawah
			comboResetTimer = 0;
		}

		// =======================================================
		// CUSTOM SWING ANIMATION VIA UseStyle (MENEMPEL DI TANGAN)
		// =======================================================
		public override void UseStyle(Player player, Rectangle heldItemFrame)
		{
			if (player.itemAnimation > 0)
			{
				// Progress ayunan dari 0.0 (awal) hingga 1.0 (akhir)
				float progress = 1f - ((float)player.itemAnimation / player.itemAnimationMax);

				// Tentukan arah ayunan: Atas ke Bawah atau Bawah ke Atas
				bool topToBottom = (stage == ComboStage.UpSlash1 || stage == ComboStage.UpSlash2);

				// Sudut ayunan pedang (dalam Radian)
				float startAngle = topToBottom ? -MathHelper.ToRadians(110f) : MathHelper.ToRadians(60f);
				float endAngle = topToBottom ? MathHelper.ToRadians(60f) : -MathHelper.ToRadians(110f);

				// Interpolasi rotasi sudut
				float currentAngle = MathHelper.Lerp(startAngle, endAngle, progress);
				player.itemRotation = currentAngle * player.direction;

				// Hitung koordinat posisi tangan player berdasarkan stage ayunan
				Vector2 handOffset;
				if (progress < 0.35f)
				{
					// Awal ayunan
					handOffset = topToBottom 
						? new Vector2(player.direction * -2f, -8f) 
						: new Vector2(player.direction * 6f, 6f);
				}
				else if (progress < 0.70f)
				{
					// Tengah ayunan (Depan dada)
					handOffset = new Vector2(player.direction * 6f, -2f);
				}
				else
				{
					// Akhir ayunan
					handOffset = topToBottom 
						? new Vector2(player.direction * 6f, 6f) 
						: new Vector2(player.direction * -2f, -8f);
				}

				// MENIMPA PUSH OFFSET VANILLA: Mengunci gagang pedang tepat di tangan player
				player.itemLocation = player.MountedCenter + handOffset;
			}
		}

		public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			comboResetTimer = 0;

			switch (stage)
			{
				case ComboStage.UpSlash1:
					stage = ComboStage.DownSlash1; // Selanjutnya Bawah ke Atas
					break;

				case ComboStage.DownSlash1:
					FireElementalArc(player, source, position, velocity, damage, knockback);
					stage = ComboStage.UpSlash2; // Selanjutnya Atas ke Bawah
					break;

				case ComboStage.UpSlash2:
					stage = ComboStage.DownSlash2; // Selanjutnya Bawah ke Atas
					break;

				case ComboStage.DownSlash2:
					FireElementalArc(player, source, position, velocity, damage, knockback);
					stage = ComboStage.AutoFinisher; // Selanjutnya Finisher
					break;

				case ComboStage.AutoFinisher:
				default:
					// Panggil Pedang Otomatis (Phantom Blade)
					Projectile.NewProjectile(source, position, velocity * 1.2f, ModContent.ProjectileType<AutoAttackingHellSword>(), (int)(damage * 1.4f), knockback * 1.2f, player.whoAmI);

					// Efek ledakan visual finisher
					SoundEngine.PlaySound(SoundID.Item74, position);
					for (int i = 0; i < 15; i++)
					{
						Vector2 speed = Main.rand.NextVector2Circular(5f, 5f);
						Dust.NewDust(position, Item.width, Item.height, DustID.Torch, speed.X, speed.Y, 0, default, 1.6f);
					}

					// RESET KAMBALI KE AYUNAN ATAS KE BAWAH!
					ResetCombo();
					break;
			}

			return false;
		}

		private void FireElementalArc(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int damage, float knockback)
		{
			const int starCount = 3;
			const float spreadDegrees = 40f;
			int starDamage = (int)(damage * 0.45f);

			for (int i = 0; i < starCount; i++)
			{
				float t = starCount == 1 ? 0.5f : i / (float)(starCount - 1);
				float angle = MathHelper.ToRadians(MathHelper.Lerp(-spreadDegrees / 2f, spreadDegrees / 2f, t));
				Vector2 starVelocity = velocity.RotatedBy(angle) * 0.95f;

				Projectile.NewProjectile(source, position, starVelocity, ModContent.ProjectileType<HellHomingFireball>(), starDamage, knockback * 0.5f, player.whoAmI);
			}
		}
	}

	// =======================================================
	// 2. PROYEKTIL FINISHER OTOMATIS (AUTO-ATTACKING HELLSWORD)
	// =======================================================
	public class AutoAttackingHellSword : ModProjectile
	{
		private const int BurstCount = 4;
		private int burstCooldown = 0;
		private const int BurstCooldownMax = 30;

		public override string Texture => "TheSanity/Items/FastasyBlade/HellSword/HellSword";

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 10;
			ProjectileID.Sets.TrailingMode[Type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 36;
			Projectile.height = 36;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 300; // 5 Detik
			Projectile.tileCollide = false;
			Projectile.scale = 1.8f;
			Projectile.ignoreWater = true;
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 10;
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

			if (burstCooldown > 0)
			{
				burstCooldown--;
			}

			NPC target = FindClosestNPC(900f);

			if (target != null)
			{
				int attackCycle = 24;
				int cycleTimer = (int)(300 - Projectile.timeLeft) % attackCycle;

				if (cycleTimer < 14)
				{
					Vector2 targetOffset = (Projectile.Center - target.Center).SafeNormalize(-Vector2.UnitY) * 110f;
					Vector2 destination = target.Center + targetOffset;
					Vector2 moveVector = destination - Projectile.Center;
					
					Projectile.velocity = Vector2.Lerp(Projectile.velocity, moveVector * 0.25f, 0.2f);

					float targetRot = (target.Center - Projectile.Center).ToRotation() + MathHelper.PiOver4;
					Projectile.rotation = Utils.AngleLerp(Projectile.rotation, targetRot, 0.25f);
				}
				else if (cycleTimer == 14)
				{
					Vector2 dashDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitX);
					Projectile.velocity = dashDir * 28f;
					SoundEngine.PlaySound(SoundID.Item71, Projectile.Center);
				}
				else
				{
					if (Projectile.velocity.Length() > 0.5f)
					{
						float targetRot = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
						Projectile.rotation = Utils.AngleLerp(Projectile.rotation, targetRot, 0.3f);
					}
				}
			}
			else
			{
				Vector2 destination = player.Center + new Vector2(0f, -70f);
				Vector2 moveVector = destination - Projectile.Center;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, moveVector * 0.1f, 0.15f);

				if (Projectile.velocity.Length() > 0.5f)
				{
					float targetRot = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
					Projectile.rotation = Utils.AngleLerp(Projectile.rotation, targetRot, 0.15f);
				}
			}

			float pulse = (float)Math.Sin(Main.GameUpdateCount * 0.15f) * 0.15f;
			Projectile.scale = 1.8f + pulse;

			Lighting.AddLight(Projectile.Center, 1.0f, 0.5f, 0.1f);

			if (Main.rand.NextBool(2))
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f);
				dust.noGravity = true;
				dust.scale = 1.2f;
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

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D texture = TextureAssets.Projectile[Type].Value;
			Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

			for (int k = Projectile.oldPos.Length - 1; k > 0; k--)
			{
				Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + drawOrigin + new Vector2(0f, Projectile.gfxOffY);
				float progress = (float)k / Projectile.oldPos.Length;
				
				Color trailColor = Color.Lerp(Color.Gold, Color.Red, progress) * ((1f - progress) * 0.6f);
				float oldRotation = Projectile.oldRot[k];

				Main.EntitySpriteDraw(
					texture,
					drawPos,
					null,
					trailColor,
					oldRotation,
					drawOrigin,
					Projectile.scale * (1f - (progress * 0.2f)),
					SpriteEffects.None,
					0
				);
			}

			return true;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (Projectile.owner == Main.myPlayer)
			{
				if (burstCooldown <= 0)
				{
					SummonBoneSerpentBurst(target.Center);
					burstCooldown = BurstCooldownMax;
				}

				int orbCount = Main.rand.Next(1, 3);
				for (int i = 0; i < orbCount; i++)
				{
					Vector2 spawnVelocity = Main.rand.NextVector2Circular(4f, 4f);
					Projectile.NewProjectile(
						Projectile.GetSource_OnHit(target),
						target.Center,
						spawnVelocity,
						ModContent.ProjectileType<HellOrbitOrb>(),
						(int)(Projectile.damage * 0.5f),
						1.5f,
						Projectile.owner,
						0f,
						Main.rand.NextFloat(MathHelper.TwoPi)
					);
				}
			}
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item14, Projectile.position);

			for (int i = 0; i < 20; i++)
			{
				Vector2 speed = Main.rand.NextVector2Circular(7f, 7f);
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, speed.X, speed.Y);
				dust.noGravity = true;
				dust.scale = 1.6f;
			}
		}

		private void SummonBoneSerpentBurst(Vector2 targetPos)
		{
			SoundEngine.PlaySound(SoundID.NPCDeath13, targetPos);

			for (int i = 0; i < BurstCount; i++)
			{
				Vector2 spawnPos = targetPos + new Vector2(Main.rand.Next(-60, 61), 150 + (i * 18));
				Vector2 velocity = (targetPos - spawnPos).SafeNormalize(Vector2.UnitY) * 14f;

				int proj = Projectile.NewProjectile(
					Projectile.GetSource_FromThis(),
					spawnPos,
					velocity,
					ModContent.ProjectileType<HellHomingFireball>(),
					(int)(Projectile.damage * 0.65f),
					Projectile.knockBack,
					Projectile.owner
				);

				if (proj < Main.maxProjectiles)
				{
					Main.projectile[proj].friendly = true;
					Main.projectile[proj].hostile = false;
					Main.projectile[proj].tileCollide = false;
				}
			}
		}
	}

	// =======================================================
	// 3. PROYEKTIL BOLA API MENGORBIT & LAUNCH (HELL ORBIT ORB)
	// =======================================================
	public class HellOrbitOrb : ModProjectile
	{
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 8;
			ProjectileID.Sets.TrailingMode[Type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 16;
			Projectile.height = 16;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 600;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
		}

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];

			if (!player.active || player.dead)
			{
				Projectile.Kill();
				return;
			}

			if (Main.rand.NextBool(2))
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 1.2f);
				dust.noGravity = true;
				dust.velocity *= 0.2f;
			}

			Lighting.AddLight(Projectile.Center, 0.8f, 0.4f, 0.1f);

			bool isOrbiting = Projectile.ai[0] == 0f;

			if (isOrbiting)
			{
				bool finisherActive = player.ownedProjectileCounts[ModContent.ProjectileType<AutoAttackingHellSword>()] > 0;

				if (finisherActive)
				{
					Projectile.ai[1] += 0.09f;
					float orbitRadius = 75f;

					Vector2 targetOrbitPos = player.Center + new Vector2(orbitRadius, 0f).RotatedBy(Projectile.ai[1]);
					
					Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetOrbitPos - Projectile.Center, 0.25f);
					Projectile.rotation += 0.2f;
				}
				else
				{
					Projectile.ai[0] = 1f;

					Vector2 toCursor = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitY);
					Projectile.velocity = toCursor * 20f;

					SoundEngine.PlaySound(SoundID.Item20, Projectile.Center);
				}
			}
			else
			{
				float homingRange = 600f;
				NPC target = FindClosestNPC(homingRange);

				if (target != null)
				{
					float speed = 19f;
					float inertia = 8f;

					Vector2 targetDirection = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
					Projectile.velocity = (Projectile.velocity * (inertia - 1f) + targetDirection * speed) / inertia;
				}

				if (Projectile.velocity.Length() > 0.1f)
				{
					float targetRotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
					Projectile.rotation = Utils.AngleLerp(Projectile.rotation, targetRotation, 0.25f);
				}
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

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D texture = TextureAssets.Projectile[Type].Value;
			Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

			for (int k = Projectile.oldPos.Length - 1; k > 0; k--)
			{
				Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + drawOrigin;
				float progress = (float)k / Projectile.oldPos.Length;
				Color color = Color.Lerp(Color.Gold, Color.OrangeRed, progress) * ((1f - progress) * 0.7f);

				Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.oldRot[k], drawOrigin, Projectile.scale * (1f - progress * 0.3f), SpriteEffects.None, 0);
			}

			return true;
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item14, Projectile.position);
			for (int i = 0; i < 10; i++)
			{
				Vector2 speed = Main.rand.NextVector2Circular(4f, 4f);
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, speed.X, speed.Y, 100, default, 1.4f);
				dust.noGravity = true;
			}
		}
	}

	// =======================================================
	// 4. PROYEKTIL BOLA API HOMING SWING (HELL HOMING FIREBALL)
	// =======================================================
	public class HellHomingFireball : ModProjectile
	{
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BallofFire;

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 8;
			ProjectileID.Sets.TrailingMode[Type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 18;
			Projectile.height = 18;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Melee;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 240;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
		}

		public override void AI()
		{
			if (Main.rand.NextBool(2))
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 1.4f);
				dust.noGravity = true;
				dust.velocity *= 0.3f;
			}

			Lighting.AddLight(Projectile.Center, 0.9f, 0.45f, 0.1f);

			float homingRange = 550f;
			NPC target = FindClosestNPC(homingRange);

			if (target != null)
			{
				float speed = 17f;
				float inertia = 9f;

				Vector2 targetDirection = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
				Projectile.velocity = (Projectile.velocity * (inertia - 1f) + targetDirection * speed) / inertia;
			}

			if (Projectile.velocity.Length() > 0.1f)
			{
				float targetRotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
				Projectile.rotation = Utils.AngleLerp(Projectile.rotation, targetRotation, 0.25f);
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

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D texture = TextureAssets.Projectile[Type].Value;
			Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

			for (int k = Projectile.oldPos.Length - 1; k > 0; k--)
			{
				Vector2 drawPos = (Projectile.oldPos[k] - Main.screenPosition) + drawOrigin;
				float progress = (float)k / Projectile.oldPos.Length;
				Color color = Color.Lerp(Color.Orange, Color.Red, progress) * ((1f - progress) * 0.7f);

				Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.oldRot[k], drawOrigin, Projectile.scale * (1f - progress * 0.3f), SpriteEffects.None, 0);
			}

			return true;
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item20, Projectile.position);
			for (int i = 0; i < 12; i++)
			{
				Vector2 speed = Main.rand.NextVector2Circular(4f, 4f);
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, speed.X, speed.Y, 100, default, 1.5f);
				dust.noGravity = true;
			}
		}
	}
}