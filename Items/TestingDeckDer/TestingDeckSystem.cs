using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.Items.TestingDeckDer
{
    public class TestingDeckSystem : ModSystem
    {
        internal static ModKeybind ToggleKeybind;
        private static UserInterface testingDeckInterface;
        private static TestingDeckUIState uiState;
        private static bool isVisible;

        public static int SelectedProjectileType = 0;

        // This mod's own internal name (e.g. "TheSanity"), used so its
        // category tab can always be pinned right after "Vanilla".
        public static string OwnModName { get; private set; }

        public override void Load()
        {
            if (Main.dedServ) return;

            OwnModName = Mod.Name;

            // Dedicated hotkey to open/close the menu (Default: [ key)
            ToggleKeybind = KeybindLoader.RegisterKeybind(Mod, "Open Testing Deck", "OemOpenBrackets");

            uiState = new TestingDeckUIState();
            uiState.Activate();
            testingDeckInterface = new UserInterface();
        }

        public override void Unload()
        {
            ToggleKeybind = null;
            uiState = null;
            testingDeckInterface = null;
            OwnModName = null;
        }

        public static void ToggleUI()
        {
            // VALIDATION: Only allow OPENING the menu while holding the TestingDeck item.
            // If the menu is already OPEN, it can be closed directly without needing to hold the item.
            if (!isVisible && Main.LocalPlayer.HeldItem.type != ModContent.ItemType<TestingDeck>())
            {
                return;
            }

            isVisible = !isVisible;
            testingDeckInterface.SetState(isVisible ? uiState : null);

            if (isVisible && uiState != null)
            {
                uiState.RefreshList();
            }
            else
            {
                // Extra safety so game keyboard input goes back to normal immediately when the UI closes
                Main.blockInput = false;
            }
        }

        public override void PostUpdateInput()
        {
            if (Main.dedServ || ToggleKeybind == null) return;

            if (ToggleKeybind.JustPressed)
            {
                // If the UI is already open, allow the hotkey to close it even without holding the item
                if (isVisible || Main.LocalPlayer.HeldItem.type == ModContent.ItemType<TestingDeck>())
                {
                    ToggleUI();
                }
            }
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (isVisible)
            {
                // NOTE: The continuous "holding the item" check was intentionally removed here,
                // so the UI stays on screen even if you switch weapons/items.
                testingDeckInterface?.Update(gameTime);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int mouseTextIndex = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex == -1) return;

            layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                "TheSanity: Testing Deck UI",
                delegate
                {
                    if (isVisible)
                        testingDeckInterface.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI));
        }
    }
}
