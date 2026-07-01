using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent.Bestiary;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.Utilities;

namespace TheSanity.GlobalNPC.TownNPCs
{
    [AutoloadHead]
    public class SunCultist : ModNPC
    {
        public override string Texture => "TheSanity/GlobalNPC/TownNPCs/SunCultist";

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 12;

            NPCID.Sets.AttackType[Type] = 1; // 1 = Ranged
            NPCID.Sets.DangerDetectRange[Type] = 600; 
            NPCID.Sets.AttackAverageChance[Type] = 1;
            
            NPCID.Sets.ActsLikeTownNPC[Type] = true;
            NPCID.Sets.SpawnsWithCustomName[Type] = true;
        }

        public override void SetDefaults() {
            NPC.townNPC = true;
            NPC.friendly = true;
            NPC.width = 18;
            NPC.height = 40;
            NPC.aiStyle = 7; 
            NPC.lifeMax = 3000;
            NPC.defense = 40;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.knockBackResist = 0.4f;
            AnimationType = -1; 
        }

        public override List<string> SetNPCNameList() {
            return new List<string>() { "Sol", "Helios", "Ra", "Phaeton", "Ignis" };
        }

        // =========================================================================
        // SYARAT SPAWN: NPC hanya akan datang jika SALAH SATU boss awal ini sudah mati
        // =========================================================================
        public override bool CanTownNPCSpawn(int numTownNPCs) {
            return NPC.downedSlimeKing      // King Slime
                || NPC.downedBoss1          // Eye of Cthulhu
                || NPC.downedBoss2          // Eater of Worlds / Brain of Cthulhu
                || NPC.downedQueenBee       // Queen Bee
                || NPC.downedBoss3          // Skeletron
                || NPC.downedDeerclops;     // Deerclops
        }

        public override void FindFrame(int frameHeight) {
            if (Main.dedServ) return;

            // FIX 1: Selalu samakan arah visual sprite dengan arah pergerakan AI 
            NPC.spriteDirection = NPC.direction;

            // FIX 2: Cek apakah player lokal sedang mengajak NPC ini berbicara
            bool isTalkingToPlayer = Main.player[Main.myPlayer].active && Main.player[Main.myPlayer].talkNPC == NPC.whoAmI;

            NPC targetEnemy = null;
            float closestDist = 600f;
            bool enemyDetected = false;

            // FIX 3: Hanya cari musuh jika NPC TIDAK sedang diajak bicara oleh player
            if (!isTalkingToPlayer) {
                for (int k = 0; k < Main.maxNPCs; k++) {
                    NPC target = Main.npc[k];
                    if (target.active && !target.friendly && target.damage > 0) {
                        float dist = Vector2.Distance(NPC.Center, target.Center);
                        if (dist < closestDist) {
                            closestDist = dist;
                            targetEnemy = target;
                            enemyDetected = true;
                        }
                    }
                }
            }

            if (enemyDetected && targetEnemy != null) {
                // ==========================================
                // MODE TEMPUR (Membidik Senjata ke Musuh)
                // ==========================================
                Vector2 shootDir = targetEnemy.Center - NPC.Center;
                NPC.direction = shootDir.X > 0 ? 1 : -1;
                NPC.spriteDirection = NPC.direction;

                if (NPC.direction == 1) {
                    shootDir.X = -shootDir.X;
                }

                float degrees = MathHelper.ToDegrees(shootDir.ToRotation());

                if (degrees > 55 && degrees < 115) {
                    NPC.frame.Y = 2 * frameHeight;
                } 
                else if (degrees >= 115 && degrees < 155) {
                    NPC.frame.Y = 3 * frameHeight;
                } 
                else if (degrees >= 155 || degrees <= -155) {
                    NPC.frame.Y = 4 * frameHeight;
                } 
                else if (degrees > -155 && degrees <= -115) {
                    NPC.frame.Y = 5 * frameHeight;
                } 
                else {
                    NPC.frame.Y = 6 * frameHeight;
                }
            }
            else {
                // ==========================================
                // MODE BIASA (Jalan, Diam, & Interaksi Player)
                // ==========================================
                if (NPC.velocity.Y != 0) {
                    NPC.frame.Y = 1 * frameHeight; // Frame melompat/jatuh
                } 
                else if (NPC.velocity.X == 0) {
                    NPC.frame.Y = 0 * frameHeight; // Frame diam
                } 
                else {
                    // Animasi berjalan
                    NPC.frameCounter += Math.Abs(NPC.velocity.X) * 0.25f;
                    if (NPC.frameCounter >= 5) {
                        NPC.frameCounter = 0;
                    }
                    int currentWalkFrame = 7 + (int)NPC.frameCounter;
                    NPC.frame.Y = currentWalkFrame * frameHeight;
                }
            }
        }

        public override string GetChat() {
            WeightedRandom<string> chat = new WeightedRandom<string>();
            chat.Add("The sun is exceptionally scorching today, absolutely perfect for purifying evil monsters.");
            chat.Add("Are you in need of war supplies? May the sacred flame be with you.");
            chat.Add("Rest easy; as long as I am here, darkness will never breach this village.");
            return chat;
        }

        public override void SetChatButtons(ref string button, ref string button2) {
            button = Language.GetTextValue("LegacyInterface.28"); 
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName) {
            if (firstButton) {
                shopName = "SunShop"; 
            }
        }

        public override void AddShops() {
            var npcShop = new NPCShop(Type, "SunShop");
            npcShop.Register();
        }

        public override void TownNPCAttackStrength(ref int damage, ref float knockback) {
            damage = 65;
            knockback = 4f;
        }

        public override void TownNPCAttackCooldown(ref int cooldown, ref int randExtraCooldown) {
            cooldown = 25;
            randExtraCooldown = 5;
        }

        public override void TownNPCAttackProj(ref int projType, ref int attackDelay) {
            projType = ModContent.ProjectileType<SunCultistShot>();
            attackDelay = 1; 
        }

        public override void TownNPCAttackProjSpeed(ref float moveSpeed, ref float attackMultiplier, ref float attackDelay) {
            moveSpeed = 14f;
            attackMultiplier = 1f;
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
                BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Surface,
                new FlavorTextBestiaryInfoElement("A follower of an ancient solar cult who dedicates their life to preserving peace using a sacred flame bow.")
            });
        }
    }
}