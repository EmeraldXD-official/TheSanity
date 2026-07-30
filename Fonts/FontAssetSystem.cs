using ReLogic.Content;
using ReLogic.Graphics;
using Terraria.ModLoader;

namespace TheSanity.Fonts
{
    public class FontAssetSystem : ModSystem
    {
        public static Asset<DynamicSpriteFont> SaiFont;

        public override void Load()
        {
            // path = "NamaMod/Fonts/NamaFileTanpaEkstensi" (tanpa .dynamicfont/.xnb)
            SaiFont = ModContent.Request<DynamicSpriteFont>("TheSanity/Fonts/SaiFont", AssetRequestMode.ImmediateLoad);
        }
    }
}