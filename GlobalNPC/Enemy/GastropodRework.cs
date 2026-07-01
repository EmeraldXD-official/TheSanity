using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles; // Memanggil folder proyektil agar bisa membaca RainbowRocket

namespace TheSanity.NPCs
{
    public class GastropodRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Gastropod;
        }

        // =========================================================================
        // FIX UTAMA: MENGGUNAKAN VARIABEL MANDIRI (ANTI-BENTROK DENGAN VANILLA GAME)
        // =========================================================================
        private float skillCooldown = 180f; // Jeda awal saat pertama kali spawn (3 detik biar tidak langsung nembak)
        private int skillState = 0;         // Status skill (0 = Terbang Normal, 1 = Diam & Tembak Roket)
        private int fireDelayTimer = 0;     // Jeda antar peluru saat merapid
        private int rocketCount = 0;        // Penghitung jumlah roket yang keluar

        public override bool PreAI(NPC npc)
        {
            npc.TargetClosest(true);
            Player targetPlayer = Main.player[npc.target];

            if (targetPlayer == null || !targetPlayer.active || targetPlayer.dead)
            {
                return true; 
            }

            // -------------------------------------------------------------------------
            // FASE AKTIF: PAKSA DIAM & JALANKAN SERANGAN ROKET KUSTOM (STATE = 1)
            // -------------------------------------------------------------------------
            if (skillState == 1)
            {
                // FORCE DIAM: Mengunci total posisi Gastropod di udara
                npc.velocity = Vector2.Zero;

                // Jeda antar tembakan roket (1 roket keluar setiap 15 frame / 0.25 detik sekali)
                fireDelayTimer++;
                if (fireDelayTimer >= 15)
                {
                    fireDelayTimer = 0; // Reset jeda tembakan

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // --- BALANCING KECEPATAN AWAL ROKET ---
                        Vector2 launchVelocity = new Vector2(0f, -8f); // Melesat lurus ke atas langit

                        // --- BALANCING DAMAGE ROKET GASTROPOD ---
                        // Base damage 25, di Master Mode otomatis dikali 4 (= 100 Damage)
                        int rocketDamage = 25; 

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center + new Vector2(0f, -20f), // Keluar dari atas kepalanya
                            launchVelocity,
                            ModContent.ProjectileType<RainbowRocket>(),
                            rocketDamage,
                            4f,
                            Main.myPlayer
                        );
                    }

                    // Suara desisan roket meluncur
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item13, npc.Center);

                    rocketCount++; // Tambah hitungan roket yang sudah keluar
                }

                // --- REKUES: BATASAN 5 MISSILE ---
                if (rocketCount >= 1)
                {
                    skillState = 0;     // Matikan status skill khusus (Kembali terbang normal)
                    rocketCount = 0;    // Reset hitungan jumlah roket ke 0
                    fireDelayTimer = 0; // Reset timer delay
                    
                    // --- REKUES BALANCING: COOLDOWN 5 DETIK ---
                    // 5 detik * 60 frame = 300 frame jeda sebelum bisa mengamuk lagi
                    skillCooldown = 300f; 
                }

                // Matikan AI bawaan selama dia menembak 5 roket agar posisinya aman terkunci
                return false; 
            }

            // -------------------------------------------------------------------------
            // FASE IDLE: JALANKAN TIMEOUT COOLDOWN (AI DEFAULT GASTROPOD JALAN PENUH)
            // -------------------------------------------------------------------------
            if (skillCooldown > 0f)
            {
                skillCooldown--; // Kurangi timer cooldown mandiri
            }
            else
            {
                // Cooldown habis! Aktifkan mode menembak vertikal di frame berikutnya
                skillState = 1;
                fireDelayTimer = 0;
                rocketCount = 0;
            }

            // Jalankan AI default (terbang & nembak laser pink bawaan) selama cooldown berlangsung
            return true; 
        }
    }
}