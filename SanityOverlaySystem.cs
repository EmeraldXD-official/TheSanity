using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;

namespace TheSanity
{
    public class SanityOverlaySystem : ModSystem
    {
        // Menyimpan data gambar horor
        private Asset<Texture2D>[] horrorTextures;
        private int totalImages = 3; // Ubah angka ini sesuai jumlah file PNG horor kamu

        // Variabel Timer
        private int flashTimer = 0;      // Berapa lama gambar tampil di layar
        private int appearanceTimer = 0; // Jeda waktu acak antar penampakan
        private int chosenImageIndex = 0;

        public override void Load() {
            if (!Main.dedServ) {
                horrorTextures = new Asset<Texture2D>[totalImages];
                for (int i = 0; i < totalImages; i++) {
                    // Membaca file dari folder TheSanity/Mecanic/Jumpscare1, dst.
                    horrorTextures[i] = ModContent.Request<Texture2D>($"TheSanity/Mecanic/Jumpscare{i + 1}");
                }
            }
        }

        public override void PostUpdateEverything() {
            if (Main.dedServ || Main.gameMenu) return;

            Player player = Main.LocalPlayer;
            if (player == null || !player.active) return;

            // Mengambil variabel SanityCurrent dari SanityPlayer
            if (player.TryGetModPlayer(out SanityPlayer sanityPlayer)) {
                if (sanityPlayer.SanityCurrent >= 100) {
                    
                    // Jika sedang tidak menampilkan gambar, jalankan penghitung jeda kemunculan
                    if (flashTimer <= 0) {
                        appearanceTimer++;
                        
                        // Cek acak setiap beberapa detik (misal meroll acak antara 5 sampai 15 detik)
                        if (appearanceTimer >= Main.rand.Next(300, 900)) {
                            chosenImageIndex = Main.rand.Next(totalImages);
                            flashTimer = 6; // 6 frames = 0.1 detik sekelebat muncul
                            appearanceTimer = 0;
                        }
                    }
                }
                else {
                    // Jika sanity turun dari 100, reset semua timer agar bersih
                    flashTimer = 0;
                    appearanceTimer = 0;
                }
            }

            // Kurangi durasi penampilan gambar di setiap frame
            if (flashTimer > 0) {
                flashTimer--;
            }
        }

        // Hook khusus tModLoader untuk menggambar sesuatu tepat di atas layar game
        public override void PostDrawInterface(SpriteBatch spriteBatch) {
            if (flashTimer > 0 && horrorTextures != null && horrorTextures[chosenImageIndex].IsLoaded) {
                Texture2D texture = horrorTextures[chosenImageIndex].Value;

                // Mengambil ukuran resolusi layar Terraria pemain saat itu
                Rectangle screenBounds = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);

                spriteBatch.End();
                // Membuka SpriteBatch baru dengan efek BlendState transparan/normal
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.UIScaleMatrix);

                // Menggambar foto memenuhi satu layar penuh dengan opasitas agak samar-samar misterius (0.6f / 60%)
                // Ubah Color.White * 0.6f menjadi Color.White * 1f jika ingin gambarnya jernih/jelas banget
                spriteBatch.Draw(texture, screenBounds, Color.White * 0.6f); 
            }
        }
    }
}