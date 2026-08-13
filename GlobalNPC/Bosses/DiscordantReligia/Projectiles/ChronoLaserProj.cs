using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Effects;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia.Projectiles
{
    public class ChronoLaserProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.PurpleLaser;

        private const int MaxTimeLeft = 200;

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 2;
            Projectile.tileCollide = false;
            Projectile.timeLeft = MaxTimeLeft;
        }

        public override void AI() {
            if (Projectile.velocity.Length() < 18f) {
                Projectile.velocity *= 1.025f;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0, 0, 100, default, 1.1f);
                d.noGravity = true;
                d.velocity = -Projectile.velocity * 0.2f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // 1. HALO LUAR (Soft Bloom): Dibuat transparan agar memberikan efek pendaran lembut di sekitar laser
            Main.spriteBatch.Draw(tex, drawPos, null, Color.MediumPurple * 0.2f, Projectile.rotation, origin, Projectile.scale * 2.2f, SpriteEffects.None, 0f);

            // 2. MOTION TRAIL: Jejak ekor beruntun yang menunjukkan arah laju proyektil
            for (int i = 6; i >= 1; i--) {
                Vector2 trailPos = drawPos - (Projectile.velocity * i * 0.8f);
                float alpha = (7 - i) / 7f * 0.5f;
                Main.spriteBatch.Draw(tex, trailPos, null, Color.MediumPurple * alpha, Projectile.rotation, origin, Projectile.scale * 0.9f, SpriteEffects.None, 0f);
            }

            // 3. CORE DENGAN SHADER (BossGlowShader)
            // DINAMIKA BLOOM: Intensitas cahaya merespons kecepatan laju proyektil
            float speedFactor = MathHelper.Clamp(Projectile.velocity.Length() / 18f, 0.8f, 1.4f);
            BossGlowRenderer.DrawGlowCore(
                Main.spriteBatch, tex, drawPos, null, origin, Projectile.rotation, Projectile.scale * 1.15f,
                Color.MediumPurple, Color.Cyan, pulseSpeed: 7.0f, rimPower: 2.5f, intensity: 1.1f * speedFactor);

            return false;
        }
    }
}