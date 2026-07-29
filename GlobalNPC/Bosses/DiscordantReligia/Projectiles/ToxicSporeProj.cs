using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Effects;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia.Projectiles
{
    public class ToxicSporeProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.SporeTrap;

        private const int MaxTimeLeft = 90; // 1.5 Detik total sebelum meledak

        public override void SetDefaults() {
            Projectile.width = 24;
            Projectile.height = 24;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = MaxTimeLeft;
        }

        public override void AI() {
            Projectile.rotation += 0.08f;
            Projectile.velocity *= 0.92f; // Melaju lebih jauh dulu sebelum berhenti jadi ranjau diam, biar ring 360 tidak numpuk deket boss

            float progress = 1f - ((float)Projectile.timeLeft / MaxTimeLeft); // 0.0 -> 1.0

            // TELEGRAPH VISUAL: Ring indikator membesar sesuai progress ledakan
            float ringRadius = 18f + (progress * 62f);
            for (int i = 0; i < 4; i++) {
                Vector2 dustOffset = MathHelper.ToRadians(i * 90 + Projectile.timeLeft * 8).ToRotationVector2() * ringRadius;
                Dust d = Dust.NewDustDirect(Projectile.Center + dustOffset, 0, 0, DustID.CursedTorch, 0, 0, 100, default, 1.2f);
                d.noGravity = true;
                d.velocity = Vector2.Zero;
            }

            // Pulsing scale saat mendekati detik ledakan
            Projectile.scale = 1f + (float)Math.Sin(progress * MathHelper.TwoPi * 4) * 0.25f;
        }

        public override void OnKill(int timeLeft) {
            // LEDAKAN UTAMA (EXECUTION)
            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.2f, Volume = 0.9f }, Projectile.Center);

            // Partikel ledakan AOE besar
            for (int i = 0; i < 28; i++) {
                Vector2 dustVel = Main.rand.NextVector2Circular(7f, 7f);
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.CursedTorch, dustVel.X, dustVel.Y, 100, default, 2f);
                d.noGravity = true;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(BuffID.Venom, 240);
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            float progress = 1f - ((float)Projectile.timeLeft / MaxTimeLeft);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // Faint far halo, subtle at spawn and more present as it nears detonation
            Main.spriteBatch.Draw(tex, drawPos, null, Color.SpringGreen * (0.15f + progress * 0.25f), Projectile.rotation, origin, Projectile.scale * 2.2f, SpriteEffects.None, 0f);

            // Warning Aura merah-hijau yang makin terang mendekati ledakan
            Color glowColor = Color.Lerp(Color.SpringGreen, Color.Red, progress) * (0.4f + progress * 0.6f);
            Main.spriteBatch.Draw(tex, drawPos, null, glowColor, Projectile.rotation, origin, Projectile.scale * (1.2f + progress * 0.5f), SpriteEffects.None, 0f);

            // Core spore dengan shader; warna & kecepatan pulse ikut naik mendekati waktu ledakan
            Effect shader = BossShaderLoader.BossGlowShader;
            if (shader != null) {
                shader.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                shader.Parameters["uColor"]?.SetValue(Color.Lerp(Color.SpringGreen, Color.Red, progress).ToVector4());
                shader.Parameters["uSecondaryColor"]?.SetValue(Color.White.ToVector4());
                shader.Parameters["uPulseSpeed"]?.SetValue(6.0f + progress * 8.0f);
                shader.Parameters["uRimPower"]?.SetValue(2.5f);
                shader.Parameters["uIntensity"]?.SetValue(1.0f + progress * 0.4f);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, shader, Main.GameViewMatrix.TransformationMatrix);

                Main.spriteBatch.Draw(tex, drawPos, null, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            }
            else {
                Main.spriteBatch.Draw(tex, drawPos, null, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}