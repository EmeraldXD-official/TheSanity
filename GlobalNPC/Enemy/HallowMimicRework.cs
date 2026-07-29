using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles; // Menghubungkan namespace proyektil kustom kita

namespace TheSanity.NPCs
{
    public class HallowMimicRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer utama untuk siklus Stormbow dan Grenade
        public int hallowAttackTimer = 0;

        // Timer internal khusus untuk mengatur jeda lemparan granat kristal
        public int grenadeShootTimer = 0;

        // Penghitung jumlah lemparan granat untuk memicu lemparan ke-3 (Triple Throw)
        public int grenadeThrowCount = 0;

        // Timer internal untuk mengatur jeda kemunculan Flying Knife Zone baru
        public int knifeSpawnTimer = 0;

        // FIX ANTI-SPAM: Saklar pengunci fase serangan agar tidak dirusak oleh AI bawaan vanilla
        // 0 = Daedalus Stormbow Phase | 1 = Crystal Grenade Phase (Cooldown)
        public int attackState = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            // AI ini hanya disuntikkan secara khusus ke Hallow Mimic raksasa vanilla
            return entity.type == NPCID.BigMimicHallow;
        }

        public override void PostAI(NPC npc)
        {
            if (!npc.active) return;

            // Mengunci target ke Player terdekat yang aktif
            Player targetPlayer = Main.player[npc.target];
            if (!targetPlayer.active || targetPlayer.dead) return;

            // Timer global independen untuk siklus serangan
            hallowAttackTimer++;

            // =========================================================================
            // INDEPENDENT ATTACK: FLYING KNIFE ZONE (MENIMPA ATTACK LAIN)
            // =========================================================================
            knifeSpawnTimer++;
            
            // --- LOKASI BALANCING: JEDA SPAWN FLYING KNIFE ---
            int knifeSpawnDelay = 720; // 12 detik

            if (knifeSpawnTimer >= knifeSpawnDelay)
            {
                knifeSpawnTimer = 0;

                // Memeriksa apakah sudah ada FlyingKnifeZone aktif di arena
                bool knifeAlreadyExists = false;
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile proj = Main.projectile[i];
                    if (proj.active && proj.type == ModContent.ProjectileType<FlyingKnifeZone>() && proj.owner == Main.myPlayer)
                    {
                        knifeAlreadyExists = true;
                        break;
                    }
                }

                // REKUES KHUSUS: Hanya boleh ada 1 Flying Knife aktif per Mimic
                if (!knifeAlreadyExists)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // Memanggil zona pisau terbang tepat di koordinat posisi Player saat itu
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            targetPlayer.Center,
                            Vector2.Zero,
                            ModContent.ProjectileType<FlyingKnifeZone>(),
                            28, // Base Damage sabetan pisau kustom
                            3f,
                            Main.myPlayer
                        );
                    }
                }
            }

            // =========================================================================
            // CORE PATTERN FASE 1: DAEDALUS STORMBOW ATTACK (STATE 0)
            // =========================================================================
            if (attackState == 0)
            {
                // FIX BUG 1 & 2: Tambahkan pengecekan ketat agar tidak terjadi penumpukan/spam proyektil visual busur
                bool bowAlreadyExists = false;
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile proj = Main.projectile[i];
                    // Jika ditemukan DaedalusBowVisual yang aktif dan itu milik Mimic ini (melalui kecocokan ai[1])
                    if (proj.active && proj.type == ModContent.ProjectileType<DaedalusBowVisual>() && proj.ai[1] == npc.whoAmI)
                    {
                        bowAlreadyExists = true;
                        break;
                    }
                }

                // Menembakkan/Memunculkan Busur Daedalus hanya jika belum ada busur aktif di tubuh Mimic ini
                if (!bowAlreadyExists && (hallowAttackTimer == 10 || hallowAttackTimer % 210 == 0))
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // FIX BUG 2: Mengubah Vector2.Zero menjadi new Vector2(0f, -1f) agar internal engine 
                        // Terraria mendeteksi arah laju ke atas dan otomatis merender sprite menghadap ke ATAS/LANGIT.
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            new Vector2(0f, -1f), 
                            ModContent.ProjectileType<DaedalusBowVisual>(),
                            0, // Visual semata
                            0f,
                            Main.myPlayer,
                            0f, // ai[0]
                            npc.whoAmI // <--- SUNTIKKAN INI agar menempel!
                        );
                    }
                }

                // --- LOKASI BALANCING: DURASI FASE STORMBOW (600 Frame = 10 Detik) ---
                if (hallowAttackTimer >= 600)
                {
                    // Hancurkan visual busur Daedalus saat fase berakhir agar bersih total sebelum masuk fase granat
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        Projectile proj = Main.projectile[i];
                        if (proj.active && proj.type == ModContent.ProjectileType<DaedalusBowVisual>() && proj.ai[1] == npc.whoAmI)
                        {
                            proj.Kill();
                        }
                    }

                    hallowAttackTimer = 0; // Reset timer untuk digunakan fase berikutnya
                    attackState = 1;       // FIX ANTI-SPAM: Pindah paksa ke fase Granat (Cooldown)
                }
            }

            // =========================================================================
            // CORE PATTERN FASE 2: CRYSTAL SHARD GRENADE REPLACE (STATE 1 / COOLDOWN 15 DETIK)
            // =========================================================================
            else if (attackState == 1)
            {
                grenadeShootTimer++;

                // --- LOKASI BALANCING: JEDA LEMPARAN GRANAT ---
                int throwDelay = 60; // 1 detik jeda

                if (grenadeShootTimer >= throwDelay)
                {
                    grenadeShootTimer = 0; 
                    grenadeThrowCount++;   

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // --- FIX: LEMPARAN GRANAT MELEBAR KE SELURUH ARENA ---
                        Vector2 throwDirection = targetPlayer.Center - npc.Center;
                        
                        // Menghitung jarak horizontal (X) antara Mimic and Player
                        float distanceX = Math.Abs(throwDirection.X);
                        
                        // Memberikan dorongan Y negatif ke atas yang LEBIH BESAR agar lemparannya lebih melebar/jauh parabola
                        throwDirection.Y -= distanceX * 0.25f; 
                        throwDirection.Normalize();

                        // --- LOKASI BALANCING: KECEPAN GRANAT KRISTAL DINAMIS ---
                        // Clamp agar kecepatan tidak terlalu pelan saat dekat atau terlalu cepat saat jauh
                        float throwSpeed = MathHelper.Clamp(distanceX * 0.05f, 10f, 22f); 

                        // --- LOKASI BALANCING: BASE DAMAGE GRANAT KRISTAL ---
                        int grenadeDamage = 35;

                        // REKUES KHUSUS: Setiap lemparan ke-3, pemicu TRIPLE THROW (Mengeluarkan 3 proyektil sekaligus)
                        if (grenadeThrowCount >= 3)
                        {
                            grenadeThrowCount = 0; // Reset hitungan kembali ke 0

                            // Mainkan suara lemparan ganda yang keras
                            SoundEngine.PlaySound(SoundID.Item61, npc.Center);

                            // Membagi sudut sebaran 3 arah sebesar 18 derajat
                            float spreadAngle = MathHelper.ToRadians(18f);

                            for (int i = -1; i <= 1; i++)
                            {
                                Vector2 perturbedSpeed = throwDirection.RotatedBy(spreadAngle * i) * throwSpeed;

                                Projectile.NewProjectile(
                                    npc.GetSource_FromAI(),
                                    npc.Center,
                                    perturbedSpeed,
                                    ModContent.ProjectileType<CrystalShardGrenade>(),
                                    grenadeDamage,
                                    4f,
                                    Main.myPlayer
                                );
                            }
                        }
                        else
                        {
                            // Lemparan Normal Tunggal (Untuk Lemparan ke-1 dan ke-2)
                            SoundEngine.PlaySound(SoundID.Item19, npc.Center); // Suara ayunan/lemparan biasa

                            Vector2 finalSpeed = throwDirection * throwSpeed;

                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                npc.Center,
                                finalSpeed,
                                ModContent.ProjectileType<CrystalShardGrenade>(),
                                grenadeDamage,
                                4f,
                                Main.myPlayer
                            );
                        }
                    }
                }

                // --- LOKASI BALANCING: DURASI COOLDOWN / FASE GRANAT (900 Frame = 15 Detik) ---
                if (hallowAttackTimer >= 900)
                {
                    // Otomatis menghentikan pola granat kristal dan mereset siklus kembali ke Stormbow
                    hallowAttackTimer = 0;
                    grenadeShootTimer = 0;
                    grenadeThrowCount = 0;
                    attackState = 0; // Kembalikan ke Fase Stormbow
                }
            }
        }
    }
}