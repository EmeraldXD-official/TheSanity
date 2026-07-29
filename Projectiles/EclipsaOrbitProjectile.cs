using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TheSanity.Dusts;
using Microsoft.Xna.Framework.Graphics;
using Terraria.Audio; 
using System;
namespace TheSanity.Projectiles
{
    public class EclipsaOrbitProjectile : ModProjectile
    {
        public override void SetDefaults()
        {
            Projectile.width = 16; Projectile.height = 16;
            Projectile.friendly = false; Projectile.tileCollide = false;
            Projectile.timeLeft = 600;
            
        }
public override void OnSpawn(Terraria.DataStructures.IEntitySource source)
{
    Player player = Main.player[Projectile.owner];
    // Hitung posisi awal berdasarkan sudut ai[0]
    Vector2 offset = new Vector2(
        (float)System.Math.Cos(Projectile.ai[0]) * 100f, 
        (float)System.Math.Sin(Projectile.ai[0]) * 60f
    );
    Projectile.Center = player.Center + offset;
}

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];
            Projectile.ai[0] += 0.05f;
            Vector2 offset = new Vector2(
        (float)System.Math.Cos(Projectile.ai[0]) * 100f, 
        (float)System.Math.Sin(Projectile.ai[0]) * 60f
            );
           Projectile.Center = player.Center + offset;
    Projectile.rotation = Projectile.ai[0] + MathHelper.PiOver2;

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, ModContent.DustType<EclipsaParticle>(), 0, 0, 100, Color.White, 1.2f);
                d.velocity *= 0.2f; d.noGravity = true;
            }
        }

        public void FireAsMainProjectile()
{
    if (Projectile.owner == Main.myPlayer) {
        // Cukup spawn dengan velocity (0,0), dia akan otomatis bergerak ke atas karena logika AI di atas
        Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Vector2.Zero, 
            ModContent.ProjectileType<EclipsaProjectile>(), Projectile.damage, 4f, Projectile.owner);
    }
    Projectile.Kill();
}
        public override bool PreDraw(ref Color lightColor)
{
    Texture2D texture = ModContent.Request<Texture2D>("TheSanity/Projectiles/EclipsaProjectile").Value;
    Vector2 origin = new Vector2(texture.Width / 2f, texture.Height / 2f);
    Vector2 drawPos = Projectile.Center - Main.screenPosition;

    // 1. Efek Berdetak (Pulsing)
    float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.1f + 1f;
    float currentScale = Projectile.scale * pulse;

    // 2. Gambar Utama
    Main.EntitySpriteDraw(texture, drawPos, null, lightColor, Projectile.rotation, origin, currentScale, SpriteEffects.None, 0);

    // 3. Efek Glow (Additive Blending)
    Main.spriteBatch.End();
    Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

    // Menggambar aura cahaya ungu yang berdetak
    Main.EntitySpriteDraw(texture, drawPos, null, Color.Purple * 0.4f, Projectile.rotation, origin, currentScale + 0.2f, SpriteEffects.None, 0);

    Main.spriteBatch.End();
    Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

    return false;
}
    }
}