using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input; // REQUIRED: Untuk menangani polling Main.keyState & Main.oldKeyState
using System;
using Terraria;
using Terraria.ID;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;
using Terraria.ModLoader;
using ReLogic.Content; // Namespace yang benar untuk Asset<>

namespace TheSanity.Buff
{
    public class DebuffSelectorUI : UIState
    {
        private UIPanel panel;
        private UIList debuffList;
        private UIScrollbar scrollbar;
        private UITextPanel<string> searchButton;

        // Tab Categories Panels
        private UIPanel tabDebuffButton;
        private UIPanel tabEnemyButton;
        private UIPanel tabBossButton;
        
        public string searchFilter = "";
        public bool isTyping = false;
        
        // Active selections
        public int SelectedBuffID = 0; 
        public int SelectedNPCID = 0; 
        public int currentTab = 0; // 0 = Debuffs, 1 = Enemies, 2 = Bosses
        
        private int cursorTimer = 0;

        public override void OnInitialize() {
            // Main Background Panel
            panel = new UIPanel();
            panel.SetPadding(10);
            panel.Left.Set(Main.screenWidth / 2f - 200f, 0f);
            panel.Top.Set(Main.screenHeight / 2f - 270f, 0f);
            panel.Width.Set(400f, 0f);
            panel.Height.Set(540f, 0f);
            panel.BackgroundColor = new Color(33, 43, 73, 240); 
            Append(panel);

            // GUI Title
            UIText title = new UIText(" Dummy Control ", 1f, true);
            title.Left.Set(10f, 0f);
            title.Top.Set(10f, 0f);
            panel.Append(title);

            // Close Button (X)
            UITextPanel<string> closeButton = new UITextPanel<string>("X");
            closeButton.SetPadding(5);
            closeButton.Left.Set(350f, 0f);
            closeButton.Top.Set(5f, 0f);
            closeButton.Width.Set(30f, 0f);
            closeButton.Height.Set(30f, 0f);
            closeButton.BackgroundColor = Color.Red * 0.7f;
            closeButton.OnLeftClick += (evt, element) => ModContent.GetInstance<DebuffUISystem>().CloseUI();
            panel.Append(closeButton);

            // ==================== TABS SYSTEM INITIALIZATION ====================
            
            // Tab 1: Debuffs (Menggunakan Kustom Icon BuffyIco)
            tabDebuffButton = new UIPanel();
            tabDebuffButton.Left.Set(15f, 0f);
            tabDebuffButton.Top.Set(45f, 0f);
            tabDebuffButton.Width.Set(115f, 0f);
            tabDebuffButton.Height.Set(40f, 0f);
            tabDebuffButton.SetPadding(0);
            tabDebuffButton.OnLeftClick += (evt, element) => { currentTab = 0; searchFilter = ""; PopulateList(); };
            panel.Append(tabDebuffButton);

            // FIX: Mengambil tekstur lokal BuffyIco
            UIImage debuffTabIcon = new UIImage(ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Environment/BuffyIco"));
            debuffTabIcon.Left.Set(41f, 0f);
            debuffTabIcon.Top.Set(4f, 0f);
            tabDebuffButton.Append(debuffTabIcon);

            // Tab 2: Regular Enemies (Menggunakan Kustom Icon EnemyIco)
            tabEnemyButton = new UIPanel();
            tabEnemyButton.Left.Set(140f, 0f);
            tabEnemyButton.Top.Set(45f, 0f);
            tabEnemyButton.Width.Set(115f, 0f);
            tabEnemyButton.Height.Set(40f, 0f);
            tabEnemyButton.SetPadding(0);
            tabEnemyButton.OnLeftClick += (evt, element) => { currentTab = 1; searchFilter = ""; PopulateList(); };
            panel.Append(tabEnemyButton);

            // FIX: Mengambil tekstur lokal EnemyIco
            UIImage enemyTabIcon = new UIImage(ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Environment/EnemyIco"));
            enemyTabIcon.Left.Set(41f, 0f);
            enemyTabIcon.Top.Set(4f, 0f);
            tabEnemyButton.Append(enemyTabIcon);

            // Tab 3: Bosses (Menggunakan Kustom Icon BossyIco)
            tabBossButton = new UIPanel();
            tabBossButton.Left.Set(265f, 0f);
            tabBossButton.Top.Set(45f, 0f);
            tabBossButton.Width.Set(115f, 0f);
            tabBossButton.Height.Set(40f, 0f);
            tabBossButton.SetPadding(0);
            tabBossButton.OnLeftClick += (evt, element) => { currentTab = 2; searchFilter = ""; PopulateList(); };
            panel.Append(tabBossButton);

            // FIX: Mengambil tekstur lokal BossyIco
            UIImage bossTabIcon = new UIImage(ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Environment/BossyIco"));
            bossTabIcon.Left.Set(41f, 0f);
            bossTabIcon.Top.Set(4f, 0f);
            tabBossButton.Append(bossTabIcon);

            // ====================================================================

            // Search Bar Input Box
            searchButton = new UITextPanel<string>("Search: [Click here to type...]");
            searchButton.Left.Set(15f, 0f);
            searchButton.Top.Set(95f, 0f);
            searchButton.Width.Set(365f, 0f);
            searchButton.Height.Set(35f, 0f);
            searchButton.BackgroundColor = Color.DarkSlateGray * 0.8f;
            searchButton.OnLeftClick += (evt, element) => {
                isTyping = !isTyping;
                if (isTyping) {
                    searchButton.BackgroundColor = Color.SlateGray;
                    Main.clrInput();
                } else {
                    searchButton.BackgroundColor = Color.DarkSlateGray * 0.8f;
                }
            };
            panel.Append(searchButton);

            // List container panel
            UIPanel listPanel = new UIPanel();
            listPanel.Left.Set(15f, 0f);
            listPanel.Top.Set(140f, 0f);
            listPanel.Width.Set(345f, 0f);
            listPanel.Height.Set(380f, 0f);
            listPanel.BackgroundColor = Color.Black * 0.4f;
            panel.Append(listPanel);

            // UIList for dynamic display
            debuffList = new UIList();
            debuffList.Width.Set(325f, 0f);
            debuffList.Height.Set(370f, 0f);
            debuffList.ListPadding = 5f;
            listPanel.Append(debuffList);

            // Scrollbar
            scrollbar = new UIScrollbar();
            scrollbar.Left.Set(365f, 0f);
            scrollbar.Top.Set(140f, 0f);
            scrollbar.Width.Set(15f, 0f);
            scrollbar.Height.Set(380f, 0f);
            panel.Append(scrollbar);
            debuffList.SetScrollbar(scrollbar);
        }

        public override void Update(GameTime gameTime) {
            base.Update(gameTime);

            // Keep the GUI centered dynamically
            panel.Left.Set(Main.screenWidth / 2f - 200f, 0f);
            panel.Top.Set(Main.screenHeight / 2f - 270f, 0f);

            // Dynamic color highlights for active tabs
            tabDebuffButton.BackgroundColor = (currentTab == 0) ? Color.Goldenrod * 0.8f : Color.DarkSlateGray * 0.5f;
            tabEnemyButton.BackgroundColor = (currentTab == 1) ? Color.Goldenrod * 0.8f : Color.DarkSlateGray * 0.5f;
            tabBossButton.BackgroundColor = (currentTab == 2) ? Color.Goldenrod * 0.8f : Color.DarkSlateGray * 0.5f;

            if (isTyping) {
                Terraria.GameInput.PlayerInput.WritingText = true;

                foreach (Keys key in Enum.GetValues(typeof(Keys))) {
                    if (Main.keyState.IsKeyDown(key) && Main.oldKeyState.IsKeyUp(key)) {
                        
                        // Handle Backspace
                        if (key == Keys.Back) {
                            if (searchFilter.Length > 0) {
                                searchFilter = searchFilter.Substring(0, searchFilter.Length - 1);
                                PopulateList();
                            }
                        }
                        // Handle Escape & Enter to close typing focus
                        else if (key == Keys.Escape || key == Keys.Enter) {
                            ResetTypingState();
                            break;
                        }
                        // Handle Spacebar
                        else if (key == Keys.Space) {
                            searchFilter += " ";
                            PopulateList();
                        }
                        // Handle Alphabetical & Numerical inputs
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
                searchButton.SetText(searchFilter == "" ? "Search: [Click here to type...]" : "Search: " + searchFilter);
            }
        }

        public void ResetTypingState() {
            isTyping = false;
            searchButton.BackgroundColor = Color.DarkSlateGray * 0.8f;
        }

        public void PopulateList() {
            if (debuffList == null) return;
            debuffList.Clear();

            string filter = searchFilter.ToLower();

            // TAB 0: DEBUFFS MODE
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
            // TAB 1: REGULAR ENEMIES MODE (Murni Text)
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
            // TAB 2: BOSSES MODE (Ada Ikon Minimap Keren!)
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
            itemPanel.Height.Set(40f, 0f);
            itemPanel.SetPadding(0);

            bool isSelected = isDebuffType ? (SelectedBuffID == id) : (SelectedNPCID == id);
            itemPanel.BackgroundColor = isSelected ? Color.LimeGreen * 0.6f : Color.Indigo * 0.4f;

            float textLeftOffset = 10f; 

            if (id > 0) {
                Asset<Texture2D> textureAsset = null;

                // 1. Jika tipenya Debuff, panggil icon buff aslinya
                if (isDebuffType) {
                    textureAsset = TextureAssets.Buff[id];
                } 
                // 2. Jika tipenya NPC/Boss, dan posisi tab sedang aktif di BOSS (Tab 2)
                else if (currentTab == 2) {
                    int headIndex = NPCID.Sets.BossHeadTextures[id];
                    // Gunakan Boss Head Map Texture agar ukuran icon pas & presisi (tidak bikin UI melar)
                    if (headIndex != -1 && headIndex < TextureAssets.NpcHeadBoss.Length) {
                        textureAsset = TextureAssets.NpcHeadBoss[headIndex];
                    } else {
                        // FIX: Fallback diganti ke BossyIco lokal biar ga pakai Suspicious Looking Eye vanilla lagi
                        textureAsset = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Environment/BossyIco");
                    }
                }

                // Append Icon jika asetnya valid
                if (textureAsset != null) {
                    UIImage icon = new UIImage(textureAsset);
                    icon.Left.Set(5f, 0f);
                    icon.Top.Set(4f, 0f);
                    icon.Width.Set(32f, 0f);
                    icon.Height.Set(32f, 0f);
                    itemPanel.Append(icon);
                    textLeftOffset = 45f; // Geser teks agar tidak menabrak ikon
                }
            }

            UIText text = new UIText(name);
            text.Left.Set(textLeftOffset, 0f); 
            text.Top.Set(10f, 0f);
            itemPanel.Append(text);

            // Handle Clicks
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

            itemPanel.OnMouseOver += (evt, element) => { if (!isSelected) itemPanel.BackgroundColor = Color.Indigo * 0.8f; };
            itemPanel.OnMouseOut += (evt, element) => { if (!isSelected) itemPanel.BackgroundColor = Color.Indigo * 0.4f; };

            debuffList.Add(itemPanel);
        }
    }
}