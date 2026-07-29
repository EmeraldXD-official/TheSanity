using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    // =========================================================================
    // FIX DINAMIS: CrystalShardShaft (TINGGI MENYESUAIKAN PLAYER + 20 BLOCK)
    // =========================================================================
    public class CrystalShardShaft : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.CrystalVileShardShaft}";

        public override void SetDefaults()
        {
            Projectile.width = 16;                
            Projectile.height = 16;
            Projectile.aiStyle = -1;             
            Projectile.friendly = false;         
            Projectile.hostile = true;           
            Projectile.penetrate = -1;           
            Projectile.ignoreWater = true;
            Projectile.tileCollide = true;       

            Projectile.timeLeft = 600; 
            Projectile.scale = 1.2f;
        }

        public override void AI()
        {
            // BIARKAN VANILLA RENDER
            Projectile.velocity = new Vector2(0f, -0.0001f); 

            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PinkCrystalShard, Main.rand.NextVector2Circular(2f, 2f), 100, default, 1.0f);
                d.noGravity = true;
            }

            // =========================================================================
            // LOGIKA KALKULASI TINGGI DINAMIS BERDASARKAN PLAYER
            // =========================================================================
            if (Projectile.ai[0] == 0)
            {
                Projectile.ai[0] = 1; // Index block saat ini (Block ke-1)
            }

            int currentBlockIndex = (int)Projectile.ai[0];
            
            // --- LOKASI BALANCING & KALKULASI TARGET SEGMENT ---
            // localAI[1] digunakan untuk menyimpan batas maksimum total block yang harus dicapai
            int maxRequiredBlocks = (int)Projectile.localAI[1];

            if (currentBlockIndex == 1 && maxRequiredBlocks == 0)
            {
                // Cari player terdekat dari base pilar pertama
                Player targetPlayer = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
                if (targetPlayer != null && targetPlayer.active && !targetPlayer.dead)
                {
                    // Hitung jarak vertikal dari base kristal ke kepala player dalam satuan pixel
                    float pixelDistanceToPlayer = Projectile.Center.Y - targetPlayer.Center.Y;
                    
                    // Konversi pixel ke total block (1 block = 16 pixel)
                    int blockDistanceToPlayer = (int)(pixelDistanceToPlayer / 16f);

                    // --- REKUES KHUSUS: MELEBIHI PLAYER SEBANYAK 20 BLOCK ---
                    maxRequiredBlocks = blockDistanceToPlayer + 20;

                    // Batasan proteksi (Safe-guard) agar tidak terjadi loop spawning tak terbatas jika di luar jangkauan
                    if (maxRequiredBlocks < 5) maxRequiredBlocks = 5; 
                    if (maxRequiredBlocks > 150) maxRequiredBlocks = 150; 

                    Projectile.localAI[1] = maxRequiredBlocks;
                }
                else
                {
                    // Jika player tidak ditemukan/mati saat spawn awal, gunakan default fallback 30 block
                    maxRequiredBlocks = 30;
                    Projectile.localAI[1] = maxRequiredBlocks;
                }
            }

            // AI parameter pencatat waktu interval (Growth Timer)
            Projectile.ai[1]++;

            // --- LOKASI BALANCING: KECEPATAN TUMBUH BERANTAI (DELAY FRAME) ---
            int growthDelay = 2; 

            if (Projectile.ai[1] == growthDelay)
            {
                // REKUES KHUSUS: Badan terus menyusun ke atas jika belum mencapai batas maxRequiredBlocks - 1
                if (currentBlockIndex < maxRequiredBlocks - 1)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        float segmentOffset = 16f; // Geser tepat 1 grid block ke atas
                        Vector2 spawnPosition = new Vector2(Projectile.Center.X, Projectile.Center.Y - segmentOffset);

                        // FIX CS0029: NewProjectile langsung mengembalikan ID indeks (int) proyektil yang baru dibuat
                        int newlySpawnedIndex = Projectile.NewProjectile(
                            Projectile.GetSource_FromAI(),
                            spawnPosition,
                            new Vector2(0f, -0.0001f), 
                            Projectile.type,     
                            Projectile.damage,
                            Projectile.knockBack,
                            Main.myPlayer,
                            Projectile.ai[0] + 1, // ai[0] = Mengirim index selanjutnya (currentBlockIndex + 1)
                            0f
                        );
                        
                        // Sistem Estafet: Mengirimkan hasil kalkulasi limit tinggi ke segmen baru lewat localAI[1] proyektil baru
                        if (newlySpawnedIndex >= 0 && newlySpawnedIndex < Main.maxProjectiles)
                        {
                            Main.projectile[newlySpawnedIndex].localAI[1] = maxRequiredBlocks;
                        }
                    }
                }
                // REKUES KHUSUS: Saat mencapai target limit terakhir, panggil bagian Pucuk/Head lancip sebagai penutup
                else if (currentBlockIndex == maxRequiredBlocks - 1)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        float segmentOffset = 16f;
                        Vector2 spawnPosition = new Vector2(Projectile.Center.X, Projectile.Center.Y - segmentOffset);

                        Projectile.NewProjectile(
                            Projectile.GetSource_FromAI(),
                            spawnPosition,
                            new Vector2(0f, -0.0001f), 
                            ModContent.ProjectileType<CrystalShardHead>(), 
                            Projectile.damage,
                            Projectile.knockBack,
                            Main.myPlayer,
                            maxRequiredBlocks // Kirim total block akhir ke ai[0] milik Head
                        );
                    }
                }
            }

            // =========================================================================
            // SYSTEM SINKRONISASI MATI BERSAMAAN (GLOBAL TIMER DESPAWN)
            // =========================================================================
            bool isTopStructureReady = false;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == ModContent.ProjectileType<CrystalShardHead>() && p.owner == Projectile.owner)
                {
                    if (p.ai[1] >= 1f) 
                    {
                        isTopStructureReady = true;
                        break;
                    }
                }
            }

            if (isTopStructureReady)
            {
                Projectile.localAI[0]++;

                // --- LOKASI BALANCING: DURASI PILAR KRISTAL BERTAHAN DI ARENA ---
                if (Projectile.localAI[0] >= 150f)
                {
                    Projectile.Kill(); 
                }
            }
        }

        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 hitboxModifier)
        {
            fallThrough = true; 
            return true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            return true; 
        }
    }

    // =========================================================================
    // CrystalShardHead (PUCUK LANCIK - SINKRON DENGAN TINGGI DINAMIS)
    // =========================================================================
    public class CrystalShardHead : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.CrystalVileShardHead}";

        public override void SetDefaults()
        {
            Projectile.width = 18;                
            Projectile.height = 18;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;
            Projectile.hostile = true;           
            Projectile.penetrate = -1;
            Projectile.tileCollide = true;       

            Projectile.timeLeft = 600; 
            Projectile.scale = 1.2f;
        }

        public override void AI()
        {
            // BIARKAN VANILLA RENDER
            Projectile.velocity = new Vector2(0f, -0.0001f); 

            // ai[1] diatur ke 1f sebagai sinyal ke seluruh badan (Shaft) bahwa pilar sudah komplit dan timer despawn dimulai
            Projectile.ai[1] = 1f; 

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PinkCrystalShard, Main.rand.NextVector2Circular(4f, 4f), 50, default, 1.3f);
                d.noGravity = true;
            }

            Projectile.localAI[0]++;

            // --- LOKASI BALANCING: DURASI KEPALA KRISTAL BERTAHAN DI ARENA ---
            if (Projectile.localAI[0] >= 150f)
            {
                Projectile.Kill(); 
            }
        }

        public override bool TileCollideStyle(ref int width, ref int height, ref bool fallThrough, ref Vector2 modifier)
        {
            fallThrough = true;
            return true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            return true; 
        }
    }
}