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

        // Offset unik buat sampling noise di shader, biar tiap partikel
        // punya pola noise sendiri-sendiri, bukan pola yang sama diulang-ulang.
        public readonly Vector2 NoiseOffset;

        private float lifeTime;
        private readonly float fadeInTime;
        private readonly float fadeOutStart;
        private readonly float maxLifeTime;

        public WhiteWhaleFogParticle(Vector2 position, float maxLifeTime) {
            Position = position;
            Velocity = Main.rand.NextVector2Circular(1.1f, 1.1f);
            Rotation = Main.rand.NextFloat(MathHelper.TwoPi);
            RotationSpeed = Main.rand.NextFloat(-0.008f, 0.008f);
            // Sebelumnya 1.1f-2.0f (bisa sampai 1024px per partikel dari tekstur 512x512),
            // itu yang bikin asapnya numpuk jadi tembok putih solid. Diperkecil jadi
            // proporsi asap yang lebih wajar/atmospheric.
            Scale = Main.rand.NextFloat(0.55f, 0.95f);

            NoiseOffset = new Vector2(Main.rand.NextFloat(0f, 1000f), Main.rand.NextFloat(0f, 1000f));

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
        // Tetap dipakai sebagai MASK/siluet buat shader-nya, jadi tetap WAJIB
        // versi yang backgroundnya transparan (alpha channel asli).
        private const string TexturePath = "TheSanity/GlobalNPC/Bosses/WhiteWhale/Asep";

        // Source .fx ada di Effects/FogNoise.fx, di-compile otomatis jadi .xnb
        // sama build system tModLoader. Sesuaikan "TheSanity" kalau internal
        // name mod kalian beda.
        private const string EffectPath = "TheSanity/Effects/FogNoise";

        private static Asset<Texture2D> fogTexture;
        private static Asset<Effect> fogEffect;
        private static readonly List<WhiteWhaleFogParticle> particles = new();

        // BlendState custom: ambil nilai MAX antar pixel yang tumpang-tindih, bukan dijumlah.
        // BlendState.AlphaBlend biasa itu compounding — tiap partikel yang overlap nambah opacity
        // terus, jadi di area yang sering ke-overlap (misal deket titik spawn) cepet banget nyampe
        // opaque/putih solid walau opacity per-partikel udah kecil. Dengan Max blend, overlap
        // sebanyak apapun gak akan pernah lebih terang dari 1 partikel tunggal di titik itu,
        // jadi tekstur noise/gumpalan asapnya tetap kebaca alih-alih ketutup jadi tembok putih.
        private static readonly BlendState FogBlendState = new BlendState {
            ColorBlendFunction = BlendFunction.Max,
            ColorSourceBlend = Blend.One,
            ColorDestinationBlend = Blend.One,
            AlphaBlendFunction = BlendFunction.Max,
            AlphaSourceBlend = Blend.One,
            AlphaDestinationBlend = Blend.One,
        };

        public override void Load() {
            if (Main.dedServ) return;

            fogTexture = ModContent.Request<Texture2D>(TexturePath, AssetRequestMode.AsyncLoad);
            fogEffect = ModContent.Request<Effect>(EffectPath, AssetRequestMode.AsyncLoad);
        }

        public override void Unload() {
            fogTexture = null;
            fogEffect = null;
            particles.Clear();
        }

        /// <summary>
        /// Spawn satu partikel asep di posisi tertentu.
        /// Dipanggil dari WhiteWhaleBoss.SpawnFog().
        /// </summary>
        private static int spawnSkipCounter = 0;

        public static void SpawnFog(Vector2 position, float lifeTime = 130f) {
            if (Main.dedServ) return;
            if (particles.Count > 80) return; // batas biar ga nyekik FPS / numpuk kelihatan solid

            // Boss manggil SpawnFog() tiap tick dengan intensity tinggi.
            // Kita saring di sini biar kabutnya tetap kelihatan renggang, ga numpuk jadi tembok solid.
            spawnSkipCounter++;
            if (spawnSkipCounter % 5 != 0) return;

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

            // Kalau shader belum kelar load (async), fallback ke effect null =
            // dia gambar pakai default SpriteBatch shader (tekstur polos) daripada crash/invisible.
            bool useShader = fogEffect != null && fogEffect.IsLoaded && fogEffect.Value != null;
            Effect effect = useShader ? fogEffect.Value : null;

            float globalTime = Main.GlobalTimeWrappedHourly;

            EffectParameter pTime = null;
            EffectParameter pNoiseOffset = null;
            EffectParameter pNoiseScale = null;
            EffectParameter pIntensity = null;

            if (useShader) {
                pTime = effect.Parameters["uTime"];
                pNoiseOffset = effect.Parameters["uNoiseOffset"];
                pNoiseScale = effect.Parameters["uNoiseScale"];
                pIntensity = effect.Parameters["uIntensity"];

                // Scale-nya dinaikkan dari 4.5f -> 9f: noise-nya jadi lebih rapat/detail
                // (butiran lebih kecil-kecil) alih-alih gumpalan besar polos.
                // Tinggal naik/turunin angka ini kalau mau lebih kasar/halus lagi.
                pNoiseScale?.SetValue(9f);
                pIntensity?.SetValue(1f);
            }

            // Immediate + effect custom, karena tiap partikel butuh uNoiseOffset
            // beda-beda (di-set per Draw call). Deferred gak bisa gitu karena
            // batch-nya baru di-flush sekali pas End().
            spriteBatch.Begin(
                useShader ? SpriteSortMode.Immediate : SpriteSortMode.Deferred,
                FogBlendState,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                effect,
                Main.GameViewMatrix.TransformationMatrix
            );

            foreach (WhiteWhaleFogParticle particle in particles) {
                if (useShader) {
                    pTime?.SetValue(globalTime);
                    pNoiseOffset?.SetValue(particle.NoiseOffset);
                }

                // Pakai FogBlendState (Max) sekarang, jadi overlap antar partikel gak lagi numpuk
                // opacity-nya. Karena itu, opacity per-partikel bisa dinaikkan lagi biar kabutnya
                // tetap kelihatan jelas (sebelumnya 0.22f ditekan rendah buat nyiasatin compounding
                // dari AlphaBlend biasa, yang sekarang udah gak relevan lagi).
                Color drawColor = Color.White * (particle.Opacity * 0.5f);

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