using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class FishRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // =========================================================================
        // [MULTIPLAYER SYNC SYSTEM]: Mengganti bool biasa ke AI slots bawaan Terraria
        // =========================================================================
        // npc.ai[1] -> Digunakan untuk attackTimer
        // npc.ai[2] -> Digunakan untuk randomCooldown
        // npc.ai[3] -> Status Fase: 0 = Berenang, 1 = Melempar/Breaching, 2 = Dive Bombing
        
        private const float Gravity = 0.4f; 

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            // Daftarkan semua jenis ikan predator yang ingin di-rework
            return entity.type == 58 || entity.type == 102 || entity.type == 157 || entity.type == 57 || entity.type == 465 || entity.type == 241;
        }

        public override bool PreAI(NPC npc)
        {
            // Pastikan target valid
            if (npc.target < 0 || npc.target == 255) npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead) return true;

            // Set jeda acak (10 - 15 detik) jika belum terisi
            if (npc.ai[2] == 0)
            {
                npc.ai[2] = Main.rand.Next(600, 901);
            }

            float distance = Vector2.Distance(npc.Center, target.Center);
            float detectionRadius = 800f; // 50 Blocks

            // --- LOGIKA TRIGGER SKILL (Hanya diproses oleh Server / Singleplayer) ---
            if (npc.ai[3] == 0) // Jika sedang status Berenang biasa
            {
                if (distance < detectionRadius)
                {
                    npc.ai[1]++; // attackTimer jalan
                    if (npc.ai[1] >= npc.ai[2])
                    {
                        npc.ai[1] = 0; // Reset timer
                        npc.ai[2] = Main.rand.Next(600, 901); // Acak ulang cooldown
                        npc.ai[3] = 1; // Ubah status menjadi 1 (Breaching)

                        // Hanya server yang menentukan kecepatan awal lemparan ketapel
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            LaunchLikeCatapult(npc, target);
                        }
                    }
                }
                else
                {
                    if (npc.ai[1] > 0) npc.ai[1]--;
                }
            }

            // --- OVERRIDE VANILLA PENUH SAAT MENYERANG ---
            if (npc.ai[3] > 0) // Jika statusnya 1 (Breaching) atau 2 (Dive Bombing)
            {
                ExecuteCatapultAndDive(npc, target);
                return false; // Matikan AI asli agar Client & Server tidak bentrok melompat
            }

            return true;
        }

        private void LaunchLikeCatapult(NPC npc, Player target)
        {
            npc.noTileCollide = true; 
            
            Vector2 targetPos = target.Center;
            float diffX = targetPos.X - npc.Center.X;
            float diffY = targetPos.Y - npc.Center.Y;

            // BALANCING LOCATION: Tinggi lompatan awal ikan
            float launchHeight = -18f; 
            if (diffY < 0) launchHeight += diffY * 0.4f; 

            float time = (float)Math.Sqrt(2f * launchHeight / -Gravity);
            float velX = diffX / (time * 2f); 
            velX = MathHelper.Clamp(velX, -20f, 20f);

            npc.velocity.X = velX;
            npc.velocity.Y = launchHeight;
            
            // Perintahkan server untuk menyamakan posisi & kecepatan ikan ke semua client
            npc.netUpdate = true;
        }

        private void ExecuteCatapultAndDive(NPC npc, Player target)
        {
            npc.noTileCollide = true; // Paksa tembus blok baik di Server maupun Client secara kompak

            // Efek partikel tebal Aqua Scepter (Bisa dijalankan di Client)
            if (Main.netMode != NetmodeID.Server)
            {
                for (int i = 0; i < 4; i++)
                {
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, 172, 0f, 0f, 100, default, 1.4f);
                    d.noGravity = true;
                    d.velocity *= 0.1f;
                }
            }

            // --- DETEKSI PAS DI ATAS KEPALA PLAYER (FORCE HENTAKAN BAWAH) ---
            if (npc.ai[3] == 1) // Masih fase melayang naik ke atas
            {
                npc.velocity.Y += Gravity;

                float diffX = Math.Abs(target.Center.X - npc.Center.X);

                // Jika posisi X ikan sudah pas di atas player (toleransi 35 pixel) DAN posisi ikan berada di atas player
                if (diffX < 35f && npc.Center.Y < target.Center.Y)
                {
                    npc.ai[3] = 2; // Naikkan status ke fase 2 (Dive Bombing)
                    npc.velocity.X = 0f; 

                    // BALANCING LOCATION: Kecepatan hentakan lurus menukik ke bawah (24f)
                    npc.velocity.Y = 24f; 

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, npc.Center);
                    npc.netUpdate = true; // Sinkronisasikan perubahan sentakan menukik ke seluruh player
                }
            }
            else if (npc.ai[3] == 2) // Sedang menghentak ke bawah
            {
                npc.velocity.X = 0f;
                npc.velocity.Y = 24f;
            }

            // --- KONDISI RESET / MENDARAT ---
            bool hitPlayer = npc.Hitbox.Intersects(target.Hitbox);
            bool landedBelowPlayer = npc.Center.Y >= target.Center.Y && !Collision.CanHitLine(npc.position, npc.width, npc.height, npc.position + npc.velocity, npc.width, npc.height);

            // Keputusan pendaratan krusial dilakukan oleh server agar tidak terjadi bug reset sepihak
            if (hitPlayer || landedBelowPlayer || npc.velocity.Length() < 1f)
            {
                npc.ai[3] = 0; // Kembalikan status ke Berenang biasa (0)
                npc.ai[1] = 0; // Bersihkan timer serangan
                npc.noTileCollide = false; // Kembalikan fisik normal agar bisa berenang lagi
                npc.velocity *= 0.1f; 
                npc.netUpdate = true; // Kirim paket data akhir pendaratan

                // Suara & partikel splash saat mendarat
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item19, npc.Center);
                if (Main.netMode != NetmodeID.Server)
                {
                    for (int i = 0; i < 20; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, 172, Main.rand.NextFloat(-4f, 4f), -3f, 100, default, 1.5f);
                        d.noGravity = true;
                    }
                }
            }
        }
    }
}