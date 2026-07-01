using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class MossHornetRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer utama untuk siklus serangan 3 detik
        public int attackTimer = 0;
        
        // Menyimpan status apakah Hornet sedang dalam mode rentetan stinger
        public bool isBarraging = false;
        public int barrageCount = 0;
        public int barrageDelay = 0;
        
        // Arah sapuan: true = dari kiri ke kanan, false = dari kanan ke kiri
        public bool sweepDirection = false;

        public override bool PreAI(NPC npc)
        {
            // LOKASI ID TARGET: Moss Hornet beserta seluruh varian ID kustom/kembarannya
            if (npc.type == NPCID.MossHornet || npc.type == 176 || npc.type == -18 || npc.type == -19 || npc.type == -20 || npc.type == -21)
            {
                // BLOKIR TEMBAKAN VANILLA: Menyetel ulang timer tembak bawaan AI Hornet agar tidak bentrok
                npc.ai[1] = 0f;

                // FIX FATAL: Pengecekan target yang aman agar tidak NullReferenceException
                if (npc.target < 0 || npc.target >= Main.maxPlayers || !Main.player[npc.target].active || Main.player[npc.target].dead)
                {
                    return true;
                }
                Player target = Main.player[npc.target];

                // --- 1. SIKLUS UTAMA SERANGAN (TIAP 3 DETIK) ---
                if (!isBarraging)
                {
                    attackTimer++;

                    // LOKASI TIMING ATTACK: 180 frame = 3 Detik
                    if (attackTimer >= 180)
                    {
                        isBarraging = true;
                        attackTimer = 0;
                        barrageCount = 0;
                        barrageDelay = 0;
                        
                        sweepDirection = Main.rand.NextBool();

                        // LOKASI EFEK GLOW KUNING SEKELIBAT
                        for (int i = 0; i < 45; i++) 
                        {
                            Vector2 speed = Main.rand.NextVector2Circular(6f, 6f); 
                            Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.YellowTorch, speed.X, speed.Y, 50, default, 1.8f);
                            d.noGravity = true;
                            d.velocity *= 1.4f; 
                        }
                        
                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item15, npc.Center);
                    }
                }

                // --- 2. LOGIKA KETIKA BARRAGE AKTIF ---
                if (isBarraging)
                {
                    // FORCE DIAM: Paksa Hornet berhenti bergerak
                    npc.velocity = Vector2.Zero;

                    if (Main.rand.NextBool(2))
                    {
                        Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.YellowTorch, 0f, 0f, 100, default, 1.2f);
                        d.noGravity = true;
                    }

                    barrageDelay++;
                    if (barrageDelay >= 4)
                    {
                        barrageDelay = 0;

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            float startAngle = sweepDirection ? -0.4f : 0.4f;
                            float endAngle = sweepDirection ? 0.4f : -0.4f;
                            
                            float progress = (float)barrageCount / 9f; 
                            float currentOffset = MathHelper.Lerp(startAngle, endAngle, progress);

                            Vector2 fireVelocity = target.Center - npc.Center;
                            fireVelocity.Normalize();
                            
                            float stingerSpeed = 7.5f;
                            fireVelocity = fireVelocity.RotatedBy(currentOffset) * stingerSpeed;

                            int proj = Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                npc.Center,
                                fireVelocity,
                                ProjectileID.HornetStinger,
                                22,
                                1f,
                                Main.myPlayer
                            );

                            if (proj < Main.maxProjectiles)
                            {
                                Main.projectile[proj].hostile = true;
                                Main.projectile[proj].friendly = false;
                                Main.projectile[proj].localAI[0] = 999f; 
                            }
                        }

                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item17, npc.Center);
                        
                        barrageCount++;
                        if (barrageCount >= 10)
                        {
                            isBarraging = false;
                        }
                    }

                    Lighting.AddLight(npc.Center, 0.9f, 0.7f, 0.1f); 
                    return false; 
                }
            }
            return true;
        }
    }

    // --- 3. GLOBAL PROJECTILE UNTUK MENYUNTIKKAN DEBUFF ---
    public class MossHornetProjectileMod : GlobalProjectile
    {
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (projectile.type == ProjectileID.HornetStinger && projectile.localAI[0] == 999f)
            {
                target.AddBuff(70, 180);
            }
        }
    }
}