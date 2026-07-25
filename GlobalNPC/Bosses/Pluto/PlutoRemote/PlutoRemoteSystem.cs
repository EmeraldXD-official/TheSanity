using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoRemote
{
    // Boilerplate standar tModLoader buat nampilin 1 UIState custom di atas game (nge-hook masuk
    // di layer interface vanilla, tepat SEBELUM "Vanilla: Mouse Text" biar tooltip vanilla tetap
    // kegambar paling atas).
    public class PlutoRemoteSystem : ModSystem
    {
        private UserInterface remoteInterface;
        private PlutoRemoteUIState remoteUI;
        private bool visible = false;

        public override void Load() {
            if (Main.dedServ) return; // gak ada GUI di dedicated server

            remoteUI = new PlutoRemoteUIState();
            remoteUI.Activate();
            remoteInterface = new UserInterface();
        }

        public override void UpdateUI(GameTime gameTime) {
            if (visible) {
                remoteInterface?.Update(gameTime);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            if (!visible) return;

            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex == -1) return;

            layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                "TheSanity: Pluto Remote GUI",
                delegate {
                    remoteInterface?.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI));
        }

        public void ShowGUI() {
            visible = true;
            remoteInterface.SetState(remoteUI);
        }

        public void HideGUI() {
            visible = false;
            remoteInterface.SetState(null);
        }

        public void ToggleGUI() {
            if (visible) HideGUI();
            else ShowGUI();
        }
    }
}
