using Terraria;
using Terraria.ID;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class IceGolemRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // --- VARIABEL KONTROL PATTERN KUSTOM ---
        public int attackPhase = 0;       // 0 = Tunggu Cooldown, 1 = Barrage Laser, 2 = Semburan Blizzard
        public int globalTimer = 0;       // Pengukur waktu siklus utama
        public int laserCounter = 0;      // Menghitung jumlah laser (Maksimal 10)
        public int laserTimer = 0;        // Jeda antar tembakan laser individu
        public int sweepDirection = 1;    // Arah sapuan sudut laser

        public override bool PreAI(NPC npc)
        {
            // LOKASI ID TARGET: 243 (Ice Golem)
            if (npc.type != NPCID.IceGolem) return true;

            // Cari target player terdekat
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (!target.active || target.dead) return true;

            // --- TRIK RAHASIA: MEMBLOKIR TEMBAKAN ORIGINAL VANILLA ---
            // Di kode asli Terraria, npc.ai[1] mengontrol jeda tembakan laser Frost Beam miliknya.
            // Kita potong dan paksa nilainya tetap di bawah batas tembak agar laser aslinya mati total!
            if (npc.ai[1] > 10f)
            {
                npc.ai[1] = 0f;
            }

            // --- STRUKTUR MESIN PATTERN SKILL KUSTOM ---
            switch (attackPhase)
            {
                // [FASE 0]: TUNGGU COOLDOWN 3 DETIK (180 Frame) SEBELUM POLA DIMULAI
                case 0:
                    globalTimer++;
                    if (globalTimer >= 180) 
                    {
                        globalTimer = 0;
                        laserCounter = 0;
                        laserTimer = 0;
                        attackPhase = 1; // Masuk ke Barrage Laser

                        // Tentukan arah sapuan melengkung laser berdasarkan posisi tinggi player
                        sweepDirection = (target.Center.Y < npc.Center.Y) ? 1 : -1;
                    }
                    break;

                // [PATTERN 1]: BARRAGE 10 LASER FROST BEAM
                case 1:
                    laserTimer++;

                    // LOKASI JEDA ANTAR LASER: Keluar setiap 6 frame sekali (~10 laser dalam 1 detik)
                    if (laserTimer >= 6)
                    {
                        laserTimer = 0;

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            Vector2 baseDirection = target.Center - npc.Center;
                            baseDirection.Normalize();
                            float baseAngle = (float)Math.Atan2(baseDirection.Y, baseDirection.X);

                            // Logika sapuan sudut dari tembakan ke-1 sampai ke-10
                            float angleOffset = MathHelper.Lerp(-0.35f, 0.35f, (float)laserCounter / 9f) * sweepDirection;
                            float finalAngle = baseAngle + angleOffset;
                            
                            // LOKASI KECEPATAN LASER: Mengunci laju gerak Frost Beam di angka 11f
                            Vector2 shootVelocity = new Vector2((float)Math.Cos(finalAngle), (float)Math.Sin(finalAngle)) * 11f;

                            // Menembakkan Frost Beam (ID 257) dari area mata golem
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                npc.Center + new Vector2(0f, -16f), 
                                shootVelocity,
                                ProjectileID.FrostBeam, 
                                35, // DAMAGE LASER KUSTOM
                                2f,
                                Main.myPlayer
                            );
                        }

                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item33, npc.Center);
                        laserCounter++;

                        if (laserCounter >= 10)
                        {
                            globalTimer = 0;
                            attackPhase = 2; // Selesai 10 laser, langsung pindah ke Pattern 2
                        }
                    }
                    break;

                // [PATTERN 2]: BURST 3 BLIZZARD CLOUD (ATAS, BAWAH, BELAKANG)
                case 2:
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // Otomatis deteksi arah belakang punggung golem berdasarkan hadap wajahnya
                        float behindDirectionX = -npc.direction; 

                        // LOKASI KOORDINAT TITIK PANGGIL:
                        Vector2 spawnTop = npc.Center + new Vector2(0f, -45f);               // 1. Atas kepala
                        Vector2 spawnBottom = npc.Center + new Vector2(0f, npc.height / 2f);  // 2. Bawah kaki/tanah
                        Vector2 spawnBehind = npc.Center + new Vector2(behindDirectionX * 40f, 0f); // 3. Belakang punggung

                        Vector2[] spawnPositions = new Vector2[] { spawnTop, spawnBottom, spawnBehind };
                        float cloudSpeed = 4f; // Kecepatan dorong awal awan badai

                        foreach (Vector2 spawnPos in spawnPositions)
                        {
                            Vector2 shootVelocity = target.Center - spawnPos;
                            shootVelocity.Normalize();
                            shootVelocity *= cloudSpeed;

                            // Memanggil Blizzard Cloud murni partikel pelacak milikmu
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                spawnPos,
                                shootVelocity,
                                global::Terraria.ModLoader.ModContent.ProjectileType<BlizzardCloud>(), 
                                40, // DAMAGE AWAN BADAI
                                3f,
                                Main.myPlayer
                            );

                            // Partikel ledakan penanda spawn awan
                            for (int k = 0; k < 10; k++)
                            {
                                Dust d = Dust.NewDustDirect(spawnPos - new Vector2(10f, 10f), 20, 20, DustID.IceTorch, 0f, 0f, 100, default, 1.2f);
                                d.noGravity = true;
                                d.velocity *= 1.5f;
                            }
                        }
                    }

                    // Suara hembusan mantra Frost Staff dari 3 penjuru tubuh golem
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item28, npc.Center);

                    // Bersihkan timer dan paksa masuk fase cooldown selama 5 detik
                    globalTimer = 0;
                    attackPhase = 3; 
                    break;

                // [FASE 3]: JEDA COOLDOWN TOTAL SETELAH RANGKAIAN SERANGAN (5 Detik = 300 Frame)
                case 3:
                    globalTimer++;
                    if (globalTimer >= 300) 
                    {
                        globalTimer = 0;
                        attackPhase = 0; // Siklus serangan kembali berputar ke awal!
                    }
                    break;
            }

            // --- DATA GERAKAN VANILLA (CONTOH DARI ICE ELEMENTAL) ---
            // Mengembalikan true agar seluruh kode AI pergerakan asli bawaan Terraria 
            // (seperti jalan, melompat 1 block, gravitasi) tetap berjalan normal tanpa diganggu.
            return true; 
        }
    }
}