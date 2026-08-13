using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Effects;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    public class DimensionalGridLine : ModProjectile
    {
        public override string Texture => "Terraria/Images/Extra_197";

        public const int TelegraphTime = 75;
        public const int SliceTime = 18;
        public const float LineLength = 4200f; // DIPERPANJANG dari 3200f agar ujung terlihat

        public const int MemWarnTime = 25;
        public const int MemDormantTime = 50;
        public const int MemReactivateTime = 15;

        public ref float Timer => ref Projectile.ai[0];
        public bool IsMagenta => Projectile.ai[1] == 1f;
        public bool IsMemory => Projectile.ai[2] == 1f;

        public int SliceStartTime => IsMemory ? (MemWarnTime + MemDormantTime + MemReactivateTime) : TelegraphTime;

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 400;
            Projectile.ignoreWater = true;
            Projectile.damage = 65;
        }

        public override void AI()
        {
            Timer++;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Timer == 1)
            {
                SoundEngine.PlaySound(new SoundStyle("TheSanity/SFX/DimensionalGridLineApperSFX") with { Volume = 1f }, Projectile.Center);
            }

            Color mainColor = IsMagenta ? Color.Magenta : Color.Cyan;
            int sliceStart = SliceStartTime;

            if (!IsMemory)
            {
                if (Timer < sliceStart)
                {
                    if (Main.rand.NextBool(4))
                    {
                        float offset = Main.rand.NextFloat(-LineLength / 2f, LineLength / 2f);
                        Vector2 dustPos = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * offset;
                        Dust d = Dust.NewDustPerfect(dustPos, DustID.Electric, Vector2.Zero, 0, mainColor, 0.6f);
                        d.noGravity = true;
                    }
                }
                else if (Timer == sliceStart)
                {
                    PlaySliceSound();
                    SpawnSliceBurstDust(mainColor);
                }
            }
            else
            {
                if (Timer < MemWarnTime)
                {
                    if (Main.rand.NextBool(5))
                    {
                        float offset = Main.rand.NextFloat(-LineLength / 2f, LineLength / 2f);
                        Vector2 dustPos = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * offset;
                        Dust d = Dust.NewDustPerfect(dustPos, DustID.Electric, Vector2.Zero, 0, mainColor, 0.5f);
                        d.noGravity = true;
                    }
                }
                else if (Timer == MemWarnTime)
                {
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.7f, Volume = 0.3f }, Projectile.Center);
                }
                else if (Timer > MemWarnTime && Timer < MemWarnTime + MemDormantTime)
                {
                    if (Main.rand.NextBool(55))
                    {
                        float offset = Main.rand.NextFloat(-LineLength / 2f, LineLength / 2f);
                        Vector2 dustPos = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * offset;
                        Dust d = Dust.NewDustPerfect(dustPos, DustID.Electric, Vector2.Zero, 200, mainColor, 0.3f);
                        d.noGravity = true;
                    }
                }
                else if (Timer == MemWarnTime + MemDormantTime)
                {
                    SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.2f, Volume = 0.55f }, Projectile.Center);
                }
                else if (Timer > MemWarnTime + MemDormantTime && Timer < sliceStart)
                {
                    if (Main.rand.NextBool(2))
                    {
                        float offset = Main.rand.NextFloat(-LineLength / 2f, LineLength / 2f);
                        Vector2 dustPos = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * offset;
                        Dust d = Dust.NewDustPerfect(dustPos, DustID.Electric, Vector2.Zero, 0, mainColor, 0.8f);
                        d.noGravity = true;
                    }
                }
                else if (Timer == sliceStart)
                {
                    PlaySliceSound();
                    SpawnSliceBurstDust(mainColor);
                }
            }

            if (Timer >= sliceStart + SliceTime)
            {
                Projectile.Kill();
            }
        }

        private void PlaySliceSound()
        {
            SoundEngine.PlaySound(new SoundStyle("TheSanity/SFX/DimensionalGridLineimpactSFX") with { Volume = 1.2f }, Projectile.Center);
        }

        private void SpawnSliceBurstDust(Color mainColor)
        {
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            for (int i = -8; i <= 8; i++)
            {
                Vector2 dustPos = Projectile.Center + dir * (i * 180f);
                Dust d = Dust.NewDustPerfect(dustPos, DustID.BlueTorch, Main.rand.NextVector2Circular(3f, 3f), 0, mainColor, 1f);
                d.noGravity = true;

                if (i % 2 == 0)
                {
                    Dust spark = Dust.NewDustPerfect(dustPos, DustID.WhiteTorch, Main.rand.NextVector2Circular(5f, 5f), 0, Color.White, 0.7f);
                    spark.noGravity = true;
                }
            }
        }

        public override bool CanHitPlayer(Player target)
        {
            return Timer >= SliceStartTime;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (Timer < SliceStartTime)
                return false;

            float point = 0f;
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Vector2 start = Projectile.Center - dir * (LineLength / 2f);
            Vector2 end = Projectile.Center + dir * (LineLength / 2f);

            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, 22f, ref point);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.immune = true;
            target.immuneTime = 20;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = TextureAssets.Extra[197].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);

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

            Color drawColor = IsMagenta ? Color.Magenta : Color.Cyan;
            int sliceStart = SliceStartTime;

            if (Timer < sliceStart)
            {
                float alpha;

                if (!IsMemory)
                {
                    float progress = Timer / sliceStart;
                    alpha = 0.15f + (progress * 0.35f);
                }
                else if (Timer < MemWarnTime)
                {
                    float progress = Timer / MemWarnTime;
                    alpha = 0.15f + (progress * 0.30f);
                }
                else if (Timer < MemWarnTime + MemDormantTime)
                {
                    float fadeProgress = MathHelper.Clamp((Timer - MemWarnTime) / 20f, 0f, 1f);
                    float dormantPulse = 0.02f + (float)Math.Sin(Timer * 0.15f) * 0.015f;
                    alpha = MathHelper.Lerp(0.45f, 0.035f, fadeProgress) + dormantPulse;
                }
                else
                {
                    float reactivateProgress = (Timer - (MemWarnTime + MemDormantTime)) / MemReactivateTime;
                    alpha = MathHelper.Lerp(0.05f, 0.55f, reactivateProgress);
                    drawColor = Color.Lerp(drawColor, Color.White, reactivateProgress * 0.4f);
                }

                Vector2 scale = new Vector2(LineLength / tex.Width, 0.08f);
                Main.spriteBatch.Draw(tex, drawPos, null, drawColor * alpha, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
            }
            else
            {
                float sliceProgress = (Timer - sliceStart) / (float)SliceTime;
                float intensity = (float)Math.Sin(sliceProgress * MathHelper.Pi);

                Vector2 scale = new Vector2(LineLength / tex.Width, 0.48f * intensity);

                Effect shader = DeathRayShaderLoader.Shader;
                Texture2D noise = DeathRayShaderLoader.NoiseTexture?.Value;

                if (shader != null && noise != null)
                {
                    Color secondaryColor = IsMagenta ? Color.DeepPink : Color.DeepSkyBlue;

                    shader.Parameters["uTime"]?.SetValue((float)Main.time * 0.09f);
                    shader.Parameters["uColor"]?.SetValue(drawColor.ToVector4());
                    shader.Parameters["uSecondaryColor"]?.SetValue(secondaryColor.ToVector4());

                    Main.graphics.GraphicsDevice.Textures[1] = noise;
                    shader.CurrentTechnique.Passes[0].Apply();

                    Main.spriteBatch.Draw(tex, drawPos, null, Color.White * intensity, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);

                    Main.graphics.GraphicsDevice.Textures[1] = null;
                }
                else
                {
                    Main.spriteBatch.Draw(tex, drawPos, null, drawColor * intensity * 0.65f, Projectile.rotation, origin, scale, SpriteEffects.None, 0f);
                }

                Main.spriteBatch.Draw(tex, drawPos, null, Color.White * intensity * 0.35f, Projectile.rotation, origin, scale * 0.45f, SpriteEffects.None, 0f);

                if (Timer <= sliceStart + 6f)
                {
                    float flashProgress = (Timer - sliceStart) / 6f;
                    float flashAlpha = 1f - flashProgress;
                    float flashScale = 0.7f + flashProgress * 1.4f;

                    Main.spriteBatch.Draw(tex, drawPos, null, Color.White * flashAlpha * 0.8f, 0f, origin, flashScale * 0.6f, SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(tex, drawPos, null, drawColor * flashAlpha * 0.6f, 0f, origin, flashScale, SpriteEffects.None, 0f);
                }
            }

            Main.spriteBatch.End();
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
    }
}