using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.PedguinSet
{
	[AutoloadEquip(EquipType.Head)]
	public class PedguinsHood : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 18;
			Item.height = 18;
			Item.value = Item.sellPrice(0, 1, 50, 0);
			Item.rare = ItemRarityID.LightRed; 
			Item.defense = 3; // Rebalanced for Early Hardmode
		}

		public override void UpdateEquip(Player player)
		{
			player.GetDamage(DamageClass.Ranged) += 0.04f; // +5% Ranged Damage
			player.GetCritChance(DamageClass.Ranged) += 5;  // +3% Ranged Crit
		}

		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			tooltips.Add(new TooltipLine(Mod, "RangedDamage", "Increases ranged damage by 4%"));
			tooltips.Add(new TooltipLine(Mod, "RangedCrit", "5% increased ranged critical strike chance"));

			var flavorLine = new TooltipLine(Mod, "FlavorText", "'A classy secret agent hood with a cool factor.'")
			{
				OverrideColor = Color.Cyan
			};
			tooltips.Add(flavorLine);
		}

		public override bool IsArmorSet(Item head, Item body, Item legs)
		{
			return body.type == ModContent.ItemType<PedguinsJacket>() 
				&& legs.type == ModContent.ItemType<PedguinsTrousers>();
		}

		public override void UpdateArmorSet(Player player)
		{
			player.setBonus = "Taking damage grants Rapid Healing for 2 seconds\n" +
			                  "Ranged attacks fill the Hit Bar (8 Hits)\n" +
			                  "When full, the next hit triggers 8 homing Frost Cards surrounding the target\n" +
			                  "Killed enemies explode into a deadly Shatter Nova";

			player.GetModPlayer<PedguinPlayer>().hasAbsoluteZeroSet = true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(3757, 1) // Pedguin's Hood (Vanilla ID)
				.AddIngredient(ItemID.PalladiumBar, 10)
				.AddIngredient(ItemID.FrostDaggerfish, 50)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}