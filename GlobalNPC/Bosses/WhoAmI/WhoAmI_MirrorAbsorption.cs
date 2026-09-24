using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ============================================================================================
    // PERFECT MIRROR - PRE-SPAWN "ABSORPTION" CUTSCENE
    // ============================================================================================
    // Dipicu oleh BloodBagItem.UseItem() (WhoAmI_MirrorItems.cs) SETELAH lolos semua syarat summon
    // (lihat WhoAmIMirrorPaintingTile.TryCheckSummonReady, WhoAmI_MirrorPainting.cs) TAPI SEBELUM
    // boss-nya beneran di-spawn. Selama sequence ini player dikunci di tempat sambil isi
    // inventory-nya (armor+accessory+vanity yang lagi dipakai/armor[], dye yang lagi dipakai/dye[],
    // senjata yang lagi dipegang/HeldItem, dan seluruh isi inventory/ammo) "ditarik" sebagai SPRITE
    // ITEM ASLINYA (bukan cuma dust generik) yang terbang melengkung ke arah lukisan - seolah
    // cerminnya lagi menyerap identitas & perlengkapan player sebelum WhoAmI (bayangannya sendiri)
    // keluar dari sana. Baru SETELAH animasi ini kelar, WhoAmIMirrorPaintingTile.TrySummon()
    // dipanggil buat beneran nge-spawn NPC-nya.
    //
    // PENTING: ini CUMA VISUAL. Nggak ada satupun item yang beneran dihapus/dipindah/disentuh
    // state-nya - inventory player cuma DIBACA (buat nentuin sprite/tint apa aja yang boleh
    // "kebang", dipakai buat milih Item.type & warna tiap partikel terbang), item-nya sendiri tetap
    // utuh di tempatnya sepanjang & sesudah animasi. Sprite yang digambar cuma layer dekoratif di
    // atas game (PostDrawInterface), sama sekali nggak nyentuh Item instance beneran di inventory.
    //
    // KENAPA DIPISAH JADI MODSYSTEM SENDIRI (bukan ditumpuk ke WhoAmI.HandleCutscenes di WhoAmI.cs):
    // pada titik sequence ini mulai, NPC WhoAmI-nya SENDIRI BELUM ADA (NPC.NewNPC belum dipanggil
    // sama sekali) - jadi belum ada instance ModNPC buat nampung state cutscene-nya. Alasannya sama
    // persis kayak WhoAmIDefeatMenuSystem (lihat Whoamidefeatmenusystem.cs) buat sequence
    // kekalahan: butuh state yang jalan independen dari ada/nggaknya NPC boss.
    //
    // KAMERA: dulu cuma lerp ke satu TITIK TETAP (midpoint player<->mirror) dan nyangkut di situ
    // selamanya sepanjang sequence. Sekarang bias-nya DINAMIS - makin lama makin condong penuh ke
    // cermin (dari sedikit condong ke cermin pas Windup, sampai FULL ngunci ke cermin pas Flash) -
    // biar kerasa kamera "dipaksa" merhatiin cermin yang lagi "menyerap", bukan cuma nongkrong di
    // titik netral yang sama terus dari awal sampai akhir. Field STATIC yang dipakai sama persis
    // kayak WhoAmI.cs buat cutscene boss (WhoAmI.IsCutsceneActive / CutsceneCameraTarget /
    // CutsceneShakeIntensity, lihat WhoAmICutscenePlayer.ModifyScreenPosition) - field2 itu murni
    // static jadi bisa dipakai walaupun belum ada NPC instance sama sekali. Begitu animasi ini kelar
    // & boss-nya spawn, IsCutsceneActive SENGAJA nggak di-reset ke false dulu (lihat
    // FinishAndSummon) supaya kamera nyambung mulus dari "efek serap" langsung ke intro cutscene
    // boss-nya sendiri (aiState 100 di WhoAmI.HandleCutscenes) tanpa jeda balik ke kontrol vanilla
    // di antara keduanya.
    // ============================================================================================
    public class WhoAmIMirrorAbsorptionSystem : ModSystem
    {
        public static bool SequenceActive = false;

        private static Point16 paintingPos;
        private static int playerIndex;
        private static int timer;

        // Durasi tiap fase (dalam tick, 60 tick = 1 detik):
        //   Windup (~0.4 detik): cerminnya mulai nyala/glow pelan, belum ada apapun ketarik.
        //   Pull   (~2.5 detik): fase utama - sprite item yang mewakili isi inventory kebang dari
        //           player ke cermin, makin lama makin deras & makin cepat ("vacuum" makin kuat).
        //   Flash  (~0.4 detik): kilatan terang + cincin shockwave + shake gede sebelum boss nongol.
        private const int WindupDuration = 25;
        private const int PullDuration = 150;
        private const int FlashDuration = 25;
        private const int TotalDuration = WindupDuration + PullDuration + FlashDuration;

        // ================== SUMBER SPRITE: SNAPSHOT ISI GEAR + INVENTORY PLAYER ==================
        // Beda dari dustTypePool versi lama (yang cuma nyimpen dust TYPE ID generik per kategori),
        // ini nyimpen Item.type ASLI-nya + tint warna berbasis kategori - jadi pas digambar,
        // Terraria.GameContent.TextureAssets.Item[type] yang dipakai adalah SPRITE ASLI item
        // tersebut (armor beneran, senjata beneran, botol potion beneran, dst), bukan dust kotak
        // generik. Diisi ulang tiap kali BeginAbsorption dipanggil (lihat BuildVisualPools).
        private struct ItemVisualSeed
        {
            public int ItemType;
            public Color Tint;
            public int TrailDust;
        }

        private static readonly List<ItemVisualSeed> itemVisualPool = new List<ItemVisualSeed>();

        // ================== SPRITE YANG LAGI BENERAN TERBANG DI LAYAR ==================
        private struct FlyingItemVisual
        {
            public int ItemType;
            public Color Tint;
            public int TrailDust;
            public Vector2 StartPos;
            public Vector2 ControlPos; // titik kontrol bezier - bikin jalurnya melengkung, bukan garis lurus kaku
            public Vector2 EndPos;
            public float Delay;        // sisa tick sebelum mulai gerak (stagger biar nggak numpuk barengan)
            public float Progress;     // 0..1 sepanjang durasi terbangnya sendiri
            public float Duration;
            public float Rotation;
            public float RotationSpeed;
            public float BaseScale;
        }

        private static readonly List<FlyingItemVisual> activeVisuals = new List<FlyingItemVisual>();

        public override void OnWorldUnload()
        {
            // Jaga2 kalau player keluar dunia di tengah2 sequence (misal exit ke menu manual lewat
            // pause) - reset semua state statis biar nggak "nyangkut aktif" pas masuk dunia lain.
            SequenceActive = false;
            itemVisualPool.Clear();
            activeVisuals.Clear();
        }

        // ================== MUSIC KENA PAUSE JUGA ==================
        // Vanilla Terraria: pas player nge-pause game (ESC di singleplayer), Main.gamePaused true dan
        // SEMUA hook Update biasa (PostUpdateEverything di sini, AI() punya boss di WhoAmI.cs) BERHENTI
        // dipanggil sama sekali - tapi audio engine-nya sendiri TETAP jalan di belakang layar (itu
        // sebabnya musik vanilla biasanya tetap kedengeran walau game lagi di-pause). Karena baris
        // "paksa full volume" kita (Main.musicFade[...] = 1f, lihat PostUpdateEverything di bawah &
        // WhoAmI.AI()) juga cuma jalan lewat hook2 yang sama itu, begitu di-pause baris itu ikut
        // berhenti - TAPI lagunya sendiri nggak otomatis kemute, jadi tetap kedengeran nyala terus
        // selama pause (kebalikan dari yang diminta).
        //
        // Fix: UpdateUI() adalah salah satu hook tModLoader yang DIJAMIN tetap dipanggil walaupun
        // Main.gamePaused true (dipakai buat elemen UI yang perlu tetap "hidup"/animasi pas menu
        // pause kebuka) - manfaatin itu buat maksa mute musiknya sendiri PERSIS selama pause aktif.
        // Begitu player resume, Main.gamePaused balik false, method ini stop nge-mute, dan
        // PostUpdateEverything/AI() balik ambil alih narik full volume lagi seperti biasa.
        //
        // Dibatasi cuma nyala pas konten WhoAmI ini emang lagi aktif (fase absorpsi ATAU boss-nya
        // udah spawn) - di luar itu nggak nyentuh musik apapun sama sekali, biar nggak ikut ngubah
        // perilaku pause musik vanilla/mod lain yang nggak ada hubungannya.
        public override void UpdateUI(GameTime gameTime)
        {
            if (!Main.gamePaused) return;
            if (!SequenceActive && !NPC.AnyNPCs(ModContent.NPCType<WhoAmI>())) return;

            if (Main.curMusic >= 0 && Main.curMusic < Main.musicFade.Length)
                Main.musicFade[Main.curMusic] = 0f;
        }

        // ================== ENTRY POINT (dipanggil dari BloodBagItem.UseItem) ==================
        public static void BeginAbsorption(Point16 mirrorOrigin, Player player)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                // Dedicated server nggak punya "layar" buat nampilin animasi apapun, dan sinkronisasi
                // NPC utk klien remote di setup dedicated server emang udah jadi TODO terpisah (lihat
                // catatan MULTIPLAYER di WhoAmIMirrorPaintingTile.TrySummon) - di context ini langsung
                // skip ke summon instan tanpa sequence visual, biar nggak ada state yang nyangkut aktif
                // selamanya di server yang gak pernah nge-tick PostUpdateEverything client-side ini.
                WhoAmIMirrorPaintingTile.TrySummon(mirrorOrigin, player);
                return;
            }

            paintingPos = mirrorOrigin;
            playerIndex = player.whoAmI;
            timer = 0;
            SequenceActive = true;

            BuildVisualPools(player);

            Vector2 mirrorCenter = ComputeMirrorCenter(mirrorOrigin);

            WhoAmI.IsCutsceneActive = true;
            WhoAmI.CutsceneShakeIntensity = 0f;
            // Langsung ditarik SEBAGIAN ke arah cermin dari tick pertama (bukan mulai dari player
            // dulu terus baru pelan2 gerak kayak versi lama) - biar kerasa kamera "dipaksa" ngeliat
            // cermin begitu ritualnya mulai, bukan masih nongkrong penuh di player.
            WhoAmI.CutsceneCameraTarget = Vector2.Lerp(player.Center, mirrorCenter, 0.35f);

            // (Dulu ada MusicSyncStartTick di-set di sini buat nyinkronin fight-start ke lagu - sudah
            // dicabut, lihat catatan di WhoAmI.cs bagian deklarasi field itu. Musiknya sendiri emang
            // sengaja senyap total sepanjang fase absorpsi & intro cutscene, jadi nggak ada apa2 lagi
            // yang perlu disinkronin dari titik ini.)

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item6 with { Pitch = -0.5f, Volume = 0.9f }, player.Center);
        }

        private static Vector2 ComputeMirrorCenter(Point16 origin)
        {
            return new Vector2(
                origin.X * 16f + (WhoAmIMirrorPaintingTile.FootprintWidth * 16f) / 2f,
                origin.Y * 16f + (WhoAmIMirrorPaintingTile.FootprintHeight * 16f) / 2f);
        }

        // Baca (BUKAN ubah) seluruh gear & inventory player, dan isi itemVisualPool dengan satu
        // entry per slot yang keisi - armor/accessory yang dipakai & vanity (Player.armor, semuanya
        // dalam 1 array di vanilla), dye yang dipakai (Player.dye), senjata yang lagi dipegang
        // (Player.HeldItem), dan seluruh isi inventory termasuk ammo & potion (Player.inventory).
        // Tiap entry nyimpen Item.type ASLI-nya (buat sprite-nya sendiri) + tint kategori (buat
        // glow/trail dust-nya).
        private static void BuildVisualPools(Player player)
        {
            itemVisualPool.Clear();

            // Player.armor: separuh pertama = SLOT YANG DIPAKAI (index 0-2 armor, sisanya
            // accessory), separuh kedua = SLOT VANITY (armor + accessory vanity).
            int half = player.armor.Length / 2;
            for (int i = 0; i < player.armor.Length; i++)
            {
                Item it = player.armor[i];
                if (it == null || it.IsAir) continue;

                bool isVanitySlot = i >= half;
                bool isArmorPiece = (i % half) < 3;

                Color tint = isVanitySlot
                    ? new Color(255, 225, 140)   // vanity - keemasan lembut, beda dari gear "asli"
                    : (isArmorPiece ? new Color(200, 210, 255) : new Color(255, 205, 90)); // armor perak-kebiruan vs accessory kuning
                int trailDust = isVanitySlot ? DustID.SilverCoin : (isArmorPiece ? DustID.Silver : DustID.PurpleTorch);

                itemVisualPool.Add(new ItemVisualSeed { ItemType = it.type, Tint = tint, TrailDust = trailDust });
            }

            foreach (Item dyeItem in player.dye)
            {
                if (dyeItem == null || dyeItem.IsAir) continue;
                // Dye disamain kayak item lain (botolnya ikut keliatan kesedot), tapi tint-nya
                // di-random per slot pakai hslToRgb biar kerasa "dye" beneran (macem-macem warna),
                // bukan cuma 1 warna flat kayak kategori lain.
                Color hueTint = Main.hslToRgb(Main.rand.NextFloat(), 0.65f, 0.65f);
                itemVisualPool.Add(new ItemVisualSeed { ItemType = dyeItem.type, Tint = hueTint, TrailDust = 198 });
            }

            if (player.HeldItem != null && !player.HeldItem.IsAir)
                itemVisualPool.Add(new ItemVisualSeed { ItemType = player.HeldItem.type, Tint = new Color(230, 170, 255), TrailDust = DustID.SilverCoin });

            foreach (Item inv in player.inventory)
            {
                if (inv == null || inv.IsAir) continue;

                Color tint;
                int trailDust;
                if (inv.potion)
                {
                    tint = new Color(255, 110, 110); // potion - merah, senada tema blood bag
                    trailDust = DustID.Blood;
                }
                else if (inv.ammo != AmmoID.None)
                {
                    tint = new Color(255, 170, 90);
                    trailDust = DustID.PurpleTorch;
                }
                else
                {
                    tint = new Color(210, 200, 255);
                    trailDust = DustID.Silver;
                }

                itemVisualPool.Add(new ItemVisualSeed { ItemType = inv.type, Tint = tint, TrailDust = trailDust });
            }

            // Edge case aneh (inventory player kebetulan beneran kosong total, misal char baru
            // lewat debug/testing) - biarin kosong, SpawnFlyingItem sendiri udah guard buat itu.
        }

        public override void PostUpdateEverything()
        {
            if (!SequenceActive)
            {
                // Sequence utamanya udah kelar (misal boss keburu di-summon), tapi mungkin masih ada
                // sisa sprite yang belum "nyampe" ke cermin - biarin tetep dianimasikan sampai abis
                // daripada ilang/snap mendadak begitu sequence-nya berhenti.
                if (activeVisuals.Count > 0) UpdateFlyingVisuals();
                return;
            }

            if (playerIndex < 0 || playerIndex >= Main.maxPlayers || !Main.player[playerIndex].active || Main.player[playerIndex].dead)
            {
                // Player disconnect/mati di tengah animasi (edge case langka) - batalin sequence-nya
                // bersih2, jangan sampai boss nyangkut ke-pending nyummon buat player yang udah nggak
                // valid, dan jangan sampai kunci kamera nyangkut aktif selamanya.
                SequenceActive = false;
                itemVisualPool.Clear();
                activeVisuals.Clear();
                WhoAmI.IsCutsceneActive = false;
                WhoAmI.CutsceneShakeIntensity = 0f;
                return;
            }

            Player player = Main.player[playerIndex];
            Vector2 mirrorCenter = ComputeMirrorCenter(paintingPos);

            // Kunci kontrol player - pattern yang sama persis kayak HandleDesperationCutscene di
            // WhoAmI.cs buat freeze player selama cutscene boss lainnya (di-reset tiap tick sepanjang
            // sequence aktif, bukan cuma sekali di awal).
            player.controlLeft = false;
            player.controlRight = false;
            player.controlUp = false;
            player.controlDown = false;
            player.controlJump = false;
            player.controlUseItem = false;
            player.controlUseTile = false;
            player.controlThrow = false;
            player.itemAnimation = 0;
            player.itemTime = 0;
            player.velocity.X *= 0.85f;

            WhoAmI.IsCutsceneActive = true;

            // ============================================================================
            // TEMP DEBUG (hapus baris ini setelah kepastian posisi player.Center kekonfirmasi
            // bener/salah): tembak dust hijau gede TIAP TICK persis di player.Center yang dipakai
            // kode ini buat nentuin StartPos item. Kalau titik hijau ini nongol NEMPEL di karakter
            // yang keliatan berdiri di depan mirror di layar -> player.Center udah bener, masalah
            // rendering-nya ada di tempat lain. Kalau titik hijau ini malah nongol JAUH dari
            // karakternya (misal malah di pojok bareng cluster item) -> player.Center yang dibaca
            // di sini emang udah nunjuk ke koordinat yang salah dari awal.
            Dust dbg = Dust.NewDustPerfect(player.Center, DustID.GreenTorch, Vector2.Zero, 0, default, 3f);
            dbg.noGravity = true;
            dbg.noLight = false;
            // ============================================================================

            // Bias kamera - TAPI nggak lagi dipaksa full 1.0 (nempel cermin) selama fase Pull, karena
            // itu yang bikin player (sumber asal sprite yang lagi terbang) kedorong keluar dari
            // tengah layar, jadi item2-nya keliatan "nyasar ngambang di pojok" tanpa konteks jelas
            // lagi ngapain. Sekarang dibagi 2 kurva:
            //   - Windup+Pull: bias cuma naik pelan dari 0.15 ke 0.6 - player & cermin SAMA-SAMA
            //     kelihatan (kadang bareng di 1 frame) selama item2-nya lagi kebang, biar jelas
            //     kelihatan alurnya "dari player ke cermin".
            //   - Flash: baru di fase inilah bias digas penuh ke 1.0 - momen klimaks di mana kamera
            //     LAYAK ngunci total ke cermin karena item2-nya udah abis terserap semua.
            float camBias;
            if (timer <= WindupDuration + PullDuration)
            {
                float pullT = MathHelper.Clamp(timer / (float)(WindupDuration + PullDuration), 0f, 1f);
                camBias = MathHelper.Lerp(0.15f, 0.6f, pullT);
            }
            else
            {
                float flashT = MathHelper.Clamp((timer - WindupDuration - PullDuration) / (float)FlashDuration, 0f, 1f);
                camBias = MathHelper.Lerp(0.6f, 1f, flashT);
            }
            Vector2 desiredCamTarget = Vector2.Lerp(player.Center, mirrorCenter, camBias);
            WhoAmI.CutsceneCameraTarget = Vector2.Lerp(WhoAmI.CutsceneCameraTarget, desiredCamTarget, 0.09f);

            timer++;
            UpdateFlyingVisuals();

            // Paksa full volume instan buat WhoAmITheme di sini juga (persis kayak yang dilakuin
            // WhoAmI.AI() begitu boss-nya spawn nanti, lihat WhoAmI.cs) - tanpa ini, lagu yang baru
            // aja mulai diputer WhoAmISceneEffect.Music (dari BeginAbsorption tadi) bakal nge-fade-in
            // pelan2 lewat curve default vanilla, kedengeran kecil di detik-detik pertama absorpsi.
            if (Main.curMusic >= 0 && Main.curMusic < Main.musicFade.Length)
                Main.musicFade[Main.curMusic] = 1f;

            if (timer <= WindupDuration)
            {
                HandleWindup(mirrorCenter, timer / (float)WindupDuration);
            }
            else if (timer <= WindupDuration + PullDuration)
            {
                float t = (timer - WindupDuration) / (float)PullDuration;
                HandlePull(player, mirrorCenter, t);
            }
            else if (timer <= TotalDuration)
            {
                float t = (timer - WindupDuration - PullDuration) / (float)FlashDuration;
                HandleFlash(mirrorCenter, t);
            }
            else
            {
                FinishAndSummon(player);
            }
        }

        // Fase 1: cerminnya mulai "bangun" - glow pelan-pelan nyala, dust ambient orbit tipis di
        // sekitarnya, belum ada apapun yang ketarik dari player.
        private static void HandleWindup(Vector2 mirrorCenter, float t)
        {
            Lighting.AddLight(mirrorCenter, 0.7f * t, 0.4f * t, 0.9f * t);

            if (Main.rand.NextBool(3))
            {
                Vector2 vel = Main.rand.NextVector2Circular(1.2f, 1.2f);
                Dust d = Dust.NewDustPerfect(mirrorCenter + Main.rand.NextVector2Circular(30f, 30f), DustID.PurpleTorch, vel, 0, default, 1.2f);
                d.noGravity = true;
            }

            WhoAmI.CutsceneShakeIntensity = MathHelper.Lerp(0f, 1.5f, t);
        }

        // Fase 2: fase utama - sprite item yang mewakili tiap slot inventory player kebang menuju
        // cermin (lihat SpawnFlyingItem), plus vortex ambient dust yang muter ngelilingin cermin &
        // mengecil radiusnya biar area sekitarnya keliatan "hidup" bukan cuma pas sprite lewat.
        private static void HandlePull(Player player, Vector2 mirrorCenter, float t)
        {
            Lighting.AddLight(mirrorCenter, 0.8f, 0.5f, 1f);

            // Makin lama makin deras: chance spawn per-tick naik dari 20% ke 90% biar "vacuum"-nya
            // kerasa makin kuat mendekati akhir fase.
            float spawnChance = MathHelper.Lerp(0.2f, 0.9f, t);
            if (Main.rand.NextFloat() < spawnChance)
                SpawnFlyingItem(player, mirrorCenter);

            // Cincin vortex: 2 titik dust yang muter ngelilingin cermin, radiusnya pelan2 mengecil -
            // biar terasa medan tarikannya "berputar", bukan cuma partikel lurus ke tengah doang.
            float vortexAngle = timer * 0.25f;
            float vortexRadius = MathHelper.Lerp(70f, 20f, t);
            for (int ring = 0; ring < 2; ring++)
            {
                float ang = vortexAngle + ring * MathHelper.Pi;
                Vector2 ringPos = mirrorCenter + new Vector2((float)System.Math.Cos(ang), (float)System.Math.Sin(ang) * 0.6f) * vortexRadius;
                Dust rd = Dust.NewDustPerfect(ringPos, DustID.PurpleTorch, (mirrorCenter - ringPos) * 0.05f, 0, default, 1f);
                rd.noGravity = true;
            }

            // Kilau kecil di badan player sendiri biar kelihatan efeknya emang MULAI dari player,
            // bukan cuma nongol tiba2 di tengah jalan menuju cermin.
            if (Main.rand.NextBool(2))
            {
                Dust d2 = Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(20f, 20f), DustID.Silver, Vector2.Zero, 0, default, 0.8f);
                d2.noGravity = true;
                d2.fadeIn = 0.6f;
            }

            WhoAmI.CutsceneShakeIntensity = MathHelper.Lerp(1.2f, 3.5f, t);

            // Suara "tarikan" berulang tiap ~25 tick biar berasa ritmis, bukan cuma 1 suara di awal.
            if ((timer - WindupDuration) % 25 == 0)
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.3f, Volume = 0.5f }, mirrorCenter);
        }

        // Fase 3: kilatan terang + cincin shockwave yang ngembang keluar + shake gede sesaat sebelum
        // boss-nya nongol - "titik puncak" dari proses penyerapannya.
        private static void HandleFlash(Vector2 mirrorCenter, float t)
        {
            Lighting.AddLight(mirrorCenter, MathHelper.Lerp(0.8f, 3f, t), MathHelper.Lerp(0.5f, 2f, t), MathHelper.Lerp(1f, 3.4f, t));
            WhoAmI.CutsceneShakeIntensity = MathHelper.Lerp(3.5f, 9f, t);

            for (int i = 0; i < 3; i++)
            {
                Vector2 vel = Main.rand.NextVector2Circular(4f, 4f);
                Dust d = Dust.NewDustPerfect(mirrorCenter, DustID.PurpleTorch, vel, 0, default, 1.8f);
                d.noGravity = true;
            }

            // Cincin kejut yang ngembang keluar dari cermin - dust-nya diposisikan di titik-titik
            // lingkaran (bukan random scatter) biar bentuk cincinnya jelas kelihatan, bukan cuma
            // gerombolan partikel acak.
            float ringRadius = MathHelper.Lerp(8f, 90f, t);
            const int ringPoints = 16;
            for (int p = 0; p < ringPoints; p++)
            {
                float ang = (MathHelper.TwoPi / ringPoints) * p + timer * 0.05f;
                Vector2 ringPos = mirrorCenter + new Vector2((float)System.Math.Cos(ang), (float)System.Math.Sin(ang)) * ringRadius;
                Vector2 outward = ringPos - mirrorCenter;
                if (outward != Vector2.Zero) outward.Normalize();
                Dust rd = Dust.NewDustPerfect(ringPos, DustID.SilverCoin, outward * MathHelper.Lerp(1f, 6f, t), 0, default, 1.4f);
                rd.noGravity = true;
            }
        }

        // ================== SPRITE ITEM YANG BENERAN TERBANG ==================

        // Ambil 1 entry random dari itemVisualPool, spawn 1 FlyingItemVisual baru dengan titik awal
        // di sekitar player & jalur melengkung (bezier kuadratik lewat ControlPos yang di-offset ke
        // samping) menuju cermin - biar keliatan "ditarik paksa" berputar, bukan meluncur lurus kaku.
        private static void SpawnFlyingItem(Player player, Vector2 mirrorCenter)
        {
            if (itemVisualPool.Count == 0) return;

            var seed = itemVisualPool[Main.rand.Next(itemVisualPool.Count)];

            Vector2 startPos = player.Center + Main.rand.NextVector2Circular(26f, 34f) - new Vector2(0f, 10f);

            Vector2 toMirror = mirrorCenter - startPos;
            Vector2 perpendicular = new Vector2(-toMirror.Y, toMirror.X);
            if (perpendicular != Vector2.Zero) perpendicular.Normalize();
            float arcSign = Main.rand.NextBool() ? 1f : -1f;
            Vector2 controlPos = Vector2.Lerp(startPos, mirrorCenter, 0.5f) + perpendicular * arcSign * Main.rand.NextFloat(40f, 110f);

            activeVisuals.Add(new FlyingItemVisual
            {
                ItemType = seed.ItemType,
                Tint = seed.Tint,
                TrailDust = seed.TrailDust,
                StartPos = startPos,
                ControlPos = controlPos,
                EndPos = mirrorCenter,
                Delay = Main.rand.NextFloat(0f, 8f),
                Progress = 0f,
                Duration = Main.rand.NextFloat(26f, 42f),
                Rotation = Main.rand.NextFloat(0f, MathHelper.TwoPi),
                RotationSpeed = Main.rand.NextFloat(-0.35f, 0.35f),
                BaseScale = Main.rand.NextFloat(0.75f, 1.15f),
            });
        }

        // Update posisi/rotasi tiap sprite yang lagi terbang (dipanggil 1x per tick, independen dari
        // fase mana yang lagi jalan, supaya sprite yang baru kebang di akhir Pull tetap kebaca mulus
        // sampai ke Flash). Pas sampai (Progress >= 1) langsung dihapus dari list + ledakan dust
        // kecil di titik cermin sebagai "titik serap".
        private static void UpdateFlyingVisuals()
        {
            for (int i = activeVisuals.Count - 1; i >= 0; i--)
            {
                var v = activeVisuals[i];

                if (v.Delay > 0f)
                {
                    v.Delay -= 1f;
                    activeVisuals[i] = v;
                    continue;
                }

                v.Progress += 1f / v.Duration;
                v.Rotation += v.RotationSpeed;

                if (v.Progress >= 1f)
                {
                    Vector2 pos = QuadraticBezier(v.StartPos, v.ControlPos, v.EndPos, 1f);
                    for (int d = 0; d < 4; d++)
                    {
                        Dust dd = Dust.NewDustPerfect(pos, v.TrailDust, Main.rand.NextVector2Circular(2.5f, 2.5f), 0, default, 1.3f);
                        dd.noGravity = true;
                    }
                    activeVisuals.RemoveAt(i);
                    continue;
                }

                // Jejak sparkle sepanjang jalur (di-throttle biar nggak keramean dust juga).
                if (Main.rand.NextBool(3))
                {
                    Vector2 trailPos = QuadraticBezier(v.StartPos, v.ControlPos, v.EndPos, v.Progress);
                    Dust td = Dust.NewDustPerfect(trailPos, v.TrailDust, Vector2.Zero, 0, v.Tint, 0.9f);
                    td.noGravity = true;
                    td.fadeIn = 0.4f;
                }

                activeVisuals[i] = v;
            }
        }

        private static Vector2 QuadraticBezier(Vector2 a, Vector2 control, Vector2 b, float t)
        {
            float u = 1f - t;
            return (u * u * a) + (2f * u * t * control) + (t * t * b);
        }

        // Gambar semua sprite yang lagi terbang. Dipanggil terus selama activeVisuals masih ada
        // isinya, TERLEPAS dari SequenceActive (lihat guard di PostUpdateEverything) biar sisa
        // sprite yang belum nyampe nggak snap ilang pas sequence-nya berhenti.
        //
        // FIX POSISI MELESET: sebelumnya ini di-hook ke PostDrawInterface (layer UI), dan posisinya
        // cuma dihitung `worldPos - Main.screenPosition` TANPA ikut transform zoom kamera
        // (Main.GameViewMatrix). Semua VFX lain di file ini (dust: ring vortex, shockwave, dsb)
        // otomatis kena zoom yang benar karena mereka digambar lewat sistem Dust vanilla (layer
        // WORLD). Sprite item ini beda - digambar manual, jadi kalau zoom kamera != 100% (zoom-out
        // dikit aja), posisinya melenceng makin jauh dari titik tengah layar (item keliatan
        // "kepental" ke pojok, padahal player & cermin-nya sendiri kelihatan normal). Fix: pindah ke
        // PostDrawTiles (layer WORLD, sebelum UI) dan buka SpriteBatch sendiri pakai
        // Main.GameViewMatrix.TransformationMatrix - transform yang PERSIS sama dipakai buat gambar
        // tile/dust vanilla - biar sprite-nya ikut ter-scale zoom dengan benar & selalu nempel pas
        // di posisi world yang seharusnya, sama kayak dust di sekitarnya.
        public override void PostDrawTiles()
        {
            if (activeVisuals.Count == 0) return;

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            foreach (var v in activeVisuals)
            {
                if (v.Delay > 0f) continue; // belum mulai kebang, jangan digambar dulu

                Main.instance.LoadItem(v.ItemType);
                Texture2D tex = TextureAssets.Item[v.ItemType].Value;
                if (tex == null) continue;

                // Posisi WORLD (sama persis kayak yang dipakai Dust.NewDustPerfect di seluruh file
                // ini) - PENTING: JANGAN dikurangi Main.screenPosition manual lagi di sini, matrix
                // yang dipakai Main.spriteBatch.Begin di atas udah nanganin translasi+zoom-nya
                // sekaligus (persis kayak gimana Main gambar tile & dust vanilla).
                Vector2 worldPos = QuadraticBezier(v.StartPos, v.ControlPos, v.EndPos, v.Progress);
                Vector2 drawPos = worldPos - Main.screenPosition;

                // Fade in di ~15% pertama & fade out (sambil mengecil) di ~25% terakhir - biar
                // transisinya halus di kedua ujung, bukan pop in/out mendadak.
                float fadeIn = MathHelper.Clamp(v.Progress / 0.15f, 0f, 1f);
                float fadeOut = MathHelper.Clamp((1f - v.Progress) / 0.25f, 0f, 1f);
                float alpha = System.Math.Min(fadeIn, fadeOut);
                float shrink = MathHelper.Lerp(0.15f, 1f, fadeOut); // mengecil pas hampir "kesedot"
                float scale = v.BaseScale * shrink * 0.9f;

                Vector2 origin = new Vector2(tex.Width / 2f, tex.Height / 2f);

                // Glow ganda: layer belakang lebih besar & transparan (tema ungu boss), layer depan
                // sprite item aslinya - biar keliatan "berpendar" pas ditarik, bukan sprite polos.
                Color glowColor = new Color(190, 120, 255) * (alpha * 0.5f);
                Main.spriteBatch.Draw(tex, drawPos, null, glowColor, v.Rotation, origin, scale * 1.35f, SpriteEffects.None, 0f);

                Color itemColor = Color.Lerp(Color.White, v.Tint, 0.55f) * alpha;
                Main.spriteBatch.Draw(tex, drawPos, null, itemColor, v.Rotation, origin, scale, SpriteEffects.None, 0f);
            }

            Main.spriteBatch.End();
        }

        // Fase akhir: lepas kunci kontrol player, lalu beneran panggil TrySummon buat nge-spawn
        // boss-nya. Kamera SENGAJA dibiarkan tetap terkunci (IsCutsceneActive tetap true) kalau
        // summon-nya berhasil - lihat catatan di banner komentar atas file ini.
        private static void FinishAndSummon(Player player)
        {
            SequenceActive = false;
            itemVisualPool.Clear();

            // FIX: dulu sisa sprite yang belum nyampe cermin dibiarin lanjut animasi sendirian
            // setelah sequence ini berhenti - kelihatan bagus SELAMA kamera masih di area yang sama,
            // TAPI begitu boss-nya spawn & WhoAmI ngambil alih kamera (intro cutscene-nya sendiri,
            // lihat WhoAmI.HandleCutscenes aiState 100), kamera langsung pindah fokus ke boss
            // sementara sprite2 yang ketinggalan itu tetap di posisi WORLD lama (deket player) -
            // hasilnya keliatan "nyangkut ngambang" jauh dari titik fokus baru, nggak nyambung sama
            // sekali. Fix: paksa SEMUA sisa sprite langsung "terserap" instan di sini (ledakan dust
            // kecil di titik cermin masing2, lalu dibuang dari list) - lebih baik hilang cepat pas
            // masih di frame yang sama daripada nyangkut ngambang di frame berikutnya.
            foreach (var v in activeVisuals)
            {
                Vector2 pos = QuadraticBezier(v.StartPos, v.ControlPos, v.EndPos, 1f);
                for (int d = 0; d < 3; d++)
                {
                    Dust dd = Dust.NewDustPerfect(pos, v.TrailDust, Main.rand.NextVector2Circular(2f, 2f), 0, default, 1.2f);
                    dd.noGravity = true;
                }
            }
            activeVisuals.Clear();

            player.controlLeft = true;
            player.controlRight = true;
            player.controlUp = true;
            player.controlDown = true;
            player.controlJump = true;
            player.controlUseItem = true;
            player.controlUseTile = true;
            player.controlThrow = true;

            bool started = WhoAmIMirrorPaintingTile.TrySummon(paintingPos, player);
            if (!started)
            {
                // Race condition langka (misal lukisannya kebetulan hancur PAS lagi di tengah
                // animasi, atau - di MP - boss-nya keburu di-summon dari sumber lain) - lepas juga
                // kunci kameranya, jangan sampai IsCutsceneActive nyangkut true selamanya tanpa ada
                // boss yang bakal ngambil alih & ngelepasnya sendiri.
                WhoAmI.IsCutsceneActive = false;
                WhoAmI.CutsceneShakeIntensity = 0f;
            }
        }
    }
}