using Terraria.ModLoader;

namespace TheSanity
{
    public class BlackwhipSystem : ModSystem
    {
        public static ModKeybind TetherKeybind { get; private set; }
        public static ModKeybind WebWalkKeybind { get; private set; } // BARU: Tombol Skill Doc Ock

        public override void Load() {
            TetherKeybind = KeybindLoader.RegisterKeybind(Mod, "Blackwhip Tether", "E");
            WebWalkKeybind = KeybindLoader.RegisterKeybind(Mod, "Blackwhip Web Walk", "Z"); // BARU: Default Tombol Z
        }

        public override void Unload() {
            TetherKeybind = null;
            WebWalkKeybind = null;
        }
    }
}