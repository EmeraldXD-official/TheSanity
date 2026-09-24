using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.WhoAmI;

namespace TheSanity.Debug
{
    // ================================================================================================
    // DEBUG TOOL — WhoAmI Pattern Forcer
    // ================================================================================================
    // Dev/testing item only - not meant to ship to players. Left-click forces the boss into whichever
    // pattern is currently selected; right-click cycles the selection forward (hold Shift to cycle
    // backward) and prints the name to chat so you can see what you're about to trigger before you
    // commit to it.
    //
    // Uses the vanilla Wrench's sprite (no new art needed) - swap ItemID.Wrench for anything else if
    // you'd rather it look like something else; the numeric-ID trick (`"Terraria/Images/Item_" + id`)
    // works for any vanilla item.
    //
    // NAMESPACE/FOLDER: dropped this under TheSanity.Debug so it's easy to find and strip out of a
    // release build - move it into whatever your project's actual item folder convention is.
    // ================================================================================================
    public class WhoAmIPatternForcerItem : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Wrench;

        // Persists for the game session (not saved) - just remembers where you left off cycling.
        private static int selectedIndex = 0;

        public override void SetDefaults()
        {
            Item.width = 24;
            Item.height = 24;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useAnimation = 15;
            Item.useTime = 15;
            Item.useTurn = false;
            Item.autoReuse = false;
            Item.noMelee = true;
            Item.rare = ItemRarityID.Quest; // visually flags it as "not a normal drop"
            Item.value = 0;
            Item.maxStack = 1;
        }

        public override bool AltFunctionUse(Player player) => true;

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            var (_, label) = WhoAmI.GetCatalogEntry(selectedIndex);
            tooltips.Add(new TooltipLine(Mod, "WhoAmIForcerCurrent", $"Selected: {label} ({selectedIndex}/{WhoAmI.GetCatalogCount() - 1})"));
            tooltips.Add(new TooltipLine(Mod, "WhoAmIForcerHelp1", "Left click: force this pattern on the boss"));
            tooltips.Add(new TooltipLine(Mod, "WhoAmIForcerHelp2", "Right click: cycle selection (Shift = backward)"));
            tooltips.Add(new TooltipLine(Mod, "WhoAmIForcerHelp3", "Ctrl + left click: toggle isPhase2 on the boss"));
        }

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return true;

            bool isRightClick = player.altFunctionUse == 2;

            if (isRightClick)
            {
                bool backward = Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);
                selectedIndex += backward ? -1 : 1;
                selectedIndex = ((selectedIndex % WhoAmI.GetCatalogCount()) + WhoAmI.GetCatalogCount()) % WhoAmI.GetCatalogCount();
                var (state, label) = WhoAmI.GetCatalogEntry(selectedIndex);
                Main.NewText($"[WhoAmI Forcer] Selected [{selectedIndex}] {label} (state={state})", Color.SkyBlue);
                return true;
            }

            int bossIndex = WhoAmI.FindRealBossIndex();
            if (bossIndex < 0)
            {
                Main.NewText("[WhoAmI Forcer] No live WhoAmI boss found on screen.", Color.OrangeRed);
                return true;
            }

            if (Main.npc[bossIndex].ModNPC is not WhoAmI boss)
            {
                Main.NewText("[WhoAmI Forcer] Found the NPC but couldn't resolve its ModNPC instance.", Color.OrangeRed);
                return true;
            }

            if (Main.keyState.IsKeyDown(Keys.LeftControl) || Main.keyState.IsKeyDown(Keys.RightControl))
            {
                string toggled = boss.DebugTogglePhase2();
                Main.NewText($"[WhoAmI Forcer] {toggled}", Color.Yellow);
                return true;
            }

            var (targetState, targetLabel) = WhoAmI.GetCatalogEntry(selectedIndex);
            string result = boss.DebugForceState(targetState);
            Main.NewText($"[WhoAmI Forcer] {result}", Color.LimeGreen);
            return true;
        }
    }
}
