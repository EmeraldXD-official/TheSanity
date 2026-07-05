using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using System;
namespace TheSanity.Projectiles
{
    // Bintang Biasa dengan afterimage dan efek gerakan
    public class StarProj : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            // Menambahkan efek cahaya (opsional)
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 8; // jumlah afterimage
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = true;
            Projectile.penetrate = 2; // bisa menembus
            Projectile.timeLeft = 300; // durasi
        }

        public override void AI()
        {
            // Rotasi konstan
            Projectile.rotation += 0.25f;

            // Efek berkedip (skala berubah sedikit)
            Projectile.scale = 1f + 0.1f * (float)Math.Sin(Main.GameUpdateCount * 0.1f);

            // Trail partikel (afterimage tambahan)
            if (Main.rand.NextBool(2)) // 50% per frame
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.YellowStarDust, 0, 0, 100, default, 0.6f);
                dust.noGravity = true;
                dust.velocity = -Projectile.velocity * 0.3f + new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-0.5f, 0.5f));
                dust.fadeIn = 0.4f;
                dust.scale = 0.5f + Main.rand.NextFloat(0.3f);
            }

            // Cahaya redup (opsional)
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.8f, 0.3f) * 0.3f);
        }

        // Gambar afterimage dari oldPos
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>("TheSanity/Projectiles/StarProj").Value;
            Vector2 origin = texture.Size() / 2;

            // Gambar jejak (afterimage) dari posisi sebelumnya
            for (int i = 1; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) break;
                float alpha = 1f - (float)i / Projectile.oldPos.Length;
                Color color = lightColor * alpha * 0.4f;
                Main.EntitySpriteDraw(texture, Projectile.oldPos[i] + origin - Main.screenPosition, null, color, Projectile.oldRot[i], origin, Projectile.scale * 0.8f, SpriteEffects.None, 0);
            }

            // Gambar utama
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    // Super Star dengan efek RGB dan afterimage lebih besar
    public class SuperStarProj : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 12;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = true;
            Projectile.penetrate = 2;
            Projectile.timeLeft = 400;
        }

        public override void AI()
        {
            // Rotasi lebih cepat
            Projectile.rotation += 0.4f;

            // Skala berdenyut dengan warna RGB
            float pulse = 1f + 0.15f * (float)Math.Sin(Main.GameUpdateCount * 0.08f);
            Projectile.scale = pulse;

            // Trail partikel warna-warni
            if (Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.RainbowTorch, 0, 0, 100, Main.DiscoColor, 1.2f);
                dust.noGravity = true;
                dust.velocity = -Projectile.velocity * 0.3f + new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1f, 1f));
                dust.fadeIn = 0.3f;
                dust.scale = 0.8f + Main.rand.NextFloat(0.6f);
            }

            // Tambahan bintang kecil di sekitar
            if (Main.rand.NextBool(3))
            {
                Vector2 offset = new Vector2(Main.rand.NextFloat(-20, 20), Main.rand.NextFloat(-20, 20));
                Dust dust = Dust.NewDustDirect(Projectile.Center + offset, 0, 0, DustID.YellowStarDust, 0, 0, 100, default, 0.5f);
                dust.noGravity = true;
                dust.velocity = Vector2.Zero;
                dust.fadeIn = 0.3f;
            }

            // Cahaya lebih terang
            Lighting.AddLight(Projectile.Center, new Vector3(1f, 0.5f, 0.8f) * 0.5f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>("TheSanity/Projectiles/StarProj").Value;
            Vector2 origin = texture.Size() / 2;
            Color baseColor = Main.DiscoColor;

            // Gambar afterimage dengan warna RGB
            for (int i = 1; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) break;
                float alpha = 1f - (float)i / Projectile.oldPos.Length;
                Color color = baseColor * alpha * 0.5f;
                Main.EntitySpriteDraw(texture, Projectile.oldPos[i] + origin - Main.screenPosition, null, color, Projectile.oldRot[i], origin, Projectile.scale * 0.7f, SpriteEffects.None, 0);
            }

            // Gambar utama
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, baseColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void OnKill(int timeLeft)
        {
            // Ledakan spektakuler dengan berbagai jenis dust
            int count = 80;
            for (int i = 0; i < count; i++)
            {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-15f, 15f), Main.rand.NextFloat(-15f, 15f));
                int dustType;
                float scale = Main.rand.NextFloat(1f, 3f);
                int choice = Main.rand.Next(5);
                Color color = default;
                switch (choice)
                {
                    case 0: dustType = DustID.YellowStarDust; break;
                    case 1: dustType = DustID.Smoke; scale *= 1.8f; break;
                    case 2: dustType = DustID.Torch; break;
                    case 3: dustType = DustID.RainbowTorch; color = Main.DiscoColor; break;
                    default: dustType = DustID.RainbowTorch; color = Main.DiscoColor; break;
                }

                Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, dustType, vel.X, vel.Y, 100, color, scale);
                dust.noGravity = (dustType != DustID.Smoke);
                dust.fadeIn = 0.4f;
                if (dustType == DustID.RainbowTorch )
                    dust.color = Main.DiscoColor;
            }

            // Gelombang kejut (shockwave) berbentuk cincin
            for (int i = 0; i < 40; i++)
            {
                float angle = i / 40f * MathHelper.TwoPi;
                Vector2 vel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 10f;
                Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Smoke, vel.X, vel.Y, 100, Color.White, 2.5f);
                dust.noGravity = true;
                dust.fadeIn = 0.2f;
            }

            // Kilatan putih besar
            for (int i = 0; i < 15; i++)
            {
                Vector2 vel = new Vector2(Main.rand.NextFloat(-8f, 8f), Main.rand.NextFloat(-8f, 8f));
                Dust dust = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.WhiteTorch, vel.X, vel.Y, 100, Color.White, 2f);
                dust.noGravity = true;
                dust.fadeIn = 0.2f;
            }
        }
    }
}