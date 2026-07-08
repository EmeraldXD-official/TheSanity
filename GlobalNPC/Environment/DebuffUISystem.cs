using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.GameInput;
using TheSanity.Items; // ✨ REQUIRED: Agar sistem mengenali item DummyTool

namespace TheSanity.Buff
{
    public class DebuffUISystem : ModSystem
    {
        internal UserInterface DebuffUserInterface;
        internal DebuffSelectorUI DebuffUI;
        public static ModKeybind ToggleUIKeybind { get; private set; }
        
        // ✨ VARIABEL BARU: Menyimpan status On/Off Contact Damage (Default: false / OFF)
        public static bool ContactDamageEnabled = false;

        public override void Load() {
            if (!Main.dedServ) {
                DebuffUserInterface = new UserInterface();
                DebuffUI = new DebuffSelectorUI();
                DebuffUI.Activate();
                ToggleUIKeybind = KeybindLoader.RegisterKeybind(Mod, "Toggle Dummy UI", "K");
            }
        }

        public override void Unload() {
            ToggleUIKeybind = null;
        }

        public override void UpdateUI(GameTime gameTime) {
            if (DebuffUserInterface?.CurrentState != null) {
                DebuffUserInterface.Update(gameTime);
            }
        }

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

        public void ToggleUI() {
            if (DebuffUserInterface.CurrentState == null) {
                DebuffUI.searchFilter = "";
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

    public class DebuffUIPlayer : ModPlayer
    {
        public override void ProcessTriggers(TriggersSet triggersSet) {
            if (DebuffUISystem.ToggleUIKeybind != null && DebuffUISystem.ToggleUIKeybind.JustPressed) {
                // ✨ FIX UTAMA: UI hanya bisa terbuka jika item yang sedang dipegang aktif adalah DummyTool
                if (Player.HeldItem.type == ModContent.ItemType<DummyTool>()) {
                    ModContent.GetInstance<DebuffUISystem>().ToggleUI();
                }
            }
        }
    }
}