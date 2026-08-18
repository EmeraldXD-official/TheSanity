using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items
{
    public class LoveBag : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 9999;
            Item.consumable = true;
            Item.rare = ItemRarityID.Pink;
        }

        public override bool CanRightClick()
        {
            return true;
        }

        // FIX: matiin auto-consume bawaan tModLoader (biasanya jalan otomatis abis RightClick).
        // Bag sekarang cuma dikonsumsi manual di LoveBagUIState setelah gacha kelar,
        // dan TIDAK dikonsumsi kalau player klik tombol X (batal).
        public override bool ConsumeItem(Player player)
        {
            return false;
        }

        public override void RightClick(Player player)
        {
            // Bangun daftar reward (guaranteed + gacha) sekali di awal, RNG-nya udah final di sini.
            // Item.type dipakai sebagai "type" utk GetSource_OpenItem() di dalam reward set builder,
            // sementara Item (this.Item) sendiri kita kirim sebagai referensi yang nanti dikonsumsi manual.
            var rewardSet = LoveBagRewardBuilder.Build(player);

            ModContent.GetInstance<LoveBagUISystem>().OpenLoveBagUI(Item, rewardSet);
        }
    }
}
