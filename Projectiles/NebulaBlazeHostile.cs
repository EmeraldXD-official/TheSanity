using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class NebulaBlazeHostile : ModProjectile
    {
        // 🛠️ FIX: Menyambungkan secara absolut ke file .png kustom di dalam folder proyek mod Anda
        public override string Texture => "TheSanity/Projectiles/NebulaBlazeHostile";

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 4; // Berisi 4 frame ke bawah sesuai sprite sheet Anda
        }

        public override void SetDefaults() {
            // Ukuran presisi disesuaikan dengan 1 frame asli (40 Width x 42 Height)
            Projectile.width = 40;
            Projectile.height = 42;
            
            Projectile.hostile = true;    
            Projectile.friendly = false;  
            Projectile.tileCollide = false; 
            Projectile.timeLeft = 300;    // Durasi hidup peluru (5 detik)
            Projectile.alpha = 255;       // Efek transparan saat muncul
        }

        public override void AI() {
            // Efek memudar muncul (Fade-in)
            if (Projectile.alpha > 0) {
                Projectile.alpha -= 25;
                if (Projectile.alpha < 0) Projectile.alpha = 0;
            }

            // 🎞️ Logika Animasi Sprite Sheet (4 Frame vertikal berurutan ke bawah)
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 6) { // Kecepatan pergantian frame animasi
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= 4) {
                    Projectile.frame = 0;
                }
            }

            // Efek Visual: Partikel api pink mistik di sekeliling peluru
            if (Main.rand.NextBool(3)) {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.PinkTorch, 0f, 0f, 100, default, 1.5f);
                dust.noGravity = true;
                dust.velocity *= 0.1f;
            }

            // 🎯 LOGIKA SEMI-HOMING (Bergerak lambat & berbelok melengkung halus)
            Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            if (target != null && target.active && !target.dead) {
                Vector2 targetDirection = target.Center - Projectile.Center;
                float distance = targetDirection.Length();

                if (distance < 1600f) {
                    targetDirection.Normalize();
                    float speed = 7.5f; // Kecepatan ditingkatkan agar proyektil tidak terasa terlalu lambat
                    Vector2 desiredVelocity = targetDirection * speed;

                    // Nilai Lerp 0.12f membuat peluru lebih responsif saat berbelok menuju player
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.12f);
                }
            }

            // 🔄 FIX ROTASI: Menggunakan rotasi berputar konstan (bukan mengunci arah hadap proyektil)
            Projectile.rotation += 0.2f * (Projectile.velocity.X >= 0 ? 1 : -1);
        }

        // 🎨 SISTEM RGB WARNA PINK CAMPUR PUTIH BERCAHAYA (GLOWING DYNAMIC)
        public override Color? GetAlpha(Color lightColor) {
            // Gelombang transisi warna menggunakan fungsi sin agar halus
            float wave = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.5f + 0.5f;
            
            Color pinkColor = new Color(255, 90, 190); // Pink menyala khas nebula
            Color whiteColor = Color.White;           // Putih inti cahaya
            
            // Mencampur kedua warna secara dinamis berdasarkan waktu game
            Color finalColor = Color.Lerp(pinkColor, whiteColor, wave);
            
            return finalColor * Projectile.Opacity;
        }
    }
}