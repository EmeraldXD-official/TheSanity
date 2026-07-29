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
                // Akselerasi bertahap agar terasa ada dorongan energi
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

                // Faint outer halo - large and soft, gives bloom something to grab onto
                Main.spriteBatch.Draw(tex, drawPos, null, Color.MediumPurple * 0.25f, Projectile.rotation, origin, Projectile.scale * 2.0f, SpriteEffects.None, 0f);

                // Motion trail beruntun di belakang proyektil
                for (int i = 6; i >= 1; i--) {
                    Vector2 trailPos = drawPos - (Projectile.velocity * i * 0.8f);
                    float alpha = (7 - i) / 7f * 0.6f;
                    Main.spriteBatch.Draw(tex, trailPos, null, Color.MediumPurple * alpha, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
                }

                // Proyektil utama dengan glow terang, ditambah shader rim/energy agar senada dengan boss
                Effect shader = BossShaderLoader.BossGlowShader;
                if (shader != null) {
                    shader.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                    shader.Parameters["uColor"]?.SetValue(Color.MediumPurple.ToVector4());
                    shader.Parameters["uSecondaryColor"]?.SetValue(Color.Cyan.ToVector4());
                    shader.Parameters["uPulseSpeed"]?.SetValue(6.5f);
                    shader.Parameters["uRimPower"]?.SetValue(2.75f);
                    shader.Parameters["uIntensity"]?.SetValue(1.05f);

                    Main.spriteBatch.End();
                    Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, shader, Main.GameViewMatrix.TransformationMatrix);

                    Main.spriteBatch.Draw(tex, drawPos, null, Color.White, Projectile.rotation, origin, Projectile.scale * 1.1f, SpriteEffects.None, 0f);
                }
                else {
                    Main.spriteBatch.Draw(tex, drawPos, null, Color.White, Projectile.rotation, origin, Projectile.scale * 1.1f, SpriteEffects.None, 0f);
                }

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

                return false;
            }
        }
    }