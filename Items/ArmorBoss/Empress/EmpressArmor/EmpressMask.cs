using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.ArmorBoss.Empress.Players;
using TheSanity.GlobalNPC.Bosses.WhiteWhale;

namespace TheSanity.Items.ArmorBoss.Empress.EmpressArmor
{
    // Head piece. Sprite files: EmpressMask.png (icon), EmpressMask_Head.png (equip layer)
    [AutoloadEquip(EquipType.Head)]
    public class EmpressMask : ModItem
    {
        public override void SetStaticDefaults()
        {
            // Tooltip text lives in Localization/en-US.hjson under
            // Mods.EmpressArmorMod.Items.EmpressMask.Tooltip
        }

        public override void SetDefaults()
        {
            Item.width = 22;
            Item.height = 22;
            Item.defense = 11;
            Item.value = Terraria.Item.sellPrice(gold: 8);
            Item.rare = ItemRarityID.LightRed; // adjust to match your mod's rarity tier
        }

        public override void UpdateEquip(Player player)
        {
            player.maxMinions += 1;
            player.GetDamage(DamageClass.Summon) += 0.16f;
        }

        // These two hooks belong here (any one piece — head, by convention),
        // not on ModPlayer.
        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return body.type == ModContent.ItemType<EmpressChestplate>()
                && legs.type == ModContent.ItemType<EmpressHooves>();
        }

        public override void UpdateArmorSet(Player player)
        {
            player.setBonus = "Increases summon damage by 16%\n" +
                               "An Ethereal Lance strikes enemies within 80 tiles,\n" +
                               "inflicting 'Light in Your Soul'. Enemies that die under\n" +
                               "this effect release 4 seeking Prismatic Bolts.\n" +
                               "A Sun Dance of rays continuously surrounds you\n" +
                               "in 3 waves, damaging nearby enemies.";

            player.GetModPlayer<EmpressPlayer>().empressSet = true;
        }

        public override void AddRecipes()
        {
            // Placeholder recipe — swap ingredients/station for whatever your
            // Empress-tier boss / material actually is.
            Recipe recipe = CreateRecipe();
            recipe.AddIngredient(ModContent.ItemType<PrismaticEssence>(), 6);
            recipe.AddTile(TileID.MythrilAnvil); // Ancient Manipulator, adjust as needed
            recipe.AddCondition(WhiteWhaleDownedSystem.KillCondition);
            recipe.Register();
        }
    }
}