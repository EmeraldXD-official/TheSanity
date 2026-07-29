using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO; // <--- INI DIA YANG KURANG!
using Microsoft.Xna.Framework;
using Terraria.Audio;
using System;
using System.IO;

namespace TheSanity
{
    public class ZombieMermanRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private bool hasSpawnedMinions = false;
        private int attackPhase = 0; // 0 = Jalan & Duri, 1 = Diam & Putar Jangkar, 2 = Jeda Pasca Lempar
        private int attackTimer = 0;

        private float lastSpikeXPosition = 0f;
        private bool isFirstStep = true;
        private int heldAnchorIndex = -1;
        private float customRotationCounter = 0f;

        public override bool AppliesToEntity(NPC npc, bool lateInstantiation)
        {
            return npc.type == NPCID.ZombieMerman;
        }

        // =========================================================================
        // FIX REAL 1.4.4+: Signature Hook Sinkronisasi yang Benar untuk GlobalNPC
        // =========================================================================
        public override void SendExtraAI(NPC npc, BitWriter bitWriter, BinaryWriter writer)
        {
            bitWriter.WriteBit(hasSpawnedMinions); // Menulis boolean secara efisien
            writer.Write(attackPhase);
            writer.Write(attackTimer);
        }

        public override void ReceiveExtraAI(NPC npc, BitReader bitReader, BinaryReader reader)
        {
            hasSpawnedMinions = bitReader.ReadBit(); // Membaca boolean
            attackPhase = reader.ReadInt32();
            attackTimer = reader.ReadInt32();
        }

        public override bool PreAI(NPC npc)
        {
            if (Main.gameMenu) return true;

            Player target = null; 
            if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            
            if (target == null || !target.active || target.dead) 
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    ResetMermanAI(npc);
                }
                return true;
            }

            // ---------------------------------------------------------
            // MEKANIK 1: BURST SPAWN (Hanya Berjalan 1 Kali)
            // ---------------------------------------------------------
            if (!hasSpawnedMinions)
            {
                hasSpawnedMinions = true;

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // [MINION SPAWN QUANTITY BALANCING]
                    int totalZombies = Main.rand.Next(4, 7); 

                    for (int i = 0; i < totalZombies; i++)
                    {
                        Vector2 spawnPos = npc.Center + new Vector2(Main.rand.Next(-20, 21), Main.rand.Next(-10, 11));
                        int zombieIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, NPCID.Zombie);
                        
                        if (zombieIndex != Main.maxNPCs)
                        {
                            NPC zombie = Main.npc[zombieIndex];
                            zombie.target = npc.target;
                            
                            float launchX = Main.rand.NextFloat(-5f, 5f);
                            float launchY = Main.rand.NextFloat(-7f, -3f); 
                            zombie.velocity = new Vector2(launchX, launchY);
                            zombie.netUpdate = true;

                            for (int j = 0; j < 6; j++)
                            {
                                Dust.NewDust(zombie.position, zombie.width, zombie.height, DustID.Blood, launchX * 0.5f, launchY * 0.5f);
                            }
                        }
                    }
                    SoundEngine.PlaySound(SoundID.NPCDeath21, npc.Center); 
                    npc.netUpdate = true; 
                }
            }

            // =========================================================================
            // CORE STATE MACHINE: LOGIKA FASE SERANGAN
            // =========================================================================
            if (attackPhase == 0)
            {
                if (isFirstStep)
                {
                    isFirstStep = false;
                    lastSpikeXPosition = npc.Bottom.X;
                }

                float horizontalDistanceMoved = Math.Abs(npc.Bottom.X - lastSpikeXPosition);

                if (horizontalDistanceMoved >= 16f && npc.velocity.Y == 0f && Math.Abs(npc.velocity.X) > 0.1f)
                {
                    lastSpikeXPosition = npc.Bottom.X;
                    Vector2 spikePos = new Vector2(npc.Bottom.X, npc.Bottom.Y - 4f);

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // [SPIKE TRAIL DAMAGE BALANCING LOCATION]
                        int p = Projectile.NewProjectile(
                            npc.GetSource_FromAI(), 
                            spikePos, 
                            Vector2.Zero, 
                            ModContent.ProjectileType<BloodSpikeHostile>(), 
                            24, // Damage duri tanah
                            1f, 
                            npc.target
                        );
                        if (p != Main.maxProjectiles)
                        {
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                            Main.projectile[p].netUpdate = true;
                        }
                    }
                }

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    attackTimer++;
                    // [THROW COOLDOWN BALANCING LOCATION]
                    if (attackTimer >= 360)
                    {
                        attackPhase = 1; 
                        attackTimer = 0;
                        customRotationCounter = 0f;
                        npc.velocity = Vector2.Zero; 
                        npc.netUpdate = true; 
                    }
                }

                return true; 
            }

            // Membekukan musuh saat menyerang
            npc.velocity.X = 0f;
            if (npc.velocity.Y < 0f) npc.velocity.Y = 0f; 

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                attackTimer++;

                if (attackPhase == 1)
                {
                    if (heldAnchorIndex == -1)
                    {
                        int a = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<AnchorVisual>(), 0, 0f, npc.target);
                        if (a != Main.maxProjectiles)
                        {
                            heldAnchorIndex = a;
                            Main.projectile[a].ai[0] = 1f;        
                            Main.projectile[a].ai[1] = npc.whoAmI; 
                            NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, a);
                        }
                    }

                    if (attackTimer >= 60)
                    {
                        if (heldAnchorIndex != -1)
                        {
                            Main.projectile[heldAnchorIndex].Kill();
                            heldAnchorIndex = -1;
                        }

                        Vector2 shootDirection = target.Center - npc.Center;
                        shootDirection.Normalize();

                        // [ANCHOR THROW SPEED BALANCING LOCATION]
                        float throwSpeed = 16f; 
                        Vector2 launchVelocity = shootDirection * throwSpeed;

                        // [ANCHOR THROW DAMAGE BALANCING LOCATION]
                        int p = Projectile.NewProjectile(
                            npc.GetSource_FromAI(), 
                            npc.Center, 
                            launchVelocity, 
                            ModContent.ProjectileType<AnchorVisual>(), 
                            38, // Damage lemparan jangkar raksasa
                            2f, 
                            npc.target
                        );
                        
                        if (p != Main.maxProjectiles)
                        {
                            Main.projectile[p].ai[0] = 0f; 
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                            Main.projectile[p].rotation = launchVelocity.ToRotation(); 
                            NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, p);
                        }

                        SoundEngine.PlaySound(SoundID.Item74, npc.Center); 

                        attackPhase = 2;
                        attackTimer = 0;
                        npc.netUpdate = true;
                    }
                }
                else if (attackPhase == 2)
                {
                    if (attackTimer >= 40)
                    {
                        ResetMermanAI(npc);
                    }
                }
            }

            if (attackPhase == 1)
            {
                isFirstStep = true; 
                
                // [ANCHOR SPIN SPEED BALANCING LOCATION]
                customRotationCounter += 0.25f; 

                Projectile anchor = null;
                if (Main.netMode != NetmodeID.MultiplayerClient && heldAnchorIndex != -1)
                {
                    anchor = Main.projectile[heldAnchorIndex];
                }
                else
                {
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        Projectile p = Main.projectile[i];
                        if (p.active && p.type == ModContent.ProjectileType<AnchorVisual>() && p.ai[0] == 1f && p.ai[1] == npc.whoAmI)
                        {
                            anchor = p;
                            break;
                        }
                    }
                }

                if (anchor != null && anchor.active)
                {
                    anchor.Center = npc.Center;
                    anchor.rotation = customRotationCounter;
                    anchor.timeLeft = 10; 
                }
            }

            return false; 
        }

        private void ResetMermanAI(NPC npc)
        {
            if (heldAnchorIndex != -1)
            {
                if (Main.projectile[heldAnchorIndex].active) Main.projectile[heldAnchorIndex].Kill();
                heldAnchorIndex = -1;
            }
            attackPhase = 0;
            attackTimer = 0;
            isFirstStep = true; 
            customRotationCounter = 0f;
            
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                npc.netUpdate = true;
            }
        }
    }
}