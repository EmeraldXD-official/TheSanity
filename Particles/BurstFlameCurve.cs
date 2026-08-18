using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Particles
{
    /// <summary>
    /// Tail melengkung pake sprite "BurstFlame" (446x961, BG HITAM PEKAT, bentuk
    /// nge-taper kayak BloomTail - gembung di 1 ujung, ngerucut di ujung lain).
    ///
    /// Ini gabungan 2 teknik yang udah dipakai sebelumnya:
    /// - Geometri: SAMA PERSIS kayak BloomTailCurve - triangle-strip dari riwayat
    ///   posisi, dihalusin Catmull-Rom, tekstur di-STRETCH SEKALI PENUH (gak
    ///   di-tiling) di sepanjang kurva, biar bentuk taper bawaan gambarnya
    ///   kepakai utuh dan bisa BENERAN MELENGKUNG ngikutin lintasan belok.
    /// - Blending: SAMA PERSIS kayak DualLineTrail - BlendState.Additive, karena
    ///   BG hitam pekat (0,0,0) di-additive-kan ke layar = gak kegambar sama
    ///   sekali, yang nongol cuma bagian nyala/terang dari sprite-nya. Makanya
    ///   fade WAJIB lewat kecerahan RGB (`Color.White * opacity`), BUKAN cuma
    ///   nurunin channel Alpha doang - kalau cuma alpha, additive bakal ngabaiin
    ///   itu dan hasilnya keliatan gak pernah fade.
    ///
    /// ORIENTASI SPRITE: asumsi SAMA kayak BloomTail.png - bagian DEPAN
    /// (gembung/lebar/terang) ada di TEPI BAWAH gambar, makin ke ATAS makin
    /// ngerucut/ngecil (ekor). KALAU TERNYATA KEBALIK di BurstFlame.png kamu,
    /// gampang dibalik - tinggal tukar <c>vBottomEdge</c> &lt;-&gt; <c>vTopEdge</c>
    /// di baris <c>MathHelper.Lerp</c> paling bawah DrawStrip.
    /// </summary>
    public static class BurstFlameCurve
    {
        private const string TexturePath = "TheSanity/Particles/BurstFlame";

        private static Asset<Texture2D> texAsset;
        private static BasicEffect effect;

        private static Texture2D Tex
        {
            get
            {
                texAsset ??= ModContent.Request<Texture2D>(TexturePath, AssetRequestMode.ImmediateLoad);
                return texAsset.Value;
            }
        }

        /// <summary>
        /// Gambar tail melengkung mengikuti titik-titik posisi WORLD.
        /// </summary>
        /// <param name="points">
        /// Riwayat Projectile.Center per tick. Index 0 = titik paling baru
        /// (kepala/depan), index terakhir = paling lama (ekor/belakang).
        /// </param>
        /// <param name="widthFunc">
        /// Lebar (pixel) sebagai fungsi progress (0=kepala, 1=ekor). Biasanya
        /// konstan aja (mis. <c>p => 30f</c>) - biarin taper visual diurus sama
        /// bentuk tekstur BurstFlame.png sendiri, kecuali mau nge-double-taper.
        /// </param>
        /// <param name="colorFunc">
        /// Warna+KECERAHAN (bukan cuma alpha - lihat catatan additive di atas)
        /// sebagai fungsi progress.
        /// </param>
        /// <param name="maxLength">Panjang maksimal tail dalam pixel.</param>
        /// <param name="segmentsPerPoint">Kehalusan interpolasi Catmull-Rom.</param>
        /// <param name="sourceRect">
        /// Optional crop VERTIKAL doang (cuma Top &amp; Height dipakai, Left/Width
        /// diabaikan sengaja - biar strip gak "berat sebelah" kalau crop-nya gak
        /// presisi center, lihat histori bug yang sama di DualLineTrail).
        /// </param>
        public static void Draw(
            IList<Vector2> points,
            Func<float, float> widthFunc,
            Func<float, Color> colorFunc,
            float maxLength,
            int segmentsPerPoint = 8,
            Rectangle? sourceRect = null)
        {
            if (points == null || points.Count < 2 || widthFunc == null || colorFunc == null)
                return;

            List<Vector2> smoothPath = BuildSmoothPath(points, maxLength, segmentsPerPoint);
            if (smoothPath.Count < 2)
                return;

            DrawStrip(smoothPath, widthFunc, colorFunc, sourceRect);
        }

        private static List<Vector2> BuildSmoothPath(IList<Vector2> raw, float maxLength, int segmentsPerPoint)
        {
            var clean = new List<Vector2>(raw.Count);
            foreach (Vector2 p in raw)
            {
                if (clean.Count == 0 || Vector2.DistanceSquared(clean[clean.Count - 1], p) > 1f)
                    clean.Add(p);
            }

            if (clean.Count < 2)
                return clean;

            var clipped = new List<Vector2> { clean[0] };
            float accumulated = 0f;
            for (int i = 1; i < clean.Count; i++)
            {
                float segLen = Vector2.Distance(clean[i - 1], clean[i]);
                if (accumulated + segLen >= maxLength)
                {
                    float remaining = maxLength - accumulated;
                    float t = segLen > 0.0001f ? remaining / segLen : 0f;
                    clipped.Add(Vector2.Lerp(clean[i - 1], clean[i], MathHelper.Clamp(t, 0f, 1f)));
                    break;
                }

                clipped.Add(clean[i]);
                accumulated += segLen;
            }

            if (clipped.Count <= 2)
                return clipped;

            var smooth = new List<Vector2>();
            int count = clipped.Count;
            for (int i = 0; i < count - 1; i++)
            {
                Vector2 p0 = i == 0 ? clipped[0] + (clipped[0] - clipped[1]) : clipped[i - 1];
                Vector2 p1 = clipped[i];
                Vector2 p2 = clipped[i + 1];
                Vector2 p3 = i + 2 < count ? clipped[i + 2] : clipped[i + 1] + (clipped[i + 1] - clipped[i]);

                for (int s = 0; s < segmentsPerPoint; s++)
                {
                    float t = s / (float)segmentsPerPoint;
                    smooth.Add(Vector2.CatmullRom(p0, p1, p2, p3, t));
                }
            }
            smooth.Add(clipped[count - 1]);

            return smooth;
        }

        private static void DrawStrip(List<Vector2> path, Func<float, float> widthFunc, Func<float, Color> colorFunc, Rectangle? sourceRect)
        {
            int count = path.Count;

            var cumulative = new float[count];
            cumulative[0] = 0f;
            for (int i = 1; i < count; i++)
                cumulative[i] += cumulative[i - 1] + Vector2.Distance(path[i - 1], path[i]);

            float totalLength = cumulative[count - 1];
            if (totalLength <= 0.0001f)
                return;

            Texture2D tex = Tex;
            Rectangle rect = sourceRect ?? new Rectangle(0, 0, tex.Width, tex.Height);

            // U selalu full 0->1 (gak di-crop) - alasan sama kayak BloomTailCurve
            // & fix DualLineTrail: crop horizontal yang gak presisi center bikin
            // strip berat sebelah, dan biasnya ikut berputar sesuai arah gerak.
            const float uLeft = 0f;
            const float uRight = 1f;

            // V di-stretch penuh non-tiling (gak diulang) di sepanjang kurva -
            // progress 0 (kepala) -> tepi bawah crop (bagian gembung/depan),
            // progress 1 (ekor) -> tepi atas crop (bagian ngerucut/belakang).
            float vTopEdge = rect.Top / (float)tex.Height;
            float vSpan = rect.Height / (float)tex.Height;
            float vBottomEdge = vTopEdge + vSpan;

            var vertices = new VertexPositionColorTexture[count * 2];

            for (int i = 0; i < count; i++)
            {
                float progress = cumulative[i] / totalLength;

                Vector2 dir;
                if (i == 0)
                    dir = path[1] - path[0];
                else if (i == count - 1)
                    dir = path[i] - path[i - 1];
                else
                    dir = path[i + 1] - path[i - 1];

                if (dir.LengthSquared() < 0.0001f)
                    dir = Vector2.UnitX;
                dir.Normalize();

                Vector2 normal = new Vector2(-dir.Y, dir.X);

                float halfWidth = widthFunc(progress) * 0.5f;
                Color color = colorFunc(progress);

                Vector2 top = path[i] + normal * halfWidth;
                Vector2 bottom = path[i] - normal * halfWidth;

                float v = MathHelper.Lerp(vBottomEdge, vTopEdge, progress);

                vertices[i * 2] = new VertexPositionColorTexture(new Vector3(top, 0f), color, new Vector2(uLeft, v));
                vertices[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(bottom, 0f), color, new Vector2(uRight, v));
            }

            GraphicsDevice gd = Main.instance.GraphicsDevice;
            EnsureEffect(gd);

            effect.Texture = tex;
            effect.World = Matrix.CreateTranslation(-Main.screenPosition.X, -Main.screenPosition.Y, 0f) * Main.GameViewMatrix.TransformationMatrix;
            effect.View = Matrix.Identity;
            effect.Projection = Matrix.CreateOrthographicOffCenter(0, gd.Viewport.Width, gd.Viewport.Height, 0, -1f, 1f);

            var oldBlend = gd.BlendState;
            var oldDepth = gd.DepthStencilState;
            var oldRaster = gd.RasterizerState;
            var oldSampler = gd.SamplerStates[0];

            // Additive (BUKAN AlphaBlend) - beda dari BloomTailCurve, karena
            // BurstFlame.png pake trik BG hitam pekat, sama kayak DualLine.png.
            gd.BlendState = BlendState.Additive;
            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState = RasterizerState.CullNone;
            // Clamp (bukan Wrap) - V di sini SENGAJA gak pernah keluar dari
            // [vTopEdge, vBottomEdge] karena gak di-tiling.
            gd.SamplerStates[0] = SamplerState.LinearClamp;

            foreach (EffectPass pass in effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                gd.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices, 0, vertices.Length - 2);
            }

            gd.BlendState = oldBlend;
            gd.DepthStencilState = oldDepth;
            gd.RasterizerState = oldRaster;
            gd.SamplerStates[0] = oldSampler;
        }

        private static void EnsureEffect(GraphicsDevice gd)
        {
            effect ??= new BasicEffect(gd)
            {
                VertexColorEnabled = true,
                TextureEnabled = true,
            };
        }

        /// <summary>
        /// Panggil ini dari Unload() class Mod utama, sama kayak
        /// DualLineTrail.Unload() / BloomTailCurve.Unload().
        /// </summary>
        public static void Unload()
        {
            effect?.Dispose();
            effect = null;
            texAsset = null;
        }
    }
}
