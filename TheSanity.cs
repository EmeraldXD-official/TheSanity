using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Terraria.ModLoader;

namespace TheSanity
{
	// Please read https://github.com/tModLoader/tModLoader/wiki/Basic-tModLoader-Modding-Guide#mod-skeleton-contents for more information about the various files in a mod.
	public class TheSanity : Mod
	{

	}

	// =========================================================================
	// CONFIG: MENAMPILKAN ICON MOD DI FILTER BESTIARY (BESTIARY ICON LOCATION)
	// =========================================================================
	public class TheSanityBestiaryIcon : ModBiome
	{
		// PAKSA PATH: Mengarahkan game secara manual ke file "BestiaryIconMod.png" di folder utama
		public override string BestiaryIcon => "TheSanity/BestiaryIconMod";

		public override bool IsBiomeActive(Terraria.Player player) 
		{
			// Dikunci false karena ini murni hanya untuk memunculkan Icon di filter Bestiary
			return false; 
		}
	}
}