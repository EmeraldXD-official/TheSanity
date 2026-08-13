using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    // ==================== 1. KEPALA (HEAD) ====================
    public class DimensionalSerpentHead : ModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.BoneSerpentHead;

        public override void SetStaticDefaults()
        {
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
        }

        public override void SetDefaults()
        {
            NPC.width = 32;
            NPC.height = 32;
            NPC.damage = 60;
            NPC.defense = 25;
            NPC.lifeMax = 3500;
            
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
            NPC.behindTiles = true;
            NPC.netAlways = true;
        }

        public override void AI()
        {
            // --- PERBAIKAN 1: SPAWN SEGMEN BODY & TAIL ---
            if (Main.netMode != NetmodeID.MultiplayerClient && NPC.ai[0] == 0f)
            {
                NPC.ai[0] = 1f; 
                int segmentLength = 12;
                int currentParent = NPC.whoAmI;
                int waveID = (int)NPC.ai[2];

                for (int i = 0; i < segmentLength; i++)
                {
                    int segType = (i == segmentLength - 1) 
                        ? ModContent.NPCType<DimensionalSerpentTail>() 
                        : ModContent.NPCType<DimensionalSerpentBody>();

                    int segID = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, segType);
                    
                    if (segID >= 0 && segID < Main.maxNPCs)
                    {
                        NPC segment = Main.npc[segID];
                        segment.realLife = NPC.whoAmI;
                        segment.ai[0] = NPC.ai[0];
                        segment.ai[1] = currentParent;
                        segment.ai[2] = waveID; 
                        segment.Center = NPC.Center;
                        segment.velocity = NPC.velocity;
                        segment.netUpdate = true;
                        currentParent = segID; 
                    }
                }
            }

            // --- AI PERGERAKAN ---
            NPC.TargetClosest(true);
            Player player = Main.player[NPC.target];

            if (!player.active || player.dead)
            {
                NPC.velocity.Y += 0.3f;
                NPC.EncourageDespawn(10);
                return;
            }

            // Inisialisasi phase zigzag agar tidak berderet kaku
            if (NPC.ai[1] == 0f) NPC.ai[1] = Main.rand.NextFloat(100f);

            // Jika Mode Dash (dari Boss), ular tetap lurus
            if (NPC.ai[0] == 1f)
            {
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;
            }
            else // Mode Zigzag mengejar player
            {
                Vector2 targetDir = (player.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                float flySpeed = 12f;
                
                NPC.ai[1] += 0.08f;
                Vector2 waveOffset = targetDir.RotatedBy(MathHelper.PiOver2) * (float)Math.Sin(NPC.ai[1]) * 4f;
                Vector2 desiredVel = (targetDir * flySpeed) + waveOffset;

                NPC.velocity = Vector2.Lerp(NPC.velocity, desiredVel, 0.06f);
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;
            }

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(NPC.Center, DustID.Electric, -NPC.velocity * 0.2f, 0, Color.Cyan, 1.2f);
                d.noGravity = true;
            }
        }
    }

    // ==================== 2. BADAN (BODY) ====================
    public class DimensionalSerpentBody : ModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.BoneSerpentBody;

        public override void SetDefaults()
        {
            NPC.width = 30;
            NPC.height = 30;
            NPC.damage = 45;
            NPC.defense = 30;
            NPC.lifeMax = 3500;
            
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
            NPC.behindTiles = true;
        }

        public override bool CheckActive() => false;

        public override void AI()
        {
            NPC parent = Main.npc[(int)NPC.ai[1]];

            if (!parent.active)
            {
                NPC.life = 0;
                NPC.HitEffect();
                NPC.active = false;
                NPC.netUpdate = true;
                return;
            }

            Vector2 toParent = parent.Center - NPC.Center;
            if (toParent != Vector2.Zero)
            {
                NPC.rotation = toParent.ToRotation() + MathHelper.PiOver2;
                float spacing = 22f;
                NPC.Center = parent.Center - toParent.SafeNormalize(Vector2.Zero) * spacing;
            }
            NPC.velocity = parent.velocity;
        }
    }

    // ==================== 3. EKOR (TAIL) ====================
    public class DimensionalSerpentTail : ModNPC
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.BoneSerpentTail;

        public override void SetDefaults()
        {
            NPC.width = 28;
            NPC.height = 28;
            NPC.damage = 40;
            NPC.defense = 35;
            NPC.lifeMax = 3500;
            
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;

            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
            NPC.behindTiles = true;
        }

        public override bool CheckActive() => false;

        public override void AI()
        {
            NPC parent = Main.npc[(int)NPC.ai[1]];

            if (!parent.active)
            {
                NPC.life = 0;
                NPC.HitEffect();
                NPC.active = false;
                NPC.netUpdate = true;
                return;
            }

            Vector2 toParent = parent.Center - NPC.Center;
            if (toParent != Vector2.Zero)
            {
                NPC.rotation = toParent.ToRotation() + MathHelper.PiOver2;
                float spacing = 20f;
                NPC.Center = parent.Center - toParent.SafeNormalize(Vector2.Zero) * spacing;
            }
            NPC.velocity = parent.velocity;
        }
    }
}