using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.Items
{
    public class LoveBagUISystem : ModSystem
    {
        internal UserInterface loveBagInterface;
        internal LoveBagUIState loveBagUIState;

        private GameTime lastUpdateUiGameTime;

        public override void Load()
        {
            if (Main.dedServ) return; // Server nggak butuh UI

            loveBagUIState = new LoveBagUIState();
            loveBagUIState.Activate();

            loveBagInterface = new UserInterface();
        }

        public override void Unload()
        {
            loveBagUIState = null;
            loveBagInterface = null;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            lastUpdateUiGameTime = gameTime;
            if (loveBagInterface?.CurrentState != null)
            {
                loveBagInterface.Update(gameTime);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1)
            {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "TheSanity: Love Bag UI",
                    delegate
                    {
                        if (lastUpdateUiGameTime != null && loveBagInterface?.CurrentState != null)
                        {
                            loveBagInterface.Draw(Main.spriteBatch, lastUpdateUiGameTime);
                        }
                        return true;
                    },
                    InterfaceScaleType.UI));
            }
        }

        // Dipanggil dari LoveBag.RightClick. bagItem = item Love Bag yang lagi di-klik
        // (referensi langsung ke Item di inventory, biar bisa dikonsumsi manual belakangan).
        public void OpenLoveBagUI(Item bagItem, LoveBagRewardSet rewardSet)
        {
            Main.playerInventory = false; // FIX: auto close inventory biar nggak tabrakan sama GUI
            Main.mouseItem?.TurnToAir();  // jaga-jaga item lagi "nempel" di cursor

            loveBagUIState.Setup(bagItem, rewardSet);
            loveBagInterface.SetState(loveBagUIState);
        }

        public void CloseLoveBagUI()
        {
            loveBagInterface.SetState(null);
        }
    }
}
