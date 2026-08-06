using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class GlobalExtractinator : GlobalItem
    {
        public override void ExtractinatorUse(int extractType, int extractStyle, ref int resultType, ref int resultStack)
        {
            // 10% chance (1 dari 10 kali pemrosesan)
            if (Main.rand.NextBool(10))
            {
                resultType = ItemID.Coal;
                
                // Menghasilkan jumlah acak antara 3 sampai 5
                resultStack = Main.rand.Next(3, 6); 
            }
        }
    }
}