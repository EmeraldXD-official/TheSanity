using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.OreBar.Ambarium;

namespace TheSanity.Items
{
	public class AmbariumPickaxe : ModItem
	{
		public override void SetDefaults()
		{
			Item.width = 42;
			Item.height = 42;
			Item.damage = 40;
			Item.DamageType = DamageClass.Melee;
			Item.useTime = 12;
			Item.useAnimation = 16;
			Item.useStyle = ItemUseStyleID.Swing;
			Item.knockBack = 4f;
			Item.value = Item.sellPrice(0, 4, 50, 0);
			Item.rare = ItemRarityID.Pink;
			Item.UseSound = SoundID.Item1;
			Item.autoReuse = true;
			Item.crit = 6;

			// Statistik Penambangan & Penebangan
			Item.pick = 100;            // 100% Pickaxe Power (Maksimal menambang Cobalt / Palladium)
			Item.axe = 20;              // 20 x 5 = 100% Axe Power (Bisa menebang pohon)
			Item.tileBoost = 1;         // Jangkauan tambang +1 tile
		}

		// Efek cahaya ungu di sekitar pickaxe saat dipegang
		public override void HoldItem(Player player)
		{
			Vector2 position = player.Center + new Vector2(player.direction * 16, -10);
			Lighting.AddLight(position, 0.6f, 0.15f, 0.8f);
		}

		// Mengubah tooltip bawaan engine untuk tool agar tertulis "melee damage" secara spesifik
		public override void ModifyTooltips(List<TooltipLine> tooltips)
		{
			foreach (var line in tooltips)
			{
				if (line.Mod == "Terraria" && line.Name == "Damage")
				{
					line.Text = Item.damage + " melee damage";
				}
			}

			// Tooltips deskripsi tambahan
			var descLine = new TooltipLine(Mod, "AmbariumPickaxeDesc",
				"Forged from pure Ambarium energy\n" +
				"Capable of mining up to Cobalt ore\n" +
				"Doubles as an axe to chop down trees\n" +
				"Emits a magical purple resonance");
			tooltips.Add(descLine);
		}

		public override void AddRecipes()
		{
			CreateRecipe()
				.AddIngredient(ModContent.ItemType<AmbariumBar>(), 12)
				.AddTile(TileID.Anvils)
				.Register();
		}
	}
}