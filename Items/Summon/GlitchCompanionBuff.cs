using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Items.Summon
{
    public class GlitchCompanionBuff : ModBuff
    {
        public override string Texture => "Terraria/Images/Buff_307";

        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<GlitchCompanionProj>()] > 0) {
                player.buffTime[buffIndex] = 18000;
            } else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}