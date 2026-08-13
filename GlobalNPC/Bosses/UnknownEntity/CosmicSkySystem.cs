using Terraria;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    public class CosmicSkySystem : ModSystem
    {
        // Key unik untuk sky ini, dipakai saat Activate/Deactivate dari mana pun
        public const string SkyKey = "TheSanity:CosmicSky";

        public override void Load()
        {
            if (Main.dedServ)
                return;

            SkyManager.Instance[SkyKey] = new CosmicSkyBackground();
        }

        public override void Unload()
        {
            if (Main.dedServ)
                return;

            if (SkyManager.Instance != null)
                SkyManager.Instance[SkyKey] = null;

            CosmicSkyBackground.UnloadAssets();
        }
    }
}