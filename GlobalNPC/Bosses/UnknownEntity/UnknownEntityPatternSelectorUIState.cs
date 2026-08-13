using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;
using Terraria.UI;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    // Panel kecil draggable buat "drag handle" di bagian atas UI - biar user bisa geser
    // posisi panelnya tanpa harus nge-drag dari area tombol (yang bakal ketuker sama klik tombol).
    internal class UIDragHandle : UIPanel
    {
        private readonly UIPanel target;
        private bool dragging;
        private Vector2 dragOffset;

        public UIDragHandle(UIPanel targetPanel)
        {
            target = targetPanel;
            SetPadding(0f);
        }

        public override void LeftMouseDown(UIMouseEvent evt)
        {
            base.LeftMouseDown(evt);
            dragging = true;
            dragOffset = new Vector2(evt.MousePosition.X - target.Left.Pixels, evt.MousePosition.Y - target.Top.Pixels);
        }

        public override void LeftMouseUp(UIMouseEvent evt)
        {
            base.LeftMouseUp(evt);
            dragging = false;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            if (dragging)
            {
                if (Main.mouseLeft)
                {
                    target.Left.Set(Main.mouseX - dragOffset.X, 0f);
                    target.Top.Set(Main.mouseY - dragOffset.Y, 0f);
                    target.Recalculate();
                }
                else
                {
                    dragging = false;
                }
            }
        }
    }

    public class UnknownEntityPatternSelectorUIState : UIState
    {
        public bool Visible = false;

        private UIPanel mainPanel;
        private UIList attackList;
        private UIText selectedLabel;

        private static readonly Color NormalColorPhase1 = new Color(35, 55, 90, 235);
        private static readonly Color NormalColorPhase2 = new Color(80, 30, 90, 235);
        private static readonly Color SelectedBorder = new Color(255, 220, 120);
        private static readonly Color NormalBorder = new Color(120, 120, 160);

        public override void OnInitialize()
        {
            mainPanel = new UIPanel();
            mainPanel.Width.Set(360f, 0f);
            mainPanel.Height.Set(440f, 0f);
            mainPanel.Left.Set(Main.screenWidth / 2f - 180f, 0f);
            mainPanel.Top.Set(Main.screenHeight / 2f - 220f, 0f);
            mainPanel.BackgroundColor = new Color(25, 15, 45, 235);
            mainPanel.BorderColor = new Color(150, 90, 255);
            mainPanel.SetPadding(6f);
            Append(mainPanel);

            // ---- Drag handle di header ----
            var header = new UIDragHandle(mainPanel);
            header.Width.Set(0f, 1f);
            header.Height.Set(34f, 0f);
            header.BackgroundColor = new Color(60, 30, 100, 255);
            header.BorderColor = new Color(150, 90, 255);
            mainPanel.Append(header);

            var title = new UIText("Unknown Entity - Pilih Pola", 0.85f)
            {
                HAlign = 0.5f,
                VAlign = 0.5f
            };
            header.Append(title);

            var closeButton = new UITextPanel<string>("X", 0.8f, false);
            closeButton.Width.Set(24f, 0f);
            closeButton.Height.Set(24f, 0f);
            closeButton.Left.Set(-28f, 1f);
            closeButton.Top.Set(5f, 0f);
            closeButton.BackgroundColor = new Color(120, 30, 30);
            closeButton.OnLeftClick += (evt, el) => Hide();
            header.Append(closeButton);

            // ---- Label penunjuk pilihan sekarang ----
            selectedLabel = new UIText("", 0.75f) { HAlign = 0.5f };
            selectedLabel.Top.Set(40f, 0f);
            selectedLabel.Width.Set(0f, 1f);
            mainPanel.Append(selectedLabel);

            // ---- List scrollable berisi tombol tiap pola serangan ----
            attackList = new UIList();
            attackList.Width.Set(-25f, 1f);
            attackList.Height.Set(-88f, 1f);
            attackList.Top.Set(64f, 0f);
            attackList.Left.Set(0f, 0f);
            attackList.ListPadding = 5f;
            mainPanel.Append(attackList);

            var scrollbar = new UIScrollbar();
            scrollbar.Height.Set(-88f, 1f);
            scrollbar.Top.Set(64f, 0f);
            scrollbar.HAlign = 1f;
            mainPanel.Append(scrollbar);
            attackList.SetScrollbar(scrollbar);

            RebuildList();
        }

        private void RebuildList()
        {
            attackList.Clear();

            foreach (UnknownEntity.AIState state in PatternSelectorHelper.SelectableStates)
            {
                bool isPhase2 = PatternSelectorHelper.Phase2States.Contains(state);
                string label = PatternSelectorHelper.DisplayName(state) + (isPhase2 ? "  [Phase 2]" : "  [Phase 1]");

                var button = new UITextPanel<string>(label, 0.75f, false);
                button.Width.Set(0f, 1f);
                button.Height.Set(36f, 0f);
                button.SetPadding(6f);
                button.BackgroundColor = isPhase2 ? NormalColorPhase2 : NormalColorPhase1;
                button.BorderColor = state == PatternSelectorHelper.SelectedState ? SelectedBorder : NormalBorder;

                UnknownEntity.AIState capturedState = state;
                button.OnLeftClick += (evt, el) =>
                {
                    int idx = Array.IndexOf(PatternSelectorHelper.SelectableStates, capturedState);
                    if (idx >= 0) PatternSelectorHelper.SelectedIndex = idx;

                    PatternSelectorHelper.ForcePattern(capturedState);
                    RefreshSelectedVisuals();
                };

                attackList.Add(button);
            }

            RefreshSelectedVisuals();
        }

        // Update border tiap tombol biar cuma yang lagi kepilih yang nyala kuning, sisanya abu-abu.
        private void RefreshSelectedVisuals()
        {
            selectedLabel.SetText($"Pilihan sekarang: {PatternSelectorHelper.DisplayName(PatternSelectorHelper.SelectedState)}");

            foreach (UIElement element in attackList._items)
            {
                if (element is UITextPanel<string> panel)
                {
                    int index = attackList._items.IndexOf(element);
                    bool selected = index == PatternSelectorHelper.SelectedIndex;
                    panel.BorderColor = selected ? SelectedBorder : NormalBorder;
                }
            }
        }

        public override void OnActivate()
        {
            RefreshSelectedVisuals();
        }

        public void Show()
        {
            Visible = true;
            RefreshSelectedVisuals();
        }

        public void Hide()
        {
            Visible = false;
            ModContent.GetInstance<UnknownEntityPatternSelectorUISystem>().HideUI();
        }
    }
}
