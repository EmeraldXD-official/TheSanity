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
        public Vector2 initialCenter = Vector2.Zero;
        public float initialRotation = 0f;
        public bool hasLockedPosition = false;
        public bool isBossEverlasting = false;
        public int customTimer = 0; 

        // Dipakai buat mengunci arah HallowBossLastingRainbow supaya dia jalan lurus, tidak melengkung/berputar
        public Vector2 lockedVelocity = Vector2.Zero;
        public bool hasLockedVelocity = false;

        // Persingkat jeda (dalam tick) sebelum HallowBossLastingRainbow "meledak" jadi HallowBossRainbowStreak
        // Vanilla defaultnya cukup lama, angka ini yang menentukan seberapa cepat dia meledak. Tinggal disesuaikan.
        private const int LastingRainbowLifetime = 90;

        public override void SetDefaults(Projectile projectile)
        {
            if (projectile.type == ProjectileID.HallowBossLastingRainbow)
            {
                // Ini yang bikin jeda sebelum meledak jadi lebih singkat.
                projectile.timeLeft = LastingRainbowLifetime;
            }
        }

        public override void PostAI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.HallowBossLastingRainbow)
            {
                // Kunci arah gerak di tick pertama, jadi dia jalan lurus terus, tidak melengkung
                if (!hasLockedVelocity)
                {
                    lockedVelocity = projectile.velocity;
                    hasLockedVelocity = true;
                }

                // Paksa kembali ke arah awal tiap tick (nge-override hasil AI vanilla yang bikin dia melengkung)
                projectile.velocity = lockedVelocity;

                // Kunci rotasi searah gerak, jadi dia tidak berputar/spin sendiri
                // +PiOver2 karena sprite HallowBossLastingRainbow default-nya menghadap ke atas (bukan ke kanan)
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
                    Vector2 velocity = new Vector2(0, 6f).RotatedBy(rotation);
                    int p = Projectile.NewProjectile(projectile.GetSource_Death(), projectile.Center, velocity, ProjectileID.HallowBossRainbowStreak, 25, 0f, Main.myPlayer);
                    
                    if (p != Main.maxProjectiles) {
                        Main.projectile[p].GetGlobalProjectile<EmpressProjectileColorModifier>().projectileIndexInSpread = i;
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
                        if (customTimer <= 25) indicatorOpacity = 0.7f; 
                        else indicatorOpacity = MathHelper.Lerp(0.7f, 0f, (customTimer - 25) / 17f);
                    }

                    if (indicatorOpacity > 0f)
                    {
                        Texture2D indicatorTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/EmpressRework/BloomLine").Value;
                        Vector2 lineOrigin = new Vector2(indicatorTex.Width / 2f, indicatorTex.Height / 2f);
                        float lineRotation = initialRotation + MathHelper.PiOver2; 
                        Vector2 lineScale = new Vector2(0.6f, 3500f / indicatorTex.Height);
                        Vector2 indicatorDrawPos = initialCenter - Main.screenPosition + new Vector2(0f, projectile.gfxOffY);
                        Color indicatorColor = pastelColor * indicatorOpacity;
                        indicatorColor.A = 0; 
                        Main.EntitySpriteDraw(indicatorTex, indicatorDrawPos, null, indicatorColor * 0.7f, lineRotation, lineOrigin, lineScale, SpriteEffects.None, 0);
                        
                        Texture2D burstTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/EmpressRework/ChromaticBurst").Value;
                        float burstProgress = customTimer / 42f;
                        Main.EntitySpriteDraw(burstTex, indicatorDrawPos, null, pastelColor * MathHelper.Lerp(0.4f, 0f, burstProgress), 0f, burstTex.Size() / 2f, MathHelper.Lerp(2.5f, 0.1f, burstProgress), SpriteEffects.None, 0);
                    }
                }

                Color glowColor = pastelColor * 0.3f;
                glowColor.A = 0;
                for (int j = 0; j < 4; j++)
                {
                    Vector2 glowOffset = Vector2.UnitX.RotatedBy(MathHelper.PiOver2 * j) * 4f;
                    Main.EntitySpriteDraw(texture, drawPosition + glowOffset, sourceRectangle, glowColor, projectile.rotation, origin, projectile.scale * 1.05f, SpriteEffects.None, 0);
                }

                if (projectile.type == ProjectileID.HallowBossRainbowStreak)
                {
                    for (int i = 0; i < projectile.oldPos.Length; i++)
                    {
                        if (projectile.oldPos[i] == Vector2.Zero) continue;
                        float trailFactor = 1f - (i / (float)projectile.oldPos.Length);
                        Vector2 trailPosition = projectile.oldPos[i] + projectile.Size / 2f - Main.screenPosition + new Vector2(0f, projectile.gfxOffY);
                        Main.EntitySpriteDraw(texture, trailPosition, sourceRectangle, pastelColor * trailFactor * 0.25f, projectile.oldRot[i], origin, projectile.scale * trailFactor, SpriteEffects.None, 0);
                    }
                }
                Main.EntitySpriteDraw(texture, drawPosition, sourceRectangle, pastelColor, projectile.rotation, origin, projectile.scale, SpriteEffects.None, 0);
                return false; 
            }
            return true;
        }
    }
}