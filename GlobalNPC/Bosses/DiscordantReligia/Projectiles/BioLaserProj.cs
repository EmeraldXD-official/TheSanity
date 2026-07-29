using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Effects;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia.Projectiles
{
    public class BioLaserProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.GreenLaser;

        private float waveTimer = 0f;
        private const int MaxTimeLeft = 180;

        public override void SetDefaults() {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 2;
            Projectile.tileCollide = false;
            Projectile.timeLeft = MaxTimeLeft;
        }

        public override void AI() {
            waveTimer += 0.35f;

            // Pergerakan meliuk bergelombang
            Vector2 perp = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X);
            perp.Normalize();
            Projectile.position += perp * (float)Math.Sin(waveTimer) * 4f;

            Projectile.velocity *= 1.01f;
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.CursedTorch, 0, 0, 100, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(BuffID.CursedInferno, 180);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // Faint outer halo for extra bloom presence
            Main.spriteBatch.Draw(tex, drawPos, null, Color.LimeGreen * 0.25f, Projectile.rotation, origin, Projectile.scale * 2.0f, SpriteEffects.None, 0f);

            for (int i = 5; i >= 1; i--) {
                Vector2 trailPos = drawPos - (Projectile.velocity * i * 0.9f);
                float alpha = (6 - i) / 6f * 0.5f;
                Main.spriteBatch.Draw(tex, trailPos, null, Color.LimeGreen * alpha, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            }

            Effect shader = BossShaderLoader.BossGlowShader;
            if (shader != null) {
                shader.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                shader.Parameters["uColor"]?.SetValue(Color.LimeGreen.ToVector4());
                shader.Parameters["uSecondaryColor"]?.SetValue(Color.PaleGreen.ToVector4());
                shader.Parameters["uPulseSpeed"]?.SetValue(7.0f);
                shader.Parameters["uRimPower"]?.SetValue(3.0f);
                shader.Parameters["uIntensity"]?.SetValue(1.1f);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, shader, Main.GameViewMatrix.TransformationMatrix);

                Main.spriteBatch.Draw(tex, drawPos, null, Color.White, Projectile.rotation, origin, Projectile.scale * 1.15f, SpriteEffects.None, 0f);
            }
            else {
                Main.spriteBatch.Draw(tex, drawPos, null, Color.White, Projectile.rotation, origin, Projectile.scale * 1.15f, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}