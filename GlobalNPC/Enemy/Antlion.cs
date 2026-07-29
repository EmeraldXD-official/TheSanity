using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class AntlionRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int shootTimer = 0;

        public override void PostAI(NPC npc)
        {
            if (npc.type != NPCID.Antlion) return;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead || npc.Distance(target.Center) > 800f)
            {
                shootTimer = 0;
                return;
            }

            shootTimer++;

            // --- LOGIKA SHOTGUN MUNTAH (Setiap 5 Detik) ---
            if (shootTimer >= 300)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // Arah ke player
                    Vector2 baseVelocity = (target.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                    
                    for (int i = 0; i < 10; i++)
                    {
                        // LOKASI SPREAD: 20 derajat
                        // LOKASI SPEED: 14f (Ditingkatkan supaya jauh meluncurnya)
                        Vector2 perturbedSpeed = baseVelocity.RotatedByRandom(MathHelper.ToRadians(20)) * 14f; 

                        // LOKASI DAMAGE: 12
                        int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, perturbedSpeed, 31, 12, 1f, Main.myPlayer);
                        
                        // Sedikit manipulasi agar tidak langsung jatuh ke tanah
                        if (Main.projectile[p].type == 31) {
                            Main.projectile[p].velocity.Y -= 2f; // Kasih dorongan ke atas sedikit biar melambung jauh
                        }

                        if (Main.netMode == NetmodeID.Server)
                            NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, p);
                    }
                }

                // Efek visual muntahan pasir
                for (int j = 0; j < 15; j++)
                {
                    Dust d = Dust.NewDustDirect(npc.Center, 0, 0, DustID.Sand, 0, 0, 100, default, 1.2f);
                    d.velocity = (target.Center - npc.Center).SafeNormalize(Vector2.Zero).RotatedByRandom(0.5f) * 6f;
                    d.noGravity = false;
                }

                // LOKASI SOUND: Menggunakan suara muntah (NPCDeath13)
                Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath13, npc.Center); 
                
                shootTimer = 0;
            }
        }
    }
}