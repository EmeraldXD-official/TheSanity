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
    // ⚔️ PILAR PRE-HM: VilethornSpikeShaft (BADAN / BASE)
    // =========================================================================
    public class VilethornSpikeShaft : ModProjectile
    {
        // Mengubah visual sprite menggunakan varian Vilethorn Base vanilla
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.VilethornBase}";

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
            // Biarkan vanilla render mendeteksi pergerakan statis
            Projectile.velocity = new Vector2(0f, -0.0001f); 

            if (Main.rand.NextBool(3))
            {
                // Menyesuaikan dust ke tema Corruption (Demonite)
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Demonite, Main.rand.NextVector2Circular(2f, 2f), 100, default, 1.0f);
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
            int maxRequiredBlocks = (int)Projectile.localAI[1];

            if (currentBlockIndex == 1 && maxRequiredBlocks == 0)
            {
                // Mencari target terdekat
                Player targetPlayer = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
                if (targetPlayer != null && targetPlayer.active && !targetPlayer.dead)
                {
                    float pixelDistanceToPlayer = Projectile.Center.Y - targetPlayer.Center.Y;
                    int blockDistanceToPlayer = (int)(pixelDistanceToPlayer / 16f);

                    // [DISTANCE LOCATION]: Melebihi tinggi player sebanyak 20 Block
                    maxRequiredBlocks = blockDistanceToPlayer + 20;

                    // Batasan aman (Safe-guard) struktur pilar
                    if (maxRequiredBlocks < 5) maxRequiredBlocks = 5; 
                    if (maxRequiredBlocks > 150) maxRequiredBlocks = 150; 

                    Projectile.localAI[1] = maxRequiredBlocks;
                }
                else
                {
                    maxRequiredBlocks = 30; // Fallback jika player tidak terdeteksi
                    Projectile.localAI[1] = maxRequiredBlocks;
                }
            }

            Projectile.ai[1]++;

            // --- ⏱️ LOKASI BALANCING: KECEPATAN TUMBUH BERANTAI (DELAY FRAME) ---
            // Semakin kecil nilainya, semakin cepat pilar mencuat ke atas (1 = Instan)
            int growthDelay = 2; 

            if (Projectile.ai[1] == growthDelay)
            {
                if (currentBlockIndex < maxRequiredBlocks - 1)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        float segmentOffset = 16f; // Jarak per 1 grid tile
                        Vector2 spawnPosition = new Vector2(Projectile.Center.X, Projectile.Center.Y - segmentOffset);

                        // [DAMAGE LOCATION]: Diturunkan otomatis mengikuti passing damage dari NPC pemanggil (Varian Pre-HM)
                        int newlySpawnedIndex = Projectile.NewProjectile(
                            Projectile.GetSource_FromAI(),
                            spawnPosition,
                            new Vector2(0f, -0.0001f), 
                            Projectile.type,     
                            Projectile.damage, // Nilai damage diatur saat NPC memanggil projectile ini
                            Projectile.knockBack,
                            Main.myPlayer,
                            Projectile.ai[0] + 1, 
                            0f
                        );
                        
                        if (newlySpawnedIndex >= 0 && newlySpawnedIndex < Main.maxProjectiles)
                        {
                            Main.projectile[newlySpawnedIndex].localAI[1] = maxRequiredBlocks;
                        }
                    }
                }
                // Saat mencapai ujung atas, panggil bagian Pucuk/Tip Vilethorn
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
                            ModContent.ProjectileType<VilethornSpikeHead>(), 
                            Projectile.damage,
                            Projectile.knockBack,
                            Main.myPlayer,
                            maxRequiredBlocks 
                        );
                    }
                }
            }

            // =========================================================================
            // SYSTEM SINKRONISASI MATI BERSAMAAN (DESPAWN TIMER)
            // =========================================================================
            bool isTopStructureReady = false;
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == ModContent.ProjectileType<VilethornSpikeHead>() && p.owner == Projectile.owner)
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

                // --- ⏳ LOKASI BALANCING: DURASI BADAN KRISTAL BERTAHAN DI ARENA ---
                // 150 Ticks = 2.5 Detik pilar diam menetap sebelum hancur bersamaan
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
    // ⚔️ PILAR PRE-HM: VilethornSpikeHead (PUCUK / TIP LANCIIP)
    // =========================================================================
    public class VilethornSpikeHead : ModProjectile
    {
        // Mengubah visual sprite menggunakan varian Vilethorn Tip vanilla
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.VilethornTip}";

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
            Projectile.velocity = new Vector2(0f, -0.0001f); 

            // Mengirimkan sinyal balik ke segmen Shaft bawah bahwa struktur pilar atas sudah selesai dibuat
            Projectile.ai[1] = 1f; 

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.Demonite, Main.rand.NextVector2Circular(4f, 4f), 50, default, 1.3f);
                d.noGravity = true;
            }

            Projectile.localAI[0]++;

            // --- ⏳ LOKASI BALANCING: DURASI PUCUK KRISTAL BERTAHAN DI ARENA ---
            // Harus disamakan dengan nilai durasi Shaft di atas (150f) agar hilangnya kompak
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