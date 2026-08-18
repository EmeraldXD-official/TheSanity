using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using YourModName.Content.NPCs;

namespace TheSanity.Projectiles
{
    // ==========================================
    // GreenBolt — "sibling" dari RedPhantasmalBolt, pakai kurva speed exponential yang sama
    // POLA-nya (StartSpeed -> GrowthRate^tick -> clamp MaxSpeed, arah dikunci di ai[0], gak ada
    // homing sama sekali), TAPI beda angka & beda render total:
    //
    //   - StartSpeed LEBIH PELAN 50% dari RedPhantasmalBolt (dihitung langsung dari konstanta
    //     Red-nya: RedPhantasmalBolt.StartSpeed * 0.5f = 1f).
    //   - GrowthRate LEBIH CEPAT naiknya dari RedPhantasmalBolt (1.10f vs 1.06f Red-nya).
    //   - MaxSpeed 2x LIPAT dari RedPhantasmalBolt (dihitung dari RedPhantasmalBolt.MaxSpeed * 2f
    //     = 180f).
    //
    //   - Sprite-nya CUSTOM (bukan numpang asset vanilla kayak Red yang minjem PhantasmalBolt),
    //     diambil dari "TheSanity/Projectiles/GreenBolt", 18x18, satu frame doang (gak ada
    //     spritesheet animasi).
    //
    //   - FIX: after-image versi sebelumnya (offset sintetis dari posisi SEKARANG, dihitung
    //     manual lawan arah velocity) ternyata gak muncul pas ditest. Sekarang DIGANTI TOTAL
    //     ke pola oldPos[]/oldRot[] yang SAMA PERSIS kayak RedPhantasmalBolt (yang udah terbukti
    //     jalan) - trail-nya beneran ngerekam histori posisi asli proyektil tiap tick, otomatis
    //     "menyesuaikan kecepatan" (jarak antar titik histori otomatis lebih lebar pas proyektil
    //     lagi ngebut, lebih rapet pas masih pelan), dan tiap titik makin ke belakang makin kecil
    //     & makin transparan biar smooth kayak ekor, bukan barisan ghost yang patah-patah.
    //
    //   - Ekornya digambar pakai BLEND ADDITIVE (bukan alpha-blend biasa) - itu yang bikin dia
    //     kerasa "bersinar"/glow, dan warnanya Color.White (gak ada tint tambahan) supaya yang
    //     kepake beneran warna ASLI sprite GreenBolt-nya sendiri, bukan warna baru.
    //
    //   - "Glow in the dark tanpa sprite glow tambahan": sprite utamanya digambar pakai warna
    //     Color.White FIXED (bukan `lightColor` yang dikasih parameter PreDraw), jadi dia
    //     selalu full-bright walau lagi di tempat gelap - efeknya mirip glowmask tapi tanpa
    //     butuh texture glow terpisah. Plus Lighting.AddLight tiap tick buat nerangin area
    //     sekitarnya (hijau).
    //
    //   - COSTUME: suara spawn sekarang TwinsSounds.GreenBoltShot (dulu sempet
    //     SoundID.DD2_BetsyFireballShot).
    //
    //   - OnHitPlayer nge-infeksi Cursed Inferno selama 5 detik (300 tick).
    //
    //   - GARIS PEMANDU (telegraph): sekarang munculin garis aim/laser HIJAU searah tembak
    //     pas awal kemunculannya, PERSIS teknik (stretch-pixel) & bentuknya (outline + core)
    //     kayak DrawGuideLine punya RedPhantasmalBolt - BEDANYA cuma nyala SEBENTAR
    //     (TelegraphDurationTicks = 30 tick, ~0,5 detik) di awal doang, bukan nyala terus-
    //     terusan sepanjang hidup proyektil kayak RedBolt. Selama window itu garisnya tetap
    //     ikut nempel di posisi/rotasi GreenBolt TERKINI (lihat DrawGuideLine di bawah).
    //
    // CATATAN ARAH SPRITE: di sini diasumsikan sprite GreenBolt.png default-nya menghadap KANAN
    // (rotation = angle langsung, gak ada offset). Kalau ternyata asetnya ngadep arah lain,
    // tinggal tambahin offset di baris `Projectile.rotation = angle;` di AI() (contoh: kurangin
    // MathHelper.PiOver2 kalau default-nya ngadep atas, sama kayak pola RedPhantasmalBolt).
    //
    // CATATAN LAIN: kayak RedPhantasmalBolt, proyektil ini diasumsikan serangan BOSS/musuh ke
    // player (hostile = true, friendly = false, ignore tileCollide, pierce/penetrate = -1).
    // ==========================================
    public class GreenBolt : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/GreenBolt";

        // ---- Kurva kecepatan exponential (dua konstanta di bawah DIHITUNG LANGSUNG dari
        // konstanta RedPhantasmalBolt, biar kalau nilai Red-nya diubah nanti, GreenBolt otomatis
        // ikut nyesuain, gak perlu update dua tempat manual). ----
        public const float StartSpeed = RedPhantasmalBolt.StartSpeed * 0.5f; // 50% lebih lambat dari Red
        public const float MaxSpeed = RedPhantasmalBolt.MaxSpeed * 2f;         // 2x lipat dari Red
        public const float GrowthRate = 1.10f;                                  // naik lebih cepat dari Red (1.06f)

        // Durasi Cursed Inferno: 5 detik * 60 tick/detik.
        private const int CursedInfernoDuration = 300;

        // ==========================================
        // GARIS PEMANDU (telegraph) — request: sama konsepnya kayak DrawGuideLine punya
        // RedPhantasmalBolt (garis lurus panjang searah tembak, teknik stretch-pixel yang
        // sama), TAPI BEDA DI DURASI: RedBolt garisnya nyala TERUS SELAMA proyektil hidup,
        // GreenBolt ini garisnya CUMA nyala ~0.5 detik (TelegraphDurationTicks = 30 tick) di
        // AWAL kemunculannya doang, abis itu ilang total (proyektil-nya sendiri tetap lanjut
        // hidup & melesat seperti biasa, cuma garis pemandunya yang berhenti digambar).
        // Selama window 0,5 detik itu, garisnya tetap "nempel" & ikut gerak posisi/rotasi
        // GreenBolt TERKINI (bukan statis dari titik spawn doang) - dicek dari Projectile.ai[1]
        // (counter umur proyektil, sudah ada & di-increment di AI()) di PreDraw.
        // ==========================================
        private const int TelegraphDurationTicks = 30; // ~0.5 detik (30 tick / 60 tick per detik)
        private const float TelegraphLineLength = 2600f; // sama panjangnya kayak punya RedBolt
        private const int TelegraphCoreThickness = 3;
        private const int TelegraphOutlineThickness = 6;
        private static readonly Color TelegraphLineColor = new Color(60, 255, 90); // hijau, senada tema GreenBolt

        public override void SetStaticDefaults()
        {
            Main.projFrames[Projectile.type] = 1; // sprite polos, satu frame

            // Ini yang bikin Projectile.oldPos[]/oldRot[] beneran ke-isi tiap tick (pola SAMA
            // kayak RedPhantasmalBolt) - tanpa ini, array oldPos-nya pendek/kosong dan trail-nya
            // gak akan pernah keliatan. TrailCacheLength = 20 -> ekornya lebih panjang, kira-kira
            // ~5 block pas proyektil lagi di kecepatan jelajahnya (panjang beneran tetap ngikutin
            // kecepatan SEKARANG karena basisnya histori posisi asli, bukan angka fix).
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 20;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.scale = 1f;

            Projectile.friendly = false;
            Projectile.hostile = true;

            Projectile.DamageType = DamageClass.Default;
            Projectile.penetrate = -1;    // tembus/pierce
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            // COSTUME: TwinsSounds.GreenBoltShot (dulu SoundID.DD2_BetsyFireballShot).
            // Cuma bunyi sekali pas spawn (tick pertama, SEBELUM ai[1] di-increment).
            if (Projectile.ai[1] == 0f)
            {
                SoundEngine.PlaySound(TwinsSounds.GreenBoltShot, Projectile.Center);
            }

            // ai[1] = umur proyektil dalam tick sejak spawn.
            Projectile.ai[1]++;

            float speed = StartSpeed * (float)System.Math.Pow(GrowthRate, Projectile.ai[1]);
            if (speed > MaxSpeed)
            {
                speed = MaxSpeed;
            }

            // Arah SELALU dibaca dari ai[0] (sudut, radian) yang dikunci sekali oleh spawner -
            // TIDAK PERNAH dihitung ulang / homing, persis pola RedPhantasmalBolt.
            float angle = Projectile.ai[0];
            Vector2 direction = angle.ToRotationVector2();
            Projectile.velocity = direction * speed;

            // Asumsi sprite default menghadap KANAN. Sesuaikan offset di sini kalau salah arah.
            Projectile.rotation = angle;

            // Glow HIJAU di sekitar proyektil (dynamic lighting, bukan sprite glow terpisah).
            Lighting.AddLight(Projectile.Center, 0.15f, 1.1f, 0.3f);
        }

        // Damage GreenBolt SENGAJA ignore SELURUH defense DAN damage reduction player, sama
        // persis kayak RedPhantasmalBolt/TwinsCursedBeam - lewat helper satu tempat di
        // TwinsDebuffGlobalProjectile biar konsisten sama semua sumber damage Twins lain.
        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            TwinsDebuffGlobalProjectile.IgnoreDefenseAndDamageReduction(ref modifiers);
        }

        // Kena GreenBolt ngasih Cursed Inferno selama 5 detik, DITAMBAH paket debuff standar
        // Twins (Broken Armor + Weak + Bleeding) - sama kayak attack lain (RedPhantasmalBolt,
        // TwinsCursedBeam, dll) - lihat TwinsDebuffGlobalProjectile.ApplyDebuffs.
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.CursedInferno, CursedInfernoDuration);
            TwinsDebuffGlobalProjectile.ApplyDebuffs(target);
        }

        // ==========================================
        // RENDER — ekor after-image dari Projectile.oldPos[]/oldRot[] (pola RedPhantasmalBolt,
        // terbukti jalan) + additive glow + taper mengecil-transparan ke belakang, lalu sprite
        // utama full-bright di atasnya.
        // ==========================================
        public override bool PreDraw(ref Color lightColor)
        {
            // Garis pemandu hijau CUMA digambar selama TelegraphDurationTicks (~0,5 detik)
            // PERTAMA sejak proyektil ini spawn - lihat komentar TelegraphDurationTicks di
            // atas. Projectile.ai[1] = umur proyektil dalam tick (sudah di-increment tiap
            // tick di AI(), dipakai juga buat kurva speed exponential), jadi tinggal
            // dibandingkan langsung di sini, gak perlu counter terpisah.
            if (Projectile.ai[1] <= TelegraphDurationTicks)
            {
                DrawGuideLine();
            }

            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Rectangle frameRect = texture.Bounds; // satu frame doang, ambil seluruh texture
            Vector2 origin = new Vector2(frameRect.Width * 0.5f, frameRect.Height * 0.5f);

            // ---------- 1. EKOR AFTER-IMAGE (additive blend = "bersinar"/glow) ----------
            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                // Titik histori yang kosong (proyektil baru aja spawn, belum sempet ngerekam
                // sebanyak itu) posisinya (0,0) - skip biar gak ada trail nyasar ke pojok map.
                if (Projectile.oldPos[i] == Vector2.Zero)
                {
                    continue;
                }

                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size * 0.5f - Main.screenPosition;
                float t = i / (float)Projectile.oldPos.Length; // 0 = paling deket badan, 1 = paling ekor

                float segAlpha = MathHelper.Lerp(0.9f, 0f, t);      // lebih tebel/opaque di awal ekor
                float segScale = MathHelper.Lerp(1f, 0.25f, t) * Projectile.scale; // taper lebih landai -> ekornya kerasa lebih "gemuk"

                // Color.White = TIDAK ada tint tambahan, jadi warna after-image = warna ASLI
                // sprite GreenBolt-nya sendiri (sesuai request). Additive blend di atas yang
                // bikin tumpukannya kerasa nyala/glow, bukan warnanya yang beda.
                Main.EntitySpriteDraw(
                    texture,
                    drawPos,
                    frameRect,
                    Color.White * segAlpha,
                    Projectile.oldRot[i],
                    origin,
                    segScale,
                    SpriteEffects.None,
                    0
                );
            }

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState,
                DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            // ---------- 2. SPRITE UTAMA ----------
            // Warna dipaksa Color.White (BUKAN parameter `lightColor`) biar sprite-nya selalu
            // full-bright / "nyala di gelap" walau lagi di tempat minim cahaya - efeknya mirip
            // glowmask tapi TANPA perlu texture glow terpisah.
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(
                texture,
                mainDrawPos,
                frameRect,
                Color.White,
                Projectile.rotation,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false;
        }

        // Garis pemandu HIJAU memanjang searah gerak - teknik stretch-pixel PERSIS sama kayak
        // DrawGuideLine punya RedPhantasmalBolt. BEDA rotasi: sprite GreenBolt default-nya
        // udah menghadap KANAN (Projectile.rotation = angle LANGSUNG, gak ada offset -PiOver2
        // kayak Red - lihat komentar "CATATAN ARAH SPRITE" di atas class), jadi di sini
        // Projectile.rotation dipakai APA ADANYA tanpa perlu ditambah balik PiOver2 lagi.
        // Cuma dipanggil selama TelegraphDurationTicks pertama (lihat PreDraw), abis itu
        // berhenti dipanggil sama sekali - proyektilnya sendiri TETAP lanjut melesat normal.
        private void DrawGuideLine()
        {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Vector2 start = Projectile.Center - Main.screenPosition;
            float rotation = Projectile.rotation; // udah PERSIS arah tembak asli (ai[0]), gak perlu offset balik

            // Kedip halus (bukan fade in/out) - sama filosofi kayak RedBolt, tapi karena
            // window hidupnya cuma ~0,5 detik, dia bakal kerasa lebih kayak "kilat" cepat
            // ketimbang telegraph yang nyala lama.
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
