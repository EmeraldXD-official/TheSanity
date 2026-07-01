using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using System;

namespace TheSanity.Projectiles
{
    public class ClownHappyBomb : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.HappyBomb}";

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults()
        {
            Projectile.width = 22;
            Projectile.height = 22;
            
            Projectile.hostile = true;       
            Projectile.friendly = false;     
            Projectile.tileCollide = true;   // Tetap memantul di tanah/tile
            Projectile.penetrate = -1;       // Tetap -1 agar TIDAK HANCUR saat menabrak Player/NPC!
            
            // --- KEMBALI KE AI EXPLOSIVE VANILLA ---
            // Menggunakan AI bawaan bom agar otomatis berputar dan menggelinding alami
            Projectile.aiStyle = ProjAIStyleID.Explosive; 
            AIType = ProjectileID.HappyBomb;
            
            // [HAPPY BOMB LIFETIME BALANCING LOCATION]
            Projectile.timeLeft = 300;       // 5 detik sebelum meledak otomatis
        }

        // Kita override OnTileCollide agar dia memantul alami dan TIDAK mati saat menyentuh blok
        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (Projectile.velocity.X != oldVelocity.X) Projectile.velocity.X = -oldVelocity.X * 0.6f;
            if (Projectile.velocity.Y != oldVelocity.Y) Projectile.velocity.Y = -oldVelocity.Y * 0.4f;
            return false; // Mengembalikan false agar proyektil tidak hancur saat menabrak dinding/lantai
        }

        public override void OnKill(int timeLeft)
        {
            // Suara ledakan utama bom
            SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // Jangkauan 10 Block = 10 * 16 pixel = 160 pixel
                // [EXPLOSION RANDOM RADIUS BALANCING LOCATION]
                float randomRadius = 160f; 

                // Mengeluarkan 4 ledakan di posisi acak di dalam area radius 10 block
                for (int i = 0; i < 4; i++)
                {
                    float randomOffsetX = Main.rand.NextFloat(-randomRadius, randomRadius);
                    float randomOffsetY = Main.rand.NextFloat(-randomRadius, randomRadius);
                    Vector2 randomSpawnPos = Projectile.Center + new Vector2(randomOffsetX, randomOffsetY);

                    // [EXPLOSION DAMAGE BALANCING LOCATION]
                    int nuclearDamage = 1000; 

                    int expProj = Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        randomSpawnPos,
                        Vector2.Zero,
                        ProjectileID.DD2ExplosiveTrapT3Explosion, 
                        nuclearDamage,
                        0f,
                        Main.myPlayer
                    );

                    if (expProj != Main.maxProjectiles)
                    {
                        Main.projectile[expProj].hostile = true;
                        Main.projectile[expProj].friendly = false;
                        Main.projectile[expProj].netUpdate = true;
                    }
                }
            }

            // Partikel hancuran debu asap hitam pekat
            for (int g = 0; g < 20; g++)
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0f, 0f, 100, default, 1.6f);
                d.velocity *= 2f;
            }
        }
    }
}