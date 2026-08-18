using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ID;

namespace TheSanity.CostumeTile
{
    // =========================================================
    // Aturan: town NPC yang BELUM PERNAH spawn sama sekali boleh "arrive"
    // natural seperti biasa (housing dll tetep berlaku vanilla). Tapi kalau
    // NPC itu udah PERNAH spawn terus MATI, dia ga akan arrive natural lagi
    // -- satu-satunya cara balik adalah lewat tombol Revive di Soul Collector
    // Altar (bayar SoulCollectorAltarEntity.ReviveCost Soul).
    //
    // KEKECUALIAN: Town Pet (Town Slime x7 warna, Town Cat, Town Dog, Town
    // Bunny) TIDAK KENA lock ini sama sekali -- mereka boleh balik/"arrive"
    // natural kapan aja walau udah pernah mati sebelumnya, ngikutin aturan
    // vanilla mereka sendiri (butuh License + rumah kosong). Lihat
    // ExemptFromLockTypes di bawah.
    //
    // CATATAN API PENTING: tModLoader TIDAK punya hook resmi buat "nyegah
    // SEBELUM spawn" yang berlaku ke NPC VANILLA (Guide, Merchant, dll).
    // Hook ModNPC.CanTownNPCSpawn(int) cuma jalan buat NPC custom bikinan
    // mod sendiri, bukan NPC vanilla (lihat tModLoader issue #1499 - vanilla
    // NPC spawning emang hardcoded & ga di-expose ke mod). Makanya di sini
    // pendekatannya beda: kita biarin NPC-nya sempet "muncul" sepersekian
    // detik itu (proses internal NPC.NewNPC), TAPI langsung kita batalin
    // (npc.active = false) di OnSpawn SEBELUM dia sempet di-draw / diproses
    // tick apapun. Ini pola umum buat "nolak spawn" pas ga ada hook yang
    // lebih halus.
    // =========================================================
    public class TownNPCRespawnLockGlobalNPC : global::Terraria.ModLoader.GlobalNPC
    {
        // Town pet -- Town Slime (7 warna/varian, MASING-MASING punya NPCID
        // SENDIRI), plus Town Cat, Town Dog, Town Bunny (Cat/Dog/Bunny beda
        // dari Town Slime: masing-masing CUMA 1 NPCID -- "breed"/warnanya
        // cuma variasi visual random pas dia muncul, BUKAN tipe NPC yang
        // beda-beda kayak Town Slime).
        public static readonly System.Collections.Generic.HashSet<int> ExemptFromLockTypes = new System.Collections.Generic.HashSet<int>
        {
            NPCID.TownSlimeGreen, NPCID.TownSlimeOld, NPCID.TownSlimePurple, NPCID.TownSlimeRainbow,
            NPCID.TownSlimeRed, NPCID.TownSlimeYellow, NPCID.TownSlimeCopper, NPCID.TownSlimeBlue,
            NPCID.TownCat, NPCID.TownDog, NPCID.TownBunny,
            // Ketiga ini punya aturan arrival unik sendiri di vanilla
            // (Travelling Merchant dateng/pergi random tiap hari, Old Man &
            // Skeleton Merchant malah ga pernah nampilin pesan "has arrived"
            // sama sekali di vanilla) -- dikecualiin total dari lock juga.
            NPCID.TravellingMerchant, NPCID.OldMan, NPCID.SkeletonMerchant
        };

        // =====================================================
        // Flag koordinasi buat 2 On-hook yang DIPASANG DI Mod.cs KAMU
        // (bukan di file ini) -- hook registration harus dari
        // Mod.Load()/Unload(), bukan dari GlobalNPC. Lihat contoh kode
        // hook-nya yang dikasih terpisah.
        //
        // SuppressNextArrivalText: di-set true pas kita cancel spawn NPC
        // yang udah pernah mati -> dibaca sama hook Main.NewText buat
        // nyegah pesan "X has arrived" nongol.
        //
        // LastSpawnAttemptBlocked: sama, tapi dibaca sama hook
        // WorldGen.SpawnHomelessNPC buat mutusin perlu retry atau ngga
        // (biar kuota spawn harian ga kebuang gara-gara ke-block).
        // =====================================================
        public static bool SuppressNextArrivalText = false;
        public static bool LastSpawnAttemptBlocked = false;

        public static void ResetBlockedFlag() => LastSpawnAttemptBlocked = false;

        // Di-set sesaat SEBELUM NPC.NewNPC() dipanggil dari
        // SoulCollectorUIState.TryRevive(), biar OnSpawn tau kalau spawn NPC
        // tipe ini LEGIT (lagi dibayar/direvive lewat Altar), bukan arrival
        // natural biasa. One-shot: otomatis ke-reset begitu kepake sekali,
        // jadi ga nyangkut ganggu spawn NPC lain tipe yang sama nantinya.
        private static int _authorizedReviveType = -1;

        public static void AuthorizeRevive(int npcType)
        {
            _authorizedReviveType = npcType;
        }

        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            if (!npc.townNPC)
                return;

            bool isAuthorizedRevive = _authorizedReviveType == npc.type;
            if (isAuthorizedRevive)
                _authorizedReviveType = -1; // konsumsi flag one-shot-nya

            // Town pet dikecualikan total dari lock -- boleh arrive natural
            // kapan aja terlepas dari hasDiedBefore.
            bool isExempt = ExemptFromLockTypes.Contains(npc.type);

            bool hasDiedBefore = SoulTrackerSystem.GetKillCount(npc.type) > 0;

            if (hasDiedBefore && !isAuthorizedRevive && !isExempt)
            {
                // NPC ini udah pernah mati & bukan lagi di-revive lewat Altar
                // (dan bukan town pet yang dikecualikan) -> batalin arrival
                // natural-nya. Cuma sisi otoritatif yang boleh, biar client
                // ga ikut mutusin sepihak.
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    npc.active = false;
                    npc.life = 0;
                    npc.netUpdate = true;

                    // Kasih tau hook di Mod.cs: attempt ini ke-block, jadi
                    // (a) sembunyiin pesan "has arrived"-nya, dan
                    // (b) jangan biarin kuota spawn harian kebuang -> retry.
                    SuppressNextArrivalText = true;
                    LastSpawnAttemptBlocked = true;
                }

                return;
            }

            // Town pet (isExempt) TIDAK dikasih animasi fade-in sama sekali --
            // BUKAN cuma soal estetika, tapi FIX BUG: PreAI() SoulReviveGlobalNPC
            // nge-anchor NPC diem (npc.Center dipaksa balik tiap tick) + return
            // false (skip AI default) selama FadeDurationTicks (1 detik penuh).
            // Town Pet (Slime/Cat/Dog/Bunny) ternyata punya logic
            // spawn-validation SENDIRI di AI vanilla-nya yang HARUS jalan dari
            // tick pertama dia hidup -- kalau di-freeze/di-skip 1 detik penuh,
            // validasi itu ke-skip juga dan si pet keanggep "invalid" ->
            // langsung di-despawn/poof sendiri sama vanilla abis freeze-nya
            // lepas. Karena "materialize dari kematian" ini emang konsepnya
            // ga relevan buat town pet (mereka boleh muncul kapan aja, dan
            // ga pernah "di-lock" abis mati -- lihat isExempt di atas), pet
            // yang exempt CUKUP dibiarin arrive polos kayak vanilla biasa,
            // AI-nya jalan normal dari detik pertama.
            //
            // Non-exempt NPC (kedatangan pertama kali ATAU lagi direvive
            // lewat Altar) tetep dapet animasi fade-in 1 detik seperti biasa.
            // (Khusus revive: TryRevive() bakal langsung manggil BeginRevive()
            // lagi setelah ini, yang restart fade + nambahin efek kepental --
            // overwrite fade natural yang barusan dimulai di sini, jadi aman.)
            if (!isExempt)
                npc.GetGlobalNPC<SoulReviveGlobalNPC>().BeginNaturalSpawnFade(npc);
        }
    }
}
