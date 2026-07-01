using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace TheSanity
{
    public class BoneSerpentRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer mandiri khusus untuk mendeteksi serangan
        private int chainTimer = 0;
        private int burstTimer = 0;

        // Menyimpan urutan indeks segmen tubuh untuk diserang bergantian
        private int currentChainSegmentIndex = -1;
        private int segmentShotDelay = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            // Hanya aktif untuk Kepala Bone Serpent (ID: 39)
            return entity.type == NPCID.BoneSerpentHead;
        }

        public override void PostAI(NPC npc)
        {
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead) return;

            // Pastikan server yang menghitung spawn proyekil agar sinkron di multiplayer
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // Hitung maju timer berdasarkan frame (60 frame = 1 detik)
            if (currentChainSegmentIndex == -1)
            {
                chainTimer++;
            }
            burstTimer++;

            // =========================================================
            // SKILL 1: SEMBURAN BOLA API BERANTAI (1 PELURU = 1 BADAN BERGANTIAN)
            // Cooldown: 5 Detik (300 Frame) Sebelum Rentetan Dimulai
            // =========================================================
            if (chainTimer >= 300 && currentChainSegmentIndex == -1)
            {
                // Mulai rentetan dari segmen pertama (indeks 0 = Kepala)
                currentChainSegmentIndex = 0;
                segmentShotDelay = 0;
            }

            // Jika rentetan berantai sedang berjalan
            if (currentChainSegmentIndex >= 0)
            {
                segmentShotDelay++;

                // BALANCING GUIDE: Mengatur jeda waktu peluru antar segmen (setiap 6 frame = 0.1 detik)
                if (segmentShotDelay >= 6) 
                {
                    segmentShotDelay = 0; // Reset jeda untuk segmen berikutnya

                    // Kumpulkan semua segmen milik Bone Serpent ini secara berurutan
                    List<NPC> bodySegments = new List<NPC>();
                    
                    // Masukkan kepala sebagai urutan pertama
                    bodySegments.Add(npc);

                    // Cari semua bagian badan dan ekor yang terhubung
                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC segment = Main.npc[i];
                        if (segment.active && segment.realLife == npc.whoAmI && segment.whoAmI != npc.whoAmI)
                        {
                            bodySegments.Add(segment);
                        }
                    }

                    // Jika indeks saat ini masih dalam jangkauan total segmen tubuh
                    if (currentChainSegmentIndex < bodySegments.Count)
                    {
                        NPC shootingSegment = bodySegments[currentChainSegmentIndex];

                        if (shootingSegment.active)
                        {
                            Vector2 shootVelocity = target.Center - shootingSegment.Center;
                            shootVelocity.Normalize();
                            
                            // [FIREBALL SPEED LOCATION]
                            shootVelocity *= 7.5f; 

                            // BALANCING GUIDE: Damage disetel di angka 25
                            int p = Projectile.NewProjectile(npc.GetSource_FromAI(), shootingSegment.Center, shootVelocity, ProjectileID.Fireball, 25, 1f, Main.myPlayer);
                            
                            if (p != Main.maxProjectiles)
                            {
                                Main.projectile[p].aiStyle = -1; // Matikan AI gravitasi biar meluncur lurus
                                Main.projectile[p].hostile = true;
                                Main.projectile[p].friendly = false;
                                Main.projectile[p].tileCollide = false; // [IGNORE WALLS LOCK] Paksa tembus block
                                Main.projectile[p].timeLeft = 240; 
                            }

                            SoundEngine.PlaySound(SoundID.Item20, shootingSegment.Center);
                        }

                        // Lanjut ke segmen berikutnya di tubuh belakangnya
                        currentChainSegmentIndex++;
                    }
                    else
                    {
                        // Jika ekor paling belakang sudah menembak, reset seluruh sistem ke mode cooldown
                        currentChainSegmentIndex = -1;
                        chainTimer = 0;
                    }
                }
            }

            // =========================================================
            // SKILL 2: BURST 5 BOLA API SEKALIGUS DARI KEPALA
            // Cooldown: 10 Detik (600 Frame)
            // =========================================================
            if (burstTimer >= 600)
            {
                burstTimer = 0; 

                Vector2 baseVelocity = target.Center - npc.Center;
                baseVelocity.Normalize();
                
                // [BURST SPEED LOCATION]
                baseVelocity *= 9f; 

                float spreadAngle = MathHelper.ToRadians(45f); 
                int totalShots = 5;

                for (int i = 0; i < totalShots; i++)
                {
                    float factor = (float)i / (totalShots - 1);
                    float angleOffset = (factor * spreadAngle) - (spreadAngle / 2f);
                    Vector2 finalVelocity = baseVelocity.RotatedBy(angleOffset);

                    // BALANCING GUIDE: Damage burst disetel di angka 30
                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, finalVelocity, ProjectileID.Fireball, 30, 1f, Main.myPlayer);
                    
                    if (p != Main.maxProjectiles)
                    {
                        Main.projectile[p].aiStyle = -1; 
                        Main.projectile[p].hostile = true;
                        Main.projectile[p].friendly = false;
                        Main.projectile[p].tileCollide = false; // [IGNORE WALLS LOCK] Paksa tembus block
                        Main.projectile[p].timeLeft = 240;
                    }
                }

                SoundEngine.PlaySound(SoundID.Item73, npc.Center); 
            }
        }
    }

    // =========================================================
    // GLOBAL PROJECTILE: PERGERAKAN LURUS & PARTIKEL BAWAAN ASLI
    // =========================================================
    public class BoneSerpentFireballMod : GlobalProjectile
    {
        public override void AI(Projectile projectile)
        {
            // Hanya modifikasi Fireball kustom kita yang meluncur lurus tembus block
            if (projectile.type == ProjectileID.Fireball && projectile.aiStyle == -1)
            {
                projectile.position += projectile.velocity;

                // --- NATURAL VANILLA PARTICLES ONLY ---
                // Mengembalikan efek partikel api oranye asli bawaan engine Terraria tanpa campuran warna lain
                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.Torch, 0f, 0f, 100, default, 1.5f);
                    d.noGravity = true;
                    d.velocity *= 0.3f;
                }

                Lighting.AddLight(projectile.Center, 0.6f, 0.4f, 0.1f); // Cahaya oranye hangat alami
            }
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (projectile.type == ProjectileID.Fireball && projectile.aiStyle == -1)
            {
                // [DEBUFF DURATION BALANCING LOCATION]
                target.AddBuff(BuffID.OnFire, 240);
            }
        }
    }
}