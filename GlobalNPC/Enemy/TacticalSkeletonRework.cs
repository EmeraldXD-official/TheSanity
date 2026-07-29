using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class TacticalSkeletonRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer kustom untuk menghitung jeda tembakan (3 detik = 180 ticks)
        private int shotgunTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.TacticalSkeleton;
        }

        // =========================================================================
        // [AI REWORK LOCATION]: TEMBAKAN SHOTGUN BOLA MERIAM SETIAP 3 DETIK
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.TacticalSkeleton) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active) return;

            shotgunTimer++;

            // -------------------------------------------------------------------------
            // [AI COOLDOWN BALANCING]: 180 Ticks = 3 Detik Jeda Tembakan
            // -------------------------------------------------------------------------
            if (shotgunTimer >= 180)
            {
                shotgunTimer = 0; // Reset timer kustom

                // Hitung arah lurus menuju ke pusat koordinat player
                Vector2 shootDir = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                
                // -------------------------------------------------------------------------
                // [SPEED & SPREAD BALANCING]: Kecepatan bola meriam dan jumlah peluru
                // -------------------------------------------------------------------------
                float cannonSpeed = 9f; // Kecepatan luncuran bola meriam
                int bulletCount = 3;    // Jumlah tembakan shotgun (3 arah menyebar)
                float spreadAngle = MathHelper.ToRadians(15); // Jarak penyebaran sudut antar bola (15 derajat)

                // Loop untuk menembakkan pola menyebar (Kiri, Tengah, Kanan)
                for (int i = 0; i < bulletCount; i++)
                {
                    // Kalkulasi sudut kemiringan peluru (-15°, 0°, +15°)
                    float rotation = spreadAngle * (i - (bulletCount - 1) / 2f);
                    Vector2 finalVelocity = shootDir.RotatedBy(rotation) * cannonSpeed;

                    // Damage dasar bola meriam di Master Mode otomatis menyesuaikan dari nilai 35 ini
                    int finalDamage = 35;

                    // Spawn CannonballHostile (ID: 162)
                    int p = Projectile.NewProjectile(
                        npc.GetSource_FromAI(), 
                        npc.Center, 
                        finalVelocity, 
                        ProjectileID.CannonballHostile, 
                        finalDamage, 
                        5f, 
                        Main.myPlayer
                    );

                    // Pastikan properti peluru dikunci murni memusuhi player
                    if (p != Main.maxProjectiles)
                    {
                        Main.projectile[p].hostile = true;
                        Main.projectile[p].friendly = false;
                    }
                }

                // Efek suara tembakan meriam berat saat meledak keluar
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item11, npc.Center);

                npc.netUpdate = true;
            }
        }
    }
}