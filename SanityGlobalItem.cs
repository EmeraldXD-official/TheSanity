using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class SanityGlobalItem : GlobalItem
    {
        public override void UpdateAccessory(Item item, Player player, bool hideVisual)
        {
            
            if (item.type == ItemID.AnglerTackleBag)
            {
                player.GetModPlayer<SanityPlayer>().sanityImmunity = true;
            }
        }
    }
}