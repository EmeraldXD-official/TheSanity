using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.OreBar.Ambarium;
namespace TheSanity.Items.Armor.RiperArmor
{
	[AutoloadEquip(EquipType.Body)]
	public class RiperRobe : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 18;
			Item.height = 18;
			Item.value = Item.buyPrice(gold: 8);
			Item.rare = ItemRarityID.LightRed;
			Item.defense = 12;
		}

		public override void UpdateEquip(Player player)
		{
			player.GetDamage(DamageClass.Generic) += 0.05f;      // +5% damage
			player.GetCritChance(DamageClass.Generic) += 10f;    // +10% crit chance
		}

		public override void AddRecipes()
		{
			CreateRecipe()
			.AddIngredient<AmbariumBar>(10)
				.AddIngredient(521, 12)
				.AddIngredient(1327)
				.AddIngredient(1820)
				.AddIngredient(ItemID.ShadowScale, 10) // placeholder, replace with your own hardmode material
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}