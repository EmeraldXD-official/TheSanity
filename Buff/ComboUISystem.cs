using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.Buff
{
    public class ComboUISystem : ModSystem
    {
        private ComboUIState comboUIState;
        private UserInterface comboUserInterface;

        public override void Load()
        {
            if (Main.dedServ)
            {
                return; // server tidak butuh UI
            }

            comboUIState = new ComboUIState();
            comboUserInterface = new UserInterface();
            comboUserInterface.SetState(comboUIState);
        }

        public override void Unload()
        {
            comboUIState = null;
            comboUserInterface = null;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            comboUserInterface?.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            if (comboUserInterface == null)
            {
                return;
            }

            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex == -1)
            {
                return;
            }

            layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                "TheSanity: Combo UI",
                delegate
                {
                    comboUserInterface.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI
            ));
        }
    }
}
