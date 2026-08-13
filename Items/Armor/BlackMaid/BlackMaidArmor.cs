using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent.UI;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Conditions;
using TheSanity.Items.OreBar.Regilia;

namespace TheSanity.Items.Armor.BlackMaid
{
	// 5 Jenis Mood yang Berbeda
	public enum MaidMood
	{
		Normal,
		Angry,
		Happy,
		Sleepy,
		Shy
	}

	// =======================================================
	// 1. BLACK MAID BONNET
	// =======================================================
	[AutoloadEquip(EquipType.Head)]
	public class BlackMaidBonnet : ModItem
	{
		public override string Texture => "TheSanity/Items/Armor/BlackMaid/BlackMaidBonnet";

		public override void SetStaticDefaults()
		{
			ArmorIDs.Head.Sets.DrawFullHair[Item.headSlot] = true;
		}

		public override void SetDefaults()
		{
			Item.width = 18;
			Item.height = 18;
			Item.value = Item.sellPrice(0, 1, 50, 0);
			Item.rare = ItemRarityID.Pink;
			Item.defense = 6;
		}

		public override void UpdateEquip(Player player)
		{
			player.GetDamage(DamageClass.Summon) += 0.10f;
			player.maxMinions += 1;
			player.GetModPlayer<BlackMaidPlayer>().wearingBonnet = true;
		}

		public override void UpdateVanity(Player player)
		{
			player.GetModPlayer<BlackMaidPlayer>().wearingBonnet = true;
		}

		public override bool IsArmorSet(Item head, Item body, Item legs)
		{
			return body.type == ModContent.ItemType<BlackMaidDress>() && legs.type == ModContent.ItemType<BlackMaidShoes>();
		}

		public override void UpdateArmorSet(Player player)
		{
			player.setBonus = "+2 max minion slots\n" +
			                  "Minions can critically strike (+10% base summon crit chance)\n" +
			                  "[c/FF69B4:Random Maid Mood System:]\n" +
			                  " Periodically switches mood with unique effects:\n" +
			                  " [c/FFB6C1:• Normal:] +5% summon damage & +5% summon crit\n" +
			                  " [c/FF3333:• Angry:] +12% summon damage & +8% summon crit\n" +
			                  " [c/FFD700:• Happy:] +1 max minion & +15% move speed\n" +
			                  " [c/00FFFF:• Sleepy:] +8 defense & +2 HP/s regen (-10% move speed)\n" +
			                  " [c/DA70D6:• Shy:] +8% dodge chance & +10% move speed (-5% summon dmg)";

			player.maxMinions += 2;
			player.GetCritChance(DamageClass.Summon) += 10f;

			player.GetModPlayer<BlackMaidPlayer>().hasBlackMaidSet = true;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "BlackMaidBonnetDesc", "Increases summon damage by 10% and max minions by 1\nChanges hairstyle to style 6 while worn"));
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<ReligiaBar>(5)
				.AddIngredient(4128, 1)
				.AddCondition(PlayerStateConditions.WearingDiamondRing)
				.AddTile(TileID.Loom)
				.Register();
		}
	}

	// =======================================================
	// 2. BLACK MAID DRESS
	// =======================================================
	[AutoloadEquip(EquipType.Body)]
	public class BlackMaidDress : ModItem
	{
		public override string Texture => "TheSanity/Items/Armor/BlackMaid/BlackMaidDress";

		public override void SetMatch(bool male, ref int equipSlot, ref bool robes)
		{
			robes = true;
		}

		public override void SetDefaults()
		{
			Item.width = 18;
			Item.height = 18;
			Item.value = Item.sellPrice(0, 2, 0, 0);
			Item.rare = ItemRarityID.Pink;
			Item.defense = 10;
		}

		public override void UpdateEquip(Player player)
		{
			player.GetDamage(DamageClass.Summon) += 0.12f;
			player.maxMinions += 1;
			player.GetModPlayer<BlackMaidPlayer>().wearingDress = true;
		}

		public override void UpdateVanity(Player player)
		{
			player.GetModPlayer<BlackMaidPlayer>().wearingDress = true;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "BlackMaidDressDesc", "Increases summon damage by 12% and max minions by 1\n[c/FF69B4:Glows with magical energy!]"));
		}

		public override void PostDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, float rotation, float scale, int whoAmI)
		{
			Texture2D texture = Terraria.GameContent.TextureAssets.Item[Item.type].Value;
			Vector2 position = Item.Center - Main.screenPosition;
			Vector2 origin = texture.Size() * 0.5f;
			Color glowColor = new Color(255, 105, 180, 0) * 0.7f;
			
			spriteBatch.Draw(texture, position, null, glowColor, rotation, origin, scale, SpriteEffects.None, 0f);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<ReligiaBar>(8)
				.AddIngredient(4129, 1)
				.AddCondition(PlayerStateConditions.WearingDiamondRing)
				.AddTile(TileID.Loom)
				.Register();
		}
	}

	// =======================================================
	// 3. BLACK MAID SHOES
	// =======================================================
	[AutoloadEquip(EquipType.Legs)]
	public class BlackMaidShoes : ModItem
	{
		public override string Texture => "TheSanity/Items/Armor/BlackMaid/BlackMaidShoes";

		public override void SetDefaults()
		{
			Item.width = 18;
			Item.height = 18;
			Item.value = Item.sellPrice(0, 1, 50, 0);
			Item.rare = ItemRarityID.Pink;
			Item.defense = 8;
		}

		public override void UpdateEquip(Player player)
		{
			player.GetDamage(DamageClass.Summon) += 0.08f;
			player.moveSpeed += 0.10f;
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "BlackMaidShoesDesc", "Increases summon damage by 8% and movement speed by 10%"));
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<ReligiaBar>(6)
				.AddIngredient(4130, 1)
				.AddCondition(PlayerStateConditions.WearingDiamondRing)
				.AddTile(TileID.Loom)
				.Register();
		}
	}

	// =======================================================
	// 4. GLOBAL PROJECTILE: MINION CRITICAL STRIKE
	// =======================================================
	public class BlackMaidGlobalProjectile : GlobalProjectile
	{
		public override void ModifyHitNPC(Projectile projectile, NPC target, ref NPC.HitModifiers modifiers)
		{
			if (projectile.minion || projectile.sentry || ProjectileID.Sets.MinionShot[projectile.type])
			{
				Player player = Main.player[projectile.owner];
				if (player.active && !player.dead)
				{
					if (player.GetModPlayer<BlackMaidPlayer>().hasBlackMaidSet)
					{
						float critChance = player.GetCritChance(DamageClass.Summon);
						
						if (Main.rand.NextFloat() < critChance / 100f)
						{
							modifiers.SetCrit();
						}
					}
				}
			}
		}
	}

	// =======================================================
	// 5. PLAYER HOOK: BALANCED PURELY RANDOM MOOD SYSTEM
	// =======================================================
	public class BlackMaidPlayer : ModPlayer
	{
		public bool hasBlackMaidSet = false;
		public MaidMood currentMood = MaidMood.Normal;

		public bool wearingBonnet = false;
		public bool wearingDress = false;
		public int originalHair = -1;

		private int randomMoodTimer = 0;
		private int maidTextCooldown = 0;

		public override void ResetEffects()
		{
			hasBlackMaidSet = false;

			if (!wearingBonnet && originalHair != -1)
			{
				Player.hair = originalHair;
				originalHair = -1;
			}

			wearingBonnet = false;
			wearingDress = false;
		}

		public override void PostUpdateEquips()
		{
			if (wearingBonnet)
			{
				if (originalHair == -1)
				{
					originalHair = Player.hair;
				}

				Player.hair = 6; 
			}

			if (!hasBlackMaidSet) return;

			// Aplikasi efek statistik seimbang untuk setiap mood
			switch (currentMood)
			{
				case MaidMood.Angry:
					Player.GetDamage(DamageClass.Summon) += 0.12f;
					Player.GetCritChance(DamageClass.Summon) += 8f;
					Player.AddBuff(ModContent.BuffType<MaidAngryBuff>(), 2);
					break;

				case MaidMood.Happy:
					Player.maxMinions += 1;
					Player.moveSpeed += 0.15f;
					Player.AddBuff(ModContent.BuffType<MaidHappyBuff>(), 2);
					break;

				case MaidMood.Sleepy:
					Player.statDefense += 8;
					Player.lifeRegen += 4; // +2 HP per detik
					Player.moveSpeed -= 0.10f; // Penalitas movement speed
					Player.AddBuff(ModContent.BuffType<MaidSleepyBuff>(), 2);
					break;

				case MaidMood.Shy:
					Player.moveSpeed += 0.10f;
					Player.GetDamage(DamageClass.Summon) -= 0.05f; // Penalitas damage kecil
					Player.AddBuff(ModContent.BuffType<MaidShyBuff>(), 2);
					break;

				case MaidMood.Normal:
				default:
					Player.GetDamage(DamageClass.Summon) += 0.05f;
					Player.GetCritChance(DamageClass.Summon) += 5f;
					Player.AddBuff(ModContent.BuffType<MaidNormalBuff>(), 2);
					break;
			}
		}

		public override bool FreeDodge(Player.HurtInfo hurtInfo)
		{
			// Peluang 8% Dodge khusus saat sedang mood Shy
			if (hasBlackMaidSet && currentMood == MaidMood.Shy && Main.rand.NextFloat() < 0.08f)
			{
				Player.BrainOfConfusionDodge(); // Efek animasi dodge standar Terraria
				return true;
			}
			return base.FreeDodge(hurtInfo);
		}

		public override void PostUpdate()
		{
			if (!hasBlackMaidSet) return;

			if (maidTextCooldown > 0)
			{
				maidTextCooldown--;
			}

			// --- PERGANTIAN MOOD MURNI ACAK (8 - 16 DETIK) ---
			if (randomMoodTimer > 0)
			{
				randomMoodTimer--;
			}
			else
			{
				// Memilih secara acak 1 dari 5 Mood
				Array moods = Enum.GetValues(typeof(MaidMood));
				MaidMood nextMood = (MaidMood)moods.GetValue(Main.rand.Next(moods.Length));
				
				int durationTicks = Main.rand.Next(480, 960); // 8-16 detik
				
				SetMood(nextMood);
				randomMoodTimer = durationTicks;
			}

			// Particle Aura Visual sesuai Mood
			if (Main.rand.NextBool(12))
			{
				int dustType = currentMood switch
				{
					MaidMood.Angry => DustID.Torch,
					MaidMood.Happy => DustID.YellowTorch,
					MaidMood.Sleepy => DustID.IceTorch,
					MaidMood.Shy => DustID.PurpleTorch,
					_ => DustID.PinkTorch
				};

				int dust = Dust.NewDust(Player.position, Player.width, Player.height, dustType, 0f, 0f, 100, default, 0.9f);
				Main.dust[dust].noGravity = true;
				Main.dust[dust].velocity *= 0.25f;
			}
		}

		public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (hasBlackMaidSet)
			{
				TriggerMaidDialogue(target);
			}
		}

		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (hasBlackMaidSet)
			{
				TriggerMaidDialogue(target);
			}
		}

		public void SetMood(MaidMood newMood)
		{
			if (currentMood != newMood)
			{
				currentMood = newMood;
				ShowMoodEmote();
			}
		}

		private void ShowMoodEmote()
		{
			if (Main.netMode == NetmodeID.Server || Player.whoAmI != Main.myPlayer) return;

			// ID Emote Bubble Bawaan Terraria
			int emoteID = currentMood switch
			{
				MaidMood.Angry => 88,  // Anger Symbol
				MaidMood.Happy => 87,  // Heart Symbol
				MaidMood.Sleepy => 133, // Zzz Symbol
				MaidMood.Shy => 80,    // Sweat / Embarrassed
				_ => 134               // Musical Note / Calm
			};

			EmoteBubble.NewBubble(emoteID, new WorldUIAnchor(Player), 90);
		}

		private void TriggerMaidDialogue(NPC target)
		{
			if (hasBlackMaidSet && maidTextCooldown <= 0 && Main.rand.NextFloat() < 0.08f)
			{
				string[] maidLines = currentMood switch
				{
					MaidMood.Angry => new string[] { "Okoru yo!", "Baka!", "Mada mada!", "Kurae!" },
					MaidMood.Happy => new string[] { "Master~", "Ureshii!", "Tanoshii!", "Yatta!" },
					MaidMood.Sleepy => new string[] { "Fuwaa..", "Nemu~", "Oyasumi..", "Zzz.." },
					MaidMood.Shy => new string[] { "Ano..", "Hazukashii..", "M-Master?", "Hie.." },
					_ => new string[] { "Hai!", "Daijoubu?", "Gomenne", "Sumimasen", "Hau.." }
				};

				string chosenLine = maidLines[Main.rand.Next(maidLines.Length)];
				Color textColor = currentMood switch
				{
					MaidMood.Angry => new Color(255, 60, 60),
					MaidMood.Happy => new Color(255, 215, 0),
					MaidMood.Sleepy => new Color(135, 206, 250),
					MaidMood.Shy => new Color(218, 112, 214),
					_ => new Color(255, 105, 180)
				};

				int textIndex = CombatText.NewText(target.Hitbox, textColor, chosenLine, dramatic: false);
				if (textIndex >= 0 && textIndex < Main.maxCombatText)
				{
					Main.combatText[textIndex].scale *= 0.45f;
				}

				maidTextCooldown = 60;
			}
		}
	}

	// =======================================================
	// 6. MOD BUFFS (5 MOOD BUFFS)
	// =======================================================
	public class MaidNormalBuff : ModBuff
	{
		public override string Texture => "Terraria/Images/Buff_119";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = true;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
		{
			buffName = "Maid Mood: Normal";
			tip = "Feeling calm and composed\n+5% summon damage & +5% summon critical strike chance";
		}
	}

	public class MaidAngryBuff : ModBuff
	{
		public override string Texture => "Terraria/Images/Buff_115";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = true;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
		{
			buffName = "Maid Mood: Angry";
			tip = "Furious and aggressive!\n+12% summon damage & +8% summon critical strike chance";
		}
	}

	public class MaidHappyBuff : ModBuff
	{
		public override string Texture => "Terraria/Images/Buff_192";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = true;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
		{
			buffName = "Maid Mood: Happy";
			tip = "Overjoyed and full of energy!\n+1 max minion slot & +15% movement speed";
		}
	}

	public class MaidSleepyBuff : ModBuff
	{
		public override string Texture => "Terraria/Images/Buff_3";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = true;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
		{
			buffName = "Maid Mood: Sleepy";
			tip = "Drowsy and taking it easy\n+8 defense & +2 HP/s regen (-10% movement speed)";
		}
	}

	public class MaidShyBuff : ModBuff
	{
		public override string Texture => "Terraria/Images/Buff_10";

		public override void SetStaticDefaults()
		{
			Main.buffNoSave[Type] = true;
			Main.buffNoTimeDisplay[Type] = true;
		}

		public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
		{
			buffName = "Maid Mood: Shy";
			tip = "Timid and agile!\n+8% dodge chance & +10% movement speed (-5% summon damage)";
		}
	}
}