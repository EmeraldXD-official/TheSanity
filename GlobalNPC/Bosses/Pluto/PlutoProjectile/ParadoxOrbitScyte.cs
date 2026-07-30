using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;

// MENGHOOK BUFFER SENSE DARI DIRECTORY CUSTOM KAMU
using TheSanity.Buff;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile
{
    // ==========================================================================================
    // PARADOX ORBIT SCYTE
    // Varian dari ParadoxScyte, dipakai khusus buat Pattern 6 (Scyte Orbit Storm) di PlutoHead.
    // Bedanya sama ParadoxScyte biasa: proyektil ini TIDAK melesat lurus ke player. Begitu
    // dipanggil, dia "dilempar" sedikit menjauh dari titik panggil Pluto lalu masuk fase SPIRAL:
    // terus berputar mengelilingi titik panggil itu sambil radiusnya perlahan membesar (jadi
    // kesannya muter sambil menjauh, kayak pusaran bilah yang melebar).
    //
    // 🛑 [CATATAN POSISI] Proyektil ini gerak FULL berbasis rumus (posisi absolut dihitung tiap
    // tick dari waktu hidupnya), BUKAN dari Projectile.velocity kayak biasa. Makanya velocity
    // sengaja di-nolkan tiap tick -- ini supaya lintasan spiral-nya presisi & gampang di-tweak.
    // ==========================================================================================
    public class ParadoxOrbitScyte : ModProjectile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/ParadoxScyte";

        // Path texture noise/glitch hasil generate -- ukurannya SAMA PERSIS dengan ParadoxScyte.png
        // (siluet noise-nya udah di-mask ngikutin bentuk bilah aslinya, jadi nempel pas & ga bocor
        // keluar bentuk sabit). Taruh file PNG-nya di path yang sama persis kek di bawah ini.
        private const string NoiseTexturePath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoProjectile/ParadoxScyteNoise";

        // =========================================================================
        // 🛑 [LOKASI BALANCING LAMA HIDUP] Timer manual DIHAPUS sesuai request -- proyektil ini
        // sekarang TIDAK dipaksa hilang oleh timer bikinan sendiri. Nilai di bawah cuma jaring
        // pengaman ekstrem (± 8 menit) supaya tidak ada proyektil "abadi" beneran nyangkut di
        // memori kalau suatu saat ada bug aneh. Dalam praktiknya proyektil ini bakal dibersihkan
        // sendiri oleh engine Terraria begitu radiusnya udah kelewat jauh dari semua player
        // (auto-despawn bawaan game buat proyektil yang keluar jauh dari jangkauan), PERSIS
        // seperti yang diminta -- bukan timer buatan kita lagi yang matiin dia.
        // =========================================================================
        private const int LifetimeSafetyCeiling = 28800; // ~8 menit, jaring pengaman doang

        // 🛑 [LOKASI BALANCING FASE LEMPAR AWAL] Berapa lama fase "dilempar menjauh" sebelum masuk
        // fase spiral penuh. Sengaja singkat, cuma buat kasih kesan "terlontar" dari titik panggil.
        private const int FlingDuration = 16;
        private const float FlingDistance = 70f; // jarak tempuh selama fase lempar (px)

        // =========================================================================
        // 🛑 [LOKASI BALANCING PERCEPATAN SPIRAL] Sekarang geraknya TIDAK LINEAR lagi -- dia mulai
        // pelan banget lalu makin lama makin ngebut seiring radiusnya membesar (kurva eksponensial),
        // dan radiusnya TIDAK ADA BATAS MAKSIMAL SAMA SEKALI (beneran bisa membesar tanpa henti
        // sampai akhirnya ke-despawn sendiri sama sistem Terraria di atas).
        //   SpiralGrowthBase   = titik awal skala pertumbuhan radius (px)
        //   SpiralGrowthRate   = seberapa tajam kurva eksponensial radiusnya (k) -- makin besar,
        //                        makin cepat dia "meledak" membesar setelah beberapa detik.
        //   SpiralAngularBase  = kecepatan muter dasar (radian/tick) saat baru mulai (pelan).
        //   SpiralAngularRampRate = seberapa cepat kecepatan muternya ikut naik seiring waktu.
        //   SpiralAngularMultiplierCap = biar putarannya ga sampe absurd/glitchy walau radius
        //                        tetap terus membesar tanpa batas (cap ini CUMA buat kecepatan
        //                        muternya, bukan buat radius/ukurannya).
        // =========================================================================
        // 🛑 [UPDATE BALANCING] Dipercepat -- versi lama kerasa "lambat lambat banget" beberapa
        // detik pertama. Sekarang base-nya sendiri udah lebih gede (jadi dari awal udah kerasa
        // gerak) DAN kurva eksponensialnya lebih tajam (naik/meledak lebih cepat), plus cap
        // kecepatan muternya dinaikkan biar di akhir beneran kerasa ngebut, bukan mentok pelan.
        private const float SpiralGrowthBase = 16f;
        private const float SpiralGrowthRate = 0.024f;
        private const float SpiralAngularBase = 0.034f;
        private const float SpiralAngularRampRate = 0.015f;
        private const float SpiralAngularMultiplierCap = 6f;

        // 🛑 [LOKASI BALANCING SPIN VISUAL BILAH] rotasi sprite itu sendiri (efek "muter kek kipas"),
        // ikut dipercepat seiring waktu (pakai multiplier yang sama dengan kecepatan muter orbit).
        private const float BladeSpinSpeed = 0.22f;

        // Ekor bayangan, sama konsepnya kek ParadoxScyte biasa tapi lebih pendek (proyektil ini
        // jumlahnya 10x tiap volley, jangan sampai kebanyakan overdraw).
        private Vector2[] tailSegments = new Vector2[14];
        private float[] tailRotations = new float[14];

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults() {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = LifetimeSafetyCeiling;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), 3 * 60);
        }

        public override void AI() {
            // =========================================================================
            // OVERRIDE DAMAGE BERDASARKAN DIFFICULTY (sinkron sama ParadoxScyte biasa)
            // =========================================================================
            if (Main.masterMode) {
                // 🛑 [LOKASI BALANCING DAMAGE MASTER MODE]
                Projectile.damage = 13;
            }
            else if (Main.expertMode) {
                // 🛑 [LOKASI BALANCING DAMAGE EXPERT MODE]
                Projectile.damage = 15;
            }
            else {
                // 🛑 [LOKASI BALANCING DAMAGE CLASSIC/NORMAL MODE]
                Projectile.damage = 20;
            }

            // =========================================================================
            // 🛑 [SEKALI SAAT SPAWN] Kunci titik panggil (origin) & sudut lontar awal.
            // localAI TIDAK disinkron lewat jaringan, tapi aman dipakai di sini karena nilainya
            // cuma diturunkan dari posisi spawn -- posisi spawn itu sendiri SUDAH disinkron via
            // Projectile.NewProjectile, jadi hasilnya konsisten di server maupun client.
            // =========================================================================
            if (Projectile.localAI[1] == 0f) {
                Projectile.localAI[1] = 1f; // penanda "sudah di-init"

                // ai[1] menyimpan sudut lontar awal (arah radian dari velocity saat NewProjectile
                // dipanggil di PlutoHead) -- dipakai sebagai sudut awal muter spiralnya.
                if (Projectile.velocity != Vector2.Zero) {
                    Projectile.ai[1] = Projectile.velocity.ToRotation();
                }

                // Simpan origin (titik panggil Pluto) ke localAI[0] via trik: karena localAI cuma
                // 2 slot float, kita simpan origin X & Y lewat sepasang field private non-network
                // (aman karena posisi spawn sendiri sudah sinkron server/client).
                originPoint = Projectile.Center;

                SoundEngine.PlaySound(SoundID.Item71, Projectile.Center);
            }

            float elapsedTime = LifetimeSafetyCeiling - Projectile.timeLeft;
            float launchAngle = Projectile.ai[1];

            Vector2 newCenter;

            if (elapsedTime < FlingDuration) {
                // ---------------- FASE 1: LEMPAR MENJAUH ----------------
                float flingProgress = elapsedTime / FlingDuration;
                // Ease-out biar lontaran kerasa "nyentak" di awal terus ngerem halus
                float easedProgress = 1f - (float)Math.Pow(1f - flingProgress, 3);
                float currentDistance = FlingDistance * easedProgress;

                Vector2 flingDir = launchAngle.ToRotationVector2();
                newCenter = originPoint + flingDir * currentDistance;

                Projectile.rotation += BladeSpinSpeed * 0.5f;
            }
            else {
                // ---------------- FASE 2: SPIRAL MENJAUH (MAKIN LAMA MAKIN CEPAT) ----------------
                float spiralTime = elapsedTime - FlingDuration;

                // Multiplier kecepatan muter -- mulai dari 1x (pelan, pakai SpiralAngularBase apa
                // adanya) lalu naik eksponensial seiring waktu, di-cap biar ga absurd.
                float angularMultiplier = 1f + (float)(Math.Exp(SpiralAngularRampRate * spiralTime) - 1f);
                angularMultiplier = MathHelper.Clamp(angularMultiplier, 1f, SpiralAngularMultiplierCap);
                float spiralAngle = launchAngle + spiralTime * SpiralAngularBase * angularMultiplier;

                // Radius TIDAK ADA BATAS ATAS -- kurva eksponensial murni, awalnya nyaris flat
                // (nambah pelan banget) terus makin lama makin "meledak" membesar tanpa henti.
                float radius = FlingDistance + SpiralGrowthBase * (float)(Math.Exp(SpiralGrowthRate * spiralTime) - 1f);

                Vector2 spiralDir = spiralAngle.ToRotationVector2();
                newCenter = originPoint + spiralDir * radius;

                Projectile.rotation += BladeSpinSpeed * angularMultiplier;

                // Sedikit percikan noise/glitch particle biar makin berasa "corrupt" pas mekar
                if (Main.rand.NextFloat() < 0.35f) {
                    Dust d = Dust.NewDustPerfect(newCenter, DustID.Electric, Main.rand.NextVector2Circular(1.2f, 1.2f));
                    d.noGravity = true;
                    d.color = Color.Red;
                    d.scale = Main.rand.NextFloat(0.5f, 0.9f);
                    d.fadeIn = 0.6f;
                }
            }

            Projectile.velocity = Vector2.Zero; // gerak full berbasis rumus, bukan physics
            Projectile.Center = newCenter;

            // =========================================================================
            // SIMULASI DELAY FISIK BAYANGAN (ROPE PHYSICS) -- sama kayak ParadoxScyte biasa
            // =========================================================================
            if (tailSegments[0] == Vector2.Zero) {
                for (int i = 0; i < tailSegments.Length; i++) {
                    tailSegments[i] = Projectile.Center;
                    tailRotations[i] = Projectile.rotation;
                }
            }

            tailSegments[0] = Projectile.Center;
            tailRotations[0] = Projectile.rotation;

            for (int i = 1; i < tailSegments.Length; i++) {
                float elasticity = 0.4f;
                tailSegments[i] = Vector2.Lerp(tailSegments[i], tailSegments[i - 1], elasticity);
                tailRotations[i] = Utils.AngleLerp(tailRotations[i], tailRotations[i - 1], elasticity);
            }
        }

        // Origin non-network -- aman karena diturunkan dari posisi spawn yang sudah sinkron
        // (lihat penjelasan di blok init AI() di atas).
        private Vector2 originPoint;

        public override bool PreDraw(ref Color lightColor) {
            SpriteBatch spriteBatch = Main.spriteBatch;
            Texture2D scyteTex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = scyteTex.Size() * 0.5f;

            // =========================================================================
            // 🆕 BAYANGAN/EKOR PAKAI TEXTURE NOISE (bukan salinan merah dari bilah asli lagi).
            // Texture noise ini udah di-mask persis ngikutin siluet ParadoxScyte.png, jadi
            // bentuknya tetap kebaca sebagai sabit, tapi teksturnya berbutir/glitch -- kesannya
            // "residu korup" yang ditinggalkan bilah utama pas melesat, bukan afterimage bersih.
            // Digambar pakai BlendState.Additive (bawaan XNA, tanpa shader custom) biar butiran
            // noise-nya nyala nimpa background dengan rapi & makin ke belakang makin pudar.
            // =========================================================================
            Texture2D noiseTex = ModContent.Request<Texture2D>(NoiseTexturePath).Value;
            Texture2D shadowTex = noiseTex ?? scyteTex; // fallback ke tex asli kalau noise gagal load
            Vector2 shadowOrigin = shadowTex.Size() * 0.5f;

            if (noiseTex != null) {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            for (int i = tailSegments.Length - 1; i > 0; i--) {
                if (tailSegments[i] == Vector2.Zero) continue;

                float trailProgress = (float)i / tailSegments.Length;

                // 🛑 [LOKASI BALANCING KETEBALAN BAYANGAN]
                float shadowOpacity = 0.85f * (1f - trailProgress);
                // Sedikit flicker biar noise-nya keliatan "hidup"/glitchy, bukan statis diam.
                float flicker = 0.75f + 0.25f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 16f + i * 0.9f + Projectile.identity * 0.7f);
                // 🛑 Ditintai MERAH (bukan putih) -- versi putih keliatan aneh, jadi warna bayangan
                // dikunci merah biar konsisten sama tema darah/paradox si sabit.
                Color shadowColor = Color.Red * (shadowOpacity * flicker);
                float shadowScale = Projectile.scale * (1f - trailProgress * 0.3f);

                Vector2 shadowDrawPos = tailSegments[i] - Main.screenPosition;

                spriteBatch.Draw(shadowTex, shadowDrawPos, null, shadowColor, tailRotations[i], shadowOrigin, shadowScale, SpriteEffects.None, 0);
            }

            if (noiseTex != null) {
                spriteBatch.End();
                spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
            }

            // ---------------- SABIT UTAMA (bersih, tanpa noise) ----------------
            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            spriteBatch.Draw(scyteTex, drawPos, null, Color.White, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}
