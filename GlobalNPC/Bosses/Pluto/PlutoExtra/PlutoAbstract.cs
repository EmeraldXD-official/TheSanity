using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoExtra
{
    public class PlutoAbstract : Particle
    {
        // Property wajib Luminance untuk pendaftaran ke Atlas
        public override string AtlasTextureName => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoExtra/Plutickle";

        // Cache tekstur untuk penggambaran manual
        private static Asset<Texture2D> particleTexture;

        // Otomatis mengaktifkan Additive Blend (Glow merah neon)
        public override BlendState BlendState => BlendState.Additive;

        public PlutoAbstract(Vector2 position, Vector2 velocity, Color color, float scale, int lifetime)
        {
            Position = position;
            Velocity = velocity;
            DrawColor = color;       // Menggunakan DrawColor milik Luminance
            Scale = new Vector2(scale);
            Lifetime = lifetime;     // Menggunakan Lifetime milik Luminance
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
        }

        public override void Update()
        {
            // 1. PERGERAKAN & GESEKAN (Melambat perlahan saat menyebar)
            Velocity *= 0.94f;

            // Rotasi berputar perlahan mengikuti pergerakan
            Rotation += Velocity.X * 0.04f;

            // 2. ANIMASI FRAME (0 -> 6 seiring waktu hidup)
            float lifetimeProgress = 1f - ((float)Time / Lifetime);
            int currentFrame = (int)(lifetimeProgress * 7);
            currentFrame = Math.Clamp(currentFrame, 0, 6);

            // Sprite sheet 32x224 (7 frame, masing-masing 32x32 pixel)
            Frame = new Rectangle(0, currentFrame * 32, 32, 32);
        }

        public override void Draw(SpriteBatch spriteBatch)
        {
            particleTexture ??= ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoExtra/Plutickle");
            Texture2D texture = particleTexture.Value;

            Vector2 drawPos = Position - Main.screenPosition;
            Vector2 origin = new Vector2(32 / 2f, 32 / 2f);

            // Transparansi memudar di akhir umur
            float alpha = MathHelper.Clamp(1f - ((float)Time / Lifetime), 0f, 1f);

            // Warna Merah Glow Neon (RGB: 255, 30, 60)
            Color glowRedColor = new Color(255, 30, 60, 255) * alpha;

            spriteBatch.Draw(
                texture,
                drawPos,
                Frame,
                glowRedColor,
                Rotation,
                origin,
                Scale,
                SpriteEffects.None,
                0f
            );
        }

        // =======================================================
        // HELPER METHOD: MELETUPKAN PULSE / BURST KE SEGALA ARAH
        // =======================================================
        public static void SpawnPulse(Vector2 center, int count = 30, float minSpeed = 4f, float maxSpeed = 12f)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 randomDirection = Main.rand.NextVector2Unit();
                float speed = Main.rand.NextFloat(minSpeed, maxSpeed);
                Vector2 velocity = randomDirection * speed;

                float randomScale = Main.rand.NextFloat(0.6f, 1.8f);
                int lifetime = Main.rand.Next(20, 40);

                new PlutoAbstract(center, velocity, Color.Red, randomScale, lifetime).Spawn();
            }
        }
    }
}