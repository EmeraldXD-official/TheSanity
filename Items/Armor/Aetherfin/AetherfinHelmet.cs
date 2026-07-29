using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.Aetherfin
{
    [AutoloadEquip(EquipType.Head)]
    public class AetherfinHelmet : ModItem
    {
        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(0, 0, 75, 0);
            Item.rare = ItemRarityID.Blue;
            Item.defense = 4;
        }

        public override void UpdateEquip(Player player) {
            player.GetDamage(DamageClass.Summon) += 0.06f; // +6% Summon Damage
            player.maxMinions += 1;                         // Stat Base Pasif: +1 Max Minion
        }

        public override bool IsArmorSet(Item head, Item body, Item legs) {
            return body.type == ModContent.ItemType<AetherfinChestplate>() && legs.type == ModContent.ItemType<AetherfinGreaves>();
        }

        public override void UpdateArmorSet(Player player) {
            AetherfinPlayer modPlayer = player.GetModPlayer<AetherfinPlayer>();
            modPlayer.aetherfinSetEquipped = true;

            // Stat Base Set Bonus
            player.GetDamage(DamageClass.Summon) += 0.10f; // +10% Summon Damage Pasif
            player.gills = true;                            // Bernapas di air

            player.setBonus = 
                "Creates an Aquarium Aura around you\n" +
                "Minions inside aura gain +50% movement speed and 8 Armor Penetration\n" +
                "Press 'AetherfinFloodgate' to trigger Floodgate Expansion (10s duration, 30s cd)";
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<BossDrop.BrokenTv>(), 4)
                .AddIngredient(2498)
                .AddIngredient(2306,10)
                .AddIngredient(1017)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}