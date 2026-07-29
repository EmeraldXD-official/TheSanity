using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.Utilities;
using Terraria.GameContent.ItemDropRules;

namespace TheSanity.GlobalNPC.Enemy
{
    public class Goldfish_Walker : ModNPC
    {
        public override void SetStaticDefaults()
        {
            // Frame count tetap 6 sesuai sprite walker
            Main.npcFrameCount[NPC.type] = 6; 
        }

        public override void SetDefaults()
        {
            NPC.width = 25;   
            NPC.height = 28;  
            
            // =========================================================================
            // [BALANCING STATUS]
            // =========================================================================
            NPC.damage = 40;            // Base Damage 40. Di Master Mode akan otomatis jadi ~120.
            NPC.defense = 10;            
            NPC.lifeMax = 1000;          
            
            NPC.value = 10000f;          
            NPC.knockBackResist = 0.5f; 
            
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            
            NPC.friendly = false;        
            NPC.aiStyle = 0; // AI Custom
        }

        public override float SpawnChance(NPCSpawnInfo spawnInfo)
        {
            // Spawn di Overworld saat hujan, kecuali di biome jahat/hallow
            if (spawnInfo.Player.ZoneOverworldHeight && Main.raining)
            {
                if (spawnInfo.Player.ZoneCrimson || spawnInfo.Player.ZoneCorrupt || spawnInfo.Player.ZoneHallow)
                {
                    return 0f;
                }
                return 0.1f;
            }
            return 0f;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            npcLoot.Add(ItemDropRule.Common(ItemID.Minishark, 1, 1, 1));
            npcLoot.Add(ItemDropRule.Common(ItemID.GoldCoin, 1, 1, 2)); // Contoh drop koin emas
        }

        public override void AI()
        {
            // -----------------------------------------------------------------
            // [FIX MULTIPLAYER: TARGETING]
            // -----------------------------------------------------------------
            // Panggil TargetClosest di awal agar NPC.target valid sebelum spawn projectile
            NPC.TargetClosest(true);
            Player player = Main.player[NPC.target];

            // -----------------------------------------------------------------
            // [FIX MULTIPLAYER: PROJECTILE SPAWNING]
            // -----------------------------------------------------------------
            if (NPC.localAI[0] == 0f)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // FIX: Gunakan Main.myPlayer (255 di Server) sebagai owner agar sinkron.
                    // [PROJECTILE DAMAGE BALANCING LOCATION]: Damage dilempar langsung dari NPC.damage (40).
                    int proj = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<MinisharkVisual>(), NPC.damage, 0f, Main.myPlayer);
                    
                    if (proj != Main.maxProjectiles)
                    {
                        Main.projectile[proj].ai[0] = NPC.whoAmI;

                        // FIX PELURU NUMPANG LEWAT: Suntik sifat musuh agar melukai player
                        Main.projectile[proj].friendly = false;
                        Main.projectile[proj].hostile = true;

                        // Pastikan server mengirim data projectile ke semua client secara paksa
                        if (Main.netMode == NetmodeID.Server)
                        {
                            NetMessage.SendData(MessageID.SyncProjectile, -1, -1, null, proj);
                        }
                    }
                }
                NPC.localAI[0] = 1f; 
            }

            // Logika Pergerakan
            if (player != null && player.active && !player.dead)
            {
                float distance = Vector2.Distance(NPC.Center, player.Center);
                float targetDistance = 20f * 16f; 

                NPC.direction = (player.Center.X < NPC.Center.X) ? -1 : 1;

                bool canSeePlayer = Collision.CanHitLine(NPC.Center, 1, 1, player.Center, 1, 1);
                NPC.ai[1] = canSeePlayer ? 1f : 0f;

                // [MOVEMENT SPEED BALANCING LOCATION]
                float moveSpeed = 2.5f; 

                if (!canSeePlayer)
                {
                    if (player.Center.X < NPC.Center.X)
                        NPC.velocity.X = -moveSpeed * 1.35f; 
                    else
                        NPC.velocity.X = moveSpeed * 1.35f;  
                }
                else
                {
                    // Menjaga jarak dengan player (Kiting)
                    if (distance < targetDistance - 32f) 
                    {
                        if (player.Center.X < NPC.Center.X)
                            NPC.velocity.X = moveSpeed;  
                        else
                            NPC.velocity.X = -moveSpeed; 
                    }
                    else if (distance > targetDistance + 32f)
                    {
                        if (player.Center.X < NPC.Center.X)
                            NPC.velocity.X = -moveSpeed; 
                        else
                            NPC.velocity.X = moveSpeed;  
                    }
                    else
                    {
                        NPC.velocity.X *= 0.8f;
                    }
                }

                // Gravitas
                NPC.velocity.Y += 0.3f;
                if (NPC.velocity.Y > 10f) NPC.velocity.Y = 10f;

                // FIX ANTI-SPIDERMAN: Hanya lompat jika menabrak dinding DAN sedang di tanah
                if (NPC.velocity.X != 0f && NPC.collideX && NPC.velocity.Y == 0f)
                {
                    NPC.velocity.Y = !canSeePlayer ? -7.2f : -5.5f; 
                }
            }
        }

        public override void FindFrame(int frameHeight)
        {
            NPC.spriteDirection = NPC.direction;

            if (NPC.velocity.X == 0f)
            {
                NPC.frame.Y = 0; 
                NPC.frameCounter = 0;
            }
            else 
            {
                NPC.frameCounter += 1.0; 
                if (NPC.frameCounter >= 6.0) 
                {
                    NPC.frameCounter = 0;
                    NPC.frame.Y += frameHeight; 
                    
                    if (NPC.frame.Y >= frameHeight * 6)
                    {
                        NPC.frame.Y = frameHeight; 
                    }
                }
            }
        }
    }
}