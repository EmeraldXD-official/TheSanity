using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    public class UnknownEntityDeathRay : ModProjectile
    {
        public override string Texture => "Terraria/Images/Extra_197";

        public const float MaxBeamLength = 2400f;
        public const int TelegraphTime = 35;
        public const int LaserTime = 180;

        public ref float Timer => ref Projectile.ai[0];

        public override void SetDefaults()
        {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = TelegraphTime + LaserTime;
            Projectile.ignoreWater = true;
            Projectile.damage = 70;
        }

        public override void AI()
        {
            Timer++;

            // MODE BLENDER / SPINNING LASER
            if (Projectile.ai[1] == 1f)
            {
                NPC ownerBoss = Main.npc.Length > 0 ? Array.Find(Main.npc, n => n.active && n.type == ModContent.NPCType<UnknownEntity>()) : null;
                if (ownerBoss != null)
                {
                    Projectile.Center = ownerBoss.Center;
                }

                if (Timer >= TelegraphTime)
                {
                    if (Projectile.localAI[0] == 0f)
                    {
                        float direction = Projectile.ai[2] != 0f ? Projectile.ai[2] : (Main.rand.NextBool() ? 1f : -1f);
                        float spinDuration = 200f;
                        Projectile.localAI[0] = (MathHelper.TwoPi / spinDuration) * direction;
                    }

                    Projectile.velocity = Projectile.velocity.RotatedBy(Projectile.localAI[0]);
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Timer < TelegraphTime)
            {
                Projectile.scale = 0.4f;

                if (Main.rand.NextBool(2))
                {
                    Vector2 spawnDustPos = Projectile.Center + Main.rand.NextVector2CircularEdge(70f, 70f);
                    Vector2 dustVel = (Projectile.Center - spawnDustPos) * 0.12f;
                    Dust d = Dust.NewDustPerfect(spawnDustPos, DustID.BlueTorch, dustVel, 0, Color.Cyan, 1.2f);
                    d.noGravity = true;
                }
            }
            else
            {
                if (Timer == TelegraphTime)
                {
                    SoundEngine.PlaySound(SoundID.Item125 with { Pitch = -0.5f, Volume = 1.2f }, Projectile.Center);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.1f, Volume = 1.0f }, Projectile.Center);
                }

                float laserProgress = (Timer - TelegraphTime) / (float)LaserTime;
                float maxScale = (Projectile.ai[1] == 1f) ? 1.4f : 1.2f;
                Projectile.scale = (float)Math.Sin(laserProgress * MathHelper.Pi) * maxScale;

                if (Main.rand.NextBool(2))
                {
                    float dist = Main.rand.NextFloat(20f, MaxBeamLength * 0.8f);
                    Vector2 dustPos = Projectile.Center + Projectile.velocity.SafeNormalize(Vector2.UnitX) * dist;
                    Dust d = Dust.NewDustPerfect(dustPos + Main.rand.NextVector2Circular(20f, 20f), DustID.Electric, Main.rand.NextVector2Circular(3f, 3f), 0, Color.Magenta, 1.2f);
                    d.noGravity = true;
                }
            }
        }

        public override bool CanHitPlayer(Player target)
        {
            return Timer >= TelegraphTime;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (Timer < TelegraphTime) return false;

            float point = 0f;
            Vector2 beamDir = Projectile.rotation.ToRotationVector2();
            Vector2 beamEnd = Projectile.Center + beamDir * MaxBeamLength;

            float baseWidth = (Projectile.ai[1] == 1f) ? 60f : 48f;
            float coreBeamWidth = Math.Max(22f, baseWidth * Projectile.scale * 0.6f);

            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, beamEnd, coreBeamWidth, ref point);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.immune = true;
            target.immuneTime = 25;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D laserTex = TextureAssets.Extra[197].Value;   // Laser Main Texture
            Texture2D noiseTex = TextureAssets.Extra[193].Value;   // Cosmic Noise Texture

            // Aman: Request asset Luminance langsung di PreDraw dengan AsyncLoad agar tidak konflik saat loading awal
            Texture2D luminanceFlare = null;
            if (ModLoader.TryGetMod("Luminance", out _))
            {
                luminanceFlare = ModContent.Request<Texture2D>("Luminance/Assets/GreyscaleTextures/ShineFlare", AssetRequestMode.AsyncLoad).Value;
            }

            Vector2 beamStart = Projectile.Center - Main.screenPosition;
            float time = (float)Main.GlobalTimeWrappedHourly;

            Main.spriteBatch.End();

            // ==================== PASS 1: LASER BODY & TILING NOISE (LinearWrap) ====================
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive,
                SamplerState.LinearWrap,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            if (Timer < TelegraphTime)
            {
                float progress = Timer / (float)TelegraphTime;
                Color telegraphColor = Color.Lerp(Color.Cyan, Color.Magenta, (float)Math.Sin(time * 12f) * 0.5f + 0.5f) * (0.4f + progress * 0.6f);

                Vector2 lineScale = new Vector2(MaxBeamLength / laserTex.Width, 0.12f * progress);
                Main.spriteBatch.Draw(laserTex, beamStart, null, telegraphColor, Projectile.rotation, new Vector2(0, laserTex.Height / 2f), lineScale, SpriteEffects.None, 0f);

                Vector2 coreLineScale = new Vector2(MaxBeamLength / laserTex.Width, 0.03f * progress);
                Main.spriteBatch.Draw(laserTex, beamStart, null, Color.White * progress * 0.8f, Projectile.rotation, new Vector2(0, laserTex.Height / 2f), coreLineScale, SpriteEffects.None, 0f);
            }
            else
            {
                float baseWidth = (Projectile.ai[1] == 1f) ? 72f : 56f;
                float beamWidth = baseWidth * Projectile.scale;

                // --- A. ATMOSPHERIC OUTER GLOW ---
                Vector2 outerGlowScale = new Vector2(MaxBeamLength / laserTex.Width, (beamWidth * 1.6f) / laserTex.Height);
                Color outerColor = Color.Lerp(new Color(180, 30, 255), new Color(0, 220, 255), (float)Math.Sin(time * 4f) * 0.5f + 0.5f) * 0.55f;
                Main.spriteBatch.Draw(laserTex, beamStart, null, outerColor, Projectile.rotation, new Vector2(0, laserTex.Height / 2f), outerGlowScale, SpriteEffects.None, 0f);

                // --- B. MAIN LASER BODY ---
                Vector2 drawScale = new Vector2(MaxBeamLength / laserTex.Width, beamWidth / laserTex.Height);
                Color baseColor = Color.Lerp(Color.Magenta, Color.DeepPink, (float)Math.Cos(time * 3f) * 0.5f + 0.5f) * 0.85f;
                Main.spriteBatch.Draw(laserTex, beamStart, null, baseColor, Projectile.rotation, new Vector2(0, laserTex.Height / 2f), drawScale, SpriteEffects.None, 0f);

                // --- C. LAYER 1: FORWARD COSMIC NOISE ---
                if (noiseTex != null)
                {
                    int noiseScrollX1 = (int)(time * 1500f);
                    int noiseScrollY1 = (int)(time * 300f);
                    int tileW = 1024;
                    int tileH = 256;

                    Rectangle noiseSource1 = new Rectangle(noiseScrollX1, noiseScrollY1, tileW, tileH);
                    Vector2 noiseScale1 = new Vector2(MaxBeamLength / tileW, (beamWidth * 0.95f) / tileH);
                    Vector2 noiseOrigin1 = new Vector2(0, tileH / 2f);

                    Color noiseColor1 = Color.Lerp(new Color(255, 60, 200), Color.Cyan, (float)Math.Sin(time * 5f) * 0.5f + 0.5f) * 0.85f;
                    Main.spriteBatch.Draw(noiseTex, beamStart, noiseSource1, noiseColor1, Projectile.rotation, noiseOrigin1, noiseScale1, SpriteEffects.None, 0f);

                    // --- D. LAYER 2: COUNTER-TURBULENCE NOISE ---
                    int noiseScrollX2 = (int)(-time * 1800f);
                    int noiseScrollY2 = (int)(time * 500f);

                    Rectangle noiseSource2 = new Rectangle(noiseScrollX2, noiseScrollY2, tileW, tileH);
                    Vector2 noiseScale2 = new Vector2(MaxBeamLength / tileW, (beamWidth * 0.65f) / tileH);

                    Color noiseColor2 = Color.Cyan * 0.75f;
                    Main.spriteBatch.Draw(noiseTex, beamStart, noiseSource2, noiseColor2, Projectile.rotation, noiseOrigin1, noiseScale2, SpriteEffects.None, 0f);
                }

                // --- E. BRIGHT WHITE INNER CORE ---
                Vector2 coreScale = new Vector2(MaxBeamLength / laserTex.Width, (beamWidth * 0.28f) / laserTex.Height);
                Main.spriteBatch.Draw(laserTex, beamStart, null, Color.White * 0.95f, Projectile.rotation, new Vector2(0, laserTex.Height / 2f), coreScale, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.End();

            // ==================== PASS 2: LUMINANCE SHINE FLARES (LinearClamp) ====================
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.Additive,
                SamplerState.LinearClamp,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            if (luminanceFlare != null)
            {
                Vector2 flareOrigin = luminanceFlare.Size() * 0.5f;

                if (Timer < TelegraphTime)
                {
                    float progress = Timer / (float)TelegraphTime;
                    float flareScale = 0.52f * progress; // Diperbesar 30% (0.4 * 1.3)
                    Main.spriteBatch.Draw(luminanceFlare, beamStart, null, Color.Cyan * progress * 1.2f, Projectile.rotation, flareOrigin, new Vector2(flareScale * 1.5f, flareScale * 0.5f), SpriteEffects.None, 0f);
                }
                else
                {
                    float baseWidth = (Projectile.ai[1] == 1f) ? 72f : 56f;
                    float beamWidth = baseWidth * Projectile.scale;
                    float pulse = 1f + (float)Math.Sin(time * 20f) * 0.15f;

                    // Diperbesar 30% (3.2f * 1.3 = 4.16f)
                    float flareLen = (beamWidth / luminanceFlare.Width) * 4.16f * pulse;
                    float flareThick = flareLen * 0.45f;

                    // --- EXTRA WIDE GLOW BACKDROP (Efek Lebih Terang & Menyala) ---
                    Main.spriteBatch.Draw(luminanceFlare, beamStart, null, Color.Cyan * 0.5f, Projectile.rotation, flareOrigin, new Vector2(flareLen * 2.4f, flareThick * 1.8f), SpriteEffects.None, 0f);

                    // 1. Layer Cyan Utama
                    Main.spriteBatch.Draw(luminanceFlare, beamStart, null, Color.Cyan, Projectile.rotation, flareOrigin, new Vector2(flareLen * 1.8f, flareThick), SpriteEffects.None, 0f);

                    // 2. Layer Pendukung Magenta (Menyilang)
                    Main.spriteBatch.Draw(luminanceFlare, beamStart, null, Color.HotPink, Projectile.rotation + MathHelper.PiOver2, flareOrigin, new Vector2(flareLen * 1.4f, flareThick), SpriteEffects.None, 0f);

                    // 3. Inti Putih Kilau Cepat
                    Main.spriteBatch.Draw(luminanceFlare, beamStart, null, Color.White, Projectile.rotation + (time * 8f), flareOrigin, new Vector2(flareLen * 0.7f, flareThick * 0.7f), SpriteEffects.None, 0f);
                }
            }

            Main.spriteBatch.End();

            // Kembalikan SpriteBatch ke State Default Terraria
            Main.spriteBatch.Begin(
                SpriteSortMode.Deferred,
                BlendState.AlphaBlend,
                Main.DefaultSamplerState,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            return false;
        }
    }
}