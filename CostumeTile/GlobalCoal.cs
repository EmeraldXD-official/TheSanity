using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.CostumeTile;

namespace TheSanity
{
    public class GlobalCoal : GlobalItem
    {
        public override void SetDefaults(Item item)
        {
            if (item.type == ItemID.Coal)
            {
                item.maxStack = 9999;
                item.createTile = ModContent.TileType<CoalOre>();
                item.useStyle = ItemUseStyleID.Swing;
                item.useTurn = true;
                item.useAnimation = 15;
                item.useTime = 10;
                item.autoReuse = true;
                item.consumable = true;
                item.UseSound = SoundID.Item1;
            }
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            if (item.type == ItemID.Coal)
            {
                // Menghapus seluruh tooltip bawaan Terraria ("You've been Naughty this year")
                tooltips.RemoveAll(line => line.Mod == "Terraria" && line.Name.StartsWith("Tooltip"));

                // Menambahkan tooltip kustom milikmu
                tooltips.Add(new TooltipLine(Mod, "CoalLore", "'Burn your ores with me'"));
            }
        }
    }
}