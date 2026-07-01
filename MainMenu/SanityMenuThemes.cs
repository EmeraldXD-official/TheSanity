using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.MainMenu
{
    // =========================================================================
    // 1. TEMA: REALITY (ORIGINAL)
    // =========================================================================
    public class RealityModMenu : ModMenu
    {
        public override string DisplayName => "Reality";

        public override Asset<Texture2D> Logo => ModContent.Request<Texture2D>("TheSanity/MainMenu/Reality1");

        public override int Music => MusicLoader.GetMusicSlot(Mod, "Music/RealityMusic");

        public override void OnSelected()
        {
            Main.newMusic = Music;
            // Hapus baris Main.musicVolume = 1f;
        }

        public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter, ref float logoRotation, ref float logoScale, ref Color logoColor)
        {
            Texture2D bgTex = ModContent.Request<Texture2D>("TheSanity/MainMenu/RealityBG").Value;
            spriteBatch.Draw(bgTex, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            logoColor = Color.White;
            return true;
        }
    }

    // =========================================================================
    // 2. TEMA: REALITY 1 (VARIAN BARU)
    // =========================================================================
    public class Reality1ModMenu : ModMenu
    {
        public override string DisplayName => "Pain of Reality";

        public override Asset<Texture2D> Logo => ModContent.Request<Texture2D>("TheSanity/MainMenu/Reality1");

        public override int Music => MusicLoader.GetMusicSlot(Mod, "Music/RealityMusic1");

        public override void OnSelected()
        {
            Main.newMusic = Music;
            // Hapus baris Main.musicVolume = 1f;
        }

        public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter, ref float logoRotation, ref float logoScale, ref Color logoColor)
        {
            Texture2D bgTex = ModContent.Request<Texture2D>("TheSanity/MainMenu/RealityBG1").Value;
            spriteBatch.Draw(bgTex, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            logoColor = Color.White;
            return true;
        }
    }

    // =========================================================================
    // 3. TEMA: TWILIGHT (ORIGINAL)
    // =========================================================================
    public class TwilightModMenu : ModMenu
    {
        public override string DisplayName => "The Perfect Twilight in the end";

        public override Asset<Texture2D> Logo => ModContent.Request<Texture2D>("TheSanity/MainMenu/Twilight1");

        public override int Music => MusicLoader.GetMusicSlot(Mod, "Music/TwilightMusic");

        public override void OnSelected()
        {
            Main.newMusic = Music;
            // Hapus baris Main.musicVolume = 1f;
        }

        public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter, ref float logoRotation, ref float logoScale, ref Color logoColor)
        {
            Texture2D bgTex = ModContent.Request<Texture2D>("TheSanity/MainMenu/TwilightBG").Value;
            spriteBatch.Draw(bgTex, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            logoColor = Color.White;
            return true;
        }
    }

    // =========================================================================
    // 4. TEMA: TWILIGHT 1 (VARIAN BARU)
    // =========================================================================
    public class Twilight1ModMenu : ModMenu
    {
        public override string DisplayName => "Twilight";

        public override Asset<Texture2D> Logo => ModContent.Request<Texture2D>("TheSanity/MainMenu/Twilight1");

        public override int Music => MusicLoader.GetMusicSlot(Mod, "Music/TwilightMusic1");

        public override void OnSelected()
        {
            Main.newMusic = Music;
            // Hapus baris Main.musicVolume = 1f;
        }

        public override bool PreDrawLogo(SpriteBatch spriteBatch, ref Vector2 logoDrawCenter, ref float logoRotation, ref float logoScale, ref Color logoColor)
        {
            Texture2D bgTex = ModContent.Request<Texture2D>("TheSanity/MainMenu/TwilightBG1").Value;
            spriteBatch.Draw(bgTex, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
            logoColor = Color.White;
            return true;
        }
    }
}