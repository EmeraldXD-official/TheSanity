using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [CUSTOM PROJECTILE]: VISUAL SUMMON SIGN WITH GLOW-IN-THE-DARK & TIMED BOLT
    // =========================================================================
    public class DarkMageSummonSign : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DD2DarkMageRaise;

        public override void SetStaticDefaults() {
            // Meniru jumlah frame animasi asli dari tekstur DD2DarkMageRaise vanilla
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.DD2DarkMageRaise];
        }

        public override void SetDefaults() {
            Projectile.width = 32;
            Projectile.height = 32;
            
            // Pengaturan Sifat: Murni Visual & Penanda Waktu
            Projectile.friendly = false;
            Projectile.hostile = false;     // Dibuat false agar lingkaran visualnya sendiri tidak melukai player secara instan
            Projectile.tileCollide = false; // Menembus blok agar bisa spawn di mana saja secara bebas
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;      // Tidak hancur jika terkena serangan apa pun
            
            // LOKASI UKURAN BALANCING: Kita kecilkan skalanya menjadi 65% dari ukuran asli bawaan vanilla
            Projectile.scale = 0.65f;

            // Waktu hidup total: 5 Detik (5 * 60 Frame = 300 Frame)
            Projectile.timeLeft = 300; 
        }

        public override void AI() {
            // -------------------------------------------------------------------------
            // 1. SISTEM ANIMASI FRAME
            // -------------------------------------------------------------------------
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5) { // Kecepatan pergantian frame animasi
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Main.projFrames[Projectile.type]) {
                    Projectile.frame = 0;
                }
            }

            // -------------------------------------------------------------------------
            // 2. TIMING ATTACK SYSTEM (MENGGUNAKAN AI[0] SEBAGAI CONTROLLER)
            // -------------------------------------------------------------------------
            Projectile.ai[0]++; // Naik 1 angka setiap frame game berjalan

            // DETIK KE-3 (Frame 180): Eksekusi Tembakan Peluru DD2DarkMageBolt Vanilla
            if (Projectile.ai[0] == 180f) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    
                    // Cari Player terdekat sebagai target bidikan
                    Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
                    
                    if (target != null && target.active && !target.dead) {
                        // Hitung arah vektor presisi dari pusat proyektil menuju pusat tubuh player
                        Vector2 shootDirection = target.Center - Projectile.Center;
                        shootDirection.Normalize(); // Ubah panjangnya menjadi 1 unit dasar

                        // LOKASI VELOCITY BALANCING: Dipercepat dari 6f menjadi 13f agar lesatan bola hitam lebih sigap!
                        float boltSpeed = 13f; 
                        Vector2 boltVelocity = shootDirection * boltSpeed;

                        // Ambil ID Proyektil Bolt milik Dark Mage asli
                        int boltType = ProjectileID.DD2DarkMageBolt;

                        // Kunci: Ambil damage dinamis yang sudah dikirim otomatis oleh GlobalNPC
                        int boltDamage = Projectile.damage; 
                        float boltKnockback = 3f;

                        // Spawn peluru vanilla yang asli dan pastikan bersifat HOSTILE (Melukai Player)
                        int spawnedBolt = Projectile.NewProjectile(
                            Projectile.GetSource_FromAI(), 
                            Projectile.Center, 
                            boltVelocity, 
                            boltType, 
                            boltDamage, 
                            boltKnockback, 
                            Main.myPlayer
                        );

                        if (spawnedBolt < Main.maxProjectiles) {
                            Main.projectile[spawnedBolt].hostile = true;
                            Main.projectile[spawnedBolt].friendly = false;
                        }

                        // Mainkan suara sihir gelap vanilla saat peluru terlontar
                        SoundEngine.PlaySound(SoundID.Item8, Projectile.Center);
                    }
                }
            }

            // DETIK KE-4 s/d DETIK KE-5 (Frame 240 ke atas): Memasuki Fase Fade Away
            if (Projectile.ai[0] >= 240f) {
                // Kurangi nilai kepekatan visual (Alpha) secara bertahap
                // Nilai 255 artinya transparan total, nilai 0 artinya padat total
                Projectile.alpha += 4; 
                if (Projectile.alpha > 255) Projectile.alpha = 255;
            }

            // DETIK KE-5 (Frame 300): Hancur total secara otomatis setelah dibenarkan
            if (Projectile.ai[0] >= 300f) {
                Projectile.Kill();
            }
        }

        // =========================================================================
        // 3. SPECIAL DRAWING: RENDER GLOW-IN-THE-DARK DENGAN ADDITIVE BLENDING
        // =========================================================================
        public override bool PreDraw(ref Color lightColor) {
            // Ambil data aset gambar internal Terraria yang sudah kita deklarasikan di atas
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;

            // Hitung ukuran potongan frame vertikal gambar
            int height = texture.Height / Main.projFrames[Projectile.type];
            int yFrame = height * Projectile.frame;
            Rectangle sourceRectangle = new Rectangle(0, yFrame, texture.Width, height);

            // Origin dihitung dari pusat potongan frame agar rotasi dan skala mengecil pas di tengah koordinat
            Vector2 origin = sourceRectangle.Size() / 2f;
            Vector2 drawPosition = Projectile.Center - Main.screenPosition;

            // RAHASIA NEON MENYALA: Paksa Alpha warna menjadi 0 agar memicu Additive Blending
            // Kita gunakan warna ungu mistis (R=180, G=100, B=255) agar senada dengan Dark Mage
            Color glowColor = new Color(180, 100, 255, 0);

            // Hitung faktor transparansi interpolasi saat memasuki detik ke-4 agar visualnya ikut memudar halus
            float fadeFactor = (255f - Projectile.alpha) / 255f;
            glowColor *= fadeFactor;

            // Eksekusi draw ke layar game dengan membawa variabel Projectile.scale yang sudah mengecil
            Main.spriteBatch.Draw(
                texture, 
                drawPosition, 
                sourceRectangle, 
                glowColor, 
                Projectile.rotation, 
                origin, 
                Projectile.scale, // Otomatis mengecil rapi (0.65f)
                SpriteEffects.None, 
                0f
            );

            // Return false agar Terraria tidak menggambar ulang sprite aslinya yang gelap
            return false;
        }
    }
}