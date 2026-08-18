using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using Terraria.ModLoader;
using ReLogic.Content;

namespace TheSanity.Buff
{
    public class DebuffSelectorUI : UIState
    {
        // ================== PALET WARNA (biar konsisten & gampang di-tweak sekali tempat) ==================
        private static readonly Color ColPanelBG     = new Color(21, 24, 36, 248);
        private static readonly Color ColAccent      = new Color(150, 120, 235);   // ungu, aksen utama
        private static readonly Color ColTabActive   = new Color(150, 120, 235) * 0.85f;
        private static readonly Color ColTabInactive = new Color(38, 42, 58) * 0.95f;
        private static readonly Color ColTabHover    = new Color(58, 62, 84) * 0.95f;
        private static readonly Color ColListBG      = new Color(13, 15, 22) * 0.9f;
        private static readonly Color ColItemBG      = new Color(45, 49, 68) * 0.55f;
        private static readonly Color ColItemHover   = new Color(84, 68, 140) * 0.7f;
        private static readonly Color ColItemSelected = new Color(76, 209, 145) * 0.5f;
        private static readonly Color ColSearchIdle   = new Color(30, 33, 46) * 0.9f;
        private static readonly Color ColSearchActive = new Color(60, 55, 92) * 0.95f;
        private static readonly Color ColOn  = new Color(76, 209, 145);  // hijau mint
        private static readonly Color ColOff = new Color(214, 82, 82);   // merah

        // 🔧 FIX: UIPanel SELALU nggambar tekstur border 9-slice bawaan vanilla, walau BorderColor
        // di-set Transparent. Di elemen setipis 2-3px, tekstur itu ke-stretch jadi kotak gembung
        // aneh (bukan garis tipis). Makanya buat garis aksen tipis, kita gambar warna solid manual
        // lewat MagicPixel (tekstur 1x1 putih polos), gak lewat UIPanel sama sekali.
        private class UISolidColor : UIElement
        {
            private readonly Color color;
            public UISolidColor(Color color) { this.color = color; }
            protected override void DrawSelf(SpriteBatch spriteBatch) {
                CalculatedStyle dims = GetDimensions();
                spriteBatch.Draw(TextureAssets.MagicPixel.Value, dims.ToRectangle(), color);
            }
        }

        // ✨ Toggle switch "chip" generik (dipakai untuk Contact Damage / Friendly Mode / Show DPS)
        // supaya nggak perlu copy-paste blok UI toggle 3x sendiri-sendiri.
        private class ToggleSwitch
        {
            public UIPanel Row;
            public UIText SubLabel;
            public UIPanel Track;
            public UIPanel Knob;
            public Func<bool> GetState;
            public string OnText;
            public string OffText;

            public void Refresh() {
                bool on = GetState();
                Color stateColor = on ? ColOn : ColOff;

                Row.BackgroundColor = stateColor * 0.35f;
                Row.BorderColor = stateColor * 0.7f;
                SubLabel.SetText(on ? OnText : OffText);
                SubLabel.TextColor = stateColor;

                Track.BackgroundColor = stateColor * 0.6f;
                Knob.Left.Set(on ? 30f : 2f, 0f);
            }
        }

        private UIPanel panel;
        private UIList debuffList;
        private UIScrollbar scrollbar;
        private UITextPanel<string> searchButton;

        private UIPanel tabDebuffButton;
        private UIPanel tabEnemyButton;
        private UIPanel tabBossButton;
        private UISolidColor tabIndicator; // garis aksen kecil yang geser ke tab yang lagi aktif

        // ✨ Readout "Global DPS" - SELALU kelihatan di dalam menu, apapun status toggle
        // "Show DPS (World)". Yang di-toggle cuma tampilan angka di samping Dummy di dunia.
        private UIText dpsReadoutText;

        private readonly List<ToggleSwitch> toggleSwitches = new List<ToggleSwitch>();

        // ✨ Drag state: seret dari area header (title) buat mindahin panel ke mana aja.
        private bool isDragging;
        private Vector2 dragOffset;

        public string searchFilter = "";
        public bool isTyping = false;

        public int SelectedBuffID = 0; 
        public int SelectedNPCID = 0; 
        public int currentTab = 0; 
        
        private int cursorTimer = 0;

        public override void OnInitialize() {
            panel = new UIPanel();
            panel.SetPadding(12);
            panel.Left.Set(Main.screenWidth / 2f - 200f, 0f);
            panel.Top.Set(Main.screenHeight / 2f - 350f, 0f);
            panel.Width.Set(400f, 0f);
            panel.Height.Set(700f, 0f);
            panel.BackgroundColor = ColPanelBG;
            panel.BorderColor = ColAccent * 0.45f;
            Append(panel);

            // ─── DRAG HANDLE (invisible, sits over the ENTIRE title bar) ───
            // Nutupin seluruh lebar header dari kiri sampai sebelum tombol close, jadi
            // panel bisa diseret dari MANA AJA di title bar itu (bukan cuma spot tertentu),
            // tanpa nge-block klik ke close button.
            UIElement dragHandle = new UIElement();
            dragHandle.Left.Set(0f, 0f);
            dragHandle.Top.Set(0f, 0f);
            dragHandle.Width.Set(354f, 0f); // stop tepat sebelum closeButton (Left 358f)
            dragHandle.Height.Set(36f, 0f);
            dragHandle.OnLeftMouseDown += (evt, el) => {
                isDragging = true;
                dragOffset = Main.MouseScreen - new Vector2(panel.Left.Pixels, panel.Top.Pixels);
            };
            dragHandle.OnLeftMouseUp += (evt, el) => isDragging = false;
            panel.Append(dragHandle);

            // ─── HEADER ───
            // 🔧 FIX: font "large" ternyata jauh lebih tinggi dari perkiraan dan nabrak tombol close.
            // Diganti ke scale normal yang lebih gedean dikit (1.15f) biar tetep nonjol tapi muat rapi.
            UIText title = new UIText("Dummy Control Panel", 1.15f, false);
            title.Left.Set(2f, 0f);
            title.Top.Set(6f, 0f);
            // 🔧 FIX DRAG: tanpa ini, teks judul "menyerap" klik mouse duluan (karena
            // digambar di atas dragHandle), jadi drag cuma nyala kalau nge-klik di
            // celah kosong di sekitar teksnya. Dengan IgnoresMouseInteraction = true,
            // klik "tembus" ke dragHandle di bawahnya, jadi bisa diseret dari mana aja.
            title.IgnoresMouseInteraction = true;
            panel.Append(title);

            UITextPanel<string> closeButton = new UITextPanel<string>("X");
            closeButton.SetPadding(0);
            closeButton.Left.Set(358f, 0f);
            closeButton.Top.Set(2f, 0f);
            closeButton.Width.Set(26f, 0f);
            closeButton.Height.Set(26f, 0f);
            closeButton.BackgroundColor = ColOff * 0.55f;
            closeButton.BorderColor = ColOff * 0.9f;
            closeButton.OnLeftClick += (evt, element) => ModContent.GetInstance<DebuffUISystem>().CloseUI();
            closeButton.OnMouseOver += (evt, element) => closeButton.BackgroundColor = ColOff * 0.9f;
            closeButton.OnMouseOut += (evt, element) => closeButton.BackgroundColor = ColOff * 0.55f;
            panel.Append(closeButton);

            UISolidColor headerLine = new UISolidColor(ColAccent * 0.55f);
            headerLine.Left.Set(2f, 0f);
            headerLine.Top.Set(36f, 0f);
            headerLine.Width.Set(340f, 0f);
            headerLine.Height.Set(2f, 0f);
            headerLine.IgnoresMouseInteraction = true; // biar konsisten, ini pun cuma garis visual
            panel.Append(headerLine);

            // ─── GLOBAL DPS READOUT (selalu tampil, terlepas dari toggle Show DPS di dunia) ───
            UIPanel dpsReadoutPanel = new UIPanel();
            dpsReadoutPanel.SetPadding(0);
            dpsReadoutPanel.Left.Set(15f, 0f);
            dpsReadoutPanel.Top.Set(44f, 0f);
            dpsReadoutPanel.Width.Set(365f, 0f);
            dpsReadoutPanel.Height.Set(22f, 0f);
            dpsReadoutPanel.BackgroundColor = ColAccent * 0.18f;
            dpsReadoutPanel.BorderColor = ColAccent * 0.4f;
            panel.Append(dpsReadoutPanel);

            dpsReadoutText = new UIText("Global DPS: 0", 0.8f);
            dpsReadoutText.Left.Set(8f, 0f);
            dpsReadoutText.Top.Set(1f, 0f);
            dpsReadoutText.TextColor = ColAccent;
            dpsReadoutPanel.Append(dpsReadoutText);

            // ─── TABS ───
            tabDebuffButton = CreateTabButton(15f, "TheSanity/GlobalNPC/Environment/BuffyIco", 0);
            tabEnemyButton  = CreateTabButton(140f, "TheSanity/GlobalNPC/Environment/EnemyIco", 1);
            tabBossButton   = CreateTabButton(265f, "TheSanity/GlobalNPC/Environment/BossyIco", 2);

            tabIndicator = new UISolidColor(ColAccent);
            tabIndicator.Left.Set(15f, 0f);
            tabIndicator.Top.Set(113f, 0f);
            tabIndicator.Width.Set(115f, 0f);
            tabIndicator.Height.Set(3f, 0f);
            panel.Append(tabIndicator);

            // ─── SEARCH BAR ───
            searchButton = new UITextPanel<string>("Search: [click to type...]");
            searchButton.Left.Set(15f, 0f);
            searchButton.Top.Set(124f, 0f);
            searchButton.Width.Set(365f, 0f);
            searchButton.Height.Set(35f, 0f);
            searchButton.BackgroundColor = ColSearchIdle;
            searchButton.BorderColor = ColAccent * 0.3f;
            searchButton.OnLeftClick += (evt, element) => {
                isTyping = !isTyping;
                if (isTyping) {
                    searchButton.BackgroundColor = ColSearchActive;
                    searchButton.BorderColor = ColAccent;
                    Main.clrInput();
                } else {
                    searchButton.BackgroundColor = ColSearchIdle;
                    searchButton.BorderColor = ColAccent * 0.3f;
                }
            };
            panel.Append(searchButton);

            // ─── LIST CONTAINER ───
            UIPanel listPanel = new UIPanel();
            listPanel.SetPadding(6);
            listPanel.Left.Set(15f, 0f);
            listPanel.Top.Set(169f, 0f);
            listPanel.Width.Set(345f, 0f);
            listPanel.Height.Set(360f, 0f);
            listPanel.BackgroundColor = ColListBG;
            listPanel.BorderColor = ColAccent * 0.25f;
            panel.Append(listPanel);

            debuffList = new UIList();
            debuffList.Width.Set(325f, 0f);
            debuffList.Height.Set(348f, 0f);
            debuffList.ListPadding = 6f;
            listPanel.Append(debuffList);

            scrollbar = new UIScrollbar();
            scrollbar.Left.Set(365f, 0f);
            scrollbar.Top.Set(169f, 0f);
            scrollbar.Width.Set(15f, 0f);
            scrollbar.Height.Set(360f, 0f);
            panel.Append(scrollbar);
            debuffList.SetScrollbar(scrollbar);

            // ─── STATUS TOGGLES (toggle switch model, bukan cuma tombol teks) ───
            CreateToggleSwitch(
                549f, "Contact Damage", "ON - Hurts Player", "OFF - Safe Mode",
                () => DebuffUISystem.ContactDamageEnabled,
                () => DebuffUISystem.ContactDamageEnabled = !DebuffUISystem.ContactDamageEnabled);

            CreateToggleSwitch(
                597f, "Friendly Mode", "ON - Takes Hostile Dmg", "OFF - Hostile Enemy",
                () => DebuffUISystem.FriendlyModeEnabled,
                () => DebuffUISystem.FriendlyModeEnabled = !DebuffUISystem.FriendlyModeEnabled);

            CreateToggleSwitch(
                645f, "Show DPS (World)", "ON - Visible by Dummy", "OFF - Menu Only",
                () => DpsTracker.ShowWorldDps,
                () => DpsTracker.ShowWorldDps = !DpsTracker.ShowWorldDps);
        }

        private UIPanel CreateTabButton(float left, string iconPath, int tabIndex) {
            UIPanel tab = new UIPanel();
            tab.Left.Set(left, 0f);
            tab.Top.Set(70f, 0f);
            tab.Width.Set(115f, 0f);
            tab.Height.Set(40f, 0f);
            tab.SetPadding(0);
            tab.BorderColor = Color.Transparent;
            tab.OnLeftClick += (evt, element) => { currentTab = tabIndex; searchFilter = ""; PopulateList(); };
            tab.OnMouseOver += (evt, element) => { if (currentTab != tabIndex) tab.BackgroundColor = ColTabHover; };
            tab.OnMouseOut += (evt, element) => { if (currentTab != tabIndex) tab.BackgroundColor = ColTabInactive; };

            UIImage icon = new UIImage(ModContent.Request<Texture2D>(iconPath));
            icon.Left.Set(41f, 0f);
            icon.Top.Set(4f, 0f);
            tab.Append(icon);

            panel.Append(tab);
            return tab;
        }

        /// <summary>
        /// Builds one full toggle-switch row (label + sub-label + track + knob),
        /// wires up the click handler, and registers it so <see cref="Update"/>
        /// keeps its visuals in sync every frame automatically.
        /// </summary>
        private ToggleSwitch CreateToggleSwitch(float top, string title, string onText, string offText, Func<bool> getState, Action toggleAction) {
            var row = new UIPanel();
            row.SetPadding(0);
            row.Left.Set(15f, 0f);
            row.Top.Set(top, 0f);
            row.Width.Set(365f, 0f);
            row.Height.Set(40f, 0f);
            row.BackgroundColor = ColOff * 0.35f;
            row.BorderColor = ColOff * 0.7f;
            panel.Append(row);

            UIText label = new UIText(title);
            label.Left.Set(12f, 0f);
            label.Top.Set(4f, 0f);
            row.Append(label);

            UIText subLabel = new UIText(offText, 0.75f);
            subLabel.Left.Set(12f, 0f);
            subLabel.Top.Set(22f, 0f);
            row.Append(subLabel);

            UIPanel track = new UIPanel();
            track.SetPadding(0);
            track.Left.Set(305f, 0f);
            track.Top.Set(10f, 0f);
            track.Width.Set(48f, 0f);
            track.Height.Set(20f, 0f);
            track.BackgroundColor = Color.Black * 0.5f;
            track.BorderColor = Color.White * 0.3f;
            row.Append(track);

            UIPanel knob = new UIPanel();
            knob.SetPadding(0);
            knob.Left.Set(2f, 0f);
            knob.Top.Set(2f, 0f);
            knob.Width.Set(16f, 0f);
            knob.Height.Set(16f, 0f);
            knob.BackgroundColor = Color.White;
            knob.BorderColor = Color.Transparent;
            track.Append(knob);

            var toggle = new ToggleSwitch {
                Row = row,
                SubLabel = subLabel,
                Track = track,
                Knob = knob,
                GetState = getState,
                OnText = onText,
                OffText = offText,
            };

            row.OnLeftClick += (evt, element) => {
                toggleAction();
                Terraria.Audio.SoundEngine.PlaySound(SoundID.MenuTick);
                toggle.Refresh();
            };
            row.OnMouseOver += (evt, element) => row.BorderColor = Color.White * 0.6f;
            row.OnMouseOut += (evt, element) => toggle.Refresh();

            toggle.Refresh();
            toggleSwitches.Add(toggle);
            return toggle;
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);

            // Lock world mouse interaction while hovering the panel so clicks
            // don't leak through to the game world.
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

            tabDebuffButton.BackgroundColor = (currentTab == 0) ? ColTabActive : ColTabInactive;
            tabEnemyButton.BackgroundColor  = (currentTab == 1) ? ColTabActive : ColTabInactive;
            tabBossButton.BackgroundColor   = (currentTab == 2) ? ColTabActive : ColTabInactive;

            tabIndicator.Left.Set(15f + (currentTab * 125f), 0f);

            // Global DPS readout: selalu update, terlepas dari toggle "Show DPS (World)".
            dpsReadoutText.SetText($"Global DPS: {DpsTracker.CurrentDps}");

            foreach (ToggleSwitch toggle in toggleSwitches) {
                toggle.Refresh();
            }

            if (isTyping) {
                Terraria.GameInput.PlayerInput.WritingText = true;
                foreach (Keys key in Enum.GetValues(typeof(Keys))) {
                    if (Main.keyState.IsKeyDown(key) && Main.oldKeyState.IsKeyUp(key)) {
                        if (key == Keys.Back) {
                            if (searchFilter.Length > 0) {
                                searchFilter = searchFilter.Substring(0, searchFilter.Length - 1);
                                PopulateList();
                            }
                        }
                        else if (key == Keys.Escape || key == Keys.Enter) {
                            ResetTypingState();
                            break;
                        }
                        else if (key == Keys.Space) {
                            searchFilter += " ";
                            PopulateList();
                        }
                        else {
                            string keyStr = key.ToString();
                            if (keyStr.Length == 1 && char.IsLetter(keyStr[0])) {
                                bool shift = Main.keyState.IsKeyDown(Keys.LeftShift) || Main.keyState.IsKeyDown(Keys.RightShift);
                                char c = keyStr[0];
                                if (!shift) c = char.ToLower(c);
                                searchFilter += c;
                                PopulateList();
                            }
                            else if (keyStr.Length == 2 && keyStr.StartsWith("D") && char.IsDigit(keyStr[1])) {
                                searchFilter += keyStr[1];
                                PopulateList();
                            }
                            else if (keyStr.StartsWith("NumPad") && keyStr.Length == 7 && char.IsDigit(keyStr[6])) {
                                searchFilter += keyStr[6];
                                PopulateList();
                            }
                        }
                    }
                }
                cursorTimer++;
                string blinkingCursor = (cursorTimer % 40 < 20) ? "|" : "";
                searchButton.SetText("Search: " + searchFilter + blinkingCursor);
            } else {
                searchButton.SetText(searchFilter == "" ? "Search: [click to type...]" : "Search: " + searchFilter);
            }
        }

        public void ResetTypingState() {
            isTyping = false;
            searchButton.BackgroundColor = ColSearchIdle;
            searchButton.BorderColor = ColAccent * 0.3f;
        }

        public void PopulateList() {
            if (debuffList == null) return;
            debuffList.Clear();

            string filter = searchFilter.ToLower();

            if (currentTab == 0) {
                AddListItem(0, "No Contact Debuff (Reset)", true);
                for (int i = 1; i < BuffLoader.BuffCount; i++) {
                    if (Main.debuff[i]) {
                        string name = Lang.GetBuffName(i);
                        if (!string.IsNullOrEmpty(name) && (filter == "" || name.ToLower().Contains(filter))) {
                            AddListItem(i, name, true);
                        }
                    }
                }
            }
            else if (currentTab == 1) {
                AddListItem(0, "No Stat Mimic (Reset Dummy)", false);
                for (int i = 1; i < NPCLoader.NPCCount; i++) {
                    NPC npc = new NPC();
                    npc.SetDefaults(i);
                    bool isRegularEnemy = !npc.boss && !npc.townNPC && npc.damage > 0 && NPCID.Sets.BossHeadTextures[i] == -1;
                    if (isRegularEnemy) {
                        string name = Lang.GetNPCName(i).Value;
                        if (!string.IsNullOrEmpty(name) && (filter == "" || name.ToLower().Contains(filter))) {
                            AddListItem(i, name, false);
                        }
                    }
                }
            }
            else if (currentTab == 2) {
                AddListItem(0, "No Stat Mimic (Reset Dummy)", false);
                for (int i = 1; i < NPCLoader.NPCCount; i++) {
                    NPC npc = new NPC();
                    npc.SetDefaults(i);
                    bool isBoss = npc.boss || NPCID.Sets.BossHeadTextures[i] != -1;
                    if (isBoss && !npc.townNPC) {
                        string name = Lang.GetNPCName(i).Value;
                        if (!string.IsNullOrEmpty(name) && (filter == "" || name.ToLower().Contains(filter))) {
                            AddListItem(i, name, false);
                        }
                    }
                }
            }
        }

        private void AddListItem(int id, string name, bool isDebuffType) {
            UIPanel itemPanel = new UIPanel();
            itemPanel.Width.Set(315f, 0f);
            itemPanel.Height.Set(42f, 0f);
            itemPanel.SetPadding(0);
            itemPanel.BorderColor = Color.Transparent;

            bool isSelected = isDebuffType ? (SelectedBuffID == id) : (SelectedNPCID == id);
            itemPanel.BackgroundColor = isSelected ? ColItemSelected : ColItemBG;
            if (isSelected) itemPanel.BorderColor = ColOn * 0.7f;

            float textLeftOffset = 12f; 

            if (id > 0) {
                Asset<Texture2D> textureAsset = null;
                if (isDebuffType) {
                    textureAsset = TextureAssets.Buff[id];
                } 
                else if (currentTab == 2) {
                    int headIndex = NPCID.Sets.BossHeadTextures[id];
                    if (headIndex != -1 && headIndex < TextureAssets.NpcHeadBoss.Length) {
                        textureAsset = TextureAssets.NpcHeadBoss[headIndex];
                    } else {
                        textureAsset = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Environment/BossyIco");
                    }
                }

                if (textureAsset != null) {
                    UIPanel iconBackdrop = new UIPanel();
                    iconBackdrop.SetPadding(0);
                    iconBackdrop.Left.Set(6f, 0f);
                    iconBackdrop.Top.Set(5f, 0f);
                    iconBackdrop.Width.Set(32f, 0f);
                    iconBackdrop.Height.Set(32f, 0f);
                    iconBackdrop.BackgroundColor = Color.Black * 0.35f;
                    iconBackdrop.BorderColor = Color.Transparent;
                    itemPanel.Append(iconBackdrop);

                    UIImage icon = new UIImage(textureAsset);
                    icon.Left.Set(6f, 0f);
                    icon.Top.Set(5f, 0f);
                    icon.Width.Set(32f, 0f);
                    icon.Height.Set(32f, 0f);
                    itemPanel.Append(icon);
                    textLeftOffset = 48f;
                }
            }

            UIText text = new UIText(name);
            text.Left.Set(textLeftOffset, 0f); 
            text.Top.Set(11f, 0f);
            itemPanel.Append(text);

            itemPanel.OnLeftClick += (evt, element) => {
                if (isDebuffType) {
                    SelectedBuffID = id;
                    Main.NewText($"[Target Contact Debuff] Set to: {name}", Color.GreenYellow);
                } else {
                    SelectedNPCID = id;
                    Main.NewText($"[Target Stat Mimic] Set to: {name}", Color.Cyan);
                }
                PopulateList();
            };

            itemPanel.OnMouseOver += (evt, element) => { if (!isSelected) itemPanel.BackgroundColor = ColItemHover; };
            itemPanel.OnMouseOut += (evt, element) => { if (!isSelected) itemPanel.BackgroundColor = ColItemBG; };

            debuffList.Add(itemPanel);
        }
    }
}
