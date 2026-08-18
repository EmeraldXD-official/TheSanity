using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TheSanity.CostumeTile
{
    // Ngelacak NPC apa aja yang udah pernah player lihat & berapa kali mereka mati.
    // World-scoped (per save). Karena cuma nyimpen berdasarkan NPC.type (int) dan
    // bukan hardcode daftar ID vanilla, ini otomatis "support" NPC modded juga.
    //
    // CATATAN: nyimpen NPC.type mentah-mentah kurang aman buat kompatibilitas
    // jangka panjang kalau daftar mod yang ke-load berubah (id modded NPC bisa geser).
    // Kalau versi mod kamu bakal dipublish luas dan world-nya harus awet lintas update,
    // pertimbangin nyimpen "ModName:NPCName" string di save data, bukan int type.
    //
    // =========================================================
    // FIX: NPC list di GUI Soul Collector kosong ("?") buat SEMUA town NPC
    // sampe minimal 1 yang mati.
    //
    // Root cause: DiscoveredNPCs cuma keisi lewat 2 hook -- OnSpawn (NPC
    // BENERAN baru "lahir" lewat NPC.NewNPC()) dan OnKill (NPC mati). Town
    // NPC yang UDAH ADA & HIDUP dari save file (di-load langsung ke Main.npc[]
    // pas world dibuka, BUKAN lewat NewNPC()) TIDAK PERNAH triggering OnSpawn
    // sama sekali -- jadi dia ga pernah kecatet "discovered", dan bakal
    // ke-render "?" terus di grid sampe entah dia mati (OnKill) atau ada NPC
    // town lain yang arrival natural BARU di sesi ini (OnSpawn).
    //
    // Fix: scan manual Main.npc[] sekali tiap kali world di-load (dijadwalin
    // 1 tick SETELAH OnWorldLoad lewat PostUpdateEverything, bukan langsung
    // di OnWorldLoad, biar ga peduli urutan hook OnWorldLoad vs LoadWorldData
    // -- pas PostUpdateEverything jalan, world/NPC/save data udah PASTI
    // selesai ke-load semua) -- semua town NPC yang lagi aktif otomatis
    // di-mark "discovered" walau belum pernah trigger OnSpawn/OnKill di sesi
    // ini. Jalan juga di sisi client (masing-masing peer scan Main.npc versi
    // dia sendiri yang emang udah di-sync dari server lewat netcode vanilla),
    // jadi ini juga ngebantu kasus client baru join server yang altar-nya
    // udah lama jalan.
    // =========================================================
    public class SoulTrackerSystem : ModSystem
    {
        public static HashSet<int> DiscoveredNPCs = new HashSet<int>();
        public static Dictionary<int, int> KillCounts = new Dictionary<int, int>();

        // Nge-flag "abis world load, ada rescan tertunda". Dieksekusi 1 tick
        // kemudian lewat PostUpdateEverything (bukan langsung di OnWorldLoad),
        // biar TIDAK gantung ke urutan pasti antara OnWorldLoad dan
        // LoadWorldData(tag) -- di titik PostUpdateEverything pertama, world
        // udah 100% selesai ke-load (tile, NPC, & tag data mod semua udah
        // beres), jadi Main.npc[] dijamin udah keisi NPC hasil save.
        private static bool _pendingInitialScan;

        public override void OnWorldLoad()
        {
            DiscoveredNPCs.Clear();
            KillCounts.Clear();
            _pendingInitialScan = true;
        }

        public override void OnWorldUnload()
        {
            DiscoveredNPCs.Clear();
            KillCounts.Clear();
            _pendingInitialScan = false;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            tag["discovered"] = new List<int>(DiscoveredNPCs);

            var killTypes = new List<int>();
            var killAmounts = new List<int>();
            foreach (KeyValuePair<int, int> kvp in KillCounts)
            {
                killTypes.Add(kvp.Key);
                killAmounts.Add(kvp.Value);
            }
            tag["killTypes"] = killTypes;
            tag["killAmounts"] = killAmounts;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            // Pakai UnionWith (bukan reassign "= new HashSet<int>(...)") biar
            // kalau rescan dari PostUpdateEverything kebetulan udah sempet
            // jalan duluan (harusnya ga -- LoadWorldData ini bagian dari
            // proses loading, sebelum tick pertama -- tapi ini jaga-jaga aja
            // biar urutan hook ga bisa saling menimpa/ngilangin data satu
            // sama lain).
            DiscoveredNPCs.UnionWith(tag.GetList<int>("discovered"));

            IList<int> types = tag.GetList<int>("killTypes");
            IList<int> amounts = tag.GetList<int>("killAmounts");
            for (int idx = 0; idx < types.Count && idx < amounts.Count; idx++)
                KillCounts[types[idx]] = amounts[idx];
        }

        public override void PostUpdateEverything()
        {
            if (!_pendingInitialScan)
                return;

            _pendingInitialScan = false;
            RescanExistingTownNPCs();
        }

        // Liat komentar panjang di atas class -- ini yang nutup celah "town
        // NPC yang udah hidup dari save file ga pernah trigger OnSpawn".
        private static void RescanExistingTownNPCs()
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.townNPC)
                    DiscoveredNPCs.Add(npc.type);
            }
        }

        public static bool IsAlive(int npcType)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == npcType)
                    return true;
            }
            return false;
        }

        public static int GetKillCount(int npcType)
        {
            return KillCounts.TryGetValue(npcType, out int count) ? count : 0;
        }
    }

    public class SoulTrackerGlobalNPC : global::Terraria.ModLoader.GlobalNPC
    {
        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            SoulTrackerSystem.DiscoveredNPCs.Add(npc.type);
        }

        public override void OnKill(NPC npc)
        {
            SoulTrackerSystem.DiscoveredNPCs.Add(npc.type);

            if (SoulTrackerSystem.KillCounts.ContainsKey(npc.type))
                SoulTrackerSystem.KillCounts[npc.type]++;
            else
                SoulTrackerSystem.KillCounts[npc.type] = 1;
        }
    }
}
