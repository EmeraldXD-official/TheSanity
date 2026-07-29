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
    public class HomingBloodBat : ModProjectile
    {
        // Jalur tekstur kustom otomatis mencari file "HomingBloodBat.png" di folder yang sama (Projectiles)
        
        public override void SetStaticDefaults()
        {
            // Menentukan bahwa proyektil ini memiliki total 4 frame animasi vertikal
            Main.projFrames[Projectile.type] = 4;
        }

        public override void SetDefaults()
        {
            Projectile.width = 32;       // Disesuaikan dengan lebar frame sprite kamu
            Projectile.height = 28;      // Disesuaikan dengan tinggi frame sprite kamu
            Projectile.aiStyle = -1;         // AI Custom penuh
            Projectile.hostile = true;       // Menyakiti player
            Projectile.friendly = false;     
            Projectile.penetrate = 1;        
            Projectile.tileCollide = false;  // Biar bisa menembus dinding saat ngehoming player
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            // Menentukan target (Player terdekat)
            Player player = Main.player[Projectile.owner];
            if (Projectile.ai[1] >= 0 && Projectile.ai[1] < Main.maxPlayers)
            {
                player = Main.player[(int)Projectile.ai[1]];
            }

            Projectile.ai[0]++; // Timer Frame (1 detik = 60 Frame)

            // ==========================================
            // LOGIKA ANIMASI FLAPPING (MENGEPAK SAYAP)
            // ==========================================
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5) // Kecepatan kepakan sayap (berganti setiap 5 tick)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Main.projFrames[Projectile.type])
                {
                    Projectile.frame = 0; // Reset kembali ke frame pertama jika sudah mentok frame ke-4
                }
            }

            // Arah hadap horizontal otomatis mendeteksi arah kecepatan velocity horizontalnya
            Projectile.spriteDirection = (Projectile.velocity.X > 0f) ? 1 : -1;

            // ==========================================
            // FASE 1: HOMING MECHANIC (7 DETIK / 420 FRAME)
            // ==========================================
            if (Projectile.ai[0] <= 420f)
            {
                if (player.active && !player.dead)
                {
                    // --- BALANCING LOCATION: KECEPATAN & KELENTURAN HOMING ---
                    float homingSpeed = 6.5f;      // Kecepatan maksimal kejar
                    float homingInertia = 25f;     // Semakin besar angkatnya, semakin lambat/beloknya melebar (kelenturan)

                    Vector2 desiredVelocity = (player.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * homingSpeed;
                    Projectile.velocity = (Projectile.velocity * (homingInertia - 1f) + desiredVelocity) / homingInertia;
                }

                // EFEK VISUAL: Partikel cahaya merah saat fase homing aktif
                if (Main.rand.NextBool(3))
                {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.VampireHeal, Vector2.Zero, 100, Color.Red, 1.2f);
                    d.noGravity = true;
                }
            }
            // ==========================================
            // FASE 2: BONUS DELAY SEBELUM PECAH (2 DETIK / 120 FRAME)
            // ==========================================
            else if (Projectile.ai[0] > 420f && Projectile.ai[0] <= 540f)
            {
                // Memperlambat proyektil secara halus sebagai indikasi visual mau meledak
                Projectile.velocity *= 0.92f;

                // Efek partikel berdenyut cepat tanda bahaya
                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Blood, Main.rand.NextVector2Circular(2f, 2f), 0, default, 1.5f);
                    d.noGravity = true;
                }

                // Waktunya habis total (9 detik), picu hancur!
                if (Projectile.ai[0] >= 540f)
                {
                    Projectile.Kill();
                }
            }

            // ROTASI HALUS: Mengikuti sudut ke mana arah laju proyektil (Diselaraskan dengan sprite horizontal asli)
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Projectile.spriteDirection == -1)
            {
                Projectile.rotation += MathHelper.Pi; // Memutar sudut gambar jika mendeteksi arah hadap terbalik (kiri)
            }
        }

        public override void OnKill(int timeLeft)
        {
            // Suara pecah laser/darah
            SoundEngine.PlaySound(SoundID.Item101, Projectile.Center);

            // Ledakan partikel ke atas
            for (int i = 0; i < 12; i++)
            {
                Vector2 goreVel = new Vector2(Main.rand.NextFloat(-2f, 2f), Main.rand.NextFloat(-5f, -1f));
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, goreVel.X, goreVel.Y, 0, default, 1.3f);
            }

            // ==========================================
            // SPREAD PECAHAN: SELALU KE ARAH ATAS
            // ==========================================
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int totalShots = Main.rand.Next(3, 5); // Mengacak antara 3 atau 4 pecahan
                
                // --- LOKASI DAMAGE & KECEPATAN PECAHAN UPWARD ---
                int upwardDamage = 40;        // Ganti untuk setting damage pecahan
                float upwardSpeedBase = 7f;   // Kecepatan dasar meluncur ke atas

                for (int i = 0; i < totalShots; i++)
                {
                    // Membuat sudut menyebar tipis ke arah atas (antara -60 derajat sampai -120 derajat)
                    float angleOffset = MathHelper.ToRadians(Main.rand.NextFloat(-30f, 30f));
                    Vector2 upwardVelocity = new Vector2(0f, -1f).RotatedBy(angleOffset) * (upwardSpeedBase + Main.rand.NextFloat(-1.5f, 1.5f));

                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        upwardVelocity,
                        ProjectileID.BloodShot, // Memakai pecahan bola darah vanilla
                        upwardDamage,
                        1f,
                        Main.myPlayer
                    );
                }
            }
        }

        // ==========================================
        // DRAW METHOD UNTUK SPRITE SHEET VERTIKAL 4 FRAME
        // ==========================================
        public override bool PreDraw(ref Color lightColor)
        {
            // Mengambil aset tekstur asli kustom milik kelelawar ini
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            // Menghitung tinggi satu frame tunggal secara presisi (Total tinggi / 4 frame)
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            int currentFrameY = frameHeight * Projectile.frame;

            // Kotak potong frame untuk memotong baris vertikal tekstur yang sedang aktif berjalan
            Rectangle sourceRectangle = new Rectangle(0, currentFrameY, texture.Width, frameHeight);

            // Origin titik tengah gambar disandarkan murni pada setengah lebar dan setengah tinggi 1 frame (32x28)
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, frameHeight * 0.5f);

            // Membalik arah hadap mata kelelawar ke kiri/kanan berdasarkan orientasi gerak
            SpriteEffects effects = (Projectile.spriteDirection == 1) ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                sourceRectangle,
                lightColor,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale,
                effects,
                0
            );

            return false; // Mengembalikan nilai false agar engine Terraria tidak menggambar tekstur bawaan double
        }
    }
}