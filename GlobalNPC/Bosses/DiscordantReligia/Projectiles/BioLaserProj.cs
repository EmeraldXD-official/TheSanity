using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Effects;
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Particles;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia.Projectiles
{
    public class BioLaserProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.GreenLaser;

        private float waveTimer = 0f;
        private const int MaxTimeLeft = 180;
        // FIX: sebelumnya velocity *= 1.01f tanpa batas, jadi laser ini terus makin cepat
        // sepanjang 180 tick lifetime-nya (~6x kecepatan awal di akhir). Disamakan pola cap-nya
        // dengan ChronoLaserProj supaya kecepatan tetap predictable/fair.
        private const float MaxSpeed = 12f;

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

            Vector2 perp = new Vector2(-Projectile.velocity.Y, Projectile.velocity.X);
            perp.Normalize();
            Projectile.position += perp * (float)Math.Sin(waveTimer) * 4f;

            if (Projectile.velocity.Length() < MaxSpeed) {
                Projectile.velocity *= 1.01f;
            }
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.CursedTorch, 0, 0, 100, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(BuffID.CursedInferno, 180);

            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 6; i++) {
                    Vector2 pVel = Main.rand.NextVector2Circular(4f, 4f);
                    ReligiaParticleManager.Particles.Add(new ReligiaEnergyParticle(Projectile.Center, pVel, Color.LimeGreen, 25, 0.9f));
                }
            }
        }

        public override void OnKill(int timeLeft) {
            if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 8; i++) {
                    Vector2 pVel = Main.rand.NextVector2Circular(5f, 5f);
                    ReligiaParticleManager.Particles.Add(new ReligiaEnergyParticle(Projectile.Center, pVel, Color.SpringGreen, 30, 1.0f));
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;
            Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            Main.spriteBatch.Draw(tex, drawPos, null, Color.LimeGreen * 0.25f, Projectile.rotation, origin, Projectile.scale * 2.0f, SpriteEffects.None, 0f);

            for (int i = 5; i >= 1; i--) {
                Vector2 trailPos = drawPos - (Projectile.velocity * i * 0.9f);
                float alpha = (6 - i) / 6f * 0.5f;
                Main.spriteBatch.Draw(tex, trailPos, null, Color.LimeGreen * alpha, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            }

            float speedFactor = MathHelper.Clamp(Projectile.velocity.Length() / 12f, 0.8f, 1.3f);
            BossGlowRenderer.DrawGlowCore(
                Main.spriteBatch, tex, drawPos, null, origin, Projectile.rotation, Projectile.scale * 1.15f,
                Color.LimeGreen, Color.PaleGreen, pulseSpeed: 7.0f, rimPower: 3.0f, intensity: 1.1f * speedFactor);

            return false;
        }
    }
}