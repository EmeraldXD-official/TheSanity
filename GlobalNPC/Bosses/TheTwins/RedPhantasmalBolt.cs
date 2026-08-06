using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    // ==========================================
    // RedPhantasmalBolt — "plek ketiplek" clone dari PhantasmalBolt bawaan Terraria (proyektil
    // Phantasmal Eye punya Moon Lord), CUMA di-recolor MERAH (default-nya cyan/biru terang).
    //
    // UPDATE: proyektil ini sekarang jadi PENGGANTI "RedLaser" (ProjectileID.DeathLaser) yang
    // dipakai buat spike-wall di TwinsRetBeam.FireSpikeWall. Karena sekarang ini ModProjectile
    // sendiri (bukan numpang ID vanilla + GlobalProjectile hack kayak DeathLaser dulu), semua
    // logic ramp-up-nya bisa langsung nyatu di AI() sini sendiri — gak perlu lagi PreAI() hijack
    // + marker ai[0] kayak punya TwinsRedLaserAcceleration punya DeathLaser.
    //
    // PERUBAHAN DARI VERSI SEBELUMNYA:
    //   - hostile = true, friendly = false  -> ini serangan BOSS ke player, bukan proyektil
    //     ramah lagi.
    //   - HOMING DIHAPUS TOTAL. Spike-wall nembak LURUS tegak lurus garis beam, arahnya udah
    //     dikunci sekali pas spawn (dari NPC.Center ke titik tembak) dan gak pernah berubah/
    //     ngoreksi ke musuh kayak versi lama. FindNearestEnemy & Lerp velocity ke arah target
    //     dibuang semua.
    //   - Kecepatan sekarang EXPONENTIAL: mulai pelan (StartSpeed) terus naik tiap tick pakai
    //     kurva speed = StartSpeed * GrowthRate^tick, di-clamp ke MaxSpeed. Arah tetap dibaca
    //     dari sudut yang dikunci di ai[0] (bukan dihitung ulang dari velocity), biar gak ada
    //     celah drift/ngebelok numpuk kayak masalah yang dulu ditemuin di DeathLaser hack.
    //   - ai[0]  = sudut arah terkunci (radian), di-isi SEKALI oleh spawner (FireSpikeWall) lewat
    //              parameter ai0 di Projectile.NewProjectile(...).
    //   - ai[1]  = counter tick sejak spawn, buat hitung kurva exponential. Increment sendiri
    //              di AI() sini.
    //
    // Pattern render-nya TETAP nyontek dari EyeOfCthulhuPhase2.cs (gak berubah):
    //   - Texture di-arahin ke ASSET VANILLA (bukan asset custom mod kita) via string path,
    //     "Terraria/Images/Projectile_" + ProjectileID.PhantasmalBolt.
    //   - PreDraw override manual: ambil texture vanilla-nya langsung dari TextureAssets, gambar
    //     after-image trail dulu (loop Projectile.oldPos[]), baru sprite utama di atasnya —
    //     dua-duanya di-tint merah.
    //   - Frame sprite sheet-nya dihitung DINAMIS dari Main.projFrames[ProjectileID.PhantasmalBolt]
    //     (bukan di-hardcode angka).
    //
    // CATATAN ARAH: sprite PhantasmalBolt di file Terraria-nya MENGHADAP KE ATAS secara default,
    // jadi rotasinya butuh offset -MathHelper.PiOver2 biar "atas" sprite ngikutin arah gerak.
    // ==========================================
    public class RedPhantasmalBolt : ModProjectile
    {
        // Asset vanilla PhantasmalBolt di-load lewat property Texture ini (sama kayak pattern
        // referensi), walau ujung-ujungnya gak langsung dipakai buat gambar di sini — kita ambil
        // manual lagi di PreDraw lewat TextureAssets.Projectile[...].
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.PhantasmalBolt;

        // Tint merah buat sprite utama & after-image trail. RedTint ini dipakai sebagai
        // MULTIPLY terhadap warna asli sprite — masalahnya PhantasmalBolt asli itu cyan
        // (G & B tinggi, R hampir 0), jadi abis dikaliin doang hasilnya masih keliatan
        // teal/biru gelap, bukan merah pekat. Makanya di PreDraw di bawah, abis draw yang
        // di-tint biasa ini, kita tumpuk SEKALI LAGI draw solid merah (SolidRedOverlay) di
        // atasnya buat "nutup" sisa warna biru/cyan yang msaih nembus.
        private static readonly Color RedTint = new Color(255, 40, 40);

        // Overlay solid merah (hampir opaque) yang ditumpuk DI ATAS draw yang ditint biasa.
        // Karena ini alpha-blend biasa (bukan multiply), dia nutupin sisa hue biru/cyan dari
        // sprite asli dan bikin hasil akhirnya kerasa "merah pekat" solid, bukan cuma tint
        // muda yang masih nembus warna aslinya.
        private static readonly Color SolidRedOverlay = new Color(220, 0, 0);

        // ---- Kurva kecepatan exponential (dulu ada di TwinsRetBeam, sekarang pindah ke sini
        // biar proyektilnya self-contained — spawner cukup nembak arah, sisanya proyektil ini
        // yang urus sendiri). ----
        public const float StartSpeed = 2f;     // kecepatan awal pas baru muncul (pelan banget)
        // MaxSpeed dinaikin JAUH lebih tinggi dari sebelumnya (24f -> 90f) sesuai request biar
        // di ujung ramp-nya proyektil ini beneran "ngebut parah", bukan cuma agak cepat.
        // GrowthRate dibiarin sama (masih 1.06f/tick) - dia tetap butuh waktu buat nyampe
        // MaxSpeed baru ini (sekitar ~65 tick / ~1.1 detik dari StartSpeed), jadi ramp-nya
        // tetap kerasa "nambah kenceng" bukan langsung ngebut dari tick pertama.
        public const float MaxSpeed = 90f;      // kecepatan puncak/mentok - BENER BENER KENCENG
        public const float GrowthRate = 1.06f;  // pengali per-tick (>1 = exponential naik)

        // ==========================================
        // GARIS PEMANDU (bukan telegraph delay lagi) - proyektil ini LANGSUNG melesat sejak
        // tick pertama (persis AI lama, gak ada fase diem/nunggu sama sekali), CUMA sekarang
        // selalu nampilin garis merah panjang di depan arah gerak-nya SELAMA proyektil ini
        // hidup - gak pernah fade/ilang sampai proyektilnya sendiri Kill() (nembus ScanSafetyCap
        // atau timeLeft habis). Garis ini SELALU digambar dari posisi TERKINI proyektil
        // (bukan cuma dari titik spawn), jadi kesannya "nempel" & ikut maju di depannya.
        // ==========================================
        private const float TelegraphLineLength = 2600f; // BENER BENER PANJANG, nembus ujung layar manapun
        private const int TelegraphCoreThickness = 3;     // tetap tipis
        private const int TelegraphOutlineThickness = 6;
        private static readonly Color TelegraphLineColor = new Color(255, 30, 30);

        public override void SetStaticDefaults()
        {
            // Ambil jumlah frame ASLI dari PhantasmalBolt vanilla, dipakai juga buat proyektil kita.
            Main.projFrames[Projectile.type] = Main.projFrames[ProjectileID.PhantasmalBolt];

            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 10;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.scale = 1f;

            // Sekarang serangan BOSS ke player, bukan proyektil ramah lagi.
            Projectile.friendly = false;
            Projectile.hostile = true;

            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;   // tembus/pierce, khas laser lurus
            Projectile.tileCollide = false; // tembus block, sama kayak RedLaser spike-wall lama
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            // ==========================================
            // KECEPATAN EXPONENTIAL + ARAH TERKUNCI (gak ada homing sama sekali) - PERSIS
            // AI LAMA, langsung melesat dari tick pertama, TANPA fase diem/telegraph lagi.
            // ==========================================
            // ai[1] = umur proyektil dalam tick sejak spawn.
            Projectile.ai[1]++;

            float speed = StartSpeed * (float)System.Math.Pow(GrowthRate, Projectile.ai[1]);
            if (speed > MaxSpeed)
            {
                speed = MaxSpeed;
            }

            // Arah SELALU dibaca dari ai[0] (sudut, radian) yang dikunci sekali oleh spawner
            // pas NewProjectile — TIDAK PERNAH dihitung ulang dari velocity atau dari posisi
            // musuh, jadi proyektil ini dijamin melesat LURUS presisi sesuai arah tembak awal.
            float angle = Projectile.ai[0];
            Vector2 direction = angle.ToRotationVector2();

            Projectile.velocity = direction * speed;

            // Sprite PhantasmalBolt defaultnya ngadep ATAS, jadi rotasinya butuh offset
            // -PiOver2 biar "depan" sprite ngikutin arah gerak beneran (BUKAN cuma "angle"
            // mentah). Ini yang kelewat di versi sebelumnya makanya sprite-nya kerasa
            // "miring 90 derajat" — nembak ke kanan malah depannya ngadep ke atas, nembak ke
            // kiri malah ngadep ke bawah.
            Projectile.rotation = angle - MathHelper.PiOver2;

            // Animasi frame (kalau PhantasmalBolt vanilla emang punya lebih dari 1 frame).
            int frameCount = Main.projFrames[ProjectileID.PhantasmalBolt];
            if (frameCount > 1)
            {
                Projectile.frameCounter++;
                if (Projectile.frameCounter >= 5)
                {
                    Projectile.frameCounter = 0;
                    Projectile.frame = (Projectile.frame + 1) % frameCount;
                }
            }

            // Glow MERAH.
            Lighting.AddLight(Projectile.Center, 1.2f, 0.15f, 0.15f);
        }

        // Damage RedBolt SENGAJA ignore SELURUH armor/damage reduction player - armor
        // penetration digedein jauh di atas defense player manapun yang mungkin dicapai.
        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            modifiers.ArmorPenetration += 999f;
        }

        // Kena RedBolt ngasih paket debuff standar Twins (Broken Armor + Weak + Bleeding) -
        // lihat TwinsDebuffGlobalProjectile.ApplyDebuffs buat durasi & detail lengkapnya
        // (satu tempat, dipakai bareng semua sumber damage Twins yang lain).
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            TwinsDebuffGlobalProjectile.ApplyDebuffs(target);
        }

        // ==========================================
        // RENDER — sama persis kayak versi sebelumnya (pattern PreDraw EyeOfCthulhuPhase2):
        // ambil texture VANILLA langsung, gambar after-image trail dari oldPos[] dulu, baru
        // sprite utama di atasnya.
        // ==========================================
        public override bool PreDraw(ref Color lightColor)
        {
            // Garis pemandu SELALU digambar tiap frame selama proyektil ini hidup - gak
            // pernah fade/ilang, posisinya ikut nempel di titik TERKINI proyektil (bukan
            // cuma titik spawn), jadi kesannya garisnya "maju" bareng bolt-nya.
            DrawGuideLine();

            Texture2D texture = TextureAssets.Projectile[ProjectileID.PhantasmalBolt].Value;
            int frameCount = Main.projFrames[ProjectileID.PhantasmalBolt];
            int frameHeight = texture.Height / frameCount;

            Rectangle frameRect = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);
            Vector2 origin = new Vector2(texture.Width * 0.5f, frameHeight * 0.5f);

            // 1. DRAW AFTER-IMAGE TRAIL MERAH
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float alpha = (1f - (i / (float)Projectile.oldPos.Length)) * 0.65f;

                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    frameRect,
                    RedTint * alpha,
                    Projectile.oldRot[i],
                    origin,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                );

                // Overlay solid merah, biar sisa hue cyan/biru dari sprite asli ke-tutup dan
                // trail-nya kerasa merah pekat, bukan teal gelap.
                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    frameRect,
                    SolidRedOverlay * (alpha * 0.85f),
                    Projectile.oldRot[i],
                    origin,
                    Projectile.scale,
                    SpriteEffects.None,
                    0
                );
            }

            // 2. DRAW SPRITE UTAMA
            Vector2 mainPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(
                texture,
                mainPos,
                frameRect,
                RedTint * 0.95f,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            // Overlay solid merah di sprite utama juga, tumpukan terakhir biar hasil akhirnya
            // beneran merah pekat (nutupin sisa hue biru/cyan dari sprite vanilla aslinya).
            Main.EntitySpriteDraw(
                texture,
                mainPos,
                frameRect,
                SolidRedOverlay * 0.85f,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }

        // Garis pemandu MERAH memanjang searah gerak (Projectile.rotation, udah termasuk
        // offset -PiOver2 dari AI() - ditambah balik di sini biar arahnya PAS sama arah
        // tembak asli/ai[0], bukan arah sprite) - digambar pakai teknik stretch-pixel yang
        // sama kayak DrawAimLine di TwinsReworkOverride. SELALU digambar (gak fade, gak
        // ilang) dari posisi TERKINI proyektil selama dia masih hidup.
        private void DrawGuideLine()
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 start = Projectile.Center - Main.screenPosition;
            float rotation = Projectile.rotation + MathHelper.PiOver2; // balikin ke arah tembak asli (ai[0])

            // Kedip halus doang (BUKAN fade in/out) biar garisnya kerasa "hidup", tapi
            // selalu ada - alpha minimumnya cukup tinggi, gak pernah turun sampai transparan.
            float flicker = 0.8f + 0.2f * (float)System.Math.Sin(Main.GameUpdateCount * 1.1f);
            Rectangle sourceRect = new Rectangle(0, 0, pixel.Width, pixel.Height);
            Vector2 originInSourceSpace = new Vector2(0f, pixel.Height * 0.5f); // kiri, center vertikal

            // Outline tipis transparan.
            Color outlineColor = TelegraphLineColor * (flicker * 0.35f);
            Rectangle outlineDest = new Rectangle((int)start.X, (int)start.Y, (int)TelegraphLineLength, TelegraphOutlineThickness);
            Main.spriteBatch.Draw(pixel, outlineDest, sourceRect, outlineColor, rotation, originInSourceSpace, SpriteEffects.None, 0f);

            // Core lebih solid, tetap tipis.
            Color coreColor = TelegraphLineColor * (flicker * 0.75f);
            Rectangle coreDest = new Rectangle((int)start.X, (int)start.Y, (int)TelegraphLineLength, TelegraphCoreThickness);
            Main.spriteBatch.Draw(pixel, coreDest, sourceRect, coreColor, rotation, originInSourceSpace, SpriteEffects.None, 0f);
        }
    }
}
