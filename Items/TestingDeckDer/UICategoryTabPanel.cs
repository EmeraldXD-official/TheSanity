using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace TheSanity.Items.TestingDeckDer
{
    /// <summary>
    /// Hosts the category tabs ("All", "Vanilla", "[ModName]", ...) in a
    /// wrapping "tag" layout: tabs are placed left-to-right and automatically
    /// continue on the next row when they don't fit anymore, instead of
    /// scrolling sideways.
    ///
    /// Every tab's Left/Top/Width/Height is recomputed every frame from
    /// scratch, so the drawn box and the clickable hitbox can never drift
    /// apart - this is what was causing the right-most tab to become
    /// unclickable when the bar got long under the old horizontal-scroll
    /// approach.
    ///
    /// Vertical overflow (many mods -> many rows) is handled with a normal
    /// <see cref="UIScrollbar"/>, the exact same scrollbar type already used
    /// (and working fine) for the projectile list below it.
    /// </summary>
    public class UICategoryTabPanel : UIElement
    {
        private readonly UIElement content;
        private readonly List<UIElement> tabs = new List<UIElement>();
        private float contentHeight;

        private UIScrollbar linkedScrollbar;

        private const float TabSpacingX = 6f;
        private const float TabSpacingY = 6f;
        private const float TabHeight = 26f;

        public UICategoryTabPanel()
        {
            OverflowHidden = true;

            content = new UIElement();
            content.Width.Set(0f, 1f);
            content.Height.Set(0f, 0f);
            content.HAlign = 0f;
            Append(content);
        }

        /// <summary>
        /// Links a visible vertical scrollbar to this panel, same pattern as
        /// UIList.SetScrollbar.
        /// </summary>
        public void SetScrollbar(UIScrollbar bar)
        {
            linkedScrollbar = bar;
        }

        public void ClearTabs()
        {
            content.RemoveAllChildren();
            tabs.Clear();
            contentHeight = 0f;
            content.Top.Set(0f, 0f);
        }

        public void AddTab(UIElement tab, float width)
        {
            tab.Width.Set(width, 0f);
            tab.Height.Set(TabHeight, 0f);
            tabs.Add(tab);
            content.Append(tab);
        }

        /// <summary>
        /// Recomputes the wrap layout of every tab based on the panel's
        /// current visible width. Safe to call every frame - cheap for the
        /// handful of tabs this bar ever holds - so it always stays correct
        /// even if the parent window gets resized via the resize handle.
        /// </summary>
        public void LayoutTabs()
        {
            float visibleWidth = GetDimensions().Width;
            if (visibleWidth <= 0f || tabs.Count == 0)
            {
                contentHeight = 0f;
                content.Height.Set(0f, 0f);
                SyncScrollbar();
                return;
            }

            float x = 0f;
            float y = 0f;

            foreach (UIElement tab in tabs)
            {
                float tabWidth = tab.Width.Pixels;

                if (x > 0f && x + tabWidth > visibleWidth)
                {
                    // Doesn't fit anymore on this row -> wrap to the next one.
                    x = 0f;
                    y += TabHeight + TabSpacingY;
                }

                tab.Left.Set(x, 0f);
                tab.Top.Set(y, 0f);
                tab.Recalculate();

                x += tabWidth + TabSpacingX;
            }

            contentHeight = y + TabHeight;
            content.Height.Set(contentHeight, 0f);
            content.Recalculate();

            SyncScrollbar();
        }

        private void SyncScrollbar()
        {
            if (linkedScrollbar == null) return;

            float visibleHeight = GetDimensions().Height;
            linkedScrollbar.SetView(visibleHeight, contentHeight);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // Re-validate every frame in case the host panel was resized,
            // which changes the visible width without us otherwise being
            // notified.
            LayoutTabs();

            if (ContainsPoint(Main.MouseScreen))
            {
                Main.LocalPlayer.mouseInterface = true;
            }

            if (linkedScrollbar != null)
            {
                content.Top.Set(-linkedScrollbar.ViewPosition, 0f);
                content.Recalculate();
            }
        }
    }
}
