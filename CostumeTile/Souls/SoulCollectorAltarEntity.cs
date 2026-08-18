using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using ReLogic.Utilities;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.ObjectData;

namespace TheSanity.CostumeTile
{
    // TileEntity yang nempel di tiap SoulCollectorAltar yang di-place di dunia.
    // Nyimpen jumlah soul yang udah dikumpulin altar ini (max 100.000), dan
    // dipakai buat nentuin titik dunia dari sprite SoulEyeBlue (target homing Souls.cs).
    public class SoulCollectorAltarEntity : ModTileEntity
    {
        public const int MaxSoulCapacity = 100000;
        public const int ReviveCost = 500;

        // Biaya convert: 100 Soul -> 1 Soul Token, lewat tombol "Convert" di GUI.
        public const int ConvertSoulCost = 100;

        public int SoulCount;

        // Slot khusus (1 item) tempat Soul Token hasil convert nangkring.
        // SENGAJA ga langsung masuk inventory player -- token-nya nunggu di sini
        // dulu, baru diambil manual lewat klik slot-nya di GUI (lihat
        // SoulTokenSlotElement di SoulCollectorUIState.cs). Slot ini CUMA nerima
        // item tipe SoulToken, ga bisa dipakai naro item lain.
        public Item TokenSlotItem = new Item();

        public bool IsFull => SoulCount >= MaxSoulCapacity;

        // =====================================================================
        // RITUAL REVIVE (animasi visual pas ngerevive NPC lewat tombol Revive)
        //
        // Timeline (dalam tick, 60 tick = 1 detik):
        //   10 soul dibagi jadi WaveCount (5) WAVE, isinya 2 soul per wave
        //   (1 kanan + 1 kiri) yang MUNCUL BARENGAN dalam 1 wave yang sama --
        //   BUKAN gantian satu-satu lagi. Antar wave berjarak WaveIntervalTicks
        //   (0,4 detik). Titik munculnya PAS di altar-nya sendiri (tengah
        //   horizontal, permukaan atas altar) -- BUKAN dari SoulEye yang
        //   posisinya ngambang tinggi di atas altar.
        //
        //   Begitu wave ke-w muncul (di tick w * WaveIntervalTicks), ke-2
        //   soul di wave itu jalanin fase-nya sendiri:
        //     - LateralPhaseTicks pertama (1 detik penuh): melesat menyebar
        //       lebar ke kanan/kiri.
        //     - RiseDurationTicks berikutnya: ketarik (ease-out) ke titik
        //       konvergensi: tengah horizontal altar, 5 block (80px) DI ATAS
        //       sisi atas altar.
        //   Wave PALING TERAKHIR muncul (w = WaveCount - 1) bakal selesai
        //   fase-nya di tick ConvergeTick -- itu titik semua soul udah
        //   ngumpul penuh di titik konvergensi.
        //
        //   Tepat di tick ConvergeTick:
        //       Bunyi "Zombie53", NPC yang direvive di-spawn PAS di titik
        //       konvergensi itu (masih transparan penuh, lihat SoulReviveGlobalNPC).
        //   ConvergeTick -> ConvergeTick + FadeDurationTicks:
        //       Soul-soul TETEP nangkring diem di titik konvergensi (ga ada
        //       yang ilang duluan) barengan NPC-nya yang perlahan makin
        //       keliatan (fade alpha, lihat SoulReviveGlobalNPC.PreAI).
        //   Tepat di tick ConvergeTick + FadeDurationTicks (momen NPC-nya
        //   FULL muncul -- barengan particle "Blink" putih + sound
        //   materialize ala Magic Mirror dari SoulReviveGlobalNPC):
        //       SEMUA 10 soul (& projectile visualnya) poof & ke-Kill
        //       SERENTAK BARENGAN di tick yang sama persis -- bukan nyicil
        //       lagi kayak sebelumnya.
        //   >= ConvergeTick + FadeDurationTicks (+ dikit buffer): ritual dianggap
        //       kelar, semua state di-reset.
        //
        // AnyRitualActive itu flag GLOBAL (bukan per-altar) -- sengaja dibikin
        // gitu biar player CUMA bisa ngerevive 1 NPC dalam satu waktu (di altar
        // manapun), jadi ga numpuk 2 animasi ritual bareng-bareng yang bisa
        // bikin bingung/berantakan secara visual. IsRitualActive (instance,
        // per-altar) dipakai buat nentuin altar MANA yang lagi jalanin ritual-nya.
        // =====================================================================

        private const int RitualSoulCount = 10;
        private const int WaveCount = RitualSoulCount / 2; // 5 wave, isi 2 soul (kanan+kiri) per wave
        private const int WaveIntervalTicks = 24; // 0,4 detik * 60 tick/detik -- jeda antar WAVE (bukan antar soul individual lagi)
        private const int LateralPhaseTicks = 60;   // 1 detik PENUH (60 tick/detik) -- waktu menyebar kanan/kiri (per-soul, dihitung dari saat dia muncul)
        private const int RiseDurationTicks = 35;   // waktu ketarik dari titik lateral ke titik konvergensi

        // Tick di mana WAVE TERAKHIR selesai konvergen -> saat itu jugalah
        // NPC-nya beneran di-spawn. Dihitung otomatis dari jeda spawn wave
        // terakhir + durasi fase lateral & rise-nya sendiri.
        private static int ConvergeTick =>
            (WaveCount - 1) * WaveIntervalTicks + LateralPhaseTicks + RiseDurationTicks;

        public bool IsRitualActive { get; private set; }
        public static bool AnyRitualActive { get; private set; }

        private int _ritualTimer;
        private int _ritualNpcType;
        private Player _ritualPlayer;
        private Vector2 _ritualTarget;
        private readonly List<RitualSoulParticle> _ritualSouls = new List<RitualSoulParticle>();

        // Slot buat sound "Item9" yang diputer TERUS-MENERUS selama NPC-nya
        // lagi keluar/fade-in (dari SpawnRitualNPC() sampe dia FULL muncul).
        // CATATAN: SoundID.Item9 itu sound pendek/one-shot (bukan looped sound
        // style), jadi "loop"-nya di-emulasi dengan cara REPLAY ULANG tiap
        // kali instance sebelumnya udah abis kedengeran -- lihat pengecekan
        // SoundEngine.TryGetActiveSound di Update() di bawah.
        //
        // TIPE SlotId eksplisit (bukan "dynamic" lagi): di build tModLoader
        // sekarang SoundEngine.PlaySound() balikin ReLogic.Audio.SlotId dan
        // SoundEngine.TryGetActiveSound(SlotId, out ActiveSound?) makannya
        // param kedua HARUS persis "out Terraria.Audio.ActiveSound?" --
        // compiler ga bisa infer itu dari "out dynamic" (CS1503), jadi
        // namespace-nya di-import langsung (ReLogic.Audio + Terraria.Audio)
        // dan tipe-nya diketik eksplisit. _reviveLoopSoundActive tetep
        // dipertahankan sebagai penanda "udah pernah di-assign belum",
        // soalnya SlotId itu struct (ga bisa null kayak dynamic dulu).
        private SlotId _reviveLoopSound;
        private bool _reviveLoopSoundActive;

        private struct RitualSoulParticle
        {
            public Vector2 SpawnOrigin;      // titik persis di altar tempat soul ini "lahir"
            public int SpawnDelay;           // tick (relatif ke _ritualTimer) sebelum soul ini mulai muncul/bergerak
            public Vector2 Position;
            public Vector2 LateralVelocity;
            public int Age;                  // umur LOKAL soul ini (dihitung sejak dia muncul, bukan sejak ritual mulai)

            // Index (whoAmI) Projectile SoulVisualProjectile yang jadi representasi
            // visual soul ini di dunia -- (-1) berarti belum di-spawn (soul-nya
            // belum "lahir" lagi nunggu SpawnDelay). Di-spawn SEKALI aja pas soul
            // ini pertama kali aktif (lihat TickRitualSouls), abis itu tiap tick
            // cuma di-update TargetPosition-nya doang lewat index ini.
            public int ProjectileWhoAmI;
        }

        // Dipanggil dari SoulCollectorUIState.TryRevive() (sisi otoritatif aja).
        // Ga langsung spawn NPC -- cuma mulai animasi ritualnya. NPC-nya baru
        // beneran ke-spawn nanti di SpawnRitualNPC() pas fase konvergensi kelar.
        public void BeginReviveRitual(int npcType, Player player)
        {
            if (AnyRitualActive)
                return;

            IsRitualActive = true;
            AnyRitualActive = true;

            _ritualTimer = 0;
            _ritualNpcType = npcType;
            _ritualPlayer = player;
            _ritualSouls.Clear();
            _reviveLoopSoundActive = false;

            // Titik asal soul: PERSIS di altar-nya sendiri (tengah horizontal,
            // di permukaan atas altar) -- bukan dari SoulEye yang ngambang
            // tinggi di atas, biar keliatan soul-nya beneran "keluar dari altar".
            // X: 33f (bukan 38f) -- digeser 5px LAGI ke kiri dari sebelumnya.
            Vector2 spawnOrigin = new Vector2(Position.X * 16f + 33f, Position.Y * 16f);

            // Titik konvergensi: sama offset X-nya kayak spawnOrigin (biar
            // soul naik lurus ke atas titik asalnya, ga geser), 5 block (80px)
            // di atas SISI ATAS altar.
            _ritualTarget = new Vector2(Position.X * 16f + 33f, Position.Y * 16f - 80f);

            for (int n = 0; n < RitualSoulCount; n++)
            {
                // 2 soul per wave (index genap = kanan, ganjil = kiri di wave
                // yang sama) -- SpawnDelay dihitung dari WAVE-nya (n / 2), jadi
                // pasangan kanan-kiri dalam 1 wave punya SpawnDelay SAMA PERSIS
                // -> muncul BARENGAN, bukan gantian kayak sebelumnya.
                int waveIndex = n / 2;
                bool goRight = n % 2 == 0;

                // Kecepatan lateral -- di-scale biar jarak tempuh MAX (speed *
                // LateralPhaseTicks) jadi sekitar 5.5-7.5 block dari altar
                // (lebih lebar/nyebar dari sebelumnya yang cuma ~3-4 block),
                // sambil tetep make durasi LateralPhaseTicks yang sekarang full
                // 1 detik -- jadi soul-nya nyebar lebih pelan & anggun, bukan
                // ngebut kabur.
                float speed = Main.rand.NextFloat(1.5f, 2.0f);
                Vector2 lateralVel = new Vector2(goRight ? speed : -speed, Main.rand.NextFloat(-0.15f, 0.15f));

                var soul = new RitualSoulParticle
                {
                    SpawnOrigin = spawnOrigin,
                    SpawnDelay = waveIndex * WaveIntervalTicks, // seluruh wave ke-n "lahir" bareng di tick ini
                    Position = spawnOrigin,
                    LateralVelocity = lateralVel,
                    Age = 0,
                    ProjectileWhoAmI = -1 // belum di-spawn -- di-spawn nanti pas beneran "lahir"
                };

                _ritualSouls.Add(soul);
            }
        }

        // CATATAN API: ModTileEntity.Update() ini otomatis kepanggil TIAP TICK
        // buat semua TileEntity yang lagi ke-place di dunia (pola standar
        // tModLoader, sama kayak Hook_AfterPlacement). Kalau nama/signature-nya
        // beda di versi tModLoader kamu, cek ExampleMod buat pola yang sesuai.
        public override void Update()
        {
            if (!IsRitualActive)
                return;

            // =====================================================================
            // MULTIPLAYER: dulu method ini SEPENUHNYA di-skip di client (return
            // langsung di sini), jadi animasi ritual (soul-soul terbang) cuma
            // keliatan di host/server -- client lain ga liat apa-apa sama sekali.
            //
            // Sekarang TICKING VISUAL-nya (timer, gerakan soul, sound, poof, dst)
            // dijalanin di SEMUA peer (server MAUPUN tiap client) -- masing2 peer
            // punya instance TileEntity LOKALnya sendiri yang mulai "IsRitualActive"
            // bareng-bareng lewat broadcast SoulNetworking.RitualStartBroadcast
            // (lihat HandleReviveRequest / HandleRitualStartBroadcast di
            // SoulNetworking.cs), lalu masing2 manggil BeginReviveRitual() versi
            // lokalnya sendiri-sendiri. Ini AMAN biarpun tiap peer generate
            // Main.rand-nya sendiri buat LateralVelocity tiap soul (jadi arah
            // nyebarnya bisa beda dikit antar layar tiap orang) -- karena animasi
            // ini MURNI KOSMETIK, ga ada dampak gameplay sama sekali. SoulCount
            // udah disinkronin duluan (TileEntitySharing yang udah ada), dan
            // NPC-nya beneran cuma di-spawn SATU KALI di sisi server (nyambung ke
            // netcode NPC bawaan buat sampe ke semua client). SoulVisualProjectile
            // sendiri emang dari awal udah didesain buat representasi visual
            // LOKAL per-client (owner = Main.myPlayer, hide=true, netImportant =
            // false -- lihat catatan di SoulVisualProjectile.cs), jadi motor
            // animasinya udah support pola ini tanpa perlu diubah.
            //
            // SATU-SATUNYA bagian yang WAJIB tetep cuma jalan di sisi otoritatif
            // adalah SpawnRitualNPC() -- itu beneran nyiptain NPC baru (state
            // gameplay asli). Kalau itu ikut kepanggil di tiap client juga bisa
            // dobel-spawn / desync total. Makanya guard netMode-nya digeser ke
            // situ doang, BUKAN lagi di paling atas method ini.
            // =====================================================================

            _ritualTimer++;
            TickRitualSouls();

            int convergeTick = ConvergeTick;
            int fadeEndTick = convergeTick + SoulReviveGlobalNPC.FadeDurationTicks;

            if (_ritualTimer == convergeTick)
            {
                SoundEngine.PlaySound(SoundID.Zombie53, _ritualTarget);

                // Cuma sisi otoritatif yang boleh beneran nyiptain NPC-nya --
                // client cukup denger bunyinya & liat soul-nya ngumpul; NPC
                // aslinya bakal "muncul" sendiri lewat netcode NPC bawaan begitu
                // server nge-spawn-nya di sini.
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    SpawnRitualNPC();
            }

            // NPC-nya lagi keluar/fade-in (dari convergeTick sampe fadeEndTick)
            // -> Item9 "diputer terus" dengan cara di-replay ulang tiap kali
            // instance sebelumnya udah selesai kedengeran (Item9 pendek, jadi
            // efeknya kedengeran kayak loop tanpa perlu bikin loop-sound-style
            // custom baru). CATATAN: _reviveLoopSoundActive dicek DULU sebelum
            // manggil TryGetActiveSound -- soalnya _reviveLoopSound belum
            // pernah di-assign sama sekali di tick pertama (default dynamic
            // null), jadi TryGetActiveSound(null, ...) bisa meledak kalau
            // dipaksa dipanggil.
            if (_ritualTimer >= convergeTick && _ritualTimer < fadeEndTick)
            {
                bool needsReplay = true;

                if (_reviveLoopSoundActive)
                {
                    // "ActiveSound" cukup (bukan "ActiveSound?" persis) --
                    // "?" di signature TryGetActiveSound cuma anotasi
                    // nullable-reference-type (C# 8), bukan Nullable<T>
                    // struct (ActiveSound-nya sendiri class), jadi tipe IL
                    // yang sebenarnya tetep sama persis dan match langsung.
                    ActiveSound activeLoopSound;
                    bool found = SoundEngine.TryGetActiveSound(_reviveLoopSound, out activeLoopSound);
                    needsReplay = !found || activeLoopSound == null || !activeLoopSound.IsPlaying;
                }

                if (needsReplay)
                {
                    _reviveLoopSound = SoundEngine.PlaySound(SoundID.Item9, _ritualTarget);
                    _reviveLoopSoundActive = true;
                }
            }

            if (_ritualTimer == fadeEndTick)
            {
                // Loop Item9-nya berhenti PAS di momen NPC full muncul (blink) --
                // sinyal yang sama dipake buat nentuin ritual "selesai".
                StopReviveLoopSound();

                // Momen ini PERSIS pas SoulReviveGlobalNPC.PreAI() nge-trigger
                // particle "Blink" putih + sound materialize ala Magic Mirror
                // (NPC-nya udah FULL muncul/ga tint abu-abu lagi) -- jadi SEMUA
                // 20 soul & projectile visualnya poof & ke-Kill SERENTAK di
                // tick yang sama persis, bukan nyicil satu-satu lagi kayak
                // sebelumnya.
                foreach (RitualSoulParticle p in _ritualSouls)
                {
                    SpawnSoulPoof(p.Position);
                    KillRitualProjectile(p.ProjectileWhoAmI);
                }

                _ritualSouls.Clear();
            }

            // Total durasi = fase lateral + fase naik + fase fade NPC (samain
            // sama SoulReviveGlobalNPC.FadeDurationTicks) + fase burst
            // ShineFlare/ChromaticBurst (SoulReviveGlobalNPC.BurstDurationTicks
            // -- NPC-nya masih anchor/diem nunggu ini kelar sebelum beneran
            // dilempar, lihat SoulReviveGlobalNPC.PreAI) + sedikit buffer.
            // Tanpa nyamain buffer ini, AnyRitualActive bisa balik false
            // duluan padahal NPC-nya masih berdiri diem nunggu dilempar --
            // bikin player bisa mulai ritual revive BARU numpuk di atas
            // animasi yang lama belum beneran kelar.
            if (_ritualTimer >= fadeEndTick + SoulReviveGlobalNPC.BurstDurationTicks + 5)
                EndRitual();
        }

        private void TickRitualSouls()
        {
            int visualProjType = ModContent.ProjectileType<SoulVisualProjectile>();

            for (int idx = 0; idx < _ritualSouls.Count; idx++)
            {
                RitualSoulParticle p = _ritualSouls[idx];

                // Umur LOKAL soul ini, dihitung dari saat dia sendiri "lahir"
                // (bukan dari saat ritual mulai) -- ini yang bikin soul-soul
                // muncul & bergerak satu-persatu/bertahap, ga langsung 20
                // sekaligus.
                int localAge = _ritualTimer - p.SpawnDelay;

                if (localAge < 0)
                {
                    // Belum waktunya soul ini muncul -- diem aja di titik asal,
                    // projectile-nya juga belum di-spawn sama sekali.
                    continue;
                }

                p.Age = localAge;

                Vector2 previousPosition = p.Position;

                // Titik ujung fase 1 (dipakai di kedua fase, jadi dihitung sekali aja).
                Vector2 lateralEndPosition = p.SpawnOrigin + p.LateralVelocity * LateralPhaseTicks;

                if (localAge <= LateralPhaseTicks)
                {
                    // Fase 1: menyebar ke kanan/kiri dari altar-nya. Dipakein
                    // smoothstep (ease-in-OUT), bukan gerak linear konstan kayak
                    // sebelumnya -- soul-nya "berangkat" pelan, ngebut di
                    // tengah jalan, terus MELAMBAT lagi (velocity ~0) pas nyampe
                    // titik ujung nyebarnya.
                    float t1 = localAge / (float)LateralPhaseTicks;
                    float smooth1 = t1 * t1 * (3f - 2f * t1);
                    p.Position = Vector2.Lerp(p.SpawnOrigin, lateralEndPosition, smooth1);
                }
                else
                {
                    // Fase 2: ketarik ke titik konvergensi. Sama-sama smoothstep
                    // (bukan ease-out doang) -- MULAI dari velocity ~0 (nyambung
                    // mulus sama akhir fase 1, ga ada "sentakan"/kink di titik
                    // transisinya) dan MELAMBAT lagi pas mepet titik konvergensi
                    // (soft landing, ga kayak "nabrak").
                    float t2 = MathHelper.Clamp((localAge - LateralPhaseTicks) / (float)RiseDurationTicks, 0f, 1f);
                    float smooth2 = t2 * t2 * (3f - 2f * t2);
                    p.Position = Vector2.Lerp(lateralEndPosition, _ritualTarget, smooth2);
                }

                // Baru "lahir" tick ini (ProjectileWhoAmI masih -1) -> spawn
                // SoulVisualProjectile-nya SEKALI di titik asal, biar ga
                // "teleport" dari (0,0) ke posisi choreographed-nya duluan.
                if (p.ProjectileWhoAmI == -1)
                {
                    p.ProjectileWhoAmI = Projectile.NewProjectile(
                        new EntitySource_TileUpdate(Position.X, Position.Y),
                        p.SpawnOrigin, Vector2.Zero, visualProjType, 0, 0f, Main.myPlayer);

                    // Tiap kali 1 soul "lahir"/muncul (2 soul per wave, tiap
                    // WaveIntervalTicks) -> bunyi kecil biar kerasa "berdenyut"
                    // seiring wave demi wave keluar dari altar.
                    SoundEngine.PlaySound(SoundID.NPCHit52, p.SpawnOrigin);
                }

                // Maksa posisi projectile-nya PERSIS ngikutin jalur
                // choreographed (smoothstep) di atas -- BUKAN AI wander bawaan
                // LostSoulFriendly. AI vanilla-nya (dari AIType) tetep jalan
                // tiap tick (ngurus animasi frame dkk), tapi Center/velocity-nya
                // ditimpa ulang manual di sini abis itu.
                if (p.ProjectileWhoAmI < Main.maxProjectiles)
                {
                    Projectile proj = Main.projectile[p.ProjectileWhoAmI];
                    if (proj.active && proj.type == visualProjType && proj.ModProjectile is SoulVisualProjectile visual)
                    {
                        visual.ForcePosition(p.Position, p.Position - previousPosition);
                        visual.RefreshLifetime();
                    }
                }

                _ritualSouls[idx] = p;

                // Ekor/after-image di belakang soul yang lagi jalan -- lihat
                // SpawnTrailDust buat penjelasan kenapa dust (bukan sprite item).
                SpawnTrailDust(p.Position);
            }
        }

        // Bunuh projectile visual punya 1 soul ritual (dipanggil pas soul-nya
        // "keserap"/poof di akhir ritual, atau pas ritual di-cancel/reset
        // paksa lewat EndRitual) -- biar ga ada projectile "hantu" yang
        // nyangkut nampang diem di dunia abis soul-nya harusnya udah ilang.
        private static void KillRitualProjectile(int projWhoAmI)
        {
            if (projWhoAmI == -1 || projWhoAmI >= Main.maxProjectiles)
                return;

            Projectile proj = Main.projectile[projWhoAmI];
            if (proj.active && proj.type == ModContent.ProjectileType<SoulVisualProjectile>())
                proj.Kill();
        }

        // NPC-nya beneran di-spawn di sini, PAS di titik konvergensi soul-nya,
        // masih transparan penuh (alpha 255) -- fade-in-nya diurus
        // SoulReviveGlobalNPC.BeginRevive().
        private void SpawnRitualNPC()
        {
            Player player = _ritualPlayer ?? Main.LocalPlayer;

            TownNPCRespawnLockGlobalNPC.AuthorizeRevive(_ritualNpcType);

            int npcIndex = NPC.NewNPC(player.GetSource_Misc("SoulCollectorRevive"),
                (int)_ritualTarget.X, (int)_ritualTarget.Y, _ritualNpcType);

            NPC spawned = Main.npc[npcIndex];
            spawned.GetGlobalNPC<SoulReviveGlobalNPC>().BeginRevive(spawned, _ritualTarget);
        }

        // Poof kecil & senyap pas 1 soul "keserap"/ilang -- cuma dust, ga ada
        // sound (biar ga numpuk 20x bunyi berturut-turut selama fase fade).
        private static void SpawnSoulPoof(Vector2 pos)
        {
            for (int d = 0; d < 4; d++)
            {
                Dust dust = Dust.NewDustPerfect(pos, DustID.WhiteTorch, Vector2.Zero, 100, default, 0.9f);
                dust.noGravity = true;
                dust.velocity = Main.rand.NextVector2Circular(1.5f, 1.5f);
                dust.fadeIn = 0.2f;
            }
        }

        // Warna tint buat ekor/after-image -- disamain sama tint biru yang
        // dipake buat gambar soul-nya sendiri di DrawRitualSouls (dan juga
        // dipakai versi Souls.cs punya buat trail item Souls yang di dunia).
        private static readonly Color TrailTint = new Color(150, 195, 255);

        // Ekor/after-image di belakang soul yang lagi terbang. SENGAJA pake
        // dust WhiteTorch yang di-tint biru (bukan gambar ulang sprite item
        // Souls kecil-kecil berkali-kali) -- dust jenis "torch" ini otomatis
        // mengecil & pudar sendiri seiring umurnya, jadi hasilnya persis kayak
        // ekor komet yang smooth & makin mengecil ke ujung, tanpa perlu bikin
        // texture/asset baru atau custom vertex trail yang lebih ribet.
        // Dipanggil TIAP TICK per-soul (bukan di Draw) biar jejaknya rapat &
        // rapi ngikutin jalur gerakannya.
        private static void SpawnTrailDust(Vector2 position)
        {
            Dust dust = Dust.NewDustPerfect(position, DustID.WhiteTorch, Vector2.Zero, 150, TrailTint, Main.rand.NextFloat(0.55f, 0.8f));
            dust.noGravity = true;
            dust.fadeIn = 0f;
        }

        // Helper terpusat buat matiin loop Item9 -- dipanggil pas ritual
        // selesai normal (fadeEndTick) maupun pas EndRitual() jaring pengaman.
        // Ngecek _reviveLoopSoundActive dulu (bukan langsung manggil
        // TryGetActiveSound) biar ga meledak kalau loop-nya emang belum
        // pernah nyala sama sekali.
        private void StopReviveLoopSound()
        {
            if (!_reviveLoopSoundActive)
                return;

            ActiveSound activeLoopSound;
            bool found = SoundEngine.TryGetActiveSound(_reviveLoopSound, out activeLoopSound);
            if (found && activeLoopSound != null)
                activeLoopSound.Stop();

            _reviveLoopSoundActive = false;
        }

        private void EndRitual()
        {
            // Jaring pengaman: bunuh semua projectile visual yang masih
            // nyangkut (harusnya udah abis semua lewat poof di atas, tapi
            // kalau ritual ke-reset paksa di tengah jalan -- misal world
            // unload -- ini biar ga ninggalin projectile "hantu").
            foreach (RitualSoulParticle p in _ritualSouls)
                KillRitualProjectile(p.ProjectileWhoAmI);

            // Jaring pengaman: kalau ritual ke-reset paksa (misal world unload)
            // sebelum sempet nyampe fadeEndTick, loop Item9-nya ikut dimatiin
            // di sini juga biar ga nyangkut nyala terus.
            StopReviveLoopSound();

            IsRitualActive = false;
            AnyRitualActive = false;

            _ritualTimer = 0;
            _ritualPlayer = null;
            _ritualSouls.Clear();
        }

        // =========================================================
        // DrawRitualSouls() DIHAPUS -- soul-soul ritual dulu digambar manual
        // di sini pakai texture item Souls (dipanggil dari
        // SoulCollectorAltar.PostDraw()). Sekarang soul-soul ritual adalah
        // SoulVisualProjectile beneran (spawn di TickRitualSouls() di atas),
        // jadi gambarnya otomatis lewat jalur draw Projectile bawaan game --
        // ga perlu hook draw manual lagi sama sekali. Lihat SoulVisualProjectile.cs.
        // =========================================================

        // Syarat tombol Convert nyala: Soul cukup (>= ConvertSoulCost) DAN slot
        // token masih kosong ATAU udah keisi Soul Token tapi belom full stack.
        public bool CanConvert =>
            SoulCount >= ConvertSoulCost
            && (TokenSlotItem.IsAir
                || (TokenSlotItem.type == ModContent.ItemType<SoulToken>() && TokenSlotItem.stack < TokenSlotItem.maxStack));

        // Dipanggil dari ConvertButton. Cuma boleh dieksekusi di sisi otoritatif
        // (sama kayak AddSoul/RemoveSoul & TryRevive di SoulCollectorUIState) biar
        // ga dobel jalan di multiplayer client. Sinkronisasi ke client lain lewat
        // packet custom belum diimplementasikan di sini (sama kayak fitur lain).
        public bool TryConvertSouls()
        {
            if (!CanConvert)
                return false;

            RemoveSoul(ConvertSoulCost); // ini udah manggil Sync() sendiri, tapi TokenSlotItem
                                          // baru diubah SETELAH ini -- jadi Sync() dipanggil LAGI
                                          // di akhir method biar TokenSlotItem yang baru ikut ke-broadcast.

            int tokenType = ModContent.ItemType<SoulToken>();
            if (TokenSlotItem.IsAir)
            {
                TokenSlotItem = new Item();
                TokenSlotItem.SetDefaults(tokenType);
                TokenSlotItem.stack = 1;
            }
            else
            {
                TokenSlotItem.stack++;
            }

            Sync();
            return true;
        }

        public override void SaveData(TagCompound tag)
        {
            tag["soulCount"] = SoulCount;

            if (!TokenSlotItem.IsAir)
                tag["tokenSlot"] = ItemIO.Save(TokenSlotItem);
        }

        public override void LoadData(TagCompound tag)
        {
            SoulCount = tag.GetInt("soulCount");

            TokenSlotItem = tag.ContainsKey("tokenSlot")
                ? ItemIO.Load(tag.GetCompound("tokenSlot"))
                : new Item();
        }

        // =====================================================================
        // MULTIPLAYER SYNC
        //
        // SaveData/LoadData di atas CUMA buat persist ke file dunia (.wld) --
        // itu ga ada hubungannya sama sekali sama sinkronisasi REAL-TIME antar
        // client di multiplayer. Sebelumnya SoulCount & TokenSlotItem berubah
        // (AddSoul/RemoveSoul/TryConvertSouls) cuma di sisi otoritatif (server
        // atau singleplayer) TANPA pernah dikirim ke client lain -- jadi client
        // bakal liat angka Soul yang basi/nyangkut di nilai awal terus.
        //
        // NetSend/NetReceive ini hook BAWAAN ModTileEntity (bukan custom
        // ModPacket -- jadi ga butuh apa-apa dari Mod.cs) yang otomatis
        // dipanggil tModLoader tiap kali ada yang manggil
        // NetMessage.SendData(MessageID.TileEntitySharing, ...) buat
        // TileEntity ini. NetSend jalan di sisi PENGIRIM (server), NetReceive
        // jalan di sisi PENERIMA (client) buat nulis ulang field lokalnya.
        //
        // Sync() di bawah ini yang manggil NetMessage.SendData-nya -- dipanggil
        // manual tiap abis AddSoul/RemoveSoul/TryConvertSouls beneran ngubah
        // state (liat pemanggilannya di method masing-masing).
        // =====================================================================

        // CATATAN API: kalau ItemIO.Send/ItemIO.Receive ga ada / signature-nya
        // beda di versi tModLoader kamu (compile error), ganti TokenSlotItem
        // jadi dikirim manual minimal (type + stack) kayak gini, cukup buat
        // kasus item vanilla/simple kayak SoulToken (ga ada prefix/data mod
        // tambahan yang perlu disave):
        //   writer.Write((short)TokenSlotItem.type);
        //   writer.Write((short)TokenSlotItem.stack);
        //   ...
        //   short type = reader.ReadInt16();
        //   short stack = reader.ReadInt16();
        //   TokenSlotItem = type > 0 ? new Item(type) { stack = stack } : new Item();
        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(SoulCount);
            ItemIO.Send(TokenSlotItem, writer, writeStack: true);
        }

        public override void NetReceive(BinaryReader reader)
        {
            SoulCount = reader.ReadInt32();
            TokenSlotItem = ItemIO.Receive(reader, readStack: true);
        }

        // Broadcast state terbaru TileEntity ini ke semua client yang lagi
        // konek. CUMA dipanggil dari sisi SERVER (dedicated server ATAU host
        // di singleplayer/LAN) -- singleplayer murni (NetmodeID.SinglePlayer)
        // ga punya client lain buat disinkronin jadi ga perlu ngirim apa-apa.
        //
        // PUBLIC (bukan private) biar bisa dipanggil juga dari luar
        // (SoulCollectorUIState.SoulTokenSlotElement) abis TokenSlotItem
        // diubah manual lewat interaksi GUI slot token -- lihat catatan di
        // SoulTokenSlotElement.LeftClick.
        public void Sync()
        {
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.TileEntitySharing, number: ID, number2: Position.X, number3: Position.Y);
        }

        public override bool IsTileValidForEntity(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            return tile.HasTile && tile.TileType == ModContent.TileType<SoulCollectorAltar>();
        }

        // Pola standar tModLoader buat naro TileEntity pas tile-nya di-place.
        // Kalau ada error compile di sini karena API berubah antar versi tModLoader,
        // bandingin sama ExampleMod/ExampleTileEntity.cs versi kamu.
        public override int Hook_AfterPlacement(int i, int j, int type, int style, int direction, int alternate)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                NetMessage.SendTileSquare(Main.myPlayer, i, j,
                    TileObjectData.GetTileData(type, style).Width,
                    TileObjectData.GetTileData(type, style).Height);
                NetMessage.SendData(MessageID.TileEntityPlacement, number: i, number2: j, number3: type);
                return -1;
            }

            return Place(i, j);
        }

        public void AddSoul(int amount = 1)
        {
            SoulCount = (int)MathHelper.Clamp(SoulCount + amount, 0, MaxSoulCapacity);
            Sync();
        }

        // Dipakai buat bayar biaya Revive Town NPC (500 Soul per revive).
        public void RemoveSoul(int amount)
        {
            SoulCount = (int)MathHelper.Clamp(SoulCount - amount, 0, MaxSoulCapacity);
            Sync();
        }

        // Titik tengah dunia (world position) dari sprite SoulEyeBlue milik altar ini.
        // Dipakai Souls.cs buat "menarik" item soul ke arah sini.
        public Vector2 GetEyeWorldCenter()
        {
            Vector2 tileTopLeftPixel = new Vector2(Position.X * 16f, Position.Y * 16f);
            return tileTopLeftPixel + SoulCollectorAltar.EyeOffset
                + new Vector2(SoulCollectorAltar.EyeFrameWidth / 2f, SoulCollectorAltar.EyeFrameHeight / 2f);
        }
    }
}