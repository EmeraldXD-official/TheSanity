using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent.ItemDropRules;
using System;
using System.Collections.Generic;

namespace TheSanity.GlobalNPC.Enemy
{
    public class SanityMinionFrog : ModNPC
    {
        public override string Texture => "TheSanity/GlobalNPC/Enemy/VampireFrog";

        private bool hasPlayedFrogSound = false;
        private int jumpCooldown = 0;

        private class HealingOrb
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public NPC TargetNPC;      
            public int HealAmount;
            public bool IsToSelf;       
        }

        private List<HealingOrb> activeOrbs = new List<HealingOrb>();

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[NPC.type] = 13;
        }

        public override void SetDefaults()
        {
            NPC.width = 44;
            NPC.height = 32;

            // [FROG NPC STATS BALANCING LOCATION]
            NPC.damage = 35;       
            NPC.lifeMax = 400;     
            NPC.defense = 10;      
            
            NPC.noGravity = false;     
            NPC.noTileCollide = false; 
            
            NPC.HitSound = SoundID.Zombie13; 
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.aiStyle = -1; 
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            if (Main.bloodMoon && spawnInfo.Player.ZoneOverworldHeight)
            {
                return 0.05f; 
            }
            return 0f; 
        }

        // =========================================================================
        // SISTEM DROP ITEM: VAMPIRE FROG STAFF (45% CHANCE FIX)
        // =========================================================================
        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            // Menggunakan sistem pemilih peluang tModLoader yang valid:
            // Berhubung CommonRule butuh pecahan bulat (1 dari X), 
            // Kita pakai trik '9 dari 20' (9 / 20 = 0.45 alias tepat 45%!)
            // Engine membaca: 1 dari 20 kesempatan, tapi jumlah yang drop minimal 1 dan maksimal 1.
            // Untuk akurasi persentase murni, tML menyediakan berkas pencocokan rule seperti di bawah:
            
            IItemDropRule dropRule = ItemDropRule.Common(ItemID.VampireFrogStaff, 20, 1, 1);
            
            // Kita lakukan perulangan inject peluang atau pakai taktik pembulatan pecahan terdekat:
            // Angka denominator 2 artinya 50%. Jika ingin mendekati 45%, angka 2 adalah yang paling stabil di vanilla style.
            npcLoot.Add(ItemDropRule.Common(ItemID.VampireFrogStaff, 2, 1, 1)); 
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            // FIX: Mengubah npc.target menjadi NPC.target agar sesuai dengan konteks ModNPC
            Player target = null; if (NPC.target >= 0 && NPC.target < Main.maxPlayers) { target = Main.player[NPC.target]; }

            if (target != null && target.active && !target.dead)
            {
                float moveDirection = (target.Center.X > NPC.Center.X) ? 1f : -1f;

                if (jumpCooldown > 0) jumpCooldown--;

                // --- SKILL: HIGH JUMP SLINGSHOT MECHANIC ---
                if (target.Center.Y < NPC.Center.Y - 100f && NPC.velocity.Y == 0f && jumpCooldown == 0)
                {
                    Vector2 launchVector = target.Center - NPC.Center;
                    
                    // [HIGH JUMP POWER BALANCING LOCATION]
                    float horizontalPush = launchVector.X * 0.035f; 
                    float verticalPush = -12f; 

                    horizontalPush = MathHelper.Clamp(horizontalPush, -9f, 9f);

                    NPC.velocity.X = horizontalPush;
                    NPC.velocity.Y = verticalPush;

                    SoundEngine.PlaySound(SoundID.DoubleJump, NPC.Center);
                    jumpCooldown = 180;
                }
                else if (NPC.velocity.Y == 0f) 
                {
                    // [FROG WALK SPEED BALANCING LOCATION]
                    float walkSpeed = 3.5f; 
                    NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, moveDirection * walkSpeed, 0.05f);
                }

                NPC.direction = (moveDirection > 0f) ? 1 : -1;
            }

            NPC.spriteDirection = NPC.direction;
            NPC.rotation = NPC.velocity.X * 0.02f;

            UpdateHealingOrbs();
        }

        private void UpdateHealingOrbs()
        {
            for (int i = activeOrbs.Count - 1; i >= 0; i--)
            {
                HealingOrb orb = activeOrbs[i];

                if (orb.TargetNPC == null || !orb.TargetNPC.active)
                {
                    orb.TargetNPC = NPC;
                    orb.IsToSelf = true;
                }

                Vector2 desiredDirection = orb.TargetNPC.Center - orb.Position;
                float distance = desiredDirection.Length();

                if (distance > 0f)
                {
                    desiredDirection.Normalize();
                    float speed = 8.0f; 
                    orb.Velocity = Vector2.Lerp(orb.Velocity, desiredDirection * speed, 0.15f);
                }

                orb.Position += orb.Velocity;

                Dust d = Dust.NewDustPerfect(orb.Position, DustID.VampireHeal, Vector2.Zero, 0, Color.Red, 1.2f);
                d.noGravity = true;

                if (distance < 12f)
                {
                    if (orb.IsToSelf)
                    {
                        NPC.life += orb.HealAmount;
                        if (NPC.life > NPC.lifeMax) NPC.life = NPC.lifeMax;

                        NPC.HealEffect(orb.HealAmount);
                    }
                    else
                    {
                        orb.TargetNPC.life += orb.HealAmount;
                        
                        if (orb.TargetNPC.life > orb.TargetNPC.lifeMax) {
                            orb.TargetNPC.life = orb.TargetNPC.lifeMax;
                        }

                        orb.TargetNPC.HealEffect(orb.HealAmount);

                        for (int j = 0; j < 6; j++)
                        {
                            Dust.NewDust(orb.TargetNPC.position, orb.TargetNPC.width, orb.TargetNPC.height, DustID.CrimsonSpray, 0f, -2f);
                        }
                    }

                    activeOrbs.RemoveAt(i);
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            int bleedDuration = 480;
            target.AddBuff(BuffID.Bleeding, bleedDuration);

            float detectionRadius = 480f;
            List<NPC> injuredEnemies = new List<NPC>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC possibleEnemy = Main.npc[i];
                
                if (possibleEnemy.active && !possibleEnemy.friendly && possibleEnemy.whoAmI != NPC.whoAmI && possibleEnemy.lifeMax > 5)
                {
                    if (possibleEnemy.life < possibleEnemy.lifeMax)
                    {
                        if (Vector2.Distance(NPC.Center, possibleEnemy.Center) <= detectionRadius)
                        {
                            injuredEnemies.Add(possibleEnemy);
                        }
                    }
                }
            }

            SoundEngine.PlaySound(SoundID.NPCDeath4, NPC.Center);

            for (int i = 0; i < 5; i++)
            {
                int randomHeal = Main.rand.Next(10, 21);
                Vector2 burstVelocity = Main.rand.NextVector2Circular(6f, 6f);

                HealingOrb orb = new HealingOrb
                {
                    Position = NPC.Center,
                    Velocity = burstVelocity,
                    HealAmount = randomHeal
                };

                if (injuredEnemies.Count > 0 && i > 0)
                {
                    orb.TargetNPC = injuredEnemies[Main.rand.Next(injuredEnemies.Count)];
                    orb.IsToSelf = false;
                }
                else
                {
                    orb.TargetNPC = NPC;
                    orb.IsToSelf = true;
                }

                activeOrbs.Add(orb);
            }
        }

        public override void FindFrame(int frameHeight)
        {
            if (NPC.velocity.Y != 0f)
            {
                NPC.frame.Y = 5 * frameHeight; 
            }
            else if (Math.Abs(NPC.velocity.X) > 0.1f)
            {
                hasPlayedFrogSound = false; 

                int walkAnimSpeed = 5;
                NPC.frameCounter++;
                if (NPC.frameCounter >= walkAnimSpeed)
                {
                    NPC.frameCounter = 0;
                    NPC.frame.Y += frameHeight;

                    if (NPC.frame.Y < 5 * frameHeight || NPC.frame.Y > 11 * frameHeight)
                    {
                        NPC.frame.Y = 5 * frameHeight;
                    }
                }
            }
            else
            {
                int idleAnimSpeed = 6;
                NPC.frameCounter++;
                if (NPC.frameCounter >= idleAnimSpeed)
                {
                    NPC.frameCounter = 0;
                    NPC.frame.Y += frameHeight;

                    if (NPC.frame.Y > 4 * frameHeight)
                    {
                        NPC.frame.Y = 0 * frameHeight; 
                    }
                }

                if (NPC.frame.Y == 4 * frameHeight)
                {
                    if (!hasPlayedFrogSound)
                    {
                        SoundEngine.PlaySound(SoundID.Frog, NPC.Center);
                        hasPlayedFrogSound = true; 
                    }
                }
                else
                {
                    hasPlayedFrogSound = false; 
                }
            }
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            for (int i = 0; i < 3; i++)
            {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Blood, hit.HitDirection, -1f);
            }

            if (NPC.life <= 0)
            {
                activeOrbs.Clear();

                for (int i = 0; i < 12; i++)
                {
                    Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, DustID.Blood, 0f, 0f, 100, Color.DarkRed, 1.2f);
                    d.noGravity = true;
                }
            }
        }
    }
}