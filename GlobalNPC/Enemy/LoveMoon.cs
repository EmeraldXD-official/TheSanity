using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using System;

namespace TheSanity
{
    public class LoveMoon : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int lovePotionTimer = 0;
        private int waterComboTimer = 0;
        private int doveSpawnCooldown = 0; 
        private bool forcedPartnerSpawn = false;

        public override bool AppliesToEntity(NPC npc, bool lateInstantiation)
        {
            return npc.type == NPCID.TheBride || npc.type == NPCID.TheGroom;
        }

        public override void PostAI(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int partnerType = (npc.type == NPCID.TheGroom) ? NPCID.TheBride : NPCID.TheGroom;
            NPC partner = FindLivingPartner(partnerType);

            // =========================================================
            // MEKANIK 1: DUAL SPAWN & LIMITER SYSTEM
            // =========================================================
            if (!forcedPartnerSpawn && partner == null)
            {
                forcedPartnerSpawn = true;

                if (CountNPC(NPCID.TheBride) + CountNPC(NPCID.TheGroom) < 2)
                {
                    int pIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, partnerType);
                    if (pIndex != Main.maxNPCs)
                    {
                        Main.npc[pIndex].target = npc.target;
                        Main.npc[pIndex].netUpdate = true;
                    }
                }
                else
                {
                    npc.active = false;
                    return;
                }
            }

            // =========================================================
            // MEKANIK 2 & 3: SMOOTH PULL & DISTANCE KEEPING
            // =========================================================
            float maxElasticDistance = 160f; 
            float idealKeepDistance = 40f;  

            if (partner != null && partner.active)
            {
                float currentDistance = Math.Abs(npc.Center.X - partner.Center.X);

                if (npc.type == NPCID.TheGroom)
                {
                    partner.direction = npc.direction;
                    partner.spriteDirection = npc.spriteDirection;
                    partner.ai[0] = npc.ai[0];

                    if (currentDistance < idealKeepDistance)
                    {
                        float pushDirection = (partner.Center.X > npc.Center.X) ? 1f : -1f;
                        partner.velocity.X = npc.velocity.X + (pushDirection * 0.5f);
                    }
                    else
                    {
                        partner.velocity.X = npc.velocity.X;
                    }

                    if (currentDistance > maxElasticDistance)
                    {
                        float targetX = npc.Center.X + (npc.direction == -1 ? -idealKeepDistance : idealKeepDistance);
                        partner.Center = new Vector2(MathHelper.Lerp(partner.Center.X, targetX, 0.1f), partner.Center.Y);
                        partner.netUpdate = true;
                    }
                }
            }

            // =========================================================
            // MEKANIK 4: HUJAN 5 BOTOL LOVE POTION
            // =========================================================
            lovePotionTimer++;
            if (lovePotionTimer >= 180) 
            {
                lovePotionTimer = 0;

                for (int i = 0; i < 5; i++)
                {
                    float randomVelX = Main.rand.NextFloat(-5f, 6f);
                    float randomVelY = Main.rand.NextFloat(-11f, -6f); 

                    int proj = Projectile.NewProjectile(
                        npc.GetSource_FromAI(), 
                        npc.Top, 
                        new Vector2(randomVelX, randomVelY), 
                        ProjectileID.LovePotion, 
                        0, 
                        0f, 
                        Main.myPlayer
                    );

                    if (proj != Main.maxProjectiles)
                    {
                        Main.projectile[proj].hostile = true;  
                        Main.projectile[proj].friendly = false;
                        Main.projectile[proj].netUpdate = true;
                    }
                }
                SoundEngine.PlaySound(SoundID.Item106, npc.Center);
            }

            // =========================================================
            // REVISI MEKANIK 5: TIMING COMBO WATER & REVIVE DIUBAH KE 20 DETIK
            // =========================================================
            if (npc.type == NPCID.TheGroom)
            {
                waterComboTimer++;
                // 20 Detik = 20 * 60 frame = 1200 frame
                // [WATER COMBO COOLDOWN BALANCING LOCATION]
                if (waterComboTimer >= 1200) 
                {
                    waterComboTimer = 0;

                    if (partner == null || !partner.active)
                    {
                        int spawnedBrideIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X - 30, (int)npc.Center.Y, NPCID.TheBride);
                        if (spawnedBrideIndex != Main.maxNPCs)
                        {
                            Main.npc[spawnedBrideIndex].target = npc.target;
                            Main.npc[spawnedBrideIndex].netUpdate = true;
                            partner = Main.npc[spawnedBrideIndex]; 
                        }

                        for (int k = 0; k < 20; k++) Dust.NewDust(npc.Center, 20, 20, DustID.Blood, 0f, -3f);
                    }

                    Vector2 unholyVelocity = new Vector2(-6f, -4f);
                    Vector2 bloodVelocity = new Vector2(6f, -4f);

                    int pUnholy = Projectile.NewProjectile(npc.GetSource_FromAI(), partner.Top, unholyVelocity, ProjectileID.UnholyWater, 22, 1f, Main.myPlayer);
                    if (pUnholy != Main.maxProjectiles)
                    {
                        Main.projectile[pUnholy].hostile = true; 
                        Main.projectile[pUnholy].friendly = false;
                    }

                    int pBlood = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Top, bloodVelocity, ProjectileID.BloodWater, 22, 1f, Main.myPlayer);
                    if (pBlood != Main.maxProjectiles)
                    {
                        Main.projectile[pBlood].hostile = true; 
                        Main.projectile[pBlood].friendly = false;
                    }

                    SoundEngine.PlaySound(SoundID.Item107, npc.Center);
                }
            }
            else if (npc.type == NPCID.TheBride && (partner == null || !partner.active))
            {
                waterComboTimer++;
                // Jeda 20 detik juga untuk sang Istri menghidupkan suaminya
                if (waterComboTimer >= 1200)
                {
                    waterComboTimer = 0;

                    int spawnedGroomIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X + 30, (int)npc.Center.Y, NPCID.TheGroom);
                    if (spawnedGroomIndex != Main.maxNPCs)
                    {
                        Main.npc[spawnedGroomIndex].target = npc.target;
                        Main.npc[spawnedGroomIndex].netUpdate = true;
                    }

                    for (int k = 0; k < 20; k++) Dust.NewDust(npc.Center, 20, 20, DustID.Blood, 0f, -3f);
                }
            }

            // =========================================================
            // REVISI MEKANIK 6: SPAWN HANYA 1 BURUNG MERPATI (400 HP)
            // =========================================================
            if (npc.type == NPCID.TheGroom && partner != null && partner.active)
            {
                bool isAnyDoveAlive = NPC.AnyNPCs(ModContent.NPCType<LoveDoveNPC>());

                if (isAnyDoveAlive)
                {
                    doveSpawnCooldown = 0; 
                }
                else
                {
                    doveSpawnCooldown++; 

                    if (doveSpawnCooldown >= 300) 
                    {
                        doveSpawnCooldown = 0;

                        // Cukup spawn 1 ekor saja di tengah-tengah antara Groom dan Bride
                        float spawnX = (npc.Center.X + partner.Center.X) / 2f;
                        int b1 = NPC.NewNPC(npc.GetSource_FromAI(), (int)spawnX, (int)npc.Center.Y - 30, ModContent.NPCType<LoveDoveNPC>());
                        
                        if (b1 != Main.maxNPCs) Main.npc[b1].netUpdate = true;

                        SoundEngine.PlaySound(SoundID.Item24, npc.Center); 
                    }
                }
            }
        }

        private NPC FindLivingPartner(int partnerNpcId)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC targetNpc = Main.npc[i];
                if (targetNpc.active && targetNpc.type == partnerNpcId)
                {
                    return targetNpc;
                }
            }
            return null;
        }

        private int CountNPC(int npcId)
        {
            int count = 0;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == npcId)
                {
                    count++;
                }
            }
            return count;
        }
    }
}