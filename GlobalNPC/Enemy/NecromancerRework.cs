using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.NPCs
{
    public class NecromancerRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // =========================================================================
        // VARIABEL KUSTOM MANDIRI (MURNI UNTUK SKILL BARRAGE)
        // =========================================================================
        private int skillTimer = 0;       // Timer utama 15 detik
        private bool isBarraging = false;  // Apakah sedang dalam mode memberondong?
        private int barrageCount = 0;     // Menghitung jumlah laser yang sudah keluar (maks 10)
        private int barrageTimer = 0;     // Jeda waktu antar peluru laser

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Necromancer || entity.type == NPCID.NecromancerArmored;
        }

        public override void AI(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // Targetkan player terdekat
            npc.TargetClosest(true);
            Player player = Main.player[npc.target];
            if (!player.active || player.dead) return;

            // 1. PENGHITUNG TIMER UTAMA (15 Detik Cooldown)
            if (!isBarraging)
            {
                skillTimer++;
                if (skillTimer >= 900) // 900 Frame = 15 Detik
                {
                    skillTimer = 0;
                    isBarraging = true; // Aktifkan mode memberondong!
                    barrageCount = 0;   // Reset hitungan peluru
                    barrageTimer = 0;   // Reset jeda peluru
                }
            }
            // 2. LOGIKA MEMBERONDONG SATU PER SATU (BARRAGE MODE)
            else
            {
                barrageTimer++;

                // --- LOKASI BALANCING JEDA ANTAR PELURU ---
                // Angka 5 berarti setiap 5 frame sekali, 1 peluru laser akan ditembakkan.
                // Semakin kecil angkanya, berondongannya semakin cepat!
                if (barrageTimer >= 5) 
                {
                    barrageTimer = 0;

                    // =========================================================================
                    // LOKASI BALANCING DAMAGE DAN KECEPATAN LAJU SHADOW BEAM
                    // =========================================================================
                    int totalProjectiles = 10;   // Target total tembakan
                    float projectileSpeed = 6f;  // Kecepatan laju laser
                    int projectileDamage = 50;   // Damage laser per hantaman

                    // Deteksi arah player saat ini (1 = Kanan, -1 = Kiri)
                    float directionX = (player.Center.X > npc.Center.X) ? 1f : -1f;

                    // Tentukan sudut awal (Pojok Bawah) dan sudut akhir (Pojok Atas)
                    float startAngle = (directionX == 1f) ? MathHelper.PiOver4 : MathHelper.Pi - MathHelper.PiOver4;
                    float endAngle = (directionX == 1f) ? -MathHelper.PiOver4 : MathHelper.Pi + MathHelper.PiOver4;

                    // Hitung rentang sapuan sudut berdasarkan urutan peluru ke-berapa yang sedang keluar
                    float angleRange = endAngle - startAngle;
                    float angleStep = angleRange / (totalProjectiles - 1);

                    // Kalkulasi sudut spesifik untuk peluru saat ini (Menyapu dari bawah ke atas secara bertahap)
                    float currentAngle = startAngle + (angleStep * barrageCount);
                    Vector2 shootVelocity = currentAngle.ToRotationVector2() * projectileSpeed;

                    // Tembakkan 1 proyektil laser Shadow Beam
                    int proj = Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        shootVelocity,
                        ProjectileID.ShadowBeamHostile,
                        projectileDamage,
                        2f,
                        Main.myPlayer
                    );

                    if (proj != Main.maxProjectiles)
                    {
                        Main.projectile[proj].hostile = true;
                        Main.projectile[proj].friendly = false;
                    }

                    // Efek visual dust dan suara per satu tembakan peluru biar kerasa efek berondongannya
                    int dust = Dust.NewDust(npc.position, npc.width, npc.height, DustID.ShadowbeamStaff, 0f, 0f, 100, default(Color), 1.5f);
                    Main.dust[dust].noGravity = true;
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item8, npc.Center);

                    // Tambah hitungan peluru yang sudah ditembakkan
                    barrageCount++;

                    // Jika sudah menembakkan total 10 peluru, matikan mode barrage dan kembali ke cooldown 15 detik
                    if (barrageCount >= totalProjectiles)
                    {
                        isBarraging = false;
                    }
                }
            }
        }

        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.Necromancer || npc.type == NPCID.NecromancerArmored)
            {
                // Referensi balancing stat dasar (silakan diaktifkan jika perlu)
                // npc.damage = 0;
                // npc.lifeMax = 600;
            }
        }
    }
}