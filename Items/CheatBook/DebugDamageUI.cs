using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace TheSanity.Items.CheatBook
{
    public class DebugDamageUI : UIState
    {
        private UIPanel panel;
        private UITextPanel<string> damageInputButton; 
        
        public bool isTyping = false;
        private int cursorTimer = 0;

        public override void OnInitialize() {
            panel = new UIPanel();
            panel.Width.Set(300f, 0f);
            panel.Height.Set(220f, 0f);
            panel.HAlign = 0.5f; 
            panel.VAlign = 0.4f; 
            Append(panel);

            UIText title = new UIText("Tome Debugger Config", 0.8f, true);
            title.Top.Set(10f, 0f);
            title.HAlign = 0.5f;
            panel.Append(title);

            damageInputButton = new UITextPanel<string>("RMB Damage: 1000");
            damageInputButton.Top.Set(45f, 0f);
            damageInputButton.Width.Set(260f, 0f);
            damageInputButton.Height.Set(35f, 0f);
            damageInputButton.HAlign = 0.5f;
            damageInputButton.BackgroundColor = Color.DarkSlateGray * 0.8f;
            damageInputButton.OnLeftClick += (evt, element) => {
                isTyping = !isTyping;
                if (isTyping) {
                    damageInputButton.BackgroundColor = Color.SlateGray;
                    Main.clrInput(); 
                } else {
                    damageInputButton.BackgroundColor = Color.DarkSlateGray * 0.8f;
                }
            };
            panel.Append(damageInputButton);

            CreateButton("+1K", 90f, 15f, (evt, element) => AdjustDamage(1000));
            CreateButton("+10K", 90f, 110f, (evt, element) => AdjustDamage(10000));
            CreateButton("+100K", 90f, 205f, (evt, element) => AdjustDamage(100000));
            
            CreateButton("-1K", 135f, 15f, (evt, element) => AdjustDamage(-1000));
            CreateButton("Reset 1K", 135f, 110f, (evt, element) => SetDamage(1000));
            CreateButton("ONE SHOT", 135f, 205f, (evt, element) => SetDamage(9999999));
            
            // Fix: Menutup lewat tombol 'X' sekarang otomatis mematikan status typing agar tidak mengunci keyboard
            CreateButton("X", 5f, 265f, (evt, element) => {
                ModContent.GetInstance<DebugUISystem>().DebugUserInterface.SetState(null);
                isTyping = false;
            }, 25f, 25f);
        }

        private void CreateButton(string text, float top, float left, UIElement.MouseEvent onClick, float width = 80f, float height = 30f) {
            UIPanel button = new UIPanel();
            button.Width.Set(width, 0f);
            button.Height.Set(height, 0f);
            button.Top.Set(top, 0f);
            button.Left.Set(left, 0f);
            button.BackgroundColor = new Color(50, 60, 90);
            button.OnLeftClick += onClick;

            UIText btnText = new UIText(text, 0.75f);
            btnText.HAlign = 0.5f;
            btnText.VAlign = 0.5f;
            button.Append(btnText);

            panel.Append(button);
        }

        private void AdjustDamage(int amount) {
            if (isTyping) return; 
            var player = Main.LocalPlayer.GetModPlayer<DebugPlayer>();
            player.RmbDamage += amount;
            if (player.RmbDamage < 0) player.RmbDamage = 0;
            Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
        }

        private void SetDamage(int amount) {
            if (isTyping) return;
            var player = Main.LocalPlayer.GetModPlayer<DebugPlayer>();
            player.RmbDamage = amount;
            Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);
            if (damageInputButton == null || Main.LocalPlayer == null) return;

            var player = Main.LocalPlayer.GetModPlayer<DebugPlayer>();

            if (isTyping) {
                Terraria.GameInput.PlayerInput.WritingText = true;
                string currentDamageStr = player.RmbDamage.ToString();

                foreach (Keys key in Enum.GetValues(typeof(Keys))) {
                    if (Main.keyState.IsKeyDown(key) && Main.oldKeyState.IsKeyUp(key)) {
                        
                        if (key == Keys.Back) {
                            if (currentDamageStr.Length > 0) {
                                currentDamageStr = currentDamageStr.Substring(0, currentDamageStr.Length - 1);
                                if (string.IsNullOrEmpty(currentDamageStr)) {
                                    player.RmbDamage = 0;
                                } else {
                                    if (long.TryParse(currentDamageStr, out long parsed)) {
                                        player.RmbDamage = (int)Math.Min(parsed, int.MaxValue);
                                    }
                                }
                            }
                        }
                        else if (key == Keys.Escape || key == Keys.Enter) {
                            isTyping = false;
                            damageInputButton.BackgroundColor = Color.DarkSlateGray * 0.8f;
                            break;
                        }
                        else {
                            string keyStr = key.ToString();
                            char digit = '\0';

                            if (keyStr.Length == 2 && keyStr.StartsWith("D") && char.IsDigit(keyStr[1])) {
                                digit = keyStr[1];
                            }
                            else if (keyStr.StartsWith("NumPad") && keyStr.Length == 7 && char.IsDigit(keyStr[6])) {
                                digit = keyStr[6];
                            }

                            if (digit != '\0') {
                                if (player.RmbDamage == 0) {
                                    currentDamageStr = digit.ToString();
                                } else {
                                    currentDamageStr += digit;
                                }

                                if (long.TryParse(currentDamageStr, out long parsed)) {
                                    player.RmbDamage = (int)Math.Min(parsed, int.MaxValue);
                                }
                            }
                        }
                    }
                }

                cursorTimer++;
                string blinkingCursor = (cursorTimer % 40 < 20) ? "|" : "";
                damageInputButton.SetText($"RMB Damage: {player.RmbDamage}{blinkingCursor}");
            } else {
                damageInputButton.SetText($"RMB Damage: {player.RmbDamage}");
            }
        }
    }
}