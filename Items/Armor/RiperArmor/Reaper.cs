using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace TheSanity.NPCs
{
	public class Reaper : ModNPC
	{
		// Reference the vanilla Reaper sprite directly (internal NPC ID 253)
		// instead of shipping a custom texture.
		public override string Texture => "Terraria/Images/NPC_" + NPCID.Reaper;

		// ai[0] = whoAmI of the owning player (set by RiperPlayer when spawning)
		// ai[2] = generic timer, reused for windup/cooldown depending on ai[3]
		// ai[3] = attack state: 0 = approaching/idle, 1 = winding up, 2 = cooldown
		// localAI[0] = chosen sickle throw variant for the current attack: 0 = standard, 1 = fan, 2 = charged
		// localAI[1] = idle float/bob timer (purely cosmetic, doesn't need to sync)
		private int OwnerIndex => (int)NPC.ai[0];

		private const float FollowDistance = 60f;
		private const float AggroRange = 700f;   // hunts from much farther away
		private const float ThrowRange = 220f;   // pure ranged attacker now, so it engages from farther out
		private const int AttackCooldown = 20;
		private const float ChaseSpeed = 18f;

		// Standard throw - single sickle, balanced stats
		private const int StandardWindup = 10;
		private const float StandardSpeed = 17f;
		private const int StandardDamage = 60;

		// Fan throw - 3 sickles in a spread, faster but weaker individually
		private const int FanWindup = 8;
		private const float FanSpeed = 18f;
		private const int FanDamage = 32;
		private const float FanSpreadDegrees = 14f;

		// Charged throw - single big empowered sickle, slow windup but hits hard
		private const int ChargedWindup = 24;
		private const float ChargedSpeed = 11f;
		private const int ChargedDamage = 120;

		// Death burst - fired once when the Reaper is removed/killed
		private const int DeathBurstCount = 6;
		private const float DeathBurstDamage = 60f;
		private const float DeathBurstSpeed = 14f;
		private bool hasSpawnedDeathBurst = false;

		public override void SetStaticDefaults()
		{
			// Match the vanilla Reaper's real frame count so the sprite sheet
			// is sliced correctly (fixes the "stacked sprite" bug).
			Main.npcFrameCount[NPC.type] = Main.npcFrameCount[NPCID.Reaper];
			NPCID.Sets.CantTakeLunchMoney[NPC.type] = true;
		}

		public override void SetDefaults()
		{
			NPC.width = 24;
			NPC.height = 24;
			NPC.damage = 0;   // damage comes from attacks below, not contact
			NPC.defense = 20;
			NPC.lifeMax = 1000;
			NPC.knockBackResist = 0f;
			NPC.friendly = true;
			NPC.dontTakeDamage = false;
			NPC.noGravity = true;
			NPC.noTileCollide = true;
			NPC.aiStyle = -1;
			NPC.HitSound = SoundID.NPCHit1;
			NPC.DeathSound = SoundID.NPCDeath1;
			AnimationType = NPCID.Reaper;
		}

		public override void AI()
		{
			if (OwnerIndex < 0 || OwnerIndex >= Main.maxPlayers)
			{
				NPC.active = false;
				return;
			}

			Player owner = Main.player[OwnerIndex];
			if (!owner.active || owner.dead)
			{
				NPC.active = false;
				return;
			}

			NPC target = FindTarget(owner);

			if (target != null)
			{
				AttackTarget(target);
			}
			else
			{
				NPC.ai[3] = 0;
				NPC.ai[2] = 0;
				NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.2f);
				FollowOwner(owner);
			}

			NPC.spriteDirection = NPC.velocity.X >= 0 ? 1 : -1;

			EmitAmbientEffects();
			AnimateFrames();
		}

		private NPC FindTarget(Player owner)
		{
			float closestDist = AggroRange;
			NPC closest = null;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC potential = Main.npc[i];
				if (!potential.active || potential.friendly || potential.dontTakeDamage || potential.immortal)
					continue;
				if (potential.lifeMax <= 5 && potential.type != NPCID.TargetDummy)
					continue;

				float dist = Vector2.Distance(owner.Center, potential.Center);
				if (dist < closestDist)
				{
					closestDist = dist;
					closest = potential;
				}
			}

			return closest;
		}

		private void FollowOwner(Player owner)
		{
			// Continuous multi-frequency drift so the hover point is always moving -
			// the Reaper is perpetually chasing a moving point instead of settling
			// into a fixed spot and going still.
			NPC.localAI[1] += 0.045f;
			float driftX = (float)System.Math.Sin(NPC.localAI[1] * 0.7f) * 34f
				+ (float)System.Math.Sin(NPC.localAI[1] * 1.9f) * 14f;
			float driftY = (float)System.Math.Sin(NPC.localAI[1] * 0.5f) * 20f
				+ (float)System.Math.Cos(NPC.localAI[1] * 1.3f) * 10f;

			Vector2 hoverPoint = owner.Center + new Vector2(-owner.direction * FollowDistance + driftX, -40f + driftY);
			Vector2 direction = hoverPoint - NPC.Center;
			float distance = direction.Length();

			if (distance > 600f)
			{
				NPC.position = owner.Center;
				return;
			}

			direction.Normalize();
			// Minimum speed floor of 3f means it never fully stops, even once it
			// catches up to the (constantly drifting) hover point.
			float speed = MathHelper.Clamp(distance / 15f, 3f, 16f);
			NPC.velocity = Vector2.Lerp(NPC.velocity, direction * speed, 0.12f);
		}

		private void AttackTarget(NPC target)
		{
			Vector2 toTarget = target.Center - NPC.Center;
			float distance = toTarget.Length();

			if (distance > ThrowRange)
			{
				NPC.ai[3] = 0;
				NPC.ai[2] = 0;
				NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.2f);

				toTarget.Normalize();
				NPC.velocity = Vector2.Lerp(NPC.velocity, toTarget * ChaseSpeed, 0.25f);
				return;
			}

			NPC.velocity *= 0.93f;
			int state = (int)NPC.ai[3];

			switch (state)
			{
				case 0: // pick a sickle variant and begin windup
					NPC.ai[3] = 1;
					NPC.ai[2] = 0;
					// 0 = standard (50%), 1 = fan (30%), 2 = charged (20%)
					int roll = Main.rand.Next(10);
					NPC.localAI[0] = roll < 5 ? 0 : (roll < 8 ? 1 : 2);
					break;

				case 1: // winding up
					NPC.ai[2]++;
					int variant = (int)NPC.localAI[0];
					int windup = variant switch
					{
						1 => FanWindup,
						2 => ChargedWindup,
						_ => StandardWindup,
					};
					float progress = NPC.ai[2] / (float)windup;
					NPC.rotation = MathHelper.Lerp(-1.3f, 1.3f, progress) * NPC.spriteDirection;

					// Telegraph dust while winding up - charged throws get a bigger tell
					if (Main.rand.NextBool(variant == 2 ? 1 : 2))
					{
						Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, 0f, 0f, 100, default, variant == 2 ? 1.6f : 1.2f);
					}

					if (NPC.ai[2] >= windup)
					{
						if (Main.netMode != NetmodeID.MultiplayerClient)
							ThrowSickle(target, variant);

						NPC.ai[3] = 2;
						NPC.ai[2] = 0;
					}
					break;

				case 2: // cooldown
					NPC.ai[2]++;
					NPC.rotation = MathHelper.Lerp(NPC.rotation, 0f, 0.2f);

					if (NPC.ai[2] >= AttackCooldown)
					{
						NPC.ai[3] = 0;
						NPC.ai[2] = 0;
					}
					break;
			}
		}

		private void ThrowSickle(NPC target, int variant)
		{
			Vector2 baseDirection = target.Center - NPC.Center;
			if (baseDirection == Vector2.Zero)
				baseDirection = new Vector2(NPC.spriteDirection, 0f);
			baseDirection.Normalize();

			switch (variant)
			{
				case 1: // Fan throw - 3 sickles in a spread
					float spreadRad = MathHelper.ToRadians(FanSpreadDegrees);
					Vector2[] directions =
					{
						baseDirection.RotatedBy(-spreadRad),
						baseDirection,
						baseDirection.RotatedBy(spreadRad),
					};

					Vector2 perpendicular = new Vector2(-baseDirection.Y, baseDirection.X);

					for (int i = 0; i < directions.Length; i++)
					{
						// Offset -1, 0, +1 slots wide so the 3 sickles don't spawn stacked on the same pixel
						Vector2 spawnPos = NPC.Center + perpendicular * ((i - 1) * 14f);

						Projectile.NewProjectile(
							NPC.GetSource_FromAI(),
							spawnPos,
							directions[i] * FanSpeed,
							ModContent.ProjectileType<DeathSickleProjectile>(),
							FanDamage,
							1.5f,
							OwnerIndex,
							ai0: 0f,
							ai1: NPC.whoAmI
						);
					}
					break;

				case 2: // Charged throw - single big empowered sickle
					int chargedIndex = Projectile.NewProjectile(
						NPC.GetSource_FromAI(),
						NPC.Center,
						baseDirection * ChargedSpeed,
						ModContent.ProjectileType<DeathSickleProjectile>(),
						ChargedDamage,
						6f,
						OwnerIndex,
						ai0: 0f,
						ai1: NPC.whoAmI
					);

					if (chargedIndex >= 0 && chargedIndex < Main.maxProjectiles)
					{
						Main.projectile[chargedIndex].scale = 1.6f;
						Main.projectile[chargedIndex].netUpdate = true;
					}
					break;

				default: // Standard single throw
					Projectile.NewProjectile(
						NPC.GetSource_FromAI(),
						NPC.Center,
						baseDirection * StandardSpeed,
						ModContent.ProjectileType<DeathSickleProjectile>(),
						StandardDamage,
						2f,
						OwnerIndex,
						ai0: 0f,
						ai1: NPC.whoAmI
					);
					break;
			}
		}

		private void EmitAmbientEffects()
		{
			// Soft purple glow so it reads clearly at night / in dark biomes
			Lighting.AddLight(NPC.Center, 0.55f, 0.15f, 0.75f);

			// Light trailing dust while moving fast, sells the "flying wraith" feel
			if (NPC.velocity.LengthSquared() > 16f && Main.rand.NextBool(4))
			{
				Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, -NPC.velocity.X * 0.2f, -NPC.velocity.Y * 0.2f, 150, default, 0.9f);
			}
		}

		public override void OnKill()
		{
			SpawnDeathBurst();
		}

		/// <summary>
		/// Fires 6 Death Sickles outward in a full circle (60 degree gaps between each),
		/// exactly once. Safe to call from multiple removal paths (real death, despawn).
		/// </summary>
		public void SpawnDeathBurst()
		{
			if (hasSpawnedDeathBurst)
				return;

			hasSpawnedDeathBurst = true;

			// Always play the visual burst locally, but only the server spawns
			// the actual damaging projectiles.
			for (int i = 0; i < 24; i++)
			{
				Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch, 0f, 0f, 60, default, 1.8f);
			}

			if (Main.netMode == NetmodeID.MultiplayerClient)
				return;

			for (int i = 0; i < DeathBurstCount; i++)
			{
				float angle = MathHelper.ToRadians(i * (360f / DeathBurstCount));
				Vector2 direction = new Vector2((float)System.Math.Cos(angle), (float)System.Math.Sin(angle));

				Projectile.NewProjectile(
					NPC.GetSource_Death(),
					NPC.Center,
					direction * DeathBurstSpeed,
					ModContent.ProjectileType<DeathSickleProjectile>(),
					(int)DeathBurstDamage,
					3f,
					OwnerIndex,
					ai0: 0f,
					ai1: -1f // no Reaper left to boomerang back to - these just fly out and fade
				);
			}
		}

		private void AnimateFrames()
		{
			int frameCount = Main.npcFrameCount[NPC.type];
			if (frameCount <= 1)
				return;

			int frameHeight = TextureAssets.Npc[NPC.type].Value.Height / frameCount;

			NPC.frameCounter++;
			if (NPC.frameCounter >= 5)
			{
				NPC.frameCounter = 0;
				NPC.frame.Y += frameHeight;

				if (NPC.frame.Y >= frameHeight * frameCount)
					NPC.frame.Y = 0;
			}
		}

		public override bool CheckActive()
		{
			return false;
		}

		public override void HitEffect(NPC.HitInfo hit)
		{
			for (int i = 0; i < 5; i++)
			{
				Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.PurpleTorch);
			}
		}
	}
}