using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TheSanity.Items.CheatBook
{
    public class DebugPlayer : ModPlayer
    {
        public int RmbDamage = 1000;

        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet) {
            if (DebugUISystem.OpenDebugUIKeybind.JustPressed) {
                if (Player.HeldItem.type == ModContent.ItemType<CheatManipulationTome>()) {
                    var uiSystem = ModContent.GetInstance<DebugUISystem>();
                    if (uiSystem.DebugUserInterface.CurrentState == null) {
                        uiSystem.DebugUserInterface.SetState(uiSystem.MyDebugUI);
                    } else {
                        uiSystem.DebugUserInterface.SetState(null);
                    }
                }
            }
        }

        public override void PostUpdate() {
            var uiSystem = ModContent.GetInstance<DebugUISystem>();
            if (uiSystem?.DebugUserInterface?.CurrentState != null) {
                if (Player.HeldItem.type != ModContent.ItemType<CheatManipulationTome>()) {
                    uiSystem.DebugUserInterface.SetState(null);
                    if (uiSystem.MyDebugUI != null) {
                        uiSystem.MyDebugUI.isTyping = false; 
                    }
                }
            }
        }

        public override void SaveData(TagCompound tag) {
            tag["RmbDamage"] = RmbDamage;
        }

        public override void LoadData(TagCompound tag) {
            if (tag.ContainsKey("RmbDamage")) {
                RmbDamage = tag.GetInt("RmbDamage");
            }
        }
    }
}