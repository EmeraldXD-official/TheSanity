using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.ItemDropRules;

namespace TheSanity.Items
{
    public class GolemBagDrop : GlobalItem
    {
        public override void ModifyItemLoot(Item item, ItemLoot itemLoot)
        {
            // Memastikan item yang sedang dibuka kodenya adalah Golem Treasure Bag
            if (item.type == ItemID.GolemBossBag)
            {
                // =========================================================================
                // LOKASI PENGATURAN JUMLAH DROP (BALANCE DI SINI)
                // =========================================================================
                // Format: ItemDropRule.Common(TipeItem, Peluang, JumlahMinimal, JumlahMaksimal)
                // Peluang '1' artinya 100% pasti drop saat bag dibuka
                // Sesuai request: minimal 15, maksimal 25
                int jumlahMinimal = 15;
                int jumlahMaksimal = 25;

                itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<SoulOfTheSun>(), 1, jumlahMinimal, jumlahMaksimal));
            }
        }
    }
}