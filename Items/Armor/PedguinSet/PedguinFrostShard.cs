using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.Graphics;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.PedguinSet
{
	public class PedguinFrostShard : ModProjectile
	{
		public override string Texture => "Terraria/Images/Projectile_263";

		private ref float Timer => ref Projectile.ai[0];
		
		private readonly VertexStrip _vertexStrip = new VertexStrip();

		public override void SetStaticDefaults()
		{
			ProjectileID.Sets.TrailCacheLength[Type] = 16;
			ProjectileID.Sets.TrailingMode[Type] = 2;
		}

		public override void SetDefaults()
		{
			Projectile.width = 22;
			Projectile.height = 22;
			Projectile.friendly = true;
			Projectile.hostile = false;
			Projectile.DamageType = DamageClass.Ranged;
			Projectile.penetrate = 1;
			Projectile.timeLeft = 260;
			Projectile.tileCollide = false;
			Projectile.scale = 0.2f;
		}

		public override bool? CanHitNPC(NPC target)
		{
			if (Timer < 22f)
			{
				return false; 
			}
			return null;
		}

		public override void AI()
		{
			Timer++;

			Lighting.AddLight(Projectile.Center, 0.4f, 0.95f, 1.2f);
			Projectile.rotation += 0.65f * Projectile.direction;

			// --- FASE 1: STAR EXPANSION (0 - 14 FRAME) ---
			if (Timer < 14f)
			{
				Projectile.scale = MathHelper.Lerp(Projectile.scale, 1.15f, 0.3f);
				Projectile.velocity *= 0.88f;

				if (Main.rand.NextBool(2))
				{
					Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch, 0f, 0f, 100, default, 1.2f);
					d.noGravity = true;
					d.velocity *= 0.3f;
				}
				return;
			}

			// --- FASE 2: LOCK-ON HOVER & SHOCKWAVE (14 - 22 FRAME) ---
			if (Timer < 22f)
			{
				Projectile.velocity = Vector2.Zero;
				Projectile.scale = MathHelper.Lerp(Projectile.scale, 1.0f, 0.2f);

				float ringAngle = (Timer - 14f) * 0.9f;
				for (int i = 0; i < 3; i++)
				{
					Vector2 offset = Vector2.UnitX.RotatedBy(ringAngle + (i * MathHelper.TwoPi / 3f)) * 18f;
					Dust d = Dust.NewDustDirect(Projectile.Center + offset, 0, 0, DustID.IceTorch, 0f, 0f, 100, default, 1.1f);
					d.noGravity = true;
					d.velocity = Vector2.Zero;
				}

				if (Timer == 18f)
				{
					SoundEngine.PlaySound(SoundID.Item30 with { Pitch = 0.4f, Volume = 0.5f }, Projectile.Center);
				}
				return;
			}

			// --- FASE 3: HOMING BLITZ (22+ FRAME) ---
			NPC target = FindTarget();

			if (target != null)
			{
				Vector2 desiredVelocity = Vector2.Normalize(target.Center - Projectile.Center) * 25f;
				Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.24f);
			}
			else
			{
				if (Projectile.velocity == Vector2.Zero)
				{
					Projectile.velocity = new Vector2(0, -12f);
				}
				Projectile.velocity *= 1.02f;
			}

			if (Main.rand.NextBool(2))
			{
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch, -Projectile.velocity.X * 0.2f, -Projectile.velocity.Y * 0.2f, 100, default, 1.3f);
				d.noGravity = true;
			}
		}

		// =========================================================
		// PRIMITIVE VERTEX SHADER RENDER PASS WITH AFTERIMAGE
		// =========================================================
		public override bool PreDraw(ref Color lightColor)
		{
			SpriteBatch spriteBatch = Main.spriteBatch;
			Texture2D texture = TextureAssets.Projectile[Type].Value;
			Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

			// 1. PRIMITIVE SHADER TRAIL
			if (Timer >= 22f)
			{
				MiscShaderData miscShaderData = GameShaders.Misc["EmpressBlade"];
				miscShaderData.UseOpacity(0.85f);
				miscShaderData.Apply();

				_vertexStrip.PrepareStrip(
					Projectile.oldPos, 
					Projectile.oldRot, 
					StripColorFunction, 
					StripWidthFunction, 
					-Main.screenPosition + Projectile.Size / 2f, 
					Projectile.oldPos.Length
				);

				_vertexStrip.DrawTrail();
			}

			// 2. SPRITE GLOW & AFTERIMAGE AURA
			Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
			
			Matrix matrix = Main.GameViewMatrix.TransformationMatrix;
			spriteBatch.End();
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, matrix);

			Color auraColor = new Color(80, 220, 255, 0) * 0.8f;
			for (int i = 0; i < 4; i++)
			{
				Vector2 offset = Vector2.UnitX.RotatedBy(MathHelper.PiOver2 * i + Projectile.rotation) * 3.5f;
				spriteBatch.Draw(texture, mainDrawPos + offset, null, auraColor, Projectile.rotation, drawOrigin, Projectile.scale * 1.2f, SpriteEffects.None, 0f);
			}

			Color coreHotWhite = new Color(255, 255, 255, 0) * 0.95f;
			spriteBatch.Draw(texture, mainDrawPos, null, coreHotWhite, Projectile.rotation, drawOrigin, Projectile.scale * 1.05f, SpriteEffects.None, 0f);

			spriteBatch.End();
			spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, RasterizerState.CullNone, null, matrix);

			spriteBatch.Draw(texture, mainDrawPos, null, lightColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0f);

			return false;
		}

		private Color StripColorFunction(float progressOnStrip)
		{
			Color colorHead = new Color(180, 255, 255, 220);
			Color colorTail = new Color(0, 110, 255, 0);
			return Color.Lerp(colorHead, colorTail, progressOnStrip);
		}

		private float StripWidthFunction(float progressOnStrip)
		{
			return MathHelper.Lerp(26f, 0f, progressOnStrip);
		}

		private NPC FindTarget()
		{
			NPC closestNPC = null;
			float maxSqrDistance = 950f * 950f;

			for (int i = 0; i < Main.maxNPCs; i++)
			{
				NPC npc = Main.npc[i];
				bool isValid = npc.active && !npc.friendly && (npc.CanBeChasedBy() || npc.type == NPCID.TargetDummy);

				if (isValid)
				{
					float sqrDist = Vector2.DistanceSquared(npc.Center, Projectile.Center);
					if (sqrDist < maxSqrDistance)
					{
						maxSqrDistance = sqrDist;
						closestNPC = npc;
					}
				}
			}
			return closestNPC;
		}

		public override void OnKill(int timeLeft)
		{
			SoundEngine.PlaySound(SoundID.Item27 with { Pitch = 0.2f, Volume = 0.8f }, Projectile.Center);

			// =========================================================
			// AFTERIMAGE / PHANTOM ECHO UPON IMPACT (BAYANGAN BEKU)
			// =========================================================
			for (int i = 0; i < 6; i++)
			{
				Vector2 ghostOffset = Main.rand.NextVector2Circular(12f, 12f);
				Dust ghost = Dust.NewDustDirect(Projectile.position + ghostOffset, Projectile.width, Projectile.height, DustID.IceTorch, 0f, 0f, 100, default, 1.5f);
				ghost.noGravity = true;
				ghost.fadeIn = 1.2f;
			}

			for (int i = 0; i < 16; i++)
			{
				Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.IceTorch, 0f, 0f, 100, default, 1.7f);
				d.velocity *= 2.2f;
				d.noGravity = true;
			}
		}
	}
}