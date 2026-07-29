using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class GraniteGolemRework : global::Terraria.ModLoader.GlobalNPC
    {
        // InstancePerEntity harus true agar setiap Golem punya timer sendiri-sendiri
        public override bool InstancePerEntity => true;

        private int chargeTimer = 0;
        private int totalProjectiles = 0;
        private int cooldownTimer = 0;
        private bool isFusing = false;

        public override void PostAI(NPC npc)
        {
            if (npc.type != NPCID.GraniteGolem) return;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead) 
            {
                // Reset jika player mati/hilang agar tidak nge-bug pas spawn lagi
                totalProjectiles = 0;
                isFusing = false;
                return;
            }

            // Jalankan cooldown
            if (cooldownTimer > 0)
            {
                cooldownTimer--;
                return;
            }

            // --- 1. LOGIKA CHARGING (MENGUMPULKAN BOLA) ---
            if (totalProjectiles < 5 && !isFusing)
            {
                chargeTimer++;
                
                // Efek visual dust saat sedang charging (biar kelihatan dia lagi kerja)
                if (chargeTimer % 10 == 0)
                {
                    Dust.NewDustDirect(npc.position, npc.width, npc.height, DustID.Granite, 0, -2f, 100, default, 1f);
                }

                if (chargeTimer >= 45) 
                {
                    // Gunakan Main.myPlayer untuk Single Player dan Netmode check untuk Multiplayer
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // LOKASI SPEED & DAMAGE ORBIT: Damage 15
                        int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<GraniteOrbitOrb>(), 15, 1f, Main.myPlayer, npc.whoAmI, totalProjectiles);
                        
                        // Sinkronisasi ke server jika di multiplayer
                        if (Main.netMode == NetmodeID.Server)
                        {
                            NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, p);
                        }
                    }
                    totalProjectiles++;
                    chargeTimer = 0;
                }
            }
            // --- 2. TRANSISI KE FUSION ---
            else if (totalProjectiles >= 5 && !isFusing)
            {
                isFusing = true;
                chargeTimer = 0; // Reset timer untuk durasi fusion
            }

            // --- 3. LOGIKA FUSION (MENEMBAK) ---
            if (isFusing)
            {
                chargeTimer++;

                // Efek suara/visual saat bola-bola menyatu
                if (chargeTimer == 1)
                {
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item15, npc.Center); 
                }

                if (chargeTimer >= 60) // Tunggu 1 detik biar bola menyatu di tengah
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 shootVel = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY) * 10f;
                        
                        // LOKASI DAMAGE BIG BLAST: 30 (90 di Master Mode)
                        int pBig = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, ModContent.ProjectileType<GraniteBigBlast>(), 30, 2f, Main.myPlayer);
                        
                        if (Main.netMode == NetmodeID.Server)
                        {
                            NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, pBig);
                        }
                    }

                    // Reset semua state setelah menembak
                    totalProjectiles = 0;
                    isFusing = false;
                    chargeTimer = 0;
                    cooldownTimer = 180; // Cooldown 3 detik
                    
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item62, npc.Center); // Suara tembakan
                }
            }
        }
    }
}