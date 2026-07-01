using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using Terraria.GameContent;
using ReLogic.Graphics; // BARU: Ditambahkan agar mengenali DynamicSpriteFont bawaan tModLoader 1.4

namespace TheSanity.CostumeRarity
{
    public class TwinkleRarityDrawer : GlobalItem
    {
        // Cetak biru data untuk partikel UI internal
        private class UIParticle
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Scale;
            public int LifeTime;
            public int MaxLife;
            public float Rotation;
            public float RotSpeed;
            public bool IsFalling; // True = partikel hujan, False = serpihan ledakan
        }

        private static List<UIParticle> particles = new List<UIParticle>();
        private static float starTimer = 0f;
        private static Vector2 starPosition;
        private static float starRotation = 0f;
        private static bool starActive = false;
        private static uint lastUpdateFrame = 0;

        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset) {
            // Eksekusi HANYA jika baris tersebut adalah Nama Item dan item tersebut memakai Rarity Twinkle kita
            if (line.Mod == "Terraria" && line.Name == "ItemName" && item.rare == ModContent.RarityType<CostumeRarity.TwinkleRarity>()) {
                
                // Mengunci update agar animasi berjalan konstan 60 FPS (tidak ngebut jika tooltip digambar berkali-kali)
                if (Main.GameUpdateCount != lastUpdateFrame) {
                    UpdateAnimationLogic(line);
                    lastUpdateFrame = Main.GameUpdateCount;
                }

                // 1. Gambar Seluruh Partikel aktif (Di balik posisi teks)
                DrawParticles(Main.spriteBatch);

                // 2. Gambar Efek Teks Kuning Glow Berjalan dari Kiri ke Kanan
                DrawGlowText(line);

                // 3. Gambar Animasi Bintang Boss yang Melesat & Berputar
                DrawStarAnimation(Main.spriteBatch, line);

                return false; // Matikan gambar teks standar Terraria agar tidak tumpang tindih
            }
            return true;
        }

        private static void UpdateAnimationLogic(DrawableTooltipLine line) {
            Vector2 textDim = ChatManager.GetStringSize(line.Font, line.Text, line.BaseScale);

            // --- A. LOGIKA PARTIKEL JATUH DARI ATAS ---
            if (Main.rand.NextBool(3)) { // Peluang spawn partikel jatuh tiap frame
                float randomX = line.X + Main.rand.NextFloat(0f, textDim.X);
                particles.Add(new UIParticle {
                    Position = new Vector2(randomX, line.Y - Main.rand.NextFloat(4f, 12f)),
                    Velocity = new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(0.8f, 2f)),
                    Color = new Color(255, Main.rand.Next(210, 256), Main.rand.Next(0, 80)),
                    Scale = Main.rand.NextFloat(0.5f, 0.9f),
                    LifeTime = 0,
                    MaxLife = Main.rand.Next(40, 75),
                    IsFalling = true
                });
            }

            // --- B. LOGIKA TIMING BINTANG (Setiap 5 Detik = 300 Frame) ---
            starTimer++;
            if (starTimer >= 300f) {
                starTimer = 0f;
                starActive = true;
                // Muncul di sebelah kanan layar nama teks (+ offset luar 90 pixel)
                starPosition = new Vector2(line.X + textDim.X + 90f, line.Y + textDim.Y / 2f);
                starRotation = 0f;
            }

            if (starActive) {
                Vector2 targetCenter = new Vector2(line.X + textDim.X / 2f, line.Y + textDim.Y / 2f);
                
                // Melesat mulus ke tengah menggunakan interpolasi Lerp
                starPosition = Vector2.Lerp(starPosition, targetCenter, 0.09f);
                starRotation += 0.3f; // Berputar kencang saat melesat

                // Memicu ledakan dahsyat jika sudah sampai di pusat nama item
                if (Vector2.Distance(starPosition, targetCenter) < 5f) {
                    starActive = false;
                    TriggerExplosion(targetCenter);
                }
            }

            // --- C. UPDATE PERGERAKAN SEMUA PARTIKEL ---
            for (int i = particles.Count - 1; i >= 0; i--) {
                UIParticle p = particles[i];
                p.LifeTime++;
                p.Position += p.Velocity;

                if (p.IsFalling) {
                    p.Velocity.Y += 0.02f; // Efek gravitasi lambat untuk partikel jatuh
                } else {
                    p.Velocity *= 0.93f; // Hambatan udara agar partikel ledakan melambat alami
                    p.Rotation += p.RotSpeed;
                }

                if (p.LifeTime >= p.MaxLife) {
                    particles.RemoveAt(i);
                }
            }
        }

        private static void TriggerExplosion(Vector2 center) {
            // Spawn 25-35 pecahan partikel ke segala arah mata angin
            int burstCount = Main.rand.Next(25, 36);
            for (int i = 0; i < burstCount; i++) {
                Vector2 burstVel = Main.rand.NextVector2Circular(4.5f, 4.5f);
                particles.Add(new UIParticle {
                    Position = center,
                    Velocity = burstVel,
                    Color = new Color(255, Main.rand.Next(180, 256), Main.rand.Next(0, 120)),
                    Scale = Main.rand.NextFloat(0.6f, 1.3f),
                    LifeTime = 0,
                    MaxLife = Main.rand.Next(25, 55),
                    Rotation = Main.rand.NextFloat(0f, MathHelper.TwoPi),
                    RotSpeed = Main.rand.NextFloat(-0.15f, 0.15f),
                    IsFalling = false
                });
            }
            // Bunyi ledakan kecil kosmik di UI
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.2f, Pitch = 0.4f });
        }

        private static void DrawGlowText(DrawableTooltipLine line) {
            Vector2 pos = new Vector2(line.X, line.Y);
            string text = line.Text;
            DynamicSpriteFont font = line.Font; // PERBAIKAN UTAMA: Mengubah tipe data ke DynamicSpriteFont agar kompatibel
            Vector2 scale = line.BaseScale;

            float currentXOffset = 0f;

            // Menggambar teks huruf demi huruf untuk menciptakan efek gelombang berjalan (Left to Right Glow)
            for (int i = 0; i < text.Length; i++) {
                string singleChar = text[i].ToString();
                Vector2 charSize = font.MeasureString(singleChar) * scale;

                // Rumus Sinus bersambung berdasarkan posisi X koordinat huruf untuk efek berjalan ke kanan
                float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6.5f - (pos.X + currentXOffset) * 0.025f);
                float glowFactor = (wave + 1f) / 2f; // Konversi dari rentang [-1, 1] ke [0, 1]

                Color baseGold = new Color(255, 200, 0);       // Warna Kuning Emas gelap
                Color neonYellow = new Color(255, 255, 160);    // Warna Kuning Glow terang benderang
                Color finalColor = Color.Lerp(baseGold, neonYellow, glowFactor);

                // Menggambar Bayangan Hitam Teks (Drop Shadow) khas Terraria agar teks terbaca jelas
                ChatManager.DrawColorCodedString(Main.spriteBatch, font, singleChar, 
                    pos + new Vector2(currentXOffset, 0f) + new Vector2(2f, 2f), Color.Black, line.Rotation, line.Origin, scale);

                // Menggambar Teks Utama Berwarna Glow di atas bayangan
                ChatManager.DrawColorCodedString(Main.spriteBatch, font, singleChar, 
                    pos + new Vector2(currentXOffset, 0f), finalColor, line.Rotation, line.Origin, scale);

                currentXOffset += charSize.X;
            }
        }

        private static void DrawParticles(SpriteBatch spriteBatch) {
            Texture2D pixel = TextureAssets.MagicPixel.Value; // Ambil tekstur 1x1 pixel internal game
            Rectangle rect = new Rectangle(0, 0, 1, 1);

            foreach (var p in particles) {
                float alpha = 1f - ((float)p.LifeTime / p.MaxLife); // Efek memudar tipis sebelum hancur
                Color drawnColor = p.Color * alpha;

                if (p.IsFalling) {
                    // Partikel jatuh berbentuk kotak vertikal kecil mendatar
                    spriteBatch.Draw(pixel, p.Position, rect, drawnColor, 0f, Vector2.Zero, p.Scale * 2.5f, SpriteEffects.None, 0f);
                } else {
                    // Partikel ledakan berbentuk serpihan berputar acak
                    spriteBatch.Draw(pixel, p.Position, rect, drawnColor, p.Rotation, new Vector2(0.5f, 0.5f), p.Scale * 3.5f, SpriteEffects.None, 0f);
                }
            }
        }

        private static void DrawStarAnimation(SpriteBatch spriteBatch, DrawableTooltipLine line) {
            if (!starActive) return;

            // Load file boss asset secara instan dari folder Twinkle yang kamu tentukan
            Texture2D starTexture = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Twinkle/TwinkleStars_Head_Boss").Value;
            Vector2 centerOrigin = starTexture.Size() / 2f;

            // Digambar menggunakan Color.White penuh agar bersinar konstan mengabaikan pencahayaan sekitar
            spriteBatch.Draw(starTexture, starPosition, null, Color.White, starRotation, centerOrigin, line.BaseScale * 0.45f, SpriteEffects.None, 0f);
        }
    }
}