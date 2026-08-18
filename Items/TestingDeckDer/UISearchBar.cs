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

            // If a left click is detected outside the search bar area, unfocus it
            if (isFocused && Main.mouseLeft && !ContainsPoint(Main.MouseScreen))
            {
                isFocused = false;
                BackgroundColor = new Color(35, 35, 55);
            }

            if (isFocused)
            {
                Main.LocalPlayer.mouseInterface = true; // Lock game mouse interaction
                PlayerInput.WritingText = true;         // Lock the game's default text input

                // Manual keyboard key scanning, adapted from the DebuffSelectorUI approach
                foreach (Keys key in Enum.GetValues(typeof(Keys)))
                {
                    if (Main.keyState.IsKeyDown(key) && Main.oldKeyState.IsKeyUp(key))
                    {
                        // Delete character (Backspace)
                        if (key == Keys.Back)
                        {
                            if (currentString.Length > 0)
                            {
                                currentString = currentString.Substring(0, currentString.Length - 1);
                                OnContentsChanged?.Invoke(currentString);
                            }
                        }
                        // Finish typing
                        else if (key == Keys.Escape || key == Keys.Enter)
                        {
                            isFocused = false;
                            BackgroundColor = new Color(35, 35, 55);
                            break;
                        }
                        // Space character
                        else if (key == Keys.Space)
                        {
                            currentString += " ";
                            OnContentsChanged?.Invoke(currentString);
                        }
                        // Letter & number characters
                        else
                        {
                            string keyStr = key.ToString();

                            // Single alphabet character (A-Z)
                            if (keyStr.Length == 1 && char.IsLetter(keyStr[0]))
                            {
                                bool shift = Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);
                                char c = keyStr[0];
                                if (!shift) c = char.ToLower(c);
                                currentString += c;
                                OnContentsChanged?.Invoke(currentString);
                            }
                            // Top-row number keys (D0 - D9)
                            else if (keyStr.Length == 2 && keyStr.StartsWith("D") && char.IsDigit(keyStr[1]))
                            {
                                currentString += keyStr[1];
                                OnContentsChanged?.Invoke(currentString);
                            }
                            // Numpad number keys (NumPad0 - NumPad9)
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

            // Static blinking cursor visual
            string blinkingCursor = (isFocused && cursorBlinkTimer % 40 < 20) ? "|" : "";

            string display = string.IsNullOrEmpty(currentString) && !isFocused ? hintText : currentString + blinkingCursor;
            Color color = string.IsNullOrEmpty(currentString) && !isFocused ? Color.Gray : Color.White;

            Utils.DrawBorderString(spriteBatch, display, textPos, color, 0.9f);
        }
    }
}
