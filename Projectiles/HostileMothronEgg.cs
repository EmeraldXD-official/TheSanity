using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class HostileMothronEgg : ModProjectile
    {
        // Menggunakan jalur sprite asli dari NPC Mothron Egg vanilla secara langsung
        public override string Texture => "Terraria/Images/NPC_" + NPCID.MothronEgg;

        public override void SetStaticDefaults()
        {
            // Tidak membutuhkan pengaturan trail visual tambahan
        }

        public override void SetDefaults()
        {
            // -------------------------------------------------------------------------
            // LOKASI BALANCING: UKURAN HITBOX & VISUAL TELUR (NEW: LEBIH KECIL)
            // -------------------------------------------------------------------------
            // width & height: Mengatur ukuran kotak tabrakan telur (hitbox).
            // scale: Mengatur skala gambar visual (1f = 100% normal, 0.6f = mengecil jadi 60%).
            // -------------------------------------------------------------------------
            Projectile.width = 16;         // Hitbox dipersempit agar pas dengan telur kecil (sebelumnya 30)
            Projectile.height = 16;        // Hitbox dipersempit agar pas dengan telur kecil (sebelumnya 30)
            Projectile.scale = 0.6f;       // Mengecilkan tampilan sprite telur menjadi 60% dari ukuran aslinya

            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = true; // Mengaktifkan sensor tabrakan block
            Projectile.penetrate = 1;      // Langsung memicu fungsi hancur (Kill) saat menabrak target/block
            Projectile.timeLeft = 600;     // Hancur otomatis dalam 10 detik jika melayang bebas di udara

            // -------------------------------------------------------------------------
            // LOKASI BALANCING: DAMAGE DASAR DARI EGG/TELUR
            // -------------------------------------------------------------------------
            Projectile.damage = 15; 
        }

        public override void AI()
        {
            // -------------------------------------------------------------------------
            // LOKASI BALANCING: GRAVITASI & KECEPATAN JATUH TELUR
            // -------------------------------------------------------------------------
            Projectile.velocity.Y += 0.2f; 
            if (Projectile.velocity.Y > 14f)
            {
                Projectile.velocity.Y = 14f;
            }

            // Memberikan efek putaran visual lambat yang mengikuti arah laju horizontal telurnya
            Projectile.rotation += Projectile.velocity.X * 0.03f + 0.02f;
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // Mengembalikan nilai true agar proyektil langsung meledak/menjalankan fungsi Kill() saat menyentuh permukaan solid
            return true; 
        }

        public override void Kill(int timeLeft)
        {
            // Efek suara lendir/telur pecah organic khas monster Terraria
            SoundEngine.PlaySound(SoundID.NPCDeath1, Projectile.Center);

            // -------------------------------------------------------------------------
            // LOKASI VISUAL: BEAUTIFY PARTIKEL PECAHAN TELUR
            // -------------------------------------------------------------------------
            for (int i = 0; i < 15; i++) // Jumlah partikel dikurangi sedikit agar pas dengan ukuran telur yang mengecil
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GreenBlood, 0f, 0f, 150, default, 1.0f);
                d.velocity = Main.rand.NextVector2Circular(3f, 3f);
                d.noGravity = false;
            }

            // Memastikan pemanggilan proyektil baru hanya diproses oleh Server/Singleplayer agar tidak desync
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // -------------------------------------------------------------------------
                // LOKASI BALANCING: JUMLAH MUNCURATAN PEDANG PATAH (2 - 4 Pedang)
                // -------------------------------------------------------------------------
                int spawnCount = Main.rand.Next(2, 5);

                for (int i = 0; i < spawnCount; i++)
                {
                    // -------------------------------------------------------------------------
                    // LOKASI BALANCING: KECEPATAN & LONTARAN PEDANG KE LANGIT (VELOCITY)
                    // -------------------------------------------------------------------------
                    float speedX = Main.rand.NextFloat(-2.5f, 2.5f);
                    float speedY = Main.rand.NextFloat(-6.0f, -3.5f);
                    Vector2 burstVelocity = new Vector2(speedX, speedY);

                    // Memanggil proyektil HostileBrokenHeroSword milikmu
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(), 
                        Projectile.Center, 
                        burstVelocity, 
                        ModContent.ProjectileType<HostileBrokenHeroSword>(), 
                        100, 
                        0f, 
                        Main.myPlayer
                    );
                }
            }
        }
    }
}