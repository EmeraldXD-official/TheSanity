using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;

namespace TheSanity.Projectiles
{
    public class GolemWallSlamFist : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.GolemFistLeft;

        private List<Point> scannedTiles = new List<Point>();
        private bool hasScanned = false;

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;     
            Projectile.friendly = false;
            Projectile.tileCollide = true;  
            Projectile.penetrate = -1;
            Projectile.timeLeft = 36000; 

            // LAYER BELAKANG: Memaksa rantai dan tinju digambar di layer belakang Golem Head
            Projectile.hide = true; 
        }

        public override void DrawBehind(int index, List<int> behindNPCsAndTiles, List<int> behindNPCs, List<int> behindProjectiles, List<int> bars, List<int> airBorneProjectiles)
        {
            behindNPCs.Add(index); 
        }

        public override void AI()
        {
            Projectile.timeLeft = 3600; // Memastikan timeLeft proyekil utama selalu aman selama fase 30 detik

            // Mencari indeks Kepala Golem secara realtime
            int headIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == NPCID.GolemHeadFree)
                {
                    headIdx = i;
                    break;
                }
            }

            // FASE 0: MELUNCUR MENCARI DINDING ARENA
            if (Projectile.ai[1] == 0)
            {
                // [PANDUAN BALANCING: KECEPATAN TINJU MELUNCUR KE DINDING]
                float launchSpeed = 24f; 
                
                Projectile.velocity.X = Projectile.ai[0] * launchSpeed;
                Projectile.velocity.Y = 0f; 

                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

                if (Projectile.localAI[1]++ > 120f)
                {
                    Projectile.ai[1] = 2; // Paksa pulang kalau tidak ketemu dinding
                    Projectile.localAI[1] = 0;
                }
            }
            // FASE 1: MENANCAP DI DINDING & MENEMBAKKAN LASER BERKALA (DURASI LUAS & LAMA)
            else if (Projectile.ai[1] == 1)
            {
                Projectile.velocity = Vector2.Zero; 

                if (!hasScanned)
                {
                    hasScanned = true;
                    PerformVerticalAirScan(); // Memanggil pemindai dinamis lantai-atap baru
                }

                Projectile.localAI[0]++;

                // [PANDUAN BALANCING: DURASI TANGAN MENEMBAK DI DINDING (60 frame = 1 detik)]
                // Mengubah nilai ke 1800 frame agar bertahan di dinding tepat selama 30 detik sebelum ditarik pulang!
                int laserDuration = 1800; 
                if (Projectile.localAI[0] >= laserDuration)
                {
                    Projectile.ai[1] = 2; // Otomatis aktifkan Fase 2 (Tarik Pulang ke Kepala) saat waktu habis
                    Projectile.localAI[0] = 0;
                    return;
                }

                // [PANDUAN BALANCING: JEDA FREKUENSI TEMBAKAN LASER (Semakin besar angkanya, semakin lambat/berjarak)]
                // Diubah ke 45 frame agar tembakannya tenang, ritmis, dan tidak terlalu rapat memenuhi layar
                int fireRate = 45;

                if (scannedTiles.Count > 0 && Projectile.localAI[0] % fireRate == 0)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // [PANDUAN BALANCING: JUMLAH LASER YANG MUNCUL SEKALI TEMBAK ("Sedikit Tapi Pasti")]
                        // Dibatasi hanya memunculkan 1 sampai 2 laser secara acak per interval tembakan
                        int fireCount = Main.rand.Next(1, 3);
                        if (fireCount > scannedTiles.Count) fireCount = scannedTiles.Count;

                        HashSet<int> chosenIndices = new HashSet<int>();
                        while (chosenIndices.Count < fireCount)
                        {
                            chosenIndices.Add(Main.rand.Next(scannedTiles.Count));
                        }

                        foreach (int idx in chosenIndices)
                        {
                            Point tileCoords = scannedTiles[idx];
                            Vector2 beamSpawnPos = new Vector2(tileCoords.X * 16 + 8, tileCoords.Y * 16 + 8);
                            
                            // [PANDUAN BALANCING: KECEPATAN LASER EYE BEAM]
                            float laserBeamSpeed = 9.5f;
                            Vector2 beamVelocity = new Vector2(-Projectile.ai[0] * laserBeamSpeed, Main.rand.NextFloat(-0.2f, 0.2f));

                            // [PANDUAN BALANCING: DAMAGE LASER EYE BEAM]
                            int laserBeamDamage = 25;

                            Projectile.NewProjectile(
                                Projectile.GetSource_FromAI(),
                                beamSpawnPos,
                                beamVelocity,
                                ProjectileID.EyeBeam,
                                laserBeamDamage,
                                0f,
                                Main.myPlayer
                            );
                        }
                        SoundEngine.PlaySound(SoundID.Item12, Projectile.Center);
                    }
                }
            }
            // FASE 2: SIKLUS SELESAI - TINJU KETARIK PULANG KEMBALI KE KEPALA GOLEM
            else if (Projectile.ai[1] == 2)
            {
                Projectile.tileCollide = false; 

                if (headIdx != -1)
                {
                    Vector2 destination = Main.npc[headIdx].Center; 
                    Vector2 returnDir = destination - Projectile.Center;
                    float dist = returnDir.Length();

                    // Ketika sudah sampai di titik tengah kepala, hilangkan proyekil tinju dengan aman
                    if (dist < 32f)
                    {
                        Projectile.Kill(); 
                        return;
                    }

                    returnDir.Normalize();
                    
                    // [PANDUAN BALANCING: KECEPATAN TINJU KETARIK PULANG KEMBALI KE KEPALA]
                    float returnSpeed = 25f;
                    Projectile.velocity = returnDir * returnSpeed; 
                    
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                }
                else
                {
                    Projectile.Kill(); 
                }
            }
        }

        // LOGIKA REWORK: Scan dinamis menyisir kolom udara dari lantai terbawah sampai atap tertinggi arena
        private void PerformVerticalAirScan()
        {
            scannedTiles.Clear();
            Point centerTile = Projectile.Center.ToTileCoordinates();
            
            int stepX = -(int)Projectile.ai[0]; 
            int checkX = centerTile.X;

            // Cari jalur kolom udara kosong di depan dinding penancapan
            for (int i = 0; i < 5; i++)
            {
                int targetX = centerTile.X + (stepX * i);
                if (WorldGen.InWorld(targetX, centerTile.Y))
                {
                    Tile tile = Main.tile[targetX, centerTile.Y];
                    if (!tile.HasTile || !Main.tileSolid[tile.TileType])
                    {
                        checkX = targetX; 
                        break;
                    }
                }
            }

            // 1. DYNAMIC SCAN UP: Menyisir ke atas sampai menabrak atap solid / batas dunia (maksimal 90 tile)
            for (int yOffset = 0; yOffset > -90; yOffset--)
            {
                int targetY = centerTile.Y + yOffset;
                if (!WorldGen.InWorld(checkX, targetY)) break;

                Tile tile = Main.tile[checkX, targetY];
                // Jika mendeteksi block padat (atap arena), hentikan scanning ke atas
                if (tile.HasTile && Main.tileSolid[tile.TileType]) 
                {
                    break;
                }
                scannedTiles.Add(new Point(checkX, targetY));
            }

            // 2. DYNAMIC SCAN DOWN: Menyisir ke bawah mulai dari titik tengah sampai menabrak lantai solid (maksimal 90 tile)
            for (int yOffset = 1; yOffset < 90; yOffset++)
            {
                int targetY = centerTile.Y + yOffset;
                if (!WorldGen.InWorld(checkX, targetY)) break;

                Tile tile = Main.tile[checkX, targetY];
                // Jika mendeteksi block padat (lantai arena), hentikan scanning ke bawah
                if (tile.HasTile && Main.tileSolid[tile.TileType]) 
                {
                    break;
                }
                scannedTiles.Add(new Point(checkX, targetY));
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            if (Projectile.ai[1] == 0)
            {
                Projectile.ai[1] = 1; 
                Projectile.position += oldVelocity * 0.8f;
                Projectile.velocity = Vector2.Zero;
                SoundEngine.PlaySound(SoundID.Item14, Projectile.Center); 
            }
            return false; 
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int headIdx = -1;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == NPCID.GolemHeadFree) 
                {
                    headIdx = i;
                    break;
                }
            }

            if (headIdx != -1)
            {
                Vector2 parentCenter = Main.npc[headIdx].Center;
                Vector2 currentChainPos = Projectile.Center;
                Vector2 dirToParent = parentCenter - Projectile.Center;
                float chainRotation = dirToParent.ToRotation() + MathHelper.PiOver2;
                float distanceLeft = dirToParent.Length();

                Texture2D chainTexture = TextureAssets.Chain21.Value; 

                while (distanceLeft > 16f && !float.IsNaN(distanceLeft))
                {
                    dirToParent.Normalize();
                    currentChainPos += dirToParent * 16f; 
                    dirToParent = parentCenter - currentChainPos;
                    distanceLeft = dirToParent.Length();

                    Color chainColor = Lighting.GetColor((int)(currentChainPos.X / 16), (int)(currentChainPos.Y / 16));

                    Main.EntitySpriteDraw(
                        chainTexture,
                        currentChainPos - Main.screenPosition,
                        null,
                        chainColor,
                        chainRotation,
                        chainTexture.Size() / 2f,
                        1f,
                        SpriteEffects.None,
                        0
                    );
                }
            }

            Texture2D mainTexture = TextureAssets.Npc[NPCID.GolemFistLeft].Value;
            SpriteEffects effects = Projectile.ai[0] == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            Main.EntitySpriteDraw(
                mainTexture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                mainTexture.Size() / 2f,
                1f,
                effects,
                0
            );

            return false; 
        }
    }
}