using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;

using TheSanity.Buff;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    // =========================================================================
    // 1. CLASS UTAMA: TUBUH ROKET NUKLIR (BISA TEMBUS BLOCK)
    // =========================================================================
    public class RedMiniNuke : ModProjectile
    {
        // Spritesheet: 28x330 -> 5 frame vertikal @ 28x66
        private const int FrameCount = 5;
        // Jumlah titik posisi lama yang di-cache buat bentuk mesh trail
        private const int TrailCacheLength = 16;

        // Efek dipakai bareng, dibuat sekali aja (lazy init) biar gak bikin BasicEffect baru tiap frame
        private static BasicEffect _trailEffect;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/RedMiniNuke";
        private string GlowTexture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/RedMiniNukeGlow";

        public override void SetStaticDefaults() {
            // Frame count buat sistem vanilla yang butuh tau jumlah frame
            Main.projFrames[Projectile.type] = FrameCount;

            // Caching posisi & rotasi lama BAWAAN vanilla. Ini cuma nyimpen data posisi,
            // BUKAN nge-draw ulang sprite di posisi lama (jadi bukan teknik after-image).
            // Data ini yang dipakai buat bentuk mesh ribbon trail di bawah.
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailCacheLength;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults() {
            Projectile.width = 22;        
            Projectile.height = 30;       
            Projectile.hostile = true;    
            Projectile.friendly = false;  
            
            // PERBAIKAN AKHIR: Di-set false agar roket bisa menembus block/dinding arena!
            Projectile.tileCollide = false; 
            
            Projectile.penetrate = 1;     
            Projectile.timeLeft = 300;    
            Projectile.damage = 0; 
        }

        public override void AI() {
            // Memutar suara peluncuran kustom saat pertama kali spawn
            if (Projectile.localAI[0] == 0) {
                string launchPath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/MiniNukeLaunch";
                SoundEngine.PlaySound(new SoundStyle(launchPath), Projectile.Center);
                Projectile.localAI[0] = 1f; 
            }

            Projectile.damage = 0;

            // Deteksi benturan manual instan dengan player
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (p.active && !p.dead && Projectile.Hitbox.Intersects(p.Hitbox)) {
                    Projectile.Kill(); 
                    return; 
                }
            }

            float maxSpeed = Projectile.ai[0] > 0f ? Projectile.ai[0] : 7.5f; 

            // Mekanik Semi-Homing (Nekuk Lembut)
            float turnSensitivity = 1.2f; 
            float maxTurnAngle = MathHelper.ToRadians(turnSensitivity); 

            float closestDistance = 1300f; 
            int targetPlayerIndex = -1;

            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (p.active && !p.dead) {
                    float distanceToPlayer = Vector2.Distance(p.Center, Projectile.Center);
                    if (distanceToPlayer < closestDistance) {
                        closestDistance = distanceToPlayer;
                        targetPlayerIndex = i;
                    }
                }
            }

            if (targetPlayerIndex != -1) {
                Player target = Main.player[targetPlayerIndex];
                Vector2 targetDirection = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                
                float currentAngle = Projectile.velocity.ToRotation();
                float targetAngle = targetDirection.ToRotation();

                currentAngle = Utils.AngleTowards(currentAngle, targetAngle, maxTurnAngle);
                Projectile.velocity = currentAngle.ToRotationVector2() * maxSpeed;
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // Animasi frame spritesheet: ganti frame tiap 4 tick, looping 5 frame
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

        public override void Kill(int timeLeft) {
            int randomExplosionSlot = Main.rand.Next(1, 4);
            string explodePath = $"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/MineExplode{randomExplosionSlot}";
            SoundEngine.PlaySound(new SoundStyle(explodePath), Projectile.Center);

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(), 
                    Projectile.Center, 
                    Vector2.Zero, 
                    ModContent.ProjectileType<RedMiniNukeExplosion>(), 
                    10, 
                    3f, 
                    Main.myPlayer
                );
            }
        }

        // =====================================================================
        // RIBBON TRAIL BERGELOMBANG (mesh primitive, BUKAN after-image)
        // Bikin satu triangle-strip mengikuti oldPos, meruncing ke ekor,
        // dengan sedikit offset sinusoidal biar pita-nya bergelombang natural.
        // =====================================================================
        private void DrawRibbonTrail(int frameHeight) {
            Vector2[] oldPos = Projectile.oldPos;
            int pointCount = oldPos.Length;
            if (pointCount < 2) return;

            Vector2 offset = Projectile.Size / 2f;

            // Substitusi titik yang belum ke-isi (masih Vector2.Zero) pakai titik valid sebelumnya,
            // biar strip-nya gak "melompat" balik ke pojok layar.
            // Titik ke-0 SENGAJA dipaksa pakai Projectile.Center langsung (bukan oldPos[0]+offset),
            // soalnya oldPos[0] itu posisi tick SEBELUMNYA (nge-lag 1 tick di belakang posisi
            // roket yang sekarang lagi digambar) -> ini sumber gap #1.
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

            // Sumber gap #2: "centers" di atas itu titik tengah HITBOX (22x30), padahal sprite
            // visualnya 28x66 -> titik ekor visual ada di frameHeight/2 di belakang center, BUKAN
            // di center itu sendiri. Geser SEMUA titik pakai vektor yang PERSIS sama kayak yang
            // dipakai DrawBoosterGlow, biar kepala ribbon nempel pas di titik yang sama dengan
            // bloom booster (bukan nongol di tengah badan roket).
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
            // GameViewMatrix biar ribbon ikut ke-transform bareng kamera (zoom/shake dll),
            // sama kayak yang dipakai spriteBatch buat body/glow/booster.
            _trailEffect.View = Main.GameViewMatrix.TransformationMatrix;
            // PENTING: pakai ukuran VIEWPORT device yang sebenarnya, BUKAN Main.screenWidth/
            // Main.screenHeight. Kalau ada "Resolution Scaling"/downscale render target aktif,
            // dua angka itu bisa beda sama render target yang lagi dipakai -> proyeksi manual kita
            // jadi salah skala, dan salah-skala ini baru KELIATAN pas roket makin jauh dari titik
            // tengah layar (jadi keliatannya "gerak ngikutin layar" tiap kamera geser, padahal
            // sebenarnya cuma makin ke-strech/off dari titik pusat proyeksi yang salah ukuran).
            _trailEffect.Projection = Matrix.CreateOrthographicOffCenter(0, device.Viewport.Width, device.Viewport.Height, 0, -1, 1);
            _trailEffect.VertexColorEnabled = true;
            _trailEffect.TextureEnabled = false;

            // Layer luar: lebih lebar, merah gelap/redup -> kesan semburan luar booster
            VertexPositionColor[] outerVerts = BuildRibbonStrip(
                positions,
                widthScale: 1f,
                colorFunc: progress => new Color(140, 20, 15) * (0.45f * (1f - progress))
            );

            // Layer dalam: lebih sempit, merah terang/oranye -> kesan core booster yang nyala
            VertexPositionColor[] innerVerts = BuildRibbonStrip(
                positions,
                widthScale: 0.42f,
                colorFunc: progress => new Color(255, 110, 40) * (0.9f * (1f - progress))
            );

            Main.spriteBatch.End();

            device.RasterizerState = RasterizerState.CullNone;
            device.BlendState = BlendState.Additive; // additive biar keliatan nyala, bukan solid shadow

            DrawStrip(device, outerVerts);
            DrawStrip(device, innerVerts);

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }

        // Bikin satu triangle-strip dari array posisi, dengan skala lebar & fungsi warna custom.
        // Dipanggil 2x (outer & inner) buat dapetin efek gradasi "core lebih terang, luar lebih redup".
        private VertexPositionColor[] BuildRibbonStrip(Vector2[] positions, float widthScale, Func<float, Color> colorFunc) {
            int pointCount = positions.Length;
            var vertices = new VertexPositionColor[pointCount * 2];

            for (int i = 0; i < pointCount; i++) {
                float progress = i / (float)(pointCount - 1); // 0 = kepala (dekat roket), 1 = ujung ekor

                // Arah lokal di titik ini, dipakai buat nentuin arah tegak lurus (lebar pita)
                Vector2 dir;
                if (i < pointCount - 1) dir = positions[i] - positions[i + 1];
                else dir = positions[i - 1] - positions[i];
                if (dir == Vector2.Zero) dir = -Projectile.velocity;
                dir = dir.SafeNormalize(Vector2.UnitY);
                Vector2 normal = dir.RotatedBy(MathHelper.PiOver2);

                // Lebar meruncing ke ekor
                float taper = MathHelper.Lerp(1f, 0f, progress);

                // Gelombang sinusoidal sepanjang pita ("Ribbon bergelombang").
                // Besar/liar deket booster (progress kecil), makin kecil & meredam ke ujung ekor
                // (progress besar) — biar arahnya SEARAH sama taper (yang juga besar->kecil), jadi
                // ujungnya beneran meruncing rapi jadi satu titik yang fade out, bukan malah tetep
                // liar goyang pas widthnya udah hampir 0.
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

        // Kalau posisi booster glow masih kebalik (nempel di depan bukan di belakang),
        // tinggal ganti angka ini ke -1f (atau 1f) buat balikin arahnya.
        private const float BoosterOffsetSign = -1f;

        // Bloom cahaya kecil KHUSUS di sekitar booster/ekor roket (bukan sepanjang trail).
        // Pakai tekstur radial lembut (sama yang dipakai buat ledakan), digambar additive + sedikit pulsating.
        // frameHeight = tinggi 1 frame VISUAL di spritesheet (bukan Projectile.height yang cuma hitbox gameplay).
        private void DrawBoosterGlow(SpriteBatch spriteBatch, int frameHeight) {
            Texture2D glowBlob = TextureAssets.Extra[98].Value;
            Vector2 origin = glowBlob.Size() * 0.5f;

            Vector2 tailDir = Projectile.velocity.SafeNormalize(-Vector2.UnitY);
            Vector2 tailWorldPos = Projectile.Center + (BoosterOffsetSign * tailDir) * (frameHeight * 0.5f * Projectile.scale);
            Vector2 tailScreenPos = tailWorldPos - Main.screenPosition;

            float pulsate = 0.9f + 0.1f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 14f + Projectile.identity);

            // Layer luar: gede & tebal, merah gelap -> "badan" bloom
            float outerScale = 0.75f * pulsate * Projectile.scale;
            Color outerColor = new Color(210, 15, 10) * 0.65f;
            spriteBatch.Draw(glowBlob, tailScreenPos, null, outerColor, 0f, origin, outerScale, SpriteEffects.None, 0f);

            // Layer dalam: lebih kecil, merah terang (bukan oren) -> core booster
            float innerScale = 0.40f * pulsate * Projectile.scale;
            Color innerColor = new Color(255, 40, 30) * 0.85f;
            spriteBatch.Draw(glowBlob, tailScreenPos, null, innerColor, 0f, origin, innerScale, SpriteEffects.None, 0f);
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            int frameHeight = texture.Height / FrameCount;

            // 1. Gambar trail shadow dulu, di belakang body roket
            //    (frameHeight dihitung duluan di atas, biar titik kepala ribbon bisa align
            //    ke titik ekor visual yang sama kayak DrawBoosterGlow)
            DrawRibbonTrail(frameHeight);

            Rectangle sourceRect = new Rectangle(0, frameHeight * Projectile.frame, texture.Width, frameHeight);
            Vector2 origin = sourceRect.Size() * 0.5f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // 2. Body utama -- pakai lightColor (BUKAN Color.White) supaya beneran "kena lighting
            //    normal" kayak komentarnya, alias meredup di tempat gelap kayak sprite lain pada
            //    umumnya. Sebelumnya sengaja/kelupaan di-hardcode Color.White, yang artinya body
            //    ini full-bright TERUS tiap saat -- jadi kesannya "seluruh badan roket nyala",
            //    padahal yang harusnya full-bright cuma bagian yang emang ada di sprite Glow
            //    (digambar terpisah di step 3 di bawah, additive).
            spriteBatch.Draw(texture, drawPos, sourceRect, lightColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            // 3. Glow mask -> full bright, additive, gak kena lighting map (perlu texture "RedMiniNukeGlow"
            //    dengan layout spritesheet yang SAMA: 28x330, 5 frame @28x66)
            Texture2D glow = ModContent.Request<Texture2D>(GlowTexture).Value;
            Rectangle glowSource = new Rectangle(0, frameHeight * Projectile.frame, glow.Width, frameHeight);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            spriteBatch.Draw(glow, drawPos, glowSource, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);

            // Bloom cahaya cuma di sekitar booster (ekor), bukan di sepanjang trail
            DrawBoosterGlow(spriteBatch, frameHeight);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false; 
        }
    }

    // =========================================================================
    // 2. CLASS KEDUA: HITBOX LEDAKAN (TRANSPARAN)
    // =========================================================================
    public class RedMiniNukeExplosion : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_0";

        public override void SetDefaults() {
            Projectile.width = 110;       
            Projectile.height = 110;      
            Projectile.hostile = true;    
            Projectile.friendly = false;  
            Projectile.tileCollide = false; 
            Projectile.penetrate = -1;    
            Projectile.timeLeft = 4;      
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), 4 * 60);
        }

        public override void AI() {
            if (Main.masterMode) {
                Projectile.damage = 7; 
            }
            else if (Main.expertMode) {
                Projectile.damage = 10; 
            }
            else {
                Projectile.damage = 15; 
            }

            if (Projectile.localAI[0] == 0) {
                for (int i = 0; i < 25; i++) {
                    int fire = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Torch, 0f, 0f, 100, default, 2f);
                    Main.dust[fire].velocity *= 3f;
                    Main.dust[fire].noGravity = true; 
                    
                    int smoke = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Smoke, 0f, 0f, 100, default, 1.5f);
                    Main.dust[smoke].velocity *= 2f;
                }
                Projectile.localAI[0] = 1f;
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D explosionRing = TextureAssets.Extra[98].Value; 
            Texture2D bloomBlob = TextureAssets.Extra[98].Value; // tekstur radial lembut, dipakai ulang buat bloom
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Vector2 origin = explosionRing.Size() * 0.5f;

            float scaleMultiplier = 1f + (float)(4 - Projectile.timeLeft) * 0.15f;

            // 0 = baru meledak, 1 = mau ilang (Projectile.timeLeft awal 4 -> 0)
            float lifeProgress = 1f - (Projectile.timeLeft / 4f);

            // 🛑 [LOKASI BALANCING TRANSPARANSI VISUAL LEDAKAN]
            float visualOpacity = 0.15f; 
            Color blastColor = Color.Red * visualOpacity;

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // ===== Bloom flash utama: nyala terang di awal, ngembang cepet + meredup ("pecah") =====
            float bloomScale = (1.2f + lifeProgress * 2.2f) * Projectile.scale;
            float bloomAlpha = MathHelper.Lerp(1f, 0f, lifeProgress);
            Color bloomColor = Color.Lerp(Color.White, new Color(255, 30, 20), lifeProgress) * (bloomAlpha * 0.9f);

            spriteBatch.Draw(bloomBlob, drawPos, null, bloomColor, 0f, origin, bloomScale, SpriteEffects.None, 0f);

            // ===== Serpihan bloom kecil mencar radial, biar keliatan "pecah" bukan cuma ring polos =====
            const int shardCount = 8;
            for (int i = 0; i < shardCount; i++) {
                float angle = (MathHelper.TwoPi / shardCount * i) + (Projectile.identity * 0.35f);
                float shardDist = 18f + lifeProgress * 55f;
                Vector2 shardPos = drawPos + angle.ToRotationVector2() * shardDist;
                float shardScale = MathHelper.Max((0.35f - lifeProgress * 0.28f) * Projectile.scale, 0.04f);
                Color shardColor = new Color(255, 45, 25) * (bloomAlpha * 0.7f);

                spriteBatch.Draw(bloomBlob, shardPos, null, shardColor, 0f, origin, shardScale, SpriteEffects.None, 0f);
            }

            spriteBatch.Draw(
                explosionRing, 
                drawPos, 
                null, 
                blastColor, 
                0f, 
                origin, 
                Projectile.scale * scaleMultiplier * 0.8f, 
                SpriteEffects.None, 
                0
            );

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return false; 
        }
    }
}
