using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class DemolitionistShop : global::Terraria.ModLoader.GlobalNPC
    {
        // 1. Menambahkan Exploding Bullet ke daftar toko Demolitionist
        public override void ModifyShop(NPCShop shop)
        {
            if (shop.NpcType == NPCID.Demolitionist)
            {
                // Syarat: Hardmode DAN Arms Dealer sedang ada/muncul di world
                shop.Add(
                    ItemID.ExplodingBullet, 
                    Condition.Hardmode, 
                    Condition.NpcIsPresent(NPCID.ArmsDealer)
                );
            }
        }

        // 2. Menghapus Explosive Powder dari toko saat toko dibuka oleh player
        public override void ModifyActiveShop(NPC npc, string shopName, Item[] items)
        {
            if (npc.type == NPCID.Demolitionist)
            {
                for (int i = 0; i < items.Length; i++)
                {
                    if (items[i] != null && items[i].type == ItemID.ExplosivePowder)
                    {
                        items[i] = new Item(); // Mengosongkan slot item Explosive Powder
                    }
                }
            }
        }
    }
}