using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    // =========================================================================================
    // 🛑 [PATTERN 9 - SPAWN ANIMATION] Cutscene pembuka, SATU KALI doang seumur hidup NPC ini
    // (di-trigger dari blok `if (!initialized)` di PlutoHead.cs, BUKAN lewat pool gacha
    // NPC.ai[0]==0f -- makanya nomor pattern-nya sengaja "dibuang jauh" ke 9 biar ga numpuk sama
    // Pattern 1-8 yang emang isi pool acak).
    //
    // 🛑 [RALAT] Versi sebelumnya nge-teleport Pluto ke titik acak jauh di luar layar dulu (atas/
    // kiri/kanan) baru lari masuk. SEKARANG DIHAPUS -- Pluto TIDAK di-relokasi sama sekali. Dia
    // lari dari POSISI ASLI-NYA APA ADANYA (di manapun dia ke-spawn di dunia, sejauh apapun itu)
    // LANGSUNG menuju titik yang udah ditentukan: titik yang jadi fokus kamera yang lagi ditarik
    // (~50 block di atas player, lihat SpawnAnimCamAboveDistance) -- SESUAI REQUEST.
    //
    // 🛑 [RALAT #2] Stage 2 dulu namanya "RoarHold" (Pluto roar + screen shake). SEKARANG DIGANTI
    // jadi "BossIntro" -- BUKAN teriak sambil screen shake kayak dulu lagi, tapi layar player
    // nge-gelap (bener-bener gelap, bukan setengah) + muncul teks judul boss merah ala
    // "Mechanical Collapse" / "XL-08 Pluto" (ketik per huruf + glow scan + fade), SESUAI REQUEST.
    // Screenshake-nya udah gak dipake lagi, TAPI ada 2 SFX baru:
    //   - Typing.ogg  : dimainin tiap 1 HURUF baru nongol pas judul atas lagi diketik.
    //   - SoundID.Roar: suara Roar bawaan Terraria (legacy id 15, style 0), dimainin SEKALI pas
    //                   nama boss "XL-08 Pluto" pertama kali muncul.
    //
    // Alurnya (4 stage, reuse ai[1]=stage & ai[2]=timer, gaya sama kayak pattern lain):
    //   Stage 0 (CameraPullIn)  : layar player yang jadi target (NPC.target) ditarik ke titik
    //                             ~50 block DI ATAS dirinya sendiri. Pluto sendiri diem dulu di
    //                             posisi dia ke-spawn (ga dipindah sama sekali).
    //   Stage 1 (RunIn)         : Begitu kamera sampai, Pluto "berlari" (dash) CEPAT dari posisi
    //                             dia SEKARANG (berapapun jaraknya) menuju titik fokus kamera itu.
    //                             Kecepatan dihitung dinamis dari jarak supaya durasi larinya
    //                             tetap kerasa konsisten baik deket maupun jauh banget (lihat
    //                             DesiredRunDuration + clamp kecepatan min/max).
    //   Stage 2 (BossIntro)     : Pluto berhenti mendadak. Kamera TETEP di posisi ketarik (ga
    //                             diapa-apain di stage ini). Sub-fase-nya (dihitung murni dari
    //                             timer, ga butuh field ai[] tambahan):
    //                               a) Darken   : layar pelan-pelan nge-gelap (IntroDarkenDuration)
    //                               b) Type     : judul "Mechanical Collapse" (BossIntroTitleText)
    //                                             muncul PER HURUF (typewriter)
    //                               c) Name     : begitu judul kelar diketik, nama boss
    //                                             "XL-08 Pluto" (BossIntroNameText) LANGSUNG
    //                                             muncul utuh (bukan diketik lagi)
    //                               d) GlowScan : sapuan glow kiri -> kanan ngelewatin kedua teks
    //                                             (IntroGlowScanDuration)
    //                               e) Hold     : teks diem sebentar (IntroHoldDuration)
    //                               f) FadeOut  : teks & layar hitam fade away BARENGAN
    //                                             (IntroFadeOutDuration), abis ini fight dimulai
    //                             DAN di titik AWAL stage inilah (pas Pluto baru berhenti)
    //                             HasTriggeredBackgroundReveal dinyalain (lihat
    //                             PlutoBackgroundSystem.cs -- baru dari sini background boss
    //                             beneran "pop" muncul, bukan dari awal NPC ke-spawn).
    //   Stage 3 (CameraReturn)  : Layar player pelan-pelan ditarik balik ke posisi normal.
    // Begitu Stage 3 kelar, pattern LANGSUNG nembak Pattern 1 (Normal Dash) tanpa lewat gacha
    // picker (NPC.ai[0]==0f).
    //
    // Invincibility Head/Body/Tail selama SELURUH pattern ini sudah di-handle terpisah:
    // - Head       : PlutoHead.cs, kondisi `NPC.dontTakeDamage` di AI() utama.
    // - Body/Tail  : PlutoBody.cs, `isTeleportInvinciblePhase` (juga bypass smart-turn-clamp,
    //                penting banget di sini karena Head bisa aja lari dari SANGAT jauh).
    //
    // 🎥 [KAMERA] Ditarik lewat ModPlayer.ModifyScreenPosition() -- lihat PlutoSpawnCameraPlayer.cs.
    // Field `SpawnAnimCameraOffset` di bawah dihitung LOKAL & DETERMINISTIK dari ai[1]/ai[2]
    // (yang otomatis network-synced kayak field ai[] NPC lainnya), jadi TIDAK butuh sinkronisasi
    // manual tambahan lewat SendExtraAI/ReceiveExtraAI -- semua client bakal ngitung offset yang
    // sama persis selama stage/timer-nya sama, konsisten sama pola pattern lain di file ini.
    //
    // 🖋️ [BOSS INTRO OVERLAY] Semua sub-progress Stage 2 (darken alpha, jumlah huruf yang udah
    // keketik, dst) di-expose lewat property public di bawah, dipakai buat DRAW aja di
    // PlutoBossIntroSystem.cs (ModSystem terpisah). File ini SENGAJA cuma ngitung angka progress-
    // nya doang, ga megang SpriteBatch/Draw sama sekali -- biar logic AI & rendering tetep kepisah.
    // Font-nya di-load di FontAssetSystem.cs (HerrFochGradient.dynamicfont).
    // =========================================================================================
    public partial class PlutoHead
    {
        private const int SpawnStageCameraPullIn = 0;
        private const int SpawnStageRunIn = 1;
        private const int SpawnStageBossIntro = 2;
        private const int SpawnStageCameraReturn = 3;

        // 🛑 [LOKASI BALANCING JARAK TARIK KAMERA] 50 block di atas player (16px/block). Titik ini
        // JUGA jadi titik tujuan lari Pluto di Stage 1 -- SESUAI REQUEST ("gerak ke arah kamera
        // player yang sedang ditarik, ke lokasi yang sudah ditentukan").
        private const float SpawnAnimCamAboveDistance = 50f * 16f;

        private const int CamPullInDuration = 40;   // ~0.67 detik
        private const int CamReturnDuration = 35;   // ~0.58 detik

        // 🛑 [KECEPATAN LARI DINAMIS] Berapapun jarak Pluto ke titik tujuan ("sejauh apapun dia
        // berada"), durasi larinya diusahakan tetep di sekitar angka ini (ticks) biar kerasa
        // konsisten -- kecepatan aktualnya dihitung dari distance/DesiredRunDuration, lalu
        // di-clamp biar ga jadi lemot banget (kalau kebetulan udah deket) atau ngebut ga masuk
        // akal (kalau jaraknya ekstrem jauh).
        private const float DesiredRunDuration = 50f;
        private const float MinRunSpeed = 40f;
        private const float MaxRunSpeed = 260f;

        // =====================================================================================
        // 🛑 [BOSS INTRO TEXT - TIMING] Semua durasi sub-fase Stage 2. Ditulis kecil-kecil biar
        // gampang di-tweak individual tanpa ganggu urutan/logic-nya.
        // =====================================================================================
        public const string BossIntroTitleText = "Mechanical Collapse"; // teks gede di atas
        public const string BossIntroNameText = "XL-08 Pluto";          // nama boss, di tengah bawah judul

        private const int IntroDarkenDuration = 15;      // layar mulai nge-gelap (~0.25 detik)
        private const float TicksPerTypedChar = 2.5f;    // kecepatan ketik judul atas (per huruf)
        private const int IntroGlowScanDuration = 40;     // sapuan glow kiri->kanan (~0.67 detik)
        private const int IntroHoldDuration = 40;         // teks diem abis glow scan (~0.67 detik)
        private const int IntroFadeOutDuration = 30;      // teks + layar hitam fade bareng (~0.5 detik)

        // Durasi ketik dihitung dari panjang teks judul, biar kalau teksnya diganti ga perlu
        // ngitung ulang manual.
        private static readonly int BossIntroTypeDuration = (int)Math.Ceiling(BossIntroTitleText.Length * TicksPerTypedChar);

        private static int BossIntroDarkenEnd => IntroDarkenDuration;
        private static int BossIntroTypeEnd => BossIntroDarkenEnd + BossIntroTypeDuration;
        private static int BossIntroGlowEnd => BossIntroTypeEnd + IntroGlowScanDuration;
        private static int BossIntroHoldEnd => BossIntroGlowEnd + IntroHoldDuration;
        private static int BossIntroFadeOutEnd => BossIntroHoldEnd + IntroFadeOutDuration;

        // --- Progress Stage 2, dibaca sama PlutoBossIntroSystem.cs buat nge-draw ---
        public float BossIntroDarkenAlpha { get; private set; } = 0f;      // 0..1, alpha layar hitam
        public int BossIntroTypedCharCount { get; private set; } = 0;      // berapa huruf judul atas yg keliatan
        public bool BossIntroShowBottomText { get; private set; } = false; // udah waktunya nama boss keliatan?
        public float BossIntroGlowScanProgress { get; private set; } = -1f; // 0..1 selama sapuan, -1 kalau lagi off
        public float BossIntroContentAlpha { get; private set; } = 1f;     // dipake fade-out teks di akhir

        // Offset kamera SAAT INI (world px) yang bakal ditambahin ke Main.screenPosition lewat
        // PlutoSpawnCameraPlayer -- HANYA dipakai/dibaca kalau IsSpawnAnimationActive true.
        public Vector2 SpawnAnimCameraOffset { get; private set; } = Vector2.Zero;

        public bool IsSpawnAnimationActive => NPC.active && NPC.ai[0] == 9f;

        // Dipake PlutoBossIntroSystem.cs buat nentuin kapan overlay teks boss intro digambar.
        public bool IsBossIntroActive => NPC.active && NPC.ai[0] == 9f && (int)NPC.ai[1] == SpawnStageBossIntro;

        // 🛑 [SINKRON BACKGROUND] Dinyalain PERSIS pas Pluto berhenti & mulai Boss Intro (Stage 2
        // dimulai), dipakai PlutoBackgroundSystem.cs buat nentuin kapan background boss beneran
        // "pop" -- BUKAN dari awal NPC ini ke-spawn (biar reveal-nya nyambung sama momen dramatis
        // ini, bukan duluan).
        public bool HasTriggeredBackgroundReveal { get; private set; } = false;

        private void ExecuteSpawnAnimationPattern(Player player) {
            int stage = (int)NPC.ai[1];
            int timer = (int)NPC.ai[2];

            if (stage == SpawnStageCameraPullIn) {
                // Pluto diem dulu di posisi dia ke-spawn -- TIDAK dipindah/di-teleport sama sekali,
                // cuma diredam biar ga ngambang aneh kalau ada sisa velocity dari spawn.
                NPC.velocity *= 0.9f;

                float pullProgress = MathHelper.Clamp(timer / (float)CamPullInDuration, 0f, 1f);
                pullProgress = pullProgress * pullProgress * (3f - 2f * pullProgress); // smoothstep
                SpawnAnimCameraOffset = new Vector2(0f, -SpawnAnimCamAboveDistance) * pullProgress;

                timer++;
                if (timer >= CamPullInDuration) {
                    NPC.ai[1] = SpawnStageRunIn;
                    NPC.ai[2] = 0f;

                    // 🛑 [LARI DARI MANAPUN DIA BERADA] Titik tujuan = titik yang sama persis
                    // dijadiin fokus kamera yang lagi ditarik (SESUAI REQUEST), BUKAN posisi acak.
                    Vector2 arrivalPoint = player.Center + new Vector2(0f, -SpawnAnimCamAboveDistance);
                    Vector2 runDir = (arrivalPoint - NPC.Center).SafeNormalize(Vector2.Zero);
                    float distanceToArrival = Vector2.Distance(NPC.Center, arrivalPoint);

                    float runSpeed = MathHelper.Clamp(distanceToArrival / DesiredRunDuration, MinRunSpeed, MaxRunSpeed);
                    NPC.velocity = runDir * runSpeed;
                    NPC.rotation = runDir.ToRotation();
                    dashDuration = MathHelper.Clamp(distanceToArrival / runSpeed, 20f, 240f);

                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
            else if (stage == SpawnStageRunIn) {
                NPC.rotation = NPC.velocity.ToRotation();

                timer++;
                if (timer >= (int)dashDuration) {
                    NPC.velocity = Vector2.Zero;
                    NPC.ai[1] = SpawnStageBossIntro;
                    NPC.ai[2] = 0f;

                    // 🛑 [BOSS INTRO REVEAL] Persis di momen ini Pluto berhenti -- background boss
                    // baru "pop" dari sini (lihat HasTriggeredBackgroundReveal & pemakaiannya di
                    // PlutoBackgroundSystem.cs). Sisa sub-fase Boss Intro (darken/ketik/glow/fade)
                    // di-drive murni dari timer di blok SpawnStageBossIntro di bawah.
                    HasTriggeredBackgroundReveal = true;

                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
            else if (stage == SpawnStageBossIntro) {
                NPC.velocity = Vector2.Zero;

                // Simpen nilai SEBELUM di-update, dipake buat deteksi "baru aja berubah" di bawah
                // (biar SFX Typing/Roar cuma bunyi PAS TRANSISI-nya doang, bukan tiap tick).
                int previousTypedCount = BossIntroTypedCharCount;
                bool previousShowBottomText = BossIntroShowBottomText;

                // -- a) Darken: layar pelan-pelan nge-gelap --
                BossIntroDarkenAlpha = MathHelper.Clamp(timer / (float)BossIntroDarkenEnd, 0f, 1f);

                // -- b) Type: judul atas diketik per huruf, mulai abis darken kelar --
                if (timer <= BossIntroDarkenEnd) {
                    BossIntroTypedCharCount = 0;
                }
                else {
                    float typeProgress = MathHelper.Clamp((timer - BossIntroDarkenEnd) / (float)BossIntroTypeDuration, 0f, 1f);
                    BossIntroTypedCharCount = (int)(typeProgress * BossIntroTitleText.Length);
                }

                // 🔊 [SFX - TYPING] Tiap ada huruf BARU yang nongol (bukan tiap tick), mainin
                // Typing.ogg sekali. Non-positional (ga dikasih posisi) biar kedengeran jelas
                // kemanapun kamera lagi ketarik.
                if (!Main.dedServ && BossIntroTypedCharCount > previousTypedCount) {
                    SoundEngine.PlaySound(new SoundStyle("TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/Typing"));
                }

                // -- c) Name: nama boss LANGSUNG muncul utuh begitu judul atas kelar diketik --
                BossIntroShowBottomText = timer >= BossIntroTypeEnd;

                // 🔊 [SFX - ROAR] Pas nama boss "XL-08 Pluto" PERTAMA KALI muncul (transisi
                // false -> true), mainin Roar bawaan Terraria (SoundID.Roar = legacy id 15, style 0).
                if (!Main.dedServ && BossIntroShowBottomText && !previousShowBottomText) {
                    SoundEngine.PlaySound(SoundID.Roar);
                }

                // -- d) GlowScan: sapuan kiri -> kanan, sekali jalan, abis kedua teks kelar tampil --
                if (timer >= BossIntroTypeEnd && timer < BossIntroGlowEnd) {
                    BossIntroGlowScanProgress = (timer - BossIntroTypeEnd) / (float)IntroGlowScanDuration;
                }
                else {
                    BossIntroGlowScanProgress = -1f; // -1 = lagi ga nyala
                }

                // -- e)+f) Hold lalu FadeOut: teks & layar hitam kompak nge-fade bareng --
                if (timer >= BossIntroHoldEnd) {
                    float fadeOutProgress = MathHelper.Clamp((timer - BossIntroHoldEnd) / (float)IntroFadeOutDuration, 0f, 1f);
                    BossIntroContentAlpha = 1f - fadeOutProgress;
                    BossIntroDarkenAlpha *= (1f - fadeOutProgress);
                }
                else {
                    BossIntroContentAlpha = 1f;
                }

                timer++;
                if (timer >= BossIntroFadeOutEnd) {
                    // Boss Intro kelar total -- pastiin semua ke-reset bersih sebelum lanjut ke
                    // Stage 3 (fight belum mulai di sini, masih nunggu kamera balik dulu).
                    BossIntroDarkenAlpha = 0f;
                    BossIntroContentAlpha = 0f;
                    BossIntroGlowScanProgress = -1f;

                    NPC.ai[1] = SpawnStageCameraReturn;
                    NPC.ai[2] = 0f;
                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
            else if (stage == SpawnStageCameraReturn) {
                float returnProgress = MathHelper.Clamp(timer / (float)CamReturnDuration, 0f, 1f);
                returnProgress = returnProgress * returnProgress * (3f - 2f * returnProgress); // smoothstep
                SpawnAnimCameraOffset = new Vector2(0f, -SpawnAnimCamAboveDistance) * (1f - returnProgress);

                timer++;
                if (timer >= CamReturnDuration) {
                    // --- Animasi kelar -- LANGSUNG nembak Pattern 1 (Normal Dash), TANPA lewat
                    // gacha picker (NPC.ai[0]==0f). Di titik inilah fight-nya beneran dimulai.
                    SpawnAnimCameraOffset = Vector2.Zero;
                    NPC.ai[0] = 1f;
                    NPC.ai[1] = 0f;
                    NPC.ai[2] = 0f;
                    NPC.ai[3] = 0f;
                    maxDashes = Main.rand.Next(5, 9);
                    NPC.netUpdate = true;
                }
                else {
                    NPC.ai[2] = timer;
                }
            }
        }
    }
}
