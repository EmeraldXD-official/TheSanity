using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class FlameRing : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            // =========================================================================
            // LOKASI PENGATURAN JUMLAH FRAME ANIMASI (Sudah diset ke 3 frame)
            // =========================================================================
            Main.projFrames[Projectile.type] = 3;
        }

        public override void SetDefaults()
        {
            // =========================================================================
            // LOKASI UKURAN HITBOX PROJECTILE (Sesuai dimensi sprite asli: 224x224 pixel)
            // =========================================================================
            Projectile.width = 224;
            Projectile.height = 224;

            Projectile.aiStyle = -1; // Menggunakan logika AI kustom sendiri di bawah
            Projectile.hostile = true; // Bisa melukai player
            Projectile.friendly = false; // Tidak melukai musuh
            Projectile.penetrate = -1; // Tidak hancur saat mengenai player (bertahan terus)
            Projectile.tileCollide = true; // Tidak bisa menembus block/dinding
            Projectile.ignoreWater = false; // Akan terpengaruh/padam atau melambat di dalam air
            
            // Mengunci agar proyektil tidak langsung hilang dalam waktu dekat
            Projectile.timeLeft = 3600; 
        }

        public override void AI()
        {
            // =========================================================================
            // LOGIKA MENJALANKAN ANIMASI FRAME (BERPUTAR KONTAN)
            // =========================================================================
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 6) // Kecepatan pergantian frame (6 frame game per 1 frame sprite)
            {
                Projectile.frameCounter = 0;
                Projectile.frame++; // Pindah ke frame berikutnya di bawahnya
                
                // Jika sudah melebihi frame ke-3 (index 2), reset kembali ke frame paling atas (0)
                if (Projectile.frame >= Main.projFrames[Projectile.type])
                {
                    Projectile.frame = 0;
                }
            }

            // =========================================================================
            // EFEK VISUAL: MENAMBAHKAN PARTIKEL API DI SEKITAR RING (BEAUTIFIER)
            // =========================================================================
            if (Main.rand.NextBool(3)) // Tingkat kerapatan partikel hiasan tambahan
            {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default(Color), 1.5f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.5f;
            }
        }

        // =========================================================================
        // LOGIKA DETEKSI TABRAKAN PLAYER & PEMBERIAN DEBUFF BURNING
        // =========================================================================
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // Saat player masuk ke dalam area radius atau terkena ring, berikan debuff Burning
            // 300 frame = 5 detik durasi efek terbakar pada player
            target.AddBuff(BuffID.Burning, 300);
        }

        // Efek visual tambahan saat lingkaran api ini menabrak dinding block padat
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            // Memunculkan ledakan partikel api kecil saat menyentuh tanah/dinding sebelum hancur
            for (int i = 0; i < 15; i++)
            {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default(Color), 2f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity = Main.rand.NextVector2Circular(4f, 4f);
            }
            return true; // Mengembalikan true agar proyektil langsung hancur/hilang saat mentok dinding
        }
    }
}