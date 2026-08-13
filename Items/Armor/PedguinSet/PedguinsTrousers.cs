using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.PedguinSet
{
	[AutoloadEquip(EquipType.Legs)]
	public class PedguinsTrousers : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 18;
			Item.height = 18;
			Item.value = Item.sellPrice(0, 1, 50, 0);
			Item.rare = ItemRarityID.LightRed; // Early Hardmode Tier
			Item.defense = 8;
		}

		public override void UpdateEquip(Player player)
		{
			player.GetCritChance(DamageClass.Ranged) += 6; // +6% Ranged Crit
			player.moveSpeed += 0.12f;                      // +12% Movement Speed
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "RangedCrit", "6% increased ranged critical strike chance"));
			tooltips.Add(new TooltipLine(Mod, "MoveSpeed", "12% increased movement speed"));

			var flavorLine = new TooltipLine(Mod, "FlavorText", "'Lightweight formal trousers built for swift maneuvering.'")
			{
				OverrideColor = Color.Cyan
			};
			tooltips.Add(flavorLine);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(3759, 1) // Pedguin's Trousers (Vanilla ID)
				.AddIngredient(ItemID.PalladiumBar, 12)
				.AddIngredient(ItemID.FrostDaggerfish, 50)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}