using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    public class HaloMinionBuff : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex) {
            // FIX: Buff akan tetap aktif jika player memiliki Minion Kecil ATAU Minion Gede
            bool hasKecil = player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.HaloMinion>()] > 0;
            bool hasGede = player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.HaloMinionGede>()] > 0;

            if (hasKecil || hasGede) {
                player.buffTime[buffIndex] = 18000;
            }
            else {
                player.DelBuff(buffIndex);
                buffIndex--;
            }
        }
    }
}