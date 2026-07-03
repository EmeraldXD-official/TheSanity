using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework.Graphics;
using System;

namespace TheSanity.Projectiles
{
    public class EclipsaStrike : ModProjectile
    {
        public override void SetStaticDefaults()
        {
            // Mengaktifkan fitur jejak (trailing)
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
        }

        public override void AI()
        {
            // Rotasi proyektil agar menghadap arah terbang
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

            // Efek partikel yang lebih halus
            if (Main.rand.NextBool(5)) // Spawn lebih jarang agar tidak memenuhi layar
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GoldFlame, 0f, 0f, 100, default, 1.2f);
            }

            // Logika Homing (diperhalus)
            NPC target = null;
            float distance = 800f;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && npc.chaseable)
                {
                    float dist = Vector2.Distance(Projectile.Center, npc.Center);
                    if (dist < distance)
                    {
                        distance = dist;
                        target = npc;
                    }
                }
            }

            if (target != null)
            {
                Vector2 direction = Vector2.Normalize(target.Center - Projectile.Center);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * 16f, 0.08f);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Menggambar jejak (Trailing)
            Texture2D texture = ModContent.Request<Texture2D>("TheSanity/Projectiles/EclipsaStrike").Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            // Efek berdenyut (Pulsing) menggunakan sinus
            float scalePulse = Projectile.scale * (0.9f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.1f);

            // Menggambar jejak di belakang proyektil
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] - Main.screenPosition + new Vector2(Projectile.width / 2, Projectile.height / 2);
                Color color = new Color(150, 0, 255, 0) * ((float)(Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(texture, drawPos, null, color, Projectile.rotation, drawOrigin, scalePulse * 0.8f, SpriteEffects.None, 0);
            }

            return true; // Gambar sprite utama
        }

        public override void PostDraw(Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>("TheSanity/Projectiles/EclipsaStrike").Value;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float scalePulse = Projectile.scale * (0.9f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.1f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // Glow Utama
            Color glowColor = new Color(150, 0, 255, 0);
            Main.EntitySpriteDraw(texture, drawPos, null, glowColor * 0.6f, Projectile.rotation, texture.Size() / 2f, scalePulse * 1.3f, SpriteEffects.None, 0);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
        

       public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
{
    // Spawn ledakan tepat di posisi musuh yang terkena
    Projectile.NewProjectile(
        Projectile.GetSource_FromThis(),
        Projectile.Center,
        Vector2.Zero,
        ModContent.ProjectileType<EclipsaExplosion>(),
        Projectile.damage/2, // Menggunakan damage dari strike
        Projectile.knockBack,
        Projectile.owner
    );
}
    }
}