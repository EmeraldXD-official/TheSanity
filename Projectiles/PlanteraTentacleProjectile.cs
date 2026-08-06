using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class PlanteraTentacleProjectile : ModProjectile
    {
        private const float OrbitRadius = 46f;
        private const float OrbitSpeed = 0.05f; 

        private const float RopeScale = 0.5f;      
        private const float RopeTrimAtTentacle = 10f; 
        private const int TrailLength = 4;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailLength;
        }

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 28;
            Projectile.scale = 1f;

            Projectile.aiStyle = -1;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.timeLeft = 5; 

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12;

            Projectile.rotation = 0f;
        }

        public override void AI()
        {
            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex < 0 || parentIndex >= Main.maxProjectiles)
            {
                Projectile.Kill();
                return;
            }

            Projectile parent = Main.projectile[parentIndex];
            bool parentValid = parent.active && parent.type == ModContent.ProjectileType<PlanteraGraspProjectile>();
            
            if (!parentValid || !(parent.ModProjectile is PlanteraGraspProjectile grasp) || !grasp.IsSecondForm)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 5; 

            // Orbit mengelilingi Yoyo
            Projectile.ai[1] += OrbitSpeed;
            float angle = Projectile.ai[1];

            Vector2 offset = new Vector2(
                (float)Math.Cos(angle),
                (float)Math.Sin(angle) * 0.85f 
            ) * OrbitRadius;

            Projectile.Center = parent.Center + offset;
            Projectile.velocity = Vector2.Zero;

            // Rotasi menghadap ke luar (membelakangi yoyo)
            Vector2 dirFromParent = Projectile.Center - parent.Center;
            if (dirFromParent != Vector2.Zero)
            {
                Projectile.rotation = dirFromParent.ToRotation() + MathHelper.Pi;
            }
        }

        // --- METHOD DIPANGGIL SAAT DASH YOYO MENGENAI MUSUH ---
        public void EmitSporeCloud()
        {
            if (Main.myPlayer != Projectile.owner)
                return;

            Vector2 dirFromParent = Projectile.Center - Main.projectile[(int)Projectile.ai[0]].Center;
            if (dirFromParent == Vector2.Zero) dirFromParent = new Vector2(0, -1);
            dirFromParent.Normalize();

            // Awan diluncurkan perlahan ke depan dari arah mulut Tentakel + variasi acak
            Vector2 cloudVel = dirFromParent * 2.5f + Main.rand.NextVector2Circular(0.5f, 0.5f);

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                cloudVel,
                ModContent.ProjectileType<PlanteraSporeCloudProjectile>(),
                (int)(Projectile.damage * 0.6f),
                0f,
                Projectile.owner
            );
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Poisoned, 60 * 4);

            // Jika tentakel itu sendiri yang mengenai musuh saat Yoyo dash, keluarkan awan juga
            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex >= 0 && parentIndex < Main.maxProjectiles)
            {
                Projectile parent = Main.projectile[parentIndex];
                if (parent.active && parent.ModProjectile is PlanteraGraspProjectile grasp && grasp.IsDashing)
                {
                    EmitSporeCloud();
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int parentIndex = (int)Projectile.ai[0];
            if (parentIndex >= 0 && parentIndex < Main.maxProjectiles)
            {
                Projectile parent = Main.projectile[parentIndex];
                if (parent.active && parent.ModProjectile is PlanteraGraspProjectile grasp)
                {
                    Vector2 start = grasp.GetHeadChainAttachPoint();
                    PlanteraGraspProjectile.DrawChainSegment(
                        start, Projectile.Center, scale: RopeScale,
                        edgeTrimStart: 0f, edgeTrimEnd: RopeTrimAtTentacle
                    );
                }
            }

            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = new Vector2(texture.Width / 2f, texture.Height / 2f);

            // Trail
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = lightColor * (progress * 0.3f);

                Main.EntitySpriteDraw(
                    texture, drawPos, null, trailColor,
                    Projectile.oldRot[i], origin, Projectile.scale,
                    SpriteEffects.None, 0
                );
            }

            // Sprite Utama
            Main.EntitySpriteDraw(
                texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale,
                SpriteEffects.None, 0
            );

            return false;
        }
    }
}