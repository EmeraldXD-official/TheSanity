using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.DukeFishron.Content.Items.Armor
{
    [AutoloadEquip(EquipType.Body)]
    public class DukeFishronBreastplate : ModItem
    {
        // NOTE: no equip "_Arms" texture sheet was supplied for this piece
        // (only Chestplate.png for the icon and Chestplate_Body.png for the worn
        // sprite, confirmed by the user as Armor_259_recolored.png). tModLoader
        // will fall back to a bright pink "missing texture" for the arm layer
        // until you add Chestplate_Arms.png next to the other two.
        //
        // TODO verify: the ~22% ammo-conservation chance is implemented in
        // DukeFishronPlayer.ConsumeAmmo() (a ModPlayer hook, since ammo conservation
        // can't be applied directly from an armor piece). Make sure that hook checks
        // for this piece being equipped, NOT the full-set setBonus flag - ammo saving
        // should work the same way dmg/crit/move speed do here: granted per equipped
        // piece, not gated behind wearing the complete set.
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(gold: 16);
            Item.rare = 9;
            Item.defense = 26;
        }

        public override void UpdateEquip(Player player)
        {
            player.GetDamage(DamageClass.Ranged) += 0.09f;
            player.GetCritChance(DamageClass.Ranged) += 9;

            // The ~22% ammo-conservation chance (between Shroomite's 20% and Vortex's 25%)
            // is implemented in DukeFishronPlayer.ConsumeAmmo(), since ammo conservation
            // is a ModPlayer hook, not something an armor piece can apply directly.
        }

        public override bool IsArmorSet(Item head, Item body, Item legs)
        {
            return head.type == ModContent.ItemType<DukeFishronHead>()
                && legs.type == ModContent.ItemType<DukeFishronLeggings>();
        }

        // The head piece owns the shared setBonus text + DukeFishronPlayer flag so it
        // isn't written 3 times; IsArmorSet is still implemented here so tModLoader's
        // per-piece set-matching (and the "armor set" glow/vanity checks) works correctly.
        public override void UpdateArmorSet(Player player)
        {
        }
    }
}