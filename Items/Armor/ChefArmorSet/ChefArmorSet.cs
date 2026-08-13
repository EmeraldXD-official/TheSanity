using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.OreBar.Regilia;

namespace TheSanity.Items.Armor.ChefArmorSet
{
	// =======================================================
	// 1. CHEF HAT (HEAD)
	// =======================================================
	[AutoloadEquip(EquipType.Head)]
	public class ChefHat : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 18;
			Item.height = 18;
			Item.value = Item.sellPrice(0, 6, 0, 0);
			Item.rare = ItemRarityID.Yellow;
			Item.defense = 8;
		}

		public override void UpdateEquip(Player player)
		{
			player.GetDamage(DamageClass.Ranged) += 0.08f;
			player.GetCritChance(DamageClass.Ranged) += 6;
			
			ChefArmorPlayer modPlayer = player.GetModPlayer<ChefArmorPlayer>();
			modPlayer.hasChefHat = true;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "HatStats", "+8% Ranged Damage"));
			tooltips.Add(new TooltipLine(Mod, "HatCrit", "+6% Ranged Critical Strike Chance"));
			tooltips.Add(new TooltipLine(Mod, "HatCritDmg", "+10% Ranged Critical Damage"));

			Player player = Main.LocalPlayer;
			if (player.armor[0].type == ModContent.ItemType<ChefHat>() &&
				player.armor[1].type == ModContent.ItemType<ChefCoat>() &&
				player.armor[2].type == ModContent.ItemType<ChefPants>())
			{
				tooltips.Add(new TooltipLine(Mod, "ChefSetDetails",
					"=== Set Bonus (Meat Grinder Station) ===\n" +
					"• Press [DOWN] 2x to deploy the Station\n" +
					"• Inside Area: +10% Ranged Damage & +25% Bullet Velocity\n" +
					"• Automatically attracts nearby Items, Hearts, and Stars\n" +
					"• Ranged attacks launch an extra 'Slicing Wave'")
				{
					OverrideColor = new Color(220, 100, 100)
				});
			}
		}

		public override bool IsArmorSet(Item head, Item body, Item legs)
		{
			return head.type == ModContent.ItemType<ChefHat>() &&
			       body.type == ModContent.ItemType<ChefCoat>() && 
			       legs.type == ModContent.ItemType<ChefPants>();
		}

		public override void UpdateArmorSet(Player player)
		{
			player.setBonus = "Press [DOWN] 2x to deploy 'Meat Grinder Station'!";
			player.GetModPlayer<ChefArmorPlayer>().hasChefSet = true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.ChefHat, 1)
				.AddIngredient(ItemID.ShroomiteBar, 12)
                 .AddIngredient<ReligiaBar>(15)
				.AddTile(TileID.Autohammer)
				.Register();
		}
	}

	// =======================================================
	// 2. CHEF COAT (BODY)
	// =======================================================
	[AutoloadEquip(EquipType.Body)]
	public class ChefCoat : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 18;
			Item.height = 14;
			Item.value = Item.sellPrice(0, 8, 0, 0);
			Item.rare = ItemRarityID.Yellow;
			Item.defense = 14;
		}

		public override void UpdateEquip(Player player)
		{
			player.GetDamage(DamageClass.Ranged) += 0.10f;
			player.GetCritChance(DamageClass.Ranged) += 6;
			player.ammoCost80 = true;

			ChefArmorPlayer modPlayer = player.GetModPlayer<ChefArmorPlayer>();
			modPlayer.hasChefCoat = true;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "CoatStats", "+10% Ranged Damage"));
			tooltips.Add(new TooltipLine(Mod, "CoatCrit", "+6% Ranged Critical Strike Chance"));
			tooltips.Add(new TooltipLine(Mod, "CoatCritDmg", "+10% Ranged Critical Damage"));
			tooltips.Add(new TooltipLine(Mod, "CoatAmmo", "20% chance not to consume ammo"));

			Player player = Main.LocalPlayer;
			if (player.armor[0].type == ModContent.ItemType<ChefHat>() &&
				player.armor[1].type == ModContent.ItemType<ChefCoat>() &&
				player.armor[2].type == ModContent.ItemType<ChefPants>())
			{
				tooltips.Add(new TooltipLine(Mod, "ChefSetDetails",
					"=== Set Bonus (Meat Grinder Station) ===\n" +
					"• Press [DOWN] 2x to deploy the Station\n" +
					"• Inside Area: +10% Ranged Damage & +25% Bullet Velocity\n" +
					"• Automatically attracts nearby Items, Hearts, and Stars\n" +
					"• Ranged attacks launch an extra 'Slicing Wave'")
				{
					OverrideColor = new Color(220, 100, 100)
				});
			}
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.ChefShirt, 1)
				.AddIngredient(ItemID.ShroomiteBar, 20)
                 .AddIngredient<ReligiaBar>(15)
				.AddTile(TileID.Autohammer)
				.Register();
		}
	}

	// =======================================================
	// 3. CHEF PANTS (LEGS)
	// =======================================================
	[AutoloadEquip(EquipType.Legs)]
	public class ChefPants : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 18;
			Item.height = 12;
			Item.value = Item.sellPrice(0, 6, 0, 0);
			Item.rare = ItemRarityID.Yellow;
			Item.defense = 10;
		}

		public override void UpdateEquip(Player player)
		{
			player.moveSpeed += 0.10f;
			player.GetDamage(DamageClass.Ranged) += 0.06f;
			player.GetCritChance(DamageClass.Ranged) += 5;

			ChefArmorPlayer modPlayer = player.GetModPlayer<ChefArmorPlayer>();
			modPlayer.hasChefPants = true;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "PantsStats", "+6% Ranged Damage"));
			tooltips.Add(new TooltipLine(Mod, "PantsCrit", "+5% Ranged Critical Strike Chance"));
			tooltips.Add(new TooltipLine(Mod, "PantsCritDmg", "+10% Ranged Critical Damage"));
			tooltips.Add(new TooltipLine(Mod, "PantsSpeed", "+10% Movement speed"));

			Player player = Main.LocalPlayer;
			if (player.armor[0].type == ModContent.ItemType<ChefHat>() &&
				player.armor[1].type == ModContent.ItemType<ChefCoat>() &&
				player.armor[2].type == ModContent.ItemType<ChefPants>())
			{
				tooltips.Add(new TooltipLine(Mod, "ChefSetDetails",
					"=== Set Bonus (Meat Grinder Station) ===\n" +
					"• Press [DOWN] 2x to deploy the Station\n" +
					"• Inside Area: +10% Ranged Damage & +25% Bullet Velocity\n" +
					"• Automatically attracts nearby Items, Hearts, and Stars\n" +
					"• Ranged attacks launch an extra 'Slicing Wave'")
				{
					OverrideColor = new Color(220, 100, 100)
				});
			}
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.ChefPants, 1)
				.AddIngredient(ItemID.ShroomiteBar, 16)
                 .AddIngredient<ReligiaBar>(15)
				.AddTile(TileID.Autohammer)
				.Register();
		}
	}

	// =======================================================
	// 4. MODPLAYER LOGIC (TRIGGERS, GLOW EFFECT & SET STATUS)
	// =======================================================
	public class ChefArmorPlayer : ModPlayer
	{
		public bool hasChefSet;
		public bool hasChefHat;
		public bool hasChefCoat;
		public bool hasChefPants;
		public bool inGrinderZone;
		public int sliceCooldown;

		private int doubleTapDownTimer;

		public override void ResetEffects()
		{
			hasChefSet = false;
			hasChefHat = false;
			hasChefCoat = false;
			hasChefPants = false;
			inGrinderZone = false;

			if (sliceCooldown > 0)
				sliceCooldown--;

			if (doubleTapDownTimer > 0)
				doubleTapDownTimer--;
		}

		public override void PostUpdateEquips()
		{
			// Efek Glow / Cahaya mengalir dari atas ke bawah (Top to Bottom) saat memakai bagian armor chef
			if (hasChefHat || hasChefCoat || hasChefPants)
			{
				Lighting.AddLight(Player.Center, 0.4f, 0.1f, 0.1f);

				if (Main.rand.NextBool(4))
				{
					Vector2 spawnPos = new Vector2(
						Main.rand.NextFloat(Player.getRect().X, Player.getRect().X + Player.getRect().Width),
						Player.Top.Y + Main.rand.NextFloat(0f, Player.height)
					);
					
					Dust dust = Dust.NewDustPerfect(spawnPos, DustID.GemRuby, Vector2.Zero, 120, default, 0.9f);
					dust.noGravity = true;
					dust.velocity = new Vector2(0, 2.5f); // Mengalir ke bawah
				}
			}
		}

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (!hasChefSet)
				return;

			if (Player.controlDown && Player.releaseDown)
			{
				if (doubleTapDownTimer > 0)
				{
					doubleTapDownTimer = 0;
					SpawnMeatGrinderStation();
				}
				else
				{
					doubleTapDownTimer = 15;
				}
			}
		}

		private void SpawnMeatGrinderStation()
		{
			for (int i = 0; i < Main.maxProjectiles; i++)
			{
				Projectile p = Main.projectile[i];
				if (p.active && p.owner == Player.whoAmI && p.type == ModContent.ProjectileType<MeatGrinderStation>())
				{
					p.Kill();
				}
			}

			Vector2 spawnPos = Player.Bottom - new Vector2(0, 16);
			Projectile.NewProjectile(
				Player.GetSource_Misc("ChefSetBonus"),
				spawnPos,
				Vector2.Zero,
				ModContent.ProjectileType<MeatGrinderStation>(),
				60,
				3f,
				Player.whoAmI
			);

			SoundEngine.PlaySound(SoundID.Item37, Player.Center);
		}
	}

	// =======================================================
	// 5. GLOBAL ITEM HOOK (CRIT DAMAGE FOR ITEMS & SHOOT MODS)
	// =======================================================
	public class ChefGlobalItem : GlobalItem
	{
		public override void ModifyHitNPC(Item item, Player player, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (item.CountsAsClass(DamageClass.Ranged))
			{
				ChefArmorPlayer modPlayer = player.GetModPlayer<ChefArmorPlayer>();
				if (modPlayer.hasChefHat) modifiers.CritDamage += 0.10f;
				if (modPlayer.hasChefCoat) modifiers.CritDamage += 0.10f;
				if (modPlayer.hasChefPants) modifiers.CritDamage += 0.10f;
			}
		}

		public override void ModifyShootStats(Item item, Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
		{
			ChefArmorPlayer modPlayer = player.GetModPlayer<ChefArmorPlayer>();

			if (modPlayer.inGrinderZone && item.CountsAsClass(DamageClass.Ranged))
			{
				velocity *= 1.25f;
			}
		}

		public override bool Shoot(Item item, Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
		{
			ChefArmorPlayer modPlayer = player.GetModPlayer<ChefArmorPlayer>();

			if (modPlayer.inGrinderZone && item.CountsAsClass(DamageClass.Ranged) && modPlayer.sliceCooldown <= 0)
			{
				modPlayer.sliceCooldown = 20;

				Vector2 sliceVel = velocity.SafeNormalize(Vector2.UnitX * player.direction) * 14f;

				Projectile.NewProjectile(
					source,
					position,
					sliceVel,
					ModContent.ProjectileType<MeatGrinderSlice>(),
					(int)(damage * 0.40f),
					knockback * 0.4f,
					player.whoAmI
				);

				SoundEngine.PlaySound(SoundID.Item71, position);
			}

			return base.Shoot(item, player, source, position, velocity, type, damage, knockback);
		}
	}

	// =======================================================
	// 6. GLOBAL PROJECTILE HOOK (CRIT DAMAGE FOR PROJECTILES)
	// =======================================================
	public class ChefGlobalProjectile : GlobalProjectile
	{
		public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
			{
				Player player = Main.player[projectile.owner];
				ChefArmorPlayer modPlayer = player.GetModPlayer<ChefArmorPlayer>();

				if (projectile.CountsAsClass(DamageClass.Ranged))
				{
					if (modPlayer.hasChefHat) modifiers.CritDamage += 0.10f;
					if (modPlayer.hasChefCoat) modifiers.CritDamage += 0.10f;
					if (modPlayer.hasChefPants) modifiers.CritDamage += 0.10f;
				}
			}
		}
	}

	// =======================================================
	// 7. MEAT GRINDER STATION
	// =======================================================
	public class MeatGrinderStation : ModProjectile
	{
		public override string Texture => "Terraria/Images/Item_" + ItemID.MeatGrinder;

		private const float ZoneRadius = 300f;

		public override void SetDefaults()
		{
			Projectile.width = 30;
			Projectile.height = 30;
			Projectile.friendly = true;
			Projectile.penetrate = -1;
			Projectile.timeLeft = 1800;
			Projectile.tileCollide = false;
			Projectile.ignoreWater = true;
		}

		public override void AI()
		{
			Player owner = Main.player[Projectile.owner];

			Projectile.velocity = Vector2.Zero;
			Projectile.rotation += 0.02f;

			if (Main.rand.NextBool(5))
			{
				Dust smoke = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0, -1f, 100, default, 0.8f);
				smoke.noGravity = true;
			}

			if (Vector2.Distance(owner.Center, Projectile.Center) <= ZoneRadius)
			{
				ChefArmorPlayer modPlayer = owner.GetModPlayer<ChefArmorPlayer>();
				modPlayer.inGrinderZone = true;

				owner.GetDamage(DamageClass.Ranged) += 0.10f;

				Lighting.AddLight(owner.Center, 0.8f, 0.2f, 0.2f);
			}

			for (int i = 0; i < Main.maxItems; i++)
			{
				Item item = Main.item[i];
				if (item.active && item.noGrabDelay == 0 && Vector2.Distance(item.Center, Projectile.Center) <= ZoneRadius)
				{
					Vector2 pullDirection = (owner.Center - item.Center).SafeNormalize(Vector2.Zero);
					item.velocity = Vector2.Lerp(item.velocity, pullDirection * 8f, 0.12f);
				}
			}
		}

		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D auraTexture = TextureAssets.Extra[174].Value;

			float auraScale = (ZoneRadius * 2f) / auraTexture.Width;

			Vector2 drawPosition = Projectile.Center - Main.screenPosition;
			Vector2 origin = auraTexture.Size() / 2f;

			Color auraColor = new Color(255, 80, 80, 0) * 0.35f;

			Main.EntitySpriteDraw(
				auraTexture,
				drawPosition,
				null,
				auraColor,
				Projectile.rotation,
				origin,
				auraScale,
				SpriteEffects.None,
				0
			);

			return true;
		}

		public override bool OnTileCollide(Vector2 oldVelocity)
		{
			return false;
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Dig, Projectile.Center);
			for (int i = 0; i < 12; i++)
			{
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Blood);
			}
		}
	}

	// =======================================================
	// 8. SLICING WAVE
	// =======================================================
	public class MeatGrinderSlice : ModProjectile
	{
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FlyingKnife;

		public override void SetDefaults()
		{
			Projectile.width = 18;
			Projectile.height = 18;
			Projectile.friendly = true;
			Projectile.DamageType = DamageClass.Ranged;
			Projectile.penetrate = 2;
			Projectile.timeLeft = 100;
			Projectile.tileCollide = true;
			Projectile.ignoreWater = true;
		}

		public override void AI()
		{
			Projectile.rotation += 0.35f * Projectile.direction;

			if (Main.rand.NextBool(3))
			{
				Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 100, default, 0.8f);
				dust.noGravity = true;
				dust.velocity *= 0.2f;
			}
		}

		public override void OnKill(int timeLeft)
		{
			for (int i = 0; i < 4; i++)
			{
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Blood);
			}
		}
	}
}