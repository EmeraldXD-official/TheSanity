using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;

namespace TheSanity
{
    public class HoppinJackRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        private int fireTimer = 0;

        public override void PostAI(NPC npc)
        {
            // NPC ID 304 adalah Hoppin' Jack
            if (npc.type != 304) return;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead) return;

            // --- 1. GREEK FIRE SPAM (SETIAP 1 DETIK) ---
            fireTimer++;
            if (fireTimer >= 60) // 60 frame = 1 detik
            {
                // Pilih Projectile secara acak (GreekFire 1, 2, atau 3)
                int[] fireTypes = { ProjectileID.GreekFire1, ProjectileID.GreekFire2, ProjectileID.GreekFire3 };
                int chosenFire = fireTypes[Main.rand.Next(fireTypes.Length)];

                // --- LOKASI DAMAGE: 13 (Master Mode x3 = 39, mendekati 40) ---
                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, new Vector2(0, 2f), chosenFire, 13, 1f, Main.myPlayer);
                
                if (p != Main.maxProjectiles)
                {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                    // Tag khusus agar tidak tertukar dengan GreekFire lain
                    Main.projectile[p].ai[1] = 999f; 
                }

                fireTimer = 0;
            }
        }

        // --- 2. SPAWN NATURAL (HARDMODE + NIGHT ONLY) ---
        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            // Syarat: Hardmode DAN Malam Hari DAN di Permukaan (Surface)
            if (Main.hardMode && !Main.dayTime && spawnInfo.Player.ZoneOverworldHeight)
            {
                if (!pool.ContainsKey(304))
                {
                    // Rate 0.2f (setara dengan musuh malam standar lainnya)
                    pool.Add(304, 0.2f);
                }
            }
        }
    }

    public class HoppinJackProjectileLogic : GlobalProjectile
    {
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            // Cek jika terkena GreekFire milik Hoppin' Jack
            if ((projectile.type == ProjectileID.GreekFire1 || projectile.type == ProjectileID.GreekFire2 || projectile.type == ProjectileID.GreekFire3) 
                && projectile.ai[1] == 999f)
            {
                // Memberikan debuff 323 selama 5 detik (300 frame)
                target.AddBuff(323, 5 * 60);
            }
        }
    }
}