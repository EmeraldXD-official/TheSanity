using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using System;

namespace TheSanity
{
    public class ArmedZombieRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer cooldown khusus tiap zombie agar tidak melempar barengan
        private int throwCooldown = 0;

        public override bool AppliesToEntity(NPC npc, bool lateInstantiation)
        {
            // --- FILTER DAFTAR ID ZOMBIE YANG BISA MELEMPAR TANGAN ---
            bool isArmedZombie = npc.type == NPCID.ArmedZombie ||
                                 npc.type == NPCID.ArmedZombieEskimo ||
                                 npc.type == NPCID.ArmedZombiePincussion ||
                                 npc.type == NPCID.ArmedZombieSlimed ||
                                 npc.type == NPCID.ArmedZombieSwamp ||
                                 npc.type == NPCID.ArmedZombieTwiggy ||
                                 npc.type == NPCID.ArmedZombieCenx ||
                                 npc.type == NPCID.ArmedTorchZombie;

            return isArmedZombie;
        }

        public override void PostAI(NPC npc)
        {
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead) return;

            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            throwCooldown++;

            // [THROW COOLDOWN BALANCING LOCATION]
            // Zombie akan mencoba melempar setiap 6 Detik sekali (360 Frame)
            if (throwCooldown >= 360)
            {
                // Reset timer kembali ke nol dengan sedikit random offset (1 detik) biar ngga sinkron barengan
                throwCooldown = Main.rand.Next(0, 60); 

                // [RANGE BALANCING LOCATION]
                if (Vector2.Distance(npc.Center, target.Center) < 400f)
                {
                    // FIX: Mengganyi npc.Position dan target.Position menjadi huruf kecil (position)
                    if (Collision.CanHitLine(npc.position, npc.width, npc.height, target.position, target.width, target.height))
                    {
                        // 1. HITUNG ARAH DAN KECEPATAN LEMPARAN
                        Vector2 shootVel = target.Center - npc.Center;
                        shootVel.Normalize();

                        // [PROJECTILE SPEED BALANCING LOCATION]
                        shootVel *= 8.5f;

                        // Kasih sedikit variasi parabola ke atas biar lemparannya realistis
                        shootVel.Y -= 1.5f;

                        // 2. SPAWN PROJECTILE CUSTOM ZOMBIE ARM
                        // BALANCING GUIDE: Damage proyektil disetel sebesar 18
                        int p = Projectile.NewProjectile(
                            npc.GetSource_FromAI(), 
                            npc.Center, 
                            shootVel, 
                            ModContent.ProjectileType<ZombieArmProjectile>(), 
                            18, 
                            1f, 
                            Main.myPlayer
                        );

                        if (p != Main.maxProjectiles)
                        {
                            Main.projectile[p].netUpdate = true;
                        }

                        // Suara erangan zombie marah saat melemparkan senjatanya
                        SoundEngine.PlaySound(SoundID.NPCDeath2, npc.Center);

                        // Efek debu partikel cipratan darah di tangan zombie saat melempar kuat
                        for (int i = 0; i < 6; i++)
                        {
                            Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Blood, 0f, 0f, 100, default, 1.1f);
                            d.velocity = Main.rand.NextVector2Circular(3f, 3f);
                        }

                        npc.netUpdate = true;
                    }
                }
            }
        }
    }
}