using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Mecanic
{
    public class WormholeSlotPlayer : ModPlayer
    {
        // Menyimpan data item yang ada di dalam slot kustom kita
        public Item WormholeSlotItem;

        public override void Initialize()
        {
            WormholeSlotItem = new Item();
            WormholeSlotItem.TurnToAir();
        }

        public override void PostUpdate()
        {
            // [AUTO-FILL SYSTEM LOCATION]
            if (WormholeSlotItem.IsAir || WormholeSlotItem.type != ItemID.WormholePotion)
            {
                // Otomatis memberikan 1 buah Wormhole Potion ke dalam slot di latar belakang
                WormholeSlotItem.SetDefaults(ItemID.WormholePotion);
                WormholeSlotItem.stack = 1;
            }
        }
    }
}