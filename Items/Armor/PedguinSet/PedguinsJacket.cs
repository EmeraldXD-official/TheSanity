using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.PedguinSet
{
	[AutoloadEquip(EquipType.Body)]
	public class PedguinsJacket : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 18;
			Item.height = 18;
			Item.value = Item.sellPrice(0, 2, 0, 0);
			Item.rare = ItemRarityID.LightRed; 
			Item.defense = 10; // Rebalanced for Early Hardmode
		}

		public override void UpdateEquip(Player player)
		{
			player.GetDamage(DamageClass.Ranged) += 0.03f; // +6% Ranged Damage
			player.moveSpeed += 0.05f;                      // +5% Movement Speed
			player.ammoCost80 = true;                      // 20% Chance to save ammo
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "RangedDamage", "Increases ranged damage by 3%"));
			tooltips.Add(new TooltipLine(Mod, "MoveSpeed", "5% increased movement speed"));
			tooltips.Add(new TooltipLine(Mod, "AmmoSave", "20% chance to not consume ammo"));

			var flavorLine = new TooltipLine(Mod, "FlavorText", "'A bulletproof tactical tuxedo lined with solid ice.'")
			{
				OverrideColor = Color.Cyan
			};
			tooltips.Add(flavorLine);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(3758, 1) // Pedguin's Jacket (Vanilla ID)
				.AddIngredient(ItemID.PalladiumBar, 14)
				.AddIngredient(ItemID.FrostDaggerfish, 50)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}