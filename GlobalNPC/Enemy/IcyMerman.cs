using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity.GlobalNPC.Enemy
{
    public class IcyMermanRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // --- VARIABEL KONTROL ICY MERMAN ---
        public float distanceWalked = 0f;      
        public int auraTimer = 0;              
        public int cooldownTimer = 0;          
        public bool isChargingAura = true;    

        public override bool PreAI(NPC npc)
        {
            if (npc.type != NPCID.IcyMerman) return true;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            // ------------------------------------------------------------------------
            // 1. LOGIKA JEJAK KAKI & JUMP: SPAWN PROJECTILE 263
            // ------------------------------------------------------------------------
            if (npc.velocity.X != 0f) 
            {
                distanceWalked += Math.Abs(npc.velocity.X);

                if (distanceWalked >= 16f)
                {
                    distanceWalked = 0f; 

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 footPos = new Vector2(npc.Center.X, npc.position.Y + npc.height - 8f);
                        
                        // ====================================================
                        // [BALANCING] LOKASI REWORK DAMAGE JEJAK KAKI ES
                        // ====================================================
                        int iceDamage = 20; // Silakan ganti angka 20 ini sesukamu, Ky!

                        // Pembagian otomatis biar di Expert/Master damagenya ga meledak konyol
                        if (Main.expertMode) iceDamage = (int)(iceDamage / (Main.masterMode ? 6f : 4f));

                        int p = Projectile.NewProjectile(npc.GetSource_FromAI(), footPos, Vector2.Zero, 263, iceDamage, 0f, Main.myPlayer);
                        
                        if (p != Main.maxProjectiles)
                        {
                            Main.projectile[p].friendly = false;
                            Main.projectile[p].hostile = true; 
                            
                            // [BALANCING] LOKASI LIFETIME JEJAK ES (180 frame = 3 detik)
                            Main.projectile[p].timeLeft = 180; 
                        }
                    }
                }
            }

            // ------------------------------------------------------------------------
            // 2. LOGIKA AURA ES MENYUSUT & SERANGAN BLIZZARD CLOUD DARI MERMAN
            // ------------------------------------------------------------------------
            if (target != null && !target.dead && npc.HasValidTarget)
            {
                if (isChargingAura)
                {
                    auraTimer++;

                    float currentRadius = MathHelper.Lerp(200f, 0f, (float)auraTimer / 300f);

                    for (int i = 0; i < 4; i++) 
                    {
                        double angle = Main.rand.NextDouble() * Math.PI * 2d;
                        Vector2 offset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * currentRadius;
                        
                        Dust d = Dust.NewDustDirect(npc.Center + offset, 0, 0, DustID.IceTorch, 0f, 0f, 100, default, 1.2f);
                        d.noGravity = true;
                        d.velocity = Vector2.Zero; 
                    }

                    if (auraTimer >= 300)
                    {
                        auraTimer = 0;          
                        isChargingAura = false; 

                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            Vector2 spawnPos = npc.Center; 

                            // ====================================================
                            // [BALANCING] LOKASI REWORK DAMAGE BLIZZARD CLOUD
                            // ====================================================
                            int targetCloudDamage = 25; // Ganti angka ini buat nentuin damage awannya!

                            // tModLoader 1.4.4 ke atas mewajibkan pembagian ini untuk proyektil hostile dari NPC
                            if (Main.expertMode) 
                            {
                                targetCloudDamage = (int)(targetCloudDamage / (Main.masterMode ? 6f : 4f));
                            }

                            Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, Vector2.Zero, ModContent.ProjectileType<BlizzardCloud>(), targetCloudDamage, 0f, Main.myPlayer);
                        }

                        Terraria.Audio.SoundEngine.PlaySound(SoundID.Item121, npc.Center);
                        npc.netUpdate = true;
                    }
                }
                else
                {
                    cooldownTimer++;
                    if (cooldownTimer >= 120)
                    {
                        cooldownTimer = 0;
                        isChargingAura = true; 
                        npc.netUpdate = true;
                    }
                }
            }

            return true; 
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            if (npc.type == NPCID.IcyMerman)
            {
                target.AddBuff(BuffID.Frostburn, 180); 
            }
        }
    }
}