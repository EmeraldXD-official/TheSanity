using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace TheSanity.Items.TestingDeckDer
{
    /// <summary>
    /// A visible, always-present horizontal scrollbar (track + draggable thumb)
    /// for <see cref="UIHorizontalScrollPanel"/>. Acts as a reliable fallback
    /// in case mouse-wheel or click-drag scrolling doesn't work for a given
    /// player/setup - the thumb can always be dragged, or the track clicked,
    /// to reach any tab.
    /// </summary>
    public class UIHorizontalScrollbar : UIElement
    {
        public event Action<float> OnScroll; // fires with a 0..1 scroll fraction

        private readonly UIPanel track;
        private readonly UIPanel thumb;

        private bool isDragging;
        private float dragStartMouseX;
        private float dragStartThumbLeft;

        private const float MinThumbWidth = 24f;

        public UIHorizontalScrollbar()
        {
            Width.Set(0f, 1f);
            Height.Set(10f, 0f);

            track = new UIPanel();
            track.Width.Set(0f, 1f);
            track.Height.Set(0f, 1f);
            track.SetPadding(0f);
            track.BackgroundColor = new Color(20, 20, 36);
            track.BorderColor = new Color(60, 60, 90);
            track.OnLeftClick += TrackClick;
            Append(track);

            thumb = new UIPanel();
            thumb.Height.Set(0f, 1f);
            thumb.Width.Set(MinThumbWidth, 0f);
            thumb.SetPadding(0f);
            thumb.BackgroundColor = new Color(110, 100, 160);
            thumb.BorderColor = new Color(170, 150, 230);
            thumb.OnLeftMouseDown += ThumbDragStart;
            thumb.OnLeftMouseUp += (evt, el) => isDragging = false;
            thumb.OnMouseOver += (evt, el) => thumb.BackgroundColor = new Color(135, 120, 190);
            thumb.OnMouseOut += (evt, el) => { if (!isDragging) thumb.BackgroundColor = new Color(110, 100, 160); };
            track.Append(thumb);
        }

        private void ThumbDragStart(UIMouseEvent evt, UIElement element)
        {
            isDragging = true;
            dragStartMouseX = Main.MouseScreen.X;
            dragStartThumbLeft = thumb.Left.Pixels;
        }

        private void TrackClick(UIMouseEvent evt, UIElement element)
        {
            if (evt.Target != track) return; // ignore clicks that landed on the thumb itself

            float trackWidth = track.GetDimensions().Width;
            float thumbWidth = thumb.GetDimensions().Width;
            float clickX = evt.MousePosition.X - track.GetDimensions().X;
            float maxThumbLeft = MathHelper.Max(0f, trackWidth - thumbWidth);
            float targetLeft = MathHelper.Clamp(clickX - thumbWidth / 2f, 0f, maxThumbLeft);

            SetThumbLeft(targetLeft, maxThumbLeft);
            OnScroll?.Invoke(maxThumbLeft > 0f ? targetLeft / maxThumbLeft : 0f);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

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
                    float trackWidth = track.GetDimensions().Width;
                    float thumbWidth = thumb.GetDimensions().Width;
                    float maxThumbLeft = MathHelper.Max(0f, trackWidth - thumbWidth);
                    float delta = Main.MouseScreen.X - dragStartMouseX;
                    float newLeft = MathHelper.Clamp(dragStartThumbLeft + delta, 0f, maxThumbLeft);

                    SetThumbLeft(newLeft, maxThumbLeft);
                    OnScroll?.Invoke(maxThumbLeft > 0f ? newLeft / maxThumbLeft : 0f);
                }
            }
        }

        private void SetThumbLeft(float left, float maxThumbLeft)
        {
            thumb.Left.Set(MathHelper.Clamp(left, 0f, MathHelper.Max(0f, maxThumbLeft)), 0f);
            thumb.Recalculate();
        }

        /// <summary>
        /// Called by the owning scroll panel to keep the thumb's size and
        /// position in sync with the actual content/scroll state.
        /// </summary>
        public void SyncView(float visibleWidth, float contentWidth, float currentScrollOffset)
        {
            float trackWidth = track.GetDimensions().Width;
            if (trackWidth <= 0f) trackWidth = GetDimensions().Width;
            if (trackWidth <= 0f) return;

            float viewFraction = contentWidth > 0f ? MathHelper.Clamp(visibleWidth / contentWidth, 0f, 1f) : 1f;
            float thumbWidth = MathHelper.Clamp(trackWidth * viewFraction, MinThumbWidth, trackWidth);
            thumb.Width.Set(thumbWidth, 0f);

            // Hide the scrollbar entirely (thumb fills the track) when there's nothing to scroll.
            bool needsScrolling = contentWidth > visibleWidth + 0.5f;
            thumb.BorderColor = needsScrolling ? new Color(170, 150, 230) : new Color(60, 60, 90);

            if (isDragging) return; // don't fight the player's own drag mid-frame

            float maxScroll = MathHelper.Max(0f, contentWidth - visibleWidth);
            float fraction = maxScroll > 0f ? MathHelper.Clamp(currentScrollOffset / maxScroll, 0f, 1f) : 0f;
            float maxThumbLeft = MathHelper.Max(0f, trackWidth - thumbWidth);

            thumb.Left.Set(maxThumbLeft * fraction, 0f);
            thumb.Recalculate();
        }
    }
}
