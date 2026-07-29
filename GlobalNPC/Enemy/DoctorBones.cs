using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class DoctorBonesRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer internal untuk jeda serangan 2 detik
        public int boulderTimer = 0;

        public override bool PreAI(NPC npc)
        {
            // LOKASI ID TARGET: 52 (Doctor Bones)
            if (npc.type != NPCID.DoctorBones) return true;

            // --- FIX ERROR: Pengecekan Target yang Aman ---
            // Memastikan target berada dalam jangkauan array dan aktif sebelum diakses
            if (npc.target < 0 || npc.target >= Main.maxPlayers || !Main.player[npc.target].active || Main.player[npc.target].dead)
            {
                return true; // Keluar jika tidak ada target valid
            }

            Player target = Main.player[npc.target];

            // --- LOGIKA SPAWN BOULDER (TIAP 2 DETIK) ---
            boulderTimer++;

            // LOKASI TIMING ATTACK: 120 frame = 2 Detik
            if (boulderTimer >= 120)
            {
                boulderTimer = 0; // Reset timer

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // Hitung arah dasar menuju ke koordinat pusat player
                    Vector2 baseDirection = target.Center - npc.Center;
                    baseDirection.Normalize();

                    // Mengeluarkan 3 Projectile sekaligus
                    for (int i = 0; i < 3; i++)
                    {
                        // 1. PENGACAKAN ARAH (VELOCITY SPREAD)
                        float spreadFactor = Main.rand.NextFloat(-0.22f, 0.22f);
                        Vector2 perturbedVelocity = baseDirection.RotatedBy(spreadFactor);

                        // 2. PENGACAKAN KECEPATAN (SPEED)
                        float randomSpeed = Main.rand.NextFloat(5.0f, 9.0f);

                        // Gabungkan arah acak dengan kecepatan acak
                        Vector2 finalVelocity = perturbedVelocity * randomSpeed;

                        // LOKASI ARC EFFECT: Diberi gaya ke atas acak biar batunya melengkung
                        finalVelocity.Y -= Main.rand.NextFloat(2.0f, 4.5f);

                        // Spawn Projectile
                        int projIndex = Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            finalVelocity,
                            ProjectileID.MiniBoulder, // Asset batu kecil
                            16, 
                            4f, 
                            Main.myPlayer
                        );

                        // --- FIX NETRAL: Pastikan akses projectile aman ---
                        if (projIndex < Main.maxProjectiles && Main.projectile[projIndex].active)
                        {
                            Main.projectile[projIndex].hostile = true;  // Menyerang player
                            Main.projectile[projIndex].friendly = false; // TIDAK menyerang musuh (NPC)
                        }
                    }
                }

                // Efek suara lemparan batu
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item19, npc.Center);
            }

            // Kembalikan true agar AI lompat bawaan zombie Doctor Bones tetap aktif normal
            return true;
        }
    }
}