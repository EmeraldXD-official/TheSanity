using System.Collections.Generic;
using Terraria;

namespace TheSanity.Buff
{
    /// <summary>
    /// Tracks damage dealt in the last second, two ways at once:
    /// - Global (all Dummies combined) - used for the always-on "Global DPS"
    ///   readout inside the Dummy Control Panel.
    /// - Per-Dummy (keyed by NPC.whoAmI) - used for the floating DPS number
    ///   drawn next to each individual Dummy out in the world.
    ///
    /// <see cref="ShowWorldDps"/> only controls whether the per-Dummy number
    /// is drawn in the world - both values themselves keep updating no
    /// matter what that toggle is set to.
    /// </summary>
    public static class DpsTracker
    {
        public static bool ShowWorldDps = false;

        private const uint WindowTicks = 60; // ~1 real-time second at 60 ticks/sec

        private static readonly Queue<(uint tick, int damage)> globalHits = new Queue<(uint, int)>();
        private static readonly Dictionary<int, Queue<(uint tick, int damage)>> perNpcHits = new Dictionary<int, Queue<(uint, int)>>();

        public static void RegisterDamage(int npcWhoAmI, int damage)
        {
            if (damage <= 0) return;

            uint now = Main.GameUpdateCount;
            globalHits.Enqueue((now, damage));

            if (!perNpcHits.TryGetValue(npcWhoAmI, out Queue<(uint, int)> queue))
            {
                queue = new Queue<(uint, int)>();
                perNpcHits[npcWhoAmI] = queue;
            }
            queue.Enqueue((now, damage));
        }

        /// <summary>Global DPS - all Dummies combined. Powers the GUI readout.</summary>
        public static int CurrentDps
        {
            get
            {
                Prune(globalHits);
                return Sum(globalHits);
            }
        }

        /// <summary>Per-Dummy DPS. Powers the floating number drawn beside that specific Dummy.</summary>
        public static int GetDpsForNpc(int npcWhoAmI)
        {
            if (!perNpcHits.TryGetValue(npcWhoAmI, out Queue<(uint, int)> queue)) return 0;
            Prune(queue);
            return Sum(queue);
        }

        /// <summary>
        /// Call whenever every Dummy gets force-cleared (boss auto-clear,
        /// manual RMB clear-all) so both meters drop back to 0 immediately
        /// instead of slowly decaying over the rest of the rolling window.
        /// </summary>
        public static void Reset()
        {
            globalHits.Clear();
            perNpcHits.Clear();
        }

        private static int Sum(Queue<(uint tick, int damage)> queue)
        {
            int total = 0;
            foreach (var entry in queue)
                total += entry.damage;
            return total;
        }

        private static void Prune(Queue<(uint tick, int damage)> queue)
        {
            uint now = Main.GameUpdateCount;
            while (queue.Count > 0 && now - queue.Peek().tick > WindowTicks)
            {
                queue.Dequeue();
            }
        }
    }
}
