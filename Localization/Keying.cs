using Terraria.ModLoader;

namespace TheSanity
{
    public class SanityKeybinds : ModSystem
    {
        public static ModKeybind DoubleTapOverrideKey { get; private set; }
        public static ModKeybind SwitchBroadcasterModeKey { get; private set; }
        public static ModKeybind AetherfinFloodgateKey { get; private set; }

        public override void Load()
        {
            DoubleTapOverrideKey = KeybindLoader.RegisterKeybind(Mod, "EvilBeltDashOverride", "Mouse2");
            SwitchBroadcasterModeKey = KeybindLoader.RegisterKeybind(Mod, "SwitchBroadcasterMode", "V");
            AetherfinFloodgateKey = KeybindLoader.RegisterKeybind(Mod, "AetherfinFloodgate", "G");
        }

        public override void Unload()
        {
            DoubleTapOverrideKey = null;
            SwitchBroadcasterModeKey = null;
            AetherfinFloodgateKey = null;
        }
    }
}