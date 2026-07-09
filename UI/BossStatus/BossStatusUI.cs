using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Terraria.UI;
using Terraria.UI.Chat;

namespace TheSanity.Interface
{
    // ===================================================================================
    // 💡 BOSS BALANCING & METRIC LOG LOCATION GUIDE
    // ===================================================================================
    // If you want to balance your custom boss damage/speed, look at these variable targets:
    // - BossHPPercent    : Tracks how low the boss HP reached (Lower % = Player deals more DPS).
    // - SlowdownPercent  : Tracks game lag during boss attacks (Target < 5.0% for smooth performance).
    // - TotalDamage      : Total output power registered from weapons.
    // ===================================================================================

    public class PlayerBossRecord
    {
        public string BossName;
        public int NpcType;
        public int AttemptNumber;
        public string DurationStr;
        public int DurationTicks;
        public int TotalHits; 
        public int TotalDamage;
        public string BestWeapon;
        
        public List<string> WeaponNames = new List<string>();
        public List<int> WeaponDamages = new List<int>();

        public int BossHPPercent;
        public double SlowdownPercent;
        public double AvgFPS;
        public string RtaStr;
        
        public List<string> PlayerRankNames = new List<string>();
        public List<int> PlayerRankDamages = new List<int>();
        public List<int> PlayerRankHitsReceived = new List<int>();
        public List<int> PlayerRankHitsDealt = new List<int>();
        public List<int> PlayerRankDeaths = new List<int>();
        public int GlobalDebuffTrapDamage;
        public string DeathReason = "None";
        public bool IsPinned = false;
        public string WorldName = "";

        public TagCompound Save() {
            return new TagCompound {
                ["BossName"] = BossName,
                ["NpcType"] = NpcType,
                ["AttemptNumber"] = AttemptNumber,
                ["DurationStr"] = DurationStr,
                ["DurationTicks"] = DurationTicks,
                ["TotalHits"] = TotalHits,
                ["TotalDamage"] = TotalDamage,
                ["BestWeapon"] = BestWeapon,
                ["WeaponNames"] = WeaponNames,
                ["WeaponDamages"] = WeaponDamages,
                ["BossHPPercent"] = BossHPPercent,
                ["SlowdownPercent"] = SlowdownPercent,
                ["AvgFPS"] = AvgFPS,
                ["RtaStr"] = RtaStr,
                ["PlayerRankNames"] = PlayerRankNames,
                ["PlayerRankDamages"] = PlayerRankDamages,
                ["PlayerRankHitsReceived"] = PlayerRankHitsReceived,
                ["PlayerRankHitsDealt"] = PlayerRankHitsDealt,
                ["PlayerRankDeaths"] = PlayerRankDeaths,
                ["GlobalDebuffTrapDamage"] = GlobalDebuffTrapDamage,
                ["DeathReason"] = DeathReason,
                ["IsPinned"] = IsPinned,
                ["WorldName"] = WorldName
            };
        }

        public static PlayerBossRecord Load(TagCompound tag) {
            var record = new PlayerBossRecord {
                BossName = tag.GetString("BossName"),
                NpcType = tag.GetInt("NpcType"),
                AttemptNumber = tag.GetInt("AttemptNumber"),
                DurationStr = tag.GetString("DurationStr"),
                DurationTicks = tag.GetInt("DurationTicks"),
                TotalHits = tag.GetInt("TotalHits"),
                TotalDamage = tag.GetInt("TotalDamage"),
                BestWeapon = tag.GetString("BestWeapon"),
                IsPinned = tag.ContainsKey("IsPinned") && tag.GetBool("IsPinned"),
                WorldName = tag.ContainsKey("WorldName") ? tag.GetString("WorldName") : "Unknown"
            };

            if (tag.ContainsKey("WeaponNames")) record.WeaponNames = tag.GetList<string>("WeaponNames").ToList();
            if (tag.ContainsKey("WeaponDamages")) record.WeaponDamages = tag.GetList<int>("WeaponDamages").ToList();
            if (tag.ContainsKey("BossHPPercent")) record.BossHPPercent = tag.GetInt("BossHPPercent");
            if (tag.ContainsKey("SlowdownPercent")) record.SlowdownPercent = tag.GetDouble("SlowdownPercent");
            if (tag.ContainsKey("AvgFPS")) record.AvgFPS = tag.GetDouble("AvgFPS");
            if (tag.ContainsKey("RtaStr")) record.RtaStr = tag.GetString("RtaStr");
            if (tag.ContainsKey("PlayerRankNames")) record.PlayerRankNames = tag.GetList<string>("PlayerRankNames").ToList();
            if (tag.ContainsKey("PlayerRankDamages")) record.PlayerRankDamages = tag.GetList<int>("PlayerRankDamages").ToList();
            if (tag.ContainsKey("PlayerRankHitsReceived")) record.PlayerRankHitsReceived = tag.GetList<int>("PlayerRankHitsReceived").ToList();
            if (tag.ContainsKey("PlayerRankHitsDealt")) record.PlayerRankHitsDealt = tag.GetList<int>("PlayerRankHitsDealt").ToList();
            if (tag.ContainsKey("PlayerRankDeaths")) record.PlayerRankDeaths = tag.GetList<int>("PlayerRankDeaths").ToList();
            if (tag.ContainsKey("GlobalDebuffTrapDamage")) record.GlobalDebuffTrapDamage = tag.GetInt("GlobalDebuffTrapDamage");
            if (tag.ContainsKey("DeathReason")) record.DeathReason = tag.GetString("DeathReason");

            return record;
        }
    }

    public class BossStatusPlayer : ModPlayer
    {
        public List<PlayerBossRecord> BossRecords = new List<PlayerBossRecord>();
        public bool HasUnreadRecords = false;
        public Vector2 BookButtonPosition = Vector2.Zero; 

        public override void Initialize() {
            BossRecords = new List<PlayerBossRecord>();
            HasUnreadRecords = false;
            BookButtonPosition = Vector2.Zero;
        }

        public override void SaveData(TagCompound tag) {
            var list = BossRecords.Select(r => r.Save()).ToList();
            tag["BossRecords"] = list;
            tag["HasUnreadRecords"] = HasUnreadRecords;
            tag["BookBtnX"] = BookButtonPosition.X;
            tag["BookBtnY"] = BookButtonPosition.Y;
        }

        public override void LoadData(TagCompound tag) {
            if (tag.ContainsKey("BossRecords")) {
                var list = tag.GetList<TagCompound>("BossRecords");
                BossRecords = list.Select(PlayerBossRecord.Load).ToList();
            }
            if (tag.ContainsKey("HasUnreadRecords")) HasUnreadRecords = tag.GetBool("HasUnreadRecords");
            if (tag.ContainsKey("BookBtnX")) BookButtonPosition.X = tag.GetFloat("BookBtnX");
            if (tag.ContainsKey("BookBtnY")) BookButtonPosition.Y = tag.GetFloat("BookBtnY");
        }

        // --- TAMBAHAN: method untuk renumber attempt setelah hapus ---
        public void RenumberAttempts(string bossName) {
            var records = BossRecords.Where(r => r.BossName == bossName).OrderBy(r => r.AttemptNumber).ToList();
            for (int i = 0; i < records.Count; i++) {
                records[i].AttemptNumber = i + 1;
            }
        }
    }

    public class BossStatusUISystem : ModSystem
    {
        public static ModKeybind ToggleUIKeybind { get; private set; }
        internal UserInterface MainWindowInterface;
        internal UserInterface BookButtonInterface;
        internal BossMainUIState MainUI;
        internal BookButtonUIState BookUI;

        public override void Load() {
            ToggleUIKeybind = KeybindLoader.RegisterKeybind(Mod, "Toggle Boss Status GUI", "U");
            if (!Main.dedServ) { 
                MainUI = new BossMainUIState();
                MainWindowInterface = new UserInterface();
                MainWindowInterface.SetState(MainUI);

                BookUI = new BookButtonUIState();
                BookButtonInterface = new UserInterface();
                BookButtonInterface.SetState(BookUI);
            }
        }

        public override void Unload() {
            ToggleUIKeybind = null;
            MainUI = null;
            BookUI = null;
            MainWindowInterface = null;
            BookButtonInterface = null;
        }

        public override void PostUpdateInput() {
            if (ToggleUIKeybind != null && ToggleUIKeybind.JustPressed) {
                BossMainUIState.Visible = !BossMainUIState.Visible;
                if (BossMainUIState.Visible) {
                    SoundEngine.PlaySound(SoundID.DoorOpen);
                    if (Main.LocalPlayer.TryGetModPlayer<BossStatusPlayer>(out var modPlayer)) {
                        modPlayer.HasUnreadRecords = false;
                    }
                } else {
                    SoundEngine.PlaySound(SoundID.DoorClosed);
                    Main.blockInput = false;
                    PlayerInput.WritingText = false;
                }
            }
        }

        public override void UpdateUI(GameTime gameTime) {
            if (BossMainUIState.Visible) MainWindowInterface?.Update(gameTime);
            if (Main.playerInventory) BookButtonInterface?.Update(gameTime);
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers) {
            int mouseTextIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Mouse Text"));
            if (mouseTextIndex != -1) {
                layers.Insert(mouseTextIndex, new LegacyGameInterfaceLayer(
                    "TheSanity: Boss Status Book Button",
                    delegate {
                        if (Main.playerInventory && BookButtonInterface?.CurrentState != null) {
                            BookButtonInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );

                layers.Insert(mouseTextIndex + 1, new LegacyGameInterfaceLayer(
                    "TheSanity: Boss Status Main Window",
                    delegate {
                        if (BossMainUIState.Visible && MainWindowInterface?.CurrentState != null) {
                            MainWindowInterface.Draw(Main.spriteBatch, new GameTime());
                        }
                        return true;
                    },
                    InterfaceScaleType.UI)
                );
            }
        }
    }

    public class BookButtonUIState : UIState
    {
        private bool dragging = false;
        private Vector2 offset;

        public override void Draw(SpriteBatch spriteBatch) {
            var modPlayer = Main.LocalPlayer.GetModPlayer<BossStatusPlayer>();
            if (modPlayer.BookButtonPosition == Vector2.Zero) {
                modPlayer.BookButtonPosition = new Vector2(Main.screenWidth / 2f - 16f, Main.screenHeight / 2f - 16f);
            }

            Rectangle btnRect = new Rectangle((int)modPlayer.BookButtonPosition.X, (int)modPlayer.BookButtonPosition.Y, 32, 32);
            bool isHovering = btnRect.Contains(Main.MouseScreen.ToPoint());

            if (isHovering) Main.LocalPlayer.mouseInterface = true;

            if (Main.mouseRight && isHovering && !dragging) {
                dragging = true;
                offset = Main.MouseScreen - modPlayer.BookButtonPosition;
            }
            if (dragging) {
                modPlayer.BookButtonPosition = Main.MouseScreen - offset;
                if (!Main.mouseRight) dragging = false;
            }

            if (Main.mouseLeft && Main.mouseLeftRelease && isHovering && !dragging) {
                BossMainUIState.Visible = !BossMainUIState.Visible;
                modPlayer.HasUnreadRecords = false; 
                SoundEngine.PlaySound(SoundID.DoorOpen);
            }

            Texture2D bookTex = TextureAssets.Item[ItemID.Book].Value;
            Vector2 drawPos = modPlayer.BookButtonPosition;
            
            if (modPlayer.HasUnreadRecords) {
                Color rainbowColor = Main.DiscoColor;
                for (int i = 0; i < 4; i++) {
                    Vector2 off = new Vector2(i == 0 ? -2 : i == 1 ? 2 : 0, i == 2 ? -2 : i == 3 ? 2 : 0);
                    spriteBatch.Draw(bookTex, drawPos + off, rainbowColor);
                }
            }
            spriteBatch.Draw(bookTex, drawPos, isHovering ? Color.LightCyan : Color.White);
        }
    }

    public class BossMainUIState : UIState
    {
        public static bool Visible = false;
        private bool wasVisible = false;
        private Vector2 windowOffset;
        private bool isDraggingWindow = false;
        private bool isResizingWindow = false;

        // Lebar window diperbesar agar background mengikuti
        public float winX = 100, winY = 120;
        public float winW = 1540, winH = 580; 
        private const float minW = 1380, minH = 450;

        private string bossSearchFilter = "";
        private string playerSearchFilter = "";
        private int selectedBossType = -1;
        private int inspectedPlayerIndex = -1; 
        private PlayerBossRecord selectedRecord = null;

        private bool isTypingBossSearch = false;
        private bool isTypingPlayerSearch = false;
        private int cursorBlinkTimer = 0;

        private int bossSortMode = 0; 
        private int globalRankPanelScroll = 0;

        private int attemptSortMode = 0;

        private KeyboardState oldKeyboardState;

        private int leftPanelScroll = 0;
        private int rightPanelScroll = 0;
        private int playerPanelScroll = 0;
        private int centerSubGuiScroll = 0;
        private int centerRankScroll = 0;

        private int lastTotalRecordCount = -1;

        private class UIParticle {
            public Vector2 Position;
            public Vector2 Velocity;
            public Color Color;
            public float Scale;
            public int LifeTime;
            public int MaxLife;
        }
        private List<UIParticle> particlesList = new List<UIParticle>();

        private void SpawnShatterParticles(Vector2 mousePos, Color baseColor) {
            Random rand = new Random();
            for (int i = 0; i < 20; i++) {
                float angle = (float)(rand.NextDouble() * Math.PI * 2);
                float speed = (float)(rand.NextDouble() * 3.5f + 1.5f);
                particlesList.Add(new UIParticle {
                    Position = mousePos,
                    Velocity = new Vector2((float)Math.Cos(angle) * speed, (float)Math.Sin(angle) * speed),
                    Color = baseColor,
                    Scale = (float)(rand.NextDouble() * 1.2f + 0.8f),
                    LifeTime = 0,
                    MaxLife = rand.Next(20, 40)
                });
            }
        }

        private void DrawChatString(SpriteBatch spriteBatch, string text, Vector2 pos, Color baseColor, float scale = 0.75f) {
            ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.MouseText.Value, text, pos, baseColor, 0f, Vector2.Zero, new Vector2(scale));
        }

        public static int GetMainBossId(int npcType) {
            if (npcType == NPCID.MoonLordCore || npcType == NPCID.MoonLordHand || npcType == NPCID.MoonLordHead) return NPCID.MoonLordCore;
            if (npcType == NPCID.Golem || npcType == NPCID.GolemHead || npcType == NPCID.GolemFistLeft || npcType == NPCID.GolemFistRight) return NPCID.Golem;
            if (npcType == NPCID.Retinazer || npcType == NPCID.Spazmatism) return NPCID.Retinazer;
            if (npcType == NPCID.EaterofWorldsHead || npcType == NPCID.EaterofWorldsBody || npcType == NPCID.EaterofWorldsTail) return NPCID.EaterofWorldsHead;
            if (npcType == NPCID.SkeletronHead || npcType == NPCID.SkeletronHand) return NPCID.SkeletronHead;
            if (npcType == NPCID.SkeletronPrime || npcType == NPCID.PrimeCannon || npcType == NPCID.PrimeLaser || npcType == NPCID.PrimeVice || npcType == NPCID.PrimeSaw) return NPCID.SkeletronPrime;
            if (npcType == NPCID.WallofFlesh || npcType == NPCID.WallofFleshEye) return NPCID.WallofFlesh;
            return npcType;
        }

        private void HandleKeyboardTyping(ref string filterStr, ref bool typingFlag, KeyboardState currentKeyboardState) {
            PlayerInput.WritingText = true;
            
            if (currentKeyboardState.IsKeyDown(Keys.Back) && !oldKeyboardState.IsKeyDown(Keys.Back)) {
                if (filterStr.Length > 0) {
                    filterStr = filterStr.Substring(0, filterStr.Length - 1);
                }
            }

            if ((currentKeyboardState.IsKeyDown(Keys.Escape) && !oldKeyboardState.IsKeyDown(Keys.Escape)) ||
                (currentKeyboardState.IsKeyDown(Keys.Enter) && !oldKeyboardState.IsKeyDown(Keys.Enter))) {
                typingFlag = false;
                Main.blockInput = false;
                return;
            }

            foreach (char c in Main.GetInputText(string.Empty)) {
                if (c != '\b' && c != '\r' && c != '\n' && !char.IsControl(c)) {
                    filterStr += c;
                }
            }
        }

        public override void Draw(SpriteBatch spriteBatch) {
            if (!Visible) {
                wasVisible = false;
                return;
            }

            if (Visible && !wasVisible) {
                Main.mouseX = (int)(winX + winW / 2f);
                Main.mouseY = (int)(winY + 15);
                wasVisible = true;
            }

            KeyboardState currentKeyboardState = Keyboard.GetState();
            if (inspectedPlayerIndex == -1) inspectedPlayerIndex = Main.myPlayer;

            var targetPlayerRecords = Main.player[inspectedPlayerIndex].GetModPlayer<BossStatusPlayer>().BossRecords;

            if (targetPlayerRecords.Count != lastTotalRecordCount) {
                if (selectedBossType != -1) {
                    string selectedBossName = Lang.GetNPCNameValue(selectedBossType);
                    if (selectedBossType == NPCID.EaterofWorldsHead) selectedBossName = "Eater of Worlds";
                    if (selectedBossType == NPCID.Retinazer) selectedBossName = "The Twins";
                    if (selectedBossType == NPCID.MoonLordCore) selectedBossName = "Moon Lord";
                    if (selectedBossType == NPCID.Golem) selectedBossName = "Golem";
                    if (selectedBossType == NPCID.SkeletronHead) selectedBossName = "Skeletron";
                    if (selectedBossType == NPCID.SkeletronPrime) selectedBossName = "Skeletron Prime";
                    if (selectedBossType == NPCID.WallofFlesh) selectedBossName = "Wall of Flesh";

                    selectedRecord = targetPlayerRecords.LastOrDefault(r => r.BossName == selectedBossName);
                }
                lastTotalRecordCount = targetPlayerRecords.Count;
            }

            if (selectedRecord != null) {
                var refreshedRecord = targetPlayerRecords.FirstOrDefault(r => r.BossName == selectedRecord.BossName && r.AttemptNumber == selectedRecord.AttemptNumber);
                if (refreshedRecord != null) {
                    selectedRecord = refreshedRecord;
                }
            } else if (selectedBossType != -1) {
                string selectedBossName = Lang.GetNPCNameValue(selectedBossType);
                if (selectedBossType == NPCID.EaterofWorldsHead) selectedBossName = "Eater of Worlds";
                if (selectedBossType == NPCID.Retinazer) selectedBossName = "The Twins";
                if (selectedBossType == NPCID.MoonLordCore) selectedBossName = "Moon Lord";
                if (selectedBossType == NPCID.Golem) selectedBossName = "Golem";
                if (selectedBossType == NPCID.SkeletronHead) selectedBossName = "Skeletron";
                if (selectedBossType == NPCID.SkeletronPrime) selectedBossName = "Skeletron Prime";
                if (selectedBossType == NPCID.WallofFlesh) selectedBossName = "Wall of Flesh";

                selectedRecord = targetPlayerRecords.LastOrDefault(r => r.BossName == selectedBossName);
            }

            Main.LocalPlayer.mouseInterface = true;
            bool jointClick = Main.mouseLeft && Main.mouseLeftRelease;

            Rectangle dragAreaCheck = new Rectangle((int)winX, (int)winY, (int)winW - 20, 30);
            Rectangle resizeAreaCheck = new Rectangle((int)(winX + winW - 16), (int)(winY + winH - 16), 16, 16);

            if (Main.mouseLeft && dragAreaCheck.Contains(Main.MouseScreen.ToPoint()) && !isDraggingWindow && !isResizingWindow) {
                isDraggingWindow = true;
                windowOffset = Main.MouseScreen - new Vector2(winX, winY);
            }
            if (isDraggingWindow) {
                winX = Main.MouseScreen.X - windowOffset.X;
                winY = Main.MouseScreen.Y - windowOffset.Y;
                if (!Main.mouseLeft) isDraggingWindow = false;
            }

            if (Main.mouseLeft && resizeAreaCheck.Contains(Main.MouseScreen.ToPoint()) && !isResizingWindow && !isDraggingWindow) {
                isResizingWindow = true;
            }
            if (isResizingWindow) {
                winW = Math.Max(minW, Main.MouseScreen.X - winX);
                winH = Math.Max(minH, Main.MouseScreen.Y - winY);
                if (!Main.mouseLeft) isResizingWindow = false;
            }

            Rectangle winRect = new Rectangle((int)winX, (int)winY, (int)winW, (int)winH);
            Rectangle dragArea = new Rectangle((int)winX, (int)winY, (int)winW - 20, 30);
            Rectangle resizeArea = new Rectangle((int)(winX + winW - 16), (int)(winY + winH - 16), 16, 16);

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            
            spriteBatch.Draw(pixel, winRect, Color.Black * 0.94f);
            
            int stepSize = 35; 
            Color gridColor = Color.DarkSlateBlue * 0.12f;
            for (int gx = (int)winX; gx < winX + winW; gx += stepSize) {
                spriteBatch.Draw(pixel, new Rectangle(gx, (int)winY + 32, 1, (int)winH - 34), gridColor);
            }
            for (int gy = (int)winY + 32; gy < winY + winH; gy += stepSize) {
                spriteBatch.Draw(pixel, new Rectangle((int)winX, gy, (int)winW, 1), gridColor);
            }

            for (int k = 0; k < 35; k++) {
                float driftingX = winX + 15 + ((k * 145.85f + (float)Main.GlobalTimeWrappedHourly * 12f) % (winW - 30));
                float driftingY = winY + 38 + ((k * 224.11f + (float)Main.GlobalTimeWrappedHourly * 6f) % (winH - 45));
                float blinkWave = 0.15f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.5f + k);
                spriteBatch.Draw(pixel, new Rectangle((int)driftingX, (int)driftingY, 2, 2), Color.LightCyan * blinkWave);
            }

            float animationTime = (float)Main.GlobalTimeWrappedHourly * 25f; 
            int totalLines = 3;
            for (int k = 0; k < totalLines; k++) {
                float calculatedY = winY + 35 + ((animationTime + (k * (winH - 45) / totalLines)) % (winH - 45));
                Rectangle singleLineRect = new Rectangle((int)winX + 2, (int)calculatedY, (int)winW - 4, 1);
                float alphaSinFade = (float)Math.Sin((calculatedY - winY - 35) / (winH - 45) * Math.PI);
                spriteBatch.Draw(pixel, singleLineRect, Color.Orange * (0.05f * alphaSinFade));
            }

            float pulseSinTime = 0.80f + 0.20f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 3.5f);
            Color themeOrangeAesthetic = Color.Orange * pulseSinTime;
            Color themeOrangeDarkFill = new Color(75, 30, 0) * 0.75f;

            spriteBatch.Draw(pixel, dragArea, themeOrangeDarkFill);
            spriteBatch.Draw(pixel, new Rectangle((int)winX, (int)winY, (int)winW, 3), themeOrangeAesthetic);
            spriteBatch.Draw(pixel, new Rectangle((int)winX, (int)winY + 30, (int)winW, 1), Color.Orange * 0.45f);
            spriteBatch.Draw(pixel, new Rectangle((int)winX, (int)winY + (int)winH - 2, (int)winW, 2), Color.Orange * 0.5f);

            Rectangle closeRect = new Rectangle((int)(winX + winW - 35), (int)(winY + 5), 25, 20);
            bool hoverClose = closeRect.Contains(Main.MouseScreen.ToPoint());
            spriteBatch.Draw(pixel, closeRect, hoverClose ? Color.Red : Color.DarkRed);
            DrawChatString(spriteBatch, "X", new Vector2(closeRect.X + 7, closeRect.Y + 2), Color.White, 0.80f);
            
            if (hoverClose && jointClick) {
                SpawnShatterParticles(Main.MouseScreen, Color.Red);
                Visible = false;
                isTypingBossSearch = isTypingPlayerSearch = false;
                Main.blockInput = false;
                PlayerInput.WritingText = false;
                SoundEngine.PlaySound(SoundID.DoorClosed); 
                return;
            }

            cursorBlinkTimer++;

            if (isTypingBossSearch) {
                Main.blockInput = true;
                HandleKeyboardTyping(ref bossSearchFilter, ref isTypingBossSearch, currentKeyboardState);
            }
            else if (isTypingPlayerSearch) {
                Main.blockInput = true;
                HandleKeyboardTyping(ref playerSearchFilter, ref isTypingPlayerSearch, currentKeyboardState);
            }

            //Resize UI
            float gap = 6;
            float leftW = 165;
            float rightAttemptW = 190;
            float rightPlayerW = 164;
            float globalRankW = 185; 
            float centerW = winW - (leftW + rightAttemptW + rightPlayerW + globalRankW + (gap * 6));
            float panelH = winH - 50;

            float leftX = winX + gap;
            float centerX = leftX + leftW + gap;
            float rightAttemptX = centerX + centerW + gap;
            float rightPlayerX = rightAttemptX + rightAttemptW + gap;
            float globalRankX = rightPlayerX + rightPlayerW + gap;
            float panelsY = winY + 40;

            string headerText = $"[i:{ItemID.Goggles}] BOSS OBSERVER MONITOR - VIEWING PROFILE: {Main.player[inspectedPlayerIndex].name.ToUpper()}";
            DrawChatString(spriteBatch, headerText, new Vector2(winX + 15, winY + 6), Color.White, 0.85f);

            DrawBossListPanel(spriteBatch, leftX, panelsY, leftW, panelH, ref jointClick);
            DrawCenterLogPanel(spriteBatch, centerX, panelsY, centerW, panelH);
            DrawAttemptLogsPanel(spriteBatch, rightAttemptX, panelsY, rightAttemptW, panelH, ref jointClick);
            DrawPlayerListPanel(spriteBatch, rightPlayerX, panelsY, rightPlayerW, panelH, ref jointClick);
            DrawGlobalRankPanel(spriteBatch, globalRankX, panelsY, globalRankW, panelH);

            spriteBatch.Draw(pixel, resizeArea, Color.White * (resizeArea.Contains(Main.MouseScreen.ToPoint()) ? 0.6f : 0.2f));

            for (int i = particlesList.Count - 1; i >= 0; i--) {
                var p = particlesList[i];
                p.Position += p.Velocity;
                p.Velocity *= 0.95f; 
                p.LifeTime++;
                if (p.LifeTime >= p.MaxLife) {
                    particlesList.RemoveAt(i);
                    continue;
                }
                float particleAlpha = 1.0f - ((float)p.LifeTime / p.MaxLife);
                spriteBatch.Draw(pixel, new Rectangle((int)p.Position.X, (int)p.Position.Y, (int)(3 * p.Scale), (int)(3 * p.Scale)), p.Color * particleAlpha);
            }

            oldKeyboardState = currentKeyboardState;
        }

        private void DrawBossListPanel(SpriteBatch spriteBatch, float x, float y, float w, float h, ref bool jointClick) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle rect = new Rectangle((int)x, (int)y, (int)w, (int)h);
            spriteBatch.Draw(pixel, rect, Color.Gray * 0.10f);

            Rectangle sRect = new Rectangle((int)x + 5, (int)y + 5, (int)w - 10, 24);
            bool hoverSRect = sRect.Contains(Main.MouseScreen.ToPoint());

            if (jointClick && hoverSRect) {
                SpawnShatterParticles(Main.MouseScreen, Color.White);
                isTypingBossSearch = true; isTypingPlayerSearch = false;
                jointClick = false;
            } else if (jointClick && !hoverSRect && isTypingBossSearch) {
                isTypingBossSearch = false;
                Main.blockInput = false;
            }

            spriteBatch.Draw(pixel, sRect, isTypingBossSearch ? Color.DarkSlateGray * 0.8f : (hoverSRect ? Color.DarkSlateGray * 0.6f : Color.DarkSlateGray * 0.3f));

            string blink = (isTypingBossSearch && (cursorBlinkTimer % 40 < 20)) ? "|" : "";
            string disp = string.IsNullOrEmpty(bossSearchFilter) ? "Search Boss..." : (bossSearchFilter + blink);
            DrawChatString(spriteBatch, disp, new Vector2(sRect.X + 5, sRect.Y + 4), string.IsNullOrEmpty(bossSearchFilter) ? Color.Gray : Color.White, 0.72f);

            List<int> bossIDs = new List<int>();
            for (int i = 1; i < NPCLoader.NPCCount; i++) {
                NPC sample = ContentSamples.NpcsByNetId[i];
                if (sample != null && sample.boss) {
                    int mid = GetMainBossId(i);
                    if (mid == NPCID.TorchGod || mid == NPCID.MartianSaucerCore || mid == NPCID.MartianSaucer) continue;
                    if (!bossIDs.Contains(mid)) bossIDs.Add(mid);
                }
            }

            var targetPlayerRecords = Main.player[inspectedPlayerIndex].GetModPlayer<BossStatusPlayer>().BossRecords;

            if (bossSortMode == 1) { 
                bossIDs = bossIDs.OrderBy(id => {
                    string name = Lang.GetNPCNameValue(id);
                    if (id == NPCID.EaterofWorldsHead) name = "Eater of Worlds";
                    if (id == NPCID.Retinazer) name = "The Twins";
                    if (id == NPCID.MoonLordCore) name = "Moon Lord";
                    return name;
                }).ToList();
            } 
            else if (bossSortMode == 2) { 
                bossIDs = bossIDs.OrderByDescending(id => {
                    string sName = Lang.GetNPCNameValue(id);
                    if (id == NPCID.EaterofWorldsHead) sName = "Eater of Worlds";
                    if (id == NPCID.Retinazer) sName = "The Twins";
                    if (id == NPCID.MoonLordCore) sName = "Moon Lord";
                    return targetPlayerRecords.Any(r => r.BossName == sName && r.BossHPPercent == 0) ? 1 : 0;
                }).ToList();
            }

            if (!string.IsNullOrEmpty(bossSearchFilter)) {
                bossIDs = bossIDs.Where(id => Lang.GetNPCNameValue(id).ToLower().Contains(bossSearchFilter.ToLower())).ToList();
            }

            int visibleMaxCount = (int)((h - 75) / 34);
            if (rect.Contains(Main.MouseScreen.ToPoint()) && Main.MouseScreen.Y < (y + h - 35)) {
                if (PlayerInput.ScrollWheelDelta > 0) leftPanelScroll = Math.Max(0, leftPanelScroll - 1);
                if (PlayerInput.ScrollWheelDelta < 0) leftPanelScroll = Math.Min(Math.Max(0, bossIDs.Count - visibleMaxCount), leftPanelScroll + 1);
            }

            float rowY = y + 36;
            for (int i = leftPanelScroll; i < Math.Min(bossIDs.Count, leftPanelScroll + visibleMaxCount); i++) {
                int bId = bossIDs[i];
                string name = Lang.GetNPCNameValue(bId);
                
                if (bId == NPCID.EaterofWorldsHead) name = "Eater of Worlds";
                if (bId == NPCID.Retinazer) name = "The Twins";
                if (bId == NPCID.MoonLordCore) name = "Moon Lord";

                Rectangle itemRect = new Rectangle((int)x + 5, (int)rowY, (int)w - 10, 30);
                bool hov = itemRect.Contains(Main.MouseScreen.ToPoint());
                bool isDefeated = targetPlayerRecords.Any(r => r.BossName == name && r.BossHPPercent == 0);

                Color textColor = isDefeated ? Color.LightGreen : Color.LightPink;
                spriteBatch.Draw(pixel, itemRect, selectedBossType == bId ? Color.Orange * 0.35f : (hov ? Color.White * 0.08f : Color.Transparent));

                int iconTargetId = bId;
                if (bId == NPCID.MoonLordCore) iconTargetId = NPCID.MoonLordHead;
                if (bId == NPCID.Golem) iconTargetId = NPCID.GolemHead;

                int hi = NPCID.Sets.BossHeadTextures[iconTargetId];
                if (hi >= 0 && hi < TextureAssets.NpcHeadBoss.Length) {
                    Texture2D head = TextureAssets.NpcHeadBoss[hi].Value;
                    spriteBatch.Draw(head, new Vector2(itemRect.X + 2, itemRect.Y + 3), new Rectangle(0, 0, head.Width, head.Height), Color.White, 0f, Vector2.Zero, 0.60f, SpriteEffects.None, 0f);
                }

                DrawChatString(spriteBatch, name, new Vector2(itemRect.X + 28, itemRect.Y + 6), textColor, 0.70f);

                if (hov && jointClick) {
                    selectedBossType = bId;
                    centerSubGuiScroll = 0;
                    centerRankScroll = 0;
                    selectedRecord = targetPlayerRecords.LastOrDefault(r => r.BossName == name);
                    
                    SpawnShatterParticles(Main.MouseScreen, textColor);
                    SoundEngine.PlaySound(SoundID.Item27); 
                    
                    jointClick = false; 
                }
                rowY += 34;
            }

            float sortBtnY = y + h - 32;
            Rectangle btnLeftRect = new Rectangle((int)x + 5, (int)sortBtnY, 24, 24);
            Rectangle btnRightRect = new Rectangle((int)x + (int)w - 29, (int)sortBtnY, 24, 24);
            Rectangle labelRect = new Rectangle((int)x + 31, (int)sortBtnY, (int)w - 62, 24);

            bool hovLeft = btnLeftRect.Contains(Main.MouseScreen.ToPoint());
            bool hovRight = btnRightRect.Contains(Main.MouseScreen.ToPoint());

            spriteBatch.Draw(pixel, btnLeftRect, hovLeft ? Color.Orange * 0.6f : Color.Orange * 0.2f);
            spriteBatch.Draw(pixel, btnRightRect, hovRight ? Color.Orange * 0.6f : Color.Orange * 0.2f);
            spriteBatch.Draw(pixel, labelRect, Color.Black * 0.4f);

            DrawChatString(spriteBatch, "◀", new Vector2(btnLeftRect.X + 6, btnLeftRect.Y + 4), Color.White, 0.70f);
            DrawChatString(spriteBatch, "▶", new Vector2(btnRightRect.X + 8, btnRightRect.Y + 4), Color.White, 0.70f);

            string sortLabel = "Sort: Default";
            string tooltipDesc = "Default database sorting layout.";
            if (bossSortMode == 1) { sortLabel = "Sort: Alpha"; tooltipDesc = "Sort alphabetically from A to Z."; }
            if (bossSortMode == 2) { sortLabel = "Sort: Victory"; tooltipDesc = "Sort by best combat parameters (Defeated specimens first)."; }

            Vector2 stringSize = FontAssets.MouseText.Value.MeasureString(sortLabel) * 0.62f;
            DrawChatString(spriteBatch, sortLabel, new Vector2(labelRect.X + (labelRect.Width - stringSize.X) / 2f, labelRect.Y + 5), Color.Gold, 0.62f);

            if (hovLeft && jointClick) {
                SpawnShatterParticles(Main.MouseScreen, Color.Orange);
                bossSortMode = (bossSortMode + 2) % 3;
                SoundEngine.PlaySound(SoundID.MenuTick);
                jointClick = false;
            }
            if (hovRight && jointClick) {
                SpawnShatterParticles(Main.MouseScreen, Color.Orange);
                bossSortMode = (bossSortMode + 1) % 3;
                SoundEngine.PlaySound(SoundID.MenuTick);
                jointClick = false;
            }

            if (hovLeft || hovRight || labelRect.Contains(Main.MouseScreen.ToPoint())) {
                Main.instance.MouseText(tooltipDesc);
            }
        }

        private void DrawCenterLogPanel(SpriteBatch spriteBatch, float x, float y, float w, float h) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            spriteBatch.Draw(pixel, new Rectangle((int)x, (int)y, (int)w, (int)h), Color.Gray * 0.05f);

            Vector2 drawPos = new Vector2(x + 15, y + 15);
            string profileName = Main.player[inspectedPlayerIndex].name.ToUpper();
            DrawChatString(spriteBatch, $">> METRIC ANALYSIS TERMINAL: {profileName} <<", drawPos, Color.Cyan, 0.85f);
            drawPos.Y += 28;

            if (selectedBossType == -1) {
                DrawChatString(spriteBatch, "Standby... Select diagnostic boss target to extract data files.", drawPos, Color.DarkGray, 0.78f);
                return;
            }

            if (selectedRecord == null) {
                DrawChatString(spriteBatch, "STATUS: NO RECORDS FOUND", drawPos, Color.LightPink, 0.80f);
                drawPos.Y += 24;
                DrawChatString(spriteBatch, "Target specimen has not been fought or recorded by this player.", drawPos, Color.Red * 0.8f, 0.75f);
                return;
            }

            spriteBatch.Draw(pixel, new Rectangle((int)x + 10, (int)drawPos.Y, (int)w - 20, 2), Color.Orange * 0.6f);
            drawPos.Y += 10;

            string bossTitle = selectedRecord.BossName.ToUpper();
            string statusFightStr = selectedRecord.BossHPPercent == 0 ? "[c/00FF00:VICTORY]" : $"[c/FF0000:FAILED (Boss HP: {selectedRecord.BossHPPercent}%)]";
            DrawChatString(spriteBatch, $"{bossTitle} - ATTEMPT RUN #{selectedRecord.AttemptNumber} - {statusFightStr}", drawPos, Color.Gold, 0.82f);
            drawPos.Y += 24;

            var targetPlayerRecords = Main.player[inspectedPlayerIndex].GetModPlayer<BossStatusPlayer>().BossRecords;
            var previousRecord = targetPlayerRecords.FirstOrDefault(r => r.BossName == selectedRecord.BossName && r.AttemptNumber == selectedRecord.AttemptNumber - 1);

            Color curTimeColor = Color.White;
            string prevTimeStr = "";
            if (previousRecord != null) {
                bool currentBetter = selectedRecord.DurationTicks < previousRecord.DurationTicks;
                curTimeColor = currentBetter ? Color.Lime : Color.Red;
                string formatColor = currentBetter ? "[c/FF0000:" : "[c/00FF00:";
                prevTimeStr = $" ({formatColor}Prev: {previousRecord.DurationStr}])";
            }
            DrawChatString(spriteBatch, $"[i:{ItemID.Stopwatch}] Fight Time: ", drawPos, Color.White, 0.76f);
            DrawChatString(spriteBatch, $"{selectedRecord.DurationStr}{prevTimeStr}", new Vector2(drawPos.X + 140, drawPos.Y), curTimeColor, 0.76f);
            drawPos.Y += 22;

            Color curHpColor = Color.White;
            string prevHpStr = "";
            if (previousRecord != null) {
                bool currentBetter = selectedRecord.BossHPPercent < previousRecord.BossHPPercent;
                curHpColor = currentBetter ? Color.Lime : Color.Red;
                string formatColor = currentBetter ? "[c/FF0000:" : "[c/00FF00:";
                prevHpStr = $" ({formatColor}Prev: {previousRecord.BossHPPercent}%])";
            }
            DrawChatString(spriteBatch, $"[i:{ItemID.LifeCrystal}] Boss Health: ", drawPos, Color.White, 0.76f);
            DrawChatString(spriteBatch, $"{selectedRecord.BossHPPercent}%{prevHpStr}", new Vector2(drawPos.X + 140, drawPos.Y), curHpColor, 0.76f);
            drawPos.Y += 22;

            Color curHitColor = Color.White;
            string prevHitStr = "";
            if (previousRecord != null) {
                bool currentBetter = selectedRecord.TotalHits < previousRecord.TotalHits;
                curHitColor = currentBetter ? Color.Lime : Color.Red;
                string formatColor = currentBetter ? "[c/FF0000:" : "[c/00FF00:";
                prevHitStr = $" ({formatColor}Prev: {previousRecord.TotalHits} Hits])";
            }
            DrawChatString(spriteBatch, $"[i:{ItemID.CobaltShield}] Hits Sustained: ", drawPos, Color.White, 0.76f);
            DrawChatString(spriteBatch, $"{selectedRecord.TotalHits} Hits{prevHitStr}", new Vector2(drawPos.X + 140, drawPos.Y), curHitColor, 0.76f);
            drawPos.Y += 22;

            Color curDmgColor = Color.White;
            string prevDmgStr = "";
            if (previousRecord != null) {
                bool currentBetter = selectedRecord.TotalDamage > previousRecord.TotalDamage;
                curDmgColor = currentBetter ? Color.Lime : Color.Red;
                string formatColor = currentBetter ? "[c/FF0000:" : "[c/00FF00:";
                prevDmgStr = $" ({formatColor}Prev: {previousRecord.TotalDamage} DMG])";
            }
            DrawChatString(spriteBatch, $"[i:{ItemID.FallenStar}] Output Power: ", drawPos, Color.White, 0.76f);
            DrawChatString(spriteBatch, $"{selectedRecord.TotalDamage} DMG{prevDmgStr}", new Vector2(drawPos.X + 140, drawPos.Y), curDmgColor, 0.76f);
            drawPos.Y += 22;

            DrawChatString(spriteBatch, $"[i:{ItemID.Tombstone}] Death Reason: ", drawPos, Color.White, 0.76f);
            string dReasonText = string.IsNullOrEmpty(selectedRecord.DeathReason) ? "None" : selectedRecord.DeathReason;
            Color reasonUiColor = dReasonText == "None" ? Color.LightGreen : Color.Red; 
            DrawChatString(spriteBatch, dReasonText, new Vector2(drawPos.X + 140, drawPos.Y), reasonUiColor, 0.76f);
            drawPos.Y += 22;

            // World Name
            DrawChatString(spriteBatch, $"[i:{ItemID.Compass}] World Name: ", drawPos, Color.White, 0.76f);
            string worldDisplay = string.IsNullOrEmpty(selectedRecord.WorldName) ? "Unknown" : selectedRecord.WorldName;
            DrawChatString(spriteBatch, worldDisplay, new Vector2(drawPos.X + 140, drawPos.Y), Color.LightBlue, 0.76f);
            drawPos.Y += 22;

            string speedText = $"Fight Speed: {selectedRecord.SlowdownPercent:F1}% Slowdown - {selectedRecord.AvgFPS:F1} avg FPS - {selectedRecord.RtaStr} RTA";
            DrawChatString(spriteBatch, speedText, drawPos, Color.LightGreen, 0.74f);
            drawPos.Y += 25;

            // --- KEMBALI KE UKURAN NORMAL ---
            float totalBoxesH = h - (drawPos.Y - y) - 25;
            float singleBoxH = (totalBoxesH - 12) / 2f;

            Rectangle weaponBox = new Rectangle((int)x + 12, (int)drawPos.Y, (int)w - 24, (int)singleBoxH);
            spriteBatch.Draw(pixel, weaponBox, Color.Black * 0.5f);
            DrawChatString(spriteBatch, "[ WEAPON CONTRIBUTIONS ]", new Vector2(weaponBox.X + 8, weaponBox.Y + 4), Color.Orange * 0.8f, 0.70f);

            float wItemY = weaponBox.Y + 22;
            float debuffTrapUiPct = selectedRecord.TotalDamage > 0 ? ((float)selectedRecord.GlobalDebuffTrapDamage / selectedRecord.TotalDamage * 100f) : 0f;
            // --- PERUBAHAN: label Debuff/Trap/etc ---
            DrawChatString(spriteBatch, $"[i:{ItemID.Spike}] Debuff/Trap/etc: {selectedRecord.GlobalDebuffTrapDamage} DMG ({debuffTrapUiPct:F1}%)", new Vector2(weaponBox.X + 10, wItemY), Color.Tomato, 0.72f);
            wItemY += 20; 

            List<string> wNames = selectedRecord.WeaponNames ?? new List<string>();
            List<int> wDamages = selectedRecord.WeaponDamages ?? new List<int>();
            int visibleWeaponsMax = (int)((singleBoxH - 42) / 20); 

            if (weaponBox.Contains(Main.MouseScreen.ToPoint())) {
                if (PlayerInput.ScrollWheelDelta > 0) centerSubGuiScroll = Math.Max(0, centerSubGuiScroll - 1);
                if (PlayerInput.ScrollWheelDelta < 0) centerSubGuiScroll = Math.Min(Math.Max(0, wNames.Count - visibleWeaponsMax), centerSubGuiScroll + 1);
            }

            for (int i = centerSubGuiScroll; i < Math.Min(wNames.Count, centerSubGuiScroll + visibleWeaponsMax); i++) {
                string wn = wNames[i];
                int wd = wDamages[i];
                float pct = selectedRecord.TotalDamage > 0 ? ((float)wd / selectedRecord.TotalDamage * 100f) : 0f;
                DrawChatString(spriteBatch, $"{i + 1}. {wn}: {wd} DMG ({pct:F1}%)", new Vector2(weaponBox.X + 10, wItemY), i == 0 ? Color.Gold : Color.LightGray, 0.72f);
                wItemY += 20;
            }

            Rectangle rankBox = new Rectangle((int)x + 12, (int)(weaponBox.Y + singleBoxH + 8), (int)w - 24, (int)singleBoxH);
            spriteBatch.Draw(pixel, rankBox, Color.Black * 0.5f);
            DrawChatString(spriteBatch, "[ PLAYER TIER MATRIX (DAMAGE, RECEIVED HITS, DEALT HITS, DEATHS) ]", new Vector2(rankBox.X + 8, rankBox.Y + 4), Color.LightSkyBlue * 0.9f, 0.70f);

            List<string> rNames = selectedRecord.PlayerRankNames ?? new List<string>();
            List<int> rDamages = selectedRecord.PlayerRankDamages ?? new List<int>();
            List<int> rHitsReceived = selectedRecord.PlayerRankHitsReceived ?? new List<int>();
            List<int> rHitsDealt = selectedRecord.PlayerRankHitsDealt ?? new List<int>();
            List<int> rDeaths = selectedRecord.PlayerRankDeaths ?? new List<int>();
            
            int visibleRanksMax = (int)((singleBoxH - 22) / 20);

            if (rankBox.Contains(Main.MouseScreen.ToPoint())) {
                if (PlayerInput.ScrollWheelDelta > 0) centerRankScroll = Math.Max(0, centerRankScroll - 1);
                if (PlayerInput.ScrollWheelDelta < 0) centerRankScroll = Math.Min(Math.Max(0, rNames.Count - visibleRanksMax), centerRankScroll + 1);
            }

            float rItemY = rankBox.Y + 22;
            int totalDmgAll = rDamages.Sum();

            for (int i = centerRankScroll; i < Math.Min(rNames.Count, centerRankScroll + visibleRanksMax); i++) {
                string rn = rNames[i];
                int rd = rDamages[i];
                int rRec = rHitsReceived.Count > i ? rHitsReceived[i] : 0;
                int rDlt = rHitsDealt.Count > i ? rHitsDealt[i] : 0;
                int rDth = rDeaths.Count > i ? rDeaths[i] : 0;

                float rPct = totalDmgAll > 0 ? ((float)rd / totalDmgAll * 100f) : 0f;

                Color rankColor = Color.LightGray;
                if (i == 0) rankColor = Main.DiscoColor;
                else if (i == 1) rankColor = Color.Gold;
                else if (i == 2) rankColor = new Color(220, 220, 220);

                string rankPrefix = $" #{i + 1} ";
                if (i == 0) rankPrefix = $"[i:{ItemID.GolfTrophyGold}] ";
                else if (i == 1) rankPrefix = $"[i:{ItemID.GolfTrophySilver}] ";
                else if (i == 2) rankPrefix = $"[i:{ItemID.GolfTrophyBronze}] ";

                string fullStatString = $"{rankPrefix}{rn}: {rd} DMG ({rPct:F1}%) | Dealt: {rDlt} h | Recv: {rRec} h | Deaths: [c/FF3333:{rDth}]";
                DrawChatString(spriteBatch, fullStatString, new Vector2(rankBox.X + 10, rItemY), rankColor, 0.70f);
                rItemY += 20;
            }
        }

        private void DrawAttemptLogsPanel(SpriteBatch spriteBatch, float x, float y, float w, float h, ref bool jointClick) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle rect = new Rectangle((int)x, (int)y, (int)w, (int)h);
            spriteBatch.Draw(pixel, rect, Color.Gray * 0.12f);

            DrawChatString(spriteBatch, "ATTEMPTS", new Vector2(x + 10, y + 10), Color.Orange, 0.78f);
            if (selectedBossType == -1) return;

            string selectedBossName = Lang.GetNPCNameValue(selectedBossType);
            if (selectedBossType == NPCID.EaterofWorldsHead) selectedBossName = "Eater of Worlds";
            if (selectedBossType == NPCID.Retinazer) selectedBossName = "The Twins";
            if (selectedBossType == NPCID.MoonLordCore) selectedBossName = "Moon Lord";
            if (selectedBossType == NPCID.Golem) selectedBossName = "Golem";
            if (selectedBossType == NPCID.SkeletronHead) selectedBossName = "Skeletron";
            if (selectedBossType == NPCID.SkeletronPrime) selectedBossName = "Skeletron Prime";
            if (selectedBossType == NPCID.WallofFlesh) selectedBossName = "Wall of Flesh";

            var modPlayer = Main.player[inspectedPlayerIndex].GetModPlayer<BossStatusPlayer>();
            var targetHistory = modPlayer.BossRecords.Where(r => r.BossName == selectedBossName).ToList();

            int visibleAttemptsMax = (int)((h - 45) / 26);
            if (rect.Contains(Main.MouseScreen.ToPoint()) && Main.MouseScreen.Y < (y + h - 25)) {
                if (PlayerInput.ScrollWheelDelta > 0) rightPanelScroll = Math.Max(0, rightPanelScroll - 1);
                if (PlayerInput.ScrollWheelDelta < 0) rightPanelScroll = Math.Min(Math.Max(0, targetHistory.Count - visibleAttemptsMax), rightPanelScroll + 1);
            }

            var successRuns = targetHistory.Where(r => r.BossHPPercent == 0).ToList();
            int bestTicks = successRuns.Count > 0 ? successRuns.Min(r => r.DurationTicks) : -1;
            int worstTicks = successRuns.Count > 0 ? successRuns.Max(r => r.DurationTicks) : -1;

            IOrderedEnumerable<PlayerBossRecord> orderedRuns;
            if (attemptSortMode == 1) {
                orderedRuns = targetHistory.OrderByDescending(r => r.IsPinned).ThenBy(r => r.BossHPPercent != 0).ThenBy(r => r.DurationTicks);
            } else if (attemptSortMode == 2) {
                orderedRuns = targetHistory.OrderByDescending(r => r.IsPinned).ThenByDescending(r => r.TotalDamage);
            } else {
                orderedRuns = targetHistory.OrderByDescending(r => r.IsPinned).ThenBy(r => r.AttemptNumber);
            }
            var renderingHistoryList = orderedRuns.ToList();

            float rowY = y + 34;
            for (int i = rightPanelScroll; i < Math.Min(renderingHistoryList.Count, rightPanelScroll + visibleAttemptsMax); i++) {
                var rec = renderingHistoryList[i];
                Rectangle rowRect = new Rectangle((int)x + 4, (int)rowY, (int)w - 8, 24);
                bool hov = rowRect.Contains(Main.MouseScreen.ToPoint());

                Color titleColor = Color.White; 
                if (rec.BossHPPercent > 0) {
                    titleColor = Color.Red; 
                } else {
                    if (rec.DurationTicks == bestTicks) titleColor = Color.Lime; 
                    else if (rec.DurationTicks == worstTicks && successRuns.Count > 1) titleColor = Color.White; 
                    else titleColor = Color.Yellow; 
                }

                if (selectedRecord == rec) spriteBatch.Draw(pixel, rowRect, Color.Cyan * 0.22f);
                else if (hov) spriteBatch.Draw(pixel, rowRect, Color.White * 0.08f);

                string statusSuffix = rec.BossHPPercent > 0 ? " (F)" : " (K)";
                string worldDisplay = string.IsNullOrEmpty(rec.WorldName) ? "" : $" ({rec.WorldName})";
                DrawChatString(spriteBatch, $"Run #{rec.AttemptNumber}{worldDisplay}{statusSuffix}", new Vector2(rowRect.X + 6, rowRect.Y + 4), titleColor, 0.74f);

                if (hov && jointClick) {
                    selectedRecord = rec;
                    SpawnShatterParticles(Main.MouseScreen, titleColor);
                    SoundEngine.PlaySound(SoundID.MenuTick);
                    jointClick = false; 
                }
                rowY += 26;
            }

            // 4 tombol: SRT, RFH, PIN, DEL
            float buttonsY = y + h - 2; 
            float totalButtonsWidth = (30 * 4) + (4 * 3); 
            float startBtnX = x + (w - totalButtonsWidth) / 2f;

            Rectangle btnSortRect = new Rectangle((int)startBtnX, (int)buttonsY, 30, 22);
            Rectangle btnRefreshRect = new Rectangle((int)startBtnX + 34, (int)buttonsY, 30, 22);
            Rectangle btnPinRect   = new Rectangle((int)startBtnX + 68, (int)buttonsY, 30, 22);
            Rectangle btnDelRect   = new Rectangle((int)startBtnX + 102, (int)buttonsY, 30, 22);

            bool hovSort = btnSortRect.Contains(Main.MouseScreen.ToPoint());
            bool hovRefresh = btnRefreshRect.Contains(Main.MouseScreen.ToPoint());
            bool hovPin = btnPinRect.Contains(Main.MouseScreen.ToPoint());
            bool hovDel = btnDelRect.Contains(Main.MouseScreen.ToPoint());

            spriteBatch.Draw(pixel, btnSortRect, hovSort ? Color.Orange * 0.7f : Color.Orange * 0.3f);
            spriteBatch.Draw(pixel, btnRefreshRect, hovRefresh ? Color.Orange * 0.7f : Color.Orange * 0.3f);
            spriteBatch.Draw(pixel, btnPinRect, hovPin ? Color.Orange * 0.7f : Color.Orange * 0.3f);
            spriteBatch.Draw(pixel, btnDelRect, hovDel ? Color.Red * 0.7f : Color.DarkRed * 0.5f);

            DrawChatString(spriteBatch, "SRT", new Vector2(btnSortRect.X + 4, btnSortRect.Y + 4), Color.White, 0.58f);
            DrawChatString(spriteBatch, "RFH", new Vector2(btnRefreshRect.X + 4, btnRefreshRect.Y + 4), Color.LightSkyBlue, 0.58f);
            DrawChatString(spriteBatch, "PIN", new Vector2(btnPinRect.X + 5, btnPinRect.Y + 4), Color.Gold, 0.58f);
            DrawChatString(spriteBatch, "DEL", new Vector2(btnDelRect.X + 5, btnDelRect.Y + 4), Color.White, 0.58f);

            if (hovSort) Main.instance.MouseText($"Sort Attempts (Current: {(attemptSortMode == 0 ? "Chrono" : attemptSortMode == 1 ? "Speed" : "Max Dmg")})");
            if (hovRefresh) Main.instance.MouseText("Force Refresh Database");
            if (hovPin) Main.instance.MouseText(selectedRecord != null ? $"Toggle Pin/Unpin Run #{selectedRecord.AttemptNumber}" : "Select a run first");
            if (hovDel) Main.instance.MouseText(selectedRecord != null ? $"Delete Run #{selectedRecord.AttemptNumber}" : "Select a run first");

            if (jointClick) {
                if (hovSort) {
                    attemptSortMode = (attemptSortMode + 1) % 3;
                    SpawnShatterParticles(Main.MouseScreen, Color.Orange);
                    SoundEngine.PlaySound(SoundID.MenuTick);
                    jointClick = false;
                }
                else if (hovRefresh) {
                    lastTotalRecordCount = -1;
                    SpawnShatterParticles(Main.MouseScreen, Color.LightSkyBlue);
                    SoundEngine.PlaySound(SoundID.Item4);
                    jointClick = false;
                }
                else if (hovPin) {
                    if (selectedRecord != null) {
                        selectedRecord.IsPinned = !selectedRecord.IsPinned;
                        SpawnShatterParticles(Main.MouseScreen, Color.Yellow);
                        SoundEngine.PlaySound(SoundID.Coins);
                    }
                    jointClick = false;
                }
                else if (hovDel) {
                    if (selectedRecord != null) {
                        string bossName = selectedRecord.BossName; // simpan sebelum hapus
                        modPlayer.BossRecords.Remove(selectedRecord);
                        selectedRecord = null;
                        // --- RENUMBER ulang attempt untuk boss tersebut ---
                        modPlayer.RenumberAttempts(bossName);
                        lastTotalRecordCount = -1; // refresh
                        SpawnShatterParticles(Main.MouseScreen, Color.Red);
                        SoundEngine.PlaySound(SoundID.Item14);
                    }
                    jointClick = false;
                }
            }
        }

        private void DrawPlayerListPanel(SpriteBatch spriteBatch, float x, float y, float w, float h, ref bool jointClick) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle rect = new Rectangle((int)x, (int)y, (int)w, (int)h);
            spriteBatch.Draw(pixel, rect, Color.Gray * 0.15f);

            Rectangle psRect = new Rectangle((int)x + 5, (int)y + 5, (int)w - 10, 24);
            bool hoverPsRect = psRect.Contains(Main.MouseScreen.ToPoint());

            if (jointClick && hoverPsRect) {
                SpawnShatterParticles(Main.MouseScreen, Color.White);
                isTypingPlayerSearch = true; isTypingBossSearch = false;
                jointClick = false;
            } else if (jointClick && !hoverPsRect && isTypingPlayerSearch) {
                isTypingPlayerSearch = false;
                Main.blockInput = false;
            }

            spriteBatch.Draw(pixel, psRect, isTypingPlayerSearch ? Color.DarkCyan * 0.8f : (hoverPsRect ? Color.DarkCyan * 0.6f : Color.DarkCyan * 0.3f));

            string blink = (isTypingPlayerSearch && (cursorBlinkTimer % 40 < 20)) ? "|" : "";
            string disp = string.IsNullOrEmpty(playerSearchFilter) ? "Search Player..." : (playerSearchFilter + blink);
            DrawChatString(spriteBatch, disp, new Vector2(psRect.X + 5, psRect.Y + 4), string.IsNullOrEmpty(playerSearchFilter) ? Color.DarkGray : Color.White, 0.72f);

            List<int> activePlayerIndices = new List<int>();
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (Main.player[i].active && !string.IsNullOrEmpty(Main.player[i].name)) {
                    if (string.IsNullOrEmpty(playerSearchFilter) || Main.player[i].name.ToLower().Contains(playerSearchFilter.ToLower())) {
                        activePlayerIndices.Add(i);
                    }
                }
            }

            DrawChatString(spriteBatch, $"ONLINE ({activePlayerIndices.Count})", new Vector2(x + 8, y + 35), Color.LightSkyBlue, 0.74f);

            int visiblePlayersMax = (int)((h - 60) / 28);
            if (rect.Contains(Main.MouseScreen.ToPoint())) {
                if (PlayerInput.ScrollWheelDelta > 0) playerPanelScroll = Math.Max(0, playerPanelScroll - 1);
                if (PlayerInput.ScrollWheelDelta < 0) playerPanelScroll = Math.Min(Math.Max(0, activePlayerIndices.Count - visiblePlayersMax), playerPanelScroll + 1);
            }

            float rowY = y + 55;
            for (int i = playerPanelScroll; i < Math.Min(activePlayerIndices.Count, playerPanelScroll + visiblePlayersMax); i++) {
                int pIndex = activePlayerIndices[i];
                Player pl = Main.player[pIndex];

                Rectangle pRowRect = new Rectangle((int)x + 5, (int)rowY, (int)w - 10, 26);
                bool hov = pRowRect.Contains(Main.MouseScreen.ToPoint());

                Color nameColor = (pIndex == Main.myPlayer) ? Color.Pink : Color.White;
                if (inspectedPlayerIndex == pIndex) spriteBatch.Draw(pixel, pRowRect, Color.Orange * 0.30f);
                else if (hov) spriteBatch.Draw(pixel, pRowRect, Color.White * 0.10f);

                Main.player[pIndex].PlayerFrame(); 
                Vector2 hairDrawPos = new Vector2(pRowRect.X + 2, pRowRect.Y - 2);
                spriteBatch.Draw(TextureAssets.Players[pl.skinVariant, 0].Value, hairDrawPos, new Rectangle(40, 12, 20, 20), Color.White, 0f, Vector2.Zero, 0.90f, SpriteEffects.None, 0f);
                
                if (pl.hair >= 0 && pl.hair < TextureAssets.PlayerHair.Length) {
                    spriteBatch.Draw(TextureAssets.PlayerHair[pl.hair].Value, hairDrawPos, new Rectangle(40, 12, 20, 20), pl.GetHairColor(false), 0f, Vector2.Zero, 0.90f, SpriteEffects.None, 0f);
                }

                string pNameDisplay = pl.name;
                if (pIndex == Main.myPlayer) pNameDisplay += " (You)";

                DrawChatString(spriteBatch, pNameDisplay, new Vector2(pRowRect.X + 24, pRowRect.Y + 5), nameColor, 0.72f);

                if (hov && jointClick) {
                    inspectedPlayerIndex = pIndex;
                    var targetRecords = Main.player[inspectedPlayerIndex].GetModPlayer<BossStatusPlayer>().BossRecords;
                    if (selectedBossType != -1) {
                        string selectedBossName = Lang.GetNPCNameValue(selectedBossType);
                        if (selectedBossType == NPCID.EaterofWorldsHead) selectedBossName = "Eater of Worlds";
                        if (selectedBossType == NPCID.Retinazer) selectedBossName = "The Twins";
                        if (selectedBossType == NPCID.MoonLordCore) selectedBossName = "Moon Lord";
                        if (selectedBossType == NPCID.Golem) selectedBossName = "Golem";
                        if (selectedBossType == NPCID.SkeletronHead) selectedBossName = "Skeletron";
                        if (selectedBossType == NPCID.SkeletronPrime) selectedBossName = "Skeletron Prime";
                        if (selectedBossType == NPCID.WallofFlesh) selectedBossName = "Wall of Flesh";

                        selectedRecord = targetRecords.LastOrDefault(r => r.BossName == selectedBossName);
                    }
                    
                    SpawnShatterParticles(Main.MouseScreen, nameColor);
                    SoundEngine.PlaySound(SoundID.Item27); 
                    
                    jointClick = false;
                }
                rowY += 28;
            }
        }

        private void DrawGlobalRankPanel(SpriteBatch spriteBatch, float x, float y, float w, float h) {
            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle rect = new Rectangle((int)x, (int)y, (int)w, (int)h);
            spriteBatch.Draw(pixel, rect, Color.Gray * 0.18f);

            DrawChatString(spriteBatch, $"[i:{ItemID.GolfTrophyGold}] GLOBAL RANKING", new Vector2(x + 8, y + 10), Color.LightGoldenrodYellow, 0.76f);
            DrawChatString(spriteBatch, "Universal Best Records", new Vector2(x + 8, y + 26), Color.DarkGray, 0.65f);

            List<int> activePlayerIndices = new List<int>();
            for (int i = 0; i < Main.maxPlayers; i++) {
                if (Main.player[i].active && !string.IsNullOrEmpty(Main.player[i].name)) {
                    activePlayerIndices.Add(i);
                }
            }

            var globalRankings = activePlayerIndices.Select(pIdx => {
                var pl = Main.player[pIdx];
                var recs = pl.GetModPlayer<BossStatusPlayer>().BossRecords;
                int uniqueDefeatedCount = recs.Where(r => r.BossHPPercent == 0).Select(r => r.BossName).Distinct().Count();
                return new { PlayerIndex = pIdx, Name = pl.name, KillCount = uniqueDefeatedCount };
            }).OrderByDescending(g => g.KillCount).ThenBy(g => g.Name).ToList();

            int visibleRanksMax = (int)((h - 50) / 32);
            if (rect.Contains(Main.MouseScreen.ToPoint())) {
                if (PlayerInput.ScrollWheelDelta > 0) globalRankPanelScroll = Math.Max(0, globalRankPanelScroll - 1);
                if (PlayerInput.ScrollWheelDelta < 0) globalRankPanelScroll = Math.Min(Math.Max(0, globalRankings.Count - visibleRanksMax), globalRankPanelScroll + 1);
            }

            float rowY = y + 45;
            for (int i = globalRankPanelScroll; i < Math.Min(globalRankings.Count, globalRankPanelScroll + visibleRanksMax); i++) {
                var rankItem = globalRankings[i];
                Rectangle rowRect = new Rectangle((int)x + 4, (int)rowY, (int)w - 8, 28);

                Color rankColor = Color.White;
                string rankBadge = "";
                
                if (i == 0) { rankColor = Color.Gold; rankBadge = $"[i:{ItemID.GolfTrophyGold}] "; }
                else if (i == 1) { rankColor = Color.Silver; rankBadge = $"[i:{ItemID.GolfTrophySilver}] "; }
                else if (i == 2) { rankColor = Color.Chocolate; rankBadge = $"[i:{ItemID.GolfTrophyBronze}] "; }
                else if (i == globalRankings.Count - 1 && globalRankings.Count > 3) { rankColor = Color.Red; rankBadge = $"[i:{ItemID.Tombstone}] "; }
                else { rankBadge = $"#{i + 1} "; }

                spriteBatch.Draw(pixel, rowRect, Color.Black * 0.35f);
                
                string pNameDisp = rankItem.Name;
                if (pNameDisp.Length > 11) pNameDisp = pNameDisp.Substring(0, 9) + "..";
                
                DrawChatString(spriteBatch, $"{rankBadge}{pNameDisp}", new Vector2(rowRect.X + 4, rowRect.Y + 6), rankColor, 0.70f);
                DrawChatString(spriteBatch, $"{rankItem.KillCount} Bosses", new Vector2(rowRect.X + w - 58, rowRect.Y + 6), Color.LightGreen, 0.68f);

                rowY += 32;
            }
        }
    }
}