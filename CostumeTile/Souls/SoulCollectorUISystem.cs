using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.CostumeTile
{
    public class SoulCollectorUISystem : ModSystem
    {
        private UserInterface _userInterface;
        internal SoulCollectorUIState UIState;

        public static SoulCollectorUISystem Instance => ModContent.GetInstance<SoulCollectorUISystem>();

        public override void Load()
        {
            if (Main.dedServ)
                return;

            UIState = new SoulCollectorUIState();
            _userInterface = new UserInterface();
        }

        public override void Unload()
        {
            _userInterface = null;
            UIState = null;
        }

        // Klik kanan altar yang lagi kebuka GUI-nya -> nutup. Klik kanan altar lain -> pindah ke altar itu.
        public void ToggleAltar(SoulCollectorAltarEntity altar)
        {
            if (Main.playerInventory && _userInterface.CurrentState == UIState && UIState.CurrentAltar == altar)
            {
                CloseUI();
                return;
            }

            OpenAltar(altar);
        }

        public void OpenAltar(SoulCollectorAltarEntity altar)
        {
            UIState.SetAltar(altar);
            _userInterface.SetState(UIState);
            Main.playerInventory = true;

            SoundEngine.PlaySound(SoundID.MenuOpen);
        }

        public void CloseUI()
        {
            bool wasOpen = _userInterface.CurrentState != null;

            _userInterface.SetState(null);
            Main.playerInventory = false;

            if (wasOpen)
                SoundEngine.PlaySound(SoundID.MenuClose);
        }

        // 10 block * 16px/block = 160px. Player yang jalan kejauhan dari altar
        // yang GUI-nya lagi kebuka bakal otomatis nutup GUI-nya sendiri.
        private const float AutoCloseDistance = 160f;

        public override void UpdateUI(GameTime gameTime)
        {
            if (_userInterface?.CurrentState == null)
                return;

            // Trik standar: kalau player nutup inventory (misal pencet Esc / buka
            // inventory item biasa), GUI kita ikut ketutup otomatis.
            if (!Main.playerInventory)
            {
                CloseUI();
                return;
            }

            if (UIState.CurrentAltar != null)
            {
                // Titik tengah altar (5x3 tile = 80x48px) buat ngukur jarak player.
                Vector2 altarCenter = new Vector2(
                    UIState.CurrentAltar.Position.X * 16f + 40f,
                    UIState.CurrentAltar.Position.Y * 16f + 24f);

                float distSq = Vector2.DistanceSquared(Main.LocalPlayer.Center, altarCenter);
                if (distSq > AutoCloseDistance * AutoCloseDistance)
                {
                    CloseUI();
                    return;
                }
            }

            _userInterface.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
        {
            // Inventory vanilla SENGAJA dibiarin tetep keliatan/aktif (ga di-hide
            // lagi) -- GUI kita sekarang digeser ke kanan (lihat SoulCollectorUIState.OnInitialize)
            // jadi ga nabrak/numpuk sama panel inventory di kiri layar.
            int index = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (index == -1)
                return;

            layers.Insert(index, new LegacyGameInterfaceLayer(
                "TheSanity: Soul Collector UI",
                delegate
                {
                    if (_userInterface?.CurrentState != null)
                        _userInterface.Draw(Main.spriteBatch, new GameTime());
                    return true;
                },
                InterfaceScaleType.UI));
        }
    }
}
