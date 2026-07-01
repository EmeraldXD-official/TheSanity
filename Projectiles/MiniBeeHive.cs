using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class MiniBeeHive : ModProjectile
    {
        // Mengambil texture langsung dari file aset vanilla Projectile BeeHive (ID: 566)
        // Dengan cara ini, kamu tidak perlu membuat file BeeHive.png baru di folder modmu
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BeeHive;

        public override void SetDefaults()
        {
            Projectile.width = 24;            // Dimensi hitbox disesuaikan dengan sprite BeeHive vanilla
            Projectile.height = 24;
            Projectile.hostile = true;         // Menyerang player (ubah ke friendly = true jika ini senjata player)
            Projectile.friendly = false;
            Projectile.tileCollide = true;    // Mengaktifkan deteksi tabrakan dengan block/dinding
            Projectile.penetrate = 1;         // Langsung hancur setelah 1 kali benturan
            Projectile.aiStyle = -1;          // Menggunakan AI custom di bawah
        }

        public override void AI()
        {
            // 1. Logika Efek Berputar (Spinning)
            // Kecepatan putaran dikali dengan arah horizontal (direction) agar putarannya alami
            Projectile.rotation += 0.15f * Projectile.direction;

            // 2. Efek Gravitasi Ringan (Opsional)
            // Membuat hive ini meluncur agak melengkung ke bawah seperti jatuh bebas
            Projectile.velocity.Y += 0.2f;
            if (Projectile.velocity.Y > 16f) // Batasi kecepatan jatuh maksimal
            {
                Projectile.velocity.Y = 16f;
            }

            // Memunculkan sedikit partikel serpihan sarang lebah (Hive Dust) saat terbang
            if (Main.rand.NextBool(4))
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Hive, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f);
            }
        }

        // Method ini otomatis terpanggil tepat saat projectile menyentuh block
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // Mengembalikan nilai 'true' memaksa projectile untuk langsung mati/hancur dan memicu fungsi Kill()
            return true;
        }

        public override void Kill(int timeLeft)
        {
            // 1. Memainkan suara hancur vanilla "NPCDeath1" tepat di koordinat tabrakan
            SoundEngine.PlaySound(SoundID.NPCDeath1, Projectile.Center);

            // 2. Efek Visual Partikel Ledakan Sarang Lebah
            for (int i = 0; i < 25; i++)
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Hive, 0f, 0f, 100, default, Main.rand.NextFloat(1f, 1.5f));
                d.velocity = Main.rand.NextVector2Circular(4f, 4f);
            }

            // 3. Spawning Pasukan Lebah & Hornet (Hanya dieksekusi di Server/Singleplayer agar tidak desync)
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int beeCount = Main.rand.Next(5, 7);       // Memunculkan 5 sampai 6 Bee
                int hornetCount = Main.rand.Next(1, 3);    // Memunculkan 1 sampai 2 Hornet

                // Spawn Bee
                for (int i = 0; i < beeCount; i++)
                {
                    Vector2 spawnOffset = Main.rand.NextVector2Circular(16f, 16f);
                    NPC.NewNPC(Projectile.GetSource_FromThis(), (int)(Projectile.Center.X + spawnOffset.X), (int)(Projectile.Center.Y + spawnOffset.Y), NPCID.Bee);
                }

                // Spawn Hornet
                for (int i = 0; i < hornetCount; i++)
                {
                    Vector2 spawnOffset = Main.rand.NextVector2Circular(16f, 16f);
                    NPC.NewNPC(Projectile.GetSource_FromThis(), (int)(Projectile.Center.X + spawnOffset.X), (int)(Projectile.Center.Y + spawnOffset.Y), NPCID.Hornet);
                }
            }
        }
    }
}