using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.Audio;
using Terraria.DataStructures;

namespace TheSanity.NPCs
{
    // ==================================================================================
    // 🧠 CLASS NPC CONFUSION MINION
    // ==================================================================================
    public class ConfusionMinion : ModNPC
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BrainOfConfusion;

        public const int MinionMaxHP = 150;
        public const int MinionDefense = 5;
        public const float MoveSpeed = 4.5f;
        public const int AttackCycleTime = 180;
        public const int TelegraphDuration = 45;
        public const int LaserDamage = 20;
        public const float LaserVelocity = 9.5f;
        public const float KnockbackRecoil = 6.5f;

        private float AI_State { get => NPC.ai[0]; set => NPC.ai[0] = value; }
        private float AI_Timer { get => NPC.ai[1]; set => NPC.ai[1] = value; }
        private const float STATE_CHASE = 0f;
        private const float STATE_TELEGRAPH = 1f;

        public override void SetStaticDefaults() => Main.npcFrameCount[NPC.type] = 4;

        public override void SetDefaults()
        {
            NPC.width = 24;
            NPC.height = 32;
            NPC.damage = 15;
            NPC.defense = MinionDefense;
            NPC.lifeMax = MinionMaxHP;
            NPC.HitSound = SoundID.NPCHit9;
            NPC.DeathSound = SoundID.NPCDeath11;
            NPC.value = 100f;
            NPC.knockBackResist = 0.4f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            Player player = Main.player[NPC.target];
            if (player.dead || !player.active)
            {
                NPC.velocity.Y -= 0.2f;
                return;
            }

            if (AI_State == STATE_CHASE)
            {
                Vector2 targetDir = player.Center - NPC.Center;
                targetDir.Normalize();
                NPC.velocity = Vector2.Lerp(NPC.velocity, targetDir * MoveSpeed, 0.04f);
                AI_Timer++;
                if (AI_Timer >= AttackCycleTime - TelegraphDuration)
                {
                    AI_State = STATE_TELEGRAPH;
                    AI_Timer = 0;
                    NPC.velocity *= 0.5f;
                    NPC.netUpdate = true;
                }
            }
            else if (AI_State == STATE_TELEGRAPH)
            {
                NPC.velocity *= 0.85f;
                AI_Timer++;
                if (AI_Timer >= TelegraphDuration)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 shootDir = player.Center - NPC.Center;
                        shootDir.Normalize();
                        Vector2 vel = shootDir * LaserVelocity;

                        int proj = Projectile.NewProjectile(
                            NPC.GetSource_FromAI(),
                            NPC.Center,
                            vel,
                            ProjectileID.BrainScramblerBolt,
                            LaserDamage,
                            1f,
                            Main.myPlayer,
                            ai0: 999
                        );
                        if (proj != Main.maxProjectiles)
                        {
                            Main.projectile[proj].hostile = true;
                            Main.projectile[proj].friendly = false;
                            Main.projectile[proj].tileCollide = false;
                        }

                        NPC.velocity = -shootDir * KnockbackRecoil;
                    }
                    SoundEngine.PlaySound(SoundID.Item12, NPC.Center);
                    AI_State = STATE_CHASE;
                    AI_Timer = 0;
                    NPC.netUpdate = true;
                }
            }

            NPC.spriteDirection = NPC.velocity.X > 0.1f ? 1 : (NPC.velocity.X < -0.1f ? -1 : NPC.spriteDirection);
        }

        public override void FindFrame(int frameHeight)
        {
            NPC.frameCounter++;
            if (NPC.frameCounter >= 6)
            {
                NPC.frameCounter = 0;
                NPC.frame.Y = (NPC.frame.Y + frameHeight) % (Main.npcFrameCount[NPC.type] * frameHeight);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D texture = TextureAssets.Npc[NPC.type].Value;
            Vector2 drawPos = NPC.Center - screenPos;
            int frameHeight = texture.Height / Main.npcFrameCount[NPC.type];
            Rectangle srcRect = new Rectangle(0, NPC.frame.Y, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width / 2f, frameHeight / 2f);
            SpriteEffects effects = (NPC.spriteDirection == -1) ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            if (AI_State == STATE_TELEGRAPH)
            {
                float opacity = 0.4f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 15f) * 0.3f;
                Color outline = Color.Red * opacity;
                Vector2[] offsets = { new Vector2(-2,0), new Vector2(2,0), new Vector2(0,-2), new Vector2(0,2) };
                foreach (var off in offsets)
                    spriteBatch.Draw(texture, drawPos + off, srcRect, outline, NPC.rotation, origin, NPC.scale, effects, 0f);
            }

            spriteBatch.Draw(texture, drawPos, srcRect, drawColor, NPC.rotation, origin, NPC.scale, effects, 0f);
            return false;
        }
    }

    // ==================================================================================
    // 🔴 GLOBAL PROJECTILE – MEWARNAI BRAINSCRAMBLERBOLT DENGAN ARAH YANG BENAR
    // ==================================================================================
    public class RedBrainScramblerBolt : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
            => entity.type == ProjectileID.BrainScramblerBolt;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (projectile.ai[0] == 999)
            {
                projectile.ai[0] = 0;
                projectile.localAI[0] = 1;
            }
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            if (projectile.localAI[0] == 1)
            {
                Texture2D texture = TextureAssets.Projectile[ProjectileID.BrainScramblerBolt].Value;
                Vector2 drawPos = projectile.Center - Main.screenPosition;

                int frameHeight = texture.Height / Main.projFrames[projectile.type];
                Rectangle sourceRect = new Rectangle(0, projectile.frame * frameHeight, texture.Width, frameHeight);

                Vector2 origin = new Vector2(texture.Width / 2f, frameHeight / 2f);
                Color redColor = new Color(255, 40, 40) * projectile.Opacity;

                // 🔥 FLIP jika velocity mengarah ke kiri, agar sprite menghadap arah gerak
                SpriteEffects effects = (projectile.velocity.X < 0) ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    sourceRect,
                    redColor,
                    projectile.rotation,
                    origin,
                    projectile.scale,
                    effects,   // ← tambahkan flip jika perlu
                    0
                );
                return false;
            }
            return true;
        }
    }
}