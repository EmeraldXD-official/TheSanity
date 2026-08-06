using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class YeletsLeafShot : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CrystalLeafShot;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.CrystalLeafShot];

            // Setup After-Image Lime
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = 1;
            Projectile.tileCollide = true;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 180;

            // Ekstra update frame untuk efek pergerakan kilat yang mulus
            Projectile.extraUpdates = 1; 
        }

        public override void AI()
        {
            // Karena sprite dasar menghadap KIRI, kita balik 180 derajat (+ Pi) ke arah Velocity
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.Pi;

            Lighting.AddLight(Projectile.Center, 0.2f, 0.8f, 0.2f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // 10% Chance Venom Debuff
            if (Main.rand.NextBool(10))
            {
                target.AddBuff(BuffID.Venom, 180);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle frameRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width * 0.5f, frameHeight * 0.5f);

            // 1. DRAW AFTER-IMAGE LIME / HIJAU MUDA
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float alpha = (1f - (i / (float)Projectile.oldPos.Length)) * 0.6f;

                Color limeTrail = Color.Lime * alpha; // Warna Lime / Hijau Muda

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    frameRect,
                    limeTrail,
                    Projectile.oldRot[i],
                    origin,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                );
            }

            // 2. DRAW MAIN SPRITE
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