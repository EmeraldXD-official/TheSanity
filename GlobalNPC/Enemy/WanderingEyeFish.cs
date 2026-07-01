using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using System;

namespace TheSanity
{
    public class WanderingEyeFishRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Penanda agar spawn Demon Eye hanya terpicu satu kali pas baru muncul
        private bool hasSpawnedMinions = false;

        // Timer cooldown serangan tembakan rentetan darah
        private int attackTimer = 0;

        public override bool AppliesToEntity(NPC npc, bool lateInstantiation)
        {
            // Mengunci target rework pada Wandering Eye Fish murni (ID: 586)
            return npc.type == NPCID.EyeballFlyingFish;
        }

        public override void PostAI(NPC npc)
        {
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead) return;

            // Pastikan kalkulasi spawn dan projectile diproses oleh server/singleplayer murni
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // =========================================================
            // MEKANIK 1: SPAWN KAWANAN DEMON EYE SAAT BARU SPAWN (IRL LURUS)
            // =========================================================
            if (!hasSpawnedMinions)
            {
                hasSpawnedMinions = true; // Kunci saklar agar tidak berulang terus tiap frame

                // Menentukan jumlah spawn acak antara 10 sampai 15 ekor
                int totalMinions = Main.rand.Next(10, 16);

                for (int i = 0; i < totalMinions; i++)
                {
                    // Berikan sedikit jarak sebaran posisi acak di sekitar tubuh utama biar tidak menumpuk satu titik
                    Vector2 spawnOffset = Main.rand.NextVector2Circular(50f, 50f);
                    
                    // Spawn Demon Eye vanilla murni (ID: 2)
                    int minionIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)(npc.Center.X + spawnOffset.X), (int)(npc.Center.Y + spawnOffset.Y), NPCID.DemonEye);
                    
                    if (minionIndex != Main.maxNPCs)
                    {
                        NPC minion = Main.npc[minionIndex];
                        minion.target = npc.target; // Paksa minion langsung mengunci target player yang sama
                        
                        // Berikan efek partikel kepulan asap darah mistis di posisi spawn tiap mata
                        for (int j = 0; j < 5; j++)
                        {
                            Dust d = Dust.NewDustDirect(minion.position, minion.width, minion.height, DustID.Blood, 0f, 0f, 100, default, 1.3f);
                            d.velocity = Main.rand.NextVector2Circular(3f, 3f);
                        }
                    }
                }

                // Suara raungan sihir masif saat kawanan pelayan muncul dari kabut darah
                SoundEngine.PlaySound(SoundID.Roar, npc.Center);
                npc.netUpdate = true;
            }

            // =========================================================
            // MEKANIK 2: RENTETAN TEMBAKAN BLOOD NAUTILUS SHOT (COOLDOWN 3 DETIK)
            // =========================================================
            attackTimer++;

            // [ATTACK COOLDOWN BALANCING LOCATION]
            // Jeda siklus tembakan dilepas setiap 3 detik sekali (180 Frame murni)
            if (attackTimer >= 180)
            {
                // Jangkauan tembakan aktif jika jarak musuh ke player di bawah 500 pixel
                if (Vector2.Distance(npc.Center, target.Center) < 500f)
                {
                    // Ambil arah target lurus menuju jantung koordinat player
                    Vector2 shootVelocity = target.Center - npc.Center;
                    shootVelocity.Normalize();

                    // [PROJECTILE SPEED BALANCING LOCATION]
                    // Mengatur kecepatan laju peluru darah Dreadnautilus (Kecepatan: 7f)
                    shootVelocity *= 7f;

                    // Tambahkan sedikit akurasi acak (spread) tipis agar lintasan peluru dinamis
                    shootVelocity = shootVelocity.RotatedByRandom(MathHelper.ToRadians(8f));

                    // Memanggil ProjectileID.BloodNautilusShot (ID: 751) bawaan bos Dreadnautilus murni
                    // BALANCING GUIDE: Damage rentetan jarum darah ini diset sebesar 22
                    int p = Projectile.NewProjectile(
                        npc.GetSource_FromAI(), 
                        npc.Center, 
                        shootVelocity, 
                        ProjectileID.BloodNautilusShot, 
                        22, 
                        1f, 
                        Main.myPlayer
                    );

                    if (p != Main.maxProjectiles)
                    {
                        Main.projectile[p].hostile = true;
                        Main.projectile[p].friendly = false;
                        Main.projectile[p].netUpdate = true;
                    }

                    // Efek audio laser air/darah tajam melesat di udara tiap siklusnya
                    SoundEngine.PlaySound(SoundID.Item85, npc.Center);

                    // Efek kilatan debu merah di moncong/mata ikan saat menembak
                    for (int i = 0; i < 4; i++)
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.CrimsonTorch, 0f, 0f, 50, default, 1.2f);
                        d.velocity = shootVelocity * 0.3f;
                    }
                }

                // Reset timer kembali ke nol untuk memulai ulang siklus 3 detik berikutnya
                attackTimer = 0;
                npc.netUpdate = true;
            }
        }
    }
}