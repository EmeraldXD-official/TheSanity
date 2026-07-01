using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using TheSanity.Projectiles;

namespace TheSanity.GlobalNPCs
{
    public class GolemFistOverride : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private Vector2 launchVelocity = Vector2.Zero;
        private int flyTicks = 0; // Timer pengaman saat terbang

        public override bool PreAI(NPC npc)
        {
            if (npc.type == NPCID.GolemFistLeft || npc.type == NPCID.GolemFistRight)
            {
                // FASE 1: Tangan meluncur keluar
                if (npc.ai[0] == 1f)
                {
                    flyTicks++; // Hitung frame selama terbang

                    // Kunci kecepatan awal peluncuran
                    if (launchVelocity == Vector2.Zero)
                    {
                        if (npc.velocity != Vector2.Zero)
                        {
                            launchVelocity = Vector2.Normalize(npc.velocity) * 16f;
                        }
                        else
                        {
                            npc.TargetClosest(true);
                            Player target = Main.player[npc.target];
                            launchVelocity = (target.Center - npc.Center).SafeNormalize(Vector2.UnitX * npc.direction) * 16f;
                        }
                    }

                    // Paksa kecepatan konstan tiap frame
                    npc.velocity = launchVelocity;

                    // 🔥 FIX UTAMA: Gunakan SolidCollision untuk mendeteksi block secara manual!
                    // flyTicks > 8 berfungsi sebagai pengaman agar tangan tidak langsung meledak di tubuh Golem sendiri saat baru lahir
                    if (flyTicks > 8 && Collision.SolidCollision(npc.position, npc.width, npc.height))
                    {
                        // Picu ledakan duri + laser memantul balik
                        TriggerFistImpact(npc);

                        // Paksa pulang ke tubuh Golem (Fase 2)
                        npc.ai[0] = 2f; 
                        launchVelocity = Vector2.Zero;
                        flyTicks = 0;
                        npc.netUpdate = true;
                    }

                    return false; // Matikan AI pembatas jarak milik vanilla
                }
                else
                {
                    // Jika sedang diam atau jalan pulang, reset semua tracker
                    launchVelocity = Vector2.Zero;
                    flyTicks = 0;
                }
            }

            return true;
        }

        private void TriggerFistImpact(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            npc.TargetClosest(true);
            Player target = Main.player[npc.target];
            Vector2 spawnPos = npc.Center;

            // Vektor arah pantulan balik menuju ke lokasi Player saat ini
            Vector2 dirToPlayer = (target.Center - spawnPos).SafeNormalize(new Vector2(npc.direction * -1, -1));

            // MEKANIK 1: Mengeluarkan 2-4 DeerclopsRangedProjectile (Duri Rusa)
            int deerCount = Main.rand.Next(2, 5); 
            for (int i = 0; i < deerCount; i++)
            {
                Vector2 spreadVel = dirToPlayer.RotatedByRandom(MathHelper.ToRadians(25)) * Main.rand.NextFloat(8f, 12f);
                
                int spike = Projectile.NewProjectile(
                    npc.GetSource_FromAI(), 
                    spawnPos, 
                    spreadVel, 
                    ProjectileID.DeerclopsRangedProjectile, 
                    29, 
                    3f, 
                    Main.myPlayer
                );

                if (spike != Main.maxProjectiles)
                {
                    Main.projectile[spike].friendly = false;
                    Main.projectile[spike].hostile = true;
                    Main.projectile[spike].GetGlobalProjectile<GolemProjectileMod>().isFromGolem = true;
                }
            }

            // MEKANIK 2: Mengeluarkan 3 Laser Menyebar (Tengah, Serong Atas, Serong Bawah)
            float laserSpeed = 13f;
            float spreadAngle = MathHelper.ToRadians(18f); 

            int l1 = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, dirToPlayer * laserSpeed, ProjectileID.EyeBeam, 35, 1f, Main.myPlayer);
            int l2 = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, dirToPlayer.RotatedBy(spreadAngle) * laserSpeed, ProjectileID.EyeBeam, 35, 1f, Main.myPlayer);
            int l3 = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, dirToPlayer.RotatedBy(-spreadAngle) * laserSpeed, ProjectileID.EyeBeam, 35, 1f, Main.myPlayer);

            int[] lasers = { l1, l2, l3 };
            for (int i = 0; i < 3; i++)
            {
                if (lasers[i] != Main.maxProjectiles)
                {
                    Main.projectile[lasers[i]].friendly = false;
                    Main.projectile[lasers[i]].hostile = true;
                }
            }

            // Suara ledakan hantaman kuil
            SoundEngine.PlaySound(SoundID.Item14, npc.position);
        }
    }
}