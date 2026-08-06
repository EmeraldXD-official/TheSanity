using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class YeletsLeaf : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalLeaf;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.CrystalLeaf];
            
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
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

            if (!parent.active || parent.type != ProjectileID.Yelets || parent.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;

            float leafIndex = Projectile.ai[1]; 
            float baseAngle = leafIndex * MathHelper.PiOver2; 

            // Counter Timer Berputar (Searah Jarum Jam)
            Projectile.localAI[0] += 0.03f; 
            float currentAngle = baseAngle + Projectile.localAI[0];

            float orbitRadius = 75f;
            Vector2 offset = currentAngle.ToRotationVector2() * orbitRadius;
            Projectile.Center = parent.Center + offset;

            // Ujung atas/kepala daun selalu mengarah keluar (menjauhi pusat Yoyo)
            Projectile.rotation = offset.ToRotation() + MathHelper.PiOver2;

            // LOGIKA MENEMBAK: HANYA BERJALAN JIKA ADA MUSUH
            NPC target = FindNearestNPC(750f);

            if (target != null)
            {
                Projectile.localAI[1]++;

                // Tembak setiap 50 ticks (~0.8 detik)
                if (Projectile.localAI[1] >= 50f && Projectile.owner == Main.myPlayer)
                {
                    Projectile.localAI[1] = 0; // Reset timer

                    // Velocity secepat kilat (26f)
                    Vector2 shootVel = (target.Center - Projectile.Center).SafeNormalize(Vector2.UnitY) * 26f;

                    int shotDamage = (int)(parent.damage * 0.50f);

                    Projectile.NewProjectile(
                        Projectile.GetSource_FromAI(),
                        Projectile.Center,
                        shootVel,
                        ModContent.ProjectileType<YeletsLeafShot>(),
                        shotDamage,
                        parent.knockBack * 0.2f,
                        Projectile.owner
                    );
                }
            }
            else
            {
                // Reset timer jika tidak ada musuh di sekitar
                Projectile.localAI[1] = 0;
            }

            Lighting.AddLight(Projectile.Center, 0.1f, 0.6f, 0.2f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Venom, 300);
        }

        private NPC FindNearestNPC(float maxDistance)
        {
            NPC nearest = null;
            float minDistance = maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy())
                {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearest = npc;
                    }
                }
            }
            return nearest;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle frameRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width * 0.5f, frameHeight * 0.5f);

            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float alpha = (1f - (i / (float)Projectile.oldPos.Length)) * 0.5f;

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    frameRect,
                    Color.Lime * alpha,
                    Projectile.oldRot[i],
                    origin,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                );
            }

            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(
                texture,
                mainPos,
                frameRect,
                Color.White,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}