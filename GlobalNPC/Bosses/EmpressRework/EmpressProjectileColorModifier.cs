using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.EmpressRework
{
    public class EmpressProjectileColorModifier : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        
        public int projectileIndexInSpread = -1;
        public int attackPhase = 1;
        public Vector2 initialCenter = Vector2.Zero;
        public float initialRotation = 0f;
        public bool hasLockedPosition = false;
        public bool isBossEverlasting = false;
        public int customTimer = 0; 

        public Vector2 lockedVelocity = Vector2.Zero;
        public bool hasLockedVelocity = false;

        private const int LastingRainbowLifetime = 90;

        public override void SetDefaults(Projectile projectile)
        {
            if (projectile.type == ProjectileID.HallowBossLastingRainbow)
            {
                projectile.timeLeft = LastingRainbowLifetime;
            }
        }

        public override void PostAI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.HallowBossLastingRainbow)
            {
                if (!hasLockedVelocity)
                {
                    lockedVelocity = projectile.velocity;
                    hasLockedVelocity = true;
                }

                projectile.velocity = lockedVelocity;
                projectile.rotation = lockedVelocity.ToRotation() + MathHelper.PiOver2;
            }

            if (projectile.type == ProjectileID.FairyQueenLance && projectileIndexInSpread != -1)
            {
                if (!hasLockedPosition)
                {
                    initialCenter = projectile.Center;
                    initialRotation = projectile.rotation;
                    hasLockedPosition = true;
                }
                customTimer++; 
            }
        }

        public override void OnKill(Projectile projectile, int timeLeft)
        {
            if (projectile.type == ProjectileID.HallowBossLastingRainbow && isBossEverlasting)
            {
                if (Main.netMode != NetmodeID.Server)
                {
                    Projectile.NewProjectile(projectile.GetSource_Death(), projectile.Center, Vector2.Zero, ModContent.ProjectileType<ChromaticBurstEffect>(), 0, 0f, Main.myPlayer);
                }

                for (int i = 0; i < 5; i++)
                {
                    float rotation = MathHelper.ToRadians(72f * i);
                    Vector2 initialVelocity = new Vector2(0, 1.2f).RotatedBy(rotation);
                    
                    int p = Projectile.NewProjectile(
                        projectile.GetSource_Death(), 
                        projectile.Center, 
                        initialVelocity, 
                        ModContent.ProjectileType<CustomRainbowStreak>(), 
                        22, 
                        0f, 
                        Main.myPlayer
                    );

                    if (p != Main.maxProjectiles && Main.projectile[p].ModProjectile is CustomRainbowStreak customStreak)
                    {
                        customStreak.projectileIndexInSpread = i;
                        customStreak.attackPhase = attackPhase;
                    }
                }
            }
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            if (projectileIndexInSpread == -1) return true;

            if (projectile.type == ProjectileID.HallowBossRainbowStreak || projectile.type == ProjectileID.FairyQueenLance)
            {
                Texture2D texture = TextureAssets.Projectile[projectile.type].Value;
                int frameHeight = texture.Height / Main.projFrames[projectile.type];
                int startY = frameHeight * projectile.frame;
                Rectangle sourceRectangle = new Rectangle(0, startY, texture.Width, frameHeight);
                Vector2 origin = sourceRectangle.Size() / 2f;

                float totalSpread = projectile.type == ProjectileID.HallowBossRainbowStreak ? 5f : 20f;
                float hueOffset = projectileIndexInSpread / totalSpread;
                
                float currentHue = (Main.GlobalTimeWrappedHourly * 0.25f + hueOffset) % 1f;
                Color pastelColor = Main.hslToRgb(currentHue, 0.55f, 0.8f);
                pastelColor.A = 120; 

                Vector2 drawPosition = projectile.Center - Main.screenPosition + new Vector2(0f, projectile.gfxOffY);

                if (projectile.type == ProjectileID.FairyQueenLance)
                {
                    if (!hasLockedPosition)
                    {
                        initialCenter = projectile.Center;
                        initialRotation = projectile.rotation;
                        hasLockedPosition = true;
                    }

                    float indicatorOpacity = 0f;
                    if (customTimer < 42)
                    {
                        if (customTimer <= 25) indicatorOpacity = 0.6f; 
                        else indicatorOpacity = MathHelper.Lerp(0.6f, 0f, (customTimer - 25) / 17f);
                    }

                    // Hanya menggambar garis indikator (BloomLine) tanpa ChromaticBurst
                    if (indicatorOpacity > 0f)
                    {
                        Texture2D indicatorTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/EmpressRework/BloomLine").Value;
                        Vector2 lineOrigin = new Vector2(indicatorTex.Width / 2f, indicatorTex.Height / 2f);
                        float lineRotation = initialRotation + MathHelper.PiOver2; 
                        Vector2 indicatorDrawPos = initialCenter - Main.screenPosition + new Vector2(0f, projectile.gfxOffY);

                        // Outer Glow Line
                        Color outerColor = pastelColor * (indicatorOpacity * 0.5f);
                        outerColor.A = 0; 
                        Vector2 outerScale = new Vector2(0.45f, 3500f / indicatorTex.Height);
                        Main.EntitySpriteDraw(indicatorTex, indicatorDrawPos, null, outerColor, lineRotation, lineOrigin, outerScale, SpriteEffects.None, 0);

                        // Inner Core Line
                        Color innerCore = Color.White * (indicatorOpacity * 0.75f);
                        innerCore.A = 0;
                        Vector2 innerScale = new Vector2(0.15f, 3500f / indicatorTex.Height);
                        Main.EntitySpriteDraw(indicatorTex, indicatorDrawPos, null, innerCore, lineRotation, lineOrigin, innerScale, SpriteEffects.None, 0);
                    }
                }

                Color glowColor = pastelColor * 0.25f;
                glowColor.A = 0;
                for (int j = 0; j < 4; j++)
                {
                    Vector2 glowOffset = Vector2.UnitX.RotatedBy(MathHelper.PiOver2 * j) * 3f;
                    Main.EntitySpriteDraw(texture, drawPosition + glowOffset, sourceRectangle, glowColor, projectile.rotation, origin, projectile.scale * 1.03f, SpriteEffects.None, 0);
                }

                if (projectile.type == ProjectileID.HallowBossRainbowStreak)
                {
                    for (int i = 0; i < projectile.oldPos.Length; i++)
                    {
                        if (projectile.oldPos[i] == Vector2.Zero) continue;
                        float trailFactor = 1f - (i / (float)projectile.oldPos.Length);
                        Vector2 trailPosition = projectile.oldPos[i] + projectile.Size / 2f - Main.screenPosition + new Vector2(0f, projectile.gfxOffY);
                        Main.EntitySpriteDraw(texture, trailPosition, sourceRectangle, pastelColor * trailFactor * 0.2f, projectile.oldRot[i], origin, projectile.scale * trailFactor, SpriteEffects.None, 0);
                    }
                }
                Main.EntitySpriteDraw(texture, drawPosition, sourceRectangle, pastelColor, projectile.rotation, origin, projectile.scale, SpriteEffects.None, 0);
                return false; 
            }
            return true;
        }
    }
}