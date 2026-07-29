using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.Items.CheatBook
{
    public class DebugUISystem : ModSystem
    {
        public static ModKeybind OpenDebugUIKeybind { get; private set; }
        internal UserInterface DebugUserInterface;
        internal DebugDamageUI MyDebugUI;

        public override void Load() {
            OpenDebugUIKeybind = KeybindLoader.RegisterKeybind(Mod, "Open Debug Damage UI", "K");
            if (!Main.dedServ) {
                DebugUserInterface = new UserInterface();
                MyDebugUI = new DebugDamageUI();
                MyDebugUI.Activate();
            }
        }

        public override void Unload() {
            OpenDebugUIKeybind = null;
        }

        public override void UpdateUI(GameTime gameTime) {
            if (DebugUserInterface?.CurrentState != null) {
                DebugUserInterface.Update(gameTime);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1) {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "TheSanity: Debug Damage UI",
                    delegate {
                        if (DebugUserInterface?.CurrentState != null) {
                            DebugUserInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }
    }
}