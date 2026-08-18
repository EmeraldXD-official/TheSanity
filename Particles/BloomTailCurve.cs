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
    /// Versi tail "BloomTail" yang BISA MELENGKUNG ngikutin riwayat gerak - beda
    /// sama <see cref="BloomTail"/> (1 quad kaku yang cuma rotate+stretch, gak
    /// bisa nekuk sama sekali karena cuma 2 titik/1 garis lurus).
    ///
    /// Teknik: sama kayak DualLineTrail (triangle-strip dari banyak titik riwayat
    /// posisi, dihalusin pake Catmull-Rom), TAPI beda cara sampling teksturnya:
    /// - DualLineTrail: tekstur di-TILING/diulang-ulang sepanjang trail (karena
    ///   isinya pola garis yang emang didesain buat diulang).
    /// - BloomTailCurve (INI): tekstur di-STRETCH SEKALI PENUH (V: 0 -> 1) rata
    ///   di sepanjang KESELURUHAN kurva, gak diulang - karena BloomTail.png itu
    ///   1 gambar utuh yang emang udah punya bentuk depan-gembung/ekor-ngerucut
    ///   bawaan, sama kayak yang dipakai di BloomTail.cs (versi kaku), cuma di
    ///   sini jalur yang dilewatinnya melengkung, bukan garis lurus.
    ///
    /// ORIENTASI SPRITE (WAJIB SESUAI ART ASLI "BloomTail.png", sama kayak
    /// BloomTail.cs): bagian DEPAN (gembung/lebar) ada di TEPI BAWAH gambar,
    /// makin ke ATAS gambar makin ngerucut (ekor).
    /// </summary>
    public static class BloomTailCurve
    {
        private const string TexturePath = "TheSanity/Particles/BloomTail";

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
        /// Titik posisi world (riwayat Projectile.Center per tick). Index 0 HARUS
        /// titik paling baru (kepala/depan tail), index terakhir = paling lama
        /// (arah ekor/belakang).
        /// </param>
        /// <param name="widthFunc">
        /// Lebar tail (pixel) sebagai fungsi dari progress (0 = kepala, 1 = ekor).
        /// Karena BloomTail.png SUDAH punya taper bawaan di gambarnya sendiri
        /// (gembung->ngerucut), biasanya cukup pakai lebar KONSTAN di sini
        /// (mis. <c>p => 20f</c>) dan biarin tekstur yang ngurus efek
        /// menyempitnya - kecuali kamu mau nge-double-taper (mengecilkan lebar
        /// geometrinya JUGA), baru pakai fungsi yang nurun.
        /// </param>
        /// <param name="colorFunc">
        /// Warna+opacity sebagai fungsi dari progress (0 = kepala, 1 = ekor).
        /// </param>
        /// <param name="maxLength">Panjang maksimal tail dalam pixel.</param>
        /// <param name="segmentsPerPoint">Kehalusan interpolasi Catmull-Rom.</param>
        /// <param name="sourceRect">
        /// Optional crop VERTIKAL (cuma Top &amp; Height yang dipakai, Left/Width
        /// SENGAJA diabaikan - pelajaran dari DualLineTrail: crop horizontal yang
        /// gak presisi center bikin strip "berat sebelah"/miring tergantung arah
        /// gerak). Biarin null kalau BloomTail.png gak ada padding.
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

        // Identik sama BuildSmoothPath di DualLineTrail.cs - potong path sesuai
        // maxLength lalu haluskan pake Catmull-Rom. Lihat komentar lengkap di sana.
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

            // U (lebar/sisi kiri-kanan strip) SENGAJA selalu full 0->1 - alasan
            // sama persis kayak fix di DualLineTrail: kalau di-crop horizontal
            // dan crop-nya gak presisi center, sisi kiri & kanan strip nyample
            // area tekstur yang gak simetris -> strip keliatan berat sebelah,
            // dan biasnya ikut berputar sesuai arah gerak (persis gejala
            // "miring tergantung arah" yang sempat kejadian).
            const float uLeft = 0f;
            const float uRight = 1f;

            // V TIDAK di-tiling di sini (beda dari DualLineTrail) - progress 0
            // (kepala/depan) dipetakan ke v=1 (tepi BAWAH tekstur = bagian
            // gembung/depan sesuai orientasi BloomTail.png), progress 1 (ekor)
            // dipetakan ke v=0 (tepi ATAS tekstur = bagian ngerucut). Dengan gini
            // seluruh gambar BloomTail (dan taper bawaannya) di-stretch RATA
            // menutupi keseluruhan panjang kurva, persis kayak yang kejadian di
            // BloomTail.cs versi kaku, cuma sekarang jalurnya boleh melengkung.
            Rectangle cropRect = rect;
            float vTopEdge = cropRect.Top / (float)tex.Height;
            float vSpan = cropRect.Height / (float)tex.Height;
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

                // progress=0 (kepala) -> v=vBottomEdge (tepi bawah crop, bagian
                // depan/gembung). progress=1 (ekor) -> v=vTopEdge (tepi atas
                // crop, bagian ngerucut).
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

            // AlphaBlend default (BloomTail.png konfirmasi PNG transparan biasa,
            // BUKAN trik BG hitam kayak DualLine.png). Clamp (bukan Wrap) karena
            // V di sini SENGAJA gak pernah keluar dari [vTopEdge, vBottomEdge].
            gd.BlendState = BlendState.AlphaBlend;
            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState = RasterizerState.CullNone;
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
        /// Panggil ini dari Unload() class Mod utama, sama kayak DualLineTrail.Unload().
        /// </summary>
        public static void Unload()
        {
            effect?.Dispose();
            effect = null;
            texAsset = null;
        }
    }
}
