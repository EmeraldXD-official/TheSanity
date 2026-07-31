using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.WhiteWhale;

namespace TheSanity.Items.ArmorBoss.Betsy.Armor
{
    // AutoloadEquip otomatis memuat "BetsyChestplate_Body.png" (torso, male/default) dan
    // "BetsyChestplate_Body_Female.png" (torso versi female) dari folder yang sama.
    // Kalau kamu bikin sprite lengan terpisah nanti, tambahkan juga "BetsyChestplate_Arms.png".
    [AutoloadEquip(EquipType.Body)]
    public class BetsyChestplate : ModItem
    {
        public override LocalizedText DisplayName => Language.GetOrRegister(this.GetLocalizationKey("DisplayName"), () => "Betsy Chestplate");

        public override LocalizedText Tooltip => Language.GetOrRegister(this.GetLocalizationKey("Tooltip"), () =>
            "9% increased melee damage\n" +
            "6% increased melee critical strike chance"
        );

        public override void SetStaticDefaults()
        {
        }

        public override void SetDefaults()
        {
            Item.width = 34;
            Item.height = 24;
            Item.defense = 25;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 8);
            Item.vanity = false;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Melee) += 0.09f; // +9%
            player.GetCritChance(DamageClass.Melee) += 6f; // +6%
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.BeetleScaleMail)
                .AddIngredient(ItemID.SoulofFright, 14)
                .AddIngredient(ItemID.SoulofMight, 14)
                .AddIngredient(ItemID.SoulofSight, 14)
                .AddTile(TileID.MythrilAnvil)
                .AddCondition(WhiteWhaleDownedSystem.KillCondition)
                .Register();
        }
    }
}