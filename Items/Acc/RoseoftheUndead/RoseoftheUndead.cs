using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic; // Dibutuhkan untuk List<TooltipLine>
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Acc.RoseoftheUndead
{
	// =======================================================
	// 1. MOD ITEM (AKSESORI)
	// =======================================================
	public class RoseoftheUndead : ModItem
	{
		public override string Texture => "TheSanity/Items/Acc/RoseoftheUndead/RoseoftheUndead_item";

		public override void SetDefaults()
		{
			Item.width = 24;
			Item.height = 24;
			Item.accessory = true;
			Item.value = Item.sellPrice(0, 1, 50, 0);
			Item.rare = ItemRarityID.Green;
		}

		// --- KUSTOMISASI TOOLTIP DENGAN ModifyTooltips ---
		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			// Menambahkan deskripsi efek utama
			TooltipLine line1 = new TooltipLine(Mod, "RoseDesc1", "Summons a Cursed Bone Serpent to fight for you");
			
			

			tooltips.Add(line1);
		}

		public override void UpdateAccessory(Player player, bool hideVisual)
		{
			player.AddBuff(BuffID.Bewitched, 2); // Memberi +1 Minion slot
			player.GetModPlayer<RoseoftheUndeadPlayer>().hasRose = true;

			if (player.ownedProjectileCounts[ModContent.ProjectileType<RoseoftheUndeadMinion>()] < 1)
			{
				if (player.whoAmI == Main.myPlayer)
				{
					Projectile.NewProjectile(
						player.GetSource_Accessory(Item), 
						player.Center, 
						Vector2.Zero, 
						ModContent.ProjectileType<RoseoftheUndeadMinion>(), 
						22, // Base Damage
						2.0f, 
						player.whoAmI
					);
				}
			}
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ItemID.Bone, 15)
				.AddIngredient(ItemID.JungleRose, 1)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}

	// =======================================================
	// 2. MOD PLAYER
	// =======================================================
	public class RoseoftheUndeadPlayer : ModPlayer
	{
		public bool hasRose;

		public override void ResetEffects()
		{
			hasRose = false;
		}
	}

	// =======================================================
	// 3. MOD PROJECTILE (CURSED BONE SERPENT MINION)
	// =======================================================
	public class RoseoftheUndeadMinion : ModProjectile
	{
		public override string Texture => "Terraria/Images/NPC_39"; 

		public override void SetStaticDefaults()
		{
			Main.projFrames[Projectile.type] = 1;
			
			ProjectileID.Sets.MinionTargettingFeature[Projectile.type] = true; 
			ProjectileID.Sets.MinionSacrificable[Projectile.type] = true;
			ProjectileID.Sets.CultistIsResistantTo[Projectile.type] = true;

			ProjectileID.Sets.TrailCacheLength[Projectile.type] = 32; 
			ProjectileID.Sets.TrailingMode[Projectile.type] = 2; 
		}

		public override void SetDefaults()
		{
			Projectile.width = 24;
			Projectile.height = 24;
			Projectile.tileCollide = false;
			Projectile.friendly = true; 
			Projectile.hostile = false;
			Projectile.minion = true;
			Projectile.DamageType = DamageClass.Summon;
			Projectile.minionSlots = 0f; 
			Projectile.penetrate = -1; 
			Projectile.timeLeft = 18000;
			
			Projectile.usesLocalNPCImmunity = true;
			Projectile.localNPCHitCooldown = 9; 
			Projectile.scale = 0.85f; 
		}

		// --- MEKANIK UNIK: DEBUFF & PARTIKEL SAAT HIT ---
		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			// Memberikan efek terbakar api hijau (Cursed Inferno) selama 3.5 detik
			target.AddBuff(BuffID.CursedInferno, 210);

			// Partikel ledakan api hijau saat menabrak musuh
			for (int i = 0; i < 8; i++)
			{
				Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.CursedTorch, 0f, 0f, 100, default, 1.5f);
				d.velocity *= 2.5f;
				d.noGravity = true;
			}
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (target.type == NPCID.TargetDummy)
				return true;

			if (!target.friendly && target.CanBeChasedBy())
				return true;

			return null;
		}

		public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
		{
			if (projHitbox.Intersects(targetHitbox))
				return true;

			int step = 3;
			for (int i = 0; i < Projectile.oldPos.Length; i += step)
			{
				Vector2 segmentPos = Projectile.oldPos[i];
				if (segmentPos == Vector2.Zero) continue;

				Rectangle segmentHitbox = new Rectangle((int)segmentPos.X, (int)segmentPos.Y, Projectile.width, Projectile.height);
				if (segmentHitbox.Intersects(targetHitbox))
				{
					return true;
				}
			}

			return null;
		}

		public override void AI()
		{
			Player player = Main.player[Projectile.owner];

			if (!player.active || player.dead || !player.GetModPlayer<RoseoftheUndeadPlayer>().hasRose)
			{
				Projectile.Kill();
				return;
			}

			Projectile.timeLeft = 2;

			// --- MEKANIK UNIK: PANCARAN CAHAYA & JEJAK DUST ---
			Lighting.AddLight(Projectile.Center, 0.2f, 0.8f, 0.2f); // Cahaya hijau melayang

			if (Main.rand.NextBool(3)) // Menimbulkan partikel api hijau melayang saat terbang
			{
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.CursedTorch, 0f, 0f, 150, default, 1.1f);
				d.velocity *= 0.3f;
				d.noGravity = true;
			}

			// Deteksi Target
			NPC target = null;
			float maxDetectRadius = 650f;

			if (player.HasMinionAttackTargetNPC)
			{
				NPC npc = Main.npc[player.MinionAttackTargetNPC];
				if (IsValidTarget(npc) && Vector2.Distance(npc.Center, Projectile.Center) < maxDetectRadius * 1.5f)
				{
					target = npc;
				}
			}

			if (target == null)
			{
				foreach (NPC npc in Main.npc)
				{
					if (IsValidTarget(npc))
					{
						float sqrDistanceToTarget = Vector2.DistanceSquared(npc.Center, Projectile.Center);
						if (sqrDistanceToTarget < maxDetectRadius * maxDetectRadius)
						{
							target = npc;
							maxDetectRadius = (float)Math.Sqrt(sqrDistanceToTarget);
						}
					}
				}
			}

			// Pergerakan AI
			if (target != null)
			{
				float distToTarget = Vector2.Distance(Projectile.Center, target.Center);

				if (distToTarget < 40f)
				{
					if (Projectile.velocity.Length() < 13f)
					{
						Projectile.velocity = Vector2.Normalize(Projectile.velocity == Vector2.Zero ? Vector2.UnitX : Projectile.velocity) * 13f;
					}
				}
				else
				{
					float attackSpeed = 16f;
					float turnInertia = 11f;

					Vector2 targetDir = Vector2.Normalize(target.Center - Projectile.Center) * attackSpeed;
					Projectile.velocity = (Projectile.velocity * (turnInertia - 1f) + targetDir) / turnInertia;
				}
			}
			else
			{
				// Mode Idle Orbit
				double time = Main.GameUpdateCount * 0.04;
				Vector2 orbitOffset = new Vector2((float)Math.Cos(time) * 95f, (float)Math.Sin(time * 0.8) * 45f - 50f);
				Vector2 idlePos = player.Center + orbitOffset;

				Vector2 toIdle = idlePos - Projectile.Center;
				float distToIdle = toIdle.Length();

				if (distToIdle > 850f)
				{
					Projectile.Center = player.Center;
					Projectile.velocity = Vector2.Zero;
				}
				else
				{
					float idleSpeed = MathHelper.Clamp(distToIdle * 0.08f, 6f, 13f);
					toIdle.Normalize();
					Projectile.velocity = (Projectile.velocity * 15f + toIdle * idleSpeed) / 16f;
				}
			}

			if (Projectile.velocity != Vector2.Zero)
			{
				Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
			}
		}

		private bool IsValidTarget(NPC npc)
		{
			return npc.active 
				&& !npc.friendly 
				&& npc.lifeMax > 5 
				&& !npc.dontTakeDamage 
				&& !npc.immortal 
				|| (npc.active && npc.type == NPCID.TargetDummy);
		}

		// --- MEKANIK UNIK: RENDER WARNA HIJAU TERKUTUK (CURSED TINT) ---
		public override bool PreDraw(ref Color lightColor)
		{
			Texture2D headTex = ModContent.Request<Texture2D>("Terraria/Images/NPC_39").Value;
			Texture2D bodyTex = ModContent.Request<Texture2D>("Terraria/Images/NPC_40").Value;
			Texture2D tailTex = ModContent.Request<Texture2D>("Terraria/Images/NPC_41").Value;

			int step = 3;
			int totalSegments = Projectile.oldPos.Length / step;

			for (int i = totalSegments - 1; i >= 0; i--)
			{
				int index = i * step;
				Vector2 pos = Projectile.oldPos[index];

				if (pos == Vector2.Zero)
					pos = Projectile.position;

				Vector2 drawPos = pos + Projectile.Size / 2f - Main.screenPosition;
				float rotation = Projectile.oldRot[index];

				if (pos == Projectile.position)
					rotation = Projectile.rotation;

				// PERUBAHAN VISUAL: Menggabungkan warna pencahayaan dengan warna hijau terang (Cursed Green)
				Color baseColor = Lighting.GetColor((int)(pos.X + Projectile.width / 2f) / 16, (int)(pos.Y + Projectile.height / 2f) / 16);
				Color cursedColor = Color.Lerp(baseColor, new Color(100, 255, 100), 0.55f); // Warna Tint Hijau Unik

				Texture2D texture;
				if (i == 0)
					texture = headTex;
				else if (i == totalSegments - 1)
					texture = tailTex;
				else
					texture = bodyTex;

				Vector2 origin = texture.Size() / 2f;

				Main.EntitySpriteDraw(
					texture,
					drawPos,
					null,
					cursedColor, // Menggunakan warna kustom
					rotation,
					origin,
					Projectile.scale,
					SpriteEffects.None,
					0
				);
			}

			return false;
		}
	}
} 