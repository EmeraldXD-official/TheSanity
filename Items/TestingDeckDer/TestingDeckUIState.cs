using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.Items.TestingDeckDer
{
    public class TestingDeckUIState : UIState
    {
        private UIPanel mainPanel;

        private UIPanel titleBar;
        private UIText titleText;
        private UIPanel closeButton;

        private UIList projectileList;
        private UIScrollbar scrollbar;
        private UISearchBar searchBar;
        private UICategoryTabPanel categoryBar;
        private UIScrollbar categoryScrollbar;
        private readonly List<UICategoryTab> categoryTabs = new List<UICategoryTab>();
        private string selectedCategory = "All";

        private UIPanel resizeHandle;

        private bool isDragging;
        private Vector2 dragOffset;

        private bool isResizing;
        private Vector2 startMousePos;
        private Vector2 startSize;

        private const float MinWidth = 350f;
        private const float MinHeight = 520f;
        private const float MaxWidth = 900f;
        private const float MaxHeight = 950f;

        public override void OnInitialize()
        {
            mainPanel = new UIPanel();
            mainPanel.Width.Set(460f, 0f);
            mainPanel.Height.Set(645f, 0f);
            mainPanel.Left.Set(80f, 0f);
            mainPanel.Top.Set(150f, 0f);
            mainPanel.BackgroundColor = new Color(30, 30, 50, 240);
            mainPanel.SetPadding(0f);
            Append(mainPanel);

            // =========================================================================
            // TITLE BAR (drag handle + close button)
            // =========================================================================
            titleBar = new UIPanel();
            titleBar.Width.Set(0f, 1f);
            titleBar.Height.Set(28f, 0f);
            titleBar.Left.Set(0f, 0f);
            titleBar.Top.Set(0f, 0f);
            titleBar.BackgroundColor = new Color(20, 20, 36, 255);
            titleBar.BorderColor = new Color(80, 80, 120);
            titleBar.SetPadding(0f);
            mainPanel.Append(titleBar);

            titleText = new UIText("Testing Deck", 0.9f);
            titleText.Left.Set(10f, 0f);
            titleText.VAlign = 0.5f;
            titleText.TextColor = Color.White;
            titleBar.Append(titleText);

            closeButton = new UIPanel();
            closeButton.Width.Set(24f, 0f);
            closeButton.Height.Set(20f, 0f);
            closeButton.Left.Set(-28f, 1f);
            closeButton.VAlign = 0.5f;
            closeButton.BackgroundColor = new Color(160, 35, 35, 230);
            closeButton.BorderColor = new Color(220, 60, 60);
            closeButton.SetPadding(0f);
            var closeText = new UIText("x", 0.85f) { HAlign = 0.5f, VAlign = 0.5f, TextColor = Color.White };
            closeButton.Append(closeText);
            closeButton.OnMouseOver += (evt, el) => closeButton.BackgroundColor = new Color(200, 45, 45, 240);
            closeButton.OnMouseOut += (evt, el) => closeButton.BackgroundColor = new Color(160, 35, 35, 230);
            closeButton.OnLeftClick += (evt, el) => TestingDeckSystem.ToggleUI();
            titleBar.Append(closeButton);

            // Dragging is allowed when clicking the title bar itself or its label,
            // but not when clicking the close button.
            titleBar.OnLeftMouseDown += (evt, el) =>
            {
                if (evt.Target == titleBar || evt.Target == titleText)
                {
                    isDragging = true;
                    dragOffset = Main.MouseScreen - new Vector2(mainPanel.Left.Pixels, mainPanel.Top.Pixels);
                }
            };
            titleBar.OnLeftMouseUp += (evt, el) => isDragging = false;

            // =========================================================================
            // SEARCH BAR
            // =========================================================================
            searchBar = new UISearchBar("Type a projectile name to search...");
            searchBar.Width.Set(-20f, 1f);
            searchBar.Left.Set(10f, 0f);
            searchBar.Top.Set(36f, 0f);
            searchBar.OnContentsChanged += (text) => RefreshList();
            mainPanel.Append(searchBar);

            // =========================================================================
            // CATEGORY BAR (wrapping tabs: All / Vanilla / [ModName] ...)
            // Tabs flow left-to-right and wrap to a new row instead of
            // scrolling sideways, so every tab's hitbox always matches
            // exactly where it's drawn - no more unclickable tabs on the
            // right edge. Overflow (many rows) scrolls vertically with the
            // same UIScrollbar type used by the projectile list below.
            // =========================================================================
            categoryBar = new UICategoryTabPanel();
            categoryBar.Width.Set(-30f, 1f);
            categoryBar.Height.Set(64f, 0f);
            categoryBar.Left.Set(10f, 0f);
            categoryBar.Top.Set(74f, 0f);
            mainPanel.Append(categoryBar);

            categoryScrollbar = new UIScrollbar();
            categoryScrollbar.Height.Set(64f, 0f);
            categoryScrollbar.Top.Set(74f, 0f);
            categoryScrollbar.HAlign = 1f;
            mainPanel.Append(categoryScrollbar);
            categoryBar.SetScrollbar(categoryScrollbar);

            // =========================================================================
            // PROJECTILE LIST + SCROLLBAR
            // =========================================================================
            projectileList = new UIList();
            projectileList.Width.Set(-30f, 1f);
            projectileList.Height.Set(-202f, 1f);
            projectileList.Left.Set(10f, 0f);
            projectileList.Top.Set(146f, 0f);
            projectileList.ListPadding = 5f;
            mainPanel.Append(projectileList);

            scrollbar = new UIScrollbar();
            scrollbar.Height.Set(-202f, 1f);
            scrollbar.Top.Set(146f, 0f);
            scrollbar.HAlign = 1f;
            mainPanel.Append(scrollbar);
            projectileList.SetScrollbar(scrollbar);

            // =========================================================================
            // RED "CLEAR ALL" BUTTON
            // =========================================================================
            var clearButton = new UIPanel();
            clearButton.Width.Set(-20f, 1f);
            clearButton.Height.Set(36f, 0f);
            clearButton.Left.Set(10f, 0f);
            clearButton.Top.Set(-46f, 1f);
            clearButton.BackgroundColor = new Color(160, 35, 35, 230);
            clearButton.BorderColor = new Color(220, 60, 60);

            clearButton.OnMouseOver += (evt, element) => clearButton.BackgroundColor = new Color(200, 45, 45, 240);
            clearButton.OnMouseOut += (evt, element) => clearButton.BackgroundColor = new Color(160, 35, 35, 230);

            var clearText = new UIText("CLEAR ALL ACTIVE PROJECTILES & STATES", 0.85f);
            clearText.HAlign = 0.5f;
            clearText.VAlign = 0.5f;
            clearText.TextColor = Color.White;
            clearButton.Append(clearText);

            clearButton.OnLeftClick += (evt, element) =>
            {
                int killedCount = 0;
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    if (Main.projectile[i].active)
                    {
                        Main.projectile[i].active = false;
                        Main.projectile[i].type = 0;
                        killedCount++;
                    }
                }
                Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.NPCDeath10);
                Main.NewText($"Done! Cleared {killedCount} active projectile(s) and looped states.", Color.GreenYellow);
            };
            mainPanel.Append(clearButton);

            // =========================================================================
            // RESIZE HANDLE (bottom-right corner)
            // =========================================================================
            resizeHandle = new UIPanel();
            resizeHandle.Width.Set(14f, 0f);
            resizeHandle.Height.Set(14f, 0f);
            resizeHandle.Left.Set(-14f, 1f);
            resizeHandle.Top.Set(-14f, 1f);
            resizeHandle.BackgroundColor = new Color(100, 100, 150, 200);
            resizeHandle.BorderColor = Color.Transparent;

            resizeHandle.OnLeftMouseDown += ResizeStart;
            resizeHandle.OnLeftMouseUp += ResizeEnd;
            mainPanel.Append(resizeHandle);

            BuildCategoryTabs();
            RefreshList();
        }

        #region Category bar logic
        private void BuildCategoryTabs()
        {
            categoryBar.ClearTabs();
            categoryTabs.Clear();

            // Count how many projectiles each origin actually has, so a
            // category tab only ever gets created when its count is > 0.
            // This keeps the tab bar perfectly in sync with what RefreshList
            // can actually show - no empty/"ghost" categories.
            var modCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var modDisplayNames = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            int vanillaCount = 0;

            for (int i = 1; i < ProjectileLoader.ProjectileCount; i++)
            {
                ModProjectile modProj = ModContent.GetModProjectile(i);
                if (modProj != null)
                {
                    string modName = modProj.Mod?.Name;
                    if (string.IsNullOrWhiteSpace(modName))
                        continue; // safety guard against odd/unloaded mod state

                    modCounts.TryGetValue(modName, out int count);
                    modCounts[modName] = count + 1;
                    modDisplayNames[modName] = modName;
                }
                else
                {
                    vanillaCount++;
                }
            }

            AddCategoryTab("All", "All");

            if (vanillaCount > 0)
                AddCategoryTab("Vanilla", "Vanilla");

            // Pin this mod's own tab (TheSanity) right after Vanilla so it's
            // always reachable without scrolling, regardless of alphabetical
            // order among other mods.
            string ownMod = TestingDeckSystem.OwnModName;
            if (!string.IsNullOrEmpty(ownMod) && modCounts.TryGetValue(ownMod, out int ownCount) && ownCount > 0)
            {
                AddCategoryTab(ownMod, ownMod);
            }

            var sortedModNames = new List<string>(modDisplayNames.Values);
            sortedModNames.Sort(StringComparer.OrdinalIgnoreCase);

            foreach (string modName in sortedModNames)
            {
                if (string.Equals(modName, ownMod, StringComparison.OrdinalIgnoreCase))
                    continue; // already added above, right after Vanilla

                if (modCounts.TryGetValue(modName, out int count) && count > 0)
                    AddCategoryTab(modName, modName);
            }

            UpdateCategorySelectionVisuals();
            categoryBar.LayoutTabs();
        }

        private void AddCategoryTab(string id, string displayText)
        {
            var tab = new UICategoryTab(id, displayText);
            tab.OnSelected += SelectCategory;
            categoryTabs.Add(tab);
            categoryBar.AddTab(tab, UICategoryTab.MeasureWidth(displayText));
        }

        private void SelectCategory(string categoryId)
        {
            if (selectedCategory == categoryId) return;
            selectedCategory = categoryId;
            UpdateCategorySelectionVisuals();
            RefreshList();
        }

        private void UpdateCategorySelectionVisuals()
        {
            foreach (var tab in categoryTabs)
                tab.SetSelected(tab.CategoryId == selectedCategory);
        }
        #endregion

        #region Drag & Resize logic
        private void ResizeStart(UIMouseEvent evt, UIElement element)
        {
            isResizing = true;
            startMousePos = Main.MouseScreen;
            startSize = new Vector2(mainPanel.Width.Pixels, mainPanel.Height.Pixels);
        }

        private void ResizeEnd(UIMouseEvent evt, UIElement element)
        {
            isResizing = false;
        }
        #endregion

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Lock world mouse interaction while hovering the panel so clicks
            // don't leak through to the game world.
            if (mainPanel != null && mainPanel.ContainsPoint(Main.MouseScreen))
            {
                Main.LocalPlayer.mouseInterface = true;
            }

            if (isDragging)
            {
                mainPanel.Left.Set(Main.MouseScreen.X - dragOffset.X, 0f);
                mainPanel.Top.Set(Main.MouseScreen.Y - dragOffset.Y, 0f);
                mainPanel.Recalculate();
            }

            if (isResizing)
            {
                Vector2 mouseDelta = Main.MouseScreen - startMousePos;

                float newWidth = MathHelper.Clamp(startSize.X + mouseDelta.X, MinWidth, MaxWidth);
                float newHeight = MathHelper.Clamp(startSize.Y + mouseDelta.Y, MinHeight, MaxHeight);

                mainPanel.Width.Set(newWidth, 0f);
                mainPanel.Height.Set(newHeight, 0f);
                mainPanel.Recalculate();
            }

            if (!Main.mouseLeft)
            {
                isDragging = false;
                isResizing = false;
            }
        }

        public override void OnDeactivate()
        {
            base.OnDeactivate();
            // Reset the typing focus state when the UI is force-closed.
            searchBar?.ResetTypingState();
        }

        public void RefreshList()
        {
            if (projectileList == null) return;
            projectileList.Clear();

            string query = (searchBar?.CurrentString ?? "").Trim().ToLowerInvariant();

            for (int i = 1; i < ProjectileLoader.ProjectileCount; i++)
            {
                string projName = Lang.GetProjectileName(i).Value;
                ModProjectile modProj = ModContent.GetModProjectile(i);

                if (string.IsNullOrEmpty(projName))
                {
                    projName = modProj != null ? modProj.Name : $"Unknown #{i}";
                }

                if (!string.IsNullOrEmpty(query) && !projName.ToLowerInvariant().Contains(query))
                    continue;

                if (selectedCategory != "All")
                {
                    string origin = modProj != null ? modProj.Mod.Name : "Vanilla";
                    if (!string.Equals(origin, selectedCategory, StringComparison.OrdinalIgnoreCase))
                        continue;
                }

                projectileList.Add(new UIProjectileEntry(i, projName));
            }
        }
    }
}
