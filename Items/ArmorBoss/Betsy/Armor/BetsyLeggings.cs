using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.WhiteWhale;

namespace TheSanity.Items.ArmorBoss.Betsy.Armor
{
    // PENTING: sprite "dipakai" untuk kaki (BetsyLeggings_Legs.png) belum ada di aset yang kamu kirim
    // (kamu hanya kirim icon item-nya). AutoloadEquip di bawah akan mencari file itu otomatis --
    // untuk sementara file itu sudah aku isi placeholder (copy dari icon) supaya mod tidak crash,
    // tapi tampilannya di kaki player pasti belum pas. Ganti file itu dengan sprite kaki asli
    // ukuran standar 40x56 per frame nanti.
    [AutoloadEquip(EquipType.Legs)]
    public class BetsyLeggings : ModItem
    {
        public override LocalizedText DisplayName => Language.GetOrRegister(this.GetLocalizationKey("DisplayName"), () => "Betsy Leggings");

        public override LocalizedText Tooltip => Language.GetOrRegister(this.GetLocalizationKey("Tooltip"), () =>
            "13% increased melee attack speed\n" +
            "13% increased movement speed"
        );

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 16;
            Item.defense = 20;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 6);
            Item.vanity = false;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetAttackSpeed(DamageClass.Melee) += 0.13f; // +13% melee speed
            player.moveSpeed += 0.13f;                          // +13% movement speed
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.BeetleLeggings)
                .AddIngredient(ItemID.SoulofFright, 10)
                .AddIngredient(ItemID.SoulofMight, 10)
                .AddIngredient(ItemID.SoulofSight, 10)
                .AddCondition(WhiteWhaleDownedSystem.KillCondition)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}