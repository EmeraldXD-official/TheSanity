using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.Buff
{
    public class DebuffUISystem : ModSystem
    {
        internal UserInterface DebuffUserInterface;
        internal DebuffSelectorUI DebuffUI;

        public override void Load() {
            if (!Main.dedServ) {
                DebuffUserInterface = new UserInterface();
                DebuffUI = new DebuffSelectorUI();
                DebuffUI.Activate();
            }
        }

        public override void UpdateUI(GameTime gameTime) {
            if (DebuffUserInterface?.CurrentState != null) {
                DebuffUserInterface.Update(gameTime);
            }
        }

        // Inserts the GUI dashboard into the game layout layer
        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1) {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "TheSanity: Control Dashboard GUI",
                    delegate {
                        if (DebuffUserInterface?.CurrentState != null) {
                            DebuffUserInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }

        // Toggles the Dashboard state when called via Dummy RMB interaction
        public void ToggleUI() {
            if (DebuffUserInterface.CurrentState == null) {
                DebuffUI.searchFilter = ""; // Reset search filter text on open
                
                // FIX: Changed from PopulateDebuffs() to PopulateList() to match the multi-tab upgrade
                DebuffUI.PopulateList(); 
                
                DebuffUserInterface.SetState(DebuffUI);
            } else {
                CloseUI();
            }
        }

        public void CloseUI() {
            DebuffUI.ResetTypingState();
            DebuffUserInterface.SetState(null);
        }
    }
}