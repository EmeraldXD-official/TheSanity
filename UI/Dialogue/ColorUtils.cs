using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace TheSanity.UI.DialogueSystem
{
    /// <summary>
    /// Util buat fitur "palet warna": hook cukup kasih 1 warna (nama kayak "Red", atau RGB langsung),
    /// terus SELURUH warna tema di-geser Hue-nya ke warna itu sambil Saturation &amp; Lightness asli
    /// tiap elemen tema tetap dipertahankan. Jadi tema "Dark Retro" + palet "Red" hasilnya
    /// "Dark Red" (tetap gelap kayak Dark Retro), bukan merah nyala yang norak nempel di panel gelap.
    /// </summary>
    public static class ColorUtils
    {
        public static readonly Dictionary<string, Color> NamedColors = new Dictionary<string, Color>(StringComparer.OrdinalIgnoreCase)
        {
            { "Red", Color.Red }, { "Orange", Color.Orange }, { "Yellow", Color.Yellow },
            { "Green", Color.Green }, { "Blue", Color.Blue }, { "Purple", Color.Purple },
            { "Pink", Color.Pink }, { "Cyan", Color.Cyan }, { "Teal", Color.Teal },
            { "White", Color.White }, { "Black", Color.Black }, { "Gray", Color.Gray },
            { "Brown", Color.Brown }, { "Gold", Color.Gold }, { "Silver", Color.Silver },
            { "Magenta", Color.Magenta }, { "Lime", Color.Lime }, { "Indigo", Color.Indigo },
        };

        /// <summary>Ambil Color dari nama ("Red","Blue", dst - case-insensitive). Null kalau nama ga dikenal.</summary>
        public static Color? FromName(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return NamedColors.TryGetValue(name.Trim(), out Color c) ? c : (Color?)null;
        }

        /// <summary>Geser Hue 1 warna ke arah targetHueSource, Saturation & Lightness asli dipertahankan.</summary>
        public static Color ApplyHue(Color original, Color targetHueSource)
        {
            RgbToHsl(targetHueSource, out float targetH, out _, out _);
            RgbToHsl(original, out _, out float origS, out float origL);
            Color result = HslToRgb(targetH, origS, origL);
            result.A = original.A;
            return result;
        }

        /// <summary>Terapkan 1 warna palet ke SELURUH slot warna sebuah tema sekaligus.</summary>
        public static DialogueTheme ApplyPalette(DialogueTheme theme, Color targetColor)
        {
            theme.BackgroundColor = ApplyHue(theme.BackgroundColor, targetColor);
            theme.OutlineColor = ApplyHue(theme.OutlineColor, targetColor);
            theme.IconBackgroundColor = ApplyHue(theme.IconBackgroundColor, targetColor);
            theme.NameBarColor = ApplyHue(theme.NameBarColor, targetColor);
            theme.NameBarOutline = ApplyHue(theme.NameBarOutline, targetColor);
            theme.NameTextColor = ApplyHue(theme.NameTextColor, targetColor);
            theme.DialogueTextColor = ApplyHue(theme.DialogueTextColor, targetColor);
            theme.ButtonColor = ApplyHue(theme.ButtonColor, targetColor);
            theme.ButtonHoverColor = ApplyHue(theme.ButtonHoverColor, targetColor);
            theme.ButtonTextColor = ApplyHue(theme.ButtonTextColor, targetColor);
            theme.TimerBarColor = ApplyHue(theme.TimerBarColor, targetColor);
            return theme;
        }

        private static void RgbToHsl(Color c, out float h, out float s, out float l)
        {
            float r = c.R / 255f, g = c.G / 255f, b = c.B / 255f;
            float max = Math.Max(r, Math.Max(g, b));
            float min = Math.Min(r, Math.Min(g, b));
            l = (max + min) / 2f;

            if (Math.Abs(max - min) < 0.0001f)
            {
                h = 0f;
                s = 0f;
                return;
            }

            float d = max - min;
            s = l > 0.5f ? d / (2f - max - min) : d / (max + min);

            if (max == r) h = (g - b) / d + (g < b ? 6f : 0f);
            else if (max == g) h = (b - r) / d + 2f;
            else h = (r - g) / d + 4f;
            h /= 6f;
        }

        private static Color HslToRgb(float h, float s, float l)
        {
            if (s <= 0.0001f)
            {
                byte v = (byte)(l * 255f);
                return new Color(v, v, v);
            }

            float q = l < 0.5f ? l * (1f + s) : l + s - l * s;
            float p = 2f * l - q;

            float r = HueToRgb(p, q, h + 1f / 3f);
            float g = HueToRgb(p, q, h);
            float b = HueToRgb(p, q, h - 1f / 3f);

            return new Color(
                (byte)MathHelper.Clamp(r * 255f, 0, 255),
                (byte)MathHelper.Clamp(g * 255f, 0, 255),
                (byte)MathHelper.Clamp(b * 255f, 0, 255));
        }

        private static float HueToRgb(float p, float q, float t)
        {
            if (t < 0f) t += 1f;
            if (t > 1f) t -= 1f;
            if (t < 1f / 6f) return p + (q - p) * 6f * t;
            if (t < 1f / 2f) return q;
            if (t < 2f / 3f) return p + (q - p) * (2f / 3f - t) * 6f;
            return p;
        }
    }
}
