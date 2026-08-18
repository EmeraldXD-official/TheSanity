using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace YourModName.Content.Projectiles
{
    public class SunfuryFireball : ModProjectile
    {
        // Menggunakan sprite sheet CultistBossFireBall (ID: 466)
        public override string Texture => "Terraria/Images/Projectile_466";

        public override void SetStaticDefaults()
        {
            // Ambil jumlah frame asli CultistBossFireBall secara otomatis
            Main.projFrames[Type] = Main.projFrames[ProjectileID.CultistBossFireBall];
        }

        public override void SetDefaults()
        {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false; // TEMBUS BLOK
            Projectile.penetrate = 1;       // Hancur setelah mengenai 1 musuh
            Projectile.timeLeft = 240;      // Despawn setelah 4 detik jika tidak terkena apa-apa
        }

        public override void AI()
        {
            // Rotasi menghadap ke arah terbang
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // Loop Animasi Sprite Sheet
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 4) // Kecepatan animasi
            {
                Projectile.frameCounter = 0;
                Projectile.frame++;
                if (Projectile.frame >= Main.projFrames[Type])
                {
                    Projectile.frame = 0;
                }
            }

            // Partikel Api di sepanjang lintasan
            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 1.5f);
                d.noGravity = true;
                d.velocity *= 0.3f;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Memberikan debuff OnFire3 (Hellfire) selama 4 detik (240 ticks)
            target.AddBuff(BuffID.OnFire3, 240);
        }

        // =======================================================
        // PRE-DRAW: RENDER SPRITE SHEET CULTIST FIREBALL PRESISI
        // =======================================================
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Type].Value;

            int frameHeight = texture.Height / Main.projFrames[Type];
            Rectangle sourceRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);

            Vector2 origin = sourceRect.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // Render bola api dengan efek terang/glowing (Color.White)
            Main.EntitySpriteDraw(
                texture,
                drawPos,
                sourceRect,
                Color.White,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false; // Matikan default draw tModLoader
        }
    }
}