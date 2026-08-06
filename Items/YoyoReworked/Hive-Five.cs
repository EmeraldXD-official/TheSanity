using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Globals
{
    public class HiveFiveGlobalItem : GlobalItem
    {
        public override void SetDefaults(Item item)
        {
            // Mengubah base damage Hive-Five dari 24 menjadi 40
            if (item.type == ItemID.HiveFive)
            {
                item.damage = 40;
            }
        }
    }
}