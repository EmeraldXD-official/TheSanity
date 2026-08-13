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
    // ==================== SHATTERED REFLECTION CLONE ====================
    // Klona NYATA (bukan ilusi) dari boss. Muncul diam gemetar sebentar,
    // lalu "meledak" melintas lewat titik player dengan percepatan EKSPONENSIAL:
    // nyaris tak bergerak di awal, lalu dalam sepersekian detik jadi kilatan super cepat.
    // Collision dicek lewat SWEEP line (posisi frame lalu -> posisi sekarang) supaya
    // tetap kena walau kecepatan di akhir jauh melebihi lebar hitbox (anti tunneling).
    public class ShatteredReflectionClone : ModProjectile
    {
        public override string Texture => "Terraria/Images/Extra_197";

        public const int WindupTime = 45;   // Diam gemetar, garis preview muncul pelan-pelan
        public const int DashTime = 42;     // Fase akselerasi eksponensial: lambat -> sangat cepat
        public const int FadeTime = 15;     // Memudar/hancur setelah menembus
        public const float TravelDistance = 1900f;

        public ref float Timer => ref Projectile.ai[0];
        public int ColorMode => (int)Projectile.ai[1]; // 0 = Cyan, 1 = Magenta, 2 = Putih/Rainbow
        // BUFF: klona "susulan" (echo) bisa dikasih windup jauh lebih pendek lewat ai[2]==1,
        // dipakai ExecuteShatteredReflection buat serangan kedua yang gak ketebak lagi.
        public bool FastWindup => Projectile.ai[2] == 1f;
        public int EffectiveWindupTime => FastWindup ? 14 : WindupTime;

        private Vector2 anchorPos;
        private Vector2 dashDir;
        private Vector2 prevCenter;
        private bool initialized = false;

        private static Asset<Texture2D> shineFlareTex;
        private static Asset<Texture2D> bloomCircleTex;
        private static Asset<Texture2D> bloomLineTex;

        public override void SetStaticDefaults()
        {
            if (ModContent.HasAsset("Luminance/Assets/GreyscaleTextures/ShineFlare"))
                shineFlareTex = ModContent.Request<Texture2D>("Luminance/Assets/GreyscaleTextures/ShineFlare", AssetRequestMode.ImmediateLoad);

            if (ModContent.HasAsset("Luminance/Assets/GreyscaleTextures/BloomCircleSmall"))
                bloomCircleTex = ModContent.Request<Texture2D>("Luminance/Assets/GreyscaleTextures/BloomCircleSmall", AssetRequestMode.ImmediateLoad);

            if (ModContent.HasAsset("Luminance/Assets/GreyscaleTextures/BloomLine"))
                bloomLineTex = ModContent.Request<Texture2D>("Luminance/Assets/GreyscaleTextures/BloomLine", AssetRequestMode.ImmediateLoad);
        }

        private float EaseInExpo(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return (float)Math.Pow(2, 10 * (t - 1f));
        }

        public override void SetDefaults()
        {
            Projectile.width = 46;
            Projectile.height = 46;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = WindupTime + DashTime + FadeTime + 10;
            Projectile.ignoreWater = true;
            Projectile.damage = 68;
        }

        private Color GetColor()
        {
            return ColorMode switch
            {
                0 => Color.Cyan,
                1 => Color.Magenta,
                _ => Color.White,
            };
        }

        public override void AI()
        {
            if (!initialized)
            {
                // Titik asal dikunci pas frame pertama; arah dash datang dari velocity spawn (unit vector)
                anchorPos = Projectile.Center;
                dashDir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
                prevCenter = Projectile.Center;
                initialized = true;
            }

            prevCenter = Projectile.Center;
            Timer++;

            Color mainColor = GetColor();

            if (Timer <= EffectiveWindupTime)
            {
                // ---- Fase gemetar: hampir diam, cuma jitter kecil biar kerasa "menahan tenaga" ----
                float progress = Timer / (float)EffectiveWindupTime;
                float jitter = (1f - progress) * 6f + progress * 1.5f;
                Vector2 jitterOffset = Main.rand.NextVector2Circular(jitter, jitter);
                Projectile.Center = anchorPos + jitterOffset;
                Projectile.rotation = dashDir.ToRotation();

                if (Main.rand.NextBool(3))
                {
                    Vector2 previewPos = anchorPos + dashDir * Main.rand.NextFloat(-TravelDistance / 2f, TravelDistance / 2f) * progress;
                    Dust d = Dust.NewDustPerfect(previewPos, DustID.Electric, Vector2.Zero, 100, mainColor, 0.45f);
                    d.noGravity = true;
                }

                for (int i = 0; i < 2; i++)
                {
                    Dust core = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(10f, 10f), DustID.RainbowTorch, Vector2.Zero, 0, mainColor, 1.1f * progress);
                    core.noGravity = true;
                }

                if (Timer == EffectiveWindupTime)
                {
                    SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.3f, Volume = 0.6f }, Projectile.Center);
                }
            }
            else if (Timer <= EffectiveWindupTime + DashTime)
            {
                // ---- Fase dash: easing eksponensial, awal super lambat, akhir super cepat ----
                float dashProgress = (Timer - EffectiveWindupTime) / (float)DashTime;
                float eased = EaseInExpo(dashProgress);

                Projectile.Center = anchorPos + dashDir * (TravelDistance * eased);
                Projectile.rotation = dashDir.ToRotation();

                // Intensitas trail sebanding dengan kecepatan sesaat (delta posisi frame ini)
                float instSpeed = Vector2.Distance(prevCenter, Projectile.Center);
                int trailCount = (int)MathHelper.Clamp(instSpeed * 0.35f, 1, 14);

                for (int i = 0; i < trailCount; i++)
                {
                    float t = i / (float)Math.Max(1, trailCount - 1);
                    Vector2 trailPos = Vector2.Lerp(prevCenter, Projectile.Center, t);
                    Dust d = Dust.NewDustPerfect(trailPos, DustID.BlueTorch, -dashDir * 2f, 0, mainColor, 1.2f + eased * 1.2f);
                    d.noGravity = true;
                }

                if (instSpeed > 25f && Main.rand.NextBool(2))
                {
                    Dust spark = Dust.NewDustPerfect(Projectile.Center, DustID.WhiteTorch, Main.rand.NextVector2Circular(4f, 4f), 0, Color.White, 1.4f);
                    spark.noGravity = true;
                }

                if (Timer == EffectiveWindupTime + 1)
                {
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.5f + ColorMode * 0.15f, Volume = 0.65f }, Projectile.Center);
                }
            }
            else
            {
                // ---- Fase memudar setelah menembus ----
                Projectile.alpha = (int)MathHelper.Clamp(Projectile.alpha + 20, 0, 255);

                if (Timer == EffectiveWindupTime + DashTime + 1)
                {
                    SpawnImpactBurst(mainColor);
                }

                Dust fade = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f), DustID.Electric, Vector2.Zero, 150, mainColor, 0.8f);
                fade.noGravity = true;
            }
        }

        private void SpawnImpactBurst(Color mainColor)
        {
            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.2f, Volume = 0.7f }, Projectile.Center);

            for (int i = 0; i < 24; i++)
            {
                float angle = MathHelper.TwoPi * i / 24f;
                Vector2 vel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 14f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.RainbowTorch, vel, 0, mainColor, 1.8f);
                d.noGravity = true;
            }
        }

        public override bool CanHitPlayer(Player target)
        {
            return Timer > EffectiveWindupTime && Timer <= EffectiveWindupTime + DashTime;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (Timer <= EffectiveWindupTime || Timer > EffectiveWindupTime + DashTime)
                return false;

            // Sweep dari posisi frame sebelumnya ke posisi sekarang - wajib karena di akhir
            // easing eksponensial, jarak tempuh per-frame bisa jauh melebihi lebar hitbox.
            float point = 0f;
            float hitWidth = MathHelper.Max(Projectile.width, 40f);
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), prevCenter, Projectile.Center, hitWidth, ref point);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.immune = true;
            target.immuneTime = 20;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color mainColor = GetColor();

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(
                SpriteSortMode.Immediate,
                BlendState.Additive,
                SamplerState.LinearWrap,
                DepthStencilState.None,
                RasterizerState.CullNone,
                null,
                Main.GameViewMatrix.TransformationMatrix
            );

            float alphaMul = 1f - (Projectile.alpha / 255f);

            if (Timer <= EffectiveWindupTime)
            {
                // Bola energi bulat yang berdenyut (bloom circle), bukan garis lagi
                float progress = Timer / (float)EffectiveWindupTime;

                if (bloomCircleTex?.Value != null)
                {
                    Vector2 bloomOrigin = bloomCircleTex.Value.Size() * 0.5f;
                    float coreScale = (0.5f + progress * 0.45f + (float)Math.Sin(Timer * 0.6f) * 0.06f) * 0.5f;

                    Main.spriteBatch.Draw(bloomCircleTex.Value, drawPos, null, mainColor * (0.35f + progress * 0.45f), 0f, bloomOrigin, coreScale, SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(bloomCircleTex.Value, drawPos, null, Color.White * progress * 0.6f, 0f, bloomOrigin, coreScale * 0.45f, SpriteEffects.None, 0f);
                }

                if (shineFlareTex?.Value != null)
                {
                    Vector2 flareOrigin = shineFlareTex.Value.Size() * 0.5f;
                    Main.spriteBatch.Draw(shineFlareTex.Value, drawPos, null, mainColor * (0.3f + progress * 0.4f), Timer * 0.2f, flareOrigin, (0.16f + progress * 0.14f), SpriteEffects.None, 0f);
                }
            }
            else if (Timer <= EffectiveWindupTime + DashTime)
            {
                // BUFF VISUAL: porsi "comet stretch" dikurangin banyak, glow bulat + shine flare
                // yang sekarang jadi elemen dominan (bukan garis) - sesuai gaya UnknownEntityBolt.
                float dashProgress = (Timer - EffectiveWindupTime) / (float)DashTime;
                float eased = EaseInExpo(dashProgress);
                float speedFactor = MathHelper.Clamp(Vector2.Distance(prevCenter, Projectile.Center) / 60f, 0.15f, 1f);

                Texture2D bodyTex = bloomLineTex?.Value ?? bloomCircleTex?.Value;

                if (bodyTex != null)
                {
                    Vector2 bodyOrigin = new Vector2(bodyTex.Width, bodyTex.Height / 2f); // tepi kanan jadi "kepala" komet
                    float stretch = MathHelper.Lerp(0.1f, 0.55f, speedFactor) * (120f / bodyTex.Width);
                    float thickness = (0.3f + eased * 0.12f) * (60f / bodyTex.Height);

                    Main.spriteBatch.Draw(bodyTex, drawPos, null, mainColor * 0.6f * alphaMul, dashDir.ToRotation(), bodyOrigin, new Vector2(stretch, thickness), SpriteEffects.None, 0f);
                }

                if (bloomCircleTex?.Value != null)
                {
                    // Glow bulat sekarang elemen utama, bukan pelengkap - dibikin lebih besar & terang.
                    Vector2 bloomOrigin = bloomCircleTex.Value.Size() * 0.5f;
                    Main.spriteBatch.Draw(bloomCircleTex.Value, drawPos, null, mainColor * 0.85f * alphaMul, 0f, bloomOrigin, (0.4f + eased * 0.22f) * 0.5f, SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(bloomCircleTex.Value, drawPos, null, Color.White * 0.7f * alphaMul, 0f, bloomOrigin, (0.2f + eased * 0.1f) * 0.5f, SpriteEffects.None, 0f);
                }

                if (shineFlareTex?.Value != null)
                {
                    // Shine flare Luminance jadi aksen dominan kedua - diperbesar & lebih terang
                    // dibanding versi sebelumnya, sesuai gaya yang diminta (shine flare + glow, bukan garis).
                    Vector2 flareOrigin = shineFlareTex.Value.Size() * 0.5f;
                    float pulse = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 16f) * 0.15f;
                    Main.spriteBatch.Draw(shineFlareTex.Value, drawPos, null, mainColor * 0.75f * alphaMul, Timer * 0.08f, flareOrigin, 0.3f * pulse, SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(shineFlareTex.Value, drawPos, null, Color.White * 0.5f * alphaMul, -Timer * 0.05f, flareOrigin, 0.16f * pulse, SpriteEffects.None, 0f);
                }
            }
            else
            {
                // Pecah & memudar - burst bloom bulat
                float fadeProgress = (Timer - (EffectiveWindupTime + DashTime)) / (float)FadeTime;
                float scale = MathHelper.Lerp(0.35f, 0.8f, fadeProgress);

                if (bloomCircleTex?.Value != null)
                {
                    Vector2 bloomOrigin = bloomCircleTex.Value.Size() * 0.5f;
                    Main.spriteBatch.Draw(bloomCircleTex.Value, drawPos, null, mainColor * (1f - fadeProgress) * 0.7f, 0f, bloomOrigin, scale, SpriteEffects.None, 0f);
                }
            }

            Main.spriteBatch.End();
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

        public override void Unload()
        {
            shineFlareTex = null;
            bloomCircleTex = null;
            bloomLineTex = null;
        }
    }
}