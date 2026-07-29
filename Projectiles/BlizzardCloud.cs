using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class BlizzardCloud : ModProjectile
    {
        // CONTOH CODE: Menggunakan asset kosong bawaan game agar tidak perlu repot bikin file PNG transparan
        public override string Texture => "Terraria/Images/Projectile_0";

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;    // Murni menyerang player
            Projectile.friendly = false;  // Tidak melukai sesama monster
            
            // LOKASI MENEMBUS DINDING: Diubah ke false agar awan bisa meluncur menembus block (No tile collision)
            Projectile.tileCollide = false; 
            
            Projectile.ignoreWater = true;
            
            // LOKASI LIFETIME PELURU: 600 frame = 10 Detik
            Projectile.timeLeft = 600;
            
            // CONTOH CODE: Set alpha ke maksimal (255) untuk menyembunyikan sisa visual sprite aslinya
            Projectile.alpha = 255;
            
            Projectile.aiStyle = -1; 
        }

        // CONTOH CODE: Menyesuaikan parameter PreDraw agar pas dengan mod kamu dan mengembalikan false (pure particle)
        public override bool PreDraw(ref Color lightColor) => false;

        public override void AI()
        {
            // --- LOGIKA VISUAL: MEMBENTUK AWAN TEBAL PADUAN BIRU & PUTIH ---
            // Kita keluarkan partikel debu tebal di setiap frame agar wujud awannya terbentuk sempurna
            for (int i = 0; i < 3; i++)
            {
                int dustType = Main.rand.NextBool() ? DustID.IceTorch : DustID.Snow;
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, dustType, 0f, 0f, 100, default, 1.8f);
                d.noGravity = true;
                d.velocity *= 0.4f; // Menjaga partikel tetap berkumpul padat membentuk wujud awan kabut
            }

            // Memberikan sedikit pencahayaan es biru lembut di sekitar area awan
            Lighting.AddLight(Projectile.Center, 0.2f, 0.4f, 0.6f);

            // --- LOGIKA HOMING (MENGUNCI & MENGEJAR PLAYER) ---
            float maxDetectRadius = 800f; // Jarak pandang homing awan
            int targetIndex = -1;
            float closestDistance = maxDetectRadius;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead)
                {
                    float dist = Vector2.Distance(p.Center, Projectile.Center);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        targetIndex = i;
                    }
                }
            }

            // Jika player ditemukan, belokkan arah peluru secara halus menuju koordinat player
            if (targetIndex != -1)
            {
                Player closestPlayer = Main.player[targetIndex];
                Vector2 desiredVelocity = closestPlayer.Center - Projectile.Center;
                desiredVelocity.Normalize();
                
                // LOKASI KECEPATAN GERAKAN: Mengunci kecepatan konstan gerak peluru di angka 3.0f sesuai request
                float speed = 3.0f;
                desiredVelocity *= speed;

                int turnResistance = 12; // Semakin kecil angka, belokan homing semakin tajam
                Projectile.velocity = (Projectile.velocity * (turnResistance - 1) + desiredVelocity) / turnResistance;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // LOKASI INFLICT DEBUFF: Menyuntikkan debuff Frostbite (ID 322) selama 180 frame = 3 detik
            target.AddBuff(BuffID.Frostburn2, 180);

            // Langsung lenyap/hancur setelah mengenai badan player
            Projectile.Kill();
        }
    }
}