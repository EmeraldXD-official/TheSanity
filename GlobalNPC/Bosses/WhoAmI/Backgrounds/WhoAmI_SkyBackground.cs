using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Graphics.Effects;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ============================================================================================
    // WHOAMI - CUSTOM SKY BACKGROUND
    // ============================================================================================
    // Port dari prototipe HTML/CSS parallax yang udah di-approve ke sistem CustomSky bawaan
    // tModLoader (sama kayak cara Moon Lord/Empress of Light ganti langit pas fight). Ada 2 layer:
    //
    //   1. TILE (WhoAmISeamlessTile.png) - ubin 800x800 yang mirror-tiled (bukan blend biasa),
    //      jadi sambungannya dijamin nyambung sempurna kalau di-repeat ke segala arah. Di-scroll
    //      berdasarkan posisi WORLD player * parallaxFactor, BUKAN akumulasi velocity manual -
    //      lebih robust (gak ada drift, gak kacau kalau player di-teleport/knockback jauh).
    //
    //   2. CORNER MOTION BLUR (WhoAmICornerMotionBlur.png) - overlay screen-space statis (gak
    //      pernah rotasi/zoom/geser posisi - itu yang bikin "goyang" di versi awal), cuma
    //      opacity-nya yang naik-turun ngikutin kecepatan gerak player. Attach ke SUDUT LAYAR,
    //      bukan ke dunia, jadi dia gak ikut ke-scroll sama tile-nya.
    //
    // AKTIVASI: ditangani WhoAmISkySystem di bawah, yang tiap tick ngecek WhoAmI.FindRealBossIndex()
    // - jadi file ini gak perlu nyentuh WhoAmI.cs/WhoAmI_Patterns.cs sama sekali.
    //
    // ART REQUIREMENT: taruh 2 file ini di:
    //   Content/TheSanity/GlobalNPC/Bosses/WhoAmI/Backgrounds/WhoAmISeamlessTile.png   (800x800)
    //   Content/TheSanity/GlobalNPC/Bosses/WhoAmI/Backgrounds/WhoAmICornerMotionBlur.png (1024x576, ada alpha)
    // Kalau taruh di path lain, tinggal ubah 2 const *Path di bawah.
    // ============================================================================================
    public class WhoAmISky : CustomSky
    {
        private const string TilePath = "TheSanity/GlobalNPC/Bosses/WhoAmI/Backgrounds/WhoAmISeamlessTile";
        private const string CornerPath = "TheSanity/GlobalNPC/Bosses/WhoAmI/Backgrounds/WhoAmICornerMotionBlur";

        // Seberapa jauh tile ini "ketarik" relatif ke gerakan player - 1.0 = gerak 1:1 sama player
        // (berasa nempel dekat), makin kecil makin berasa jauh/lambat (parallax layer belakang).
        private const float ParallaxFactor = 0.35f;

        // Opacity overlay pojok: idle (player diam) vs pas player ngebut (dash/mount cepat dsb).
        private const float CornerOpacityIdle = 0.45f;
        private const float CornerOpacityMax = 0.9f;
        private const float CornerOpacitySpeedCap = 12f; // kecepatan (px/tick) buat nyampe opacity max

        private bool isActive;
        private float currentOpacity = CornerOpacityIdle;

        public override void Activate(Vector2 position, params object[] args) => isActive = true;

        public override void Deactivate(params object[] args) => isActive = false;

        public override void Reset() => isActive = false;

        public override bool IsActive() => isActive;

        // Sembunyiin awan/langit vanilla selama background ini aktif, biar gak numpuk/ganggu.
        public override float GetCloudAlpha() => 0f;

        public override void Update(GameTime gameTime)
        {
            if (!isActive) return;

            float speed = Main.LocalPlayer != null ? Main.LocalPlayer.velocity.Length() : 0f;
            float targetOpacity = MathHelper.Lerp(CornerOpacityIdle, CornerOpacityMax,
                MathHelper.Clamp(speed / CornerOpacitySpeedCap, 0f, 1f));

            // Smoothing biar transisi opacity-nya halus, gak kedip/snap tiap frame kecepatan berubah.
            currentOpacity = MathHelper.Lerp(currentOpacity, targetOpacity, 0.08f);
        }

        public override void Draw(SpriteBatch spriteBatch, float minDepth, float maxDepth)
        {
            if (!isActive) return;

            // Konvensi tModLoader: layer background "sejauh mungkin" (di belakang semua tile/NPC)
            // digambar sekali dengan minDepth 0 & maxDepth float.MaxValue - draw di pass itu aja.
            if (!(maxDepth >= float.MaxValue && minDepth < float.MaxValue)) return;

            Texture2D tile = ModContent.Request<Texture2D>(TilePath, AssetRequestMode.ImmediateLoad).Value;
            Texture2D corner = ModContent.Request<Texture2D>(CornerPath, AssetRequestMode.ImmediateLoad).Value;

            // Pakai ukuran VIEWPORT AKTUAL (bukan Main.screenWidth/Height yang di-cache), dan
            // Matrix.Identity secara eksplisit buat ke-2 layer - sengaja LEPAS dari transform zoom
            // apapun yang lagi aktif (termasuk dari mod kayak Better Zoom, yang biasanya nge-scale
            // render target). Tanpa ini: tile ikut kebesaran pas di-zoom, dan overlay pojok bisa
            // kepotong kalau render target-nya di-resize sama mod zoom itu. Background ini emang
            // didesain sebagai layer screen-space tetap (kayak vignette/UI), bukan bagian dari
            // dunia yang ikut ke-zoom.
            var viewport = Main.instance.GraphicsDevice.Viewport;
            int screenW = viewport.Width;
            int screenH = viewport.Height;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            // --- Layer 1: tile infinite, offset dari posisi WORLD player (bukan akumulasi manual) ---
            Vector2 worldOffset = (Main.LocalPlayer != null ? Main.LocalPlayer.Center : Vector2.Zero) * ParallaxFactor;
            float offsetX = ((worldOffset.X % tile.Width) + tile.Width) % tile.Width;
            float offsetY = ((worldOffset.Y % tile.Height) + tile.Height) % tile.Height;

            for (float x = -offsetX - tile.Width; x < screenW + tile.Width; x += tile.Width)
            {
                for (float y = -offsetY - tile.Height; y < screenH + tile.Height; y += tile.Height)
                {
                    spriteBatch.Draw(tile, new Vector2(x, y), Color.White);
                }
            }

            // --- Layer 2: motion blur pojok, FIXED ke layar, cuma opacity yang berubah ---
            // Ganti ke Additive sebentar biar nge-"nyala nambah" ke atas tile (mirip efek
            // screen-blend di versi HTML), bukan numpuk transparan biasa. Rectangle-nya
            // full-viewport (bukan Main.screenWidth/Height) supaya nutup penuh walau render
            // target-nya lagi di-resize sama mod zoom.
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

            spriteBatch.Draw(corner, new Rectangle(0, 0, screenW, screenH), Color.White * currentOpacity);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone);
        }
    }

    // ============================================================================================
    // Registrasi + auto activate/deactivate ngikutin hidup-matinya boss (via FindRealBossIndex(),
    // jadi mirage decoy gak ikut ngetrigger/reset sky-nya).
    // ============================================================================================
    public class WhoAmISkySystem : ModSystem
    {
        private const string SkyKey = "TheSanity:WhoAmISky";
        private bool wasBossActive = false;

        public override void Load()
        {
            if (Main.dedServ) return; // server gak perlu render apapun
            SkyManager.Instance[SkyKey] = new WhoAmISky();
        }

        public override void Unload()
        {
            if (Main.dedServ) return;
            SkyManager.Instance[SkyKey]?.Deactivate();
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ) return;

            bool bossActive = WhoAmI.FindRealBossIndex() != -1;
            if (bossActive == wasBossActive) return; // cuma trigger pas transisi, gak tiap tick

            if (bossActive) SkyManager.Instance.Activate(SkyKey, Vector2.Zero);
            else SkyManager.Instance.Deactivate(SkyKey);

            wasBossActive = bossActive;
        }
    }
}