using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.WhiteWhale;

namespace TheSanity.Items.ArmorBoss.Cultist.Armor
{
    [AutoloadEquip(Terraria.ModLoader.EquipType.Legs)]
    public class CultistLegs : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.value = Terraria.Item.buyPrice(gold: 10);
            Item.rare = ItemRarityID.Purple;
            Item.defense = 12;
        }

        public override void UpdateEquip(Terraria.Player player)
        {
            player.moveSpeed += 0.06f;
            player.GetCritChance(DamageClass.Magic) += 4f;
            player.manaCost -= 0.08f;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.FragmentStardust, 10)
                .AddIngredient(ItemID.FragmentVortex, 10)
                .AddIngredient(ItemID.FragmentNebula, 10)
                .AddIngredient(ItemID.FragmentSolar, 10)
                .AddTile(TileID.LunarCraftingStation)
                .AddCondition(WhiteWhaleDownedSystem.KillCondition)
                .Register();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "CultistLegsFlavor", "Every step echoes with a hymn from below")
            {
                OverrideColor = new Microsoft.Xna.Framework.Color(180, 120, 220)
            });
            tooltips.Add(new TooltipLine(Mod, "CultistLegsStats", "9% increased magic damage\n9% increased movement speed"));
        }
    }
}