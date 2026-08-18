using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Systems
{
    /// <summary>
    /// Sistem border arena versi solo mod (full vanilla, tanpa dependency CalamityMod),
    /// SEKARANG berbentuk LINGKARAN dan digambar pakai tekstur "AuraOut.png"
    /// (TheSanity/Projectiles/AuraOut, 500x500, putih polos) yang di-tint warna
    /// merah -> hijau tergantung HealthPercent (1 = merah/sehat penuh, 0 = hijau/mau mati).
    ///
    /// Border ini gak lagi statis kayak versi kotak dulu - tiap Border sekarang
    /// punya delegate Tick yang dipanggil tiap frame (diisi dari luar, misal
    /// TwinsArenaGlobalNPC), jadi bisa dipakai buat bikin border ngikutin boss,
    /// narik player yang keluar, dsb, tanpa nge-hardcode logic Twins di sini.
    ///
    /// ==========================================
    /// REVISI: awalnya "Outward Glow" (cincin cahaya memancar KELUAR ~45 block dari tepi
    /// border), lalu jadi "Arena Fill Glow" (kabut ngisi bagian DALAM arena, dipotong tegas
    /// di tepi donat). SEKARANG REVISI KEDUA: glow VISUAL ini di-scale nutupin SELURUH DUNIA
    /// (dihitung dari diagonal ukuran map, lihat DrawSingleArenaGlowFill), TAPI border FISIK
    /// (donat AuraOut + collision ArenaBorderColliderNPC) TETAP di radius aslinya (700 buat
    /// Twins) - cuma layer glow ini doang yang membesar, gameplay-nya gak berubah. Di dalam
    /// radius arena asli efeknya tetap "energi berkumpul ke tepi" kayak sebelumnya, lalu di
    /// luar radius itu (sampai ke ujung dunia) kecerahannya dikunci penuh biar keliatan
    /// ngerata ke seluruh map. Overlay-nya tetap pakai noise TurbulentNoise.png punya
    /// Luminance (Assets/Noise/TurbulentNoise.png di repo Luminance), warnanya TETAP pakai
    /// animasi pulse merah->hijau->merah yang SAMA (GetAnimatedColor), bukan warna baru.
    ///
    /// Efek ini butuh sample 2 tekstur sekaligus (mask radial + noise) makanya WAJIB lewat
    /// custom shader - gak bisa cuma spriteBatch.Draw biasa kayak donat utama. Shader-nya
    /// di-load lewat jalur NATIVE tModLoader (ModContent.Request&lt;Effect&gt;), BUKAN lewat
    /// ShaderManager/ManagedShader punya Luminance - lihat catatan panjang di field
    /// GlowEffectPath soal kenapa. Lihat DrawArenaGlowFills()/DrawSingleArenaGlowFill() di
    /// bawah, dan file shader terpisah ArenaBorderGlow.fx.
    ///
    /// SENGAJA cuma nyala buat border DEFAULT (TexturePath == null, dipakai Twins) - border
    /// custom (TorchGod, TexturePath diisi) TIDAK dapet efek ini otomatis, biar gak nubruk
    /// visual custom spritesheet mereka sendiri. Bisa di-override manual per-Border lewat
    /// field EnableOutwardGlow kalau nanti mau dipakai border custom juga (nama field
    /// dipertahankan biar caller lama - TorchGod dkk - gak perlu diubah).
    /// ==========================================
    /// </summary>
    public class ArenaBorderSystem : ModSystem
    {
        public class Border
        {
            public Vector2 Center;

            // Radius SAAT INI (hasil animasi) - dipakai LANGSUNG buat 3 hal sekaligus:
            // visual (PostDrawTiles di bawah), hitbox collider (ArenaBorderColliderNPC,
            // di-update tiap tick lewat Tick delegate di TwinsArenaGlobalNPC), dan logic
            // "tarik player masuk" (PullPlayersInside). Jadi begitu Radius dianimasikan
            // (muncul/membesar/mengecil), KETIGANYA otomatis ikut berubah bareng - gak ada
            // lagi hitbox yang "ketinggalan" stuck di ukuran/posisi lama.
            public float Radius;

            // Radius TUJUAN animasi yang lagi berjalan (sama dengan Radius kalau lagi gak
            // ada animasi berjalan). Disediakan buat referensi luar kalau perlu tahu ke
            // mana border ini sedang menuju.
            public float TargetRadius;

            // True begitu border ini mulai proses "mengecil untuk dihapus" (lihat
            // PostUpdateEverything) - dipakai locking supaya AnimateRadiusTo(0, ...) itu
            // cuma dipicu SEKALI, dan supaya kode luar (mis. logic tarik player) bisa
            // berhenti narik player begitu border lagi dalam proses menghilang.
            public bool IsRemoving = false;

            // Dipanggil tiap tick SEBELUM RemovalCondition dicek. Dipakai buat
            // update posisi border (ngikutin boss), reposisi collider, narik
            // player yang keluar arena, dll. Boleh null kalau border statis.
            public Action<Border> Tick = null;

            // Dipanggil tiap tick. Kalau return true, border ini MULAI proses hapus
            // (animasi mengecil ke 0 dulu - lihat PostUpdateEverything), BUKAN langsung
            // hilang instan lagi.
            public Func<bool> RemovalCondition = () => true;

            // Dipanggil TEPAT SEBELUM border ini beneran dibuang dari ActiveBorders
            // (animasi mengecilnya udah kelar, Radius udah ~0). Dipakai buat beberes
            // referensi luar (mis. collider NPC-nya, field static di caller, dll).
            public Action OnFullyRemoved = null;

            // Tick game (Main.GameUpdateCount) waktu Border ini dibikin.
            // Dipakai sebagai titik nol buat animasi warna merah <-> hijau,
            // biar animasinya mulai dari awal tiap kali border baru muncul
            // (bukan ngikut jam global yang udah jalan dari awal dunia).
            public int SpawnTick = (int)Main.GameUpdateCount;

            // Opacity tekstur aura, biar gak nutupin gameplay kebanyakan.
            public float Opacity = 0.5f;

            // === Kustomisasi tekstur (dipakai TorchGod) ===
            // Kalau TexturePath == null, Border ini pakai tekstur bulat default
            // (AuraOut.png) + animasi warna merah<->hijau (UseColorPulse), sama
            // persis kayak perilaku lama (dipakai Twins).
            //
            // Kalau TexturePath diisi, Border ini pakai spritesheet sendiri:
            // FrameCount frame tersusun HORIZONTAL (kiri ke kanan) dalam SATU
            // baris, masing-masing frame dianggap PERSEGI (lebar = tinggi =
            // tinggi tekstur totalnya). Animasinya loop terus selama Border
            // masih ada, TicksPerFrame tick per frame, dan CrossfadeTicks tick
            // terakhir di tiap frame dipakai buat nge-blend/fade ke frame
            // berikutnya (bukan potong kasar).
            public string TexturePath = null;
            public int FrameCount = 1;
            public int TicksPerFrame = 10;
            public int CrossfadeTicks = 4;

            // False = tint putih polos (dipakai kalau tekstur sendiri sudah
            // "berwarna", misal TorchGod). True = tint merah<->hijau berbasis
            // waktu kayak sebelumnya (dipakai Twins/AuraOut).
            public bool UseColorPulse = true;

            // ==========================================
            // Kabut glow yang ngisi bagian DALAM arena (dulunya cincin keluar, lihat komentar
            // besar di atas kelas), di-overlay noise TurbulentNoise. Default true, tapi CUMA
            // benar-benar digambar kalau TexturePath == null juga (lihat DrawArenaGlowFills)
            // - jadi border custom style TorchGod otomatis gak kena walau field ini dibiarkan
            // true. Nama field dipertahankan "EnableOutwardGlow" (bukan di-rename) biar caller
            // lain yang udah pakai field ini gak perlu ikut diubah.
            // ==========================================
            public bool EnableOutwardGlow = true;

            // Asset tekstur custom punya Border ini sendiri, di-load lazy
            // begitu pertama kali digambar (lihat ArenaBorderSystem.PostDrawTiles).
            internal Asset<Texture2D> CustomTexture;

            private float radiusAnimFrom;
            private float radiusAnimTarget;
            private int radiusAnimStartTick;
            private int radiusAnimDuration; // <= 0 berarti gak ada animasi radius berjalan

            // Mulai animasi Radius menuju "target" selama "durationTicks" tick, pakai
            // ease-out quad (cepat di awal, halus mendekati ujung) - dipakai buat SEMUA
            // perubahan radius yang dulunya instan: muncul pertama kali ("timbul", dari 0),
            // membesar pas Phase 2 (dari radius saat ini ke radius baru), dan mengecil pas
            // mau dihapus (ke 0).
            public void AnimateRadiusTo(float target, int durationTicks)
            {
                radiusAnimFrom = Radius;
                radiusAnimTarget = target;
                radiusAnimStartTick = (int)Main.GameUpdateCount;
                radiusAnimDuration = Math.Max(1, durationTicks);
                TargetRadius = target;
            }

            // Dipanggil tiap tick dari ArenaBorderSystem.PostUpdateEverything SEBELUM
            // RemovalCondition dicek, biar Radius selalu up-to-date buat dipakai Tick
            // delegate (collider/pull-player) di tick yang sama.
            public void UpdateRadiusAnimation()
            {
                if (radiusAnimDuration <= 0)
                    return;

                int elapsed = (int)Main.GameUpdateCount - radiusAnimStartTick;
                float t = MathHelper.Clamp(elapsed / (float)radiusAnimDuration, 0f, 1f);
                float eased = 1f - (1f - t) * (1f - t); // ease-out quad

                Radius = MathHelper.Lerp(radiusAnimFrom, radiusAnimTarget, eased);

                if (t >= 1f)
                {
                    Radius = radiusAnimTarget;
                    radiusAnimDuration = 0; // animasi kelar, berhenti nge-update tiap tick
                }
            }
        }

        // Berapa tick buat satu arah animasi (merah -> hijau ATAU hijau -> merah).
        // 60 tick = 1 detik (di 60 tps), jadi 180 = 3 detik satu arah,
        // berarti satu putaran penuh (merah -> hijau -> merah) = 6 detik.
        private const float AnimationHalfCycleTicks = 180f;

        // Hitung warna animasi merah<->hijau berbasis waktu sejak Border dibikin.
        // Gak lagi nyambung ke health boss sama sekali - murni animasi visual.
        // DIPAKAI BARENGAN oleh donat (DrawBorder) MAUPUN glow ring (DrawSingleOutwardGlow) -
        // sengaja 1 sumber kebenaran yang sama biar 2 layer itu selalu senada warnanya di
        // frame yang sama, gak ada drift/lag beda fase.
        private static Color GetAnimatedColor(Border b)
        {
            float elapsed = Main.GameUpdateCount - b.SpawnTick;
            float speed = MathHelper.Pi / AnimationHalfCycleTicks;

            // sin() bikin transisi-nya halus (ease in/out) di ujung-ujungnya,
            // bukan lompat linear kaku.
            float percent = (MathF.Sin(elapsed * speed - MathHelper.PiOver2) + 1f) / 2f;

            return Color.Lerp(Color.Red, Color.LimeGreen, percent);
        }

        public static List<Border> ActiveBorders = new();

        // Berapa tick animasi "mengecil" pas border mau dihapus (RemovalCondition true),
        // dan seberapa kecil Radius-nya baru dianggap "beneran abis" (baru boleh dibuang
        // dari ActiveBorders begitu di bawah ambang ini).
        private const int ShrinkDurationTicks = 30;   // ~0.5 detik
        private const float RemovalRadiusThreshold = 4f;

        // Tekstur "AuraOut.png" - lingkaran putih polos 500x500 yang jadi dasar
        // buat semua border, tinggal di-tint warnanya tiap Border sesuai HealthPercent.
        private static Asset<Texture2D> auraTexture;
        private const string AuraTexturePath = "TheSanity/Projectiles/AuraOut";

        // Ukuran asli tekstur AuraOut.png. Kalau file-nya diganti ukurannya,
        // update juga angka ini (atau baca dari auraTexture.Width/Height langsung).
        private const float AuraTextureSize = 500f;

        // ==========================================
        // uIntensity: pengali kecerahan keseluruhan efek.
        // uInnerGlowMin: kecerahan minimum di TITIK TENGAH arena (0 = gelap polos di tengah,
        // 1 = serata tepi/sisa dunia).
        //
        // Radius efektif glow ini sendiri (dulu Radius arena, lalu direvisi lagi jadi radius
        // DUNIA - lihat komentar besar di atas kelas & DrawSingleArenaGlowFill) DIHITUNG
        // DINAMIS tiap frame dari ukuran map (Main.maxTilesX/Y), bukan konstanta di sini,
        // karena beda ukuran world (small/medium/large) beda juga radiusnya.
        // ==========================================
        private const float GlowIntensity = 1.6f;
        private const float GlowInnerMin = 0.4f;

        // Noise TurbulentNoise.png milik Luminance - path runtime-nya ngikutin struktur
        // folder Luminance sendiri (Assets/Noise/TurbulentNoise), di-prefix nama mod
        // "Luminance" karena ini request LINTAS MOD (bukan asset kepunyaan TheSanity).
        // Sumber: https://github.com/LucilleKarma/Luminance/blob/main/Assets/Noise/TurbulentNoise.png
        private const string TurbulentNoisePath = "Luminance/Assets/Noise/TurbulentNoise";
        private static Asset<Texture2D> turbulentNoiseTexture;

        // ==========================================
        // Shader glow di-load lewat jalur NATIVE tModLoader (Asset<Effect> biasa) - BUKAN
        // lewat ShaderManager/ManagedShader punya Luminance lagi. Alasannya: Luminance
        // ShaderManager butuh shader-nya "terdaftar" lewat mekanisme auto-discovery internal
        // mereka sendiri yang gak sepenuhnya jelas triggernya dari luar, dan bahkan
        // FargosSoulsMod (yang JUGA dependency Luminance) ternyata load shader custom
        // mereka SENDIRI lewat ModContent.Request<Effect> polos, bukan lewat ShaderManager -
        // jadi ini ngikutin pola yang sama biar gak gantung ke sistem yang gak konsisten.
        //
        // Path-nya "TheSanity/Effects/ArenaBorderGlow" - SESUAIKAN kalau lokasi file
        // ArenaBorderGlow.fx kamu beda (tinggal ganti string ini aja, gak ada logic lain
        // yang perlu disentuh).
        // ==========================================
        private const string GlowEffectPath = "TheSanity/Effects/ArenaBorderGlow";
        private static Asset<Effect> glowEffectAsset;

        public override void OnModLoad()
        {
            if (Main.dedServ)
                return;

            auraTexture = ModContent.Request<Texture2D>(AuraTexturePath, AssetRequestMode.AsyncLoad);
            turbulentNoiseTexture = ModContent.Request<Texture2D>(TurbulentNoisePath, AssetRequestMode.AsyncLoad);
            glowEffectAsset = ModContent.Request<Effect>(GlowEffectPath, AssetRequestMode.AsyncLoad);
        }

        public override void OnModUnload()
        {
            auraTexture = null;
            turbulentNoiseTexture = null;
            glowEffectAsset = null;
        }

        public override void PostUpdateEverything()
        {
            for (int i = ActiveBorders.Count - 1; i >= 0; i--)
            {
                Border b = ActiveBorders[i];

                b.Tick?.Invoke(b);
                b.UpdateRadiusAnimation();

                // Begitu RemovalCondition ketrigger PERTAMA KALINYA, JANGAN langsung hapus -
                // mulai animasi MENGECIL dulu ("mengecil pas hilang", simetris sama "timbul"
                // pas muncul di TwinsArenaGlobalNPC.OnSpawn). Baru beneran dibuang dari list
                // begitu Radius-nya udah nyaris 0 (cek di bawah).
                if (!b.IsRemoving && b.RemovalCondition())
                {
                    b.IsRemoving = true;
                    b.AnimateRadiusTo(0f, ShrinkDurationTicks);
                }

                if (b.IsRemoving && b.Radius <= RemovalRadiusThreshold)
                {
                    b.OnFullyRemoved?.Invoke();
                    ActiveBorders.RemoveAt(i);
                }
            }
        }

        // Gambar semua aura lingkaran di world space, di atas tile tapi di bawah
        // player/NPC (PostDrawTiles). Kalau mau di atas semuanya, pindah ke hook
        // draw lain (misal lewat PlayerLayer atau detour DrawPlayers).
        public override void PostDrawTiles()
        {
            if (Main.dedServ || ActiveBorders.Count == 0)
                return;

            // BlendState.Additive dipakai sengaja: kalau tekstur bagian yang
            // "harusnya transparan" itu ternyata masih ada sisa hitam-hitam
            // (bukan alpha 0 murni), additive bikin bagian hitam itu otomatis
            // gak kegambar (hitam + background = background), sementara bagian
            // putih/terang di tekstur tetep nyala nge-glow.
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (Border b in ActiveBorders)
                DrawBorder(b);

            Main.spriteBatch.End();

            // === Pass KEDUA - kabut glow yang ngisi bagian dalam arena (lihat komentar besar
            // di atas kelas ini). Sengaja DIPISAH dari batch donat di atas: shader custom butuh
            // SpriteSortMode.Immediate sendiri (Deferred gak bisa dipakai bareng Effect custom
            // per-draw-call), jadi gak bisa ditumpuk 1 Begin/End yang sama dengan donat. ===
            DrawArenaGlowFills();
        }

        private void DrawBorder(Border b)
        {
            Vector2 drawPos = b.Center - Main.screenPosition;
            Color baseTint = (b.UseColorPulse ? GetAnimatedColor(b) : Color.White) * b.Opacity;

            // === Border default (bulat, AuraOut.png statis) - perilaku lama ===
            if (b.TexturePath == null)
            {
                if (auraTexture == null || !auraTexture.IsLoaded)
                    return;

                Texture2D tex = auraTexture.Value;
                Vector2 circleOrigin = new Vector2(tex.Width / 2f, tex.Height / 2f);
                float circleScale = (b.Radius * 2f) / AuraTextureSize;

                Main.spriteBatch.Draw(tex, drawPos, null, baseTint, 0f, circleOrigin, circleScale, SpriteEffects.None, 0f);
                return;
            }

            // === Border custom (spritesheet ber-frame + crossfade) ===
            b.CustomTexture ??= ModContent.Request<Texture2D>(b.TexturePath, AssetRequestMode.AsyncLoad);

            if (!b.CustomTexture.IsLoaded)
                return;

            Texture2D sheet = b.CustomTexture.Value;
            int frameCount = Math.Max(1, b.FrameCount);
            int frameWidth = sheet.Width / frameCount;
            int frameHeight = sheet.Height;

            int ticksPerFrame = Math.Max(1, b.TicksPerFrame);
            int cycleLength = frameCount * ticksPerFrame;
            int tickInCycle = (int)(Main.GameUpdateCount - b.SpawnTick) % cycleLength;
            if (tickInCycle < 0)
                tickInCycle += cycleLength;

            int frameIndex = tickInCycle / ticksPerFrame;
            int nextFrameIndex = (frameIndex + 1) % frameCount;
            int tickInFrame = tickInCycle % ticksPerFrame;

            // CrossfadeTicks tick terakhir sebelum ganti frame dipakai buat
            // nge-fade frame sekarang keluar sambil frame berikutnya fade masuk.
            int crossfadeTicks = Math.Clamp(b.CrossfadeTicks, 0, ticksPerFrame);
            int fadeStartTick = ticksPerFrame - crossfadeTicks;

            float nextAlpha = 0f;
            if (crossfadeTicks > 0 && tickInFrame >= fadeStartTick)
                nextAlpha = (tickInFrame - fadeStartTick) / (float)crossfadeTicks;

            float currentAlpha = 1f - nextAlpha;

            Rectangle currentSrc = new Rectangle(frameIndex * frameWidth, 0, frameWidth, frameHeight);
            Rectangle nextSrc = new Rectangle(nextFrameIndex * frameWidth, 0, frameWidth, frameHeight);
            Vector2 frameOrigin = new Vector2(frameWidth / 2f, frameHeight / 2f);

            // Asumsi frame persegi (lebar == tinggi), jadi tinggi frame dipakai
            // sebagai referensi skala baik buat Circle maupun Square.
            float scale = (b.Radius * 2f) / frameHeight;

            if (currentAlpha > 0f)
                Main.spriteBatch.Draw(sheet, drawPos, currentSrc, baseTint * currentAlpha, 0f, frameOrigin, scale, SpriteEffects.None, 0f);

            if (nextAlpha > 0f)
                Main.spriteBatch.Draw(sheet, drawPos, nextSrc, baseTint * nextAlpha, 0f, frameOrigin, scale, SpriteEffects.None, 0f);
        }

        // ==========================================
        // Loop semua border aktif dan gambar kabut glow interior-nya kalau eligible. Di-skip
        // diam-diam (bukan error) kalau noise/shader belum siap - biasa kejadian sebentar di
        // awal load dunia sebelum async asset request-nya kelar, glow-nya bakal nongol sendiri
        // begitu asset-nya ready di frame-frame berikutnya.
        // ==========================================
        private void DrawArenaGlowFills()
        {
            if (turbulentNoiseTexture == null || !turbulentNoiseTexture.IsLoaded)
                return;

            if (glowEffectAsset == null || !glowEffectAsset.IsLoaded)
                return;

            Effect effect = glowEffectAsset.Value;
            if (effect == null)
                return;

            foreach (Border b in ActiveBorders)
            {
                // TexturePath != null -> border custom (TorchGod dkk), SENGAJA gak dapet
                // efek ini otomatis (lihat komentar EnableOutwardGlow di atas).
                if (!b.EnableOutwardGlow || b.TexturePath != null)
                    continue;

                if (b.Radius <= 1f)
                    continue;

                DrawSingleArenaGlowFill(b, effect);
            }
        }

        private void DrawSingleArenaGlowFill(Border b, Effect effect)
        {
            // SUMBER WARNA SAMA PERSIS dengan donat (GetAnimatedColor) - request eksplisit
            // "shading warna ya tetep, dari MERAH KE HIJAU KE MERAH LAGI". Opacity border
            // (b.Opacity) tetap dihormati di sini juga biar 2 layer konsisten seberapa
            // "keliatan"-nya.
            Color pulseColor = GetAnimatedColor(b) * b.Opacity;

            // ==========================================
            // REVISI: glow VISUAL ini sekarang di-scale nutupin SELURUH DUNIA, independen dari
            // radius border fisik (b.Radius, TETAP dipakai apa adanya buat donat + collision -
            // gak disentuh sama sekali di sini). worldRadius dihitung sebagai DIAGONAL penuh
            // peta (bukan cuma setengah lebar/tinggi) supaya kepastian nutupin seluruh dunia
            // terjamin BERAPAPUN posisi b.Center-nya (termasuk kalau kebetulan mepet pojok
            // map) - titik terjauh mana pun di dunia dari titik mana pun lainnya gak akan
            // pernah lebih jauh dari diagonal penuh ini.
            // ==========================================
            float worldWidthPx = Main.maxTilesX * 16f;
            float worldHeightPx = Main.maxTilesY * 16f;
            float worldRadius = MathF.Sqrt(worldWidthPx * worldWidthPx + worldHeightPx * worldHeightPx);

            // Fraction (relatif worldRadius) tempat brightness ramp-nya nyampe penuh - persis
            // di radius arena ASLI, biar bagian dalam arena kecil tetap kerasa "energi
            // berkumpul ke tepi" kayak sebelumnya, sebelum glow-nya ngerata terang ke seluruh
            // sisa dunia.
            float glowRampFraction = MathHelper.Clamp(b.Radius / worldRadius, 0.0001f, 1f);

            // Indexer EffectParameterCollection return null (bukan throw) kalau nama
            // parameternya gak ketemu di shader - makanya aman dipakai null-conditional (?.)
            // di sini, jaga-jaga kalau ada typo nama parameter antara .fx dan C# ini.
            effect.Parameters["uTime"]?.SetValue(Main.GameUpdateCount * 0.016f);
            effect.Parameters["uColor"]?.SetValue(pulseColor.ToVector4());
            effect.Parameters["uNoiseTextureObj"]?.SetValue(turbulentNoiseTexture.Value);
            effect.Parameters["uIntensity"]?.SetValue(GlowIntensity);
            effect.Parameters["uInnerGlowMin"]?.SetValue(GlowInnerMin);
            effect.Parameters["uGlowRampFraction"]?.SetValue(glowRampFraction);

            // SpriteSortMode.Immediate WAJIB dipakai buat Effect custom per-draw-call kayak
            // gini (Deferred nge-batch banyak sprite dan cuma nerapin shader di akhir batch,
            // gak per-sprite) - makanya batch ini dipisah sendiri dari batch donat di atas.
            Main.spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Additive, SamplerState.LinearClamp,
                DepthStencilState.None, RasterizerState.CullNone, effect, Main.GameViewMatrix.TransformationMatrix);

            Vector2 drawPos = b.Center - Main.screenPosition;

            // TextureAssets.MagicPixel = tekstur 1x1 putih polos bawaan vanilla - cuma
            // dipakai buat nyediain UV 0..1 penuh ke shader (isi visualnya 100% ditentukan
            // shader lewat noise+mask, bukan dari tekstur ini). Origin di tengah + scale =
            // diameter target - SEKARANG dipas-in ke diameter DUNIA (worldRadius * 2f),
            // BUKAN diameter arena (b.Radius * 2f) lagi, sesuai request "nutupin seluruh
            // World". Border fisik/donat/collision TETAP di b.Radius, gak ikut kebesaran.
            Texture2D pixel = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Vector2 origin = new Vector2(pixel.Width, pixel.Height) * 0.5f;
            float scale = worldRadius * 2f;

            Main.spriteBatch.Draw(pixel, drawPos, null, Color.White, 0f, origin, scale, SpriteEffects.None, 0f);

            Main.spriteBatch.End();
        }

        public override void OnWorldUnload()
        {
            ActiveBorders.Clear();
        }
    }
}
