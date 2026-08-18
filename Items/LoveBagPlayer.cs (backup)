using Terraria;
using Terraria.ModLoader;
using System.Collections.Generic;
using TheSanity.Items;

namespace TheSanity
{
    public class LoveBagPlayer : ModPlayer
    {
        public override IEnumerable<Item> AddStartingItems(bool mediumCoreDeath)
        {
            // Mencegah player dapat bag lagi kalau mereka mati di mode Mediumcore
            if (mediumCoreDeath)
            {
                return base.AddStartingItems(mediumCoreDeath);
            }

            return new[] {
                new Item(ModContent.ItemType<LoveBag>(), 1)
            };
        }
    }
}