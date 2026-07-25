using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace TheSanity
{
    // =========================================================================
    // [SCREEN OVERLAY]: EFEK TETESAN AIR (STRIP/WORM SHAPE) PAS KEHUJANAN
    // Cuma bereaksi ke wetVisualLevel/rainVisualActive yang isinya KHUSUS sumber HUJAN.
    // Efek basah karena air/kolam sengaja TIDAK disentuh sama sekali di sini.
    // =========================================================================
    public class RainScreenEffect : ModSystem
    {
        // Opacity maksimal dibatasi 30% sesuai request
        private const float MAX_OPACITY = 0.3f;

        // Jarak horizontal minimum antar drip pas baru nge-spawn, biar nggak numpuk (drip sekarang lebar, jadi gap-nya lebih gede)
        private const float MIN_SPAWN_GAP_X = 160f;

        private static Texture2D pixel;
        private readonly List<Drip> drips = new List<Drip>();
        private readonly UnifiedRandom rng = new UnifiedRandom();
        private int spawnCooldown = 0;

        private class Drip
        {
            public float X;               // posisi X badan drip (lurus tegak, ga geser-geser)
            public float Y;                // posisi ujung ATAS drip di layar
            public float Width;            // lebar strip (kotak, konstan sepanjang badan)
            public float Length;           // panjang total strip (acak)
            public float Speed;            // kecepatan jatuh (acak, ada yang cepat ada yang lambat)
            public float BaseAlphaFactor;  // opacity puncak drip ini, DIKUNCI saat spawn (ga ikut naik-turun wetLevel belakangan)
        }

        public override void Load()
        {
            if (Main.dedServ) return; // Server nggak butuh render apapun

            pixel = new Texture2D(Main.graphics.GraphicsDevice, 1, 1);
            pixel.SetData(new[] { Color.White });
        }

        public override void Unload()
        {
            pixel = null;
            drips.Clear();
        }

        // Digambar paling akhir (di atas semua interface), jadi overlay layar penuh
        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (Main.gameMenu || Main.dedServ || pixel == null) return;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return;

            RainPlayerEffect modPlayer = player.GetModPlayer<RainPlayerEffect>();

            // spawnAllowed pakai kondisi MENTAH (bukan yang di-ramp), jadi begitu hujan berhenti
            // atau player pakai proteksi, SPAWN BARU langsung berhenti, tapi drip yang udah ada
            // di layar tetap lanjut jatuh & fade sampai selesai sendiri.
            bool spawnAllowed = modPlayer.rainVisualActive;
            float wetLevel = modPlayer.wetVisualLevel;

            UpdateDrips(spawnAllowed, wetLevel);

            foreach (Drip d in drips)
            {
                DrawDrip(spriteBatch, d);
            }
        }

        private float RandRange(float min, float max)
        {
            return min + (float)rng.NextDouble() * (max - min);
        }

        private void UpdateDrips(bool spawnAllowed, float wetLevel)
        {
            int screenW = Main.screenWidth;
            int screenH = Main.screenHeight;

            if (spawnAllowed)
            {
                spawnCooldown--;
                if (spawnCooldown <= 0)
                {
                    TrySpawnDrip(screenW, wetLevel);

                    // Makin deras hujan (wetLevel tinggi), makin sering muncul drip baru - tapi tetap dikasih jarak
                    float minCooldown = MathHelper.Lerp(70f, 18f, wetLevel);
                    float maxCooldown = MathHelper.Lerp(140f, 40f, wetLevel);
                    spawnCooldown = rng.Next((int)minCooldown, (int)maxCooldown + 1);
                }
            }
            // Kalau spawnAllowed == false, sengaja TIDAK di-reset apapun di sini -
            // drip yang udah ada dibiarkan lanjut di loop bawah sampai natural selesai.

            for (int i = drips.Count - 1; i >= 0; i--)
            {
                Drip d = drips[i];
                d.Y += d.Speed;

                // Baru dihapus kalau seluruh badan drip udah lewat bawah layar (animasi selesai natural)
                if (d.Y > screenH + 50)
                {
                    drips.RemoveAt(i);
                }
            }
        }

        private void TrySpawnDrip(int screenW, float wetLevel)
        {
            float candidateX = rng.Next(0, screenW);

            // Cek jarak ke drip lain yang posisinya masih deket area atas layar,
            // biar drip baru nggak numpuk sama drip yang baru aja muncul juga
            foreach (Drip other in drips)
            {
                if (other.Y < 250f && Math.Abs(other.X - candidateX) < MIN_SPAWN_GAP_X)
                {
                    return; // Kegeser, ga jadi spawn - dicoba lagi di cooldown berikutnya
                }
            }

            drips.Add(new Drip
            {
                X = candidateX,
                Y = rng.Next(-220, -40),
                Width = RandRange(60f, 110f),            // gede, setara ukuran teardrop sebelumnya
                Length = RandRange(120f, 210f),          // panjang acak, gede juga
                Speed = RandRange(1.5f, 6f),             // ada yang cepat, ada yang lambat
                BaseAlphaFactor = MathHelper.Lerp(0.45f, 1f, wetLevel) * RandRange(0.75f, 1f)
            });
        }

        // Gambar satu drip sebagai strip kotak (worm shape) LURUS TEGAK, dengan warna & opacity
        // paling PEKAT di ujung PALING BAWAH, terus makin FADE ke arah atas.
        private void DrawDrip(SpriteBatch spriteBatch, Drip d)
        {
            Color darkColor = new Color(25, 60, 130);    // ujung bawah: gelap & pekat
            Color fadeColor = new Color(140, 180, 230);  // ujung atas: mulai fade/transparan

            int step = 3; // gambar per 3px, cukup mulus dan hemat performa

            for (float y = 0; y < d.Length; y += step)
            {
                float f = y / d.Length; // 0 = ujung atas (paling fade), 1 = ujung paling bawah (paling gelap/pekat)
                Color rowColor = Color.Lerp(fadeColor, darkColor, f);
                float rowAlpha = f * MAX_OPACITY * d.BaseAlphaFactor;

                if (rowAlpha <= 0.01f) continue; // ga usah gambar bagian yang udah nyaris 0% biar hemat draw call

                var rect = new Rectangle((int)(d.X - d.Width / 2f), (int)(d.Y + y), (int)d.Width, step + 1);
                spriteBatch.Draw(pixel, rect, rowColor * rowAlpha);
            }
        }
    }
}
