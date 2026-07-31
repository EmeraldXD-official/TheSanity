using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.WhiteWhale;

namespace TheSanity.Items.ArmorBoss.Empress.EmpressArmor
{
    // Legs piece. Sprite files: EmpressHooves.png (icon), EmpressHooves_Legs.png (equip layer)
    [AutoloadEquip(EquipType.Legs)]
    public class EmpressHooves : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 18;
            Item.defense = 12;
            Item.value = Terraria.Item.sellPrice(gold: 8, silver: 50);
            Item.rare = ItemRarityID.LightRed;
        }

        public override void UpdateEquip(Player player)
        {
            player.maxMinions += 1;
            player.GetDamage(DamageClass.Summon) += 0.16f;
            player.moveSpeed += 0.20f;
            player.whipRangeMultiplier += 0.10f;
        }

        public override void AddRecipes()
        {
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<PrismaticEssence>(), 7);
            recipe.AddTile(TileID.MythrilAnvil);
            recipe.AddCondition(WhiteWhaleDownedSystem.KillCondition);
            recipe.Register();
        }
    }
}
