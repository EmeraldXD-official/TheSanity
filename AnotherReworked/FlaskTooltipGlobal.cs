using System.Collections.Generic;
using System.Linq;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Globals
{
    public class FlaskTooltipGlobal : GlobalItem
    {
        // Teks tooltip baru per item Flask vanilla.
        // Silakan sesuaikan kalimatnya sesuka kamu.
        private static readonly Dictionary<int, string> FlaskTooltipOverride = new()
        {
            { ItemID.FlaskofFire,         "All attacks inflict the On Fire! debuff" },
            { ItemID.FlaskofPoison,       "All attacks inflict the Poisoned debuff" },
            { ItemID.FlaskofVenom,        "All attacks inflict the Venom debuff" },
            { ItemID.FlaskofCursedFlames, "All attacks inflict the Cursed Inferno debuff" },
            { ItemID.FlaskofIchor,        "All attacks decrease the target's defense" },
            { ItemID.FlaskofNanites,      "All attacks have a chance to confuse enemies" },
            // Flask of Gold & Flask of Party gak punya efek tempur (cosmetic aja),
            // jadi sengaja gak dimasukin.
        };

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            if (!FlaskTooltipOverride.TryGetValue(item.type, out string newText))
                return;

            // Flask vanilla cuma punya 1 baris tooltip deskripsi ("Tooltip0"),
            // jadi kita cari baris itu dan timpa teksnya.
            TooltipLine line = tooltips.FirstOrDefault(l => l.Mod == "Terraria" && l.Name == "Tooltip0");
            if (line != null)
            {
                line.Text = newText;
            }
        }
    }
}
