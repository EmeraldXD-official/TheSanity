using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria.Graphics.Shaders;

namespace TheSanity.Content.Skies
{
    /// <summary>
    /// Wrapper untuk GalaxySwirl.fx. uTime dan uIntensity adalah parameter STANDAR screen-shader
    /// yang otomatis di-set tiap frame oleh engine (uIntensity lewat .UseIntensity() saat
    /// registrasi di TheSanity.cs), jadi class ini tidak perlu override Update() sama sekali.
    /// </summary>
    public class GalaxyShaderData : ScreenShaderData
    {
        public GalaxyShaderData(Asset<Effect> shader, string passName) : base(shader, passName)
        {
        }
    }
}
