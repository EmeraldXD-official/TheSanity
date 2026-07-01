using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    public class HomunculusBuff : ModBuff
    {
        public override string Texture => "TheSanity/Buff/HomunculusBuff"; // Referensi: HomunculusBuff.png

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            // Jika player memiliki minion ini, kunci durasi buff agar tidak habis
            if (player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.HomunculusMinion>()] > 0)
            {
                player.buffTime[buffIndex] = 18000;
            }
            
        }
    }
}