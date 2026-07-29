using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia.Projectiles
{
    public class ToxicStingerProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.Stinger;

        private ref float Timer => ref Projectile.ai[0];
        private const int TelegraphDuration = 25; // 25 Frame fase sinyal/lambat awal

        public override void SetDefaults() {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 240;
        }

        public override void AI() {
            Timer++;

            // FASE 1: Lambat (Telegraph Phase)
            if (Timer <= TelegraphDuration) {
                Projectile.velocity *= 0.96f;
            }
            // FASE 2: Pergerakan Eksponensial (Langsung Cepat Banget)
            else {
                float currentSpeed = Projectile.velocity.Length();
                float newSpeed = Math.Min(currentSpeed * 1.14f + 0.6f, 28f); // Eksponensial hingga max 28f
                if (currentSpeed > 0f) {
                    Projectile.velocity = Vector2.Normalize(Projectile.velocity) * newSpeed;
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Venom, 0, 0, 100, default, 1.1f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // RENDER TELEGRAPH LINE UNTUK STINGER
            if (Timer <= TelegraphDuration) {
                Texture2D lineTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/DiscordantReligia/Assets/TelegraphLineTex").Value;
                if (lineTex != null) {
                    float progress = Timer / (float)TelegraphDuration;
                    Color lineCol = Color.Lerp(Color.SpringGreen * 0.25f, Color.Yellow * 0.85f, progress);
                    float thickness = 10f + progress * 8f;
                    Vector2 lineOrigin = new Vector2(0, lineTex.Height / 2f);
                    Vector2 scale = new Vector2(1400f / lineTex.Width, thickness / lineTex.Height);

                    Main.spriteBatch.Draw(lineTex, drawPos, null, lineCol, Projectile.velocity.ToRotation(), lineOrigin, scale, SpriteEffects.None, 0f);
                }
            }

            // Render Aset Stinger
            Main.spriteBatch.Draw(tex, drawPos, null, Color.LimeGreen, Projectile.rotation, origin, Projectile.scale * 1.4f, SpriteEffects.None, 0f);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}