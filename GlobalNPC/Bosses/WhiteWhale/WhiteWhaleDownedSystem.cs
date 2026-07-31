using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TheSanity.GlobalNPC.Bosses.WhiteWhale
{
    public class WhiteWhaleDownedSystem : ModSystem
    {
        public static bool downedWhiteWhale;

        public static Condition KillCondition => new Condition(
            Language.GetOrRegister(
                "Mods.TheSanity.Conditions.KillWhiteWhale",
                () => "After defeating the White Whale"),
            () => downedWhiteWhale);

        public override void ClearWorld()
        {
            downedWhiteWhale = false;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (downedWhiteWhale)
                tag["downedWhiteWhale"] = true;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            downedWhiteWhale = tag.ContainsKey("downedWhiteWhale");
        }
    }
}