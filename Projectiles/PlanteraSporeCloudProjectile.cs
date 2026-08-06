using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class PlanteraSporeCloudProjectile : ModProjectile
    {
        // Menggunakan sprite sheet SporeCloud vanilla
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SporeCloud;

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.SporeCloud];
        }

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360; // Lifetime 6 Detik (360 Ticks)
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;

            Projectile.alpha = 255; // Dimulai dari transparan (fade in)
        }

        public override void AI()
        {
            // Animasi Frame Sprite Sheet
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 6)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
            }

            // Gerakan Lambat & Drift Acak (Kiri/Kanan/Atas/Bawah)
            Projectile.velocity *= 0.95f; // Perlambatan agar jadi awan lambat
            Projectile.velocity += Main.rand.NextVector2Circular(0.08f, 0.08f); // Gerak acak halus

            // Rotasi perlahan
            Projectile.rotation += 0.01f * (Projectile.identity % 2 == 0 ? 1 : -1);

            // Efek Fade In di awal & Fade Away di akhir lifetime (kurang dari 1 detik tersisa)
            if (Projectile.timeLeft > 330)
            {
                Projectile.alpha = Math.Max(50, Projectile.alpha - 15); // Fade in
            }
            else if (Projectile.timeLeft < 60)
            {
                Projectile.alpha = Math.Min(255, Projectile.alpha + 5); // Fade away saat mau hilang
            }
            else
            {
                Projectile.alpha = 50;
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Memberikan debuff Poison (6 detik) dan Venom (4 detik)
            target.AddBuff(BuffID.Poisoned, 360);
            target.AddBuff(BuffID.Venom, 240);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / Main.projFrames[Projectile.type];
            Rectangle sourceRectangle = new Rectangle(0, frameHeight * Projectile.frame, texture.Width, frameHeight);
            Vector2 origin = sourceRectangle.Size() / 2f;

            Color drawColor = lightColor * ((255 - Projectile.alpha) / 255f);

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                sourceRectangle,
                drawColor,
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