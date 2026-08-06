using System;
using System.Diagnostics;
using Microsoft.Xna.Framework;
using Terraria;

namespace TheSanity.Items
{
    /// <summary>
    /// Menghasilkan array "tinggi bar" (0..1 per band) yang berubah tiap frame, dengan
    /// karakter naik CEPAT & turun PELAN (gravity-style decay) khas bar visualizer musik.
    ///
    /// Lihat catatan jujur di BoomBodLith.cs soal kenapa ini bukan FFT asli dari audio game --
    /// intinya, ini simulasi ritme yang gain keseluruhannya ditarik dari slider volume asli
    /// (Main.musicVolume / soundVolume / ambientVolume) sesuai BoomBodMode yang dipilih, supaya
    /// paling nggak ada korelasi nyata: mute slider volume-nya di Settings -> bar ikut diam/pelan.
    /// </summary>
    public static class AudioSpectrumSampler
    {
        private static float[] _smoothed;
        private static float _time;
        private static float _debugTimer;

        // Set false lagi kalau udah kelar troubleshooting -- ini cuma buat ngeliat
        // angka mentah di chat, biar ketauan apakah datanya beneran berubah atau diem.
        private const bool DebugPrint = true;

        // Pakai Stopwatch manual (bukan GameTime) karena dipanggil dari PostDrawInterface,
        // yang tidak dikasih parameter GameTime.
        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static double _lastElapsedSeconds;
        private const float MaxDelta = 0.05f;

        private static float Hash11(float n)
        {
            float s = (float)Math.Sin(n) * 43758.5453f;
            return s - (float)Math.Floor(s);
        }

        private static float Noise(float t)
        {
            float ti = (float)Math.Floor(t);
            float tf = t - ti;
            float a = Hash11(ti);
            float b = Hash11(ti + 1f);
            float smooth = tf * tf * (3f - 2f * tf);
            return MathHelper.Lerp(a, b, smooth);
        }

        private static float ConsumeDelta()
        {
            double now = Clock.Elapsed.TotalSeconds;
            double delta = now - _lastElapsedSeconds;
            _lastElapsedSeconds = now;
            if (delta < 0d || delta > MaxDelta)
                delta = MaxDelta;
            return (float)delta;
        }

        private static float GetCategoryVolume(BoomBodMode mode)
        {
            return mode switch
            {
                BoomBodMode.Music => Main.musicVolume,
                BoomBodMode.Sound => Main.soundVolume,
                BoomBodMode.Ambient => Main.ambientVolume,
                _ => (Main.musicVolume + Main.soundVolume + Main.ambientVolume) / 3f,
            };
        }

        /// <summary>
        /// Ambil array tinggi bar (0..1) untuk jumlah band tertentu. Array yang dikembalikan
        /// adalah buffer statis yang di-reuse tiap panggilan -- jangan disimpan referensinya
        /// untuk dipakai belakangan, baca nilainya langsung di frame yang sama.
        ///
        /// Prioritas: pakai spectrum AUDIO REAL (AudioCaptureEngine, hasil FFT dari suara
        /// yang beneran keluar dari speaker) kalau tersedia. Kalau capture belum siap / gagal
        /// (misal bukan Windows, atau baru sedetik nyalain game jadi buffer belum penuh),
        /// otomatis jatuh ke simulasi ritme prosedural lama supaya visualizer tetap hidup,
        /// bukan diam kaku.
        /// </summary>
        public static float[] GetBands(BoomBodMode mode, int count)
        {
            if (_smoothed == null || _smoothed.Length != count)
                _smoothed = new float[count];

            float delta = ConsumeDelta();
            _time += delta;

            float categoryVolume = GetCategoryVolume(mode);

            AudioCaptureEngine.EnsureStarted(delta);
            float[] realSpectrum = AudioCaptureEngine.GetSpectrum(count);

            // Cuma dipakai buat debug print di bawah -- nyimpen nilai MENTAH (sebelum
            // di-smooth) dari 3 band contoh, biar ketauan apakah datanya beneran berubah
            // frame-ke-frame atau nggak.
            float debugRaw0 = 0f, debugRaw12 = 0f, debugRaw24 = 0f;

            for (int i = 0; i < count; i++)
            {
                float raw;

                if (realSpectrum != null)
                {
                    // SPECTRUM REAL: energi asli dari audio yang sedang diputar. Mode dipakai
                    // sebagai gain -- lihat catatan keterbatasan di AudioCaptureEngine soal
                    // kenapa pemisahan kategori murni tidak memungkinkan dari titik ini.
                    raw = realSpectrum[i] * categoryVolume;
                }
                else
                {
                    // FALLBACK SIMULASI (lihat versi sebelumnya) -- dipakai kalau capture
                    // audio real belum tersedia.
                    float freq = 0.6f + i * 0.09f;
                    float wave = 0.5f + 0.5f * (float)Math.Sin(_time * freq * 2.1f + i * 0.9f);
                    float noise = Noise(_time * (0.8f + i * 0.05f) + i * 13.7f);
                    raw = (wave * 0.35f + noise * 0.65f) * categoryVolume;

                    float bandShape = MathHelper.Lerp(1f, 0.4f, i / (float)Math.Max(1, count - 1));
                    raw *= bandShape;
                }

                raw = MathHelper.Clamp(raw, 0f, 1f);

                if (i == 0) debugRaw0 = raw;
                if (i == 12) debugRaw12 = raw;
                if (i == 24) debugRaw24 = raw;

                // Naik cepat, turun pelan -> kesan "jedag" yang tajam tapi tetap smooth turunnya.
                float riseLerp = MathHelper.Clamp(delta * 16f, 0f, 1f);
                float fallLerp = MathHelper.Clamp(delta * 3f, 0f, 1f);
                _smoothed[i] = raw > _smoothed[i]
                    ? MathHelper.Lerp(_smoothed[i], raw, riseLerp)
                    : MathHelper.Lerp(_smoothed[i], raw, fallLerp);
            }

            if (DebugPrint)
            {
                _debugTimer += delta;
                if (_debugTimer > 1.5f)
                {
                    _debugTimer = 0f;
                    Main.NewText(
                        $"[DEBUG] real={(realSpectrum != null)} vol={categoryVolume:0.00} | " +
                        $"raw[0/12/24]={debugRaw0:0.000}/{debugRaw12:0.000}/{debugRaw24:0.000} | " +
                        $"smoothed[0/12/24]={_smoothed[0]:0.000}/{_smoothed[12]:0.000}/{_smoothed[24]:0.000}",
                        new Color(255, 200, 80));
                }
            }

            return _smoothed;
        }
    }
}
