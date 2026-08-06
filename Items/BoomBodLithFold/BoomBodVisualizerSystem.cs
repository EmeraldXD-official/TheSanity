using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    /// <summary>
    /// Menggambar "Linear Spectrum / Bar Visualizer" yang mengelilingi (framing) seluruh tepi
    /// layar -- atas, bawah, kiri, kanan -- garis-garis pendek yang naik-turun dari tiap tepi
    /// ke arah dalam layar, sinkron di keempat sisi (pakai array band yang sama) supaya
    /// kesannya satu "kotak" yang berdenyut bareng, bukan 4 bar terpisah yang acak sendiri-sendiri.
    ///
    /// Aktif otomatis kalau BoomBodPlayer.BoomBodActive true (Boom Bod Lith terpasang, di slot
    /// fungsional ataupun vanity -- lihat BoomBodPlayer). Warnanya ngikutin BoomBodPlayer.BarColor
    /// (putih polos kalau belum di-dye).
    /// </summary>
    public class BoomBodVisualizerSystem : ModSystem
    {
        private const int BarsPerEdge = 48;
        private const float BarGap = 2f;
        private const float MaxBarLength = 70f;
        private const float BarAlpha = 0.85f;

        private bool _errorAlreadyReported;

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (Main.gameMenu || Main.LocalPlayer == null || !Main.LocalPlayer.active)
                return;

            BoomBodPlayer boomBod = Main.LocalPlayer.GetModPlayer<BoomBodPlayer>();
            if (!boomBod.BoomBodActive)
                return;

            // Dibungkus try/catch supaya kalau ada error pas nggambar, kita TAU lewat chat
            // (sekali aja, biar nggak spam tiap frame) -- bukan diam-diam nggak nongol tanpa jejak.
            try
            {
                DrawSpectrumFrame(spriteBatch, boomBod);
            }
            catch (System.Exception ex)
            {
                if (!_errorAlreadyReported)
                {
                    _errorAlreadyReported = true;
                    Main.NewText($"Boom Bod Lith: ERROR pas menggambar visualizer -- {ex.GetType().Name}: {ex.Message}", Color.Red);
                }
            }
        }

        // Penting: matiin thread capture audio pas mod di-unload/reload, supaya nggak nyisain
        // thread WASAPI nyangkut kalau player reload mods atau keluar ke menu utama.
        public override void Unload()
        {
            AudioCaptureEngine.Stop();
        }

        private void DrawSpectrumFrame(SpriteBatch spriteBatch, BoomBodPlayer boomBod)
        {
            float[] bands = AudioSpectrumSampler.GetBands(boomBod.Mode, BarsPerEdge);
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Color color = boomBod.BarColor * BarAlpha;

            int screenW = Main.screenWidth;
            int screenH = Main.screenHeight;

            float horizontalBarWidth = (screenW - (BarsPerEdge - 1) * BarGap) / BarsPerEdge;
            float verticalBarHeight = (screenH - (BarsPerEdge - 1) * BarGap) / BarsPerEdge;

            for (int i = 0; i < BarsPerEdge; i++)
            {
                float amp = bands[i];
                float length = amp * MaxBarLength;
                if (length < 1f)
                    length = 1f; // baris "diam" tetap kelihatan sebagai garis tipis, bukan hilang total

                float x = i * (horizontalBarWidth + BarGap);
                float y = i * (verticalBarHeight + BarGap);

                // Atas: tumbuh dari y=0 ke bawah
                spriteBatch.Draw(pixel, new Rectangle((int)x, 0, (int)horizontalBarWidth, (int)length), color);

                // Bawah: tumbuh dari tepi bawah layar ke atas
                spriteBatch.Draw(pixel, new Rectangle((int)x, screenH - (int)length, (int)horizontalBarWidth, (int)length), color);

                // Kiri: tumbuh dari x=0 ke kanan
                spriteBatch.Draw(pixel, new Rectangle(0, (int)y, (int)length, (int)verticalBarHeight), color);

                // Kanan: tumbuh dari tepi kanan layar ke kiri
                spriteBatch.Draw(pixel, new Rectangle(screenW - (int)length, (int)y, (int)length, (int)verticalBarHeight), color);
            }
        }
    }
}
