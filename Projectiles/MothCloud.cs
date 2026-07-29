using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class MothCloud : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_0"; // Pure particle, tanpa sprite (.png)

        public override void SetDefaults()
        {
            // LOKASI UKURAN: 32x32 pixel sama dengan ukuran 2x2 block di Terraria
            Projectile.width = 32;       
            Projectile.height = 32;
            
            Projectile.hostile = true;    
            Projectile.friendly = false;  
            Projectile.tileCollide = false; // Bisa tembus block/dinding
            Projectile.ignoreWater = true;

            // LOKASI LIFETIME: 300 frame = 5 Detik
            Projectile.timeLeft = 300; 
        }

        public override void AI()
        {
            // --- 1. LOGIKA HOMING PERLAHAN (1 Block per detik) ---
            Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            
            if (target != null && target.active && !target.dead)
            {
                Vector2 direction = target.Center - Projectile.Center;
                direction.Normalize();

                // LOKASI SPEED PROJECTILE: 0.26f (Lambat, pas sekitar 1 block per detik)
                float moveSpeed = 4.0f;
                Projectile.velocity = direction * moveSpeed;
            }

            // --- 2. LOGIKA PARTIKEL DINAMIS (GERAK & HILANG-MUNCUL) ---
            
            // Kita spawn partikel setiap frame agar membentuk awan kabut yang tebal
            for (int i = 0; i < 2; i++)
            {
                // Tentukan posisi acak di dalam area 2x2 block tersebut
                Vector2 dustPosition = Projectile.position + new Vector2(Main.rand.Next(Projectile.width), Main.rand.Next(Projectile.height));

                // Partikel Ungu Utama (DustID.WitherLightning) - Tidak terlalu bersinar
                if (Main.rand.NextBool(2)) // 50% peluang tiap perulangan
                {
                    Dust purpleDust = Dust.NewDustDirect(dustPosition, 0, 0, DustID.WitherLightning, 0f, 0f, 100, default, Main.rand.NextFloat(1.2f, 1.8f));
                    
                    // Membuat partikel bergerak acak menjauh secara dinamis
                    purpleDust.velocity = new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.5f, 0.5f));
                    purpleDust.noGravity = true;
                    
                    // Efek menghilang-muncul secara visual dengan memanipulasi skala & fade
                    purpleDust.fadeIn = Main.rand.NextFloat(0.5f, 1f); 
                }

                // Partikel Ungu Bersinar (DustID.PurpleTorch) - Lebih Sedikit
                if (Main.rand.NextBool(5)) // 20% peluang (Lebih jarang muncul)
                {
                    Dust glowingDust = Dust.NewDustDirect(dustPosition, 0, 0, DustID.VioletMoss, 0f, 0f, 50, default, Main.rand.NextFloat(1f, 1.4f));
                    
                    // Gerakan partikel bersinar sedikit berbeda arah
                    glowingDust.velocity = new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-0.3f, 0.3f));
                    glowingDust.noGravity = true;
                }
            }

            // Menambahkan efek cahaya di sekitar pusat awan kabut
            Lighting.AddLight(Projectile.Center, 0.3f, 0.1f, 0.4f);
        }

        // --- 3. MODIFIKASI SEBELUM HIT (FORCE NO KNOCKBACK) ---
        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            // LOKASI KNOCKBACK FORCE: 0f (Membuat serangan awan ini tidak mendorong player sama sekali)
            modifiers.Knockback *= 0f;
        }

        // --- 4. EFEK DEBUFF & HILANG SAAT MENGENAI PLAYER ---
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // Confused (ID 31) selama 10 detik (600 frame)
            target.AddBuff(BuffID.Confused, 600);

            // Acid Venom (ID 70) selama 3 detik (180 frame)
            target.AddBuff(70, 180);

            // LANGSUNG HILANG: Menghancurkan projectile detik ini juga setelah sukses mengenai player sekali
            Projectile.Kill();
        }
    }
}