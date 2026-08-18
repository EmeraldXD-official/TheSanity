using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // TwinsCursedBeam — laser lurus KE BAWAH, dipakai pattern TwinsCursedRain (lihat file itu)
    // pas abis muter 1 putaran penuh, DAN dipakai TwinsLaserBarrage buat "RetLaserBeam"
    // (versi enraged-nya):
    //
    //   - VISUAL: TwinsBeamGlow (bloom gradient smooth) + TwinsBeamNoise (2 lapis plasma
    //     scroll berlawanan arah) + 1 garis solid tipis di atas buat kejelasan gameplay -
    //     SAMA sistem persis kayak DrawAimLine/DrawDeathrayCone di TwinsRework.cs (dulu
    //     pakai sprite RedBeamBottom/Middle/Top ala RedBeam.cs, sekarang diseragamkan biar
    //     SEMUA laser beam Twins pakai sistem noise yang sama). Lihat DrawBeamVisual di
    //     bawah buat detail-nya.
    //   - GAK ngejar/nge-scan relatif ke posisi player (RedBeam scan-nya "sampai lewatin
    //     player + buffer") — beam ini SELALU LURUS KE BAWAH dari titik spawn (posisi mulut
    //     depan Retinazer pas nembak — lihat TwinsCursedRain.FireCursedBeamDown), berhenti di
    //     block solid PERTAMA yang ketemu.
    //   - Begitu ketemu titik impact-nya (abis animasi "membesar" kelar, ~GrowTicks tick),
    //     SEKALI doang, muncrat RedPhantasmalBolt (10-15 biji, ModProjectile custom kita
    //     sendiri yang udah non-homing + speed exponential) ke ARAH ATAS dari titik impact
    //     itu, arahnya di-random tiap biji (BUKAN nyebar rata/statis kayak versi awal), jadi
    //     kerasa lebih organik/berantakan kayak ledakan beneran.
    //
    // Damage ke player pas kena garis diatur lewat Colliding() (segment check, bukan hitbox
    // kotak biasa) — pattern yang sama kayak RedBeam.cs. Nilai damage-nya sendiri dipass dari
    // spawner (TwinsCursedRain.FireCursedBeamDown) lewat parameter Damage di NewProjectile(),
    // BUKAN di-hardcode di sini.
    // ==========================================
    public class TwinsCursedBeam : ModProjectile
    {
        // Sama kayak RedBeam.cs — sprite Middle dipakai sebagai Texture "resmi" property ini,
        // walau render aslinya tetap manual di PreDraw (butuh 3 sprite: Bottom/Middle/Top).
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/RedBeamMiddle";

        private const float ScanSafetyCap = 3000f;   // jaring pengaman performa doang (~187 block)
        private const float ScanStep = 32f;          // 2 tile per langkah scan, biar murah
        private const float BeamHitThickness = 24f;  // ketebalan hit-detection FINAL (gak ikut animasi)

        private const int BeamLifeTime = 80;         // ~1.33 detik total hidup
        private const int GrowTicks = 15;            // ~0.25 detik animasi "tipis -> tebal"
        private const int ShrinkTicks = 12;          // ~0.2 detik terakhir "tebal -> nyaris 0"
        private const float StartThickness = 2f;
        private const float FullThickness = BeamHitThickness;

        private const int ImpactBoltMinCount = 10;
        private const int ImpactBoltMaxCountInclusive = 15;
        private const float ImpactBoltSpreadDegrees = 70f; // total sebaran, tapi arah tiap biji di-RANDOM di dalam rentang ini (bukan rata/statis)
        private const int ImpactBoltDamage = 10; // RedBolt: target ~30 DMG Master / 3 (engine auto-triples proyektil di Master mode) - +20% otomatis kalau Last Stand

        private float beamLength = ScanSafetyCap;
        private int lifeTimer = 0;
        private bool lengthComputed = false;
        private bool impactFired = false;

        // ==========================================
        // ENRAGED (Phase 3): di-set MANUAL oleh spawner (TwinsCursedRain.FireCursedBeamDown /
        // TwinsLaserBarrage) SEGERA setelah NewProjectile, kalau true impact-nya nambah 2
        // semburan RedPhantasmalBolt ke KANAN-KIRI (tegak lurus arah beam), bukan cuma ke
        // arah "belakang" (kebalikan arah beam) doang kayak versi normal.
        // ==========================================
        public bool Enraged;

        // Kalau true, FireImpactBolts() SKIP TOTAL - beam ini gak muncrat RedPhantasmalBolt
        // apapun sama sekali begitu kena tile solid (dipakai TwinsLaserBarrage buat
        // "RetLaserBeam"-nya, yang emang gak dimaksudkan punya impact spike sama sekali).
        public bool SuppressImpactBolts;

        // Arah beam SEKARANG DIBACA dari Projectile.rotation (di-set manual oleh spawner
        // abis NewProjectile - lihat FireCursedBeamDown & TwinsLaserBarrage) - BUKAN
        // di-hardcode Vector2.UnitY (lurus ke bawah) lagi, jadi beam ini bisa dipakai buat
        // arah manapun (lurus ke bawah dari CursedRain, ATAU diarahkan ke player dari
        // LaserBarrage "RetLaserBeam").
        private Vector2 BeamDir => Projectile.rotation.ToRotationVector2();

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 1;
        }

        public override void SetDefaults()
        {
            Projectile.width = 8;
            Projectile.height = 8;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3600;
            Projectile.alpha = 0;
        }

        public override void AI()
        {
            // Scan-nya CUKUP SEKALI aja pas tick pertama — Twin diem total selama beam ini
            // hidup (state Beaming di TwinsCursedRain), jadi gak perlu recompute tiap tick
            // kayak RedBeam yang antisipasi target gerak.
            if (!lengthComputed)
            {
                beamLength = ComputeBeamLength();
                lengthComputed = true;
            }

            lifeTimer++;

            // Begitu animasi "membesar" kelar (beam udah full ukurannya secara visual),
            // SEKALI doang muncratin RedPhantasmalBolt ke atas dari titik impact.
            if (!impactFired && lifeTimer >= GrowTicks)
            {
                impactFired = true;
                FireImpactBolts();
            }

            if (lifeTimer >= BeamLifeTime)
            {
                Projectile.Kill();
            }

            Projectile.velocity = Vector2.Zero; // beam diem total di tempat spawn-nya
        }

        // Scan LURUS SEARAH BeamDir dari titik spawn sampai ketemu block solid pertama (atau
        // mentok ScanSafetyCap kalau somehow gak ketemu apa-apa, misal spawn di ruang terbuka
        // luas). Dulu di-hardcode Vector2.UnitY (lurus ke bawah doang) - sekarang generic biar
        // bisa dipakai juga buat "RetLaserBeam" (LaserBarrage) yang diarahkan ke player.
        private float ComputeBeamLength()
        {
            Vector2 dir = BeamDir;
            float dist = ScanStep;

            while (dist < ScanSafetyCap)
            {
                Vector2 point = Projectile.Center + dir * dist;
                if (Collision.SolidCollision(point - new Vector2(4f, 4f), 8, 8))
                {
                    return dist;
                }
                dist += ScanStep;
            }
            return ScanSafetyCap;
        }

        // Hitbox custom: garis tipis lurus ke bawah sepanjang beamLength, bukan kotak default.
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 start = Projectile.Center;
            Vector2 end = start + BeamDir * beamLength;

            Vector2 targetCenter = targetHitbox.Center.ToVector2();
            Vector2 closest = ClosestPointOnSegment(start, end, targetCenter);
            float distSq = Vector2.DistanceSquared(closest, targetCenter);

            float hitRadius = BeamHitThickness * 0.5f + System.Math.Max(targetHitbox.Width, targetHitbox.Height) * 0.5f;
            return distSq <= hitRadius * hitRadius;
        }

        private static Vector2 ClosestPointOnSegment(Vector2 a, Vector2 b, Vector2 p)
        {
            Vector2 ab = b - a;
            float lenSq = ab.LengthSquared();
            if (lenSq < 0.0001f)
                return a;

            float t = MathHelper.Clamp(Vector2.Dot(p - a, ab) / lenSq, 0f, 1f);
            return a + ab * t;
        }

        // Damage beam ini ("Beam Predik" - dipakai CursedRain & LaserBarrage) SENGAJA ignore
        // SELURUH defense DAN damage reduction player - lewat helper satu tempat di
        // TwinsDebuffGlobalProjectile biar konsisten sama semua sumber damage Twins lain.
        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            TwinsDebuffGlobalProjectile.IgnoreDefenseAndDamageReduction(ref modifiers);
        }

        // Kena beam ini ngasih paket debuff standar Twins (Broken Armor + Weak + Bleeding).
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            TwinsDebuffGlobalProjectile.ApplyDebuffs(target);
        }

        // Muncrat RedPhantasmalBolt (10-15 biji) dari titik IMPACT (ujung bawah beam) ke ARAH
        // ATAS. Arah tiap biji di-RANDOM di dalam rentang ImpactBoltSpreadDegrees (BUKAN nyebar
        // rata/statis kayak versi awal yang pakai formula fan merata) — biar keliatan lebih
        // organik/berantakan kayak pecahan ledakan beneran, bukan pola geometris yang kaku.
        private void FireImpactBolts()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            // "RetLaserBeam" (LaserBarrage) gak dimaksudkan punya impact spike sama sekali.
            if (SuppressImpactBolts)
                return;

            Vector2 dir = BeamDir;
            Vector2 impactPoint = Projectile.Center + dir * beamLength;
            Vector2 back = -dir; // arah "balik" dari beam (dulu selalu "up" pas beam-nya lurus ke bawah)

            int boltType = ModContent.ProjectileType<RedPhantasmalBolt>();
            FireBoltArc(impactPoint, back, ImpactBoltMinCount, ImpactBoltMaxCountInclusive, ImpactBoltSpreadDegrees, boltType);

            // ==========================================
            // ENRAGED (Phase 3): impact-nya nambah 2 semburan lagi ke KANAN & KIRI (tegak
            // lurus arah beam), di luar semburan "balik" yang udah ada di atas.
            // ==========================================
            if (Enraged)
            {
                Vector2 sideA = dir.RotatedBy(MathHelper.PiOver2);
                Vector2 sideB = -sideA;
                FireBoltArc(impactPoint, sideA, ImpactBoltMinCount, ImpactBoltMaxCountInclusive, ImpactBoltSpreadDegrees, boltType);
                FireBoltArc(impactPoint, sideB, ImpactBoltMinCount, ImpactBoltMaxCountInclusive, ImpactBoltSpreadDegrees, boltType);
            }
        }

        // Semburin pelletCount (random di antara minCount..maxCountInclusive) RedPhantasmalBolt
        // dari origin, tersebar RANDOM (bukan merata) di antara [-halfSpread, halfSpread] dari
        // baseDir - dipakai buat semburan "balik" (normal) maupun kanan/kiri (Enraged saja).
        private void FireBoltArc(Vector2 origin, Vector2 baseDir, int minCount, int maxCountInclusive, float spreadDegrees, int boltType)
        {
            int pelletCount = Main.rand.Next(minCount, maxCountInclusive + 1);
            float halfSpread = MathHelper.ToRadians(spreadDegrees) * 0.5f;

            for (int i = 0; i < pelletCount; i++)
            {
                float angleOffset = Main.rand.NextFloat(-halfSpread, halfSpread);
                Vector2 shotDirection = baseDir.RotatedBy(angleOffset);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    origin,
                    shotDirection * RedPhantasmalBolt.StartSpeed,
                    boltType,
                    TwinsReworkOverride.ScaleBoltDamage(ImpactBoltDamage),
                    1.5f,
                    Main.myPlayer,
                    shotDirection.ToRotation(), // ai0: sudut arah terkunci
                    0f                           // ai1: counter tick, mulai dari 0
                );
            }
        }

        // ==========================================
        // RENDER — proyektil ini TIDAK gambar dirinya sendiri lewat jalur draw proyektil biasa
        // (Main.DrawProjectiles), soalnya jalur itu SELALU jalan SETELAH NPC digambar di
        // render-loop vanilla — kalau dipaksain gambar di sini, beam-nya PASTI nongol DI ATAS
        // sprite Spazmatism/Retinazer, gak peduli apapun yang kita atur.
        //
        // Makanya PreDraw() di bawah ini SENGAJA DIKOSONGIN (return false doang, gak gambar
        // apa-apa). Visual beam yang sebenernya digambar MANUAL lewat DrawBeamVisual() di
        // bawah, dipanggil dari TwinsReworkOverride.PreDraw (lihat ActiveCursedBeamIndex di
        // TwinsRework.cs) SEBELUM sprite Twins digambar — jadi urutannya kebalik dan beam-nya
        // kegambar DI BAWAH Spaz & Reti, sesuai yang diminta.
        // ==========================================
        public override bool PreDraw(ref Color lightColor)
        {
            return false;
        }

        // Dipanggil manual dari TwinsReworkOverride.PreDraw, SEBELUM sprite Spazmatism &
        // Retinazer digambar. Isinya logic draw beam, dipanggil dari luar (bukan dari jalur
        // draw proyektil normal), makanya urutan layer-nya kebalik.
        //
        // UPDATE (request "semua laser beam pakai sprite noise juga"): GANTI TOTAL dari
        // RedBeamBottom/Middle/Top (3-sprite stretch ala RedBeam.cs) ke sistem yang SAMA
        // kayak beam lain di file TwinsRework.cs (DrawAimLine/DrawDeathrayCone) - lapis
        // bloom pakai TwinsBeamGlow (gradient vertikal smooth), lapis "plasma" pakai
        // TwinsBeamNoise (2 lapis, scroll berlawanan arah biar kesan energi ngalir/crackling,
        // WRAP sampler), plus 1 garis solid tipis paling atas murni buat kejelasan gameplay
        // (biar hitbox-nya masih gampang dibaca walau plasma-nya lagi flicker). Ketebalan
        // (thicknessPx) tetep ngikutin animasi grow/shrink asli (GetVisualScale() * FullThickness),
        // jadi bloom & plasma-nya ikut "membesar/mengecil" bareng animasi beam-nya.
        //
        // Helper-nya SENGAJA di-duplikasi lokal di sini (bukan manggil punya
        // TwinsReworkOverride di TwinsRework.cs) - dua class beda file, methodnya private di
        // sana, dan pattern file-file lain di codebase ini emang udah biasa punya helper
        // kecil sendiri-sendiri per file (SpawnMuzzleDust, FireBackBolt, dst) daripada
        // coupling erat antar file.
        public void DrawBeamVisual(Vector2 screenPos)
        {
            float rotation = Projectile.rotation;
            Vector2 screenOrigin = Projectile.Center - screenPos;

            float thicknessPx = FullThickness * GetVisualScale(); // ketebalan LITERAL pixel, animasi grow/shrink tetep jalan
            if (beamLength <= 0f || thicknessPx <= 0.1f)
                return;

            Color baseColor = new Color(220, 20, 20);

            // --- BLOOM (TwinsBeamGlow, gradient smooth, ADDITIVE) - lebar ngikutin
            // thicknessPx SEKARANG biar bloom-nya ikut "membesar" pas animasi grow, bukan
            // lebar statis kayak DrawAimLine yang emang konstan dari awal ---
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            DrawBeamGlow(screenOrigin, rotation, beamLength, thicknessPx, baseColor);

            // --- PLASMA NOISE (2 lapis scroll berlawanan arah, WRAP sampler biar tiling) ---
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearWrap, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            Color plasmaBodyTint = baseColor * 0.9f;
            Color plasmaCoreTint = Color.Lerp(baseColor, Color.White, 0.4f);
            DrawBeamNoiseStrip(screenOrigin, rotation, beamLength, thicknessPx, plasmaBodyTint, 0.9f, 0f);
            DrawBeamNoiseStrip(screenOrigin, rotation, beamLength, thicknessPx * 0.45f, plasmaCoreTint, -1.6f, 400f);
            Main.spriteBatch.End();

            // --- SOLID CORE (crisp tipis, murni buat kejelasan gameplay - hitbox-nya beam
            // ini beneran nge-damage lewat Colliding(), jadi garis anchor yang jelas penting) ---
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle pixelSource = new Rectangle(0, 0, pixel.Width, pixel.Height);
            Vector2 pixelOrigin = new Vector2(0f, pixel.Height * 0.5f);
            Color coreColor = Color.Lerp(baseColor, Color.White, 0.2f);
            float solidThickness = MathHelper.Max(2f, thicknessPx * 0.14f);
            Rectangle coreDest = new Rectangle((int)screenOrigin.X, (int)screenOrigin.Y, (int)beamLength, (int)solidThickness);
            Main.spriteBatch.Draw(pixel, coreDest, pixelSource, coreColor, rotation, pixelOrigin, SpriteEffects.None, 0f);
        }

        // ==========================================
        // HELPER — bloom smooth (TwinsBeamGlow, gradient per-pixel, gak ada tumpukan
        // rectangle tegas kayak versi lama) - 2 lapis: luar lebar+samar (ambient), dalam
        // sempit+terang (numpuk deket badan beam). Sama teknik persis kayak DrawBloomAroundLine
        // di TwinsRework.cs, cuma di-duplikasi lokal (lihat catatan di atas DrawBeamVisual).
        // ==========================================
        private void DrawBeamGlow(Vector2 startScreen, float rotation, float length, float thicknessPx, Color glowColor)
        {
            Texture2D glowTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/TheTwins/TwinsBeamGlow").Value;
            if (glowTex == null || length <= 0f)
                return;

            Rectangle sourceRect = new Rectangle(0, 0, glowTex.Width, glowTex.Height);
            Vector2 originInSourceSpace = new Vector2(0f, glowTex.Height * 0.5f);

            float outerWidth = thicknessPx + 80f; // ~5 block ekstra nyebar
            float innerWidth = thicknessPx + 24f;

            Vector2 outerScale = new Vector2(length / glowTex.Width, outerWidth / glowTex.Height);
            Main.spriteBatch.Draw(glowTex, startScreen, sourceRect, glowColor * 0.5f, rotation, originInSourceSpace, outerScale, SpriteEffects.None, 0f);

            Vector2 innerScale = new Vector2(length / glowTex.Width, innerWidth / glowTex.Height);
            Main.spriteBatch.Draw(glowTex, startScreen, sourceRect, glowColor * 0.8f, rotation, originInSourceSpace, innerScale, SpriteEffects.None, 0f);
        }

        // ==========================================
        // HELPER — satu "pita plasma" TwinsBeamNoise yang di-scroll terus (efek energi
        // ngalir/crackling). Sama teknik persis kayak DrawNoisePlasmaStrip di TwinsRework.cs
        // (sourceRect sengaja MELEBIHI lebar asli texture-nya, SamplerState.LinearWrap yang
        // dipasang di Begin() nge-tile-in otomatis - satu draw call cukup buat seluruh
        // panjang beam), cuma di-duplikasi lokal di sini (lihat catatan di atas DrawBeamVisual).
        // ==========================================
        private void DrawBeamNoiseStrip(Vector2 startScreen, float rotation, float length, float thickness, Color tint, float scrollSpeedTexelsPerTick, float scrollSeedOffset)
        {
            Texture2D noiseTex = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/TheTwins/TwinsBeamNoise").Value;
            if (noiseTex == null || length <= 0f || thickness <= 0f)
                return;

            int scrollX = (int)(Main.GameUpdateCount * scrollSpeedTexelsPerTick + scrollSeedOffset);

            Rectangle sourceRect = new Rectangle(scrollX, 0, (int)length, noiseTex.Height);
            Vector2 originInSourceSpace = new Vector2(0f, noiseTex.Height * 0.5f);
            Vector2 scale = new Vector2(1f, thickness / noiseTex.Height);

            Main.spriteBatch.Draw(noiseTex, startScreen, sourceRect, tint, rotation, originInSourceSpace, scale, SpriteEffects.None, 0f);
        }

        // StartThickness -> FullThickness (dinormalisasi jadi 0..1) di GrowTicks tick pertama,
        // tahan di 1 selama tengah, lalu turun ke ~0 di ShrinkTicks tick terakhir sebelum
        // Kill(). Identik sama formula GetVisualScale() di RedBeam.cs.
        private float GetVisualScale()
        {
            if (lifeTimer < GrowTicks)
            {
                float t = (float)lifeTimer / GrowTicks;
                return MathHelper.Lerp(StartThickness, FullThickness, t) / FullThickness;
            }

            int remaining = BeamLifeTime - lifeTimer;
            if (remaining < ShrinkTicks)
            {
                float t = MathHelper.Clamp((float)remaining / ShrinkTicks, 0f, 1f);
                return MathHelper.Lerp(0.05f, FullThickness, t) / FullThickness;
            }

            return 1f;
        }
    }
}
