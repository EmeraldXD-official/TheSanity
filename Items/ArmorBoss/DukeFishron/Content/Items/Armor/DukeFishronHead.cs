using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.ArmorBoss.DukeFishron.Content.Players;

namespace TheSanity.Items.ArmorBoss.DukeFishron.Content.Items.Armor
{
    // ---- Stat design notes ----
    // Shroomite total defense: 51 (11 + 24 + 16), +13% ranged dmg, +25% ranged crit,
    //   +20% ammo conservation, +12% move speed.
    // Vortex total defense:    62 (14 + 28 + 20), +36% ranged dmg, +27% ranged crit,
    //   +25% ammo conservation, +10% move speed.
    // DukeFishron (this set) sits in between: 56 total defense (12 + 26 + 18),
    //   +24% ranged dmg, +21% ranged crit, +22% ammo conservation, +11% move speed,
    //   plus the two custom Duke Fishron set bonuses described below.
    [AutoloadEquip(EquipType.Head)]
    public class DukeFishronHead : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(gold: 9);
            Item.rare = 9; // between Shroomite (8, yellow) and Vortex (10, red)
            Item.defense = 12;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Ranged) += 0.09f;
            player.GetCritChance(DamageClass.Ranged) += 6;
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return body.type == ModContent.ItemType<DukeFishronBreastplate>()
                && legs.type == ModContent.ItemType<DukeFishronLeggings>();
        }

        public override void UpdateArmorSet(Player player)
        {
            // IMPORTANT: the ranged dmg/crit/move speed % numbers are NOT set-bonus-gated -
            // each piece already grants its own share via its own UpdateEquip, unconditionally,
            // whether or not the other two pieces are worn. Do not list those totals here, or
            // the tooltip wrongly implies the player needs the full set just to get them.
            // Only the two abilities below actually require the full set.
            player.setBonus =
                "5% chance on ranged hit to summon a Duke Fishron projectile that dashes\n" +
                "through the target 3 times, like Duke Fishron's Phase 3 charge\n" +
                "Double tap Down to summon Duke Fishron, who circles you and releases\n" +
                "15 protective bubbles that pop hostile projectiles (30 second cooldown)";

            player.GetModPlayer<DukeFishronPlayer>().setBonus = true;
        }
    }
}