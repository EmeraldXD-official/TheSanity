using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Localization;
using TheSanity.Items.OreBar.Ambarium;

namespace TheSanity.Items.Armor.RiperArmor
{
	[AutoloadEquip(EquipType.Head)]
	public class RiperHood : ModItem
	{
		public override void SetStaticDefaults()
		{
			// Displayed in the set bonus tooltip when both pieces are worn
			// (Localization/en-US.hjson: Mods.TheSanity.Items.RiperHood.DisplayName / Tooltip)
		}

		public override void SetDefaults()
		{
			Item.width = 18;
			Item.height = 18;
			Item.value = Item.buyPrice(gold: 5);
			Item.rare = ItemRarityID.LightRed;
			Item.defense = 18;
		}

		public override void UpdateEquip(Player player)
		{
			player.GetDamage(DamageClass.Generic) += 0.10f; // +10% damage
			player.moveSpeed += 0.10f;                      // +10% movement speed
		}

		public override bool IsArmorSet(Item head, Item body, Item legs)
		{
			// Full 3-piece set: hood + robe + legs must all match.
			return head.type == ModContent.ItemType<RiperHood>()
				&& body.type == ModContent.ItemType<RiperRobe>()
				&& legs.type == ModContent.ItemType<RiperLegs>();
		}

		public override void UpdateArmorSet(Player player)
		{
			// Called automatically by tModLoader when hood + body (+ optional legs) match
			player.setBonus = Language.GetTextValue("Mods.TheSanity.Items.RiperHood.SetBonus");

			// Flag read by RiperPlayer to handle Wrath NPC summon logic
			player.GetModPlayer<Players.RiperPlayer>().riperSetActive = true;
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient<AmbariumBar>(10)
				.AddIngredient(521, 12)
				.AddIngredient(1819)
				.AddIngredient(ItemID.ShadowScale, 8) // placeholder, replace with your own hardmode material
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}