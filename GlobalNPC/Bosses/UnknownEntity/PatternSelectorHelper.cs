using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    // Logika bersama buat item & UI pattern selector - dipisah ke sini biar gak duplikat
    // antara UnknownEntityPatternSelector.cs (item) dan UnknownEntityPatternSelectorUIState.cs (UI).
    public static class PatternSelectorHelper
    {
        // Daftar pola yang bisa dipilih. Sengaja TIDAK termasuk Awakening/Chase/Phase2Transition/
        // DeathSequence karena itu state internal/cutscene, bukan "serangan" yang masuk akal dipaksa manual.
        public static readonly UnknownEntity.AIState[] SelectableStates =
        {
            UnknownEntity.AIState.Dash,
            UnknownEntity.AIState.ProjectileRing,
            UnknownEntity.AIState.Teleport,
            UnknownEntity.AIState.WormholeDash,
            UnknownEntity.AIState.PrismMirage,
            UnknownEntity.AIState.PhantomGrid,
            UnknownEntity.AIState.VoidCollapse,
            UnknownEntity.AIState.SerpentPortalDash,
            UnknownEntity.AIState.SummonMinionHorde,
            UnknownEntity.AIState.DeathLaserBlender,
            UnknownEntity.AIState.DimensionalMatrix,
            UnknownEntity.AIState.DimensionalShatter,
            UnknownEntity.AIState.SingularityCollapse,
            UnknownEntity.AIState.MemoryFracture,
            UnknownEntity.AIState.ShatteredReflection,
            UnknownEntity.AIState.CorrosionSpiral,
            UnknownEntity.AIState.NullZone,
        };

        // Pola yang cuma "make sense" kalau boss dianggap sudah phase 2. Kalau dipilih, isPhase2
        // otomatis dinyalain juga biar damage scaling & perilaku lain tetap konsisten.
        public static readonly HashSet<UnknownEntity.AIState> Phase2States = new HashSet<UnknownEntity.AIState>
        {
            UnknownEntity.AIState.SerpentPortalDash,
            UnknownEntity.AIState.SummonMinionHorde,
            UnknownEntity.AIState.DeathLaserBlender,
            UnknownEntity.AIState.DimensionalMatrix,
            UnknownEntity.AIState.DimensionalShatter,
            UnknownEntity.AIState.SingularityCollapse,
            UnknownEntity.AIState.MemoryFracture,
            UnknownEntity.AIState.ShatteredReflection,
            UnknownEntity.AIState.CorrosionSpiral,
            UnknownEntity.AIState.NullZone,
        };

        // Index pilihan terakhir - dibaca tooltip item & dipakai klik kiri (re-apply cepat).
        // Diupdate juga tiap kali user klik tombol di UI.
        public static int SelectedIndex = 0;

        public static UnknownEntity.AIState SelectedState => SelectableStates[SelectedIndex];

        public static string DisplayName(UnknownEntity.AIState state) => state switch
        {
            UnknownEntity.AIState.Dash => "Dash",
            UnknownEntity.AIState.ProjectileRing => "Projectile Ring",
            UnknownEntity.AIState.Teleport => "Teleport",
            UnknownEntity.AIState.WormholeDash => "Wormhole Dash",
            UnknownEntity.AIState.PrismMirage => "Prism Mirage",
            UnknownEntity.AIState.PhantomGrid => "Phantom Grid",
            UnknownEntity.AIState.VoidCollapse => "Void Collapse",
            UnknownEntity.AIState.SerpentPortalDash => "Serpent Portal Dash",
            UnknownEntity.AIState.SummonMinionHorde => "Summon Minion Horde",
            UnknownEntity.AIState.DeathLaserBlender => "Death Laser Blender",
            UnknownEntity.AIState.DimensionalMatrix => "Dimensional Matrix",
            UnknownEntity.AIState.DimensionalShatter => "Dimensional Shatter",
            UnknownEntity.AIState.SingularityCollapse => "Singularity Collapse",
            UnknownEntity.AIState.MemoryFracture => "Memory Fracture",
            UnknownEntity.AIState.ShatteredReflection => "Shattered Reflection",
            UnknownEntity.AIState.CorrosionSpiral => "Corrosion Spiral",
            UnknownEntity.AIState.NullZone => "Null Zone",
            _ => state.ToString(),
        };

        private static UnknownEntity FindActiveBoss()
        {
            int type = ModContent.NPCType<UnknownEntity>();

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == type && npc.ModNPC is UnknownEntity ue)
                {
                    return ue;
                }
            }

            return null;
        }

        // Cari boss aktif & paksa dia langsung masuk ke state ini. Return true kalau berhasil
        // (bossnya ketemu), false kalau gak ada boss aktif (dan sudah kasih chat text sendiri).
        public static bool ForcePattern(UnknownEntity.AIState state)
        {
            UnknownEntity boss = FindActiveBoss();

            if (boss == null)
            {
                Main.NewText("Unknown Entity tidak sedang aktif di dunia ini.", 255, 90, 90);
                return false;
            }

            if (Phase2States.Contains(state))
            {
                boss.isPhase2 = true;
            }

            boss.State = state;
            boss.StateTimer = 0;
            boss.SubTimer = 0;
            boss.NPC.netUpdate = true;

            Main.NewText($"Unknown Entity dipaksa masuk pola: {DisplayName(state)}", 255, 220, 120);
            return true;
        }
    }
}
