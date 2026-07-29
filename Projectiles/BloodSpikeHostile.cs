using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace TheSanity
{
    public class BloodSpikeHostile : ModProjectile
    {
        // Pinjam Sprite Sheet Sharp Tears asli (200 x 192 total, berisi 6 frame)
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.SharpTears}";

        // Properti kustom untuk menyimpan index frame acak dan batas ukuran tumbuhnya
        private int randomFrame = 0;
        private float maxScale = 1f;
        private bool initialized = false;

        public override void SetDefaults()
        {
            // Lebar 32 dan Tinggi 48 sebagai hitbox dasar (Akan berskala otomatis secara dinamis)
            Projectile.width = 32;
            Projectile.height = 48;

            Projectile.hostile = true;     
            Projectile.friendly = false;   
            Projectile.tileCollide = false; // Tembus block agar pangkalnya bisa tertanam di dalam tanah
            
            Projectile.aiStyle = -1;
            Projectile.timeLeft = 45;       // Hidup selama 0.75 detik (Muncul, diam sejenak, hilang)
        }

        public override void AI()
        {
            // --- MEKANIK INDIKATOR & INIŚIALISASI VARIASI (RUN ONCE) ---
            if (!initialized)
            {
                initialized = true;

                // 1. Pilih 1 dari 6 variasi bentuk duri di dalam sprite sheet secara acak
                randomFrame = Main.rand.Next(0, 6);

                // 2. Berikan variasi ukuran maksimal duri secara acak (0.6x kecil sampai 1.3x raksasa)
                maxScale = Main.rand.NextFloat(0.6f, 1.3f);

                // 3. Sesuaikan ukuran Hitbox secara dinamis berdasarkan skala acak yang didapat
                // Lebar asli sprite = 32, Panjang asli = 200. Kita ambil area hitbox yang adil untuk player.
                Projectile.width = (int)(32f * maxScale);
                Projectile.height = (int)(120f * maxScale); // Diambil rata-rata tinggi tusukan efektif

                // Geser sedikit posisi spawn ke bawah agar dasarnya tenggelam di tanah saat lahir
                Projectile.position.Y += 20;

                // Mulai dari skala 0 agar menciptakan efek tumbuh dari dalam bumi
                Projectile.scale = 0f;
            }

            // --- MEKANIK VISUAL: EFEK GROW (TUMBUH KEK TANAMAN) ---
            // Di sepertiga awal umurnya, duri akan memanjang ke atas sampai batas maxScale
            if (Projectile.timeLeft > 30)
            {
                Projectile.scale = MathHelper.Lerp(Projectile.scale, maxScale, 0.25f);
            }
            // Di sepertiga akhir umurnya, duri akan mengkerut kembali masuk ke tanah
            else if (Projectile.timeLeft < 15)
            {
                Projectile.scale = MathHelper.Lerp(Projectile.scale, 0f, 0.2f);
                Projectile.damage = 0; // Matikan damage saat duri mulai tenggelam kembali
            }

            // Kunci Velocity murni ke NOL agar duri DIAM tertanam di tanah, tidak mental terbang!
            Projectile.velocity = Vector2.Zero;

            // Efek partikel uap darah merah di sekitar duri yang sedang menusuk
            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.CrimsonTorch, 0f, -2f, 100, default, Projectile.scale);
                d.noGravity = true;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.Bleeding, 300); // Debuff bleeding 5 detik
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            // --- MEKANIK MATEMATIKA POTONG SPRITE SHEET (FRAME SELECTION) ---
            // Lebar total = 200. Tinggi tiap frame = 192 / 6 = 32.
            int frameHeight = texture.Height / 6; 
            Rectangle sourceRectangle = new Rectangle(0, randomFrame * frameHeight, texture.Width, frameHeight);

            // Menetapkan poros putaran di pangkal kiri sprite (karena menghadap kanan, kiri adalah bawahnya saat diputar)
            Vector2 drawOrigin = new Vector2(0f, frameHeight * 0.5f);

            // Putar -90 derajat (MathHelper.PiOver2 * 3f) agar sprite yang tadinya tidur ke kanan berdiri tegak ke atas
            float rotationOffset = MathHelper.PiOver2 * 3f;

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                sourceRectangle, // Potong gambar agar hanya menampilkan frame acak yang dipilih
                lightColor,
                rotationOffset, // Rotasi berdiri tegak
                drawOrigin,
                Projectile.scale, // Skala dinamis (efek animasi tumbuh)
                SpriteEffects.None,
                0
            );

            return false; // Matikan gambar duplikat bawaan tML
        }
    }
}