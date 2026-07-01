using Terraria.ModLoader;

namespace TheSanity
{
    public class SanityKeybinds : ModSystem
    {
        // Menyimpan instance keybind agar bisa dipanggil di file lain
        public static ModKeybind DoubleTapOverrideKey { get; private set; }

        public override void Load()
        {
            // Mendaftarkan tombol dengan nama "Double tap override" dan default bind "Mouse2"
            DoubleTapOverrideKey = KeybindLoader.RegisterKeybind(Mod, "EvilBeltDashOverride", "Mouse2");
        }

        public override void Unload()
        {
            DoubleTapOverrideKey = null;
        }
    }
}