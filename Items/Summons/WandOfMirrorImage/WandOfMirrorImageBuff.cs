using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Summons.WandOfMirrorImage
{
    // Buff standar ala vanilla summon staff: nge-hold slot minion tetap "hidup" selama buff aktif,
    // dan otomatis ke-remove kalau minion-nya sendiri udah mati/gak ada.
    public class WandOfMirrorImageBuff : ModBuff
    {
        public override string Texture => "TheSanity/Items/Summons/WandOfMirrorImage/WandOfMirrorImageItem";

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<WandOfMirrorImageMinion>()] > 0)
                player.buffTime[buffIndex] = 18000;
            else
                player.DelBuff(buffIndex--);
        }
    }
}