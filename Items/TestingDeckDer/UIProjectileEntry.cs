using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.Items.TestingDeckDer
{
    public class UIProjectileEntry : UIElement
    {
        private readonly int projType;
        private readonly string displayName;

        public UIProjectileEntry(int type, string name)
        {
            projType = type;
            
            // Deteksi asal mod untuk mempermudah mencari projectile kustomisasi kita
            ModProjectile modProj = ModContent.GetModProjectile(type);
            string modOrigin = modProj != null ? $"[{modProj.Mod.Name}]" : "[Vanilla]";

            displayName = $"{modOrigin} ID {projType}: {name}";

            Width.Set(0f, 1f);
            Height.Set(30f, 0f);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            CalculatedStyle dims = GetDimensions();
            Rectangle rect = dims.ToRectangle();
            bool hover = rect.Contains(Main.mouseX, Main.mouseY);

            // Cek apakah data log entitas ini yang sedang dipilih oleh developer
            bool isCurrentActive = TestingDeckSystem.SelectedProjectileType == projType;

            Color bg;
            if (isCurrentActive) bg = new Color(100, 90, 40); // Warna kecokelatan emas jika dipilih aktif
            else bg = hover ? new Color(70, 70, 110) : new Color(40, 40, 60);

            Utils.DrawInvBG(spriteBatch, rect, bg);

            Color textColor = isCurrentActive ? Color.Gold : (hover ? Color.Cyan : Color.White);
            Utils.DrawBorderString(spriteBatch, displayName, new Vector2(rect.X + 8f, rect.Y + 6f), textColor, 0.80f);

            if (hover)
            {
                Main.LocalPlayer.mouseInterface = true;

                if (Main.mouseLeft && Main.mouseLeftRelease)
                {
                    // Pilih projectile ini untuk ditembakkan dengan LMB nanti
                    TestingDeckSystem.SelectedProjectileType = projType;
                    Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
                }
            }
        }
    }
}