using Terraria.ModLoader;

namespace TheSanity.Systems // PASTIKAN ADA TULISAN .Systems DI SINI
{
    public class MightyEagleKeybindSystem : ModSystem
    {
        public static ModKeybind EagleStrikeKey { get; private set; }

        public override void Load()
        {
            // Mendaftarkan tombol di pengaturan kontrol game
            EagleStrikeKey = KeybindLoader.RegisterKeybind(Mod, "Mighty Eagle Strike", "G");
        }

        public override void Unload()
        {
            EagleStrikeKey = null;
        }
    }
}