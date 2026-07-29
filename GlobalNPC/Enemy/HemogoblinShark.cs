using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using System;
using TheSanity.Projectiles; 

namespace TheSanity.GlobalNPC.Enemy
{
    public class HemogoblinSharkRework : Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true; 

        private bool hasSpawnedMinions = false;
        private int attackTimer = 0;
        private int nextAttackTime = 300; 
        private int axeTeleportProjID = -1;

        public override void PostAI(NPC npc)
        {
            if (npc.type != NPCID.GoblinShark) 
                return;

            // =========================================================================
            // 1. MECHANIC: SPAWN 5 CUSTOM VAMPIRE FROGS (ON SPAWN ENHANCEMENT)
            // =========================================================================
            if (!hasSpawnedMinions && Main.netMode != NetmodeID.MultiplayerClient)
            {
                int frogType = ModContent.NPCType<SanityMinionFrog>();
                for (int i = 0; i < 5; i++)
                {
                    int frogIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X + Main.rand.Next(-40, 41), (int)npc.Center.Y, frogType);
                    if (frogIndex < Main.maxNPCs)
                    {
                        Main.npc[frogIndex].velocity = new Vector2(Main.rand.NextFloat(-2f, 2f), -3f);
                    }
                }
                hasSpawnedMinions = true;
            }

            // =========================================================================
            // 2. MECHANIC: CONSTANT TRAIL (BLOOD SPIKE HOSTILE)
            // =========================================================================
            if (Math.Abs(npc.velocity.X) > 0.01f && Main.netMode != NetmodeID.MultiplayerClient)
            {
                if (Main.rand.NextBool(7)) 
                {
                    int spikeType = ModContent.ProjectileType<BloodSpikeHostile>(); 
                    // [TRAIL DAMAGE BALANCING LOCATION]
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, new Vector2(0f, 1f), spikeType, (int)(npc.damage * 0.4f), 1f, Main.myPlayer);
                }
            }

            // =========================================================================
            // 3. MECHANIC: TELEPORT & AXE THROW (BLOODY AXE)
            // =========================================================================
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target != null && target.active && !target.dead)
            {
                attackTimer++;

                if (attackTimer >= nextAttackTime && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    attackTimer = 0;
                    nextAttackTime = Main.rand.Next(300, 1201); 

                    // [BLOODY AXE BALANCING DAMAGE LOCATION]
                    // Menggunakan flat damage kecil (25) agar di Master Mode tidak jadi 800+ damage
                    int safeAxeDamage = 25; 

                    if (Main.rand.NextBool())
                    {
                        // --- SKILL A: MELEMPAR 3 KAPAK SEKALIGUS (SHOTGUN SPREAD) ---
                        int axeType = ModContent.ProjectileType<BloodyAxeProjectile>();
                        Vector2 shootDirection = target.Center - npc.Center;
                        shootDirection.Normalize();
                        shootDirection *= 8.5f; 

                        for (int i = -1; i <= 1; i++)
                        {
                            Vector2 perturbedSpeed = shootDirection.RotatedBy(MathHelper.ToRadians(i * 15)); 
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, perturbedSpeed, axeType, safeAxeDamage, 3f, Main.myPlayer);
                        }
                        SoundEngine.PlaySound(SoundID.Item71, npc.Center); 
                    }
                    else
                    {
                        // --- SKILL B: TELEPORTASI KAPAK PENANDA ---
                        int axeType = ModContent.ProjectileType<BloodyAxeProjectile>();
                        Vector2 shootDirection = target.Center - npc.Center;
                        shootDirection.Normalize();
                        shootDirection *= 10f; 

                        int projIndex = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootDirection, axeType, safeAxeDamage, 4f, Main.myPlayer);
                        
                        if (projIndex < Main.maxProjectiles)
                        {
                            axeTeleportProjID = projIndex; 
                        }
                    }
                }

                // --- SISTEM DETEKSI & TELEPORTASI KAPAK ---
                if (axeTeleportProjID != -1)
                {
                    Projectile trackingAxe = Main.projectile[axeTeleportProjID];
                    
                    if (trackingAxe.active && trackingAxe.type == ModContent.ProjectileType<BloodyAxeProjectile>())
                    {
                        // [TELEPORT TELEGRAPH FRAME BALANCING LOCATION]
                        // Ketika sisa umur kapak berada di bawah 60 frame (1 detik terakhir sebelum TP)
                        if (trackingAxe.timeLeft <= 60)
                        {
                            // Kapak berhenti melaju dan diam di tempat sebagai penanda tetap
                            trackingAxe.velocity = Vector2.Zero;

                            // Mengeluarkan partikel darah berputar masif untuk memperingatkan player
                            if (Main.rand.NextBool(2))
                            {
                                Vector2 dustVelocity = Main.rand.NextVector2Circular(4f, 4f);
                                Dust d = Dust.NewDustDirect(trackingAxe.position, trackingAxe.width, trackingAxe.height, DustID.Blood, dustVelocity.X, dustVelocity.Y, 100, Color.Red, 1.5f);
                                d.noGravity = true;
                            }
                        }

                        // Detik-0: Waktunya teleportasi (saat timeLeft habis / menyentuh angka 0 atau di bawah 2 frame)
                        if (trackingAxe.timeLeft <= 2)
                        {
                            // Pindahkan posisi Hemogoblin tepat ke tengah koordinat kapak
                            npc.position = trackingAxe.position - new Vector2(npc.width / 2, npc.height / 2);
                            npc.velocity = Vector2.Zero; 

                            // Efek ledakan darah masif di tempat kedatangan baru
                            for (int k = 0; k < 25; k++)
                            {
                                Dust.NewDust(npc.position, npc.width, npc.height, DustID.Blood, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f));
                            }
                            
                            SoundEngine.PlaySound(SoundID.Item6, npc.Center); 
                            
                            trackingAxe.Kill(); 
                            axeTeleportProjID = -1; 
                        }
                    }
                    else if (!trackingAxe.active)
                    {
                        axeTeleportProjID = -1; 
                    }
                }
            }
        }
    }
}