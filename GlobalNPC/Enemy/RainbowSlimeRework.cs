using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles; // Memanggil folder proyektil agar bisa membaca RainbowCustomBolt

namespace TheSanity.NPCs
{
    public class RainbowSlimeRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.RainbowSlime;
        }

        // --- VARIABEL PASIF LONCAT ---
        private bool hasFiredPassiveJump = false;

        public override void AI(NPC npc)
        {
            // 1. MENCARI TARGET PLAYER TERDEKAT
            npc.TargetClosest(true);
            Player targetPlayer = Main.player[npc.target];

            // Jika player tidak ditemukan, mati, atau terlalu jauh, abaikan seluruh logika serangan
            if (targetPlayer == null || !targetPlayer.active || targetPlayer.dead || Vector2.Distance(npc.Center, targetPlayer.Center) > 800f)
            {
                return;
            }

            // =========================================================================
            // MEKANIK PASIF: MENEMBAK 3-5 SPIKE SETIAP LONCAT (TIDAK TERPENGARUH COOLDOWN)
            // =========================================================================
            if (npc.velocity.Y < -1f)
            {
                if (!hasFiredPassiveJump)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // --- LOKASI BALANCING: JUMLAH & DAMAGE SERANGAN PASIF ---
                        int jumpSpikeCount = Main.rand.Next(3, 6); 
                        int passiveDamage = 18; 

                        for (int i = 0; i < jumpSpikeCount; i++)
                        {
                            float speedX = Main.rand.NextFloat(-4f, 4f);
                            float speedY = Main.rand.NextFloat(-7f, -4f);
                            Vector2 passiveVelocity = new Vector2(speedX, speedY);

                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                npc.Center,
                                passiveVelocity,
                                ModContent.ProjectileType<RainbowCustomBolt>(), 
                                passiveDamage,
                                2f,
                                Main.myPlayer
                            );
                        }
                    }

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item21, npc.Center);
                    hasFiredPassiveJump = true;
                }
            }

            if (npc.velocity.Y == 0f)
            {
                hasFiredPassiveJump = false; // Reset deteksi tanah
            }

            // =========================================================================
            // SERANGAN UTAMA: MUNCRAT RAPID 20-40 PELURU
            // =========================================================================
            
            // --- REKUES: JEDA COOLDOWN SKILL SPAM (5 DETIK) ---
            // Kita taruh pengecekan cooldown tepat sebelum serangan utama dimulai.
            // Dengan begini, biarpun cooldown semburan aktif, serangan pasif lompat di atas TETAP bisa keluar!
            if (npc.localAI[1] > 0f)
            {
                npc.localAI[1]--; // Hitung mundur timer cooldown
                return; 
            }

            // LOGIKA STATE AKTIF MENYEMPROT (STATE = 1)
            if (npc.localAI[0] == 1f)
            {
                npc.velocity.X *= 0.5f; // Paksa diam saat menyembur

                npc.localAI[2]++; // Timer rapid-fire
                if (npc.localAI[2] >= 2f) 
                {
                    npc.localAI[2] = 0f; 

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // --- BALANCING KECEPATAN MUNCRAT ---
                        float speedX = Main.rand.NextFloat(-2.5f, 2.5f);
                        float speedY = Main.rand.NextFloat(-12f, -8f);
                        Vector2 shootVelocity = new Vector2(speedX, speedY);

                        int projectileDamage = 25; 

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center + new Vector2(0, -16f), 
                            shootVelocity,
                            ModContent.ProjectileType<RainbowCustomBolt>(),
                            projectileDamage,
                            3f,
                            Main.myPlayer
                        );
                    }

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, npc.Center);
                    npc.localAI[3]++; // Hitung peluru keluar
                }

                // Cek apakah target acak peluru (20-40) sudah tercapai
                if (npc.localAI[3] >= npc.ai[3])
                {
                    npc.localAI[0] = 0f;  // Kembalikan ke State Idle
                    npc.localAI[3] = 0f;  // Reset hitungan peluru
                    
                    // --- REKUES BALANCING: COOLDOWN 5 DETIK ---
                    // 5 detik dikali 60 frame per detik = 300 frame
                    npc.localAI[1] = 300f; 
                }
            }
            // LOGIKA STATE BERSIAP MENYERANG / IDLE (STATE = 0)
            else
            {
                npc.localAI[0] = 1f; // Set status ke mode menyerang
                npc.localAI[2] = 0f; // Reset delay tembakan rapid
                npc.localAI[3] = 0f; // Reset jumlah peluru saat ini

                // Mengundi jumlah total peluru yang akan dikeluarkan (20 sampai 40 biji)
                npc.ai[3] = Main.rand.Next(20, 41); 
            }
        }
    }
}