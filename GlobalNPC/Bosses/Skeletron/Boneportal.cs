using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Skeletron
{
    // Portal tempat BigBoneSpike muncul.
    //
    // FIX PENTING: dulu di-assume sprite BonePortal.png itu grid rapi 4 kolom
    // x 2 baris (8 frame sama besar), terus di-slice pakai pembagian rata
    // (tex.Width/4, tex.Height/2). TERNYATA itu SALAH — sprite sheet aslinya
    // cuma punya 7 frame TIDAK SERAGAM (baris atas 4 frame, baris bawah cuma
    // 3 frame, dan tiap frame beda ukuran karena portal-nya emang tumbuh
    // membesar). Akibatnya garis potong grid lama motong LANGSUNG di
    // tengah-tengah 2 frame terakhir (baris bawah), jadi kelihatan
    // "kepotong-kepotong" pas di-render in-game.
    //
    // Sekarang pakai FrameRects: array Rectangle eksplisit hasil ukur manual
    // dari file PNG asli (7 frame, indeks 0-6):
    //   Frame 0-4 = growth animation (portal membesar, bulat polos)
    //   Frame 5-6 = warning flash (alternate cepat, ada duri/spike — cue
    //               sebelum erupsi)
    public class BonePortal : ModNPC
    {
        // Koordinat diukur langsung dari BonePortal.png (344x192), sudah
        // dikasih sedikit padding biar gak kepotong tepiannya.
        static readonly Rectangle[] FrameRects = new Rectangle[]
        {
            new Rectangle(37, 37, 18, 19),    // frame 0 - growth kecil
            new Rectangle(112, 30, 33, 34),   // frame 1
            new Rectangle(183, 20, 56, 55),   // frame 2
            new Rectangle(257, 10, 75, 72),   // frame 3
            new Rectangle(18, 108, 77, 74),   // frame 4 - growth terakhir
            new Rectangle(124, 100, 97, 89),  // frame 5 - warning (spike)
            new Rectangle(239, 95, 98, 97),   // frame 6 - warning (spike)
        };
        const int FrameCount = 7;
        const int GrowthFrameCount = 5; // frame 0-4
        // FIX: dicepetin (70 -> 18, 20 -> 6) — request user: begitu portal
        // muncul, tulangnya harus LANGSUNG nongol nyusul, bukan nunggu
        // portal tumbuh pelan-pelan dulu kayak sebelumnya (dulu total 1.5
        // detik sebelum erupsi, sekarang ~0.4 detik). Animasi growth/warning
        // (frame 0-6) TETAP jalan penuh, cuma durasinya yang dipadetin —
        // portal masih keliatan "tumbuh" & "warning flash", cuma cepet.
        public const int GrowthDuration = 18;   // tick, frame 0-4
        public const int WarningDuration = 6;   // tick, alternate frame 5/6
        public const int EruptionTick = GrowthDuration + WarningDuration; // = 24 (~0.4 detik)

        // buffer nunggu BigBoneSpike selesai proses emerge->hold->retract
        // (12+40+20 = 72 tick, EmergeTime 0.2 detik) sebelum portal ini
        // ikutan despawn
        const int PostEruptionLifetime = 100;

        // === PHASE 2: "dash portal" ===
        // Dipakai Head sendiri buat pattern PortalDash (dia yg loncat
        // masuk-keluar portal berantai) dan ShardShatter (dia reappear
        // lewat 1 portal sesudah "pecah"). Growth/warning-nya jauh lebih
        // cepat dari portal normal punya BigBoneSpike (kesan "ngedash",
        // bukan nunggu portal tumbuh pelan-pelan), dan portalnya gak
        // pernah munculin BigBoneSpike — Head sendiri yang keluar dari
        // situ (lihat Erupt() & AI() di bawah, dikontrol lewat ai[2]).
        //
        // ai[2] = 1f -> mode dash portal (suppress spawn BigBoneSpike,
        //               pakai timing cepat di bawah)
        // ai[3] = scale multiplier tambahan (0/unset dianggap 1x, biar
        //         portal biasa yg belum pernah nyetel ai[3] tetap normal)
        public const int DashGrowthDuration = 30;
        public const int DashWarningDuration = 10;
        public const int DashEruptionTick = DashGrowthDuration + DashWarningDuration; // = 40
        const int DashPostEruptionLifetime = 25; // portal dash gak perlu nangkring lama, Head udah pergi duluan

        bool IsDashPortal => NPC.ai[2] == 1f;
        // === REWORK: Bone Glove accessory ===
        // Numpang slot ai[2] yang sama kayak IsDashPortal (cuma beda angka),
        // soalnya NPC.ai cuma punya 4 slot (0-3) dan semuanya udah kepake:
        // ai[0]=timer, ai[1]=whoAmI target/owner, ai[2]=mode, ai[3]=scale.
        // ai[2] == 2f -> mode accessory: portal kecil, FRIENDLY (nyerang
        // NPC bukan player), tetep munculin BigBoneSpike (beda dari dash
        // portal yang justru suppress spike sama sekali).
        bool IsAccessoryPortal => NPC.ai[2] == 2f;

        // === PATTERN BARU: Portal Slam (4x menghentak) ===
        // Numpang slot ai[2] yang sama (== 3f) buat mode "slam fast":
        // growth+warning-nya diPADETIN LAGI dari portal normal (request
        // user: pas lagi menghentak, portal+BigBoneSpike-nya harus muncul
        // SANGAT cepat & gantian kiri-kanan kerasa rapid-fire, bukan
        // santai kayak PortalCast pattern 1 biasa) — TAPI beda dari dash
        // portal (ai[2]==1f), mode ini TETAP munculin BigBoneSpike pas
        // Erupt() (lihat guard IsDashPortal di Erupt(), IsSlamFastPortal
        // sengaja gak ikut kena guard itu).
        bool IsSlamFastPortal => NPC.ai[2] == 3f;
        public const int SlamGrowthDuration = 6;
        public const int SlamWarningDuration = 3;
        public const int SlamEruptionTick = SlamGrowthDuration + SlamWarningDuration; // = 9 (~0.15 detik)
        const int SlamPostEruptionLifetime = 40;

        int EffectiveGrowthDuration => IsDashPortal ? DashGrowthDuration : IsSlamFastPortal ? SlamGrowthDuration : GrowthDuration;
        int EffectiveEruptionTick => IsDashPortal ? DashEruptionTick : IsSlamFastPortal ? SlamEruptionTick : EruptionTick;
        int EffectivePostEruptionLifetime => IsDashPortal ? DashPostEruptionLifetime : IsSlamFastPortal ? SlamPostEruptionLifetime : PostEruptionLifetime;
        float ExtraScaleMul => NPC.ai[3] > 0f ? NPC.ai[3] : 1f;

        // === Ledakan serpihan (BoneShardParticle) pas Erupt() ===
        // Jumlah/kecepatannya sengaja lebih kecil dari ShardCount milik
        // ThrownBone (9) — di sini cuma "efek samping" erupsi portal, bukan
        // sumber damage utama kayak di ThrownBone yang emang didesain buat pecah.
        const int EruptionShardCount = 6;
        const float EruptionShardMinSpeed = 3f;
        const float EruptionShardMaxSpeed = 7f;

        // rentang tilt acak (derajat) biar tiap portal gak keliatan seragam/kaku
        const float MaxTiltDegrees = 25f;

        int currentFrame = 0;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Skeletron/BonePortal";

        public override void SetDefaults()
        {
            NPC.width = 40;
            NPC.height = 40;
            NPC.damage = 0;
            NPC.defense = 0;
            NPC.lifeMax = 5;
            NPC.dontTakeDamage = true; // portal sendiri gak bisa dihit/dibunuh player
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.chaseable = false;
        }

        public override void OnSpawn(IEntitySource source)
        {
            // FIX: portal sekarang bisa miring acak, bukan selalu lurus.
            // Origin gambarnya di tengah (lihat PreDraw), jadi rotasi di
            // sekitar titik pusat portal — aman gak perlu fix anchor kayak
            // di BigBoneSpike.
            NPC.rotation = MathHelper.ToRadians(Main.rand.NextFloat(-MaxTiltDegrees, MaxTiltDegrees));
        }

        // ai[0] = timer utama (auto-synced vanilla, gak perlu SendExtraAI manual)
        // ai[1] = whoAmI player target (buat referensi, opsional dipakai)
        public override void AI()
        {
            NPC.ai[0]++;
            float timer = NPC.ai[0];

            if (timer <= EffectiveGrowthDuration)
            {
                float progress = timer / EffectiveGrowthDuration;
                // FIX: cuma ada 5 frame growth (indeks 0-4), bukan 6 (0-5)
                // seperti asumsi lama — lihat catatan FrameRects di atas.
                currentFrame = (int)(progress * (GrowthFrameCount - 0.01f)); // 0-4
                NPC.scale = MathHelper.Lerp(0.3f, 1f, progress) * ExtraScaleMul;

                if (Main.rand.NextBool(3))
                    SpawnSuctionDust();
            }
            else if (timer <= EffectiveEruptionTick)
            {
                // FIX: warning flash sekarang alternate antara frame 5 dan 6
                // (dua frame spiky terakhir), bukan 6/7 — frame 7 gak pernah
                // ada di sprite sheet aslinya.
                currentFrame = ((int)timer / 4) % 2 == 0 ? 5 : 6;
                NPC.scale = 1f * ExtraScaleMul;

                if (Main.rand.NextBool(2))
                    SpawnSuctionDust();
            }

            if (timer == EffectiveEruptionTick)
            {
                Erupt();
            }

            if (timer > EffectiveEruptionTick + EffectivePostEruptionLifetime)
            {
                NPC.active = false;
            }
        }

        void SpawnSuctionDust()
        {
            Vector2 dustPos = NPC.Center + Main.rand.NextVector2Circular(NPC.width * 0.7f, NPC.width * 0.7f);
            Dust d = Dust.NewDustPerfect(dustPos, ModContent.DustType<VoidSparkDust>(), (NPC.Center - dustPos) * 0.06f);
            d.noGravity = true;
            d.scale = Main.rand.NextFloat(0.8f, 1.3f);
        }

        void Erupt()
        {
            SoundEngine.PlaySound(SoundID.Dig, NPC.Center); // TODO: ganti sound custom "bone crack"
            // TODO: screen shake kecil pas erupsi

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // Ledakan serpihan tulang pas portal-nya "muntahin" bone —
                // murni tambahan visual+damage kecil di titik erupsi, gak
                // terikat arah kemunculan BigBoneSpike (spike-nya sendiri
                // baru nentuin arah beberapa baris di bawah).
                //
                // FIX (accessory): BoneShardParticle SELALU hostile (nyerang
                // player), jadi buat mode accessory ini WAJIB di-skip — kalau
                // enggak, portal yang muncul dari Bone Glove malah nyakitin
                // player pemilik accessory-nya sendiri pas erupsi.
                if (!IsAccessoryPortal)
                    BoneShardParticle.SpawnBurst(NPC.GetSource_FromAI(), NPC.Center, EruptionShardCount, EruptionShardMinSpeed, EruptionShardMaxSpeed);

                // PHASE 2: portal dash gak pernah munculin BigBoneSpike —
                // Head sendiri yang "keluar" dari portal ini (lihat state
                // machine Head, PortalDashCast/ShatterCast). Serpihan tulang
                // di atas TETAP muncul di kedua mode (request: shard tetap
                // ada tiap Head keluar portal), cuma spike-nya yang di-skip.
                if (IsDashPortal)
                    return;

                // === REWORK: Bone Glove accessory (mode friendly, kecil) ===
                // Beda dari branch hostile di bawah (target = PLAYER buat
                // di-tusuk) — di sini spike-nya di-set FRIENDLY (nyerang
                // NPC, damage dikreditkan ke player pemilik accessory lewat
                // Projectile owner). anchorPos & targetPos SAMA-SAMA
                // NPC.Center (posisi PORTAL ini, bukan posisi NPC yang kena
                // hit — lihat GetRandomOffsetPosition di SpawnOnHit, portal
                // sekarang udah di-offset ke sisi atas/bawah/kiri/kanan
                // badan target), jadi spike-nya nusuk dari tanah PAS di
                // titik portal muncul (samping/bawah musuh), bukan nembus
                // dari dalam sprite musuhnya sendiri. Ukurannya ikut
                // ExtraScaleMul (ai[3]) yang udah di-set kecil dari SpawnOnHit.
                if (IsAccessoryPortal)
                {
                    int ownerIndex = (int)NPC.ai[1];
                    Player owner = (ownerIndex >= 0 && ownerIndex < Main.maxPlayers && Main.player[ownerIndex].active)
                        ? Main.player[ownerIndex]
                        : Main.player[Main.myPlayer];

                    var accDirection = (BigBoneSpike.EmergeDirection)Main.rand.Next(4);

                    int accIndex = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                        ModContent.ProjectileType<BigBoneSpike>(), 0, 3f, owner.whoAmI);

                    if (accIndex >= 0 && accIndex < Main.maxProjectiles
                        && Main.projectile[accIndex].active
                        && Main.projectile[accIndex].ModProjectile is BigBoneSpike accSpike)
                    {
                        accSpike.SetupEmerge(NPC.Center, accDirection, NPC.Center, ExtraScaleMul, friendly: true);
                    }

                    return;
                }

                // FIX ANCHOR (v2): titik acuannya sekarang NPC.Center, BUKAN
                // NPC.Bottom. Portal digambar CENTER-nya di NPC.Center (lihat
                // BonePortal.PreDraw: drawPos = NPC.Center - screenPos, origin
                // = tengah frame) — jadi NPC.Center itu titik tengah VISUAL
                // portal yang sebenarnya.
                //
                // Projectile.NewProjectile cuma butuh posisi awal placeholder
                // di sini — posisi+ukuran+rotasi FINAL-nya ditentuin sepenuhnya
                // oleh BigBoneSpike.SetupEmerge() di bawah, sesuai arah
                // kemunculan yang dipilih.
                int index = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero,
                    ModContent.ProjectileType<BigBoneSpike>(), 0, 3f, Main.myPlayer);
                // damage sengaja 0 di sini — BigBoneSpike.AI() yang nentuin damage
                // aktual (FullDamage) pas transisi ke state Holding. Kalau di-isi
                // angka di sini, bakal ke-overwrite dan bone damage dari awal muncul.

                if (index >= 0 && index < Main.maxProjectiles
                    && Main.projectile[index].active
                    && Main.projectile[index].ModProjectile is BigBoneSpike spike)
                {
                    int targetIndex = (int)NPC.ai[1];
                    Player target = (targetIndex >= 0 && targetIndex < Main.maxPlayers && Main.player[targetIndex].active)
                        ? Main.player[targetIndex]
                        : Main.player[Main.myPlayer];

                    // FIX: tulang sekarang gak melulu muncul ke ATAS — tiap
                    // portal random milih salah satu dari 4 arah kemunculan
                    // (Up/Down/Left/Right), biar serangannya lebih variatif
                    // dan gak bisa dihindarin cuma dengan diem persis di
                    // bawah portal. Arah Up tetap ngikutin posisi player
                    // (lihat SetupEmerge), arah lain tilt-nya random murni.
                    var direction = (BigBoneSpike.EmergeDirection)Main.rand.Next(4);
                    spike.SetupEmerge(NPC.Center, direction, target.Center);
                }
            }
        }

        // Jangan despawn otomatis walau player jauh — attack harus tetap konsisten
        public override bool CheckActive() => false;

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D tex = TextureAssets.Npc[NPC.type].Value;

            // FIX: pakai rectangle hasil ukur manual per-frame (FrameRects),
            // bukan hitung grid seragam — soalnya frame aslinya gak seragam
            // ukuran/posisinya (lihat catatan di atas class).
            // FIX CS0266: MathHelper.Clamp cuma punya overload float (return
            // float), jadi gak bisa langsung di-assign ke int. Clamp manual
            // di sini biar hasilnya tetap int.
            int frameIndex = currentFrame;
            if (frameIndex < 0) frameIndex = 0;
            else if (frameIndex > FrameCount - 1) frameIndex = FrameCount - 1;
            Rectangle sourceRect = FrameRects[frameIndex];

            // Origin = titik tengah LOKAL tiap frame (bukan tengah cell grid
            // seragam). Karena tiap frame beda ukuran (portal makin gede),
            // pakai tengah lokal ini otomatis bikin animasinya "tumbuh"
            // simetris dari titik spawn NPC, bukan geser-geser.
            Vector2 origin = new Vector2(sourceRect.Width / 2f, sourceRect.Height / 2f);
            Vector2 drawPos = NPC.Center - screenPos;

            spriteBatch.Draw(tex, drawPos, sourceRect, drawColor, NPC.rotation, origin,
                NPC.scale, SpriteEffects.None, 0f);

            return false;
        }

        // FIX: jarak minimum antar portal biar 15 portal gak numpuk/overlap
        // satu sama lain pas muncul di area yang sama.
        const float MinPortalSpacing = 130f;
        const int MaxSpawnAttempts = 12;

        // === Dipanggil dari state machine Head (SkeletronReworkGlobalNPC) ===
        // Server/single-player authoritative. predictionTime dalam detik.
        //
        // FIX: dulu SpawnWave() nge-loop spawn "count" portal SEKALIGUS dalam
        // 1 pemanggilan (semua di tick yang sama). Sekarang cuma spawn SATU
        // portal per panggilan — state machine di Head yang manggil method
        // ini berulang kali dengan jeda antar tick, biar portal muncul
        // bergantian/beruntun, bukan sekaligus barengan.
        public static void SpawnSingle(NPC headNpc, Player target, float predictionTime = 0.6f, float scatterRadius = 150f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 predictedPos = target.Center + target.velocity * predictionTime * 60f;

            // FIX: coba beberapa posisi acak, ambil yang jaraknya cukup jauh
            // dari portal lain yang masih aktif (MinPortalSpacing). Kalau
            // sampe MaxSpawnAttempts gagal semua (area udah padat portal),
            // tetep pakai percobaan terakhir — mending numpuk dikit drpd
            // portalnya gak jadi muncul sama sekali.
            Vector2 spawnPos = predictedPos;
            for (int attempt = 0; attempt < MaxSpawnAttempts; attempt++)
            {
                Vector2 offset = Main.rand.NextVector2CircularEdge(scatterRadius, scatterRadius)
                                  + Main.rand.NextVector2Circular(40f, 40f);
                Vector2 candidate = predictedPos + offset;
                spawnPos = candidate;

                if (IsFarEnoughFromOtherPortals(candidate))
                    break;
            }

            int npcIndex = NPC.NewNPC(headNpc.GetSource_FromAI(), (int)spawnPos.X, (int)spawnPos.Y,
                ModContent.NPCType<BonePortal>());

            if (npcIndex < Main.maxNPCs)
            {
                Main.npc[npcIndex].ai[1] = target.whoAmI;
                Main.npc[npcIndex].netUpdate = true;
            }
        }

        // === PATTERN BARU (phase 1): Portal Slam ===
        // Dipanggil dari State.PortalSlamActive buat munculin portal PERSIS
        // di posisi yang udah dihitung sendiri sama pemanggil (baris
        // kiri/kanan dari titik hentakan Head) — beda dari SpawnSingle yang
        // posisinya prediksi+scatter sendiri, dan beda dari SpawnDashPortal
        // yang selalu mode dash (suppress BigBoneSpike, khusus Head sendiri
        // yang keluar-masuk). Portal ini portal NORMAL (tetep munculin
        // BigBoneSpike pas erupsi, arah random per portal kayak biasa),
        // cuma posisinya EXACT & gak ada scatter/spacing check — spacing-
        // nya udah dijamin sendiri sama pemanggil lewat jarak antar index
        // di barisnya (lihat PortalSlamRowSpacing di state machine).
        // FIX BARU: parameter `fast` opsional — true = mode "slam fast"
        // (ai[2]=3f, growth+warning dipadetin lagi lewat
        // SlamGrowthDuration/SlamWarningDuration, TAPI tetep munculin
        // BigBoneSpike pas erupsi, beda dari dash portal). Dipakai
        // State.PortalSlamActive biar portal+spike-nya kerasa rapid-fire
        // pas lagi menghentak. Default false biar caller lama (kalau ada)
        // tetap dapet portal normal.
        public static void SpawnAtPosition(NPC headNpc, Vector2 position, Player target, bool fast = false)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int npcIndex = NPC.NewNPC(headNpc.GetSource_FromAI(), (int)position.X, (int)position.Y,
                ModContent.NPCType<BonePortal>());

            if (npcIndex < Main.maxNPCs)
            {
                Main.npc[npcIndex].ai[1] = target.whoAmI;
                if (fast)
                    Main.npc[npcIndex].ai[2] = 3f; // mode slam fast
                Main.npc[npcIndex].netUpdate = true;
            }
        }

        // === PHASE 2: dipanggil dari state machine Head sendiri ===
        // Beda dari SpawnSingle (dipakai pattern 1 phase 1, portal buat
        // munculin BigBoneSpike, posisi ditentuin sendiri di sekitar
        // prediksi player) — SpawnDashPortal posisinya DITENTUIN PERSIS
        // sama pemanggil (titik lompatan berikutnya di dash chain, atau
        // titik reappear sesudah ShardShatter), gak ada scatter/spacing
        // check, dan portalnya SELALU mode dash (ai[2]=1, timing cepat,
        // gak munculin spike) dengan scale diperbesar (ExtraScaleMul).
        public static void SpawnDashPortal(NPC headNpc, Vector2 position, Player target, float scaleMul = 1.6f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int npcIndex = NPC.NewNPC(headNpc.GetSource_FromAI(), (int)position.X, (int)position.Y,
                ModContent.NPCType<BonePortal>());

            if (npcIndex < Main.maxNPCs)
            {
                Main.npc[npcIndex].ai[1] = target.whoAmI;
                Main.npc[npcIndex].ai[2] = 1f; // dash mode: cepat + suppress BigBoneSpike
                Main.npc[npcIndex].ai[3] = scaleMul;
                Main.npc[npcIndex].netUpdate = true;
            }
        }

        // FIX (v2): sebelumnya offset dihitung dari TEPI hitbox target
        // (target.width/2 + jarak tetap) — buat NPC yang hitboxnya gede,
        // itu bikin portal (makanya spike-nya juga) muncul JAUH dari
        // sprite si musuh. Karena si musuh bisa gerak/geser dikit selama
        // portal masih growth+warning (~0.4 detik) sebelum spike-nya
        // erupt, portal yang kejauhan ini bikin spike sering ga kena sama
        // sekali (request user: biar konsisten kena, portal-nya harus
        // muncul PAS DI musuh / deket banget sama sprite-nya, bukan
        // sejauh 1 tile dari tepi hitbox).
        //
        // Sekarang offset-nya FLAT (gak lagi dihitung dari
        // target.width/height), jaraknya sengaja kecil biar portal selalu
        // nongol nempel di sisi sprite musuh apapun ukuran hitbox-nya.
        const float AccessoryOffsetDistance = 10f;

        // === REWORK: dipanggil dari BoneGloveModPlayer pas accessory Bone
        // Glove proc (on hit) ===
        // Beda dari SpawnSingle/SpawnDashPortal di atas — posisinya
        // ditentuin OTOMATIS di sini berdasarkan posisi target yang kena
        // hit (bukan prediksi posisi player, bukan juga posisi manual dari
        // caller), gak ada scatter/spacing check (numpuk dikit gak masalah,
        // proc-nya kan udah dibatasin chance kecil + cooldown di ModPlayer),
        // dan SELALU mode accessory (ai[2]=2 -> friendly, munculin spike,
        // lihat Erupt()).
        //
        // FIX: dulu portal spawn PERSIS di target.Center, jadi keliatan
        // "nempel disitu-situ aja" tiap kali proc (posisinya gak pernah
        // beda secara visual, ketutup di dalam sprite musuhnya). Sekarang
        // portal muncul di salah satu dari 4 sisi (atas/bawah/kiri/kanan)
        // musuh, dipilih random tiap proc lewat GetRandomOffsetPosition —
        // lihat catatan di method itu.
        public static void SpawnOnHit(IEntitySource source, NPC target, Player owner, float scaleMul = 0.55f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 spawnPos = GetRandomOffsetPosition(target);

            int npcIndex = NPC.NewNPC(source, (int)spawnPos.X, (int)spawnPos.Y,
                ModContent.NPCType<BonePortal>());

            if (npcIndex < Main.maxNPCs)
            {
                Main.npc[npcIndex].ai[1] = owner.whoAmI; // dipake buat kredit damage spike ke owner
                Main.npc[npcIndex].ai[2] = 2f;            // mode accessory (friendly, bukan dash/boss)
                Main.npc[npcIndex].ai[3] = scaleMul;      // portal & spike accessory jauh lebih kecil
                Main.npc[npcIndex].netUpdate = true;
            }
        }

        // Pilih 1 dari 4 sisi (atas/bawah/kiri/kanan) di sekeliling target,
        // offset-nya FLAT (AccessoryOffsetDistance) dari Center — SENGAJA
        // gak lagi ngikutin target.width/height kayak versi sebelumnya,
        // biar portal selalu deket sprite musuh & spike-nya konsisten kena,
        // gak peduli gede/kecilnya hitbox si musuh.
        static Vector2 GetRandomOffsetPosition(NPC target)
        {
            int dir = Main.rand.Next(4); // 0=Up, 1=Down, 2=Left, 3=Right
            switch (dir)
            {
                case 0:
                    return target.Center + new Vector2(0f, -AccessoryOffsetDistance);
                case 1:
                    return target.Center + new Vector2(0f, AccessoryOffsetDistance);
                case 2:
                    return target.Center + new Vector2(-AccessoryOffsetDistance, 0f);
                default:
                    return target.Center + new Vector2(AccessoryOffsetDistance, 0f);
            }
        }

        static bool IsFarEnoughFromOtherPortals(Vector2 candidate)
        {
            int myType = ModContent.NPCType<BonePortal>();
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.active || npc.type != myType) continue;

                if (Vector2.DistanceSquared(npc.Center, candidate) < MinPortalSpacing * MinPortalSpacing)
                    return false;
            }
            return true;
        }
    }
}