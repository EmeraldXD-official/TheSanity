using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity.Projectiles
{
    public class BabyEaterWatcher : ModProjectile
    {
        // Jalur pintas untuk memuat tekstur sprite asli "BabyEater" langsung dari file internal Terraria
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BabyEater;

        private int actionTimer = 0;
        private int attackDelay = 0;
        private float orbitAngle = 0f;
        private bool isInitialized = false;

        public override void SetStaticDefaults()
        {
            // Karena ukuran gambar internalnya 34x54 piksel dan beranimasi, 
            // Kita set jumlah pembagian frame vertikalnya di sini (Misal: 2 frame, tinggi per frame = 27 piksel)
            Main.projFrames[Projectile.type] = 2; 
        }

        public override void SetDefaults()
        {
            Projectile.width = 34;  // Lebar hitbox proyektil
            Projectile.height = 34; // Tinggi hitbox dibuat kotak agar rotasi putarannya seimbang
            
            Projectile.hostile = true;         // Menyerang player
            Projectile.friendly = false;       // Tidak melukai musuh
            Projectile.tileCollide = false;    // Tembus dinding agar tidak tersangkut saat memutari player
            Projectile.ignoreWater = true;     
            Projectile.timeLeft = 600;         // Waktu maksimal hidup (10 detik) sebagai antisipasi failsafe
        }

        public override void AI()
        {
            // Otomatis mendeteksi dan mengunci player terdekat yang masih aktif di dalam room
            Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            if (target == null || !target.active || target.dead) return;

            // -------------------------------------------------------------------------
            // KENDALI ANIMASI SPRITE (FRAME COUNTER)
            // -------------------------------------------------------------------------
            Projectile.frameCounter++;
            // [SPEED LOCATION]: Mengatur kecepatan kepakan sayap / pergantian frame animasi
            if (Projectile.frameCounter >= 6) 
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Main.projFrames[Projectile.type])
                {
                    Projectile.frame = 0;
                }
            }

            // -------------------------------------------------------------------------
            // INISIALISASI AWAL (TICK PERTAMA SPAWN)
            // -------------------------------------------------------------------------
            if (!isInitialized)
            {
                // Ambil sudut radian awal berdasarkan posisi spawn saat ini terhadap player
                orbitAngle = (Projectile.Center - target.Center).ToRotation();
                
                // [SPEED LOCATION]: Mengatur rentang waktu acak (dalam tick) sebelum berhenti dan menembak
                // 120 sampai 240 tick sama dengan 2 hingga 4 detik di dalam game
                attackDelay = Main.rand.Next(120, 240); 
                
                isInitialized = true;
            }

            actionTimer++;

            // [DISTANCE LOCATION]: Batas jarak melingkar ideal (20 Block x 16 Pixel = 320f)
            float jarakTargetOrbit = 20 * 16f;

            // -------------------------------------------------------------------------
            // STATE MACHINE: PERGERAKAN VS SERANGAN
            // -------------------------------------------------------------------------
            if (actionTimer < attackDelay)
            {
                // === STATE 1: MENDEKAT DAN MEMUTARI PLAYER ===
                // [SPEED LOCATION]: Kecepatan rotasi memutari player (makin besar nilainya, makin cepat dia berputar mengitari player)
                float kecepatanMelingkar = 0.025f; 
                orbitAngle += kecepatanMelingkar;

                // Hitung koordinat posisi ideal di sekeliling lingkaran orbit player
                Vector2 posisiIdeal = target.Center + orbitAngle.ToRotationVector2() * jarakTargetOrbit;

                // Membawa proyektil meluncur mulus menuju titik posisi ideal menggunakan teknik interpolasi/steering
                // [SPEED LOCATION]: Tingkat kelincahan daya kejar proyektil menuju lingkaran orbitnya
                float dayaKejar = 0.07f; 
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (posisiIdeal - Projectile.Center) * dayaKejar, 0.12f);
            }
            else
            {
                // === STATE 2: BERHENTI, MENEMBAKKAN CURSED FLAME, LALU HANCUR ===
                Projectile.velocity = Vector2.Zero; // Menghentikan total seluruh momentum pergerakan

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // Hitung arah lurus ke koordinat pusat tubuh player
                    Vector2 arahBidikan = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                    
                    // [SPEED LOCATION]: Kecepatan luncuran bola api hijau musuh (CursedFlameHostile)
                    float kecepatanPeluru = 9.5f; 
                    Vector2 velocityPeluru = arahBidikan * kecepatanPeluru;

                    // [DAMAGE LOCATION]: Besaran damage dari tembakan peluru Cursed Flame Hostile
                    int damageTembakan = 28; 

                    // Lepaskan proyektil Cursed Flame musuh ke arah player
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(), 
                        Projectile.Center, 
                        velocityPeluru, 
                        ProjectileID.CursedFlameHostile, 
                        damageTembakan, 
                        1f, 
                        Main.myPlayer
                    );
                }

                // Meledak/hilang seketika setelah tugas menembak selesai dilaksanakan
                Projectile.Kill();
            }

            // -------------------------------------------------------------------------
            // KENDALI ROTASI (SISTEM TATAPAN MATA / WAJAH)
            // -------------------------------------------------------------------------
            // Karena sprite aslinya memiliki orientasi wajah mutlak yang menghadap kebawah,
            // kita harus memotong sudutnya sebesar 90 derajat (MathHelper.PiOver2) agar presisi searah garis mata target.
            Vector2 arahMata = target.Center - Projectile.Center;
            float sudutAsli = arahMata.ToRotation();
            
            // [ROTATION OFFSET GUIDE]: Jika wajah terlihat miring saat ditest, ubah atau hilangkan modifier minus di bawah ini
            Projectile.rotation = sudutAsli - MathHelper.PiOver2;
        }
    }
}