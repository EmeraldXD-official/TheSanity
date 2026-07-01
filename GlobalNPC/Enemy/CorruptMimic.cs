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
    // PART 1: REWORK AI CORRUPT MIMIC (CLINGER ATTACK & DART RIFLE COOLDOWN PHASE)
    // =========================================================================
    public class CorruptMimicRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer internal berbasis Integer
        public int customAttackTimer = 0;
        
        // Pengaman anti-double trigger serangan Clinger
        public bool hasSpawnedProjectiles = false;

        // Timer internal khusus untuk mengatur jeda tembakan Dart Rifle saat cooldown
        public int rifleShootTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.BigMimicCorruption;
        }

        public override void PostAI(NPC npc)
        {
            if (!npc.active) return;

            // Mengunci target ke player terdekat yang aktif
            Player targetPlayer = Main.player[npc.target];
            if (!targetPlayer.active || targetPlayer.dead) return;

            customAttackTimer++;

            // --- LOKASI BALANCING: TIMER SERANGAN UTAMA ---
            int attackCooldown = 1200; // Jeda waktu setiap 20 detik sekali (20 * 60)
            int freezeDuration = 180;  // Durasi Mimic terpaku diam selama 3 detik (3 * 60)

            // =========================================================================
            // FASE A: COOLDOWN PHASE - MENEMBAK DENGAN DART RIFLE (customAttackTimer < 1200)
            // =========================================================================
            if (customAttackTimer < attackCooldown)
            {
                rifleShootTimer++;

                // --- LOKASI BALANCING: ATTACK SPEED (JEDA TEMBAKAN) ---
                // 120 frame = Senjata menembak setiap 2 detik sekali (Sesuai permintaan: Lambat)
                int rifleFireRate = 120; 

                if (rifleShootTimer % rifleFireRate == 0)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // 1. Spawn Turret Visual Dart Rifle tepat di tengah tubuh Mimic
                        // UPDATE: Kita mengirim npc.whoAmI ke parameter ai[1] (argumen terakhir sebelum Main.myPlayer)
                        // agar proyektil senapan tahu entitas mana yang harus ditempeli setiap frame.
                        int rifleProj = Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            Vector2.Zero,
                            ModContent.ProjectileType<DartRifleTurretVisual>(),
                            0, // 0 damage karena hanya unit visual senjata
                            0f,
                            Main.myPlayer,
                            0f,
                            npc.whoAmI // <--- Disuntikkan ke parameter ai[1] untuk melacak posisi Mimic
                        );

                        // Batasi umur visual senjatanya sebentar saja agar langsung hilang setelah menembak
                        if (rifleProj != Main.maxProjectiles)
                        {
                            Main.projectile[rifleProj].timeLeft = 40; 
                        }

                        // 2. Kalkulasi Vektor Arah Tembakan Peluru menuju Player
                        Vector2 shootDirection = targetPlayer.Center - npc.Center;
                        shootDirection.Normalize(); // Normalisasi vektor agar panjangnya 1

                        // --- LOKASI BALANCING: VELOCITY & SPEED PELURU ---
                        // Kecepatan lesatan peluru diset ke 22f (Sesuai permintaan: Sangat Cepat!)
                        float dartSpeed = 22f; 
                        Vector2 dartVelocity = shootDirection * dartSpeed;

                        // Efek suara tembakan Dart Rifle vanilla
                        SoundEngine.PlaySound(SoundID.Item99, npc.Center); 

                        // 3. Spawn Peluru Cursed Dart Kustom yang memberikan damage & debuff
                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            dartVelocity,
                            ModContent.ProjectileType<CursedDartVisualRework>(),
                            32, // Base Damage peluru kustom
                            4f,
                            Main.myPlayer
                        );
                    }
                }
            }
            // =========================================================================
            // FASE B: SIAP-SIAP & EKSEKUSI SERANGAN PILAR CLINGER
            // =========================================================================
            else if (customAttackTimer >= attackCooldown && customAttackTimer <= attackCooldown + freezeDuration)
            {
                // Mengurangi kecepatan horizontal Mimic secara alami agar AI Dart vanilla tidak rusak
                npc.velocity.X *= 0.5f; 

                // Efek visual partikel aura hisap terkutuk
                if (Main.rand.NextBool(3))
                {
                    Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(60f, 60f), DustID.CursedTorch, Vector2.Zero, 100, default, 1.4f);
                    d.noGravity = true;
                    d.velocity = (npc.Center - d.position) * 0.12f;
                }

                // EKSEKUSI SPAWN: ANTI-DOUBLE ATTACK
                if (!hasSpawnedProjectiles)
                {
                    hasSpawnedProjectiles = true; // Kunci langsung
                    
                    SoundEngine.PlaySound(SoundID.Zombie73, npc.Center); // Suara raungan Corrupt Mimic

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // Titik acuan horizontal dari Mimic, vertikal murni mengikuti ketinggian PLAYER
                        float targetYPosition = targetPlayer.Center.Y;
                        
                        // Jangkauan radius scan sejauh 100 blok ke kanan dan ke kiri dari posisi Mimic
                        int startX = (int)(npc.Center.X / 16) - 100;
                        int endX = (int)(npc.Center.X / 16) + 100;

                        // --- LOKASI BALANCING: JARAK ANTAR PROYEKTIL ---
                        // Melompati 20 blok ke samping tanpa mengecek kondisi solid/tidaknya block
                        for (int x = startX; x <= endX; x += 20)
                        {
                            // Pengaman koordinat batas dunia luar Terraria
                            if (x < 5 || x >= Main.maxTilesX - 5) continue;

                            // Mengonversi koordinat grid X kembali menjadi koordinat dunia game (World Position)
                            Vector2 spawnPos = new Vector2(x * 16 + 8, targetYPosition);

                            // --- LOKASI BALANCING: DAMAGE PROYEKTIL KUSTOM ---
                            int customProjDamage = 65; 

                            // Diam di tempat (Vector2.Zero) sejak awal spawn
                            Vector2 launchVelocity = Vector2.Zero;

                            // Memanggil proyektil rintangan kustom (ClingerStaffVisualRework)
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                spawnPos,
                                launchVelocity,
                                ModContent.ProjectileType<ClingerStaffVisualRework>(),
                                customProjDamage,
                                3f,
                                Main.myPlayer
                            );
                        }
                    }
                }
            }

            // FASE C: RESET TIMERS & FLAGS
            if (customAttackTimer >= attackCooldown + freezeDuration)
            {
                customAttackTimer = 0;
                rifleShootTimer = 0; // Reset timer senapan untuk siklus berikutnya
                hasSpawnedProjectiles = false; // Buka kunci pengaman pilar Clinger selesai
            }
        }
    }

    // =========================================================================
    // PART 2: MODIFIKASI PROJECTILE VANILLA
    // =========================================================================
    public class CorruptMimicProjectileMod : GlobalProjectile
    {
        public override void OnSpawn(Projectile projectile, Terraria.DataStructures.IEntitySource source)
        {
            if (projectile.type == ProjectileID.CursedDartFlame)
            {
                projectile.friendly = false;
                projectile.hostile = true;
                projectile.damage = 55; 
            }
        }

        public override bool? CanHitNPC(Projectile projectile, NPC target)
        {
            bool isHomingOrMinion = projectile.minion || 
                                    ProjectileID.Sets.MinionShot[projectile.type] || 
                                    projectile.aiStyle == 9 || 
                                    projectile.type == ProjectileID.ChlorophyteBullet;

            if (isHomingOrMinion && target.type == NPCID.BigMimicCorruption)
            {
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC otherNPC = Main.npc[i];
                    if (otherNPC.active && !otherNPC.friendly && otherNPC.whoAmI != target.whoAmI && otherNPC.chaseable)
                    {
                        return false; 
                    }
                }
            }

            return null;
        }
    }
}