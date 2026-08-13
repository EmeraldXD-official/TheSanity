using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.Broadcaster
{
    [AutoloadEquip(EquipType.Head)]
    public class BroadcasterHeadset : ModItem
    {
        public override void SetDefaults() {
            Item.width = 18;
            Item.height = 18;
            Item.value = Item.sellPrice(0, 0, 80, 0);
            Item.rare = ItemRarityID.Blue;
            Item.defense = 4; // Base Defense Post-EoC
        }

        public override void UpdateEquip(Player player) {
            player.GetDamage(DamageClass.Generic) += 0.05f; // +5% Damage
        }

        public override bool IsArmorSet(Item head, Item body, Item legs) {
            return body.type == ModContent.ItemType<BroadcasterSuit>() && legs.type == ModContent.ItemType<BroadcasterSlacks>();
        }

        public override void UpdateArmorSet(Player player) {
            BroadcasterPlayer modPlayer = player.GetModPlayer<BroadcasterPlayer>();
            modPlayer.broadcasterSetEquipped = true;

            if (!modPlayer.isDefensiveMode) {
                // MODE 1 (OFFENSE): Defense berkurang 15% (sebelumnya 30%)
                player.setBonus = "Offense Mode: +5% damage, hits spark static electricity, but Defense reduced by 15%";
                player.GetDamage(DamageClass.Generic) += 0.05f;
                player.statDefense *= 0.90f; // -15% Total Defense
            } 
            else {
                // MODE 2 (DEFENSE): Heal +3 HP saat kena hit/dodge
                player.setBonus = "Defense Mode: +8 Defense, heal 3 HP on hit/dodge (1s cd), but Damage reduced by 10%";
                player.statDefense += 8;
                player.GetDamage(DamageClass.Generic) -= 0.10f;
            }
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ModContent.ItemType<BossDrop.BrokenTv>(), 4)
                .AddIngredient(5061)
                .AddIngredient(1017)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }
}