using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using TheSanity.Projectiles;

namespace TheSanity.GlobalNPC.Enemy
{
    public class ClownRework : global::Terraria.ModLoader.GlobalNPC
    {
        private int teethBombTimer = 0;

        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC npc, bool lateInstantiation)
        {
            return npc.type == NPCID.Clown;
        }

        public override void PostAI(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // =========================================================
            // MEKANIK BARU: SPAWN NPC GIGI PALSU BERJALAN (EVERY 3 SECONDS)
            // =========================================================
            teethBombTimer++;

            // 3 Detik = 180 frame
            // [TEETH BOMB COOLDOWN BALANCING LOCATION]
            if (teethBombTimer >= 180)
            {
                teethBombTimer = 0;

                // Mengeluarkan 3 hingga 4 NPC Gigi Palsu sekaligus
                // [TEETH BOMB COUNT BALANCING LOCATION]
                int spawnCount = Main.rand.Next(3, 5); 

                for (int i = 0; i < spawnCount; i++)
                {
                    // --- FIX TYPE NPC CALL ---
                    // Menggunakan NPC.NewNPC untuk memanggil ID 378 (Chattering Teeth Bomb NPC)
                    int teethNPCIndex = NPC.NewNPC(
                        npc.GetSource_FromAI(),
                        (int)npc.Center.X,
                        (int)npc.Center.Y,
                        378 // ID NPC Chattering Teeth Bomb vanilla
                    );

                    if (teethNPCIndex != Main.maxNPCs)
                    {
                        NPC teeth = Main.npc[teethNPCIndex];
                        
                        // Beri dorongan acak agar gigi-giginya melompat menyebar dari tubuh Clown
                        float launchX = Main.rand.NextFloat(-4f, 5f);
                        float launchY = Main.rand.NextFloat(-6f, -2f);
                        teeth.velocity = new Vector2(launchX, launchY);
                        
                        // Targetkan langsung ke player yang sedang dilawan Clown
                        teeth.target = npc.target; 
                        teeth.netUpdate = true;
                    }
                }
            }
        }

        // =========================================================
        // MEKANIK MATI: MEMUNTAHKAN 10-15 HAPPY BOMB NUKLIR
        // =========================================================
        public override void OnKill(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // [DEATH BOMB COUNT BALANCING LOCATION]
            int bombCount = Main.rand.Next(10, 16); 

            for (int i = 0; i < bombCount; i++)
            {
                float launchVelocityX = Main.rand.NextFloat(-7f, 8f);
                float launchVelocityY = Main.rand.NextFloat(-12f, -4f); 
                Vector2 randomVelocity = new Vector2(launchVelocityX, launchVelocityY);

                int spawnedBomb = Projectile.NewProjectile(
                    npc.GetSource_Death(),
                    npc.Center,
                    randomVelocity,
                    ModContent.ProjectileType<ClownHappyBomb>(),
                    40, 
                    5f,
                    Main.myPlayer
                );

                if (spawnedBomb != Main.maxProjectiles)
                {
                    Main.projectile[spawnedBomb].netUpdate = true;
                }
            }
        }
    }
}