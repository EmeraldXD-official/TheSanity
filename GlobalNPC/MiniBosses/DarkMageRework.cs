using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [BOSS REWORK SYSTEM]: DARK MAGE DYNAMIC SPAM & DISTANCE-BASED VELOCITY
    // =========================================================================
    public class DarkMageRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // TIMER KUSTOM MANDIRI (Aman dari bentrokan AI Vanilla)
        public int summonSignTimer = 0;
        public int magicBookTimer = 0;

        // Memastikan efek menempel pada Dark Mage Tier 1 dan Tier 3
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation) {
            return entity.type == NPCID.DD2DarkMageT1 || entity.type == NPCID.DD2DarkMageT3;
        }

        public override void AI(NPC npc) {
            if (Main.gameMenu || Main.dedServ) return;

            // Selalu paksa target mencari player terdekat agar AI tidak linglung
            npc.TargetClosest(true);
            
            // -------------------------------------------------------------------------
            // LOGIKA PENCARIAN TARGET TERDEKAT DARI ETERNIA CRYSTAL
            // -------------------------------------------------------------------------
            Player finalTarget = Main.player[npc.target]; // Target default jika kristal tidak ada
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC crystal = Main.npc[i];
                if (crystal.active && crystal.type == NPCID.DD2EterniaCrystal) {
                    int closestPlayerIndex = Player.FindClosest(crystal.Center, 1, 1);
                    if (closestPlayerIndex != -1) {
                        finalTarget = Main.player[closestPlayerIndex];
                    }
                    break; 
                }
            }

            if (finalTarget == null || !finalTarget.active || finalTarget.dead) return;

            // -------------------------------------------------------------------------
            // 1. SETTING SEMI-TRANSPARANT & GLOW VIA PROPERTIES VANILLA
            // -------------------------------------------------------------------------
            // Memaksa opacity NPC menjadi semi-transparan (Nilai alpha 125 dari 255 = ~50%)
            // Ini membuat 5 sheet sprite glow orisinalnya ter-render sempurna tanpa merusak animasi
            npc.alpha = 125; 

            // -------------------------------------------------------------------------
            // PENENTUAN BASE DAMAGE DINAMIS BERDASARKAN TIER (BALANCING AREA)
            // -------------------------------------------------------------------------
            int currentSignBoltDamage = 15; // Maksimal 15 untuk Tier 1 (Pre-Hardmode)
            int currentBookDamage = 15;

            if (npc.type == NPCID.DD2DarkMageT3) {
                currentSignBoltDamage = 35; // Set ke 35 untuk Tier 3 (Hardmode Akhir / Post-Golem)
                currentBookDamage = 35;
            }

            // -------------------------------------------------------------------------
            // MEKANIK 1: SPAM DARK MAGE SUMMON SIGN (ACAK 3 - 5 BUAH)
            // -------------------------------------------------------------------------
            summonSignTimer++;
            // LOKASI SPEED BALANCING (Jeda Pemanggilan Simbol): Diperlama jadi 10 Detik (600 Frame) agar tidak menumpuk
            if (summonSignTimer >= 600) {
                summonSignTimer = 0;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    int totalSummons = Main.rand.Next(3, 6); 

                    for (int i = 0; i < totalSummons; i++) {
                        Vector2 randomOffset = new Vector2(Main.rand.Next(-300, 301), Main.rand.Next(-200, 201));
                        Vector2 spawnPos = finalTarget.Center + randomOffset;

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            spawnPos,
                            Vector2.Zero,
                            ModContent.ProjectileType<DarkMageSummonSign>(),
                            currentSignBoltDamage, 
                            0f,
                            Main.myPlayer
                        );
                    }
                }
                SoundEngine.PlaySound(SoundID.Zombie105, npc.Center);
            }

            // -------------------------------------------------------------------------
            // MEKANIK 2: MELEMPAR ANCIENT MAGIC BOOK (LEBIH SPAMMIK & DINAMIS JARAK)
            // -------------------------------------------------------------------------
            magicBookTimer++;
            // LOKASI SPEED BALANCING: Dipangkas drastis menjadi 3.5 Detik Sekali (210 Frame) agar sangat SPAMMIK!
            if (magicBookTimer >= 210) {
                magicBookTimer = 0;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    // Hitung arah dasar ke koordinat player
                    Vector2 shootVelocity = finalTarget.Center - npc.Center;
                    
                    // Hitung jarak murni antara pusat tubuh Dark Mage dan Player
                    float distance = Vector2.Distance(npc.Center, finalTarget.Center);

                    // -------------------------------------------------------------------------
                    // LOGIKA KECEPATAN BERDASARKAN JARAK (DISTANCE-BASED VELOCITY)
                    // -------------------------------------------------------------------------
                    // LOKASI BALANCING DISTANCE: Jarak dibagi nilai pembagi kustom (Default: 50f)
                    // Ditambahkan Clamp agar kecepatan minimal berada di 6f dan maksimal mentok di 22f (sangat cepat)
                    float launchSpeed = distance / 50f;
                    launchSpeed = MathHelper.Clamp(launchSpeed, 6f, 22f); 

                    // Terapkan kecepatan dinamis ke arah target
                    shootVelocity.Normalize();
                    shootVelocity *= launchSpeed; 

                    int randomBookType = Main.rand.Next(3);

                    // Spawn proyektil kustom buku sihir
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        shootVelocity,
                        ModContent.ProjectileType<AncientMagicBook>(),
                        currentBookDamage, 
                        3f,
                        Main.myPlayer,
                        0f,
                        randomBookType
                    );
                }
                SoundEngine.PlaySound(SoundID.Item28, npc.Center);
            }
        }

        // PreDraw dihapus sepenuhnya agar engine drawing vanilla otomatis mengurus 5 sheet sprite glow bawaannya secara aman dan rapi!
    }
}