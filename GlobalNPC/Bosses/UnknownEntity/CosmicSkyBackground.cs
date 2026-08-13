using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    /// <summary>
    /// Background kosmik utama secara global lewat SkyManager (CustomSky)[cite: 2, 3]. 
    /// Menampilkan efek kosmik penuh tanpa ada bagian yang terpotong dengan noise yang lebih tebal.
    /// </summary>
    public class CosmicSkyBackground : CustomSky
    {
        private static Asset<Texture2D> bloomCircleTex;
        private static Asset<Texture2D> shineFlareTex;
        private static Asset<Texture2D> turbulentNoiseTex;

        private struct DustStar
        {
            public Vector2 pos;
            public float scale;
            public float parallaxDepth;
            public float pulseSpeed;
            public float pulseOffset;
            public float baseAlpha;
        }

        private struct HeroStar
        {
            public Vector2 pos;
            public float scale;
            public float parallaxDepth;
            public float pulseSpeed;
            public float pulseOffset;
            public float rotation;
        }

        private struct Comet
        {
            public Vector2 pos;
            public Vector2 vel;
            public float life;
            public float maxLife;
            public float scale;
        }

        private DustStar[] dustStars;
        private HeroStar[] heroStars;
        private readonly List<Comet> comets = new List<Comet>();
        private float cometTimer;

        private float time;
        private float noiseTimer1;
        private float noiseTimer2;
        private float noiseTimer3;
        private float noiseTimer4;
        private bool active;
        private bool isPhase2;
        private float opacity;
        private bool drawnThisFrame;

        // Palet warna senada per fase (dari gelap/jauh -> terang/dekat)[cite: 2]
        private static readonly Color[] PalettePhase1 = { new Color(10, 15, 45), new Color(25, 60, 110), new Color(70, 130, 180) };
        private static readonly Color[] PalettePhase2 = { new Color(45, 10, 35), new Color(120, 30, 90), new Color(200, 90, 150) };

        public override void OnLoad()
        {
            if (ModContent.HasAsset("Luminance/Assets/GreyscaleTextures/BloomCircleSmall"))
                bloomCircleTex = ModContent.Request<Texture2D>("Luminance/Assets/GreyscaleTextures/BloomCircleSmall", AssetRequestMode.ImmediateLoad);
            if (ModContent.HasAsset("Luminance/Assets/GreyscaleTextures/ShineFlare"))
                shineFlareTex = ModContent.Request<Texture2D>("Luminance/Assets/GreyscaleTextures/ShineFlare", AssetRequestMode.ImmediateLoad);
            if (ModContent.HasAsset("Luminance/Assets/Noise/TurbulentNoise"))
                turbulentNoiseTex = ModContent.Request<Texture2D>("Luminance/Assets/Noise/TurbulentNoise", AssetRequestMode.ImmediateLoad);

            GenerateStars();
        }

        private void GenerateStars()
        {
            int dustCount = 90;
            dustStars = new DustStar[dustCount];
            for (int i = 0; i < dustCount; i++)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(100f, 2600f);

                dustStars[i].pos = angle.ToRotationVector2() * radius;
                dustStars[i].scale = Main.rand.NextFloat(0.03f, 0.14f);
                dustStars[i].parallaxDepth = Main.rand.NextFloat(0.15f, 0.9f);
                dustStars[i].pulseSpeed = Main.rand.NextFloat(0.5f, 2f);
                dustStars[i].pulseOffset = Main.rand.NextFloat(MathHelper.TwoPi);
                dustStars[i].baseAlpha = Main.rand.NextFloat(0.25f, 0.6f);
            }

            int heroCount = 45;
            heroStars = new HeroStar[heroCount];
            for (int i = 0; i < heroCount; i++)
            {
                float angle = Main.rand.NextFloat(MathHelper.TwoPi);
                float radius = Main.rand.NextFloat(200f, 2400f);

                heroStars[i].pos = angle.ToRotationVector2() * radius;
                heroStars[i].scale = Main.rand.NextFloat(0.2f, 0.45f);
                heroStars[i].parallaxDepth = Main.rand.NextFloat(0.25f, 0.8f);
                heroStars[i].pulseSpeed = Main.rand.NextFloat(0.6f, 2.0f);
                heroStars[i].pulseOffset = Main.rand.NextFloat(MathHelper.TwoPi);
                heroStars[i].rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            }
        }

        public override void Activate(Vector2 position, params object[] args)
        {
            active = true;
            if (args.Length > 0 && args[0] is bool p2)
                isPhase2 = p2;

            if (dustStars == null)
                GenerateStars();
        }

        public override void Deactivate(params object[] args) => active = false;

        public override void Reset()
        {
            active = false;
            opacity = 0f;
            comets.Clear();
            CosmicFilterSystem.SetActive(false);
        }

        public override bool IsActive() => active || opacity > 0.001f;

        public override float GetCloudAlpha() => MathHelper.Lerp(1f, 0f, opacity);

        public void SetPhase(bool phase2) => isPhase2 = phase2;

        public override void Update(GameTime gameTime)
        {
            drawnThisFrame = false;

            float fadeSpeed = 0.02f;
            opacity = active
                ? Math.Min(1f, opacity + fadeSpeed)
                : Math.Max(0f, opacity - fadeSpeed);

            if (!active && opacity <= 0f)
                return;

            time += 0.01f;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

            noiseTimer1 += dt * 10f;
            noiseTimer2 += dt * 18f;
            noiseTimer3 += dt * 28f;
            noiseTimer4 += dt * 14f;

            CosmicFilterSystem.SetActive(active || opacity > 0.001f);
            Color[] shaderPalette = isPhase2 ? PalettePhase2 : PalettePhase1;
            CosmicFilterSystem.UpdateParams(time, shaderPalette[2], opacity);

            if (active)
            {
                cometTimer -= dt;
                if (cometTimer <= 0f && comets.Count < 1)
                {
                    SpawnComet();
                    cometTimer = Main.rand.NextFloat(10f, 18f);
                }
            }

            for (int i = comets.Count - 1; i >= 0; i--)
            {
                Comet c = comets[i];
                c.pos += c.vel * dt;
                c.life -= dt;
                comets[i] = c;

                if (c.life <= 0f)
                    comets.RemoveAt(i);
            }
        }

        private void SpawnComet()
        {
            bool fromLeft = Main.rand.NextBool();
            float startY = Main.rand.NextFloat(-100f, Main.screenHeight * 0.5f);
            Vector2 start = new Vector2(fromLeft ? -150f : Main.screenWidth + 150f, startY);

            float speed = Main.rand.NextFloat(220f, 340f);
            Vector2 dir = new Vector2(fromLeft ? 1f : -1f, Main.rand.NextFloat(0.25f, 0.5f));
            dir.Normalize();

            comets.Add(new Comet
            {
                pos = start,
                vel = dir * speed,
                maxLife = 4.5f,
                life = 4.5f,
                scale = Main.rand.NextFloat(0.6f, 1.0f),
            });
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
        {
            if (opacity <= 0.001f || bloomCircleTex == null || shineFlareTex == null)
                return;

            if (drawnThisFrame || maxDepth < float.MaxValue)
                return;
            drawnThisFrame = true;

            Color[] palette = isPhase2 ? PalettePhase2 : PalettePhase1;

            Vector2 cameraCenter = Main.screenPosition + new Vector2(Main.screenWidth / 2, Main.screenHeight / 2);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 1. Dasar hitam
            Texture2D blackTile = TextureAssets.BlackTile.Value;
            spriteBatch.Draw(blackTile, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.Black * opacity);

            // 2. QUADRUPLE FULL-SCREEN LAYER NOISE (Opasitas ditingkatkan agar noise jauh lebih tebal & pekat)[cite: 2]
            if (turbulentNoiseTex != null && turbulentNoiseTex.IsLoaded && turbulentNoiseTex.Value != null)
            {
                DrawScrollingNoise(spriteBatch, turbulentNoiseTex.Value, new Vector2(0.3f, 0.1f), 2.5f, 4.0f, palette[1] * (0.50f * opacity), noiseTimer1);
                DrawScrollingNoise(spriteBatch, turbulentNoiseTex.Value, new Vector2(-0.5f, 0.4f), 4.0f, 2.5f, palette[2] * (0.42f * opacity), noiseTimer2);
                DrawScrollingNoise(spriteBatch, turbulentNoiseTex.Value, new Vector2(0.6f, -0.3f), 6.0f, 1.5f, palette[0] * (0.35f * opacity), noiseTimer3);

                // Lapisan ke-4: Noise glow putih terang dengan ketebalan yang lebih solid
                DrawScrollingNoise(spriteBatch, turbulentNoiseTex.Value, new Vector2(-0.2f, 0.3f), 3.5f, 2.0f, Color.White * (0.30f * opacity), noiseTimer4);
            }

            // Top Bloom Gradient penyatu cahaya atas
            Texture2D bloomTex = bloomCircleTex.Value;
            Vector2 bloomOrigin = bloomTex.Size() * 0.5f;
            Vector2 topCenterPos = new Vector2(Main.screenWidth * 0.5f, 0f);
            Vector2 topScale = new Vector2(Main.screenWidth / bloomOrigin.X * 0.9f, 2.2f);
            spriteBatch.Draw(bloomTex, topCenterPos, null, Color.White * (0.25f * opacity), 0f, bloomOrigin, topScale, SpriteEffects.None, 0f);

            // 3. Nebula: 3 blob raksasa[cite: 2]
            float breathSlow = 1f + (float)Math.Sin(time * 0.15f) * 0.06f;

            (Vector2 offset, float rotSpeed, float baseScale, int colorIndex, float alpha)[] nebulaBlobs =
            {
                (new Vector2(-250f, -80f), 0.03f, 7.5f, 2, 0.22f),
                (new Vector2(350f, 150f), -0.025f, 8.5f, 1, 0.18f),
                (new Vector2(50f, 350f), 0.02f, 6.0f, 0, 0.16f),
            };

            for (int b = 0; b < nebulaBlobs.Length; b++)
            {
                var blob = nebulaBlobs[b];
                Vector2 pos = cameraCenter + blob.offset * 0.15f - Main.screenPosition;
                Color color = palette[blob.colorIndex];
                float rot = time * blob.rotSpeed;
                spriteBatch.Draw(bloomTex, pos, null, color * (blob.alpha * opacity), rot, bloomOrigin, blob.baseScale * breathSlow, SpriteEffects.None, 0f);
            }

            // 4. Dust stars[cite: 2]
            for (int i = 0; i < dustStars.Length; i++)
            {
                DustStar star = dustStars[i];
                Vector2 worldPos = cameraCenter + star.pos * star.parallaxDepth;
                Vector2 drawPos = worldPos - Main.screenPosition;

                if (drawPos.X < -50f || drawPos.X > Main.screenWidth + 50f || drawPos.Y < -50f || drawPos.Y > Main.screenHeight + 50f)
                    continue;

                float pulse = 0.6f + 0.4f * (float)Math.Sin(time * star.pulseSpeed + star.pulseOffset);
                Color color = Color.Lerp(Color.White, palette[2], 0.25f);

                spriteBatch.Draw(bloomTex, drawPos, null, color * (star.baseAlpha * pulse * opacity), 0f, bloomOrigin, star.scale, SpriteEffects.None, 0f);
            }

            // 5. Hero stars + Exponential Easing Scale Animation
            Texture2D flareTex = shineFlareTex.Value;
            Vector2 flareOrigin = flareTex.Size() * 0.5f;

            for (int i = 0; i < heroStars.Length; i++)
            {
                HeroStar star = heroStars[i];
                Vector2 worldPos = cameraCenter + star.pos * star.parallaxDepth;
                Vector2 drawPos = worldPos - Main.screenPosition;

                if (drawPos.X < -300f || drawPos.X > Main.screenWidth + 300f || drawPos.Y < -300f || drawPos.Y > Main.screenHeight + 300f)
                    continue;

                float sineVal = (float)Math.Sin(time * star.pulseSpeed + star.pulseOffset);
                float t = (sineVal + 1f) * 0.5f;

                float easeExpo = t == 0f ? 0f : (float)Math.Pow(2f, 10f * (t - 1f));
                float scaleFactor = MathHelper.Lerp(0.3f, 1.8f, easeExpo);

                Color flareColor = Color.Lerp(Color.White, palette[2], 0.3f) * (0.8f * t * opacity);

                spriteBatch.Draw(bloomTex, drawPos, null, Color.White * (0.4f * t * opacity), 0f, bloomOrigin, 0.2f * star.scale * scaleFactor, SpriteEffects.None, 0f);
                spriteBatch.Draw(flareTex, drawPos, null, flareColor, star.rotation, flareOrigin, star.scale * scaleFactor, SpriteEffects.None, 0f);
            }

            // 6. Komet langka
            Vector2 circleOrigin = bloomOrigin;
            for (int i = 0; i < comets.Count; i++)
            {
                Comet c = comets[i];
                float lifeFrac = c.life / c.maxLife;
                float fade = MathHelper.Clamp(Math.Min(lifeFrac, 1f - lifeFrac) * 4f, 0f, 1f) * opacity;

                Vector2 dirNorm = c.vel;
                if (dirNorm != Vector2.Zero)
                    dirNorm.Normalize();

                Color trailColor = Color.Lerp(Color.White, palette[2], 0.4f);

                for (int t = 5; t >= 1; t--)
                {
                    float segFrac = t / 5f;
                    Vector2 segPos = c.pos - dirNorm * (t * 20f * c.scale);
                    float segAlpha = (1f - segFrac * 0.85f) * fade * 0.6f;
                    float segScale = (0.35f - segFrac * 0.22f) * c.scale;
                    spriteBatch.Draw(bloomTex, segPos, null, trailColor * segAlpha, 0f, circleOrigin, segScale, SpriteEffects.None, 0f);
                }

                spriteBatch.Draw(bloomTex, c.pos, null, Color.White * (0.9f * fade), 0f, circleOrigin, 0.4f * c.scale, SpriteEffects.None, 0f);
            }

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private void DrawScrollingNoise(SpriteBatch spriteBatch, Texture2D noise, Vector2 scrollDir, float scrollSpeed, float scale, Color color, float noiseTimer)
        {
            Vector2 tileSize = noise.Size() * scale;
            if (tileSize.X <= 0f || tileSize.Y <= 0f)
                return;

            Vector2 rawOffset = scrollDir * noiseTimer * scrollSpeed;
            Vector2 offset = new Vector2(
                ((rawOffset.X % tileSize.X) + tileSize.X) % tileSize.X,
                ((rawOffset.Y % tileSize.Y) + tileSize.Y) % tileSize.Y
            );

            int cols = (int)Math.Ceiling(Main.screenWidth / tileSize.X) + 2;
            int rows = (int)Math.Ceiling(Main.screenHeight / tileSize.Y) + 2;

            for (int x = -1; x < cols; x++)
            {
                for (int y = -1; y < rows; y++)
                {
                    Vector2 pos = new Vector2(x * tileSize.X, y * tileSize.Y) - offset;
                    spriteBatch.Draw(noise, pos, null, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
            }
        }

        public static void UnloadAssets()
        {
            bloomCircleTex = null;
            shineFlareTex = null;
            turbulentNoiseTex = null;
        }
    }
}