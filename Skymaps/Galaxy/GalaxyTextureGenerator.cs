using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheSanity.Content.Skies
{
    /// <summary>
    /// Membangun semua tekstur yang dibutuhkan GalaxySky langsung di memori (procedural),
    /// jadi tidak perlu file .png tambahan. Ini bikin ukuran mod kecil dan waktu load cepat,
    /// cocok untuk device spek rendah ("kentang").
    ///
    /// Tekstur dibuat SEKALI SAJA (di-cache oleh GalaxySky), bukan setiap frame.
    ///
    /// Versi ini menambahkan dua tekstur baru dibanding sebelumnya:
    ///   - streak  : dipakai untuk garis bintang jatuh (shooting star) yang memudar ke satu ujung
    ///   - vignette: dipakai untuk menggelapkan tepi layar (efek "vignette") supaya galaxy
    ///               terasa lebih dramatis dan fokus ke tengah
    /// </summary>
    public static class GalaxyTextureGenerator
    {
        public static void Generate(
            GraphicsDevice device,
            out Texture2D galaxyHaze,
            out Texture2D softGlow,
            out Texture2D particle,
            out Texture2D solidPixel,
            out Texture2D streak,
            out Texture2D vignette)
        {
            galaxyHaze = BuildGalaxyHaze(device, 320);
            softGlow = BuildSoftGlow(device, 160);
            particle = BuildParticle(device, 16);
            solidPixel = BuildSolidPixel(device);
            streak = BuildStreak(device, 64, 10);
            vignette = BuildVignette(device, 256);
        }

        /// <summary>
        /// Tekstur "haze" lembut di belakang titik-titik spiral: gradasi
        /// pusat (emas hangat / putih) -> ungu -> biru gelap di tepi.
        /// Ini bukan lagi elemen utama (dulu jadi galaxy inti tunggal), sekarang
        /// cuma lapisan halus di bawah titik-titik bintang spiral supaya ada
        /// "isi"/kabut di antara lengan galaxy, biar tidak terlihat kosong.
        /// </summary>
        private static Texture2D BuildGalaxyHaze(GraphicsDevice device, int size)
        {
            var data = new Color[size * size];
            Vector2 center = new Vector2(size / 2f);
            float maxRadius = size / 2f;

            // Palet warna baru: emas hangat di pusat -> ungu/pink di tengah -> biru gelap di tepi
            Color cCenter = new Color(255, 245, 210); // inti: putih keemasan hangat
            Color cInner = new Color(255, 195, 130);  // emas/oranye lembut
            Color cMid = new Color(175, 95, 230);     // ungu-pink (blend khas lengan spiral)
            Color cOuter = new Color(35, 45, 130);    // biru gelap dengan sentuhan ungu

            const int arms = 3;
            const float armTightness = 4.2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Vector2 pos = new Vector2(x, y) - center;
                    float dist = pos.Length();

                    if (dist > maxRadius)
                    {
                        data[y * size + x] = Color.Transparent;
                        continue;
                    }

                    float t = MathHelper.Clamp(dist / maxRadius, 0f, 1f);
                    float angle = (float)Math.Atan2(pos.Y, pos.X);

                    // Mask lengan spiral logaritmik. Sama seperti sebelumnya, kita redam
                    // pola ini di dekat pusat supaya tidak "pecah"/noise, digantikan
                    // solid bright core yang mulus.
                    float spiral = (float)Math.Sin(arms * (angle + armTightness * (float)Math.Log(dist + 1)));
                    float armMask = MathHelper.Clamp(spiral * 0.5f + 0.5f, 0f, 1f);
                    armMask = (float)Math.Pow(armMask, 2.2);

                    const float coreRadius = 0.16f;
                    float innerCoreBlend = MathHelper.Clamp(t / coreRadius, 0f, 1f);
                    innerCoreBlend = innerCoreBlend * innerCoreBlend * (3f - 2f * innerCoreBlend);
                    armMask = MathHelper.Lerp(1f, armMask, innerCoreBlend);

                    Color baseColor;
                    if (t < 0.15f)
                        baseColor = Color.Lerp(cCenter, cInner, t / 0.15f);
                    else if (t < 0.5f)
                        baseColor = Color.Lerp(cInner, cMid, (t - 0.15f) / 0.35f);
                    else
                        baseColor = Color.Lerp(cMid, cOuter, (t - 0.5f) / 0.5f);

                    float coreBoost = 1f + (1f - innerCoreBlend) * 1.6f;
                    float brightness = MathHelper.Lerp(0.35f, 1f, armMask) * (1f - t * 0.5f) * coreBoost;

                    // Alpha dasar sedikit diturunkan (0.85x) dibanding versi lama karena tekstur
                    // ini sekarang cuma jadi "kabut" di bawah titik-titik bintang, bukan elemen utama.
                    float alpha = MathHelper.Clamp(1.05f - t, 0f, 1f) * MathHelper.Lerp(0.5f, 1f, armMask) * 0.85f;
                    alpha = MathHelper.Lerp(alpha, 0.9f, 1f - innerCoreBlend);

                    Vector3 rgb = baseColor.ToVector3() * brightness;
                    data[y * size + x] = new Color(rgb) * alpha;
                }
            }

            var tex = new Texture2D(device, size, size);
            tex.SetData(data);
            return tex;
        }

        /// <summary>Bulatan cahaya lembut (radial gradient), dipakai untuk glow, halo, ring, dan ray.</summary>
        private static Texture2D BuildSoftGlow(GraphicsDevice device, int size)
        {
            var data = new Color[size * size];
            Vector2 center = new Vector2(size / 2f);
            float maxRadius = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = MathHelper.Clamp(dist / maxRadius, 0f, 1f);
                    float a = (float)Math.Pow(MathHelper.Clamp(1f - t, 0f, 1f), 2.0);
                    data[y * size + x] = Color.White * a;
                }
            }

            var tex = new Texture2D(device, size, size);
            tex.SetData(data);
            return tex;
        }

        /// <summary>Titik partikel kecil bulat: dipakai untuk bintang, titik-titik lengan spiral, dan debu.</summary>
        private static Texture2D BuildParticle(GraphicsDevice device, int size)
        {
            var data = new Color[size * size];
            Vector2 center = new Vector2(size / 2f);
            float maxRadius = size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = MathHelper.Clamp(dist / maxRadius, 0f, 1f);
                    float a = (float)Math.Pow(MathHelper.Clamp(1f - t, 0f, 1f), 1.5);
                    data[y * size + x] = Color.White * a;
                }
            }

            var tex = new Texture2D(device, size, size);
            tex.SetData(data);
            return tex;
        }

        /// <summary>Tekstur 1x1 putih solid, dipakai untuk mengisi seluruh layar dengan warna (background wash).</summary>
        private static Texture2D BuildSolidPixel(GraphicsDevice device)
        {
            var tex = new Texture2D(device, 1, 1);
            tex.SetData(new[] { Color.White });
            return tex;
        }

        /// <summary>
        /// Garis pendek yang memudar dari transparan (ekor, sisi kiri) ke terang (kepala, sisi kanan),
        /// dengan falloff lembut secara vertikal supaya bentuknya seperti jejak komet/bintang jatuh.
        /// Digambar dengan origin di ujung kanan (kepala) supaya gampang diarahkan sesuai kecepatannya.
        /// </summary>
        private static Texture2D BuildStreak(GraphicsDevice device, int width, int height)
        {
            var data = new Color[width * height];
            float halfH = height / 2f;

            for (int y = 0; y < height; y++)
            {
                float vT = Math.Abs(y - halfH) / halfH;
                float vFall = (float)Math.Pow(MathHelper.Clamp(1f - vT, 0f, 1f), 1.4);

                for (int x = 0; x < width; x++)
                {
                    float hT = x / (float)(width - 1); // 0 = ekor, 1 = kepala
                    float hFall = (float)Math.Pow(hT, 1.6);
                    float a = hFall * vFall;
                    data[y * width + x] = Color.White * a;
                }
            }

            var tex = new Texture2D(device, width, height);
            tex.SetData(data);
            return tex;
        }

        /// <summary>
        /// Mask radial untuk vignette: transparan di tengah, makin gelap/opaque ke arah tepi.
        /// Warnanya putih polos (alpha saja) supaya bisa ditint bebas saat digambar.
        /// </summary>
        private static Texture2D BuildVignette(GraphicsDevice device, int size)
        {
            var data = new Color[size * size];
            Vector2 center = new Vector2(size / 2f);
            // Pakai radius diagonal supaya sudut layar (yang paling jauh dari tengah) tetap ke-cover halus
            float maxRadius = (float)Math.Sqrt(2) * size / 2f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = Vector2.Distance(new Vector2(x, y), center);
                    float t = MathHelper.Clamp(dist / maxRadius, 0f, 1f);
                    float a = (float)Math.Pow(t, 2.1);
                    data[y * size + x] = Color.White * a;
                }
            }

            var tex = new Texture2D(device, size, size);
            tex.SetData(data);
            return tex;
        }
    }
}
