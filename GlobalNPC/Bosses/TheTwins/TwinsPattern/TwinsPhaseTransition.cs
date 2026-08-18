using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Systems;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // PHASE TRANSITION — 50% HP: SUMMON "SPECTRE CLONES"
    // ==========================================
    // Dipisah ke file sendiri kayak pattern-pattern lain (TwinDash.cs, TwinsRetBeam.cs, dst),
    // TAPI ini BUKAN salah satu pattern yang ikut rotasi dispatcher normal
    // (Dash -> RetBeam -> CursedRain -> SpinningCurse -> BorderShot -> LaserBarrage -> Dash).
    // Ini EVENT SEKALI PAKAI yang dicek TERPISAH tiap tick di PreAI Spazmatism (lihat
    // TwinsRework.cs).
    //
    // SEKARANG 2 TAHAP (Arm -> Trigger), BUKAN LANGSUNG SEKALI JALAN LAGI:
    //
    //   TAHAP 1 — ARM (ShouldArm/Arm):
    //     Begitu npc.life (Spazmatism, sumber kebenaran HP - Retinazer cuma mirror) turun
    //     ke <= 50% lifeMax buat PERTAMA KALINYA (self.PhaseTwoArmed & self.PhaseTwoTriggered
    //     masih false) -> Arm() dipanggil SEKALI:
    //       - Twin original (Spaz) LANGSUNG jadi invincible (npc.dontTakeDamage = true) DETIK
    //         ITU JUGA — TIDAK nunggu pattern yang lagi jalan kelar dulu buat urusan damage.
    //         Retinazer sendiri sudah SELALU dontTakeDamage = true tiap tick (lihat logic
    //         "attached" di PreAI TwinsReworkOverride), jadi gak perlu disentuh manual di sini.
    //       - TAPI pattern attack yang LAGI BERPUTAR (Dash/RetBeam/CursedRain/dst) DIBIARIN
    //         JALAN TERUS SAMPAI SIKLUSNYA SENDIRI KELAR — Twin gak berhenti mendadak
    //         di tengah animasi, cuma dari titik ini dia gak bisa lagi kena damage.
    //       - self.PhaseTwoArmed = true (dicek tiap tick di dispatcher TwinsRework.cs).
    //
    //   TAHAP 2 — TRIGGER (Trigger()):
    //     Dipanggil dispatcher TwinsRework.cs TEPAT pada tick di mana pattern yang lagi
    //     berjalan itu SELESAI SATU SIKLUS PENUH (momen yang sama persis kayak "gantian ke
    //     pattern berikutnya" di rotasi normal) — DENGAN SYARAT self.PhaseTwoArmed masih true.
    //     Jadi efeknya "memotong antrian": pattern BERIKUTNYA yang harusnya mulai (mis. abis
    //     Dash kelar harusnya lanjut RetBeam) TIDAK PERNAH sempat jalan, digantikan Phase 2:
    //       - self.PhaseTwoTriggered = true (permanen, gak akan ke-trigger ulang lagi).
    //       - self.PhaseTwoArmed = false (window "arm" ini kelar, digantikan window
    //         PhaseTwoSpectresActive).
    //       - Twin original di-SNAP ke titik tengah arena, berhenti total, TETAP invincible.
    //       - Arena (ArenaBorderSystem.ActiveBorders[0]) MELEBAR 40% dari radius saat itu,
    //         dianimasikan halus (bukan instan) — lihat ExpandArena().
    //       - Spawn 1x SpectreSpazmatism + 1x SpectreRetinazer di sekitar arena (NPC custom
    //         mod ini, lihat SpectreSpazmatism.cs/SpectreRetinazer.cs — mereka udah punya
    //         window invincibility 2 detik + full-heal sendiri pas baru muncul).
    //       - PhaseTwoSpectresActive = true.
    //
    //   TAHAP 3 — HOLD (HoldWhileSpectresAlive), SELAMA PhaseTwoSpectresActive true:
    //     Dispatcher pattern normal (Dash/RetBeam/dst) di-SKIP TOTAL dari PreAI — Twin
    //     original cuma diem total di tempat (dontTakeDamage TETAP true tiap tick di sini
    //     juga, jaga-jaga), cuma muter ngadap ke arah player LIVE. Render-nya jadi
    //     SEMI-TRANSPARENT (lihat OriginalTwinAlpha, dibaca TwinsRework.cs.PreDraw).
    //
    //   TAHAP 4 — SELESAI:
    //     Begitu KEDUA Spectre itu mati/non-aktif:
    //       - PhaseTwoSpectresActive di-set false lagi.
    //       - npc.dontTakeDamage di-set false lagi (damage normal balik jalan).
    //       - Sprite original balik solid (otomatis, ngikutin flag PhaseTwoSpectresActive
    //         di render).
    //       - PENTING (BEDA dari versi sebelumnya): dispatcher pattern normal TIDAK
    //         melanjutkan pattern yang sempat "dipotong" tadi — CurrentPattern di-reset
    //         PAKSA balik ke pattern PALING AWAL (Dash), sama kayak boss baru mulai fight
    //         dari nol lagi buat rotasi pattern-nya.
    //
    // CATATAN MP: sama kayak field-field pattern lain di TwinsReworkOverride, field-field
    // terkait event ini BELUM ke-sync manual lewat ModPacket — cukup buat
    // singleplayer/testing dulu.
    // ==========================================
    public static class TwinsPhaseTransition
    {
        private const float TriggerLifeFraction = 0.5f;   // ambang arm/trigger: 50% HP
        private const float ArenaExpandMultiplier = 1.4f; // arena melebar 40% (bukan 30%/20% lagi)
        private const int ArenaExpandDurationTicks = 45;  // ~0.75 detik animasi membesar, bukan instan

        // Alpha sprite Twin original (Spaz + Ret) selama event ini aktif — "semi transparent",
        // dibaca balik dari TwinsRework.cs.PreDraw buat nge-kaliin drawColor/litColor.
        // HANYA berlaku pas PhaseTwoSpectresActive true (Tahap 3) — selama Tahap 1 (Armed,
        // masih nyelesein pattern lama) Twin tetap tampil solid seperti biasa, cuma udah
        // gak bisa kena damage.
        public const float OriginalTwinAlpha = 0.35f;

        // Jarak spawn tiap Spectre dari titik tengah arena (ke kiri/kanan), biar gak numpuk
        // pas persis di titik yang sama dengan Twin original / player.
        private const float SpectreSpawnDistance = 260f;

        // ---- FIX: terbang pelan-pelan ke tengah arena (BUKAN teleport instan lagi) ----
        private const float MoveToCenterSpeed = 22f;
        private const float MoveToCenterArriveThreshold = 30f;
        private const float MoveToCenterMaxDuration = 150f; // safety timeout ~2.5 detik

        // ==========================================
        // TAHAP 1: ARM
        // ==========================================
        // Dicek tiap tick SEBELUM dispatcher pattern normal (lihat PreAI di TwinsRework.cs).
        // True cuma SEKALI seumur hidup NPC ini (begitu ke-arm, PhaseTwoArmed langsung true
        // dan gak akan ke-cek ulang lagi selama PhaseTwoTriggered masih false — jadi gak akan
        // ke-arm ulang berkali-kali walau HP naik turun lagi di sekitar ambang 50%).
        public static bool ShouldArm(NPC npc, TwinsReworkOverride self)
        {
            return !self.PhaseTwoTriggered && !self.PhaseTwoArmed
                && npc.life > 0 && npc.life <= npc.lifeMax * TriggerLifeFraction;
        }

        public static void Arm(NPC npc, TwinsReworkOverride self)
        {
            self.PhaseTwoArmed = true;

            // Invincible LANGSUNG detik ini juga — pattern yang lagi jalan (kalau ada)
            // TETAP dibiarkan menyelesaikan siklusnya sendiri (dispatcher TwinsRework.cs
            // yang urus itu, file ini gak nyentuh CurrentPattern di sini sama sekali).
            npc.dontTakeDamage = true;
        }

        // ==========================================
        // TAHAP 2: TRIGGER — dipanggil dispatcher PERSIS pas siklus pattern yang lagi
        // jalan kelar, DENGAN SYARAT self.PhaseTwoArmed true (lihat TwinsRework.cs).
        //
        // FIX: dulu Trigger() ini LANGSUNG snap posisi (teleport) + expand arena + spawn
        // Spectre semuanya sekaligus di tick yang sama. Sekarang Trigger() CUMA MULAI fase
        // "terbang pelan-pelan ke tengah arena" (PhaseTwoMovingToCenter) - expand arena,
        // spawn Spectre, dan masuk PhaseTwoSpectresActive BARU beneran kejadian begitu Twin
        // udah sampai (lihat TickMovingToCenter di bawah).
        // ==========================================
        public static void Trigger(NPC npc, TwinsReworkOverride self)
        {
            self.PhaseTwoTriggered = true;
            self.PhaseTwoArmed = false;

            // FIX: jaga-jaga tambahan di luar fix dispatcher di TwinsRework.cs (yang udah
            // gak lagi manggil Start() pattern berikutnya kalau bakal ke-Trigger di tick yang
            // sama) - reset paksa semua flag visual "kontinu" (aim line, glow, dst) di sini
            // juga, biar dijamin bersih walau ada jalur lain yang somehow masih nyetel flag
            // itu true pas titik potong ini.
            self.ResetTransientPatternVisuals();

            // Tetap invincible (udah true sejak Arm(), di-set ulang di sini juga biar
            // eksplisit/jaga-jaga) - TAPI belum snap posisi, belum expand arena, belum
            // spawn Spectre. Itu semua nunggu sampai Twin BENERAN nyampe di tengah dulu
            // (lihat TickMovingToCenter).
            npc.dontTakeDamage = true;

            self.PhaseTwoMovingToCenter = true;
            self.PhaseTwoMoveTimer = 0f;
        }

        // Dipanggil TIAP TICK selama PhaseTwoMovingToCenter true (lihat dispatcher PreAI di
        // TwinsRework.cs). Twin terbang MENUJU tengah arena pakai velocity-lerp biasa (sama
        // filosofi kayak pattern Reposition/ReturnToCenter di file lain) - BUKAN teleport.
        // Begitu udah sampai (atau timeout jaga-jaga), BARU expand arena + spawn Spectre +
        // masuk PhaseTwoSpectresActive beneran terjadi.
        public static void TickMovingToCenter(NPC npc, TwinsReworkOverride self, Player target)
        {
            Vector2 arenaCenter = GetArenaCenter(npc);
            Vector2 toCenter = arenaCenter - npc.Center;
            float distance = toCenter.Length();

            self.PhaseTwoMoveTimer++;

            if (distance > MoveToCenterArriveThreshold && self.PhaseTwoMoveTimer < MoveToCenterMaxDuration)
            {
                Vector2 moveDirection = toCenter / distance;
                npc.velocity = Vector2.Lerp(npc.velocity, moveDirection * MoveToCenterSpeed, 0.12f);

                Vector2 aimVector = target.Center - npc.Center;
                npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;
                return;
            }

            // Udah sampai (atau timeout) -> posisi PAS di tengah, berhenti total, dan BARU
            // di titik INI semua efek "kejadian" Phase 2 beneran dipicu.
            npc.Center = arenaCenter;
            npc.velocity = Vector2.Zero;
            npc.dontTakeDamage = true;
            npc.netUpdate = true;

            self.PhaseTwoMovingToCenter = false;
            self.PhaseTwoSpectresActive = true;

            ExpandArena(self);
            SpawnSpectres(npc, self, arenaCenter);

            // Matiin after-image trail Twin original SEKETIKA - jaga-jaga tambahan (walau
            // sekarang gerakannya udah gradual/gak ada lompatan besar lagi, masih ada
            // kemungkinan sisa ghost dari saat baru berhenti persis di titik ini).
            self.Trail.Clear();

            int retIndexForTrail = NPC.FindFirstNPC(NPCID.Retinazer);
            if (retIndexForTrail != -1 && Main.npc[retIndexForTrail].active)
            {
                Main.npc[retIndexForTrail].GetGlobalNPC<TwinsReworkOverride>().Trail.Clear();
            }
        }

        // Melebarkan arena aktif (kalau ada) 40% dari radius SAAT INI, DIANIMASIKAN halus
        // (bukan instan kayak sebelumnya) — cuma dipanggil SEKALI dari Trigger(), gak
        // diulang-ulang. Border.AnimateRadiusTo() otomatis nge-lerp Radius (yang dipakai
        // BARENGAN buat visual, hitbox collider, dan logic "tarik player masuk") menuju
        // target baru selama ArenaExpandDurationTicks, jadi hitbox pijakannya IKUT
        // membesar bareng visualnya, gak stuck di ukuran/lokasi lama.
        //
        // Radius SEBELUM di-expand disimpen ke self.PhaseTwoPreExpandArenaRadius, biar
        // begitu kedua Spectre tumbang (lihat HoldWhileSpectresAlive), arena bisa
        // di-animasikan MENGECIL BALIK ke ukuran semula ini - bukan nyangkut selamanya di
        // ukuran yang udah di-expand.
        private static void ExpandArena(TwinsReworkOverride self)
        {
            if (ArenaBorderSystem.ActiveBorders.Count > 0)
            {
                ArenaBorderSystem.Border border = ArenaBorderSystem.ActiveBorders[0];
                self.PhaseTwoPreExpandArenaRadius = border.Radius;
                float newRadius = border.Radius * ArenaExpandMultiplier;
                border.AnimateRadiusTo(newRadius, ArenaExpandDurationTicks);
            }
        }

        private static void SpawnSpectres(NPC npc, TwinsReworkOverride self, Vector2 arenaCenter)
        {
            self.SpectreSpazIndex = -1;
            self.SpectreRetIndex = -1;

            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            Vector2 spazSpawnPos = arenaCenter + new Vector2(-SpectreSpawnDistance, 0f);
            Vector2 retSpawnPos = arenaCenter + new Vector2(SpectreSpawnDistance, 0f);

            self.SpectreSpazIndex = NPC.NewNPC(
                npc.GetSource_FromAI(),
                (int)spazSpawnPos.X,
                (int)spazSpawnPos.Y,
                ModContent.NPCType<SpectreSpazmatism>()
            );

            self.SpectreRetIndex = NPC.NewNPC(
                npc.GetSource_FromAI(),
                (int)retSpawnPos.X,
                (int)retSpawnPos.Y,
                ModContent.NPCType<SpectreRetinazer>()
            );
        }

        // ==========================================
        // TAHAP 3 & 4: HOLD selama Spectre hidup, lalu SELESAI begitu keduanya tumbang.
        // ==========================================
        // Dipanggil tiap tick SELAMA self.PhaseTwoSpectresActive true. Twin original diem
        // total, tetap invincible, cuma muter ngadep ke player. Begitu kedua Spectre
        // ketahuan udah gak aktif lagi (mati ATAU somehow ke-despawn):
        //   - flag di-matiin balik & damage diizinkan lagi,
        //   - CurrentPattern dipaksa balik ke Dash (BUKAN lanjut dari pattern yang sempat
        //     "dipotong" pas Arm/Trigger tadi) — dispatcher normal lanjut lagi tick
        //     berikutnya otomatis mulai dari awal rotasi.
        //
        // Return true = masih harus "hold" (caller di PreAI WAJIB skip dispatcher pattern
        // normal tick ini). Return false = kedua Spectre udah tumbang, Twin original boleh
        // gerak bebas lagi MULAI TICK INI JUGA (dari Dash, state Hovering).
        public static bool HoldWhileSpectresAlive(NPC npc, TwinsReworkOverride self, Player target)
        {
            bool spazAlive = self.SpectreSpazIndex != -1 && Main.npc[self.SpectreSpazIndex].active;
            bool retAlive = self.SpectreRetIndex != -1 && Main.npc[self.SpectreRetIndex].active;

            if (!spazAlive && !retAlive)
            {
                self.PhaseTwoSpectresActive = false;
                self.SpectreSpazIndex = -1;
                self.SpectreRetIndex = -1;

                // ==========================================
                // PHASE 3 / ENRAGED — begitu kedua Spectre clone ini tumbang, Twin original
                // masuk mode "enraged" PERMANEN (gak akan pernah balik lagi buat sisa fight).
                // Semua pattern attack di rotasi normal (Dash, RetBeam, CursedRain,
                // SpinningCurse, BorderShot, LaserBarrage) baca flag ini buat versi upgrade-nya
                // masing-masing — lihat komentar di file pattern masing-masing.
                // ==========================================
                self.IsEnraged = true;

                // Kedua Spectre udah tumbang - arena yang tadi melebar 40% (lihat ExpandArena)
                // di-animasikan MENGECIL BALIK ke radius semula (sebelum di-expand), bukan
                // nyangkut di ukuran besar selamanya sampai fight kelar/dihapus.
                ShrinkArenaBack(self);

                // Window invincibility Phase 2 kelar - damage normal boleh masuk lagi.
                npc.dontTakeDamage = false;

                // Balik ke pattern PALING AWAL, bukan melanjutkan pattern yang lagi
                // berputar sebelum Phase 2 motong antrian tadi.
                self.CurrentPattern = TwinsReworkOverride.SpazPattern.Dash;
                TwinDash.ResetToHover(npc);

                npc.netUpdate = true;
                return false;
            }

            // Tetap invincible penuh selama kedua Spectre masih hidup (jaga-jaga tambahan
            // di atas yang udah di-set pas Arm()/Trigger(), kalau-kalau ke-reset dari
            // tempat lain).
            npc.dontTakeDamage = true;

            // Diam total di tempat — cuma redam sisa laju kalau ada (misal abis kena
            // knockback pas snap tadi), BUKAN Lerp ke 0 pelan-pelan.
            npc.velocity *= 0.85f;

            // Muter ngadep ke player, ngikutin posisi player LIVE (sama formula rotasi yang
            // dipakai pattern-pattern lain di codebase ini).
            Vector2 aimVector = target.Center - npc.Center;
            npc.rotation = aimVector.ToRotation() - MathHelper.PiOver2;

            return true;
        }

        // Animasikan arena aktif (kalau ada) MENGECIL BALIK ke radius yang tersimpan di
        // self.PhaseTwoPreExpandArenaRadius (di-set sekali di ExpandArena() pas Trigger()).
        // Pakai durasi animasi yang sama (ArenaExpandDurationTicks) biar transisinya sama
        // halusnya kayak pas melebar tadi.
        private static void ShrinkArenaBack(TwinsReworkOverride self)
        {
            if (ArenaBorderSystem.ActiveBorders.Count > 0 && self.PhaseTwoPreExpandArenaRadius > 0f)
            {
                ArenaBorderSystem.Border border = ArenaBorderSystem.ActiveBorders[0];
                border.AnimateRadiusTo(self.PhaseTwoPreExpandArenaRadius, ArenaExpandDurationTicks);
            }
        }

        private static Vector2 GetArenaCenter(NPC npc)
        {
            if (ArenaBorderSystem.ActiveBorders.Count > 0)
                return ArenaBorderSystem.ActiveBorders[0].Center;

            return npc.Center;
        }
    }
}
