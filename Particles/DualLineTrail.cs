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
    /// Trail generik pake sprite "DualLine" (392x392, BG HITAM PEKAT, isinya 2 garis
    /// putih yang ngarah ke atas kayak tanda kutip ["]).
    ///
    /// DIRENDER PAKE PRIMITIVE TRIANGLE-STRIP (GraphicsDevice.DrawUserPrimitives),
    /// BUKAN numpuk banyak spriteBatch.Draw() per titik. Ini kuncinya biar belokan
    /// trail-nya SMOOTH dan gak "pecah"/patah-patah kayak nyusun quad kaku - strip-nya
    /// nyambung mulus dari 1 vertex ke vertex berikutnya, dan titik mentahnya (raw
    /// oldPos dari projectile/NPC yang biasanya jarang & patah-patah) di-interpolasi
    /// dulu pake Catmull-Rom spline sebelum jadi mesh, jadi hasilnya kurva halus
    /// walaupun sumber datanya cuma beberapa titik per belokan tajam.
    ///
    /// SOAL BG HITAM: sprite-nya digambar pake BlendState.Additive. Warna hitam pekat
    /// (0,0,0) kalau di-additive-kan ke layar = nambahin nol, alias GAK KEGAMBAR SAMA
    /// SEKALI. Yang beneran nongol di layar cuma 2 garis putihnya, dan hasilnya keliatan
    /// kayak "nyala"/glow, bukan kotak hitam nutupin apa-apa.
    ///
    /// PENTING soal warna/fade: karena additive (BlendState.Additive = ColorSourceBlend
    /// One, gak ngitung alpha kayak AlphaBlend biasa), fade-nya HARUS dikerjain lewat
    /// KECERAHAN warna (RGB), bukan cuma nurunin channel Alpha doang. Makanya di
    /// colorFunc, kalau mau progress tertentu makin transparan, kalikan RGB-nya
    /// (misal `Color.White * opacity` - operator * di XNA/FNA Color otomatis ngalikan
    /// RGB DAN Alpha bareng, jadi ini udah bener/setara "premultiplied alpha").
    /// JANGAN cuma pake `new Color(255,255,255, alphaSaja)` soalnya alpha-nya bakal
    /// diabaikan additive blend dan hasilnya trail keliatan gak pernah fade.
    ///
    /// SOAL sourceRect (BARU): kalau PNG sumbernya (392x392) punya padding/margin
    /// transparan/hitam di sekitar 2 garis aslinya, area kosong itu ikut ke-tiling
    /// pas di-repeat sepanjang trail -> keliatan "putus"/kepotong tiap 1 putaran
    /// tekstur. sourceRect biarin kamu crop UV cuma ke area yang BENERAN ada
    /// garisnya (dalam pixel, koordinat asli tekstur), jadi padding-nya dibuang
    /// dan tiling-nya jadi rapat/nyambung terus.
    /// </summary>
    public static class DualLineTrail
    {
        private const string TexturePath = "TheSanity/Particles/DualLine";

        private static Asset<Texture2D> texAsset;
        private static BasicEffect effect;

        // Default panjang trail = 15 blok (1 blok/tile = 16px) = 240px.
        // Caller BEBAS override lewat parameter maxLength di Draw(...).
        public const float DefaultMaxLength = 15f * 16f;

        private static Texture2D Tex
        {
            get
            {
                texAsset ??= ModContent.Request<Texture2D>(TexturePath, AssetRequestMode.ImmediateLoad);
                return texAsset.Value;
            }
        }

        /// <summary>
        /// Gambar trail smooth mengikuti titik-titik posisi WORLD.
        /// </summary>
        /// <param name="points">
        /// Titik posisi world (misal riwayat Projectile.Center / NPC.Center per tick).
        /// Index 0 HARUS titik paling baru (kepala trail), index terakhir = titik paling
        /// lama (arah ekor).
        /// </param>
        /// <param name="widthFunc">
        /// Lebar trail (dalam pixel) sebagai fungsi dari progress (0 = kepala, 1 = ekor
        /// / sejauh maxLength). Misal: <c>p => MathHelper.Lerp(20f, 2f, p)</c> biar
        /// gede di kepala terus ngerucut ke ekor.
        /// </param>
        /// <param name="colorFunc">
        /// Warna+kecerahan trail sebagai fungsi dari progress (0 = kepala, 1 = ekor).
        /// Di sinilah efek "makin jauh makin ilang" smooth diatur - lihat catatan
        /// additive-blend di ringkasan class di atas. Contoh:
        /// <c>p => Color.White * (1f - p * p)</c> (fade kuadratik, awalnya pelan
        /// abis itu cepet ngilang).
        /// </param>
        /// <param name="maxLength">
        /// Panjang maksimal trail dalam pixel. Default 15 blok (240px) - INI YANG BISA
        /// DIATUR DARI FILE PEMANGGIL sesuai request, tinggal isi parameter ini.
        /// </param>
        /// <param name="segmentsPerPoint">
        /// Berapa banyak sub-titik Catmull-Rom yang disisipkan di antara 2 titik
        /// mentah. Makin gede makin mulus tapi makin berat (default 8 biasanya udah
        /// lebih dari cukup halus).
        /// </param>
        /// <param name="textureRepeatLength">
        /// Berapa pixel panjang trail yang setara 1x pengulangan tekstur (V: 0 -> 1,
        /// karena sprite DualLine vertikal). Kalau garis di sprite keliatan
        /// ke-stretch/gepeng, kecilin angka ini; kalau keliatan ke-squeeze/numpuk,
        /// gedein.
        /// </param>
        /// <param name="sourceRect">
        /// Area crop dalam KOORDINAT PIXEL ASLI tekstur (bukan 0..1) yang beneran
        /// berisi garis putihnya, buat buang padding/margin kosong ATAS-BAWAH di
        /// sekitarnya. HANYA <c>Top</c> dan <c>Height</c> yang dipakai - <c>Left</c>
        /// dan <c>Width</c> SENGAJA DIABAIKAN (lebar tekstur selalu dipakai penuh
        /// 0..1), karena axis itu cuma disample sekali/gak di-repeat jadi gak
        /// butuh crop, dan crop horizontal yang gak presis center malah bisa bikin
        /// trail keliatan "berat sebelah"/miring tergantung arah gerak. Biarin
        /// null buat pakai seluruh tinggi tekstur (gak ada crop vertikal juga).
        /// Contoh kalau garisnya cuma ngisi baris tengah 392x392 dengan padding
        /// atas-bawah ~40px: <c>new Rectangle(0, 40, 0, 312)</c> (nilai Left/Width
        /// bebas diisi apa aja, gak dipakai).
        /// </param>
        public static void Draw(
            IList<Vector2> points,
            Func<float, float> widthFunc,
            Func<float, Color> colorFunc,
            float maxLength = DefaultMaxLength,
            int segmentsPerPoint = 8,
            float textureRepeatLength = 64f,
            Rectangle? sourceRect = null)
        {
            if (points == null || points.Count < 2 || widthFunc == null || colorFunc == null)
                return;

            List<Vector2> smoothPath = BuildSmoothPath(points, maxLength, segmentsPerPoint);
            if (smoothPath.Count < 2)
                return;

            DrawStrip(smoothPath, widthFunc, colorFunc, textureRepeatLength, sourceRect);
        }

        // ------------------------------------------------------------------
        // 1. Potong path mentah supaya panjang totalnya gak lebih dari maxLength,
        //    lalu haluskan pake Catmull-Rom spline (subdivide tiap segmen).
        // ------------------------------------------------------------------
        private static List<Vector2> BuildSmoothPath(IList<Vector2> raw, float maxLength, int segmentsPerPoint)
        {
            // Buang titik yang ke-duplikat/kegedeketan (jarak ~0), biar Catmull-Rom
            // gak dapet 2 titik sama persis (bisa bikin arah normal jadi NaN).
            var clean = new List<Vector2>(raw.Count);
            foreach (Vector2 p in raw)
            {
                if (clean.Count == 0 || Vector2.DistanceSquared(clean[clean.Count - 1], p) > 1f)
                    clean.Add(p);
            }

            if (clean.Count < 2)
                return clean;

            // Potong path berdasarkan panjang kumulatif, biar trail gak lebih
            // panjang dari maxLength - titik terakhir di-lerp presisi ke titik
            // pemotongan biar ujung ekornya gak "loncat".
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

            // Catmull-Rom: tiap pasang titik (p1,p2) di-subdivide, pake tetangga
            // (p0,p3) buat nentuin lengkungannya - hasil akhirnya kurva mulus yang
            // tetep ngelewatin semua titik mentah aslinya (bukan cuma ngedeketin).
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

        // ------------------------------------------------------------------
        // 2. Bangun triangle-strip dari path halus (2 vertex per titik: kiri &
        //    kanan normal-nya) lalu gambar pake BasicEffect + BlendState.Additive.
        // ------------------------------------------------------------------
        private static void DrawStrip(List<Vector2> path, Func<float, float> widthFunc, Func<float, Color> colorFunc, float textureRepeatLength, Rectangle? sourceRect)
        {
            int count = path.Count;

            // Panjang kumulatif per titik - dipakai buat progress (0..1, buat
            // width/color) DAN buat V texture coord (biar tekstur tiling rapi
            // ngikutin jarak asli, bukan cuma index titik).
            var cumulative = new float[count];
            cumulative[0] = 0f;
            for (int i = 1; i < count; i++)
                cumulative[i] += cumulative[i - 1] + Vector2.Distance(path[i - 1], path[i]);

            float totalLength = cumulative[count - 1];
            if (totalLength <= 0.0001f)
                return;

            // ----------------------------------------------------------------
            // Konversi sourceRect (pixel asli tekstur) jadi UV ternormalisasi
            // (0..1). Kalau sourceRect null, ya pakai seluruh tekstur (0..1 penuh)
            // -> perilakunya identik kayak sebelum ada fitur crop ini.
            //
            // PENTING - KENAPA U (uLeft/uRight) SENGAJA GAK IKUT DI-CROP:
            // U itu sisi "lebar" strip (top vertex vs bottom vertex), dan ini cuma
            // disample SEKALI per titik (0 -> 1, gak di-repeat sepanjang trail).
            // Karena gak di-repeat, U gak butuh crop buat masalah tiling apa pun.
            //
            // Kalau U ikut di-crop pakai rect.Left/rect.Width yang GAK PRESIS
            // center-nya (misal cuma tebakan kasar), sisi "top" dan sisi "bottom"
            // strip jadi nyample bagian tekstur yang gak simetris satu sama lain -
            // trail keliatan "berat sebelah". Dan karena arah top/bottom itu
            // NORMAL VECTOR yang berputar ngikutin arah gerak projectile, bias
            // beratnya itu ikut BERPUTAR juga sesuai arah tembak (persis gejala
            // "meleset beda arah beda sisi" yang biasanya muncul kalau ini salah).
            //
            // Makanya U DIPAKSA selalu 0->1 (FULL lebar tekstur), gak peduli
            // sourceRect diisi apa. Yang di-crop cuma V (vTop/vSpan) - sisi
            // "panjang" strip yang emang DI-REPEAT sepanjang trail, jadi cuma di
            // situ padding perlu dibuang biar gak ke-tiling "putus".
            // ----------------------------------------------------------------
            Texture2D tex = Tex;
            Rectangle rect = sourceRect ?? new Rectangle(0, 0, tex.Width, tex.Height);

            const float uLeft = 0f;
            const float uRight = 1f;
            float vTop = rect.Top / (float)tex.Height;
            float vSpan = rect.Height / (float)tex.Height;

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

                // v mentah 0..1 (posisi di dalam 1x repeat), lalu dipetakan ke
                // rentang crop [vTop, vTop+vSpan] biar padding di luar sourceRect
                // gak pernah ikut ke-sample.
                float vRaw = (cumulative[i] / textureRepeatLength) % 1f;
                float v = vTop + vRaw * vSpan;

                vertices[i * 2] = new VertexPositionColorTexture(new Vector3(top, 0f), color, new Vector2(uLeft, v));
                vertices[i * 2 + 1] = new VertexPositionColorTexture(new Vector3(bottom, 0f), color, new Vector2(uRight, v));
            }

            GraphicsDevice gd = Main.instance.GraphicsDevice;
            EnsureEffect(gd);

            effect.Texture = tex;
            // World: geser dari world-space ke screen-space (relatif Main.screenPosition)
            // DAN ikutin zoom kamera (Main.GameViewMatrix) - biar trail gak "ngambang"
            // salah posisi/skala pas player lagi zoom in/out.
            effect.World = Matrix.CreateTranslation(-Main.screenPosition.X, -Main.screenPosition.Y, 0f) * Main.GameViewMatrix.TransformationMatrix;
            effect.View = Matrix.Identity;
            effect.Projection = Matrix.CreateOrthographicOffCenter(0, gd.Viewport.Width, gd.Viewport.Height, 0, -1f, 1f);

            var oldBlend = gd.BlendState;
            var oldDepth = gd.DepthStencilState;
            var oldRaster = gd.RasterizerState;
            var oldSampler = gd.SamplerStates[0];

            gd.BlendState = BlendState.Additive;
            gd.DepthStencilState = DepthStencilState.None;
            gd.RasterizerState = RasterizerState.CullNone;
            // Wrap (bukan Clamp) - WAJIB biar tekstur bisa berulang di sumbu V
            // (sepanjang trail, sesuai textureRepeatLength) tanpa nyangkut di tepi 0/1.
            // Catatan: karena v yang dikirim ke GPU sekarang selalu di dalam rentang
            // [vTop, vTop+vSpan] (bukan mentah 0..1 lagi), si Wrap ini gak akan
            // pernah "nyentuh" ulang area padding di luar crop - repeat-nya tetep
            // bener walau sourceRect-nya cuma sepotong kecil dari tekstur.
            gd.SamplerStates[0] = SamplerState.LinearWrap;

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
        /// Panggil ini dari Unload() class Mod utama (mis. TheSanity.cs), biar
        /// BasicEffect-nya di-dispose bersih tiap kali mod di-reload/dimatiin -
        /// GraphicsDevice-nya gak "nyangkut" ke instance lama.
        /// </summary>
        public static void Unload()
        {
            effect?.Dispose();
            effect = null;
            texAsset = null;
        }
    }
}
