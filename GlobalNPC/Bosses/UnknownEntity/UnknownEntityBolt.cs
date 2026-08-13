using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Effects;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    public class UnknownEntityBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Extra_197";

        public ref float Timer => ref Projectile.ai[0];
        public int ColorMode => (int)Projectile.ai[1];

        private const int TrailLength = 18; 
        private Vector2 initialVelocity;
        private const float EaseDuration = 60f;
        private bool initialized = false;

        private static Texture2D _processedShineFlareTex;
        private static Texture2D _processedBloomCircleTex;
        private static Texture2D _processedBloomLineTex;

        private float EaseInExpo(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return (float)Math.Pow(2, 10 * (t - 1f));
        }

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = TrailLength;

            Asset<Texture2D> rawShineFlare = null;
            Asset<Texture2D> rawBloomCircle = null;
            Asset<Texture2D> rawBloomLine = null;

            if (ModContent.HasAsset("Luminance/Assets/GreyscaleTextures/ShineFlare"))
                rawShineFlare = ModContent.Request<Texture2D>("Luminance/Assets/GreyscaleTextures/ShineFlare", AssetRequestMode.ImmediateLoad);

            if (ModContent.HasAsset("Luminance/Assets/GreyscaleTextures/BloomCircleSmall"))
                rawBloomCircle = ModContent.Request<Texture2D>("Luminance/Assets/GreyscaleTextures/BloomCircleSmall", AssetRequestMode.ImmediateLoad);

            if (ModContent.HasAsset("Luminance/Assets/GreyscaleTextures/BloomLine"))
                rawBloomLine = ModContent.Request<Texture2D>("Luminance/Assets/GreyscaleTextures/BloomLine", AssetRequestMode.ImmediateLoad);

            // Proses edit tekstur hitam jadi transparan di Main Thread
            Main.QueueMainThreadAction(() =>
            {
                if (rawShineFlare != null) _processedShineFlareTex = ProcessTexture(rawShineFlare.Value);
                if (rawBloomCircle != null) _processedBloomCircleTex = ProcessTexture(rawBloomCircle.Value);
                if (rawBloomLine != null) _processedBloomLineTex = ProcessTexture(rawBloomLine.Value);
            });
        }

        private static Texture2D ProcessTexture(Texture2D source)
        {
            if (source == null) return null;
            Color[] data = new Color[source.Width * source.Height];
            source.GetData(data);
            for (int i = 0; i < data.Length; i++)
            {
                if (data[i].R < 15 && data[i].G < 15 && data[i].B < 15)
                {
                    data[i] = Color.Transparent;
                }
            }
            Texture2D newTex = new Texture2D(source.GraphicsDevice, source.Width, source.Height);
            newTex.SetData(data);
            return newTex;
        }

        public override void SetDefaults()
        {
            Projectile.width = 26;
            Projectile.height = 26;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 400;
            Projectile.ignoreWater = true;
            Projectile.alpha = 0;
        }

        private Color GetMainColor()
        {
            return ColorMode switch
            {
                0 => Color.Cyan,
                1 => Color.Magenta,
                2 => Color.White,
                _ => new Color(190, 80, 255),
            };
        }

        private Color GetSecondaryColor()
        {
            return ColorMode switch
            {
                0 => Color.DeepSkyBlue,
                1 => Color.DeepPink,
                2 => new Color(200, 200, 255),
                _ => new Color(150, 60, 200),
            };
        }

        public override void AI()
        {
            Timer++;

            if (!initialized)
            {
                initialVelocity = Projectile.velocity * 2.5f;
                initialized = true;
            }

            float progress = Math.Min(Timer / EaseDuration, 1f);
            float easedProgress = EaseInExpo(progress);

            Vector2 currentVel = initialVelocity * easedProgress;

            float wobble = (float)Math.Sin(Timer * 0.15f + Projectile.whoAmI) * 0.15f;
            Vector2 wobbleDir = currentVel.RotatedBy(MathHelper.PiOver2).SafeNormalize(Vector2.UnitX) * wobble;
            currentVel += wobbleDir * (1f - progress * 0.5f);

            Projectile.velocity = Vector2.Lerp(Projectile.velocity, currentVel, 0.08f);
            Projectile.rotation = Projectile.velocity.ToRotation();

            // --- FITUR ZIGZAG ADAPTIF PINTAR ---
            // Jika proyektil ini mengarah ke Player, dia akan berbelok (zigzag) mengejar.
            // Jika proyektil ini mengarah ke samping (serangan Dash), dia tetap lurus.
            Player target = Main.player[Projectile.owner];
            if (target != null && target.active && target.position != Vector2.Zero)
            {
                Vector2 toTarget = target.Center - Projectile.Center;
                Vector2 projectileDir = Projectile.velocity.SafeNormalize(Vector2.Zero);
                Vector2 dirToTarget = toTarget.SafeNormalize(Vector2.Zero);
                
                // Hitung dot product (nilai kemiripan arah). 1 = searah, 0 = tegak lurus.
                float dot = Vector2.Dot(projectileDir, dirToTarget);
                
                // Jika arah proyektil dan arah ke player cukup mirip (lebih dari 45 derajat / dot > 0.7)
                // Maka aktifkan logic Zigzag + Homing.
                if (dot > 0.7f && toTarget.Length() < 900f)
                {
                    // 1. Gerakan Homing ringan (ditarik pelan ke player)
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, dirToTarget * 14f, 0.035f);
                    
                    // 2. Gerakan Zigzag (meliuk naik turun)
                    Vector2 perpDir = projectileDir.RotatedBy(MathHelper.PiOver2);
                    float zigzagAmplitude = 4.5f * Projectile.scale;
                    float zigzagFrequency = 0.18f;
                    
                    float waveOffset = (float)Math.Sin(Timer * zigzagFrequency + Projectile.whoAmI) * zigzagAmplitude;
                    Projectile.velocity += perpDir * waveOffset;
                    
                    // Jaga agar kecepatan maksimalnya tidak overdrive
                    if (Projectile.velocity.Length() > 32f)
                        Projectile.velocity = Projectile.velocity.SafeNormalize(Vector2.Zero) * 32f;
                }
            }
            // --- END OF ZIGZAG ---

            float speedFactor = Projectile.velocity.Length() / Math.Max(initialVelocity.Length(), 1f);
            float baseScale = MathHelper.Clamp(0.6f + speedFactor * 0.12f, 0.6f, 2.0f);
            float breath = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f + Projectile.whoAmI) * 0.08f;
            Projectile.scale = baseScale * breath;

            if (Timer == 1)
            {
                SoundEngine.PlaySound(new SoundStyle("TheSanity/SFX/PlasmaBolt") with { Volume = 0.6f, PitchVariance = 0.25f }, Projectile.Center);
            }

            if (Timer < 8f)
                Projectile.alpha = (int)(255 * (1f - Timer / 8f));
            else if (Projectile.timeLeft < 10)
                Projectile.alpha = (int)(255 * (1f - Projectile.timeLeft / 10f));
            else
                Projectile.alpha = 0;

            Vector3 lightColor = GetMainColor().ToVector3() * 0.85f * Projectile.scale;
            Lighting.AddLight(Projectile.Center, lightColor);

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center - Projectile.velocity * Main.rand.NextFloat(0.5f), DustID.Electric, -Projectile.velocity * 0.15f, 0, GetMainColor(), 0.85f);
                d.noGravity = true;
            }

            if (Timer > 10 && Timer % 15 == 0 && Projectile.velocity.Length() > 20f)
            {
                for (int i = 0; i < 5; i++)
                {
                    Dust ring = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(12f, 12f), DustID.WhiteTorch, -Projectile.velocity * 0.04f, 50, Color.White, 0.8f);
                    ring.noGravity = true;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color mainColor = GetMainColor();
            Color secondaryColor = GetSecondaryColor();
            float time = (float)Main.GlobalTimeWrappedHourly;

            // ---- START ADDITIVE DRAW ----
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Additive,
                SamplerState.LinearWrap,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            DrawTrail(mainColor, secondaryColor, time);
            DrawCoreWithShader(mainColor, secondaryColor, time);

            Main.spriteBatch.End();

            // ---- START NORMAL DRAW (Overlay Bloom) ----
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            DrawBloomOverlay(mainColor, time);

            Main.spriteBatch.End();

            // ---- RESTORE DEFAULT ----
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            return false;
        }

        private void DrawTrail(Color mainColor, Color secondaryColor, float time)
        {
            Texture2D trailTex = _processedBloomLineTex ?? _processedBloomCircleTex;
            if (trailTex == null) return;

            Vector2 trailOrigin = trailTex.Size() * 0.5f;
            float speedFactor = Projectile.velocity.Length() / 15f;

            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = i / (float)TrailLength;
                if (progress >= 1f) continue;

                Vector2 trailCenter = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float fade = 1f - progress; 

                // Lapisan 1: Ombak/Gelombang trail
                float waveOffset = (float)Math.Sin(time * 4f + i * 0.9f + Projectile.whoAmI) * 2.5f * fade;
                Vector2 waveDir = Projectile.velocity.SafeNormalize(Vector2.Zero).RotatedBy(MathHelper.PiOver2);
                trailCenter += waveDir * waveOffset;

                float trailScaleBase = (0.25f + speedFactor * 0.05f) * fade * Projectile.scale;
                Color trailColorBase = mainColor * (0.5f * fade);
                Main.spriteBatch.Draw(trailTex, trailCenter, null, trailColorBase, Projectile.rotation, trailOrigin, trailScaleBase, SpriteEffects.None, 0f);

                float trailScaleCore = (0.10f + speedFactor * 0.02f) * fade * Projectile.scale;
                Color trailColorCore = Color.White * (0.7f * fade);
                Main.spriteBatch.Draw(trailTex, trailCenter, null, trailColorCore, Projectile.rotation, trailOrigin, trailScaleCore, SpriteEffects.None, 0f);

                // Lapisan 4: Kilatan listrik
                if (_processedShineFlareTex != null && i % 2 == 0)
                {
                    float sparkPulse = (float)Math.Sin(time * 12f + i * 1.7f + Projectile.whoAmI) * 0.5f + 0.5f;
                    if (sparkPulse > 0.3f)
                    {
                        Texture2D spark = _processedShineFlareTex;
                        Vector2 sparkOrigin = spark.Size() * 0.5f;
                        float sparkScale = (0.06f * fade) * Projectile.scale * sparkPulse;
                        Color sparkColor = secondaryColor * (0.8f * fade * sparkPulse);
                        
                        float randX = (float)Math.Sin(i * 117.3f + 43.7f) * 0.5f + 0.5f;
                        float randY = (float)Math.Sin(i * 271.9f + 91.2f) * 0.5f + 0.5f;
                        Vector2 randomOffset = new Vector2((randX - 0.5f) * 8f, (randY - 0.5f) * 8f) * fade;
                        
                        Main.spriteBatch.Draw(spark, trailCenter + randomOffset, null, sparkColor, time * 3f + i * 0.5f, sparkOrigin, sparkScale, SpriteEffects.None, 0f);
                    }
                }

                // Lapisan 5: Titik cahaya glow
                if (_processedBloomCircleTex != null && i % 3 == 0)
                {
                    Texture2D glow = _processedBloomCircleTex;
                    Vector2 glowOrigin = glow.Size() * 0.5f;
                    float glowScale = (0.12f * fade) * Projectile.scale;
                    float glowPulse = 0.5f + 0.5f * (float)Math.Sin(time * 8f + i * 1.2f);
                    Color glowColor = mainColor * (0.4f * fade * glowPulse);
                    Main.spriteBatch.Draw(glow, trailCenter, null, glowColor, 0f, glowOrigin, glowScale, SpriteEffects.None, 0f);
                }
            }
        }

        private void DrawCoreWithShader(Color mainColor, Color secondaryColor, float time)
        {
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Texture2D coreTex = _processedBloomCircleTex;
            if (coreTex == null) return;

            Effect shader = DeathRayShaderLoader.Shader;
            Texture2D noise = DeathRayShaderLoader.NoiseTexture?.Value;

            if (shader != null && noise != null)
            {
                Vector2 coreOrigin = coreTex.Size() * 0.5f;
                shader.Parameters["uTime"]?.SetValue(time * 0.5f + Timer * 0.03f);
                shader.Parameters["uColor"]?.SetValue(mainColor.ToVector4());
                shader.Parameters["uSecondaryColor"]?.SetValue(secondaryColor.ToVector4());

                Main.graphics.GraphicsDevice.Textures[1] = noise;
                shader.CurrentTechnique.Passes[0].Apply();

                float breathCore = 1f + (float)Math.Sin(time * 8f + Timer * 0.5f) * 0.12f;
                float coreScale = (0.35f + (Projectile.velocity.Length() / 40f) * 0.08f) * Projectile.scale * breathCore;

                Main.spriteBatch.Draw(coreTex, drawPos, null, Color.White * Projectile.scale, 0f, coreOrigin, coreScale, SpriteEffects.None, 0f);
                Main.graphics.GraphicsDevice.Textures[1] = null;
            }
            else
            {
                Vector2 coreOrigin = coreTex.Size() * 0.5f;
                float breathCore = 1f + (float)Math.Sin(time * 8f + Timer * 0.5f) * 0.12f;
                float coreScale = (0.25f + (Projectile.velocity.Length() / 40f) * 0.06f) * Projectile.scale * breathCore;

                Main.spriteBatch.Draw(coreTex, drawPos, null, mainColor * 0.8f, 0f, coreOrigin, coreScale, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(coreTex, drawPos, null, Color.White * 0.5f, 0f, coreOrigin, coreScale * 0.5f, SpriteEffects.None, 0f);
            }
        }

        private void DrawBloomOverlay(Color mainColor, float time)
        {
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            if (_processedBloomCircleTex != null)
            {
                Texture2D bloom = _processedBloomCircleTex;
                Vector2 bloomOrigin = bloom.Size() * 0.5f;
                float ringPulse = 1f + (float)Math.Sin(time * 12f + Timer) * 0.12f;
                float breathBloom = 1f + (float)Math.Sin(time * 6f + Timer * 0.7f) * 0.08f;

                Main.spriteBatch.Draw(bloom, drawPos, null, mainColor * 0.4f * Projectile.scale, time * 2.2f, bloomOrigin, 0.40f * ringPulse * Projectile.scale * breathBloom, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(bloom, drawPos, null, mainColor * 0.7f * Projectile.scale, 0f, bloomOrigin, 0.25f * Projectile.scale * breathBloom, SpriteEffects.None, 0f);
            }

            if (_processedShineFlareTex != null)
            {
                Texture2D flare = _processedShineFlareTex;
                Vector2 flareOrigin = flare.Size() * 0.5f;
                float pulse = 1f + (float)Math.Sin(time * 10f + Timer) * 0.15f;
                float speedGlow = 1f + (Projectile.velocity.Length() / 25f) * 0.4f;
                float breathFlare = 1f + (float)Math.Sin(time * 7f + Timer * 0.6f) * 0.08f;

                Main.spriteBatch.Draw(flare, drawPos, null, mainColor * 0.55f * Projectile.scale, Timer * 0.06f, flareOrigin, 0.14f * pulse * Projectile.scale * speedGlow * breathFlare, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(flare, drawPos, null, Color.White * 0.4f * Projectile.scale, -Timer * 0.09f, flareOrigin, 0.08f * pulse * Projectile.scale * speedGlow * breathFlare, SpriteEffects.None, 0f);
                Main.spriteBatch.Draw(flare, drawPos, null, mainColor * 0.35f * Projectile.scale, Timer * 0.12f + 1.2f, flareOrigin, 0.07f * pulse * Projectile.scale * speedGlow * breathFlare, SpriteEffects.None, 0f);
            }
        }

        private void SpawnBurst(Color mainColor, int count, float speed)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = MathHelper.TwoPi * i / count;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(speed * 0.4f, speed);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.BlueTorch, vel, 0, mainColor, 1f);
                d.noGravity = true;
            }

            Dust flash = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, Vector2.Zero, 0, Color.White, 1.4f);
            flash.noGravity = true;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.immune = true;
            target.immuneTime = 18;
            SpawnBurst(GetMainColor(), 8, 4f);
        }

        public override void Kill(int timeLeft)
        {
            SpawnBurst(GetMainColor(), 10, 3f);
        }

        public override void Unload()
        {
            _processedShineFlareTex = null;
            _processedBloomCircleTex = null;
            _processedBloomLineTex = null;
        }
    }
}