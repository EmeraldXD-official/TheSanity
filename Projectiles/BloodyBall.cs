using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using System;

namespace TheSanity.Projectiles
{
    public class BloodyBall : ModProjectile
    {
        // ==========================================
        // TRIK MEMINJAM SPRITE DRIPPLER FLAIL VANILLA
        // ==========================================
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.DripplerFlail}";

        public override void SetDefaults()
        {
            Projectile.width = 22;       
            Projectile.height = 22;
            Projectile.aiStyle = -1;         // Kita kendalikan gerakannya lewat AI custom sendiri
            Projectile.hostile = true;       // Set jadi hostile agar melukai player
            Projectile.friendly = false;     
            Projectile.penetrate = 1;        // Hancur saat menabrak target/dinding
            Projectile.tileCollide = true;   
            Projectile.ignoreWater = true;
            
            // --- TANPA GRAVITASI ---
            Projectile.extraUpdates = 0;
        }

        public override void AI()
        {
            // Efek berputar lambat saat melayang
            Projectile.rotation += 0.05f * (float)Projectile.direction;

            // Efek partikel darah estetik di sepanjang jalur terbangnya
            if (Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, Projectile.velocity * 0.2f, 0, default, 1.2f);
                d.noGravity = true;
            }

            // ==========================================
            // LOGIKA TIMER: 3 DETIK SEBELUM PECAH
            // ==========================================
            // 3 detik = 180 frame (Terraria berjalan di 60 FPS)
            Projectile.ai[0]++; 
            if (Projectile.ai[0] >= 180f) 
            {
                Projectile.Kill(); // Memicu method OnKill() di bawah untuk pecah
            }
        }

        public override void OnKill(int timeLeft)
        {
            // Suara cipratan darah saat bola pecah
            SoundEngine.PlaySound(SoundID.NPCDeath9, Projectile.Center);

            // Efek visual partikel gore/darah yang meledak
            for (int i = 0; i < 15; i++)
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.5f);
                d.noGravity = Main.rand.NextBool(2);
            }

            // ==========================================
            // BALANCING GUIDE: PROSES SPREAD BLOOD SHOT
            // ==========================================
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // Kamu bisa ganti angka 4 di bawah ini jika ingin jumlah pecahannya berbeda (misal jadi 3 atau 5)
                int totalShots = 4; 
                
                // --- LOKASI DAMAGE & KECEPATAN PECAHAN KAPAK/BLOOD SHOT ---
                int bloodShotDamage = 35; // Ganti angka ini untuk mengatur damage pecahan proyektil
                float bloodShotSpeed = 5.5f; // Ganti angka ini untuk mengatur kecepatan laju pecahan proyektil

                // Membagi sudut lingkaran sempurna (360 derajat) sama rata ke segala arah
                float baseRotation = Main.rand.NextFloat(MathHelper.TwoPi); // Offset rotasi acak awal agar arah pecahannya dinamis
                
                for (int i = 0; i < totalShots; i++)
                {
                    float angle = baseRotation + (MathHelper.TwoPi / totalShots) * i;
                    Vector2 shotVelocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * bloodShotSpeed;

                    // Menggunakan ProjectileID.BloodShot (proyektil vanilla bermata merah/darah)
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(), 
                        Projectile.Center, 
                        shotVelocity, 
                        ProjectileID.BloodShot, 
                        bloodShotDamage, 
                        1f, 
                        Main.myPlayer
                    );
                }
            }
        }

        // Memastikan rendering tekstur pinjaman digambar dengan presisi di tengah koordinat proyektil
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );
            return false;
        }
    }
}