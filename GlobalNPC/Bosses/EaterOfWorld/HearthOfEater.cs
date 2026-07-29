using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.Audio;

namespace TheSanity.NPCs
{
    public class EaterJantung : ModNPC
    {
        public override string Texture => "Terraria/Images/Projectile_18";

        public static bool HeartDestroyed = false;
        private int lastSegmentCount = -1;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.DontDoHardmodeScaling[Type] = true;
        }

        public override void SetDefaults()
        {
            // =========================================================================
            // GUIDE BALANCING: STATS UTAMA JANTUNG
            // - width & height : Ukuran hitbox dari Jantung (32x32)
            // - defense        : Nilai pertahanan/armor Jantung
            // - lifeMax & life : Darah dasar Jantung sebelum ditambahkan bonus per segmen
            // =========================================================================
            NPC.width = 32;
            NPC.height = 32;
            NPC.damage = 0; // Jantung tidak memberikan damage kontak ke player
            NPC.defense = 15;
            NPC.lifeMax = 1000;
            NPC.life = 1000;
            NPC.boss = false;
            NPC.dontTakeDamage = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            NPC.netAlways = true;
            NPC.chaseable = true;
            NPC.timeLeft = 999999;
            NPC.knockBackResist = 0f;

            // =========================================================================
            // GUIDE FIX: KEKEBALAN BUFF / DEBUFF
            // Menggunakan NPC.buffImmune.Length agar otomatis mengikuti ukuran array game,
            // sehingga mencakup debuff dari mod lain (seperti Fargo's) dan mencegah crash!
            // =========================================================================
            for (int i = 0; i < NPC.buffImmune.Length; i++)
            {
                NPC.buffImmune[i] = true;
            }
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            Player targetPlayer = Main.player[NPC.target];
            if (targetPlayer == null || !targetPlayer.active || targetPlayer.dead)
            {
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player pl = Main.player[i];
                    if (pl != null && pl.active && !pl.dead)
                    {
                        targetPlayer = pl;
                        break;
                    }
                }
                if (targetPlayer == null)
                {
                    NPC.active = false;
                    return;
                }
            }

            int currentSegmentCount = 0;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && (n.type == NPCID.EaterofWorldsBody || n.type == NPCID.EaterofWorldsTail))
                    currentSegmentCount++;
            }

            if (currentSegmentCount != lastSegmentCount)
            {
                lastSegmentCount = currentSegmentCount;
                
                // =========================================================================
                // GUIDE BALANCING: FORMULA PENAMBAHAN DARAH JANTUNG (SHIELD)
                // - 1000 : Darah dasar awal jantung
                // - 268  : Nilai HP yang ditambahkan untuk SETIAP segmen EoW yang masih hidup.
                // Kamu bisa ubah angka 268 ini jika ingin pelindung jantung lebih tebal/tipis.
                // =========================================================================
                int newMaxLife = 1000 + (currentSegmentCount * 268);
                NPC.lifeMax = newMaxLife;
                NPC.life = newMaxLife;
                NPC.netUpdate = true;
            }

            NPC targetSegment = null;
            float closestDist = float.MaxValue;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && (n.type == NPCID.EaterofWorldsHead || n.type == NPCID.EaterofWorldsBody || n.type == NPCID.EaterofWorldsTail))
                {
                    float d = Vector2.Distance(n.Center, targetPlayer.Center);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        targetSegment = n;
                    }
                }
            }

            if (targetSegment == null)
            {
                bool headExists = false;
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    if (Main.npc[i].active && Main.npc[i].type == NPCID.EaterofWorldsHead)
                    {
                        headExists = true;
                        targetSegment = Main.npc[i];
                        break;
                    }
                }
                if (!headExists)
                {
                    NPC.active = false;
                    return;
                }
            }

            NPC.Center = targetSegment.Center;
            NPC.velocity = targetSegment.velocity;

            bool tailExists = false;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                if (Main.npc[i].active && Main.npc[i].type == NPCID.EaterofWorldsTail)
                {
                    tailExists = true;
                    break;
                }
            }
            NPC.dontTakeDamage = !tailExists;

            if (Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(14, 14), DustID.Shadowflame, Vector2.Zero, 100, default, 1.1f);
                d.noGravity = true;
            }

            NPC.netUpdate = true;
        }

        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
        {
            // =========================================================================
            // GUIDE BALANCING: PENGALI DAMAGE MASUK
            // - 4f : Berarti damage yang diterima Jantung akan dikali 4x lipat lebih besar.
            // Silakan sesuaikan angka ini jika dirasa terlalu cepat atau terlalu lama hancur.
            // =========================================================================
            modifiers.FinalDamage *= 4f;
        }

        public override void OnKill()
        {
            HeartDestroyed = true;
            NPC.netUpdate = true;

            if (Main.netMode != NetmodeID.Server)
            {
                SoundEngine.PlaySound(SoundID.NPCDeath58, NPC.Center);
                for (int i = 0; i < 90; i++)
                {
                    Vector2 ringVelocity = Main.rand.NextVector2CircularEdge(14f, 14f);
                    Dust d = Dust.NewDustPerfect(NPC.Center, DustID.Shadowflame, ringVelocity * Main.rand.NextFloat(0.6f, 1.8f), 100, default, 2.5f);
                    d.noGravity = true;
                    d.fadeIn = 1.2f;
                }
            }
        }
    }
}