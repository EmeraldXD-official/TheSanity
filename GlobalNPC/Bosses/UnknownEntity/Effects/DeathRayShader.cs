using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Effects
{
    public class DeathRayShaderLoader : ModSystem
    {
        public static Effect Shader { get; private set; }
        public static Asset<Texture2D> NoiseTexture { get; private set; }

        public override void Load()
        {
            if (!Main.dedServ)
            {
                // Load Custom Shader .fx
                if (ModContent.HasAsset("TheSanity/Effects/DeathRayShader"))
                {
                    Shader = ModContent.Request<Effect>("TheSanity/Effects/DeathRayShader", AssetRequestMode.ImmediateLoad).Value;
                }

                // Mengambil TurbulentNoise berkualitas tinggi dari Luminance
                if (ModContent.HasAsset("Luminance/Assets/Noise/TurbulentNoise"))
                {
                    NoiseTexture = ModContent.Request<Texture2D>("Luminance/Assets/Noise/TurbulentNoise", AssetRequestMode.ImmediateLoad);
                }
                else
                {
                    // Fallback aman ke noise vanilla jika library Luminance tidak terdeteksi
                    NoiseTexture = Main.Assets.Request<Texture2D>("Images/Extra_193", AssetRequestMode.ImmediateLoad);
                }
            }
        }

        public override void Unload()
        {
            Shader = null;
            NoiseTexture = null;
        }
    }
}