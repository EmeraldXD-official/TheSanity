using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.GameInput;
using Terraria.UI;

namespace TheSanity.Items.TestingDeckDer
{
    public class UISearchBar : UIPanel
    {
        public event Action<string> OnContentsChanged;

        private string currentString = "";
        private bool isFocused;
        private readonly string hintText;
        private int cursorBlinkTimer;

        public UISearchBar(string hint)
        {
            hintText = hint;
            Height.Set(32f, 0f);
            Width.Set(0f, 1f);
            SetPadding(6f);
            BackgroundColor = new Color(35, 35, 55);
            BorderColor = new Color(80, 80, 120);
        }

        public override void LeftClick(UIMouseEvent evt)
        {
            base.LeftClick(evt);
            isFocused = !isFocused;
            if (isFocused)
            {
                BackgroundColor = new Color(55, 55, 85);
                Main.clrInput();
            }
            else
            {
                BackgroundColor = new Color(35, 35, 55);
            }
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Jika klik kiri terdeteksi di luar area Search Bar, matikan fokus mengetik[cite: 18]
            if (isFocused && Main.mouseLeft && !ContainsPoint(Main.MouseScreen))
            {
                isFocused = false;
                BackgroundColor = new Color(35, 35, 55);
            }

            if (isFocused)
            {
                Main.LocalPlayer.mouseInterface = true; // Kunci mouse game[cite: 19]
                PlayerInput.WritingText = true;         // Kunci input teks dasar game[cite: 18]

                // Mengadopsi sistem scanning tombol keyboard manual dari DebuffSelectorUI[cite: 18]
                foreach (Keys key in Enum.GetValues(typeof(Keys)))
                {
                    if (Main.keyState.IsKeyDown(key) && Main.oldKeyState.IsKeyUp(key))
                    {
                        // Hapus Karakter (Backspace)[cite: 18]
                        if (key == Keys.Back)
                        {
                            if (currentString.Length > 0)
                            {
                                currentString = currentString.Substring(0, currentString.Length - 1);
                                OnContentsChanged?.Invoke(currentString);
                            }
                        }
                        // Selesai Mengetik[cite: 18]
                        else if (key == Keys.Escape || key == Keys.Enter)
                        {
                            isFocused = false;
                            BackgroundColor = new Color(35, 35, 55);
                            break;
                        }
                        // Karakter Spasi[cite: 18]
                        else if (key == Keys.Space)
                        {
                            currentString += " ";
                            OnContentsChanged?.Invoke(currentString);
                        }
                        // Karakter Huruf & Angka[cite: 18]
                        else
                        {
                            string keyStr = key.ToString();
                            
                            // Deteksi Karakter Alfabet Tunggal (A-Z)[cite: 18]
                            if (keyStr.Length == 1 && char.IsLetter(keyStr[0]))
                            {
                                bool shift = Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);
                                char c = keyStr[0];
                                if (!shift) c = char.ToLower(c);
                                currentString += c;
                                OnContentsChanged?.Invoke(currentString);
                            }
                            // Deteksi Angka Atas Keyboard (D0 - D9)[cite: 18]
                            else if (keyStr.Length == 2 && keyStr.StartsWith("D") && char.IsDigit(keyStr[1]))
                            {
                                currentString += keyStr[1];
                                OnContentsChanged?.Invoke(currentString);
                            }
                            // Deteksi Angka Numpad Kanan (NumPad0 - NumPad9)[cite: 18]
                            else if (keyStr.StartsWith("NumPad") && keyStr.Length == 7 && char.IsDigit(keyStr[6]))
                            {
                                currentString += keyStr[6];
                                OnContentsChanged?.Invoke(currentString);
                            }
                        }
                    }
                }

                cursorBlinkTimer++;
                if (cursorBlinkTimer > 60) cursorBlinkTimer = 0;
            }
        }

        public string CurrentString => currentString;

        public void ResetTypingState()
        {
            isFocused = false;
            BackgroundColor = new Color(35, 35, 55);
        }

        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            base.DrawSelf(spriteBatch);

            CalculatedStyle dims = GetInnerDimensions();
            Vector2 textPos = new Vector2(dims.X + 4f, dims.Y + dims.Height * 0.5f - 8f);

            // Logika kursor berkedip statis visual[cite: 18]
            string blinkingCursor = (isFocused && cursorBlinkTimer % 40 < 20) ? "|" : "";
            
            string display = string.IsNullOrEmpty(currentString) && !isFocused ? hintText : currentString + blinkingCursor;
            Color color = string.IsNullOrEmpty(currentString) && !isFocused ? Color.Gray : Color.White;

            Utils.DrawBorderString(spriteBatch, display, textPos, color, 0.9f);
        }
    }
}