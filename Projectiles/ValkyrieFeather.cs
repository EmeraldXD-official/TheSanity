using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class ValkyrieFeather : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.HarpyFeather;

        private float orbitAngle = 0f;

        // KARENA SPRITE ASLINYA MENGHADAP KIRI (<---), KITA KASIH OFFSET 180 DERAJAT (MathHelper.Pi)
        // SUPAYA UJUNG TAJAMNYA PAS MENGHADAP KE MUSUH
        private float SpriteAngleOffset => MathHelper.Pi; 

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
            Projectile.timeLeft = 600;
        }

        public override bool? CanHitNPC(NPC target)
        {
            if (Projectile.ai[0] == 0f) return false;
            return null;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.GelBalloonBuff, 300); // 5 Detik
        }

        public override void AI()
        {
            int targetIndex = (int)Projectile.ai[1];

            if (targetIndex < 0 || targetIndex >= Main.maxNPCs || !Main.npc[targetIndex].active || !Main.npc[targetIndex].CanBeChasedBy())
            {
                Projectile.Kill();
                return;
            }

            NPC target = Main.npc[targetIndex];

            // ==========================================
            // FASE 0: ORBITING & CHARGING
            // ==========================================
            if (Projectile.ai[0] == 0f)
            {
                List<Projectile> myOrbitGroup = new List<Projectile>();
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];
                    if (p.active && p.type == Projectile.type && p.ai[0] == 0f && (int)p.ai[1] == targetIndex)
                    {
                        myOrbitGroup.Add(p);
                    }
                }

                int totalFeathers = myOrbitGroup.Count;
                int myRank = myOrbitGroup.IndexOf(Projectile);
                if (myRank == -1) myRank = 0;

                orbitAngle += 0.05f; 

                float angleOffset = (MathHelper.TwoPi / System.Math.Max(1, totalFeathers)) * myRank;
                float currentAngle = orbitAngle + angleOffset;
                float orbitRadius = 50f + (totalFeathers * 1.5f);

                Vector2 targetOrbitPos = target.Center + currentAngle.ToRotationVector2() * orbitRadius;
                Projectile.Center = Vector2.Lerp(Projectile.Center, targetOrbitPos, 0.35f);

                // MENGUNCI ROTASI: Ujung tajam bulu lurus ngarah ke pusat musuh
                Vector2 toTarget = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.rotation = toTarget.ToRotation() + SpriteAngleOffset;

                // Delay Peluncuran Bertahap
                if (Projectile.localAI[0] > 0f)
                {
                    Projectile.localAI[0]--;
                    if (Projectile.localAI[0] <= 0f)
                    {
                        Projectile.ai[0] = 1f;
                        Vector2 dashDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                        Projectile.velocity = dashDir * 22f;
                    }
                }
            }
            // ==========================================
            // FASE 1: DASHING TO TARGET
            // ==========================================
            else if (Projectile.ai[0] == 1f)
            {
                Vector2 dashDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dashDir * 24f, 0.3f);

                // Rotasi saat meluncur tetap mengunci lurus ke target
                Projectile.rotation = dashDir.ToRotation() + SpriteAngleOffset;
            }

            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.PinkTorch, Projectile.velocity * -0.2f, 100, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.HarpyFeather].Value;
            Vector2 origin = texture.Size() / 2f;

            Color magentaColor = new Color(255, 30, 200, 0);

            if (Projectile.ai[0] == 1f)
            {
                for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
                {
                    if (Projectile.oldPos[i] == Vector2.Zero) continue;

                    float progress = 1f - (i / (float)Projectile.oldPos.Length);
                    Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                    Color trailColor = magentaColor * (progress * 0.45f);

                    Main.EntitySpriteDraw(
                        texture, drawPos, null, trailColor,
                        Projectile.rotation, origin, Projectile.scale * (0.8f + progress * 0.2f),
                        SpriteEffects.None, 0
                    );
                }
            }

            Main.EntitySpriteDraw(
                texture, Projectile.Center - Main.screenPosition, null, magentaColor * 0.95f,
                Projectile.rotation, origin, Projectile.scale,
                SpriteEffects.None, 0
            );

            return false;
        }
    }
}