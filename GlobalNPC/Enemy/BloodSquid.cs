using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace TheSanity.GlobalNPC.Enemy
{
    public class BloodSquidRework : Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Variabel untuk mengatur burst fire (3 tembakan beruntun)
        public int burstTimer = 0;
        public int shotsFired = 0;
        public bool isBursting = false;
        public int attackCooldown = 180; // Jeda antar rentetan tembakan (180 frame = 3 detik)

        // =========================================================================
        // 1. PENGATURAN SPAWN RATE DI DARATAN SAAT BLOOD MOON + HARDMODE ONLY (CHANCE 10%)
        // =========================================================================
        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            // KONDISI MUTLAK: Harus sedang Blood Moon DAN dunia sudah masuk Hardmode
            if (Main.bloodMoon && Main.hardMode)
            {
                // Pastikan player berada di permukaan (Overworld) atau langit (Sky), bukan di bawah tanah
                if (spawnInfo.Player.ZoneOverworldHeight || spawnInfo.Player.ZoneSkyHeight)
                {
                    // Ambil peluang spawn 10% (0.1f)
                    float spawnChance = 0.1f;

                    // Daftarkan Blood Squid (ID 619) ke dalam pool lingkungan malam jika belum terdaftar
                    if (!pool.ContainsKey(NPCID.BloodSquid)) 
                    {
                        pool.Add(NPCID.BloodSquid, spawnChance);
                    }
                }
            }
        }

        // =========================================================================
        // 2. MEKANIK SERANGAN BURST FIRE (3 TEMBAKAN BERUNTUN BUKAN SHOTGUN)
        // =========================================================================
        public override bool PreAI(NPC npc)
        {
            // Pastikan murni hanya merombak Blood Squid
            if (npc.type != NPCID.BloodSquid) return true;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (target == null || target.dead || !npc.HasValidTarget) return true;
            
            // Perhitungan logika timer serangan kustom
            if (!isBursting)
            {
                if (attackCooldown > 0) attackCooldown--;
                
                if (attackCooldown <= 0)
                {
                    isBursting = true;
                    burstTimer = 0;
                    shotsFired = 0;
                    attackCooldown = Main.rand.Next(180, 241); // Jeda acak buat rentetan berikutnya (3-4 detik)
                }
            }
            else
            {
                burstTimer++;
                
                // LOKASI TIMING JEDA TEMBAKAN BERUNTUN: Setiap 12 frame (~0.2 detik) melepas 1 peluru
                if (burstTimer >= 12)
                {
                    burstTimer = 0;
                    
                    // Eksekusi tembakan murni di sisi server/singleplayer agar tersinkronisasi
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 shootVelocity = target.Center - npc.Center;
                        shootVelocity.Normalize();
                        
                        // LOKASI SPEED PELURU BLOOD SHOT: 9.5f
                        shootVelocity *= 9.5f; 

                        // Ambil setengah damage bawaan status monster agar seimbang saat beruntun
                        int damage = npc.damage / 2; 

                        // Menembakkan Blood Shot (Projectile ID 799)
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center.X, npc.Center.Y, shootVelocity.X, shootVelocity.Y, ProjectileID.BloodShot, damage, 0f, Main.myPlayer);
                    }

                    // Suara tembakan tajam setiap peluru keluar
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item21, npc.Center);

                    shotsFired++;
                    if (shotsFired >= 3) // Batasi murni hanya 3 butir peluru
                    {
                        isBursting = false;
                    }
                }
            }

            // Memotong paksa AI projectile bawaan vanilla agar tidak menembak mode shotgun
            npc.ai[1] = 0; 

            return true; // Biarkan AI pergerakan melayang aslinya tetap bekerja
        }
    }
}