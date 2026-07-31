using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.WhiteWhale;

namespace TheSanity.Items.ArmorBoss.Betsy.Armor
{
    // AutoloadEquip otomatis memuat "BetsyMask_Head.png" di folder yang sama sebagai
    // sprite yang dipakai di kepala player.
    [AutoloadEquip(EquipType.Head)]
    public class BetsyMask : ModItem
    {
        public override LocalizedText DisplayName => Language.GetOrRegister(this.GetLocalizationKey("DisplayName"), () => "Betsy Mask");

        public override LocalizedText Tooltip => Language.GetOrRegister(this.GetLocalizationKey("Tooltip"), () =>
            "6% increased melee damage\n" +
            "6% increased melee critical strike chance"
        );

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 30;
            Item.defense = 22;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 5);
            Item.vanity = false;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Melee) += 0.06f; // +6%
            player.GetCritChance(DamageClass.Melee) += 6f; // +6%
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return body.type == ModContent.ItemType<BetsyChestplate>()
                && legs.type == ModContent.ItemType<BetsyLeggings>();
        }

        public override void UpdateArmorSet(Player player)
        {
            player.setBonus = "continuously breathes Betsy's fire breath at nearby enemies within 80 tiles";
            var betsyPlayer = player.GetModPlayer<Players.BetsyArmorPlayer>();
            betsyPlayer.setBonusActive = true;
            // Ditandai di sini (bukan dicek ulang manual di BetsyPortalLayer) karena UpdateArmorSet
            // cuma dipanggil tModLoader kalau IsArmorSet() beneran valid -- ini jalur resmi/reliable
            // buat tau "full set lagi kepasang", bukan hasil ngecek head/body/legs sendiri di layer.
            betsyPlayer.fullSetActive = true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.BeetleHelmet)
                .AddIngredient(ItemID.SoulofFright, 10)
                .AddIngredient(ItemID.SoulofMight, 10)
                .AddIngredient(ItemID.SoulofSight, 10)
                .AddCondition(WhiteWhaleDownedSystem.KillCondition)
                .AddTile(TileID.MythrilAnvil) // ganti ke Ancient Manipulator jika kamu punya tile-nya
                .Register();
        }
    }
}