using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity
{
    public class BlackwhipSystem : ModSystem
    {
        public static ModKeybind TetherKeybind { get; private set; }
        public static ModKeybind WebWalkKeybind { get; private set; } 

        private UserInterface _whipUserInterface;
        internal WhipSelectionUIState WhipSelectionUI;

        public override void Load() {
            TetherKeybind = KeybindLoader.RegisterKeybind(Mod, "Blackwhip Tether", "E");
            WebWalkKeybind = KeybindLoader.RegisterKeybind(Mod, "Blackwhip Web Walk", "Z"); 

            if (!Main.dedServ) {
                _whipUserInterface = new UserInterface();
                WhipSelectionUI = new WhipSelectionUIState();
                WhipSelectionUI.Activate();
            }
        }

        public override void Unload() {
            TetherKeybind = null;
            WebWalkKeybind = null;
            _whipUserInterface = null;
            WhipSelectionUI = null;
        }

        public void ToggleWhipUI() {
            if (_whipUserInterface.CurrentState == null) {
                _whipUserInterface.SetState(WhipSelectionUI);
            } else {
                _whipUserInterface.SetState(null);
            }
        }

        public void CloseWhipUI() {
            _whipUserInterface?.SetState(null);
        }

        public override void UpdateUI(GameTime gameTime) {
            if (_whipUserInterface?.CurrentState != null) {
                _whipUserInterface.Update(gameTime);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1) {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "TheSanity: Whip Tag Selection UI",
                    delegate {
                        if (_whipUserInterface?.CurrentState != null) {
                            _whipUserInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }
    }
}
