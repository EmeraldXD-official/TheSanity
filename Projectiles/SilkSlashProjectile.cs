using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria.Audio;
using Terraria.GameContent;

namespace TheSanity.Projectiles
{
    public class SilkSlashProjectile : ModProjectile
    {
        // Config (compile-time constants)
        private const float HOMING_RANGE = 400f;
        private const float HOMING_STRENGTH = 0.12f; // lerp factor toward desired magnitude
        private const int HOMING_DELAY_TICKS = 6; // ticks before homing begins
        private const float MAX_TURN_DEGREES = 8f; // degrees per tick (compile-time constant)

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 5;
            Projectile.timeLeft = 600;
            Projectile.light = 0.6f;
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
            Projectile.alpha = 80;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            // Increment homing delay counter stored in ai[0]
            if (Projectile.ai[0] < HOMING_DELAY_TICKS)
            {
                Projectile.ai[0]++;
            }
            else
            {
                DoSmoothHoming();
            }

            // Rotation and subtle pulse
            Projectile.rotation += 0.28f * Projectile.direction;
            float pulse = 0.06f * (float)Math.Sin(Main.GameUpdateCount / 6f);
            Projectile.scale = 1f + pulse;

            // Lighting
            Lighting.AddLight(Projectile.Center, 0.18f, 0.18f, 0.28f);

            // Primary dust trail (silk)
            if (Main.rand.NextBool(2))
            {
                Vector2 dustPos = Projectile.Center - Projectile.velocity * 0.5f;
                Dust d = Dust.NewDustDirect(dustPos - new Vector2(6, 6), 12, 12, DustID.Silk, Projectile.velocity.X * -0.08f, Projectile.velocity.Y * -0.08f, 100, new Color(200, 220, 255), 1.05f);
                d.noGravity = true;
                d.velocity *= 0.18f;
            }

            // Sapphire sparkles
            if (Main.rand.NextBool(6))
            {
                Dust d2 = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GemSapphire, 0f, 0f, 100, default, 0.9f);
                d2.noGravity = true;
                d2.velocity *= 0.08f;
            }

            // Faint afterimage dust
            if (Main.rand.NextBool(10))
            {
                int idx = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.WhiteTorch, 0f, 0f, 150, new Color(220, 220, 255), 0.7f);
                Main.dust[idx].noGravity = true;
                Main.dust[idx].velocity *= 0.15f;
            }

            // Homing pulse visuals when active
            if (Projectile.ai[0] >= HOMING_DELAY_TICKS && Main.rand.NextBool(8))
            {
                Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                Vector2 pulsePos = Projectile.Center - dir * 6f;
                Dust p = Dust.NewDustDirect(pulsePos - Vector2.One * 4f, 8, 8, DustID.Frost, 0f, 0f, 120, new Color(180, 210, 255), 1.15f);
                p.noGravity = true;
                p.velocity = -dir * 0.6f + Main.rand.NextVector2Circular(0.4f, 0.4f);
            }
        }

        private void DoSmoothHoming()
        {
            NPC target = null;
            float closest = HOMING_RANGE;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy(this) && !npc.friendly && npc.active && npc.lifeMax > 5)
                {
                    float dist = Vector2.Distance(npc.Center, Projectile.Center);
                    if (dist < closest)
                    {
                        closest = dist;
                        target = npc;
                    }
                }
            }

            if (target == null) return;

            // Compute desired velocity toward target with same speed magnitude
            float speed = Projectile.velocity.Length();
            if (speed < 0.1f) speed = 10f; // fallback
            Vector2 desired = Vector2.Normalize(target.Center - Projectile.Center) * speed;

            // Angle-based smooth turning (clamp per-tick turn)
            float currentAngle = Projectile.velocity.ToRotation();
            float desiredAngle = desired.ToRotation();
            float delta = MathHelper.WrapAngle(desiredAngle - currentAngle);

            float maxTurnRadians = MathHelper.ToRadians(MAX_TURN_DEGREES);
            delta = MathHelper.Clamp(delta, -maxTurnRadians, maxTurnRadians);
            float newAngle = currentAngle + delta;

            // Smoothly lerp magnitude toward desired magnitude
            float newSpeed = MathHelper.Lerp(speed, desired.Length(), HOMING_STRENGTH);

            Projectile.velocity = new Vector2((float)Math.Cos(newAngle), (float)Math.Sin(newAngle)) * newSpeed;

            // Subtle homing light beam toward target (short-lived)
            if (Main.rand.NextBool(12))
            {
                Vector2 beamDir = Vector2.Normalize(target.Center - Projectile.Center);
                Vector2 beamPos = Projectile.Center + beamDir * 6f;
                Dust b = Dust.NewDustDirect(beamPos - Vector2.One * 4f, 8, 8, DustID.GemSapphire, beamDir.X * 0.2f, beamDir.Y * 0.2f, 140, default, 1.1f);
                b.noGravity = true;
                b.velocity *= 0.25f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Confused, 120);
            target.AddBuff(BuffID.Slow, 120);
            target.velocity *= 0.6f;

            for (int i = 0; i < 12; i++)
            {
                Vector2 pos = target.Center + Main.rand.NextVector2Circular(18, 18);
                Dust d = Dust.NewDustDirect(pos, 0, 0, DustID.Silk, Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-2f, 2f), 150, new Color(200, 220, 255), 1.1f);
                d.noGravity = true;
            }

            SoundEngine.PlaySound(SoundID.Item8, target.Center);

            // Hit burst of sapphire shards
            for (int i = 0; i < 6; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(2f, 2f);
                Dust d2 = Dust.NewDustDirect(target.Center + vel * 2f - Vector2.One * 4f, 8, 8, DustID.GemSapphire, vel.X, vel.Y, 150, default, 1.2f);
                d2.noGravity = true;
                d2.velocity *= 0.6f;
            }
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 18; i++)
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.WhiteTorch, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 150, default, 1.4f);
                d.noGravity = true;
            }

            // Ring of sapphire dust outward
            for (int i = 0; i < 12; i++)
            {
                float angle = MathHelper.TwoPi * i / 12f;
                Vector2 vel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * Main.rand.NextFloat(1.4f, 3.2f);
                Dust d2 = Dust.NewDustDirect(Projectile.Center - Vector2.One * 4f, 8, 8, DustID.GemSapphire, vel.X, vel.Y, 150, default, 1.2f);
                d2.noGravity = true;
            }
        }

        // Soft afterimage trail and glow
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = tex.Size() * 0.5f;

            // Draw old positions (fading)
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                float t = i / (float)Projectile.oldPos.Length;
                Vector2 pos = Projectile.oldPos[i] - Main.screenPosition + origin + new Vector2(0f, Projectile.gfxOffY);
                float scale = Projectile.scale * (1f - t * 0.6f);
                Color col = new Color(180, 210, 255) * (0.6f * (1f - t));
                Main.spriteBatch.Draw(tex, pos, null, col, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            }

            // Glow layer (slightly larger, translucent)
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(tex, drawPos, null, new Color(200, 230, 255, 0) * 0.6f, Projectile.rotation, origin, Projectile.scale * 1.15f, SpriteEffects.None, 0f);

            // Main sprite
            Main.spriteBatch.Draw(tex, drawPos, null, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            return false; // we've drawn it
        }
    }
}
