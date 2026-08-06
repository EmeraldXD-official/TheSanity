using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent; // ✅ Tambahkan ini untuk mengakses TextureAssets
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

        // Jarak horizontal minimum antar drip pas baru nge-spawn, biar nggak numpuk
        private const float MIN_SPAWN_GAP_X = 160f;

        // ✅ Variable 'pixel' dihapus agar tidak perlu alokasi Texture2D manual di background thread
        private readonly List<Drip> drips = new List<Drip>();
        private readonly UnifiedRandom rng = new UnifiedRandom();
        private int spawnCooldown = 0;

        private class Drip
        {
            public float X;               // posisi X badan drip
            public float Y;                // posisi ujung ATAS drip di layar
            public float Width;            // lebar strip
            public float Length;           // panjang total strip
            public float Speed;            // kecepatan jatuh
            public float BaseAlphaFactor;  // opacity puncak drip
        }

        // ✅ Load() tidak lagi membuat Texture2D baru secara manual
        public override void Load()
        {
            if (Main.dedServ) return;
        }

        public override void Unload()
        {
            drips.Clear();
        }

        // Digambar paling akhir (di atas semua interface), jadi overlay layar penuh
        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (Main.gameMenu || Main.dedServ) return;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return;

            RainPlayerEffect modPlayer = player.GetModPlayer<RainPlayerEffect>();

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

                    float minCooldown = MathHelper.Lerp(70f, 18f, wetLevel);
                    float maxCooldown = MathHelper.Lerp(140f, 40f, wetLevel);
                    spawnCooldown = rng.Next((int)minCooldown, (int)maxCooldown + 1);
                }
            }

            for (int i = drips.Count - 1; i >= 0; i--)
            {
                Drip d = drips[i];
                d.Y += d.Speed;

                if (d.Y > screenH + 50)
                {
                    drips.RemoveAt(i);
                }
            }
        }

        private void TrySpawnDrip(int screenW, float wetLevel)
        {
            float candidateX = rng.Next(0, screenW);

            foreach (Drip other in drips)
            {
                if (other.Y < 250f && Math.Abs(other.X - candidateX) < MIN_SPAWN_GAP_X)
                {
                    return;
                }
            }

            drips.Add(new Drip
            {
                X = candidateX,
                Y = rng.Next(-220, -40),
                Width = RandRange(60f, 110f),
                Length = RandRange(120f, 210f),
                Speed = RandRange(1.5f, 6f),
                BaseAlphaFactor = MathHelper.Lerp(0.45f, 1f, wetLevel) * RandRange(0.75f, 1f)
            });
        }

        private void DrawDrip(SpriteBatch spriteBatch, Drip d)
        {
            Color darkColor = new Color(25, 60, 130);    // ujung bawah: gelap & pekat
            Color fadeColor = new Color(140, 180, 230);  // ujung atas: mulai fade/transparan

            int step = 3; 

            // ✅ Menggunakan tekstur piksel putih 1x1 bawaan vanilla Terraria secara aman
            Texture2D pixelTexture = TextureAssets.MagicPixel.Value;

            for (float y = 0; y < d.Length; y += step)
            {
                float f = y / d.Length;
                Color rowColor = Color.Lerp(fadeColor, darkColor, f);
                float rowAlpha = f * MAX_OPACITY * d.BaseAlphaFactor;

                if (rowAlpha <= 0.01f) continue;

                var rect = new Rectangle((int)(d.X - d.Width / 2f), (int)(d.Y + y), (int)d.Width, step + 1);
                spriteBatch.Draw(pixelTexture, rect, rowColor * rowAlpha);
            }
        }
    }
}