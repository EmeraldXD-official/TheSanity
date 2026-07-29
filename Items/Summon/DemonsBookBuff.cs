using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Items.Summon
{
    public class DemonsBookBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            // Buff tetap aktif selama minion masih ada di dunia
            if (player.ownedProjectileCounts[ModContent.ProjectileType<KindDemonMinion>()] > 0)
            {
                player.buffTime[buffIndex] = 18000;
            }
            else
            {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}