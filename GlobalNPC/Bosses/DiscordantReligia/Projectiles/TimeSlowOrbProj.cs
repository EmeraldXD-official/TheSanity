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
    public class TimeSlowOrbProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MagnetSphereBall;

        private const int MaxTimeLeft = 130; // Explodes after 2.1 seconds if it doesn't hit

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 5;
        }

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 44;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1; // Explodes on hit or when timer expires
            Projectile.tileCollide = false;
            Projectile.timeLeft = MaxTimeLeft;
        }

        public override void AI() {
            // Animate frames
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 4) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
            }

            Projectile.rotation += 0.05f;

            // Slow, heavy homing towards the target
            Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            if (target != null && target.active && !target.dead) {
                Vector2 desiredVel = Vector2.Normalize(target.Center - Projectile.Center) * 4.5f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.03f);
            }

            // Pulsing scale for a giant, intimidating orb look
            float pulse = 2.2f + (float)Math.Sin((MaxTimeLeft - Projectile.timeLeft) * 0.18f) * 0.35f;
            Projectile.scale = pulse;

            if (Main.rand.NextBool(2)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0, 0, 100, default, 1.8f);
                d.noGravity = true;
                d.velocity *= 0.4f;
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(BuffID.Slow, 180);
        }

        public override void OnKill(int timeLeft) {
            // Sound effect when the giant sphere collapses
            SoundEngine.PlaySound(SoundID.Item93 with { Pitch = -0.3f, Volume = 1.2f }, Projectile.Center);

            // EXPLOSION PATTERN: Spawns 10 lasers firing outward in a 360 degree ring
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                int laserCount = 10;
                for (int i = 0; i < laserCount; i++) {
                    Vector2 laserVel = MathHelper.ToRadians((360f / laserCount) * i).ToRotationVector2() * 8.5f;
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(), 
                        Projectile.Center, 
                        laserVel, 
                        ModContent.ProjectileType<ChronoLaserProj>(), 
                        22, 
                        1f, 
                        Main.myPlayer
                    );
                }
            }

            // Particle shockwave
            for (int i = 0; i < 30; i++) {
                Vector2 dustVel = Main.rand.NextVector2Circular(8f, 8f);
                Dust d = Dust.NewDustDirect(Projectile.Center, 0, 0, DustID.Shadowflame, dustVel.X, dustVel.Y, 100, default, 2.2f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D tex = TextureAssets.Projectile[Type].Value;

            int numFrames = Main.projFrames[Projectile.type];
            int frameHeight = tex.Height / numFrames;
            Rectangle sourceFrame = new Rectangle(0, Projectile.frame * frameHeight, tex.Width, frameHeight);
            Vector2 origin = new Vector2(tex.Width / 2f, frameHeight / 2f);

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            // Giant glowing aura, layered from a faint far halo down to the bright core
            float farHaloScale = Projectile.scale * 2.1f;
            float glowScale = Projectile.scale * 1.35f;
            Color glowColor = Color.MediumPurple * 0.7f;

            Main.spriteBatch.Draw(tex, drawPos, sourceFrame, Color.Purple * 0.2f, Projectile.rotation * 0.3f, origin, farHaloScale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, sourceFrame, glowColor, -Projectile.rotation * 0.5f, origin, glowScale, SpriteEffects.None, 0f);

            // Core orb dengan shader rim/energy pulse, senada dengan visual boss-nya
            Effect shader = BossShaderLoader.BossGlowShader;
            if (shader != null) {
                shader.Parameters["uTime"]?.SetValue((float)Main.GlobalTimeWrappedHourly);
                shader.Parameters["uColor"]?.SetValue(Color.Violet.ToVector4());
                shader.Parameters["uSecondaryColor"]?.SetValue(Color.MediumPurple.ToVector4());
                shader.Parameters["uPulseSpeed"]?.SetValue(8.0f);
                shader.Parameters["uRimPower"]?.SetValue(2.4f);
                shader.Parameters["uIntensity"]?.SetValue(1.15f);

                Main.spriteBatch.End();
                Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, shader, Main.GameViewMatrix.TransformationMatrix);

                Main.spriteBatch.Draw(tex, drawPos, sourceFrame, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            }
            else {
                Main.spriteBatch.Draw(tex, drawPos, sourceFrame, Color.Violet, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}