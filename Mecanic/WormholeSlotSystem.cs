using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.Mecanic
{
    public class WormholeSlotSystem : ModSystem
    {
        private UserInterface _wormholeUserInterface;
        internal WormholeSlotUI WormholeSlotUIState;

        public override void Load()
        {
            if (!Main.dedServ)
            {
                _wormholeUserInterface = new UserInterface();
                WormholeSlotUIState = new WormholeSlotUI();
                WormholeSlotUIState.Activate();
                _wormholeUserInterface.SetState(WormholeSlotUIState);
            }
        }

        public override void Unload()
        {
            _wormholeUserInterface = null;
            WormholeSlotUIState = null;
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (Main.playerInventory && _wormholeUserInterface != null)
            {
                _wormholeUserInterface.Update(gameTime);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            // Memasukkan GUI kita ke dalam layer 'Vanilla: Inventory' agar urutan gambarnya sejajar dengan slot asli
            int inventoryLayerIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Inventory"));
            if (inventoryLayerIndex != -1)
            {
                layers.Insert(inventoryLayerIndex + 1, new LegacyGameInterfaceLayer(
                    "TheSanity: Wormhole Potion Slot",
                    delegate
                    {
                        if (Main.playerInventory && _wormholeUserInterface != null)
                        {
                            _wormholeUserInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }
    }
}