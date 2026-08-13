using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    /// <summary>
    /// Registrasi shader post-process (CosmicVignette.fx) ke Filters.Scene, dan
    /// menyediakan helper untuk mengaktifkan/menonaktifkan + mengupdate parameter
    /// shader (uTime, uColor) tiap frame dari CosmicSkyBackground.
    ///
    /// CATATAN: nama method fluent (UseColor/UseOpacity) dan constructor
    /// ScreenShaderData bisa sedikit berbeda antar versi tModLoader. Kalau ada
    /// error compile di file ini, kirim pesan errornya, gampang disesuaikan.
    /// </summary>
    public class CosmicFilterSystem : ModSystem
    {
        public const string FilterKey = "TheSanity:CosmicVignette";

        private static bool loaded;

        public override void Load()
        {
            if (Main.dedServ)
                return;

            try
            {
                Asset<Effect> shaderAsset = ModContent.Request<Effect>("TheSanity/Effects/CosmicVignette", AssetRequestMode.ImmediateLoad);

                var shaderData = new ScreenShaderData(shaderAsset, "CosmicVignettePass")
                    .UseColor(Color.White)
                    .UseOpacity(0f);

                Filters.Scene[FilterKey] = new Filter(shaderData, EffectPriority.High);
                Filters.Scene[FilterKey].Load();
                loaded = true;
            }
            catch (System.Exception e)
            {
                // Jangan sampai kegagalan load shader menjatuhkan seluruh mod.
                // Background tetap jalan tanpa efek vignette/chromatic aberration.
                loaded = false;
                Mod.Logger.Warn($"CosmicFilterSystem gagal load shader, efek vignette dinonaktifkan: {e.Message}");
            }
        }

        public override void Unload()
        {
            if (Main.dedServ || !loaded)
                return;

            if (Filters.Scene[FilterKey] != null)
                Filters.Scene[FilterKey].Deactivate();

            loaded = false;
        }

        public static void SetActive(bool active)
        {
            if (Main.dedServ || !loaded)
                return;

            bool currentlyActive = Filters.Scene[FilterKey].IsActive();

            if (active && !currentlyActive)
                Filters.Scene.Activate(FilterKey);
            else if (!active && currentlyActive)
                Filters.Scene[FilterKey].Deactivate();
        }

        // Dipanggil tiap frame selagi sky aktif, dari CosmicSkyBackground.Update()
        public static void UpdateParams(float time, Color tintColor, float opacity)
        {
            if (Main.dedServ || !loaded)
                return;

            if (Filters.Scene[FilterKey]?.GetShader() is ScreenShaderData shaderData)
            {
                shaderData.UseColor(tintColor).UseOpacity(opacity);
                shaderData.Shader.Parameters["uTime"]?.SetValue(time);
                shaderData.Shader.Parameters["uScreenResolution"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
            }
        }
    }
}