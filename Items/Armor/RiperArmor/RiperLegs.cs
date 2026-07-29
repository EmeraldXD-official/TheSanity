using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.OreBar.Ambarium;

namespace TheSanity.Items.Armor.RiperArmor
{
	[AutoloadEquip(EquipType.Legs)]
	public class RiperLegs : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 18;
			Item.height = 18;
			Item.value = Item.buyPrice(gold: 7);
			Item.rare = ItemRarityID.LightRed;
			Item.defense = 10;
		}

		public override void UpdateEquip(Player player)
		{
			// Placeholder split - tweak freely. Hood already covers +10% damage
			// / +10% move speed, Robe covers +5% damage / +10% crit, so Legs
			// leans into mobility to round the set out.
			player.GetDamage(DamageClass.Generic) += 0.05f; // +5% damage
			player.moveSpeed += 0.05f;                        // +5% movement speed
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<AmbariumBar>(8)
				.AddIngredient(521, 10) // placeholder, replace with your own hardmode material
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}