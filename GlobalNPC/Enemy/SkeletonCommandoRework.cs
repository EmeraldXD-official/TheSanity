using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace TheSanity
{
    // CLASS NPC Tetap Dibuat untuk Keperluan Pendaftaran Sesuai Struktur Folder
    public class SkeletonCommandoRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.SkeletonCommando;
        }
    }

    // =========================================================================
    // [PROJECTILE REWORK SYSTEM]: LOGIKA HOMING & TEMBUS BLOCK UNTUK ROKET
    // =========================================================================
    public class RocketSkeletonModifier : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        // Timer kustom untuk menghitung durasi aktifnya skill (5 detik = 300 ticks)
        private int homingTimer = 0;
        private bool isSkillActive = true;

        public override bool AppliesToEntity(Projectile projectile, bool lateInstantiation)
        {
            return projectile.type == ProjectileID.RocketSkeleton;
        }

        // 1. PADA SAAT SPAWN: Paksa proyektil agar bisa menembus dinding/block di awal
        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (projectile.hostile)
            {
                projectile.tileCollide = false; // Mematikan tabrakan dengan block (Tembus Dinding)
                isSkillActive = true;
                homingTimer = 0;
            }
        }

        // 2. SETIAP FRAME (AI): Logika pergerakan mengejar player terdekat
        public override void AI(Projectile projectile)
        {
            if (!projectile.hostile || !isSkillActive) return;

            homingTimer++;

            // -------------------------------------------------------------------------
            // [HOMING DURATION BALANCING]: 300 Ticks = 5 Detik Durasi Skill Aktif
            // -------------------------------------------------------------------------
            if (homingTimer >= 300)
            {
                isSkillActive = false;
                projectile.tileCollide = true; // Kembalikan agar bisa meledak jika menabrak dinding lagi
                projectile.netUpdate = true;
                return;
            }

            // Cari player terdekat untuk dijadikan target homing
            Player targetPlayer = null;
            float maxTrackDistance = 1000f; // Jarak maksimum roket bisa mendeteksi player

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead)
                {
                    float dist = Vector2.Distance(projectile.Center, p.Center);
                    if (dist < maxTrackDistance)
                    {
                        maxTrackDistance = dist;
                        targetPlayer = p;
                    }
                }
            }

            // Jika target ditemukan, belokkan arah roket menuju posisi player tersebut
            if (targetPlayer != null)
            {
                Vector2 targetDirection = (targetPlayer.Center - projectile.Center).SafeNormalize(Vector2.Zero);
                
                // -------------------------------------------------------------------------
                // [HOMING SPEED & TURN BALANCING]: Mengatur kecepatan guling belokan roket
                // -------------------------------------------------------------------------
                float rocketSpeed = projectile.velocity.Length(); // Pertahankan kecepatan asli roket
                float turnResistance = 25f; // Semakin BESAR angkanya, belokan roket semakin LAMBAN/KOTAK

                // Rumus interpolasi pergerakan sudut belok rapi
                projectile.velocity = (projectile.velocity * (turnResistance - 1f) + targetDirection * rocketSpeed) / turnResistance;
                
                // Samakan rotasi visual sprite roket dengan arah velocity barunya
                projectile.rotation = projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }
        }
    }
}