using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.WhoAmI;

namespace TheSanity.Debug
{
    // ================================================================================================
    // /whoamiforce — chat-command companion to WhoAmIPatternForcerItem.
    // ================================================================================================
    // Usage:
    //   /whoamiforce list                 - print every pattern's index/name/raw state id, flags any
    //                                        duplicate state ids (see WhoAmI_DebugForcer.cs)
    //   /whoamiforce <index>               - force by catalog index, e.g. /whoamiforce 8
    //   /whoamiforce <name substring>       - force by name, e.g. /whoamiforce "gravity well"
    //   /whoamiforce status                 - print the boss's current aiState/aiTimer/isPhase2
    //   /whoamiforce phase2 on|off|toggle   - set or flip isPhase2 on the boss
    // ================================================================================================
    public class WhoAmIForceCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "whoamiforce";
        public override string Usage => "/whoamiforce <list|status|phase2 on/off/toggle|index|name>";
        public override string Description => "Debug: force the WhoAmI boss into a specific attack pattern for testing.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length == 0)
            {
                caller.Reply(Usage, Color.OrangeRed);
                return;
            }

            string sub = args[0].ToLowerInvariant();

            if (sub == "list")
            {
                WhoAmI.DebugPrintCatalog();
                return;
            }

            int bossIndex = WhoAmI.FindRealBossIndex();
            if (bossIndex < 0)
            {
                caller.Reply("No live WhoAmI boss found on screen.", Color.OrangeRed);
                return;
            }

            if (Main.npc[bossIndex].ModNPC is not WhoAmI boss)
            {
                caller.Reply("Found the NPC but couldn't resolve its ModNPC instance.", Color.OrangeRed);
                return;
            }

            if (sub == "status")
            {
                caller.Reply(boss.DebugCurrentStateDescription(), Color.SkyBlue);
                return;
            }

            if (sub == "phase2")
            {
                bool? target = args.Length > 1 ? args[1].ToLowerInvariant() switch
                {
                    "on" or "true" or "1" => true,
                    "off" or "false" or "0" => false,
                    _ => (bool?)null,
                } : null;
                caller.Reply(boss.DebugTogglePhase2(target), Color.Yellow);
                return;
            }

            // Anything else: treat the whole remaining input as an index or a name query.
            string query = string.Join(" ", args);
            int catalogIndex = WhoAmI.FindCatalogIndex(query);

            if (catalogIndex == -1)
            {
                caller.Reply($"No pattern matches \"{query}\". Try /whoamiforce list.", Color.OrangeRed);
                return;
            }
            if (catalogIndex == -2)
            {
                caller.Reply($"\"{query}\" matches more than one pattern name - be more specific, or use its index.", Color.OrangeRed);
                return;
            }

            var (state, label) = WhoAmI.GetCatalogEntry(catalogIndex);
            string result = boss.DebugForceState(state);
            caller.Reply($"[{catalogIndex}] {label}: {result}", Color.LimeGreen);
        }
    }
}
