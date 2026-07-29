using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class GiantCursedSkullRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer utama untuk cooldown antar-set serangan (5 detik = 300 ticks)
        private int attackCooldownTimer = 0;

        // Variabel untuk mengatur mode beruntun (Barrage)
        private int soulsLeftToShoot = 0; // Sisa peluru yang harus dikeluarkan dalam satu siklus barrage
        private int barrageDelayTimer = 0; // Jeda waktu antar-peluru saat sedang memuntahkan barrage

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.GiantCursedSkull;
        }

        // =========================================================================
        // [AI REWORK LOCATION]: SKILL BARRAGE 3 LOST SOUL BERUNTUN
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.GiantCursedSkull) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active) return;

            // --- LOGIKA UTAMA COOLDOWN JEDA SERANGAN GLOBAL ---
            if (soulsLeftToShoot <= 0)
            {
                attackCooldownTimer++;
            }

            // -------------------------------------------------------------------------
            // [AI COOLDOWN BALANCING]: 300 Ticks = 5 Detik Cooldown Sebelum Barrage Baru Dimulai
            // -------------------------------------------------------------------------
            if (attackCooldownTimer >= 300 && soulsLeftToShoot <= 0)
            {
                attackCooldownTimer = 0;
                
                // -------------------------------------------------------------------------
                // [BARRAGE COUNT BALANCING]: Mengisi jumlah amunisi barrage (3 Peluru)
                // -------------------------------------------------------------------------
                soulsLeftToShoot = 3; 
                barrageDelayTimer = 0; // Biar peluru pertama langsung keluar instan
            }

            // --- LOGIKA PROSES SUNTIKAN BERUNTUN (BARRAGE ACTIVE) ---
            if (soulsLeftToShoot > 0)
            {
                barrageDelayTimer++;

                // -------------------------------------------------------------------------
                // [BARRAGE DELAY BALANCING]: Jeda antar-peluru (10 Ticks = ~0.16 Detik per peluru)
                // Semakin KECIL angkanya, brondongan peluru keluar semakin rapat/cepat
                // -------------------------------------------------------------------------
                if (barrageDelayTimer >= 10)
                {
                    barrageDelayTimer = 0; // Reset jeda internal peluru
                    soulsLeftToShoot--;    // Kurangi sisa amunisi

                    // Hitung arah presisi menuju player saat peluru diformasikan keluar
                    Vector2 shootDir = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    
                    // -------------------------------------------------------------------------
                    // [PROJECTILE SPEED & DAMAGE BALANCING]
                    // -------------------------------------------------------------------------
                    float soulSpeed = 6.5f; 
                    int finalDamage = 30; // Mengikuti standar penyesuaian damage dunia

                    // Spawn LostSoulHostile (ID: 288) tepat dari bagian tengah tengkorak
                    int p = Projectile.NewProjectile(
                        npc.GetSource_FromAI(), 
                        npc.Center, 
                        shootDir * soulSpeed, 
                        ProjectileID.LostSoulHostile, 
                        finalDamage, 
                        1.5f, 
                        Main.myPlayer
                    );

                    // Pastikan properti peluru murni memusuhi player
                    if (p != Main.maxProjectiles)
                    {
                        Main.projectile[p].hostile = true;
                        Main.projectile[p].friendly = false;
                    }

                    // Mainkan sound effect hantu menjerit setiap kali 1 peluru menyembur keluar
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath6, npc.Center);

                    npc.netUpdate = true;
                }
            }
        }
    }
}