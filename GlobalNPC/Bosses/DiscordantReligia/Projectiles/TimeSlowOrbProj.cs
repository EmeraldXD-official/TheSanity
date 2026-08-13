using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Particles; // WAJIB tambahkan ini di atas
using Luminance.Core.Graphics; 
using TheSanity.GlobalNPC.Bosses.DiscordantReligia.Effects;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia.Projectiles
{
    public class TimeSlowOrbProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.MagnetSphereBall;

        private const int MaxTimeLeft = 130; 

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = 5;
        }

        public override void SetDefaults() {
            Projectile.width = 44;
            Projectile.height = 44;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = 1; 
            Projectile.tileCollide = false;
            Projectile.timeLeft = MaxTimeLeft;
        }

        public override void AI() {
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 4) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Projectile.type];
            }

            Projectile.rotation += 0.05f;

            Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
            if (target != null && target.active && !target.dead) {
                Vector2 desiredVel = Vector2.Normalize(target.Center - Projectile.Center) * 4.5f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.03f);
            }

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
            SoundEngine.PlaySound(SoundID.Item93 with { Pitch = -0.3f, Volume = 1.2f }, Projectile.Center);

            // LUMINANCE FX: Screen Shake
            if (Main.netMode != NetmodeID.Server) {
                ScreenShakeSystem.StartShake(12f, 0.8f); 
            }

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

            // LUMINANCE FX: Partikel Ledakan Kosmik
           if (Main.netMode != NetmodeID.Server) {
                for (int i = 0; i < 25; i++) {
                    Vector2 pVel = Main.rand.NextVector2Circular(12f, 12f);
                    Color pColor = Main.rand.NextBool() ? Color.Violet : Color.Cyan;
                    
                    int pLife = Main.rand.Next(30, 50);
                    float pScale = Main.rand.NextFloat(0.6f, 1.2f);
                    
                    // SPAWN MENGGUNAKAN MANAGER KUSTOM KITA:
                    ReligiaParticleManager.Particles.Add(new ReligiaEnergyParticle(Projectile.Center, pVel, pColor, pLife, pScale));
                }
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

            float farHaloScale = Projectile.scale * 2.1f;
            float glowScale = Projectile.scale * 1.35f;
            Color glowColor = Color.MediumPurple * 0.7f;

            Main.spriteBatch.Draw(tex, drawPos, sourceFrame, Color.Purple * 0.2f, Projectile.rotation * 0.3f, origin, farHaloScale, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(tex, drawPos, sourceFrame, glowColor, -Projectile.rotation * 0.5f, origin, glowScale, SpriteEffects.None, 0f);

            BossGlowRenderer.DrawGlowCore(
                Main.spriteBatch, tex, drawPos, sourceFrame, origin, Projectile.rotation, Projectile.scale,
                Color.Violet, Color.MediumPurple, pulseSpeed: 8.0f, rimPower: 2.4f, intensity: 1.15f);

            return false;
        }
    }
}