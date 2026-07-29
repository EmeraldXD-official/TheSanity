using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.NPCs
{
    // =========================================================================
    // 1. REWORK AI DIABOLIST (TEMBAKAN POLA W)
    // =========================================================================
    public class DiabolistRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Variabel kustom mandiri untuk melacak cooldown skill 15 detik
        private int skillTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.DiabolistRed || entity.type == NPCID.DiabolistWhite;
        }

        public override void AI(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // Targetkan player terdekat
            npc.TargetClosest(true);
            Player player = Main.player[npc.target];
            if (!player.active || player.dead) return;

            // Timer cooldown kustom (900 frame = 15 detik)
            skillTimer++;

            if (skillTimer >= 900)
            {
                skillTimer = 0; // Reset timer kustom

                // =========================================================================
                // LOKASI BALANCING DAMAGE, KECEPATAN, DAN SUDUT SEBARAN POLA W
                // =========================================================================
                float projectileSpeed = 6f;   // Kecepatan laju bola api Inferno
                int projectileDamage = 55;    // Damage hantaman bola api Inferno
                float spreadAngle = 0.26f;    // Jarak renggang antar peluru (~15 derajat) untuk membentuk pola W

                // Hitung arah dasar lurus dari Diabolist ke koordinat tengah Player
                Vector2 baseVelocity = (player.Center - npc.Center).SafeNormalize(Vector2.UnitY) * projectileSpeed;

                // Tentukan 3 arah velositas untuk membentuk pola huruf W:
                // 1. Peluru Tengah (Lurus ke player)
                // 2. Peluru Kiri (Dirotasi ke kiri)
                // 3. Peluru Kanan (Dirotasi ke kanan)
                Vector2[] shootVelocities = new Vector2[3]
                {
                    baseVelocity,
                    baseVelocity.RotatedBy(-spreadAngle),
                    baseVelocity.RotatedBy(spreadAngle)
                };

                // Tembakkan 3 proyektil sekaligus dalam 1 frame
                for (int i = 0; i < 3; i++)
                {
                    int proj = Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        shootVelocities[i],
                        ProjectileID.InfernoHostileBolt,
                        projectileDamage,
                        4f,
                        Main.myPlayer
                    );

                    if (proj != Main.maxProjectiles)
                    {
                        Main.projectile[proj].hostile = true;
                        Main.projectile[proj].friendly = false;
                    }
                }

                // Efek visual dust kobaran api Inferno pekat di tubuh caster saat menembak
                for (int d = 0; d < 20; d++)
                {
                    int dust = Dust.NewDust(npc.position, npc.width, npc.height, DustID.InfernoFork, 0f, 0f, 100, default(Color), 2f);
                    Main.dust[dust].noGravity = true;
                    Main.dust[dust].velocity = Main.rand.NextVector2Circular(6f, 6f);
                }

                // Efek suara ledakan sihir api khas Inferno Fork
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item20, npc.Center);
            }
        }

        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.DiabolistRed || npc.type == NPCID.DiabolistWhite)
            {
                // Tempat mengubah stat dasar (silakan diaktifkan jika perlu)
                // npc.damage = 0;
                // npc.lifeMax = 700;
            }
        }
    }

    // =========================================================================
    // 2. DETEKSI PROYEKTIL INFERNO (JARAK 2 BLOK LANGSUNG MELEDAK)
    // =========================================================================
    public class InfernoBoltProxyExplosion : GlobalProjectile
    {
        public override void PostAI(Projectile projectile)
        {
            // Pastikan kode hanya mendeteksi proyektil InfernoHostileBolt milik musuh
            if (projectile.type == ProjectileID.InfernoHostileBolt && projectile.hostile)
            {
                // Cari seluruh player aktif di dalam map game
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player targetPlayer = Main.player[i];

                    if (targetPlayer.active && !targetPlayer.dead)
                    {
                        // --- LOKASI BALANCING JARAK DETEKSI PROXIMITY ---
                        // Terraria mengukur jarak dengan satuan Pixel. 1 Blok = 16 Pixel.
                        // Maka, 2 Blok = 32 Pixel.
                        float detectionRadius = 32f; 

                        // Hitung jarak asli antara koordinat pusat proyektil dengan pusat tubuh Player
                        float currentDistance = Vector2.Distance(projectile.Center, targetPlayer.Center);

                        // Jika jaraknya 2 blok atau lebih dekat, picu ledakan instan!
                        if (currentDistance <= detectionRadius)
                        {
                            // 1. Hancurkan proyektil bolt agar tidak berjalan terus
                            projectile.active = false;

                            if (Main.netMode != NetmodeID.MultiplayerClient)
                            {
                                // 2. Munculkan InfernoHostileBlast (Ledakan api melingkar besar) tepat di posisi bolt saat itu
                                int blast = Projectile.NewProjectile(
                                    projectile.GetSource_FromThis(),
                                    projectile.Center,
                                    Vector2.Zero, // Ledakan diam di tempat
                                    ProjectileID.InfernoHostileBlast,
                                    projectile.damage,
                                    projectile.knockBack,
                                    Main.myPlayer
                                );

                                if (blast != Main.maxProjectiles)
                                {
                                    Main.projectile[blast].hostile = true;
                                    Main.projectile[blast].friendly = false;
                                }
                            }

                            // Kirim update paket data sinkronisasi jika bermain di mode Multiplayer server
                            projectile.netUpdate = true;
                            break; 
                        }
                    }
                }
            }
        }
    }
}