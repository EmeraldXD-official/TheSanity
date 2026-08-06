using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class RedsBlackBolt : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.BlackBolt;

        private Vector2 spawnPoint;
        private bool isHoming = false;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override bool? CanHitNPC(NPC target)
        {
            if (!isHoming) return false;
            return null;
        }

        public override void AI()
        {
            if (Projectile.localAI[0] == 0f)
            {
                spawnPoint = Projectile.Center;
                Projectile.localAI[0] = 1f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            float distanceTraveled = Vector2.Distance(spawnPoint, Projectile.Center);
            if (distanceTraveled >= 80f)
            {
                isHoming = true;
            }

            if (isHoming)
            {
                NPC closestNPC = null;
                float maxRadius = 600f;
                float closestDist = maxRadius;

                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC npc = Main.npc[i];
                    if (npc.CanBeChasedBy())
                    {
                        float dist = Vector2.Distance(Projectile.Center, npc.Center);
                        if (dist < closestDist)
                        {
                            closestDist = dist;
                            closestNPC = npc;
                        }
                    }
                }

                if (closestNPC != null)
                {
                    Vector2 targetDir = (closestNPC.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                    float targetSpeed = 13f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetDir * targetSpeed, 0.18f);
                }
            }

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, Projectile.velocity * -0.2f, 100, default, isHoming ? 1.2f : 0.8f);
                d.noGravity = true;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Midas, 300);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.BlackBolt].Value;
            Vector2 origin = texture.Size() / 2f;

            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;

                float progress = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                
                float alphaMult = isHoming ? 0.5f : 0.25f;
                Color trailColor = new Color(255, 170, 20, 0) * (progress * alphaMult);

                Main.EntitySpriteDraw(
                    texture, drawPos, null, trailColor,
                    Projectile.oldRot[i], origin, Projectile.scale * (0.8f + progress * 0.2f),
                    SpriteEffects.None, 0
                );
            }

            Color goldMainColor = (new Color(255, 215, 30, 0) * (isHoming ? 0.95f : 0.60f));
            Main.EntitySpriteDraw(
                texture, Projectile.Center - Main.screenPosition, null, goldMainColor,
                Projectile.rotation, origin, Projectile.scale,
                SpriteEffects.None, 0
            );

            return false;
        }
    }
}