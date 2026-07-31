using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Cultist
{
    /// <summary>
    /// Registers the two active-skill keybinds for the Cultist set:
    ///  - Celestial Absorption (heal)
    ///  - Celestial Barrage
    /// Referenced from CultistPlayer.HandleActiveSkillInput() as
    /// Items.ArmorBoss.Cultist.CultistHotkeys.CelestialHealHotkey / CelestialBarrageHotkey.
    /// </summary>
    public class CultistHotkeys : ModSystem
    {
        public static ModKeybind CelestialHealHotkey;
        public static ModKeybind CelestialBarrageHotkey;

        public override void Load()
        {
            // Change the default key bindings below to whatever you'd like the default to be -
            // players can always rebind in Settings > Controls either way.
            CelestialHealHotkey = KeybindLoader.RegisterKeybind(Mod, "Celestial Absorption", "OemPeriod");
            CelestialBarrageHotkey = KeybindLoader.RegisterKeybind(Mod, "Celestial Barrage", "OemQuestion");
        }

        public override void Unload()
        {
            CelestialHealHotkey = null;
            CelestialBarrageHotkey = null;
        }
    }
}
