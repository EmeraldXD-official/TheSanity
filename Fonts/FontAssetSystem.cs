using ReLogic.Content;
using ReLogic.Graphics;
using Terraria.ModLoader;

namespace TheSanity.Fonts
{
    public class FontAssetSystem : ModSystem
    {
        public static Asset<DynamicSpriteFont> SaiFont;

        // Font buat Boss Intro Pluto ("Mechanical Collapse" / "XL-08 Pluto") -- HerrFochGradient.dynamicfont
        public static Asset<DynamicSpriteFont> HerrFochGradient;

        public override void Load()
        {
            // path = "NamaMod/Fonts/NamaFileTanpaEkstensi" (tanpa .dynamicfont/.xnb)
            SaiFont = ModContent.Request<DynamicSpriteFont>("TheSanity/Fonts/SaiFont", AssetRequestMode.ImmediateLoad);
            HerrFochGradient = ModContent.Request<DynamicSpriteFont>("TheSanity/Fonts/HerrFochGradient", AssetRequestMode.ImmediateLoad);
        }
    }
}