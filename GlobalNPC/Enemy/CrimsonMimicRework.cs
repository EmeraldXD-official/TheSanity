using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles; // Memastikan namespace proyektil kustom terhubung

namespace TheSanity.NPCs
{
    // =========================================================================
    // REWORK AI CRIMSON MIMIC (LIFE DRAIN FIELD & TRIPLE DART PISTOL BURST)
    // =========================================================================
    public class CrimsonMimicRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer utama berbasis integer untuk mengatur urutan fase
        public int crimsonAttackTimer = 0;

        // Timer internal untuk mengatur jeda tembakan beruntun Dart Pistol
        public int pistolShootTimer = 0;

        // Pengaman anti-double spawn untuk aura Life Drain
        public bool hasSpawnedLifeDrain = false;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            // AI ini hanya akan disuntikkan secara khusus ke Crimson Mimic raksasa vanilla
            return entity.type == NPCID.BigMimicCrimson;
        }

        public override void PostAI(NPC npc)
        {
            if (!npc.active) return;

            // Mengunci target ke Player terdekat yang aktif
            Player targetPlayer = Main.player[npc.target];
            if (!targetPlayer.active || targetPlayer.dead) return;

            crimsonAttackTimer++;

            // =========================================================================
            // FASE 1: INITIAL COOLDOWN (0 - 3 DETIK PERTAMA)
            // =========================================================================
            // 180 frame = 3 detik awal spawn / awal siklus
            if (crimsonAttackTimer <= 180)
            {
                // Membuat Crimson Mimic diam di tempat secara horizontal agar bersiap
                npc.velocity.X *= 0.4f;

                // --- BEAUTIFIER: PARTIKEL PENGUMPUL ENERGI ---
                if (Main.rand.NextBool(3))
                {
                    Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(50f, 50f), DustID.Blood, Vector2.Zero, 100, default, 1.3f);
                    d.noGravity = true;
                    d.velocity = (npc.Center - d.position) * 0.1f; // Efek tersedot masuk ke tubuh Mimic
                }
            }

            // =========================================================================
            // FASE 2: SPAWN & PENGAKTIFAN KABUT LIFE DRAIN (DETIK KE-3 / FRAME 181)
            // =========================================================================
            // --- LOKASI BALANCING: TIMING LIFE DRAIN ---
            // Ditrigger tepat setelah cooldown 3 detik selesai
            if (crimsonAttackTimer > 180 && crimsonAttackTimer < 900) 
            {
                if (!hasSpawnedLifeDrain)
                {
                    hasSpawnedLifeDrain = true; // Kunci pengaman agar hanya memanggil 1 proyektil aura saja

                    // Mainkan suara raungan kutukan Crimson
                    SoundEngine.PlaySound(SoundID.NPCDeath10, npc.Center);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // Memanggil proyektil aura pedang darah kustom
                        int drainProj = Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            Vector2.Zero,
                            ModContent.ProjectileType<LifeDrainVisualRework>(),
                            0, // Damage berkala diatur mandiri oleh internal script LifeDrainVisualRework per detik
                            0f,
                            Main.myPlayer,
                            0f,
                            npc.whoAmI // <--- Menyuntikkan ID Mimic ini ke ai[1] agar bisa menerima asupan HEALING!
                        );

                        // Override durasi hidup proyektil Life Drain agar sinkron dengan durasi fase di Boss ini (12 Detik sisa fase)
                        if (drainProj != Main.maxProjectiles)
                        {
                            Main.projectile[drainProj].timeLeft = 720; // 900 - 180 = 720 frame
                        }
                    }
                }
            }

            // =========================================================================
            // FASE 3: DART PISTOL VISUAL & TRIPLE ICHOR DART ATTACK (DETIK 15+ / FRAME 900+)
            // =========================================================================
            // 900 frame = Tepat saat timer mencapai durasi 15 detik (3 detik cooldown + 12 detik Life Drain aktif)
            // Fase ini akan berlangsung selama 5 detik ke depan (sampai frame 1200)
            if (crimsonAttackTimer >= 900 && crimsonAttackTimer < 1200)
            {
                pistolShootTimer++;

                // --- LOKASI BALANCING: ATTACK SPEED (RATE TEMBAKAN PISTOL) ---
                // Diset ke 45 frame = Menembak setiap 0.75 detik sekali (Lebih cepat dibanding Rifle yang 120 frame!)
                int pistolFireRate = 45;

                if (pistolShootTimer % pistolFireRate == 0)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // 1. Spawn Senjata Visual Dart Pistol Menempel di Tubuh Mimic
                        int pistolProj = Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            Vector2.Zero,
                            ModContent.ProjectileType<DartPistolTurretVisual>(),
                            0, // Visual semata
                            0f,
                            Main.myPlayer,
                            0f,
                            npc.whoAmI // <--- Mengunci posisi pistol mengikuti koordinat Mimic real-time
                        );

                        if (pistolProj != Main.maxProjectiles)
                        {
                            Main.projectile[pistolProj].timeLeft = 25; // Durasi tampil pistol sebentar saja tiap letusan
                        }

                        // 2. Kalkulasi Vektor Utama Menuju Arah Player
                        Vector2 baseDirection = targetPlayer.Center - npc.Center;
                        baseDirection.Normalize();

                        // --- LOKASI BALANCING: VELOCITY / KECEPATAN PELURU ICHOR ---
                        float ichorDartSpeed = 15f; 

                        // --- LOKASI BALANCING: BASE DAMAGE PELURU ICHOR ---
                        int ichorDartDamage = 25;

                        // Efek suara letusan Dart Pistol vanilla
                        SoundEngine.PlaySound(SoundID.Item98, npc.Center);

                        // 3. MEKANIK TRIPLE TEMBAKAN (3 DART SEKALIGUS SECARA MENYEBAR)
                        // Kita menggunakan perulangan untuk membelokkan sudut peluru tengah, kiri, dan kanan sebesar 15 derajat
                        float spreadAngle = MathHelper.ToRadians(15f); // Jarak sudut penyebaran antar peluru

                        for (int i = -1; i <= 1; i++)
                        {
                            // Memutar arah base vector sesuai dengan index urutan (-1 = Kiri, 0 = Tengah, 1 = Kanan)
                            Vector2 perturbedSpeed = baseDirection.RotatedBy(spreadAngle * i) * ichorDartSpeed;

                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                npc.Center,
                                perturbedSpeed,
                                ModContent.ProjectileType<IchorDartVisualRework>(),
                                ichorDartDamage,
                                3f, // Knockback peluru
                                Main.myPlayer
                            );
                        }
                    }
                }
            }

            // =========================================================================
            // FASE 4: SIKLUS SELESAI / RESET TIMERS
            // =========================================================================
            // Setelah total timer mencapai 20 detik (1200 frame), siklus direset kembali ke awal
            if (crimsonAttackTimer >= 1200)
            {
                crimsonAttackTimer = 0;
                pistolShootTimer = 0;
                hasSpawnedLifeDrain = false; // Membuka segel agar kabut Life Drain bisa di-spawn lagi di siklus berikutnya
            }
        }
    }
}