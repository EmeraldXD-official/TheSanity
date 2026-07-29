using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Summon
{
    public class KindDemonMinion : ModProjectile
    {
        // 😈 Mengambil sprite dari NPC Demon Vanilla Terraria (NPC_62)
        public override string Texture => "Terraria/Images/NPC_62";

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 5; 
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.ignoreWater = true;
        }

        public override bool? CanCutTiles() => false;
        public override bool MinionContactDamage() => false;

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            // Pengecekan Buff & Status Player
            if (player.dead || !player.active)
            {
                player.ClearBuff(ModContent.BuffType<DemonsBookBuff>());
            }
            if (player.HasBuff(ModContent.BuffType<DemonsBookBuff>()))
            {
                Projectile.timeLeft = 2;
            }

            // 🛡️ 1. GAYA TOLAK (ANTI-STACKING ANTAREDEMON)
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile other = Main.projectile[i];
                if (i != Projectile.whoAmI && other.active && other.owner == Projectile.owner && other.type == Projectile.type)
                {
                    float distance = Vector2.Distance(Projectile.Center, other.Center);
                    if (distance < 52f)
                    {
                        Vector2 pushDir = Projectile.Center - other.Center;
                        if (pushDir == Vector2.Zero)
                        {
                            pushDir = new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f));
                        }
                        pushDir.Normalize();
                        Projectile.velocity += pushDir * 0.6f;
                    }
                }
            }

            // Target pencarian musuh
            NPC target = FindTarget(player, 850f);
            int minionSlot = Projectile.minionPos;
            int totalMinions = player.ownedProjectileCounts[Type];
            if (totalMinions < 1) totalMinions = 1;

            if (target != null)
            {
                // 🎯 2. FORMASI BUSUR JARAK JAUH (320 Pixel dari Musuh)
                Vector2 targetToPlayer = (player.Center - target.Center).SafeNormalize(-Vector2.UnitY);
                float spreadAngle = (minionSlot - (totalMinions - 1) / 2f) * 0.35f;
                
                Vector2 attackOffset = targetToPlayer.RotatedBy(spreadAngle) * 320f;
                Vector2 targetPos = target.Center + attackOffset;
                
                Vector2 direction = targetPos - Projectile.Center;

                if (direction.Length() > 16f)
                {
                    direction.Normalize();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * 10f, 0.08f);
                }
                else
                {
                    Projectile.velocity *= 0.9f;
                }

                // Menghadap ke arah musuh
                Projectile.spriteDirection = target.Center.X > Projectile.Center.X ? 1 : -1;

                // ⏱️ TIMER SERANGAN (Tembak tiap ~1.3 detik)
                Projectile.ai[0]++;
                if (Projectile.ai[0] >= 80)
                {
                    Projectile.ai[0] = 0;

                    if (Projectile.owner == Main.myPlayer)
                    {
                        Vector2 shootVel = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 3f;
                        SoundEngine.PlaySound(SoundID.Item8, Projectile.Center);

                        int p = Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            Projectile.Center,
                            shootVel,
                            ProjectileID.DemonScythe,
                            Projectile.damage/2,
                            Projectile.knockBack,
                            Projectile.owner
                        );

                        if (p >= 0 && p < Main.maxProjectiles)
                        {
                            Projectile scythe = Main.projectile[p];
                            scythe.friendly = true;
                            scythe.hostile = false;
                            scythe.DamageType = DamageClass.Summon;

                            // ⚡ FIX IMMUNITY FRAMES: Menggunakan Local Immunity Cooldown
                            scythe.usesLocalNPCImmunity = true;
                            scythe.localNPCHitCooldown = 10; // Setiap sabit menghitung hit-cooldown sendiri per musuh
                        }
                    }
                }
            }
            else
            {
                // 🧘 MODE SANTAI: Formasi berjejer di samping & atas player
                float row = minionSlot / 3;
                float col = minionSlot % 3;

                Vector2 idleOffset = new Vector2(
                    -player.direction * (60f + col * 35f),
                    -45f - (row * 35f) + (float)Math.Sin(Main.GameUpdateCount * 0.05f + minionSlot) * 4f
                );

                Vector2 idlePosition = player.Center + idleOffset;
                Vector2 direction = idlePosition - Projectile.Center;

                if (direction.Length() > 10f)
                {
                    direction.Normalize();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * 9f, 0.1f);
                }
                else
                {
                    Projectile.velocity *= 0.92f;
                }

                Projectile.spriteDirection = player.direction;
            }

            // 🎬 Animasi Terbang
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 6)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % 4;
            }

            Lighting.AddLight(Projectile.Center, 0.4f, 0.1f, 0.1f);
        }

        private NPC FindTarget(Player player, float range)
        {
            NPC target = null;
            float closestDist = range;

            if (player.HasMinionAttackTargetNPC)
            {
                NPC npc = Main.npc[player.MinionAttackTargetNPC];
                if (npc.CanBeChasedBy(this) && Vector2.Distance(Projectile.Center, npc.Center) < range)
                {
                    return npc;
                }
            }

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy(this))
                {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < closestDist)
                    {
                        closestDist = dist;
                        target = npc;
                    }
                }
            }
            return target;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Npc[NPCID.Demon].Value;
            int frameHeight = texture.Height / 5; 
            Rectangle sourceRect = new Rectangle(0, frameHeight * Projectile.frame, texture.Width, frameHeight);
            Vector2 origin = sourceRect.Size() * 0.5f;

            SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                sourceRect,
                lightColor,
                Projectile.rotation,
                origin,
                Projectile.scale,
                effects,
                0
            );

            return false;
        }
    }
}