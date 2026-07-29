using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity.GlobalNPC.Bosses.Twinkle
{
    public class StarRainProj : ModProjectile
    {
        // Sama-sama meminjam gambar FallingStar vanilla biar serasi
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;           // Tidak piercing juga
            Projectile.timeLeft = 180;          // 3 detik hancur jika tidak mengenai apapun
            Projectile.tileCollide = false;      // Hancur jika menabrak balok tanah
        }

        public override void AI() {
            Projectile.rotation += 0.3f;
            
            // Efek partikel sihir warna biru/putih kosmik saat jatuh
            if (Main.rand.NextBool(4)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.MagicMirror, 0f, 0f, 100, default, 1f);
            }

            // Ambil nomor ID musuh yang dikunci dari peluru pertama lewat ai[0]
            int indexMusuh = (int)Projectile.ai[0];
            
            if (indexMusuh >= 0 && indexMusuh < Main.maxNPCs) {
                NPC targetFokus = Main.npc[indexMusuh];
                
                // Pastikan musuh targetnya masih hidup dan aktif di world
                if (targetFokus.active && !targetFokus.dontTakeDamage) {
                    Vector2 arahTarget = (targetFokus.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    
                    // Mengatur kecepatan agar semakin jatuh ke bawah, jalannya makin kencang
                    float speedSekarang = Projectile.velocity.Length();
                    if (speedSekarang < 14f) speedSekarang += 0.25f; 
                    
                    // Homing yang longgar / ngga nge-lock kaku (menggunakan angka lerp kecil yaitu 0.025f)
                    Projectile.velocity = Vector2.Normalize(Vector2.Lerp(Projectile.velocity, arahTarget * speedSekarang, 0.025f)) * speedSekarang;
                }
            }
        }
    }
}