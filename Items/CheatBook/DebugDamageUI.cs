using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace TheSanity.Items.CheatBook
{
    public class DebugDamageUI : UIState
    {
        // ---------------------------------------------------------------
        //  Layout constants
        // ---------------------------------------------------------------
        private const float PanelWidth = 380f;
        private const float PanelHeight = 300f;
        private const int OneShotThreshold = 9999999;

        // ---------------------------------------------------------------
        //  Color palette - senada sama Dummy Control Panel biar dua GUI
        //  ini kelihatan satu keluarga / satu mod, bukan asal beda-beda.
        // ---------------------------------------------------------------
        private static readonly Color ColBG           = new Color(21, 24, 36, 248);
        private static readonly Color ColAccent        = new Color(150, 120, 235);  // ungu, netral
        private static readonly Color ColMuted         = new Color(140, 140, 160);
        private static readonly Color ColReadoutIdle   = new Color(32, 34, 52);
        private static readonly Color ColReadoutTyping = new Color(64, 52, 102);
        private static readonly Color ColAdd           = new Color(45, 110, 80);
        private static readonly Color ColAddHover      = new Color(60, 145, 105);
        private static readonly Color ColSub           = new Color(130, 55, 55);
        private static readonly Color ColSubHover      = new Color(165, 75, 75);
        private static readonly Color ColReset         = new Color(60, 70, 105);
        private static readonly Color ColResetHover    = new Color(80, 92, 135);
        private static readonly Color ColOneShot       = new Color(190, 40, 90);
        private static readonly Color ColOneShotHover  = new Color(225, 60, 115);
        private static readonly Color ColWarn          = new Color(235, 150, 70);   // tier menengah (>=100K)
        private static readonly Color ColDanger        = new Color(235, 60, 90);    // tier one-shot (>=9,999,999)

        // 🔧 Sama kayak fix di Dummy Control Panel: UIPanel selalu nggambar tekstur
        // border 9-slice bawaan, yang di elemen tipis (garis aksen) ke-stretch jadi
        // aneh. Makanya garis tipis digambar manual pakai MagicPixel, gak lewat UIPanel.
        private class UISolidColor : UIElement
        {
            private readonly Color color;
            public UISolidColor(Color color) { this.color = color; }
            protected override void DrawSelf(SpriteBatch spriteBatch) {
                CalculatedStyle dims = GetDimensions();
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, dims.ToRectangle(), color);
            }
        }

        private UIPanel panel;
        private UIPanel readoutPanel;
        private UIText valueText;

        // ✨ Drag state - mekanisme identik sama Dummy Control Panel: seret dari
        // mana aja di title bar (kecuali tombol close) buat mindahin panel.
        private bool isDragging;
        private Vector2 dragOffset;

        public bool isTyping = false;
        private int cursorTimer = 0;

        public override void OnInitialize()
        {
            panel = new UIPanel();
            panel.SetPadding(0);
            panel.Left.Set(Main.screenWidth / 2f - PanelWidth / 2f, 0f);
            panel.Top.Set(Main.screenHeight / 2f - PanelHeight / 2f, 0f);
            panel.Width.Set(PanelWidth, 0f);
            panel.Height.Set(PanelHeight, 0f);
            panel.BackgroundColor = ColBG;
            panel.BorderColor = ColAccent * 0.45f;
            Append(panel);

            BuildDragHandle();
            BuildHeader();
            BuildDamageDisplay();
            BuildButtonGrid();
        }

        // ---------------------------------------------------------------
        //  Drag handle: nutupin seluruh title bar (minus close button)
        //  jadi bisa diseret dari sisi manapun, sama kayak Dummy UI.
        // ---------------------------------------------------------------
        private void BuildDragHandle()
        {
            UIElement dragHandle = new UIElement();
            dragHandle.Left.Set(0f, 0f);
            dragHandle.Top.Set(0f, 0f);
            dragHandle.Width.Set(PanelWidth - 34f, 0f); // berhenti pas sebelum closeButton
            dragHandle.Height.Set(38f, 0f);
            dragHandle.OnLeftMouseDown += (evt, el) => {
                isDragging = true;
                dragOffset = Main.MouseScreen - new Vector2(panel.Left.Pixels, panel.Top.Pixels);
            };
            dragHandle.OnLeftMouseUp += (evt, el) => isDragging = false;
            panel.Append(dragHandle);
        }

        // ---------------------------------------------------------------
        //  Header: title, accent underline, close button
        // ---------------------------------------------------------------
        private void BuildHeader()
        {
            UIText title = new UIText("Debug Damage Tool", 1.05f, false);
            title.Left.Set(16f, 0f);
            title.Top.Set(9f, 0f);
            title.TextColor = Color.White;
            // Biar klik "tembus" ke dragHandle di bawahnya - tanpa ini judul akan
            // nyerok klik duluan dan drag cuma nyala di celah kosong di sekitarnya.
            title.IgnoresMouseInteraction = true;
            panel.Append(title);

            UISolidColor underline = new UISolidColor(ColAccent * 0.55f);
            underline.Left.Set(16f, 0f);
            underline.Top.Set(38f, 0f);
            underline.Width.Set(PanelWidth - 32f, 0f);
            underline.Height.Set(2f, 0f);
            underline.IgnoresMouseInteraction = true;
            panel.Append(underline);

            UIPanel closeButton = new UIPanel();
            closeButton.SetPadding(0);
            closeButton.Width.Set(24f, 0f);
            closeButton.Height.Set(24f, 0f);
            closeButton.Top.Set(8f, 0f);
            closeButton.Left.Set(PanelWidth - 34f, 0f);
            closeButton.BackgroundColor = ColSub * 0.7f;
            closeButton.BorderColor = ColSub;
            closeButton.OnLeftClick += (evt, element) =>
            {
                ModContent.GetInstance<DebugUISystem>().DebugUserInterface.SetState(null);
                isTyping = false;
            };
            closeButton.OnMouseOver += (evt, element) => closeButton.BackgroundColor = ColSubHover;
            closeButton.OnMouseOut += (evt, element) => closeButton.BackgroundColor = ColSub * 0.7f;

            UIText closeText = new UIText("X", 0.75f);
            closeText.HAlign = 0.5f;
            closeText.VAlign = 0.5f;
            closeText.IgnoresMouseInteraction = true;
            closeButton.Append(closeText);
            panel.Append(closeButton);
        }

        // ---------------------------------------------------------------
        //  Damage readout: kotak gede di tengah, angka gedein, warnanya
        //  berubah otomatis makin "bahaya" (ungu -> oranye -> merah pulsing)
        //  sesuai besar nilainya. Klik buat ketik angka pasti.
        // ---------------------------------------------------------------
        private void BuildDamageDisplay()
        {
            readoutPanel = new UIPanel();
            readoutPanel.SetPadding(0);
            readoutPanel.Left.Set(16f, 0f);
            readoutPanel.Top.Set(50f, 0f);
            readoutPanel.Width.Set(PanelWidth - 32f, 0f);
            readoutPanel.Height.Set(56f, 0f);
            readoutPanel.BackgroundColor = ColReadoutIdle;
            readoutPanel.BorderColor = ColAccent * 0.5f;
            readoutPanel.OnLeftClick += (evt, element) =>
            {
                isTyping = !isTyping;
                if (isTyping) Main.clrInput();
            };
            panel.Append(readoutPanel);

            UIText smallLabel = new UIText("RMB DAMAGE", 0.65f);
            smallLabel.Left.Set(12f, 0f);
            smallLabel.Top.Set(6f, 0f);
            smallLabel.TextColor = ColMuted;
            smallLabel.IgnoresMouseInteraction = true;
            readoutPanel.Append(smallLabel);

            valueText = new UIText("1000", 1.3f, false);
            valueText.Left.Set(0f, 0f);
            valueText.Top.Set(20f, 0f);
            valueText.HAlign = 0.5f;
            valueText.TextColor = ColAccent;
            valueText.IgnoresMouseInteraction = true;
            readoutPanel.Append(valueText);

            UIText hint = new UIText("Click the box above to type an exact value", 0.62f);
            hint.Left.Set(0f, 0f);
            hint.Top.Set(110f, 0f);
            hint.HAlign = 0.5f;
            hint.TextColor = ColMuted;
            panel.Append(hint);
        }

        // ---------------------------------------------------------------
        //  Button grid: 4 kolom (+1K/+10K/+100K/+1M lalu -1K/-10K/-100K/Reset)
        //  ditambah satu tombol full-width ONE SHOT di paling bawah.
        // ---------------------------------------------------------------
        private void BuildButtonGrid()
        {
            float row1Top = 130f;
            float row2Top = 174f;
            float oneShotTop = 220f;

            float colWidth = 80f;
            float gap = 6f;
            float col1 = 16f;
            float col2 = col1 + colWidth + gap;
            float col3 = col2 + colWidth + gap;
            float col4 = col3 + colWidth + gap;

            CreateActionButton("+1K", row1Top, col1, colWidth, ColAdd, ColAddHover, (evt, element) => AdjustDamage(1000));
            CreateActionButton("+10K", row1Top, col2, colWidth, ColAdd, ColAddHover, (evt, element) => AdjustDamage(10000));
            CreateActionButton("+100K", row1Top, col3, colWidth, ColAdd, ColAddHover, (evt, element) => AdjustDamage(100000));
            CreateActionButton("+1M", row1Top, col4, colWidth, ColAdd, ColAddHover, (evt, element) => AdjustDamage(1000000));

            CreateActionButton("-1K", row2Top, col1, colWidth, ColSub, ColSubHover, (evt, element) => AdjustDamage(-1000));
            CreateActionButton("-10K", row2Top, col2, colWidth, ColSub, ColSubHover, (evt, element) => AdjustDamage(-10000));
            CreateActionButton("-100K", row2Top, col3, colWidth, ColSub, ColSubHover, (evt, element) => AdjustDamage(-100000));
            CreateActionButton("Reset", row2Top, col4, colWidth, ColReset, ColResetHover, (evt, element) => SetDamage(1000));

            UIPanel oneShotButton = new UIPanel();
            oneShotButton.SetPadding(0);
            oneShotButton.Left.Set(16f, 0f);
            oneShotButton.Top.Set(oneShotTop, 0f);
            oneShotButton.Width.Set(PanelWidth - 32f, 0f);
            oneShotButton.Height.Set(42f, 0f);
            oneShotButton.BackgroundColor = ColOneShot;
            oneShotButton.BorderColor = ColOneShot * 1.4f;
            oneShotButton.OnLeftClick += (evt, element) => SetDamage(OneShotThreshold);
            oneShotButton.OnMouseOver += (evt, element) => oneShotButton.BackgroundColor = ColOneShotHover;
            oneShotButton.OnMouseOut += (evt, element) => oneShotButton.BackgroundColor = ColOneShot;

            UIText oneShotText = new UIText("⚡ ONE SHOT", 0.85f, false);
            oneShotText.HAlign = 0.5f;
            oneShotText.VAlign = 0.5f;
            oneShotText.TextColor = Color.White;
            oneShotText.IgnoresMouseInteraction = true;
            oneShotButton.Append(oneShotText);

            panel.Append(oneShotButton);
        }

        private void CreateActionButton(string text, float top, float left, float width, Color baseColor, Color hoverColor, UIElement.MouseEvent onClick)
        {
            UIPanel button = new UIPanel();
            button.SetPadding(0);
            button.Width.Set(width, 0f);
            button.Height.Set(34f, 0f);
            button.Top.Set(top, 0f);
            button.Left.Set(left, 0f);
            button.BackgroundColor = baseColor;
            button.BorderColor = baseColor * 1.4f;
            button.OnLeftClick += onClick;
            button.OnMouseOver += (evt, element) => button.BackgroundColor = hoverColor;
            button.OnMouseOut += (evt, element) => button.BackgroundColor = baseColor;

            UIText btnText = new UIText(text, 0.68f);
            btnText.HAlign = 0.5f;
            btnText.VAlign = 0.5f;
            btnText.TextColor = Color.White;
            btnText.IgnoresMouseInteraction = true;
            button.Append(btnText);

            panel.Append(button);
        }

        // ---------------------------------------------------------------
        //  Damage adjustment helpers
        // ---------------------------------------------------------------
        private void AdjustDamage(int amount)
        {
            if (isTyping) return;

            var player = Main.LocalPlayer.GetModPlayer<DebugPlayer>();
            player.RmbDamage += amount;
            if (player.RmbDamage < 0) player.RmbDamage = 0;

            Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
        }

        private void SetDamage(int amount)
        {
            if (isTyping) return;

            var player = Main.LocalPlayer.GetModPlayer<DebugPlayer>();
            player.RmbDamage = amount;

            Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
        }

        // ---------------------------------------------------------------
        //  Update: drag movement, warna dinamis readout, dan mode typing
        //  buat masukin angka pasti.
        // ---------------------------------------------------------------
        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (panel.ContainsPoint(Main.MouseScreen)) {
                Main.LocalPlayer.mouseInterface = true;
            }

            if (isDragging) {
                panel.Left.Set(Main.MouseScreen.X - dragOffset.X, 0f);
                panel.Top.Set(Main.MouseScreen.Y - dragOffset.Y, 0f);
                panel.Recalculate();
            }
            if (!Main.mouseLeft) {
                isDragging = false;
            }

            if (valueText == null || Main.LocalPlayer == null) return;

            var player = Main.LocalPlayer.GetModPlayer<DebugPlayer>();

            // ==================== WARNA DINAMIS SESUAI TIER BAHAYA ====================
            bool isOneShotTier = player.RmbDamage >= OneShotThreshold;
            Color tierColor = isOneShotTier ? ColDanger
                             : player.RmbDamage >= 100000 ? ColWarn
                             : ColAccent;

            readoutPanel.BorderColor = isOneShotTier
                ? tierColor * (0.7f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 6f) * 0.3f)
                : ColAccent * 0.5f;
            readoutPanel.BackgroundColor = isTyping ? ColReadoutTyping : ColReadoutIdle;
            valueText.TextColor = tierColor;

            // ==================== MODE TYPING (klik kotak buat masukin angka pasti) ====================
            if (isTyping)
            {
                Terraria.GameInput.PlayerInput.WritingText = true;
                string currentDamageStr = player.RmbDamage.ToString();

                foreach (Keys key in Enum.GetValues(typeof(Keys)))
                {
                    if (Main.keyState.IsKeyDown(key) && Main.oldKeyState.IsKeyUp(key))
                    {
                        if (key == Keys.Back)
                        {
                            if (currentDamageStr.Length > 0)
                            {
                                currentDamageStr = currentDamageStr.Substring(0, currentDamageStr.Length - 1);
                                if (string.IsNullOrEmpty(currentDamageStr))
                                {
                                    player.RmbDamage = 0;
                                }
                                else if (long.TryParse(currentDamageStr, out long parsedBack))
                                {
                                    player.RmbDamage = (int)Math.Min(parsedBack, int.MaxValue);
                                }
                            }
                        }
                        else if (key == Keys.Escape || key == Keys.Enter)
                        {
                            isTyping = false;
                            break;
                        }
                        else
                        {
                            string keyStr = key.ToString();
                            char digit = '\0';

                            if (keyStr.Length == 2 && keyStr.StartsWith("D") && char.IsDigit(keyStr[1]))
                            {
                                digit = keyStr[1];
                            }
                            else if (keyStr.StartsWith("NumPad") && keyStr.Length == 7 && char.IsDigit(keyStr[6]))
                            {
                                digit = keyStr[6];
                            }

                            if (digit != '\0')
                            {
                                currentDamageStr = player.RmbDamage == 0 ? digit.ToString() : currentDamageStr + digit;

                                if (long.TryParse(currentDamageStr, out long parsed))
                                {
                                    player.RmbDamage = (int)Math.Min(parsed, int.MaxValue);
                                }
                            }
                        }
                    }
                }

                cursorTimer++;
                string blinkingCursor = (cursorTimer % 40 < 20) ? "|" : "";
                valueText.SetText($"{player.RmbDamage}{blinkingCursor}");
            }
            else
            {
                valueText.SetText($"{player.RmbDamage}");
            }
        }
    }
}
