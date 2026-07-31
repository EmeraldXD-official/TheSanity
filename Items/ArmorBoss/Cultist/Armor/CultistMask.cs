using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.WhiteWhale;

namespace TheSanity.Items.ArmorBoss.Cultist.Armor
{
    [AutoloadEquip(Terraria.ModLoader.EquipType.Head)]
    public class CultistMask : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.value = Terraria.Item.buyPrice(gold: 10);
            Item.rare = ItemRarityID.Purple;
            Item.defense = 14;
        }

        public override void UpdateEquip(Terraria.Player player)
        {
            // Set bonus is granted from the body piece (CultistRobe) via UpdateArmorSet.
            player.GetDamage(DamageClass.Magic) += 0.05f;
            player.GetCritChance(DamageClass.Magic) += 5f;
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
            tooltips.Add(new TooltipLine(Mod, "CultistMaskFlavor", "The whispers of the deep grant clarity of mind")
            {
                OverrideColor = new Microsoft.Xna.Framework.Color(180, 120, 220)
            });
            tooltips.Add(new TooltipLine(Mod, "CultistMaskStats", "Increases maximum mana by 60 and reduces mana usage by 14%\n8.5% increased magic damage and critical strike chance"));
        }
    }
}