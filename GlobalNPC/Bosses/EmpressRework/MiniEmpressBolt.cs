using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.EmpressRework
{
    public class MiniEmpressBolt : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/EmpressRework/MiniEmpressBolt";

        public bool isLiteClone = false;
        public bool canSplitOnExpire = false;

        private const int SplitShardCount = 2;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.alpha = 0;
            Projectile.extraUpdates = 1;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (!isLiteClone && Projectile.ai[0] < 20f)
            {
                Projectile.ai[0]++;
                Projectile.velocity *= 1.01f;
            }

            int dustFreq = isLiteClone ? 5 : 4;
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(dustFreq))
            {
                Dust d = Dust.NewDustDirect(Projectile.Center, 2, 2, DustID.RainbowMk2, 0f, 0f, 150, default, isLiteClone ? 0.7f : 0.85f);
                d.noGravity = true;
                d.velocity *= 0.2f;
            }
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.netMode != NetmodeID.Server)
            {
                int dustCount = isLiteClone ? 4 : 6;
                for (int i = 0; i < dustCount; i++)
                {
                    Vector2 dustVel = Main.rand.NextVector2Circular(2.5f, 2.5f);
                    Dust d = Dust.NewDustDirect(Projectile.Center, 2, 2, DustID.RainbowMk2, dustVel.X, dustVel.Y, 150, default, 1.1f);
                    d.noGravity = true;
                }
            }

            ScreenShakeSystem.StartShake(isLiteClone ? 1f : 1.5f, MathHelper.TwoPi, null);

            if (canSplitOnExpire && !isLiteClone && Main.myPlayer == Projectile.owner)
            {
                for (int i = 0; i < SplitShardCount; i++)
                {
                    float angle = MathHelper.TwoPi * (i / (float)SplitShardCount) + Main.rand.NextFloat(-0.2f, 0.2f);
                    Vector2 shardVel = angle.ToRotationVector2() * Main.rand.NextFloat(3.5f, 5f);
                    int p = Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, shardVel, ModContent.ProjectileType<MiniEmpressBolt>(), (int)(Projectile.damage * 0.35f), 0f, Projectile.owner);
                    if (p != Main.maxProjectiles)
                    {
                        var shard = Main.projectile[p].ModProjectile as MiniEmpressBolt;
                        if (shard != null)
                        {
                            shard.isLiteClone = true;
                            Main.projectile[p].timeLeft = 30;
                            Main.projectile[p].scale = 0.55f;
                        }
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;
            Texture2D burstTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/EmpressRework/ChromaticBurst").Value;

            float hue = Main.GlobalTimeWrappedHourly * 0.4f % 1f;
            Color pastelColor = Main.hslToRgb(hue, 0.6f, 0.8f);
            pastelColor.A = 0;

            float baseOpacity = isLiteClone ? 0.65f : 0.9f;
            float baseScale = isLiteClone ? 0.75f : 0.9f;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // Halo glow lembut
            Color glowColor = pastelColor * 0.25f * baseOpacity;
            glowColor.A = 0;
            Main.EntitySpriteDraw(tex, drawPos, null, glowColor, Projectile.rotation, tex.Size() / 2f, baseScale * 1.4f, SpriteEffects.None, 0);

            // Trail ekor
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float trailFactor = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                float trailHue = (hue + i * 0.015f) % 1f;
                Color trailColor = Main.hslToRgb(trailHue, 0.6f, 0.8f) * trailFactor * 0.4f * baseOpacity;
                trailColor.A = 0;

                Main.EntitySpriteDraw(tex, trailPos, null, trailColor, Projectile.oldRot[i], tex.Size() / 2f, baseScale * trailFactor, SpriteEffects.None, 0);
            }

            // Head Star-Flare Effect (Kecil & halus)
            float pulseScale = (0.45f + (float)System.Math.Sin(Main.GlobalTimeWrappedHourly * 8f) * 0.08f) * baseScale;
            Main.EntitySpriteDraw(burstTex, drawPos, null, pastelColor * 0.22f * baseOpacity, Projectile.rotation, burstTex.Size() / 2f, pulseScale, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(tex, drawPos, null, pastelColor * baseOpacity, Projectile.rotation, tex.Size() / 2f, baseScale, SpriteEffects.None, 0);
            return false;
        }
    }
}