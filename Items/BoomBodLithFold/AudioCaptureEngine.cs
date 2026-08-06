using System;
using System.Threading;
using Microsoft.Xna.Framework;
using NAudio.Dsp;
using NAudio.Wave;

namespace TheSanity.Items
{
    /// <summary>
    /// Merekam audio yang SEDANG KELUAR dari speaker/output device (loopback capture, lewat
    /// WASAPI -- Windows only) dan menghitung spectrum-nya pakai FFT. Ini yang bikin
    /// visualizer beneran ngikutin irama lagu/suara yang lagi kedengeran secara real-time,
    /// bukan simulasi lagi.
    ///
    /// KETERBATASAN YANG PERLU DISADARI:
    /// 1. WASAPI loopback merekam OUTPUT MIXED AKHIR device audio -- artinya musik, sound
    ///    effect, DAN ambient semua udah kecampur jadi satu waveform. Tidak ada cara untuk
    ///    "memisahkan" lagi jadi kategori-kategori terpisah dari titik ini (datanya sudah
    ///    hilang begitu di-mix). Makanya "Mode" (Music/Sound/Ambient/All) sekarang bekerja
    ///    sebagai FILTER GAIN di atas spectrum real ini (lihat AudioSpectrumSampler),
    ///    bukan pemisahan sumber suara yang sesungguhnya.
    /// 2. Hanya jalan di Windows (WasapiLoopbackCapture pakai Windows Audio Session API).
    ///    Di Linux/Mac, Start() akan gagal dengan aman (exception ketangkep, IsAvailable
    ///    tetap false) dan AudioSpectrumSampler otomatis balik ke mode simulasi lama.
    /// 3. Kalau device audio default berubah (headset dicabut dst.) capture bisa berhenti;
    ///    ada auto-retry sederhana di EnsureStarted().
    ///
    /// SETUP WAJIB (lihat pesan chat): perlu naruh NAudio.dll di folder lib/ mod-mu +
    /// daftarin di build.txt. Kalau belum disetup, project ini TIDAK AKAN COMPILE karena
    /// "using NAudio..." di atas nggak ketemu assembly-nya.
    /// </summary>
    public static class AudioCaptureEngine
    {
        private const int FftSize = 1024;      // harus pangkat 2
        private const int FftPow = 10;          // 2^10 = 1024

        private static WasapiLoopbackCapture _capture;
        private static readonly object Lock = new object();
        private static float[] _ringBuffer = new float[FftSize];
        private static int _writePos;
        private static bool _hasEnoughData;

        private static bool _startAttempted;
        private static bool _captureFailed;
        private static float _retryTimer;
        private const float RetryInterval = 5f; // detik, kalau capture gagal/berhenti coba lagi

        // WATCHDOG: nyimpen kapan terakhir kali OnDataAvailable beneran nerima data baru.
        // Dipakai buat mendeteksi capture yang "macet" -- status-nya masih "berhasil"
        // (nggak exception, nggak RecordingStopped) tapi diam-diam berhenti ngirim data baru,
        // jadi buffer-nya beku isinya sama terus -> bar kelihatan nongol tapi nggak gerak lagi.
        private static long _lastDataTicks = Environment.TickCount64;
        private const long StaleThresholdMs = 1500;

        // PENTING (fix bug utama): objek WASAPI (IAudioClient dkk, via COM) itu terikat
        // (apartment-affine) ke thread STA yang membuatnya. Kalau thread itu SELESAI/keluar,
        // capture-nya diam-diam berhenti ngirim data walau nggak ada exception/crash sama
        // sekali -- persis gejala "spectrum diem aja". Makanya thread capture SENGAJA ditahan
        // hidup terus (WaitOne) selama capture masih dipakai, baru dilepas pas Stop() dipanggil.
        private static readonly ManualResetEvent StopSignal = new ManualResetEvent(false);

        public static bool IsAvailable
        {
            get
            {
                if (!_startAttempted || _captureFailed || !_hasEnoughData)
                    return false;

                long sinceLastDataMs = Environment.TickCount64 - Interlocked.Read(ref _lastDataTicks);
                if (sinceLastDataMs > StaleThresholdMs)
                {
                    // Data berhenti mengalir tapi capture-nya nggak pernah "resmi" gagal --
                    // tandai gagal secara manual di sini supaya EnsureStarted() otomatis
                    // restart capture-nya di kesempatan berikutnya.
                    _captureFailed = true;
                    return false;
                }

                return true;
            }
        }

        /// <summary>
        /// Coba mulai capture kalau belum jalan. Aman dipanggil berkali-kali tiap frame --
        /// no-op kalau sudah jalan, dan cuma retry setelah RetryInterval detik kalau gagal.
        /// </summary>
        public static void EnsureStarted(float delta)
        {
            if (_startAttempted && !_captureFailed)
                return;

            if (_captureFailed)
            {
                _retryTimer -= delta;
                if (_retryTimer > 0f)
                    return;
            }

            _startAttempted = true;
            _captureFailed = false;
            _retryTimer = RetryInterval;
            StopSignal.Reset();

            // WASAPI butuh apartment thread COM (STA) di beberapa environment -- start di
            // thread khusus supaya tidak terganggu apapun apartment state thread utama game.
            // Thread ini SENGAJA nggak langsung selesai -- lihat komentar StopSignal di atas.
            var thread = new Thread(StartCaptureThreadEntry)
            {
                IsBackground = true,
                Name = "BoomBodAudioCapture"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
        }

        private static void StartCaptureThreadEntry()
        {
            try
            {
                _capture = new WasapiLoopbackCapture();
                _capture.DataAvailable += OnDataAvailable;
                _capture.RecordingStopped += (s, e) => { _captureFailed = true; };
                _capture.StartRecording();

                // Tahan thread ini hidup (jangan sampai method-nya return & thread mati)
                // selama capture masih aktif -- cuma dilepas begitu Stop() memanggil StopSignal.Set().
                StopSignal.WaitOne();
            }
            catch
            {
                _captureFailed = true;
            }
            finally
            {
                try
                {
                    _capture?.StopRecording();
                    _capture?.Dispose();
                }
                catch
                {
                    // ignore -- lagi beres-beres
                }
                _capture = null;
            }
        }

        private static void OnDataAvailable(object sender, WaveInEventArgs e)
        {
            WaveFormat fmt = _capture?.WaveFormat;
            if (fmt == null)
                return;

            int channels = Math.Max(1, fmt.Channels);
            int bytesPerSample = fmt.BitsPerSample / 8;
            if (bytesPerSample <= 0)
                return;

            bool isFloat = fmt.BitsPerSample == 32 && fmt.Encoding == WaveFormatEncoding.IeeeFloat
                || (fmt.Encoding == WaveFormatEncoding.Extensible && fmt.BitsPerSample == 32);
            int frameCount = e.BytesRecorded / (bytesPerSample * channels);

            lock (Lock)
            {
                for (int i = 0; i < frameCount; i++)
                {
                    float sum = 0f;
                    int baseOffset = i * bytesPerSample * channels;
                    for (int c = 0; c < channels; c++)
                    {
                        int offset = baseOffset + c * bytesPerSample;
                        if (offset + bytesPerSample > e.BytesRecorded)
                            continue;

                        if (bytesPerSample == 4 && isFloat)
                            sum += BitConverter.ToSingle(e.Buffer, offset);
                        else if (bytesPerSample == 2) // 16-bit PCM fallback
                            sum += BitConverter.ToInt16(e.Buffer, offset) / 32768f;
                        else if (bytesPerSample == 4) // 32-bit PCM fallback (bukan float)
                            sum += BitConverter.ToInt32(e.Buffer, offset) / 2147483648f;
                    }

                    _ringBuffer[_writePos] = sum / channels;
                    _writePos = (_writePos + 1) % FftSize;
                }

                _hasEnoughData = true;
            }

            Interlocked.Exchange(ref _lastDataTicks, Environment.TickCount64);
        }

        /// <summary>
        /// Hitung magnitude spectrum dari buffer audio real terbaru, dikelompokkan jadi
        /// <paramref name="bandCount"/> band (grouping logaritmik -- band awal = frekuensi
        /// rendah/bass, band akhir = frekuensi tinggi/treble), dinormalisasi kasar ke 0..1.
        /// Return null kalau capture belum siap (belum ada data / gagal / bukan Windows).
        /// </summary>
        public static float[] GetSpectrum(int bandCount)
        {
            if (!IsAvailable)
                return null;

            var fftBuffer = new Complex[FftSize];

            lock (Lock)
            {
                for (int i = 0; i < FftSize; i++)
                {
                    int idx = (_writePos + i) % FftSize;
                    float window = (float)FastFourierTransform.HammingWindow(i, FftSize);
                    fftBuffer[i].X = _ringBuffer[idx] * window;
                    fftBuffer[i].Y = 0f;
                }
            }

            FastFourierTransform.FFT(true, FftPow, fftBuffer);

            int usableBins = FftSize / 2;
            var bands = new float[bandCount];

            for (int b = 0; b < bandCount; b++)
            {
                float t0 = b / (float)bandCount;
                float t1 = (b + 1) / (float)bandCount;
                int bin0 = Math.Max(1, (int)Math.Pow(usableBins, t0));
                int bin1 = Math.Max(bin0 + 1, (int)Math.Pow(usableBins, t1));

                float sum = 0f;
                int count = 0;
                for (int k = bin0; k < bin1 && k < usableBins; k++)
                {
                    float mag = (float)Math.Sqrt(fftBuffer[k].X * fftBuffer[k].X + fftBuffer[k].Y * fftBuffer[k].Y);
                    sum += mag;
                    count++;
                }

                float avg = count > 0 ? sum / count : 0f;

                // Kompresi log kasar -> spectrum audio itu rentang dinamiknya lebar banget,
                // tanpa ini bar bakal keliatan diam terus kecuali pas ada suara sangat keras.
                float value = (float)Math.Log(1f + avg * 40f) / 4f;
                bands[b] = MathHelper.Clamp(value, 0f, 1f);
            }

            return bands;
        }

        public static void Stop()
        {
            StopSignal.Set(); // ini yang bikin thread capture akhirnya keluar dari WaitOne() & beres-beres sendiri
            _startAttempted = false;
            _hasEnoughData = false;
        }
    }
}
