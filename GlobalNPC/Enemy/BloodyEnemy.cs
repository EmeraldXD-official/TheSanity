using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using System; // Dibutuhkan untuk Math.Max bawaan C#

namespace TheSanity
{
    public class BloodyEnemy : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(NPC npc, bool lateInstantiation)
        {
            return npc.type == NPCID.BloodZombie || npc.type == NPCID.Drippler;
        }

        public override void OnKill(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int totalMinions = Main.rand.Next(5, 16);
            SoundEngine.PlaySound(SoundID.NPCDeath21, npc.Center);

            bool spawnSelfDuplicate = Main.rand.NextBool(20);

            // =========================================================
            // LOGIKA 1: BLOOD ZOMBIE SYSTEM
            // =========================================================
            if (npc.type == NPCID.BloodZombie)
            {
                if (spawnSelfDuplicate)
                {
                    // --- FIX ERROR CS0266 HERE ---
                    // Menggunakan explicit cast (int) agar hasil MathHelper.Max berubah jadi integer
                    totalMinions = (int)MathHelper.Max(1f, totalMinions - 1);

                    SpawnBloodMinion(npc, NPCID.BloodZombie);
                }

                for (int i = 0; i < totalMinions; i++)
                {
                    SpawnBloodMinion(npc, NPCID.Zombie);
                }
            }
            // =========================================================
            // LOGIKA 2: DRIPPLER SYSTEM
            // =========================================================
            else if (npc.type == NPCID.Drippler)
            {
                if (spawnSelfDuplicate)
                {
                    // --- FIX ERROR CS0266 HERE TOO ---
                    // Menggunakan explicit cast (int) yang sama untuk mencegah error compile
                    totalMinions = (int)MathHelper.Max(1f, totalMinions - 1);

                    SpawnBloodMinion(npc, NPCID.Drippler);
                }

                for (int i = 0; i < totalMinions; i++)
                {
                    SpawnBloodMinion(npc, NPCID.DemonEye);
                }
            }
        }

        private void SpawnBloodMinion(NPC parentNPC, int npcTypeToSpawn)
        {
            Vector2 spawnPos = parentNPC.Center + new Vector2(Main.rand.Next(-15, 16), Main.rand.Next(-15, 16));
            int newNPCIndex = NPC.NewNPC(parentNPC.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y, npcTypeToSpawn);

            if (newNPCIndex != Main.maxNPCs)
            {
                NPC minion = Main.npc[newNPCIndex];
                minion.target = parentNPC.target;

                if (minion.noGravity)
                {
                    Vector2 launchVelocity = Main.rand.NextVector2Circular(5f, 5f);
                    minion.velocity = launchVelocity;
                    
                    for (int j = 0; j < 4; j++)
                    {
                        Dust.NewDust(minion.position, minion.width, minion.height, DustID.CrimsonTorch, launchVelocity.X * 0.5f, launchVelocity.Y * 0.5f);
                    }
                }
                else
                {
                    float launchX = Main.rand.NextFloat(-6f, 6f);
                    float launchY = Main.rand.NextFloat(-8f, -3f);
                    minion.velocity = new Vector2(launchX, launchY);

                    for (int j = 0; j < 4; j++)
                    {
                        Dust.NewDust(minion.position, minion.width, minion.height, DustID.Blood, launchX * 0.5f, launchY * 0.5f);
                    }
                }

                minion.netUpdate = true;
            }
        }
    }
}