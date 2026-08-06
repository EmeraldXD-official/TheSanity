using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class KrakenTyphoonAura : ModProjectile
    {
        // Menggunakan sprite sheet resmi Typhoon
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Typhoon;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.Typhoon];
            
            // After Image Trail
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 60;
            Projectile.height = 60;
            
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1; // Menembus musuh
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

            if (!parent.active || parent.type != ProjectileID.Kraken || parent.owner != Projectile.owner)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 2;
            Projectile.Center = parent.Center; // Menempel di Kraken
            Projectile.rotation += 0.12f;

            // ANIMASI SPRITE SHEET TYPHOON
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 4)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
            }

            // Lighting Cyan Neon
            Lighting.AddLight(Projectile.Center, 0.1f, 0.7f, 1.0f);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Debuff Wet & Electrified
            target.AddBuff(BuffID.Wet, 300);
            target.AddBuff(BuffID.Electrified, 180);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle frameRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width * 0.5f, frameHeight * 0.5f);

            // AFTER-IMAGE CYAN NEON
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float alpha = (1f - (i / (float)Projectile.oldPos.Length)) * 0.5f;

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    frameRect,
                    Color.Cyan * alpha,
                    Projectile.oldRot[i],
                    origin,
                    1.1f,
                    SpriteEffects.None,
                    0
                );
            }

            // MAIN SPRITE
            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(
                texture,
                mainPos,
                frameRect,
                Color.White * 0.9f,
                Projectile.rotation,
                origin,
                1.1f,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }
}