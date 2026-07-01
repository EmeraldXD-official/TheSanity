using System.Reflection;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity
{
    public class SanityWindowTitleSystem : ModSystem
    {
        public override void PostSetupContent()
        {
            // 🔧 Ganti splash text menggunakan refleksi karena Main.splashText mungkin tidak langsung tersedia di beberapa versi
            var splashTextField = typeof(Main).GetField("splashText", BindingFlags.Static | BindingFlags.Public);
            if (splashTextField != null)
            {
                splashTextField.SetValue(null, new string[] {
                    "The Sanity: Also try Saihate Station!",
                    "The Sanity: Also try Twilight Railway",
                    "The Sanity: Wake up, Terrarian.",
                    "The Sanity: Special Thanks to びぶ / viv."
                });

                // Pilih satu teks acak untuk judul jendela
                string[] texts = (string[])splashTextField.GetValue(null);
                if (texts.Length > 0)
                {
                    Main.instance.Window.Title = texts[Main.rand.Next(texts.Length)];
                }
            }
            else
            {
                // Fallback: jika field tidak ada, set judul langsung
                string[] fallbackTexts = new string[] {
                    "The Sanity: Also try Saihate Station!",
                    "The Sanity: Also try Twilight Railway!",
                    "The Sanity: Wake up, Terrarian.",
                    "The Sanity: Special Thanks to びぶ / viv >v<"
                };
                Main.instance.Window.Title = fallbackTexts[Main.rand.Next(fallbackTexts.Length)];
            }
        }
    }
}