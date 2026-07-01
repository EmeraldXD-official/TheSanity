using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.UI.BigProgressBar;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;
using Terraria.Audio;
using TheSanity.Projectiles;
using Terraria.DataStructures;

namespace TheSanity.GlobalNPCs
{
    public class EaterOfWorldsBossBarModifier : GlobalBossBar
    {
        public override bool PreDraw(SpriteBatch spriteBatch, NPC npc, ref BossBarDrawParams drawParams)
        {
            if (npc.type != NPCID.EaterofWorldsHead &&
                npc.type != NPCID.EaterofWorldsBody &&
                npc.type != NPCID.EaterofWorldsTail)
                return true;

            NPC jantung = null;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<NPCs.EaterJantung>())
                {
                    jantung = Main.npc[i];
                    break;
                }
            }

            if (jantung != null)
            {
                drawParams.Shield = jantung.life;
                drawParams.ShieldMax = jantung.lifeMax;
                drawParams.Life = npc.lifeMax;
                drawParams.LifeMax = npc.lifeMax;
            }
            else
            {
                drawParams.Shield = 0f;
                drawParams.ShieldMax = 0f;
            }

            return true;
        }
    }

    public class EaterOfWorldsHealthManager : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private bool hasSpawnedHeart = false;

        public static bool DeathAnimationActive = false;
        public static int deathAnimTimer = 0;
        public static bool headDespawned = false;
        public static bool FinalSegmentKilled = false; 

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.EaterofWorldsHead ||
                   entity.type == NPCID.EaterofWorldsBody ||
                   entity.type == NPCID.EaterofWorldsTail;
        }

        public override void SetDefaults(NPC entity)
        {
            entity.dontTakeDamage = true;
            entity.boss = false; 
        }

        public override void PostAI(NPC npc)
        {
            // FIX: Hapus pengecekan ai[0] yang rusak. Reset kini di-handle sepenuhnya oleh ModSystem.

            if (DeathAnimationActive)
            {
                npc.dontTakeDamage = true;
                npc.velocity = Vector2.Zero;
                npc.damage = 0;
                npc.alpha = 180;
                if (Main.rand.NextBool(3))
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Shadowflame, 0f, 0f, 150, default, 1.2f);
                return;
            }

            if (NPCs.EaterJantung.HeartDestroyed)
            {
                if (!DeathAnimationActive && npc.type == NPCID.EaterofWorldsHead)
                {
                    DeathAnimationActive = true;
                    deathAnimTimer = 0;
                    headDespawned = false;

                    for (int i = 0; i < 50; i++)
                    {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.Shadowflame,
                            Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f), 150, default, 1.8f);
                    }
                    SoundEngine.PlaySound(SoundID.Roar, npc.Center);

                    for (int i = 0; i < Main.maxNPCs; i++)
                    {
                        NPC other = Main.npc[i];
                        if (other.active && (other.type == NPCID.EaterofWorldsHead || other.type == NPCID.EaterofWorldsBody || other.type == NPCID.EaterofWorldsTail))
                        {
                            other.velocity = Vector2.Zero;
                            other.alpha = 180;
                            other.dontTakeDamage = true;
                        }
                    }
                }
                return;
            }

            npc.life = npc.lifeMax;
            npc.active = true;

            if (npc.type == NPCID.EaterofWorldsHead && !hasSpawnedHeart)
            {
                bool heartExists = false;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    if (Main.npc[i].active && Main.npc[i].type == ModContent.NPCType<NPCs.EaterJantung>())
                    {
                        heartExists = true;
                        break;
                    }
                }

                if (!heartExists && Main.netMode != NetmodeID.MultiplayerClient)
                {
                    int heartIndex = NPC.NewNPC(npc.GetSource_FromAI(), (int)npc.Center.X, (int)npc.Center.Y, ModContent.NPCType<NPCs.EaterJantung>());
                    if (heartIndex < Main.maxNPCs)
                    {
                        Main.npc[heartIndex].netUpdate = true;
                        NPCs.EaterJantung.HeartDestroyed = false;
                    }
                }

                hasSpawnedHeart = true;
            }
        }

        public static void UpdateDeathAnimation()
        {
            if (!DeathAnimationActive) return;

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && (p.type == ProjectileID.CursedFlameHostile || p.type == ProjectileID.EyeFire || p.type == ModContent.ProjectileType<BabyEaterWatcher>()))
                {
                    p.Kill();
                }
            }

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == NPCID.VileSpit)
                {
                    Main.npc[i].active = false;
                }
            }

            int jedaLedakanTick = 6; 
            
            deathAnimTimer++;
            if (deathAnimTimer < jedaLedakanTick) return;
            deathAnimTimer = 0;

            var segments = new System.Collections.Generic.List<NPC>();
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && (n.type == NPCID.EaterofWorldsHead || n.type == NPCID.EaterofWorldsBody || n.type == NPCID.EaterofWorldsTail))
                {
                    segments.Add(n);
                }
            }

            if (segments.Count == 0)
            {
                DeathAnimationActive = false;
                ResetStaticState(); // Double safety reset saat list kosong
                return;
            }

            if (segments.Count > 3)
            {
                NPC targetSegment = segments[segments.Count - 1];

                for (int d = 0; d < 20; d++)
                    Dust.NewDust(targetSegment.position, targetSegment.width, targetSegment.height, DustID.Shadowflame,
                        Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 150, default, 1.5f);
                
                int playerTerdekat = Player.FindClosest(targetSegment.position, targetSegment.width, targetSegment.height);
                targetSegment.lastInteraction = playerTerdekat;
                for (int p = 0; p < Main.maxPlayers; p++)
                {
                    if (Main.player[p].active) targetSegment.playerInteraction[p] = true;
                }

                targetSegment.NPCLoot();
                targetSegment.active = false;
            }
            else
            {
                DeathAnimationActive = false; 

                if (!FinalSegmentKilled)
                {
                    FinalSegmentKilled = true;

                    foreach (NPC part in segments)
                    {
                        if (!part.active) continue;

                        for (int d = 0; d < 40; d++)
                            Dust.NewDust(part.position, part.width, part.height, DustID.Shadowflame,
                                Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 150, default, 1.8f);

                        SoundEngine.PlaySound(SoundID.NPCDeath1, part.Center);

                        int playerTerdekat = Player.FindClosest(part.position, part.width, part.height);
                        part.lastInteraction = playerTerdekat;

                        for (int p = 0; p < Main.maxPlayers; p++)
                        {
                            if (Main.player[p].active)
                            {
                                part.playerInteraction[p] = true;
                            }
                        }

                        part.dontTakeDamage = false; 
                        part.life = 0;
                        part.HitEffect();
                        part.checkDead(); 
                    }
                }

                headDespawned = true;
            }
        }

        public static void ResetStaticState()
        {
            DeathAnimationActive = false;
            deathAnimTimer = 0;
            headDespawned = false;
            FinalSegmentKilled = false;
            NPCs.EaterJantung.HeartDestroyed = false;
        }
    }
}