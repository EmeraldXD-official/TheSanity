using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.NPCs;

namespace TheSanity.Projectiles
{
	public class DeathSickleProjectile : ModProjectile
	{
		// Reuse the vanilla Death Sickle projectile sprite directly.
		public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DeathSickle;

		// ai[0] = flight phase: 0 = outbound, 1 = returning to the Reaper
		// ai[1] = whoAmI of the Reaper NPC that threw it (set at spawn)
		private const int OutboundLifetime = 28; // ticks spent flying outward before curving back (faster cycle)
		private const float HomingStrength = 0.08f;
		private const float HomingRange = 300f;
		private const float ReturnHomingStrength = 0.2f;

		public override void SetStaticDefaults()
		{
			Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.DeathSickle];
		}

		public override void SetDefaults()
		{
			Projectile.width = 32;
			Projectile.height = 32;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.tileCollide = false;
			Projectile.penetrate = 4;
			Projectile.timeLeft = 90;
			Projectile.ignoreWater = true;
			Projectile.light = 0.2f;
			Projectile.alpha = 0;
			Projectile.extraUpdates = 1;
		}

		public override void AI()
		{
			Projectile.rotation += 0.4f * Projectile.direction;

			bool returning = Projectile.ai[0] == 1f;
			bool isFanVariant = Projectile.knockBack < 1.6f; // Fan Throw sickles are spawned with 1.5f knockback

			if (!returning)
			{
				// Fan Throw sickles skip homing entirely - if all 3 curved onto the
				// same enemy they'd converge and visually overlap into what looks
				// like a single sickle. Standard/Charged are solo throws, so homing
				// on those is safe and just makes sure the single sickle connects.
				if (!isFanVariant)
					HomeOntoNearestEnemy();

				// Charged (bigger, scale > 1.2) sickles fly out further before curving back
				int outboundLifetime = Projectile.scale > 1.2f ? OutboundLifetime + 25 : OutboundLifetime;

				if (Projectile.timeLeft <= 90 - outboundLifetime)
				{
					Projectile.ai[0] = 1f;
					Projectile.penetrate = Projectile.scale > 1.2f ? 6 : 4; // refresh pierce count for the return trip
				}
			}
			else
			{
				ReturnToReaper();
			}

			if (Main.rand.NextBool(3))
			{
				Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.PurpleTorch, 0f, 0f, 100, default, 0.8f);
			}
		}

		private void HomeOntoNearestEnemy()
		{
			NPC closestTarget = FindClosestEnemy();
			if (closestTarget == null)
				return;

			Vector2 desiredDirection = closestTarget.Center - Projectile.Center;
			desiredDirection.Normalize();

			Vector2 currentDirection = Projectile.velocity;
			float speed = currentDirection.Length();
			currentDirection.Normalize();

			Vector2 newDirection = Vector2.Lerp(currentDirection, desiredDirection, HomingStrength);
			newDirection.Normalize();

			Projectile.velocity = newDirection * speed;
		}

		private void ReturnToReaper()
		{
			int reaperIndex = (int)Projectile.ai[1];
			Vector2 returnTarget;

			bool reaperAlive = reaperIndex >= 0
				&& reaperIndex < Main.maxNPCs
				&& Main.npc[reaperIndex].active
				&& Main.npc[reaperIndex].type == ModContent.NPCType<Reaper>();

			if (reaperAlive)
			{
				returnTarget = Main.npc[reaperIndex].Center;
			}
			else
			{
				// Reaper is gone (unequipped/despawned) - just keep flying off and fade out
				Projectile.timeLeft = System.Math.Min(Projectile.timeLeft, 10);
				return;
			}

			Vector2 direction = returnTarget - Projectile.Center;
			float distance = direction.Length();

			if (distance < 30f)
			{
				Projectile.Kill();
				return;
			}

			direction.Normalize();
			float speed = Projectile.velocity.Length();
			Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * speed, ReturnHomingStrength);
		}

		private NPC FindClosestEnemy()
		{
			float closestDist = HomingRange;
			NPC closest = null;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC potential = Main.npc[i];
				if (!potential.active || potential.friendly || potential.dontTakeDamage || potential.immortal)
					continue;
				if (potential.lifeMax <= 5 && potential.type != NPCID.TargetDummy)
					continue;

				float dist = Vector2.Distance(Projectile.Center, potential.Center);
				if (dist < closestDist)
				{
					closestDist = dist;
					closest = potential;
				}
			}

			return closest;
		}

		public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
		{
			for (int i = 0; i < 8; i++)
			{
				Dust.NewDust(target.position, target.width, target.height, DustID.PurpleTorch);
			}
		}
	}
}