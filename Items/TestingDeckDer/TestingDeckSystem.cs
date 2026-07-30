using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.Items.TestingDeckDer
{
    public class TestingDeckSystem : ModSystem
    {
        internal static ModKeybind ToggleKeybind;
        private static UserInterface testingDeckInterface;
        private static TestingDeckUIState uiState;
        private static bool isVisible;

        public static int SelectedProjectileType = 0; 

        public override void Load()
        {
            if (Main.dedServ) return;

            // Hotkey khusus untuk buka/tutup menu (Default: Tombol [ )
            ToggleKeybind = KeybindLoader.RegisterKeybind(Mod, "Buka Testing Deck", "OemOpenBrackets");

            uiState = new TestingDeckUIState();
            uiState.Activate();
            testingDeckInterface = new UserInterface();
        }

        public override void Unload()
        {
            ToggleKeybind = null;
            uiState = null;
            testingDeckInterface = null;
        }

        public static void ToggleUI()
        {
            // VALIDASI PEMANGGILAN: Hanya ijinkan BUKA menu jika sedang memegang TestingDeck[cite: 17]
            // Namun jika menu sedang TERBUKA, boleh langsung ditutup tanpa syarat memegang item[cite: 17]
            if (!isVisible && Main.LocalPlayer.HeldItem.type != ModContent.ItemType<TestingDeck>())
            {
                return;
            }

            isVisible = !isVisible;
            testingDeckInterface.SetState(isVisible ? uiState : null);
            
            if (isVisible && uiState != null)
            {
                uiState.RefreshList();
            }
            else
            {
                // Pengaman tambahan agar input keyboard game langsung normal saat UI ditutup
                Main.blockInput = false;
            }
        }

        public override void PostUpdateInput()
        {
            if (Main.dedServ || ToggleKeybind == null) return;

            if (ToggleKeybind.JustPressed)
            {
                // Jika UI sedang terbuka, ijinkan hotkey untuk langsung menutupnya walau tidak memegang item[cite: 17]
                if (isVisible || Main.LocalPlayer.HeldItem.type == ModContent.ItemType<TestingDeck>())
                {
                    ToggleUI();
                }
            }
        }

        public override void UpdateUI(GameTime gameTime)
        {
            if (isVisible)
            {
                // FIX: Proteksi pengecekan HeldItem terus-menerus di sini DIHAPUS[cite: 17]
                // Sehingga UI akan tetap menetap di layar meskipun kamu mengganti senjata/buku[cite: 17]
                testingDeckInterface?.Update(gameTime);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            int mouseTextIndex = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex == -1) return;

            layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                "TheSanity: Testing Deck UI",
                delegate
                {
                    if (isVisible)
                        testingDeckInterface.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI));
        }
    }
}