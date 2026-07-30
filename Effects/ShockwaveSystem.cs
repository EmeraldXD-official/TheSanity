using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Common.Systems
{
    // ==================================================================================
    // SHOCKWAVE SCREEN SHADER SYSTEM
    // Berdasarkan tutorial "Shockwave Effect for tModLoader" (forums.terraria.org, thread
    // 81685). Filter ini di-load LAZY (baru pas pertama kali ada yang manggil Trigger()),
    // BUKAN di OnModLoad() -- soalnya kalau di-load pas OnModLoad(), exception dari resource
    // yang gagal/belum ke-compile bisa "lolos" dari try/catch kita dan disebabkan oleh tahap
    // internal loading tModLoader sendiri (LoadModContent), yang berujung mod di-disable paksa
    // meskipun sudah dibungkus try/catch di kode kita. Dengan lazy-load, proses Trigger() itu
    // dipanggil belakangan (pas Explode()), jauh setelah tahap loading mod selesai, jadi aman.
    //
    // Selain itu dipakai ModContent.RequestIfExists<T>() (bukan ModContent.Request<T>())
    // -- API ini KHUSUS buat kasus "load kalau ada, kalau nggak ya skip aja", dan TIDAK PERNAH
    // throw exception walau file-nya belum ada / gagal ke-compile. Jadi mod dijamin ga pernah
    // crash gara-gara shader ini, apapun kondisinya.
    //
    // WAJIB: shader source "ShockwaveEffect.fx" (disertakan terpisah) harus ke-compile jadi
    // "ShockwaveEffect.xnb" pas Build mod (tModLoader otomatis compile file .fx yang taruh di
    // folder Effects/ pas kamu Build). Kalau abis ganti/nambah .fx tapi tetep ga kebaca:
    //   1. Coba Build ulang (bukan cuma Reload) -- tModLoader kadang nge-cache hasil compile lama.
    //   2. Hapus folder "bin" & "obj" di ModSources/TheSanity/, baru Build lagi dari nol.
    //   3. Pastikan path persis: ModSources/TheSanity/Effects/ShockwaveEffect.fx
    //
    // 🛑 [DIPAKAI MANUAL] Jalur ripple/bulge di atas SENGAJA "manual" (kamu yang panggil
    // UpdateProgress() tiap tick & Stop() sendiri pas selesai) -- soalnya dipakai sama
    // PlutoElectroBall.cs yang butuh kontrol progress/fade custom (bola hidup lama, timing
    // ledakannya khusus). JANGAN diubah jadi auto-animate, nanti bentrok sama kontrol manual
    // punya PlutoElectroBall.
    //
    // Buat proyektil yang OnKill()-nya cuma kepanggil sekali terus langsung hilang DAN bisa
    // meledak BANYAK BERSAMAAN (kayak PlutoBomb), pakai TriggerBombShockwave() -- itu bagian
    // "POOL BOMB" di bawah, sistem terpisah dengan slot filter sendiri-sendiri per ledakan,
    // auto-maju & auto-selesai sendiri tanpa perlu panggil UpdateProgress() manual.
    // ==================================================================================
    public class ShockwaveSystem : ModSystem
    {
        // 🛑 [LOKASI GANTI PATH SHADER] sesuaikan kalau kamu naruh file .fx di folder lain
        private const string ShaderAssetPath = "TheSanity/Effects/ShockwaveEffect";

        public const string FilterName = "TheSanityShockwave";

        private static bool attemptedLoad = false;
        private static bool shaderLoaded = false;

        // 🛑 [JALUR BULGE - TERPISAH TOTAL DARI RIPPLE] Filter/state sendiri, FilterName beda,
        // load lazy sendiri. Ga disentuh sama sekali oleh jalur Trigger/UpdateProgress/Stop
        // (ripple) di atas maupun jalur TriggerOneShot (PlutoBomb) -- 3 sistem ini jalan
        // independen, bisa dipakai bareng-bareng sekaligus tanpa saling ganggu.
        public const string BulgeFilterName = "TheSanityBulge";
        private static bool attemptedBulgeLoad = false;
        private static bool bulgeShaderLoaded = false;

        // ==============================================================================
        // 🛑 [POOL BOMB - BISA MELEDAK BARENGAN/DOBEL] Beda dari jalur ripple manual di atas
        // (yang cuma 1 filter tunggal, dipakai ElectroBall yang biasanya cuma 1 ekor aktif),
        // PlutoBomb sering meledak BANYAK SEKALIGUS di arena. Kalau semua bomb rebutan 1 filter
        // yang sama, bomb kedua-dst yang meledak di tick yang sama/berdekatan bakal ke-skip
        // total (filternya "keliatan masih aktif" punya bomb pertama).
        //
        // Solusinya: sediain beberapa SLOT filter independen (nama filter beda-beda per slot,
        // Effect di-Clone() sendiri-sendiri per slot juga -- lihat EnsureLoaded() soal kenapa
        // Clone() ini penting). TriggerBombShockwave() bakal nyariin slot yang lagi nganggur;
        // kalau semua slot lagi kepake (misal >8 bomb meledak bersamaan), slot yang paling
        // deket selesai bakal "dipinjam paksa" (dicabut duluan) buat ledakan yang baru.
        //
        // BombPoolSize = 8 udah lebih dari cukup buat kondisi normal (bomb-bomb arena Pluto ga
        // realistis meledak >8 bersamaan persis di tick yang sama), tapi silakan naikin angka
        // ini kalau ternyata masih kurang.
        private const int BombPoolSize = 8;

        private static readonly string[] bombFilterNames = BuildBombFilterNames();
        private static readonly bool[] bombSlotAttemptedLoad = new bool[BombPoolSize];
        private static readonly bool[] bombSlotShaderLoaded = new bool[BombPoolSize];
        private static readonly bool[] bombSlotActive = new bool[BombPoolSize];
        private static readonly int[] bombSlotTimer = new int[BombPoolSize];
        private static readonly int[] bombSlotDuration = new int[BombPoolSize];
        private static readonly float[] bombSlotProgressStart = new float[BombPoolSize];
        private static readonly float[] bombSlotProgressEnd = new float[BombPoolSize];
        private static readonly float[] bombSlotPeakOpacity = new float[BombPoolSize];

        private static string[] BuildBombFilterNames()
        {
            string[] names = new string[BombPoolSize];
            for (int i = 0; i < BombPoolSize; i++)
                names[i] = "TheSanityBombShockwave" + i;
            return names;
        }

        // 🛑 [POOL BOMB TERPISAH DARI JALUR MANUAL] Pool bomb di atas sama sekali TIDAK disentuh
        // oleh jalur manual (Trigger/UpdateProgress/Stop) yang dipakai PlutoElectroBall, jadi
        // pemakaian manual itu tetap jalan persis seperti sebelumnya, ga ada yang berubah dari
        // sisi dia.
        public override void OnModUnload()
        {
            attemptedLoad = false;
            shaderLoaded = false;
            attemptedBulgeLoad = false;
            bulgeShaderLoaded = false;

            for (int i = 0; i < BombPoolSize; i++)
            {
                bombSlotAttemptedLoad[i] = false;
                bombSlotShaderLoaded[i] = false;
                bombSlotActive[i] = false;
            }
        }

        public override void PostUpdateEverything()
        {
            UpdateBombPool();
        }

        // Dipanggil otomatis sekali doang (lazy) pas pertama kali Trigger() dipanggil.
        //
        // 🛑 [FIX BUG "BULGE GA KELIATAN" / PARAMETER SALING TIMPA] ModContent.RequestIfExists
        // itu ngembaliin asset yang di-CACHE tModLoader -- artinya effectAsset.Value di sini bakal
        // ngasih instance Effect YANG SAMA PERSIS (objek yang sama di memori) ke jalur ripple, jalur
        // bulge, MAUPUN ke tiap slot pool bomb di bawah, karena semuanya minta ke path .fx yang sama.
        // Padahal parameter shader (uColor, uProgress, uOpacity, uTint, uMaxRange) itu nempel ke
        // OBJEK Effect-nya, bukan ke masing-masing Filter/ScreenShaderData yang minjem dia. Jadi
        // kalau 2 filter aktif bersamaan & sama-sama minjem Effect yang sama, yang belakangan
        // manggil UseProgress/UseOpacity/dll bakal NIMPA nilai punya yang duluan -- makanya bulge
        // ElectroBall keliatan ga jalan (ketiban nilai ripple), dan ini juga bakal kejadian kalau
        // beberapa bomb meledak bersamaan pakai Effect yang sama.
        //
        // Fixnya: `.Clone()` Effect-nya SEBELUM dibungkus jadi ScreenShaderData, biar tiap filter
        // (ripple, bulge, tiap slot pool bomb) pegang OBJEK Effect independen sendiri-sendiri --
        // parameter satu ga bakal pernah numpuk/nimpa punya yang lain lagi.
        private static void EnsureLoaded()
        {
            if (attemptedLoad || Main.netMode == NetmodeID.Server)
                return;

            attemptedLoad = true;

            // RequestIfExists TIDAK throw kalau resource-nya ga ada -- cuma return false.
            if (ModContent.RequestIfExists(ShaderAssetPath, out Asset<Effect> effectAsset, AssetRequestMode.ImmediateLoad))
            {
                Effect clonedEffect = effectAsset.Value.Clone();
                Ref<Effect> screenRef = new Ref<Effect>(clonedEffect);
                Filters.Scene[FilterName] = new Filter(new ScreenShaderData(screenRef, "Shockwave"), EffectPriority.VeryHigh);
                Filters.Scene[FilterName].Load();
                shaderLoaded = true;
            }
            else
            {
                shaderLoaded = false;
            }
        }

        // 🛑 [LOKASI PANGGIL SHOCKWAVE] Panggil ini pas objek meledak. rippleCount/Size/Speed
        // default yang enak buat ledakan gede: (3, 5, 15). Silakan tweak sesuai selera.
        //
        // 🛑 [TAMBAHAN WARNA] tintColor (opsional) = warna yang mau dikasih ke pita gelombang
        // shockwave-nya (lihat ShockwaveEffect.fx -- parameter uTint). Biarin null kalau mau
        // shockwave polos/transparan kayak sebelumnya (ga ada campuran warna sama sekali).
        // tintStrength = seberapa kental warnanya nge-blend ke pita gelombang (0..1, default
        // 0.4f udah cukup kerasa tapi ga norak/nutupin layar).
        public static void Trigger(Vector2 origin, float rippleCount = 3f, float rippleSize = 5f, float rippleSpeed = 15f, Color? tintColor = null, float tintStrength = 0.4f)
        {
            EnsureLoaded();

            if (!shaderLoaded || Main.netMode == NetmodeID.Server)
                return;

            if (!Filters.Scene[FilterName].IsActive())
            {
                var shaderData = Filters.Scene.Activate(FilterName, origin)
                    .GetShader()
                    .UseColor(rippleCount, rippleSize, rippleSpeed)
                    .UseTargetPosition(origin);

                ApplyTint(shaderData, tintColor, tintStrength);
            }
        }

        // 🛑 [DIPISAH BIAR AMAN] Nyoba akses Shader.Value.Parameters langsung -- dibungkus
        // try/catch soalnya ini bukan lewat method UseX() bawaan ScreenShaderData (yang udah
        // pasti aman dipanggil kapan aja). Kalau ternyata .fx belum di-rebuild ulang jadi .xnb
        // (parameter "uTint" belum ada di shader hasil compile lama), ini bakal gagal senyap
        // aja -- shockwave tetep jalan normal cuma tanpa warna, TIDAK bikin mod crash.
        private static void ApplyTint(ScreenShaderData shaderData, Color? tintColor, float tintStrength)
        {
            if (!tintColor.HasValue)
                return;

            try
            {
                Color c = tintColor.Value;
                shaderData.Shader?.Parameters["uTint"]?.SetValue(new Vector4(c.R / 255f, c.G / 255f, c.B / 255f, MathHelper.Clamp(tintStrength, 0f, 1f)));
            }
            catch
            {
                // Diamkan -- warna cuma bonus visual, jangan sampai ganggu shockwave utamanya.
            }
        }

        // 🛑 [JANGKAUAN/RANGE] Set parameter uMaxRange (lihat ShockwaveEffect.fx) dalam satuan
        // PIXEL layar. maxRangeTiles <= 0 berarti TANPA BATAS (perilaku lama). 1 tile = 16px.
        // Dibungkus try/catch sama kayak ApplyTint -- kalau .fx belum di-rebuild ulang jadi .xnb
        // (parameter "uMaxRange" belum ada di shader hasil compile lama), shockwave tetap jalan
        // normal cuma tanpa batasan jangkauan, TIDAK bikin mod crash.
        private static void ApplyMaxRange(ScreenShaderData shaderData, float maxRangeTiles)
        {
            if (maxRangeTiles <= 0f)
                return;

            try
            {
                float maxRangePixels = maxRangeTiles * 16f;
                shaderData.Shader?.Parameters["uMaxRange"]?.SetValue(maxRangePixels);
            }
            catch
            {
                // Diamkan -- range cuma pembatas visual, jangan sampai ganggu shockwave utamanya.
            }
        }

        // 🛑 [DIGANTI JADI WRAPPER KE POOL BOMB] Dulu method ini pakai SATU filter tunggal
        // (FilterName yang sama kayak jalur ripple manual ElectroBall) -- itu artinya kalau
        // 2 bomb meledak barengan, panggilan kedua bakal di-SKIP TOTAL (lihat cek
        // `if (!Filters.Scene[FilterName].IsActive())` di Trigger()), bukan nambah efek baru,
        // malah ke-drop diam-diam. Sekarang method ini cuma wrapper tipis yang nerusin ke
        // TriggerBombShockwave() (lihat di bawah, bagian POOL BOMB) -- itu yang beneran pakai
        // slot terpisah per ledakan biar bisa dobel/tumpuk kalau banyak bomb meledak sekaligus.
        // Signature & nama method LAMA sengaja dipertahankan biar kode lain yang masih manggil
        // TriggerOneShot() (kalau ada) tetap jalan tanpa perlu diubah.
        public static void TriggerOneShot(Vector2 origin, float rippleCount = 1.5f, float rippleSize = 1.8f, float rippleSpeed = 11f, int duration = 55, float progressStart = -1f, float progressEnd = 3.5f, float peakOpacity = 170f, Color? tintColor = null, float tintStrength = 0.4f, float maxRangeTiles = 0f)
        {
            TriggerBombShockwave(origin, rippleCount, rippleSize, rippleSpeed, duration, progressStart, progressEnd, peakOpacity, tintColor, tintStrength, maxRangeTiles);
        }

        // Panggil tiap tick selama shockwave berlangsung buat majuin & (opsional) fade out wave-nya.
        // progress biasanya di-range -3..3 (0 = titik ledakan), opacity = kekuatan distorsinya.
        public static void UpdateProgress(float progress, float opacity)
        {
            if (!shaderLoaded || Main.netMode == NetmodeID.Server)
                return;

            if (Filters.Scene[FilterName].IsActive())
            {
                Filters.Scene[FilterName].GetShader().UseProgress(progress).UseOpacity(opacity);
            }
        }

        // Panggil pas efeknya udah selesai (misal di OnKill si proyektil) biar filter-nya bisa
        // dipakai lagi buat ledakan berikutnya.
        public static void Stop()
        {
            if (!shaderLoaded || Main.netMode == NetmodeID.Server)
                return;

            if (Filters.Scene[FilterName].IsActive())
            {
                Filters.Scene[FilterName].Deactivate();
            }
        }

        // ==============================================================================
        // JALUR BULGE (kubah/lensa cembung sesaat) -- lihat ShockwaveEffect.fx pass "Bulge"
        // buat penjelasan bedanya sama ripple. Dipakai manual sama pemanggilnya (kamu yang
        // itung & majuin "envelope" 0..1 tiap tick, mirip gaya UpdateProgress ripple), biar
        // bentuk naik-turun kekuatan kubahnya bisa disesuaikan per pemakaian (misal biar
        // ElectroBall bisa punya kurva sendiri, beda dari efek lain yang mungkin nanti
        // dipakein bulge juga).
        // ==============================================================================

        private static void EnsureBulgeLoaded()
        {
            if (attemptedBulgeLoad || Main.netMode == NetmodeID.Server)
                return;

            attemptedBulgeLoad = true;

            if (ModContent.RequestIfExists(ShaderAssetPath, out Asset<Effect> effectAsset, AssetRequestMode.ImmediateLoad))
            {
                // 🛑 [FIX BULGE GA KELIATAN] .Clone() di sini WAJIB -- tanpa ini, bulge minjem
                // Effect OBJEK YANG SAMA PERSIS dengan filter ripple (lihat penjelasan panjang di
                // EnsureLoaded() di atas). Efeknya: pas Explode() manggil Trigger() (ripple) LALU
                // TriggerBulge() (bulge) di tick yang sama, keduanya bolak-balik nimpa parameter
                // uColor/uProgress/uOpacity satu sama lain tiap tick (siapa yang manggil UseX()
                // belakangan yang menang) -- ripple jadi rusak diam-diam & bulge PRAKTIS GA PERNAH
                // kelihatan efeknya karena keburu ketiban nilai punya ripple lagi tick berikutnya.
                Effect clonedEffect = effectAsset.Value.Clone();
                Ref<Effect> bulgeRef = new Ref<Effect>(clonedEffect);
                Filters.Scene[BulgeFilterName] = new Filter(new ScreenShaderData(bulgeRef, "Bulge"), EffectPriority.VeryHigh);
                Filters.Scene[BulgeFilterName].Load();
                bulgeShaderLoaded = true;
            }
            else
            {
                bulgeShaderLoaded = false;
            }
        }

        // 🛑 [LOKASI PANGGIL BULGE] Panggil SEKALI pas mulai efeknya (misal pas Explode()).
        // radius = seberapa lebar kubahnya (dalam satuan layar yang di-normalisasi tinggi
        // layar -- 0.5f kira-kira setengah tinggi layar, coba-coba aja sesuai selera).
        // tintColor/tintStrength sama kayak jalur ripple (lihat ApplyTint di atas).
        public static void TriggerBulge(Vector2 origin, float radius = 0.6f, Color? tintColor = null, float tintStrength = 0.5f)
        {
            EnsureBulgeLoaded();

            if (!bulgeShaderLoaded || Main.netMode == NetmodeID.Server)
                return;

            if (!Filters.Scene[BulgeFilterName].IsActive())
            {
                var shaderData = Filters.Scene.Activate(BulgeFilterName, origin)
                    .GetShader()
                    .UseColor(radius, 0f, 0f)
                    .UseTargetPosition(origin);

                ApplyTint(shaderData, tintColor, tintStrength);
            }
        }

        // Panggil tiap tick selama efek bulge berlangsung. envelope = 0..1, biasanya naik
        // cepat ke 1 lalu turun pelan ke 0 lagi (kamu yang itung kurvanya di sisi pemanggil,
        // biar bentuknya bisa disesuaikan). opacity = kekuatan dorongan lensanya di titik ini.
        public static void UpdateBulgeProgress(float envelope, float opacity)
        {
            if (!bulgeShaderLoaded || Main.netMode == NetmodeID.Server)
                return;

            if (Filters.Scene[BulgeFilterName].IsActive())
            {
                Filters.Scene[BulgeFilterName].GetShader().UseProgress(envelope).UseOpacity(opacity);
            }
        }

        // Panggil pas efek bulge-nya udah selesai (misal barengan sama Stop() punya ripple).
        public static void StopBulge()
        {
            if (!bulgeShaderLoaded || Main.netMode == NetmodeID.Server)
                return;

            if (Filters.Scene[BulgeFilterName].IsActive())
            {
                Filters.Scene[BulgeFilterName].Deactivate();
            }
        }

        // ==============================================================================
        // POOL BOMB -- implementasi (lihat penjelasan field-field-nya di atas)
        // ==============================================================================

        // Load lazy PER SLOT (baru pas slot itu kepake pertama kali), Effect di-Clone() sendiri
        // per slot -- WAJIB, biar tiap bomb yang meledak bersamaan punya parameter shader sendiri
        // sendiri, ga saling nimpa (lihat penjelasan Clone() panjang di EnsureLoaded()).
        private static void EnsureBombSlotLoaded(int slot)
        {
            if (bombSlotAttemptedLoad[slot] || Main.netMode == NetmodeID.Server)
                return;

            bombSlotAttemptedLoad[slot] = true;

            if (ModContent.RequestIfExists(ShaderAssetPath, out Asset<Effect> effectAsset, AssetRequestMode.ImmediateLoad))
            {
                Effect clonedEffect = effectAsset.Value.Clone();
                Ref<Effect> screenRef = new Ref<Effect>(clonedEffect);
                Filters.Scene[bombFilterNames[slot]] = new Filter(new ScreenShaderData(screenRef, "Shockwave"), EffectPriority.VeryHigh);
                Filters.Scene[bombFilterNames[slot]].Load();
                bombSlotShaderLoaded[slot] = true;
            }
            else
            {
                bombSlotShaderLoaded[slot] = false;
            }
        }

        // Cari slot nganggur; kalau semua lagi kepake, "pinjam paksa" slot yang paling deket
        // selesai (sisa waktu paling sedikit) -- lebih baik motong ekor ledakan lama yang udah
        // hampir pudar daripada nge-drop ledakan yang baru aja kejadian.
        private static int FindFreeBombSlot()
        {
            for (int i = 0; i < BombPoolSize; i++)
            {
                if (!bombSlotActive[i])
                    return i;
            }

            int bestSlot = 0;
            int bestRemaining = int.MaxValue;
            for (int i = 0; i < BombPoolSize; i++)
            {
                int remaining = bombSlotDuration[i] - bombSlotTimer[i];
                if (remaining < bestRemaining)
                {
                    bestRemaining = remaining;
                    bestSlot = i;
                }
            }
            return bestSlot;
        }

        // 🛑 [LOKASI PANGGIL SHOCKWAVE BOMB - BISA DOBEL/TUMPUK] Ini yang dipakai PlutoBomb.OnKill().
        // Beda dari Trigger()/TriggerOneShot() lama: tiap panggilan dapet SLOT SENDIRI dari pool
        // di atas, jadi kalau 2+ bomb meledak di tick yang sama/berdekatan, efeknya BENERAN dobel/
        // tumpuk di layar (bukan ke-skip kayak sebelumnya).
        //
        // maxRangeTiles = seberapa jauh (dalam satuan BLOCK/tile, 1 tile = 16px) shockwave ini
        // masih keliatan dari titik ledakan sebelum di-fade habis. 0 (default) = tanpa batas,
        // sama kayak sebelumnya. Request kamu: 20 tile.
        public static void TriggerBombShockwave(Vector2 origin, float rippleCount = 1.5f, float rippleSize = 1.8f, float rippleSpeed = 11f, int duration = 30, float progressStart = -1f, float progressEnd = 1.6f, float peakOpacity = 70f, Color? tintColor = null, float tintStrength = 0.45f, float maxRangeTiles = 20f)
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            int slot = FindFreeBombSlot();
            EnsureBombSlotLoaded(slot);

            if (!bombSlotShaderLoaded[slot])
                return;

            string filterName = bombFilterNames[slot];

            var shaderData = Filters.Scene.Activate(filterName, origin)
                .GetShader()
                .UseColor(rippleCount, rippleSize, rippleSpeed)
                .UseTargetPosition(origin);

            ApplyTint(shaderData, tintColor, tintStrength);
            ApplyMaxRange(shaderData, maxRangeTiles);

            bombSlotActive[slot] = true;
            bombSlotTimer[slot] = 0;
            bombSlotDuration[slot] = Math.Max(duration, 1);
            bombSlotProgressStart[slot] = progressStart;
            bombSlotProgressEnd[slot] = progressEnd;
            bombSlotPeakOpacity[slot] = peakOpacity;
        }

        // Dipanggil tiap tick dari PostUpdateEverything() -- majuin & fade-out tiap slot bomb yang
        // lagi aktif secara independen (formula sama kayak jalur one-shot lama: progress di-lerp
        // progressStart..progressEnd, opacity fade out di 40% durasi terakhir).
        private static void UpdateBombPool()
        {
            if (Main.netMode == NetmodeID.Server)
                return;

            for (int i = 0; i < BombPoolSize; i++)
            {
                if (!bombSlotActive[i] || !bombSlotShaderLoaded[i])
                    continue;

                bombSlotTimer[i]++;
                float t = bombSlotTimer[i] / (float)bombSlotDuration[i];
                float progress = MathHelper.Lerp(bombSlotProgressStart[i], bombSlotProgressEnd[i], MathHelper.Clamp(t, 0f, 1f));
                const float fadeStart = 0.6f;
                float opacity = t < fadeStart
                    ? bombSlotPeakOpacity[i]
                    : MathHelper.Lerp(bombSlotPeakOpacity[i], 0f, (t - fadeStart) / (1f - fadeStart));

                string filterName = bombFilterNames[i];
                if (Filters.Scene[filterName].IsActive())
                {
                    Filters.Scene[filterName].GetShader().UseProgress(progress).UseOpacity(opacity);
                }

                if (bombSlotTimer[i] >= bombSlotDuration[i])
                {
                    bombSlotActive[i] = false;
                    if (Filters.Scene[filterName].IsActive())
                    {
                        Filters.Scene[filterName].Deactivate();
                    }
                }
            }
        }
    }
}
