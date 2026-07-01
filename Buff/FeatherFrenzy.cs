using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    public class FeatherFrenzy : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex) {
            // Menambah kecepatan serangan minion
            player.GetAttackSpeed(DamageClass.Summon) += 0.10f;
        }
    }
}