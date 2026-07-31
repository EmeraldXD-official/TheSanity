using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhiteWhale
{
    // Satu partikel kabut. Class ringan, bukan Gore/Dust vanilla,
    // biar kita bisa atur sendiri jumlah, opacity, dan cuma 1x
    // SpriteBatch Begin/End per frame (murah) alih-alih per partikel.
    internal class WhiteWhaleFogParticle
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Rotation;
        public float RotationSpeed;
        public float Scale;
        public float Opacity;

        private float lifeTime;
        private readonly float fadeInTime;
        private readonly float fadeOutStart;
        private readonly float maxLifeTime;

        public WhiteWhaleFogParticle(Vector2 position, float maxLifeTime) {
            Position = position;
            Velocity = Main.rand.NextVector2Circular(1.1f, 1.1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            RotationSpeed = Main.rand.NextFloat(-0.008f, 0.008f);
            Scale = Main.rand.NextFloat(1.1f, 2.0f);

            this.maxLifeTime = maxLifeTime;
            fadeInTime = maxLifeTime * 0.15f;
            fadeOutStart = maxLifeTime * 0.55f;
        }

        // Return true kalau partikel sudah harus dihapus.
        public bool Update() {
            lifeTime++;
            Position += Velocity;
            Velocity *= 0.985f;
            Rotation += RotationSpeed;

            if (lifeTime < fadeInTime) {
                Opacity = lifeTime / fadeInTime;
            }
            else if (lifeTime > fadeOutStart) {
                Opacity = 1f - (lifeTime - fadeOutStart) / (maxLifeTime - fadeOutStart);
            }
            else {
                Opacity = 1f;
            }

            return lifeTime >= maxLifeTime;
        }
    }

    public class WhiteWhaleFogSystem : ModSystem
    {
        // Sesuaikan path ini dengan lokasi asep.png kalian yang sebenarnya.
        // WAJIB pakai versi asep.png yang backgroundnya sudah transparan
        // (PNG dengan alpha channel asli, bukan background hitam).
        private const string TexturePath = "TheSanity/GlobalNPC/Bosses/WhiteWhale/Asep";

        private static Asset<Texture2D> fogTexture;
        private static readonly List<WhiteWhaleFogParticle> particles = new();

        public override void Load() {
            if (Main.dedServ) return;

            fogTexture = ModContent.Request<Texture2D>(TexturePath, AssetRequestMode.AsyncLoad);
        }

        public override void Unload() {
            fogTexture = null;
            particles.Clear();
        }

        /// <summary>
        /// Spawn satu partikel asep di posisi tertentu.
        /// Dipanggil dari WhiteWhaleBoss.SpawnFog().
        /// </summary>
        private static int spawnSkipCounter = 0;

        public static void SpawnFog(Vector2 position, float lifeTime = 130f) {
            if (Main.dedServ) return;
            if (particles.Count > 90) return; // batas biar ga nyekik FPS / numpuk kelihatan solid

            // Boss manggil SpawnFog() tiap tick dengan intensity tinggi.
            // Kita saring di sini biar kabutnya tetap kelihatan renggang, ga numpuk.
            spawnSkipCounter++;
            if (spawnSkipCounter % 4 != 0) return;

            particles.Add(new WhiteWhaleFogParticle(position, lifeTime));
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ) return;

            for (int i = particles.Count - 1; i >= 0; i--) {
                if (particles[i].Update()) {
                    particles.RemoveAt(i);
                }
            }
        }

        // Digambar setelah tile (di layer "belakang"), sebelum NPC/player.
        // Kalau mau kabutnya di atas boss, pindahkan pemanggilan DrawFog()
        // ini ke hook lain, misal lewat detour On_Main.DrawNPCs.
        public override void PostDrawTiles() {
            DrawFog();
        }

        private static void DrawFog() {
            if (Main.dedServ || particles.Count == 0) return;
            if (fogTexture == null || !fogTexture.IsLoaded) return;

            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D texture = fogTexture.Value;
            Vector2 origin = texture.Size() / 2f;

            // Blend alpha biasa, tanpa shader. asep.png-nya sendiri yang
            // harus sudah transparan di bagian yang bukan asap.
            spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            foreach (WhiteWhaleFogParticle particle in particles) {
                Color drawColor = Color.White * (particle.Opacity * 0.35f);

                spriteBatch.Draw(
                    texture,
                    particle.Position - Main.screenPosition,
                    null,
                    drawColor,
                    particle.Rotation,
                    origin,
                    particle.Scale,
                    SpriteEffects.None,
                    0f
                );
            }

            spriteBatch.End();
        }
    }
}