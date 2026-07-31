using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Empress.Buffs
{
    // Purely a marker debuff — it doesn't alter the NPC's stats on its own.
    // EmpressGlobalNPC checks for it in OnKill() to trigger the Prismatic Bolt burst.
    public class LightInYourSoulBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.pvpBuff[Type] = true;
        }

        public override void Update(NPC npc, ref int buffIndex)
        {
            // No stat changes — purely a "marked" state.
            // Optional: could tint the NPC or spawn light dust here for visual feedback.
        }
    }
}
