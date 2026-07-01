using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.CostumeRarity
{
    public class CelestialVanillaOverride : GlobalItem
    {
        public override void SetDefaults(Item item)
        {
            // Jika item tersebut memiliki rarity bawaan RED (Rarity 10)
            if (item.rare == ItemRarityID.Red)
            {
                // Langsung timpa menjadi Celestial Rarity milik mod kamu
                item.rare = ModContent.RarityType<CelestialRarity>();
            }
        }
    }
}