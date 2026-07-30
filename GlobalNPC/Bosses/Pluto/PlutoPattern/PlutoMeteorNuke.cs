using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    // =========================================================================
    // 🛑 [KHUSUS PATTERN 8 - METEOR STORM DASH] Roket ini "11-12" sama RedMiniNuke.cs
    // (sprite, trail, ledakan -- semua SAMA PERSIS), bedanya CUMA di siapa yang
    // di-homing: RedMiniNuke asli ngincer PLAYER TERDEKAT, roket ini ngincer SATU
    // meteor spesifik yang identity-nya dititipin PlutoHead lewat ai[1] (lihat
    // MeteorShowerDash.cs). Sengaja dibikin class terpisah biar RedMiniNuke asli
    // (dipakai Pattern 2 - Trick Dash) SAMA SEKALI ga keganggu/ketiban logic baru ini.
    // =========================================================================
    public class PlutoMeteorNuke : ModProjectile
    {
        // Spritesheet: 28x330 -> 5 frame vertikal @ 28x66 (reuse sprite RedMiniNuke apa adanya).
        private const int FrameCount = 5;
        private const int TrailCacheLength = 16;

        private static BasicEffect _trailEffect;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/RedMiniNuke";
        private string GlowTexture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/RedMiniNukeGlow";

        public override void SetStaticDefaults() {
            Main.projFrames[Projectile.type] = FrameCount;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailCacheLength;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;
            Projectile.height = 30;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            // 🛑 Lebih pendek dari RedMiniNuke biasa (300) -- target-nya (meteor) jaraknya deket
            // & jalannya bisa diprediksi, jadi ngga butuh napas selama itu sebelum nyerah.
            Projectile.timeLeft = 240;
            Projectile.damage = 0;
        }

        public override void AI() {
            if (Projectile.localAI[0] == 0) {
                string launchPath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/MiniNukeLaunch";
                SoundEngine.PlaySound(new SoundStyle(launchPath), Projectile.Center);
                Projectile.localAI[0] = 1f;
            }

            Projectile.damage = 0;

            // 🛑 [TARGETING BEDA DARI REDMININUKE ASLI] Nyari meteor lewat IDENTITY yang
            // dititipin di ai[1] (bukan posisi index Main.projectile, biar tetep valid walau
            // urutan array projectile geser-geser tiap tick), BUKAN nyari player terdekat.
            int targetIdentity = (int)Projectile.ai[1];
            Projectile targetMeteor = FindTargetMeteor(targetIdentity);

            if (targetMeteor == null) {
                // Meteor targetnya udah keburu ilang (kena duluan / timeLeft abis / dsb) --
                // ngambang lurus bentar terus mati sendiri, DAKUAN homing nyasar ke player.
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 20);
            }
            else {
                float maxSpeed = Projectile.ai[0] > 0f ? Projectile.ai[0] : 15f;

                // 🛑 [DENGAN CEPAT] turnSensitivity jauh lebih agresif drpd versi ngincer player
                // punya RedMiniNuke asli (1.2f) -- meteor jatuhnya lurus & bisa diprediksi, jadi
                // roket ini emang didesain "pasti kena" biar kesan mengincar-nya kuat.
                float turnSensitivity = 3.5f;
                float maxTurnAngle = MathHelper.ToRadians(turnSensitivity);

                Vector2 targetDirection = (targetMeteor.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                float currentAngle = Projectile.velocity.ToRotation();
                float targetAngle = targetDirection.ToRotation();

                currentAngle = Utils.AngleTowards(currentAngle, targetAngle, maxTurnAngle);
                Projectile.velocity = currentAngle.ToRotationVector2() * maxSpeed;

                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

                // 🛑 [DETEKSI MANUAL] Target kita PROYEKTIL (meteor), bukan player/tile -- vanilla
                // Terraria ga otomatis ngedeteksi tabrakan antar-proyektil, makanya dicek manual
                // jarak di sini (mirip pola manual player-hit check punya RedMiniNuke asli).
                float hitDistance = (Projectile.width * 0.5f) + (targetMeteor.width * 0.5f);
                if (Vector2.Distance(Projectile.Center, targetMeteor.Center) <= hitDistance) {
                    targetMeteor.Kill(); // ledakin meteornya -> keluarin serpihan (lihat PlutoMeteor.Kill)
                    Projectile.Kill();   // ledakin roketnya juga (efek ledakan sama kayak RedMiniNuke asli)
                    return;
                }
            }

            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 4) {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % FrameCount;
            }

            if (Main.rand.NextBool(2)) {
                Vector2 smokePos = Projectile.Center - Projectile.velocity * 0.8f;
                Dust.NewDust(smokePos, 4, 4, DustID.Smoke, 0f, 0f, 100, default, 0.9f);
            }
        }

        private static Projectile FindTargetMeteor(int identity) {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == ModContent.ProjectileType<PlutoMeteor>() && p.identity == identity) {
                    return p;
                }
            }
            return null;
        }

        public override void Kill(int timeLeft) {
            int randomExplosionSlot = Main.rand.Next(1, 4);
            string explodePath = $"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/MineExplode{randomExplosionSlot}";
            SoundEngine.PlaySound(new SoundStyle(explodePath), Projectile.Center);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<RedMiniNukeExplosion>(), // reuse hitbox ledakan yg sama persis
                    10,
                    3f,
                    Main.myPlayer
                );
            }
        }

        // =====================================================================
        // RIBBON TRAIL BERGELOMBANG -- SAMA PERSIS kayak punya RedMiniNuke.cs
        // (mesh primitive, bukan after-image), disalin apa adanya biar visualnya
        // konsisten sama roket aslinya.
        // =====================================================================
        private void DrawRibbonTrail(int frameHeight) {
            Vector2[] oldPos = Projectile.oldPos;
            int pointCount = oldPos.Length;
            if (pointCount < 2) return;

            Vector2 offset = Projectile.Size / 2f;

            Vector2[] centers = new Vector2[pointCount];
            for (int i = 0; i < pointCount; i++) {
                if (i == 0) {
                    centers[i] = Projectile.Center;
                }
                else if (oldPos[i] != Vector2.Zero) {
                    centers[i] = oldPos[i] + offset;
                }
                else {
                    centers[i] = centers[i - 1];
                }
            }

            Vector2 tailDir = Projectile.velocity.SafeNormalize(-Vector2.UnitY);
            Vector2 tailShift = (BoosterOffsetSign * tailDir) * (frameHeight * 0.5f * Projectile.scale);

            Vector2[] positions = new Vector2[pointCount];
            for (int i = 0; i < pointCount; i++) {
                positions[i] = centers[i] + tailShift;
            }

            bool hasMovement = false;
            for (int i = 1; i < pointCount; i++) {
                if (positions[i] != positions[0]) { hasMovement = true; break; }
            }
            if (!hasMovement) return;

            GraphicsDevice device = Main.instance.GraphicsDevice;

            _trailEffect ??= new BasicEffect(device);
            _trailEffect.World = Matrix.Identity;
            _trailEffect.View = Main.GameViewMatrix.TransformationMatrix;
            _trailEffect.Projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, -1, 1);
            _trailEffect.VertexColorEnabled = true;
            _trailEffect.TextureEnabled = false;

            VertexPositionColor[] outerVerts = BuildRibbonStrip(
                positions,
                widthScale: 1f,
                colorFunc: progress => new Color(140, 20, 15) * (0.45f * (1f - progress))
            );

            VertexPositionColor[] innerVerts = BuildRibbonStrip(
                positions,
                widthScale: 0.42f,
                colorFunc: progress => new Color(255, 110, 40) * (0.9f * (1f - progress))
            );

            Main.spriteBatch.End();

            device.RasterizerState = RasterizerState.CullNone;
            device.BlendState = BlendState.Additive;

            DrawStrip(device, outerVerts);
            DrawStrip(device, innerVerts);

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        private VertexPositionColor[] BuildRibbonStrip(Vector2[] positions, float widthScale, Func<float, Color> colorFunc) {
            int pointCount = positions.Length;
            var vertices = new VertexPositionColor[pointCount * 2];

            for (int i = 0; i < pointCount; i++) {
                float progress = i / (float)(pointCount - 1);

                Vector2 dir;
                if (i < pointCount - 1) dir = positions[i] - positions[i + 1];
                else dir = positions[i - 1] - positions[i];
                if (dir == Vector2.Zero) dir = -Projectile.velocity;
                dir = dir.SafeNormalize(Vector2.UnitY);
                Vector2 normal = dir.RotatedBy(MathHelper.PiOver2);

                float taper = MathHelper.Lerp(1f, 0f, progress);

                float wave = (float)Math.Sin(progress * MathHelper.TwoPi * 2f + Main.GlobalTimeWrappedHourly * 6f)
                             * 4f * (1f - progress) * widthScale;

                float halfWidth = (Projectile.width * 0.5f * taper * widthScale) + wave;

                Vector2 posScreen = positions[i] - Main.screenPosition;
                Color color = colorFunc(progress);

                vertices[i * 2] = new VertexPositionColor(new Vector3(posScreen + normal * halfWidth, 0f), color);
                vertices[i * 2 + 1] = new VertexPositionColor(new Vector3(posScreen - normal * halfWidth, 0f), color);
            }

            return vertices;
        }

        private void DrawStrip(GraphicsDevice device, VertexPositionColor[] vertices) {
            foreach (EffectPass pass in _trailEffect.CurrentTechnique.Passes) {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleStrip, vertices, 0, vertices.Length - 2);
            }
        }

        private const float BoosterOffsetSign = -1f;

        private void DrawBoosterGlow(SpriteBatch spriteBatch, int frameHeight) {
            Texture2D glowBlob = TextureAssets.Extra[98].Value;
            Vector2 origin = glowBlob.Size() * 0.5f;

            Vector2 tailDir = Projectile.velocity.SafeNormalize(-Vector2.UnitY);
            Vector2 tailWorldPos = Projectile.Center + (BoosterOffsetSign * tailDir) * (frameHeight * 0.5f * Projectile.scale);
            Vector2 tailScreenPos = tailWorldPos - Main.screenPosition;

            float pulsate = 0.9f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);

            float outerScale = 0.75f * pulsate * Projectile.scale;
            Color outerColor = new Color(210, 15, 10) * 0.65f;
            spriteBatch.Draw(glowBlob, tailScreenPos, null, outerColor, 0f, origin, outerScale, SpriteEffects.None, 0f);

            float innerScale = 0.40f * pulsate * Projectile.scale;
            Color innerColor = new Color(255, 40, 30) * 0.85f;
            spriteBatch.Draw(glowBlob, tailScreenPos, null, innerColor, 0f, origin, innerScale, SpriteEffects.None, 0f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / FrameCount;

            DrawRibbonTrail(frameHeight);

            Rectangle sourceRect = new Rectangle(0, frameHeight * Projectile.frame, texture.Width, frameHeight);
            Vector2 origin = sourceRect.Size() * 0.5f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            spriteBatch.Draw(texture, drawPos, sourceRect, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            Texture2D glow = ModContent.Request<Texture2D>(GlowTexture).Value;
            Rectangle glowSource = new Rectangle(0, frameHeight * Projectile.frame, glow.Width, frameHeight);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            spriteBatch.Draw(glow, drawPos, glowSource, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            DrawBoosterGlow(spriteBatch, frameHeight);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false;
        }
    }
}
