using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class RedsGoldenFlame : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SpiritFlame;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.SpiritFlame];
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;
        }

        public override void AI()
        {
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
            }

            if (Projectile.localAI[0] < 18f)
            {
                Projectile.localAI[0]++;
                Projectile.velocity *= 0.92f;
            }
            else
            {
                NPC target = null;
                float maxDist = 520f;

                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.CanBeChasedBy())
                    {
                        float dist = Vector2.Distance(Projectile.Center, npc.Center);
                        if (dist < maxDist)
                        {
                            maxDist = dist;
                            target = npc;
                        }
                    }
                }

                if (target != null)
                {
                    Vector2 targetDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetDir * 14f, 0.14f);
                }
                else
                {
                    Projectile.velocity *= 0.98f;
                }
            }

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(8f, 8f), DustID.GoldFlame, Projectile.velocity * -0.2f, 100, default, 1.1f);
                d.noGravity = true;
            }

            Projectile.rotation = Projectile.velocity.X * 0.05f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // PROYEKTIL API MENGHASILKAN DEBUFF OnFire3 (HELLFIRE) & MIDAS
            target.AddBuff(BuffID.OnFire3, 180);
            target.AddBuff(BuffID.Midas, 300);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.SpiritFlame].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width / 2f, frameHeight / 2f);

            Color goldFlameColor = new Color(255, 210, 40, 0) * 0.95f;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float progress = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = goldFlameColor * (progress * 0.45f);

                Main.EntitySpriteDraw(
                    texture, drawPos, sourceRect, trailColor,
                    Projectile.rotation, origin, Projectile.scale * (0.8f + progress * 0.2f),
                    SpriteEffects.None, 0
                );
            }

            Main.EntitySpriteDraw(
                texture, Projectile.Center - Main.screenPosition, sourceRect, goldFlameColor,
                Projectile.rotation, origin, Projectile.scale,
                SpriteEffects.None, 0
            );

            return false;
        }
    }
}