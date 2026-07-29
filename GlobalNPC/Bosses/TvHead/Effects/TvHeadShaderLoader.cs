using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.Graphics.Effects;
using Terraria.Graphics.Shaders;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.TvHead.Effects
{
    public class TvHeadShaderLoader : ModSystem
    {
        public override void Load() {
            if (Main.dedServ) return;

            // Pengecekan aman agar tidak error jika file shader .xnb belum di-compile oleh tModLoader
            try {
                if (ModContent.HasAsset("TheSanity/GlobalNPC/Bosses/TvHead/Effects/TvHeadTrailShader")) {
                    Asset<Effect> trailShader = ModContent.Request<Effect>("TheSanity/GlobalNPC/Bosses/TvHead/Effects/TvHeadTrailShader", AssetRequestMode.ImmediateLoad);
                    GameShaders.Misc["TheSanity:TvHeadTrail"] = new MiscShaderData(new Ref<Effect>(trailShader.Value), "TvHeadTrailPass");
                }

                if (ModContent.HasAsset("TheSanity/GlobalNPC/Bosses/TvHead/Effects/TvHeadScreenShader")) {
                    Asset<Effect> screenShader = ModContent.Request<Effect>("TheSanity/GlobalNPC/Bosses/TvHead/Effects/TvHeadScreenShader", AssetRequestMode.ImmediateLoad);
                    Filters.Scene["TheSanity:TvHeadCRT"] = new Filter(new ScreenShaderData(new Ref<Effect>(screenShader.Value), "TvHeadCRTPass"), EffectPriority.Medium);
                }
            }
            catch {
                // Mencegah game crash jika asset shader belum siap
            }
        }
    }
}