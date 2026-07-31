using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.WhiteWhale;

namespace TheSanity.Items.ArmorBoss.Empress.EmpressArmor
{
    // Body piece. Sprite files: EmpressChestplate.png (icon), EmpressChestplate_Body.png,
    // EmpressChestplate_Arms.png (equip layers)
    [AutoloadEquip(EquipType.Body)]
    public class EmpressChestplate : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 30;
            Item.height = 20;
            Item.defense = 14;
            Item.value = Terraria.Item.sellPrice(gold: 10);
            Item.rare = ItemRarityID.LightRed;
        }

        public override void UpdateEquip(Player player)
        {
            player.maxMinions += 2;
            player.GetDamage(DamageClass.Summon) += 0.16f;
            player.GetAttackSpeed(DamageClass.Summon) += 0f; // reserved, whips use range not speed here
            player.whipRangeMultiplier += 0.10f;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<PrismaticEssence>(), 9);
            recipe.AddTile(TileID.MythrilAnvil);
            recipe.AddCondition(WhiteWhaleDownedSystem.KillCondition);
            recipe.Register();
        }
    }
}
