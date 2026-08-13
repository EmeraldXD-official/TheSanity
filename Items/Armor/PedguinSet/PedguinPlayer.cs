using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.PedguinSet
{
	public class PedguinPlayer : ModPlayer
	{
		public bool hasAbsoluteZeroSet;
		public int hitCharge = 0;
		public const int MaxHitCharge = 8;
		public int burstCooldown = 0; 

		public override void ResetEffects()
		{
			hasAbsoluteZeroSet = false;
		}

		// =========================================================
		// EFEK AFTERIMAGE / BAYANGAN GERAK PADA KARAKTER PLAYER
		// =========================================================
		public override void FrameEffects()
		{
			if (hasAbsoluteZeroSet)
			{
				// Mengaktifkan bayangan jejak gerak di belakang player
				Player.armorEffectDrawShadow = true;

				// Saat Hit Bar penuh (8/8), tambahkan lapisan bayangan ganda yang pekat
				if (hitCharge >= MaxHitCharge)
				{
					Player.armorEffectDrawShadowSubtle = true;
				}
			}
		}

		public override void PostUpdate()
		{
			if (burstCooldown > 0)
			{
				burstCooldown--;
			}

			// Aura partikel di karakter saat bar terisi 8/8
			if (hasAbsoluteZeroSet && hitCharge >= MaxHitCharge && Main.rand.NextBool(3))
			{
				Vector2 dustPos = Player.position + new Vector2(Main.rand.NextFloat(Player.width), Main.rand.NextFloat(Player.height));
				Dust d = Dust.NewDustDirect(dustPos, 0, 0, DustID.IceTorch, 0f, -1.5f, 100, default, 1.2f);
				d.noGravity = true;
				d.velocity *= 0.3f;
			}
		}

		public override void OnHurt(Player.HurtInfo info)
		{
			if (hasAbsoluteZeroSet)
			{
				Player.AddBuff(BuffID.RapidHealing, 120);
			}
		}

		public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
		{
			if (hasAbsoluteZeroSet && proj.CountsAsClass(DamageClass.Ranged) && proj.type != ModContent.ProjectileType<PedguinFrostShard>())
			{
				target.AddBuff(BuffID.Frostburn2, 180);

				if (burstCooldown <= 0)
				{
					if (hitCharge >= MaxHitCharge)
					{
						hitCharge = 0;
						burstCooldown = 45;

						SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.2f, Volume = 0.9f }, target.Center);

						// Kilatan Cahaya Cyan di Titik Target
						Lighting.AddLight(target.Center, 0.8f, 2.0f, 2.5f);

						// Gelombang Kejut Ring 360 Derajat
						int ringDustCount = 36;
						for (int i = 0; i < ringDustCount; i++)
						{
							float angle = MathHelper.TwoPi / ringDustCount * i;
							Vector2 ringVel = angle.ToRotationVector2() * 9f;

							Dust ringDust = Dust.NewDustDirect(target.Center, 0, 0, DustID.IceTorch, ringVel.X, ringVel.Y, 100, default, 1.8f);
							ringDust.noGravity = true;
							ringDust.fadeIn = 1.3f;

							Dust ringDust2 = Dust.NewDustDirect(target.Center, 0, 0, DustID.IceTorch, ringVel.X * 1.5f, ringVel.Y * 1.5f, 100, default, 1.2f);
							ringDust2.noGravity = true;
						}

						if (Player.whoAmI == Main.myPlayer)
						{
							int shardDamage = (int)(hit.Damage * 0.35f);
							if (shardDamage < 1) shardDamage = 1;

							int numShards = 8;
							float randomAngleOffset = Main.rand.NextFloat(0f, MathHelper.TwoPi);

							for (int i = 0; i < numShards; i++)
							{
								float angle = (MathHelper.TwoPi / numShards * i) + randomAngleOffset;
								Vector2 dir = Vector2.UnitX.RotatedBy(angle);

								Vector2 spawnPos = target.Center;
								Vector2 vel = dir * 16f;
								
								Projectile.NewProjectile(
									Player.GetSource_OnHit(target),
									spawnPos,
									vel,
									ModContent.ProjectileType<PedguinFrostShard>(),
									shardDamage,
									2f,
									Player.whoAmI
								);
							}

							for (int i = 0; i < 22; i++)
							{
								Vector2 dustVel = Main.rand.NextVector2Circular(8f, 8f);
								Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.IceTorch, dustVel.X, dustVel.Y, 100, default, 1.8f);
								d.noGravity = true;
							}
						}
					}
					else
					{
						hitCharge++;
						SoundEngine.PlaySound(SoundID.Item30 with { Pitch = 0.5f + (hitCharge * 0.08f), Volume = 0.25f }, Player.Center);
					}
				}

				if (target.life <= 0)
				{
					if (Player.whoAmI == Main.myPlayer)
					{
						SoundEngine.PlaySound(SoundID.Item101 with { Volume = 0.6f }, target.Center);

						int novaDamage = (int)(hit.Damage * 0.25f);
						if (novaDamage < 1) novaDamage = 1;

						int novaShards = 6;
						float randomNovaOffset = Main.rand.NextFloat(0f, MathHelper.TwoPi);

						for (int i = 0; i < novaShards; i++)
						{
							Vector2 vel = Vector2.UnitX.RotatedBy((MathHelper.TwoPi / novaShards * i) + randomNovaOffset) * 12f;
							
							Projectile.NewProjectile(
								Player.GetSource_OnHit(target),
								target.Center,
								vel,
								ModContent.ProjectileType<PedguinFrostShard>(),
								novaDamage,
								2.5f,
								Player.whoAmI
							);
						}
					}
				}
			}
		}
	}
}