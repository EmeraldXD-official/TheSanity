using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.EmpressRework
{
    public class CustomRainbowStreak : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/EmpressRework/ChromaticBurst";

        private const int TotalLifetime = 240;
        private const int FadeInDuration = 20;
        private const int FadeOutDuration = 35;

        public int projectileIndexInSpread = 0;
        public int attackPhase = 1;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 16;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = TotalLifetime;
            Projectile.alpha = 255;
            Projectile.extraUpdates = 1;
        }

        public override void AI()
        {
            float speedMultiplier = attackPhase switch
            {
                3 => 1.04f,
                2 => 1.03f,
                _ => 1.02f
            };

            float maxSpeed = attackPhase switch
            {
                3 => 21f,
                2 => 18f,
                _ => 15f
            };

            if (Projectile.velocity != Vector2.Zero)
            {
                Vector2 moveDirection = Vector2.Normalize(Projectile.velocity);
                float currentSpeed = Projectile.velocity.Length();

                currentSpeed = MathHelper.Min(currentSpeed * speedMultiplier, maxSpeed);
                Projectile.velocity = moveDirection * currentSpeed;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(4))
            {
                Dust d = Dust.NewDustDirect(Projectile.Center, 2, 2, DustID.RainbowMk2, 0f, 0f, 150, default, 0.75f);
                d.noGravity = true;
                d.velocity *= 0.1f;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.HallowBossRainbowStreak].Value;
            Texture2D burstTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/EmpressRework/ChromaticBurst").Value;

            int frameCount = Main.projFrames[ProjectileID.HallowBossRainbowStreak];
            if (frameCount < 1) frameCount = 1;
            int frameHeight = texture.Height / frameCount;
            int startY = frameHeight * Projectile.frame;
            Rectangle sourceRectangle = new Rectangle(0, startY, texture.Width, frameHeight);
            Vector2 origin = sourceRectangle.Size() / 2f;

            int timeLived = TotalLifetime - Projectile.timeLeft;
            float opacity = 1f;

            if (timeLived < FadeInDuration)
            {
                opacity = timeLived / (float)FadeInDuration;
            }
            else if (Projectile.timeLeft < FadeOutDuration)
            {
                opacity = Projectile.timeLeft / (float)FadeOutDuration;
            }

            float hueOffset = projectileIndexInSpread / 7f;
            float currentHue = (Main.GlobalTimeWrappedHourly * 0.25f + hueOffset) % 1f;
            Color pastelColor = Main.hslToRgb(currentHue, 0.6f, 0.85f) * opacity;
            pastelColor.A = 0;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // Gambar Trail (Ekor)
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float trailFactor = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                float trailHue = (currentHue + i * 0.02f) % 1f;
                Color trailColor = Main.hslToRgb(trailHue, 0.6f, 0.85f) * (opacity * trailFactor * 0.35f);
                trailColor.A = 0;

                Main.EntitySpriteDraw(texture, trailPos, sourceRectangle, trailColor, Projectile.oldRot[i], origin, Projectile.scale * trailFactor, SpriteEffects.None, 0);
            }

            // Head Flare Core (Skala & Opacity diturunkan)
            Main.EntitySpriteDraw(burstTex, drawPos, null, pastelColor * 0.25f, Projectile.rotation, burstTex.Size() / 2f, Projectile.scale * 0.65f, SpriteEffects.None, 0);

            // Proyektil Utama
            Main.EntitySpriteDraw(texture, drawPos, sourceRectangle, pastelColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}