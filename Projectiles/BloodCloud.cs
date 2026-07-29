using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;

namespace TheSanity.Projectiles // Sesuaikan dengan namespace mod kamu
{
    // ==================================================================================
    // 1. CLASS AWAN INDUK (BLOOD CLOUD)
    // ==================================================================================
    public class BloodCloud : ModProjectile
    {
        // Menggunakan jalur langsung ke texture asset internal Vanilla Terraria (BloodCloudRaining = ID 244)
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BloodCloudRaining;

        // ==================================================================================
        // 🛠️ PANDUAN BALANCING KUSTOM - AWAN INDUK (1 DETIK = 60 TICK)
        // ==================================================================================
        // [SPEED LOCATION] Durasi hidup awan: 9 Detik (9 * 60 = 540 Ticks)
        public const int CloudLifeTime = 540;       

        // [SPEED LOCATION] Jeda antar hujan (Frame). Makin kecil nilainya, makin lebat hujannya
        public const int MinRainDelay = 12;         // 12 frame = ~5 hujan/detik
        public const int MaxRainDelay = 20;         // 20 frame = ~3 hujan/detik
        
        // [DAMAGE LOCATION] Kustomisasi Damage dari butiran hujan merah yang dihasilkan
        public const int RainDamage = 15;           

        // [KNOCKBACK LOCATION] Efek dorongan (knockback) hujan ke player
        public const float RainKnockback = 1f;      

        // [SPEED LOCATION] Kecepatan jatuh / gravitasi butiran hujan ke bawah
        public const float RainGravity = 7f;        
        // ==================================================================================

        private int rainTimer = 0;
        private int currentDelay = 15; // Jeda dinamis awal

        public override void SetStaticDefaults()
        {
            // Menyesuaikan jumlah frame animasi sesuai dengan sprite sheet BloodCloud vanilla (4 Frame)
            Main.projFrames[Projectile.type] = 4;
        }

        public override void SetDefaults()
        {
            // Ukuran sesuai ukuran sprite sheet internal vanilla (54x24)
            Projectile.width = 54;
            Projectile.height = 24;

            // Awan ini PURE VISUAL/UTILITY, tidak memberikan damage langsung saat disentuh
            Projectile.hostile = false;
            Projectile.friendly = false;
            
            Projectile.tileCollide = false; // Biar awan bisa melayang menembus block/arena
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = CloudLifeTime; // Mengunci lifetime awan
        }

        public override void AI()
        {
            // -------------------------------------------------------------------------
            // 🎬 LOGIKA ANIMASI SPRITE SHEET (4 FRAME)
            // -------------------------------------------------------------------------
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 8) // Kecepatan pergantian frame awan
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
            }

            // -------------------------------------------------------------------------
            // 🌧️ LOGIKA SPAWN HUJAN SECARA BERKALA (3 - 5 RAIN / DETIK)
            // -------------------------------------------------------------------------
            rainTimer++;
            if (rainTimer >= currentDelay)
            {
                rainTimer = 0;
                // Mengacak jeda spawn berikutnya agar rintik hujan terlihat natural
                currentDelay = Main.rand.Next(MinRainDelay, MaxRainDelay + 1);

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // Mengacak posisi X agar hujan keluar menyebar di sepanjang lebar awan (54 pixel)
                    float spawnX = Projectile.position.X + Main.rand.NextFloat(0f, Projectile.width);
                    // Posisi Y diatur sedikit di bawah bodi awan
                    float spawnY = Projectile.position.Y + Projectile.height;

                    Vector2 rainPosition = new Vector2(spawnX, spawnY);
                    
                    // [SPEED LOCATION] Kecepatan jatuh horizontal diacak sedikit (-0.5 sampai 0.5) agar tidak terlalu kaku
                    Vector2 rainVelocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), RainGravity); 

                    // Memanggil proyektil anak (BloodRain) yang ada di bawah
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(), 
                        rainPosition, 
                        rainVelocity, 
                        ModContent.ProjectileType<BloodRain>(), 
                        RainDamage, // Mengambil data damage dari parameter balancing di atas
                        RainKnockback, 
                        Main.myPlayer
                    );
                }
            }

            // ✨ PENINGKATAN VISUAL: Efek aura kabut darah tipis di sekitar awan induk
            if (Main.rand.NextBool(4))
            {
                // Dust keluar dari bawah awan, bergerak perlahan ke bawah menyerupai uap darah
                Dust d = Dust.NewDustDirect(new Vector2(Projectile.position.X, Projectile.position.Y + Projectile.height - 4), Projectile.width, 6, DustID.Blood, 0f, 1f, 120, default, 0.8f);
                d.velocity.X *= 0.1f;
                d.velocity.Y *= 0.5f;
            }
        }
    }

    // ==================================================================================
    // 2. CLASS ANAK PROYEKTIL (BLOOD RAIN / HUJAN MERAH)
    // ==================================================================================
    public class BloodRain : ModProjectile
    {
        // Menggunakan asset sprite internal dari RainNimbus vanilla (ID 239)
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.RainNimbus;

        public override void SetDefaults()
        {
            Projectile.width = 4;
            Projectile.height = 40; // Bentuk memanjang ke bawah khas air hujan
            
            // Karena ini bodi air hujannya, kita set HOSTILE agar bisa melukai Player!
            Projectile.hostile = true; 
            Projectile.friendly = false;
            
            Projectile.tileCollide = true; // Hujan akan hancur jika menyentuh tanah/block
            Projectile.penetrate = 1;      // Hancur setelah mengenai 1 target/block
            Projectile.timeLeft = 180;     // Otomatis mati dalam 3 detik jika menggantung di udara
            Projectile.extraUpdates = 1;   // Membuat pergerakan jatuh air hujan terlihat lebih mulus dan cepat
        }

        public override void AI()
        {
            // Membuat rotasi gambar lurus menghadap ke arah kecepatan jatuhnya
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            
            // Visual tambahan: Meninggalkan jejak dust darah tipis saat jatuh bebas
            if (Main.rand.NextBool(6))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, 0f, 0f, 150, default, 0.5f);
                d.velocity *= 0.1f;
            }
        }

        // 🎨 MEWARNAI VANILLA SPRITE MENJADI MERAH (BLOOD RENDER)
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = new Vector2(texture.Width / 2f, texture.Height / 2f);

            // Mewarnai tekstur asli menggunakan warna Merah Crimson pekat (Glow Effect)
            Color rainColor = new Color(220, 30, 30) * Projectile.Opacity;

            // Gambar ulang tekstur dengan warna merah menyala (Additive Blend Effect)
            Main.EntitySpriteDraw(texture, drawPos, null, rainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            
            return false; // Mengembalikan false agar warna biru bawaan vanilla RainNimbus tidak ikut digambar
        }

        public override void OnKill(int timeLeft)
        {
            // Efek percikan darah saat butiran hujan hancur menabrak tanah / player
            for (int i = 0; i < 4; i++)
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Blood, Main.rand.NextFloat(-1.5f, 1.5f), Main.rand.NextFloat(-2f, -0.5f), 50, default, 0.7f);
                d.velocity.X *= 1.2f;
            }
        }
    }
}