using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class GlobalExplodingBullet : GlobalItem
    {
        public override void SetDefaults(Item item)
        {
            if (item.type == ItemID.ExplodingBullet)
            {
                // Mengubah harga beli di NPC menjadi 1 Silver per peluru
                item.value = Item.buyPrice(silver: 1);
            }
        }
    }
}