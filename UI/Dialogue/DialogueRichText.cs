using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Xna.Framework;
using ReLogic.Graphics;

namespace TheSanity.UI.DialogueSystem
{
    public enum RichTokenType { Char, Icon }

    /// <summary>
    /// Satu unit terkecil teks dialog setelah di-parse: 1 karakter (dengan warna opsional)
    /// atau 1 icon item. Dipakai bareng buat wrapping, animasi ketik, DAN rendering, jadi
    /// icon/warna ikut kehitung benar di semua tahap itu (bukan cuma pas gambar doang).
    /// </summary>
    public struct RichToken
    {
        public RichTokenType Type;
        public char Char;
        public int IconItemID;
        public Color? ColorOverride; // null = pakai DialogueTheme.DialogueTextColor bawaan (warna default baris itu)
    }

    /// <summary>
    /// Parser tag inline buat teks dialog (DialogueLine.P1/P2.Dialogue). Dua tag yang didukung:
    ///
    ///   [i:ID]                     -> gambar icon item ber-ID itu inline di tengah kalimat.
    ///                                  ID bisa item vanilla ATAU item modded (ambil dari
    ///                                  ItemID.Sets / ModContent.ItemType&lt;T&gt;() di kode kamu).
    ///
    ///   [c/RRGGBB:teks di sini]    -> teks di dalam tag ini diwarnain pakai hex RRGGBB.
    ///                                  Contoh: "aku benar benar [c/FF0000:BENCI KAMU]"
    ///                                  -> kata "BENCI KAMU" doang yang jadi merah, sisanya
    ///                                  tetap pakai warna teks default tema aktif.
    ///
    /// Format tag ini SENGAJA dibikin sama persis kayak chat tag bawaan Terraria ([i:ID] dan
    /// [c/RRGGBB:teks]), jadi kalau kamu udah familiar sama chat tag vanilla, syntax-nya ga asing.
    ///
    /// BATASAN: tag [c/...] TIDAK BISA di-nest (isinya ga boleh ada tag [i:...] atau [c:...]
    /// lain di dalamnya) dan isinya ga boleh mengandung karakter "]".
    /// </summary>
    public static class DialogueRichText
    {
        private static readonly Regex TagRegex = new Regex(
            @"\[i:(?<id>\d+)\]|\[c/(?<hex>[0-9A-Fa-f]{6}):(?<txt>[^\]]*)\]",
            RegexOptions.Compiled);

        /// <summary>Ubah string mentah (boleh mengandung tag [i:...]/[c/...:...] atau nggak sama sekali) jadi list token.</summary>
        public static List<RichToken> Parse(string raw)
        {
            var tokens = new List<RichToken>();
            if (string.IsNullOrEmpty(raw)) return tokens;

            int pos = 0;
            foreach (Match m in TagRegex.Matches(raw))
            {
                // teks polos sebelum tag ini (kalau ada) - warna null = ikut default
                if (m.Index > pos)
                    AppendPlain(tokens, raw.Substring(pos, m.Index - pos), null);

                if (m.Groups["id"].Success)
                {
                    if (int.TryParse(m.Groups["id"].Value, out int itemId))
                        tokens.Add(new RichToken { Type = RichTokenType.Icon, IconItemID = itemId });
                }
                else
                {
                    Color color = HexToColor(m.Groups["hex"].Value);
                    AppendPlain(tokens, m.Groups["txt"].Value, color);
                }

                pos = m.Index + m.Length;
            }

            // sisa teks polos setelah tag terakhir
            if (pos < raw.Length)
                AppendPlain(tokens, raw.Substring(pos), null);

            return tokens;
        }

        private static void AppendPlain(List<RichToken> tokens, string text, Color? color)
        {
            foreach (char c in text)
                tokens.Add(new RichToken { Type = RichTokenType.Char, Char = c, ColorOverride = color });
        }

        private static Color HexToColor(string hex)
        {
            byte r = Convert.ToByte(hex.Substring(0, 2), 16);
            byte g = Convert.ToByte(hex.Substring(2, 2), 16);
            byte b = Convert.ToByte(hex.Substring(4, 2), 16);
            return new Color(r, g, b);
        }

        public static float TokenWidth(RichToken token, DynamicSpriteFont font, float scale, float iconSize) =>
            token.Type == RichTokenType.Icon ? iconSize : font.MeasureString(token.Char.ToString()).X * scale;

        /// <summary>
        /// Word-wrap versi rich-token (setara WrapText lama yang berbasis string biasa), tapi
        /// warna & icon tiap potongan tetap kebawa utuh ke tiap baris hasil wrap-nya. Icon
        /// dihitung sebagai 1 "kata" selebar iconSize kalau berdiri sendiri (dikelilingi spasi),
        /// atau ikut nempel ke kata di sekitarnya kalau ga dipisah spasi (mis. "kata[i:97]kata").
        /// </summary>
        public static List<List<RichToken>> Wrap(DynamicSpriteFont font, List<RichToken> tokens, int maxWidth, float scale, float iconSize)
        {
            var lines = new List<List<RichToken>>();
            var currentLine = new List<RichToken>();
            var currentWord = new List<RichToken>();
            float lineWidth = 0f;
            float wordWidth = 0f;

            void FlushWord()
            {
                if (currentWord.Count == 0) return;
                if (lineWidth + wordWidth > maxWidth && currentLine.Count > 0)
                {
                    lines.Add(currentLine);
                    currentLine = new List<RichToken>();
                    lineWidth = 0f;
                }
                currentLine.AddRange(currentWord);
                lineWidth += wordWidth;
                currentWord.Clear();
                wordWidth = 0f;
            }

            foreach (RichToken token in tokens)
            {
                if (token.Type == RichTokenType.Char && token.Char == '\n')
                {
                    FlushWord();
                    lines.Add(currentLine);
                    currentLine = new List<RichToken>();
                    lineWidth = 0f;
                    continue;
                }

                if (token.Type == RichTokenType.Char && token.Char == ' ')
                {
                    FlushWord();
                    float spaceW = font.MeasureString(" ").X * scale;
                    if (lineWidth + spaceW > maxWidth && currentLine.Count > 0)
                    {
                        lines.Add(currentLine);
                        currentLine = new List<RichToken>();
                        lineWidth = 0f;
                    }
                    else
                    {
                        currentLine.Add(token);
                        lineWidth += spaceW;
                    }
                    continue;
                }

                currentWord.Add(token);
                wordWidth += TokenWidth(token, font, scale, iconSize);
            }

            FlushWord();
            lines.Add(currentLine);
            if (lines.Count == 0) lines.Add(new List<RichToken>());

            return lines;
        }
    }
}
