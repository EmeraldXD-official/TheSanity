using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class BambooAmmoFix : GlobalItem
    {
        public override void SetDefaults(Item item) {
            // Memaksa game Terraria untuk mengenali Bamboo Block vanilla sebagai Amunisi
            if (item.type == ItemID.BambooBlock) {
                item.ammo = ItemID.BambooBlock;
            }
        }
    }
}