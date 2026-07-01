using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Projectiles.TwinkleWeapon
{
    public class TwinkleTwinkleBuff : ModBuff
    {
        public override string Texture => "TheSanity/Projectiles/TwinkleWeapon/TwinkleTwinkleBuff";

        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            if (player.ownedProjectileCounts[ModContent.ProjectileType<TwinkleTwinkleMinion>()] > 0) {
                player.buffTime[buffIndex] = 18000;
            } else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}