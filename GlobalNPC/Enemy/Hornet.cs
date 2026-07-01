using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class HornetRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Custom timer untuk mengatur jeda nembak 2 detik sekali
        public int shootTimer = 0;

        public override bool PreAI(NPC npc)
        {
            // --- 1. FILTER SEMUA ID HORNET ---
            bool isHornet = npc.type == 42 || npc.type == 231 || npc.type == 232 || 
                            npc.type == 233 || npc.type == 234 || npc.type == 235 ||
                            npc.type == -16 || npc.type == -17 || npc.type == -56 || 
                            npc.type == -57 || npc.type == -58 || npc.type == -59 || 
                            npc.type == -60 || npc.type == -61 || npc.type == -62 || 
                            npc.type == -63 || npc.type == -64 || npc.type == -65;

            if (!isHornet) return true;

            // --- FIX ERROR: Pengecekan target yang aman agar tidak NullReferenceException ---
            if (npc.target < 0 || npc.target >= Main.maxPlayers || !Main.player[npc.target].active || Main.player[npc.target].dead)
            {
                return true;
            }

            Player target = Main.player[npc.target];

            // --- Logika arah vanilla ---
            npc.direction = target.Center.X > npc.Center.X ? 1 : -1;
            npc.spriteDirection = npc.direction;

            // --- 2. OVERRIDE AI ATTACK (TIMER 2 DETIK) ---
            shootTimer++;
            
            // LOKASI TIMING ATTACK: 120 Frame = 2 Detik
            if (shootTimer >= 120)
            {
                shootTimer = 0; // Reset timer

                // Pastikan hanya server/singleplayer yang memunculkan projectile agar tidak desync di multiplayer
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    ExecuteShotgunSpread(npc, target);
                }
            }

            // Kembalikan true agar AI terbang bawaan vanilla tetap jalan.
            // Di vanilla, npc.ai[1] mengatur timer tembakan. Kita paksa reset agar AI aslinya TIDAK nembak stinger tunggal.
            npc.ai[1] = 0; 

            return true;
        }

        private void ExecuteShotgunSpread(NPC npc, Player target)
        {
            // Hitung arah dasar menuju ke koordinat pusat player
            Vector2 baseDirection = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);

            // LOKASI SPEED STINGER: 9f (Kecepatan terbang projectile)
            float projectileSpeed = 9f; 

            // LOKASI DAMAGE STINGER: 12 (Bisa kamu balance sendiri nanti)
            int damage = 12; 

            // ProjectileID.Stinger = 55 (Stinger musuh bawaan Terraria)
            int projType = ProjectileID.Stinger; 

            // Sudut sebaran shotgun (Spread Angle). 15 derajat dikonversi ke Radian
            float spreadAngle = MathHelper.ToRadians(15f);

            // Tembak 3 Stinger sekaligus dengan rotasi spread
            for (int i = -1; i <= 1; i++)
            {
                // Putar arah stinger berdasarkan indeks (-1 = kiri, 0 = lurus, 1 = kanan)
                Vector2 rotatedVelocity = baseDirection.RotatedBy(i * spreadAngle) * projectileSpeed;

                // Spawn projectile stinger ke dunia game
                int projIndex = Projectile.NewProjectile(
                    npc.GetSource_FromAI(), 
                    npc.Center, 
                    rotatedVelocity, 
                    projType, 
                    damage, 
                    0f, 
                    Main.myPlayer
                );

                // Pastikan projectile tahu bahwa dia adalah milik musuh
                if (projIndex < Main.maxProjectiles && Main.projectile[projIndex].active)
                {
                    Main.projectile[projIndex].npcProj = true;
                    Main.projectile[projIndex].hostile = true;
                    Main.projectile[projIndex].friendly = false;
                }
            }

            // --- 3. AUDIO VISUAL SAAT SHOOT ---
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item17, npc.Center); // Suara tiupan panah stinger halus

            for (int i = 0; i < 10; i++)
            {
                Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Venom, baseDirection.X * 3f, baseDirection.Y * 3f, 100, default, 1.2f);
                d.noGravity = true;
            }
        }
    }
}