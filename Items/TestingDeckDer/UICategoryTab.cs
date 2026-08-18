using System;
using Microsoft.Xna.Framework;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;

namespace TheSanity.Items.TestingDeckDer
{
    /// <summary>
    /// A single selectable "chip" used inside the horizontal category bar
    /// (e.g. "All", "Vanilla", "[SomeModName]").
    /// </summary>
    public class UICategoryTab : UIPanel
    {
        public readonly string CategoryId;
        public event Action<string> OnSelected;

        private bool isSelected;
        private readonly UIText label;

        private static readonly Color NormalBg = new Color(40, 40, 60);
        private static readonly Color HoverBg = new Color(70, 70, 110);
        private static readonly Color SelectedBg = new Color(95, 70, 150);
        private static readonly Color NormalBorder = new Color(80, 80, 120);
        private static readonly Color SelectedBorder = new Color(175, 145, 235);

        private const float HorizontalPadding = 24f;

        public UICategoryTab(string categoryId, string displayText)
        {
            CategoryId = categoryId;

            SetPadding(0f);
            BackgroundColor = NormalBg;
            BorderColor = NormalBorder;

            label = new UIText(displayText, 0.75f)
            {
                HAlign = 0.5f,
                VAlign = 0.5f
            };
            label.TextColor = Color.White;
            Append(label);

            OnMouseOver += (evt, el) => { if (!isSelected) BackgroundColor = HoverBg; };
            OnMouseOut += (evt, el) => { if (!isSelected) BackgroundColor = NormalBg; };
            OnLeftClick += (evt, el) =>
            {
                Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.MenuTick);
                OnSelected?.Invoke(CategoryId);
            };
        }

        public void SetSelected(bool selected)
        {
            isSelected = selected;
            BackgroundColor = selected ? SelectedBg : NormalBg;
            BorderColor = selected ? SelectedBorder : NormalBorder;
        }

        /// <summary>
        /// Measures the exact pixel width this tab needs for its label, so
        /// the clickable hitbox always matches the visible text width - no
        /// truncation, no mismatch between what's drawn and what's clickable.
        /// </summary>
        public static float MeasureWidth(string displayText)
        {
            float textWidth = FontAssets.MouseText.Value.MeasureString(displayText ?? "").X * 0.75f;
            return textWidth + HorizontalPadding;
        }
    }
}
