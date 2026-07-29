using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class InfernoBoltDetonator : global::Terraria.ModLoader.GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override void AI(Projectile projectile)
        {
            // Pastikan kita hanya memeriksa proyektil InfernoHostileBolt (ID: 61) yang berbahaya bagi player
            if (projectile.type == ProjectileID.InfernoHostileBolt && projectile.hostile)
            {
                // Cari player terdekat dari koordinat peluru api saat ini
                Player target = Main.player[Player.FindClosest(projectile.Center, 1, 1)];

                if (target != null && target.active && !target.dead)
                {
                    // Hitung jarak antara pusat peluru dengan pusat player
                    float distanceToPlayer = Vector2.Distance(projectile.Center, target.Center);

                    // [PROXIMITY DETONATION DISTANCE LOCATION]
                    // 5 Block = 80 Pixel (1 block di Terraria ukurannya 16x16 pixel)
                    if (distanceToPlayer <= 80f)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            // [INFERNO BLAST DAMAGE LOCATION] 
                            // Kita paksa panggil InfernoHostileBlast (ID: 62) tepat di posisi peluru saat ini
                            int b = Projectile.NewProjectile(
                                projectile.GetSource_FromThis(), 
                                projectile.Center, 
                                Vector2.Zero, // Ledakan diam di tempat, tidak bergerak
                                ProjectileID.InfernoHostileBlast, 
                                35, 
                                1f, 
                                Main.myPlayer
                            );

                            if (b < Main.maxProjectiles)
                            {
                                Main.projectile[b].hostile = true;
                                Main.projectile[b].friendly = false;
                            }
                        }

                        // --- LOGIKA PAKSA MATI VANILLA AI ---
                        // Kita paksa hilangkan peluru asli agar tidak terus melaju kedepan dan tidak memicu double hit
                        projectile.active = false; 
                        projectile.netUpdate = true;
                    }
                }
            }
        }
    }
}