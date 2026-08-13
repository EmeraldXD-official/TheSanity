using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    public class UnknownEntityPatternSelectorUISystem : ModSystem
    {
        private UserInterface patternInterface;
        internal UnknownEntityPatternSelectorUIState patternUIState;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            patternUIState = new UnknownEntityPatternSelectorUIState();
            patternUIState.Activate();
            patternInterface = new UserInterface();
        }

        public override void Unload()
        {
            patternInterface = null;
            patternUIState = null;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (patternUIState is { Visible: true })
            {
                patternInterface?.Update(gameTime);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex == -1)
                return;

            layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                "TheSanity: Unknown Entity Pattern Selector",
                delegate
                {
                    if (patternUIState is { Visible: true })
                    {
                        patternInterface.Draw(Main.spriteBatch, new GameTime());
                    }
                    return true;
                },
                InterfaceScaleType.UI
            ));
        }

        public bool IsVisible => patternUIState?.Visible == true;

        public void ShowUI()
        {
            if (patternUIState == null) return;
            patternUIState.Visible = true;
            patternInterface.SetState(patternUIState);
        }

        public void HideUI()
        {
            if (patternUIState == null) return;
            patternUIState.Visible = false;
            patternInterface.SetState(null);
        }

        public void ToggleUI()
        {
            if (IsVisible) HideUI();
            else ShowUI();
        }
    }
}
