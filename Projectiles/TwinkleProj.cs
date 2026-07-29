using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    // 1. PROYEKTIL BINTANG UTAMA (PHASE 1 & 2)
    public class TwinkleHostileStar : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            // Efek berputar dan glow kuning tipis
            Projectile.rotation += 0.15f * Projectile.direction;
            Lighting.AddLight(Projectile.Center, 1f, 0.9f, 0.4f);

            if (Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.GemTopaz, 0f, 0f, 100, default, 1f);
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }
        }
    }

    // 2. PROYEKTIL RAINBOW TRAIL (PHASE LAST STAND)
    public class TwinkleRainbowStar : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        public override void SetDefaults()
        {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 240;
        }

        public override void AI()
        {
            Projectile.rotation += 0.2f;
            
            // Membuat Trail Pelangi Acak (Bukan RGB bergeser, tapi warna-warni acak per frame)
            Color rainbowColor = Main.hslToRgb(Main.rand.NextFloat(), 1f, 0.6f);
            Lighting.AddLight(Projectile.Center, rainbowColor.ToVector3());

            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.RainbowMk2, 0f, 0f, 100, rainbowColor, 1.2f);
                d.noGravity = true;
                d.velocity *= 0.2f;
            }
        }
    }
}