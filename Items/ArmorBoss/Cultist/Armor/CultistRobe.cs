using System.Collections.Generic;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.WhiteWhale;

namespace TheSanity.Items.ArmorBoss.Cultist.Armor
{
    [AutoloadEquip(Terraria.ModLoader.EquipType.Body)]
    public class CultistRobe : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 20;
            Item.height = 20;
            Item.value = Terraria.Item.buyPrice(gold: 15);
            Item.rare = ItemRarityID.Purple;
            Item.defense = 16;
        }

        public override void UpdateEquip(Terraria.Player player)
        {
            player.statManaMax2 += 40;
            player.GetDamage(DamageClass.Magic) += 0.08f;
        }

        public override bool IsArmorSet(Terraria.Item head, Terraria.Item body, Terraria.Item legs)
        {
            return head.type == ModContent.ItemType<CultistMask>()
                && body.type == ModContent.ItemType<CultistRobe>()
                && legs.type == ModContent.ItemType<CultistLegs>();
        }

        public override void UpdateArmorSet(Terraria.Player player)
        {
            player.setBonus = Terraria.Localization.Language.GetTextValue("Mods.CultistSet.SetBonus.Cultist");

            // Full-set mage bonuses. Combined with the individual pieces, total set stats
            // land between Spectre armor and Nebula armor (below Nebula, above Spectre).
            player.GetDamage(DamageClass.Magic) += 0.05f;
            player.GetCritChance(DamageClass.Magic) += 3f;
            player.statManaMax2 += 10;
            player.manaCost -= 0.05f;

            var cp = player.GetModPlayer<Items.ArmorBoss.Cultist.Player.CultistPlayer>();
            cp.SetBonusActive();
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient(ItemID.FragmentSolar, 10)
                .AddIngredient(ItemID.FragmentVortex, 10)
                .AddIngredient(ItemID.FragmentNebula, 10)
                .AddIngredient(ItemID.FragmentStardust, 10)
                .AddTile(TileID.LunarCraftingStation)
                .AddCondition(WhiteWhaleDownedSystem.KillCondition)
                .Register();
        }

        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "CultistRobeFlavor", "Stitched from threads that remember the drowned")
            {
                OverrideColor = new Microsoft.Xna.Framework.Color(180, 120, 220)
            });
            tooltips.Add(new TooltipLine(Mod, "CultistRobeStats", "8% increased magic damage and critical strike chance"));
        }
    }
}