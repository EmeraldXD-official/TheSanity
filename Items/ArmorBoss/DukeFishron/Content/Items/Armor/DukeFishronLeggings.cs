using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.DukeFishron.Content.Items.Armor
{
    [AutoloadEquip(EquipType.Legs)]
    public class DukeFishronLeggings : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(gold: 11);
            Item.rare = 9;
            Item.defense = 18;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Ranged) += 0.06f;
            player.GetCritChance(DamageClass.Ranged) += 6;
            player.moveSpeed += 0.11f;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return head.type == ModContent.ItemType<DukeFishronHead>()
                && body.type == ModContent.ItemType<DukeFishronBreastplate>();
        }

        public override void UpdateArmorSet(Player player)
        {
            // See DukeFishronHead for the shared setBonus text + flag.
        }
    }
}
