using Microsoft.Xna.Framework;
using Terraria;
using Terraria.UI;

namespace TheSanity.Items.TestingDeckDer
{
    /// <summary>
    /// A thin horizontally-scrollable strip used to host the category tabs
    /// ("All", "Vanilla", "[ModName]", ...). Supports scrolling with the
    /// mouse wheel and by click-dragging, similar to a "sorting/category" bar.
    /// </summary>
    public class UIHorizontalScrollPanel : UIElement
    {
        private readonly UIElement content;
        private float contentWidth;
        private float scrollOffset;

        private bool isDragging;
        private float dragStartMouseX;
        private float dragStartOffset;

        private UIHorizontalScrollbar linkedScrollbar;

        public UIHorizontalScrollPanel()
        {
            OverflowHidden = true;

            content = new UIElement();
            content.Width.Set(0f, 0f);
            content.Height.Set(0f, 1f);
            Append(content);
        }

        /// <summary>
        /// Links a visible scrollbar control to this panel: dragging/clicking
        /// the scrollbar moves this panel's scroll, and this panel keeps the
        /// scrollbar's thumb size/position in sync.
        /// </summary>
        public void SetScrollbar(UIHorizontalScrollbar bar)
        {
            linkedScrollbar = bar;
            if (linkedScrollbar != null)
                linkedScrollbar.OnScroll += HandleScrollbarInput;
        }

        private void HandleScrollbarInput(float fraction)
        {
            float visibleWidth = GetDimensions().Width;
            float maxScroll = MathHelper.Max(0f, contentWidth - visibleWidth);
            scrollOffset = fraction * maxScroll;
            ClampAndApplyScroll();
        }

        public void ClearTabs()
        {
            content.RemoveAllChildren();
            contentWidth = 0f;
            scrollOffset = 0f;
            content.Left.Set(0f, 0f);
        }

        public void AddTab(UIElement tab, float width)
        {
            tab.Left.Set(contentWidth, 0f);
            tab.Width.Set(width, 0f);
            tab.Height.Set(0f, 1f);
            content.Append(tab);

            contentWidth += width + 6f; // spacing between chips
            content.Width.Set(contentWidth, 0f);
        }

        public override void ScrollWheel(UIScrollWheelEvent evt)
        {
            base.ScrollWheel(evt);
            scrollOffset -= evt.ScrollWheelValue * 0.4f;
            ClampAndApplyScroll();
        }

        public override void LeftMouseDown(UIMouseEvent evt)
        {
            base.LeftMouseDown(evt);
            isDragging = true;
            dragStartMouseX = Main.MouseScreen.X;
            dragStartOffset = scrollOffset;
        }

        public override void LeftMouseUp(UIMouseEvent evt)
        {
            base.LeftMouseUp(evt);
            isDragging = false;
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Lock the world/mouse interface while hovering the bar so clicks
            // and drags don't leak through to the game world.
            if (ContainsPoint(Main.MouseScreen))
            {
                Main.LocalPlayer.mouseInterface = true;
            }

            if (isDragging)
            {
                if (!Main.mouseLeft)
                {
                    isDragging = false;
                }
                else
                {
                    float delta = dragStartMouseX - Main.MouseScreen.X;
                    scrollOffset = dragStartOffset + delta;
                    ClampAndApplyScroll();
                }
            }

            // Re-validate every frame in case the host panel was resized
            // (e.g. via the resize handle), which changes the visible width
            // without us otherwise being notified.
            ClampAndApplyScroll();
        }

        private void ClampAndApplyScroll()
        {
            float visibleWidth = GetDimensions().Width;
            float maxScroll = MathHelper.Max(0f, contentWidth - visibleWidth);
            scrollOffset = MathHelper.Clamp(scrollOffset, 0f, maxScroll);
            content.Left.Set(-scrollOffset, 0f);
            content.Recalculate();

            linkedScrollbar?.SyncView(visibleWidth, contentWidth, scrollOffset);
        }
    }
}
