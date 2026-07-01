using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC GLOBAL REWORK]: ETERNIA GOBLIN TIER 1, 2, & 3 REWORK
    // =========================================================================
    public class EterniaGoblinRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.DD2GoblinT1 || 
                   entity.type == NPCID.DD2GoblinT2 || 
                   entity.type == NPCID.DD2GoblinT3;
        }

        public override void SetDefaults(NPC npc)
        {
            // =========================================================================
            // [GUIDE & BALANCING LOKASI: KNOCKBACK RESISTANCE (KETAHANAN DI-PENTAL)]
            // =========================================================================
            // Di engine Terraria, nilai 'knockBackResist' dihitung terbalik:
            // 1.0f = Menerima knockback penuh 100% (Artinya ketahanan = 0%)
            // 0.6f = Menerima knockback 60%       (Artinya ketahanan = 40%)
            // 0.2f = Menerima knockback 20%       (Artinya ketahanan = 80%)
            // 0.0f = Imun total / ketahanan 100%
            // =========================================================================

            if (npc.type == NPCID.DD2GoblinT2)
            {
                // TIER 2: Mendapatkan resistensi Knockback sebanyak 40%
                npc.knockBackResist = 0.6f; 
            }
            else if (npc.type == NPCID.DD2GoblinT3)
            {
                // TIER 3: Kebal knockback naik menjadi 80%
                npc.knockBackResist = 0.2f; 
            }
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            if (npc.type == NPCID.DD2GoblinT1 || npc.type == NPCID.DD2GoblinT2 || npc.type == NPCID.DD2GoblinT3)
            {
                // =========================================================================
                // [GUIDE & BALANCING LOKASI: DURASI DEBUFF BROKEN ARMOR]
                // =========================================================================
                // Hitungan waktu Terraria menggunakan Ticks (60 Ticks = 1 Detik).
                // Saat ini diatur ke 300 Ticks yang berarti efek durasinya adalah 5 Detik.
                // =========================================================================

                int brokenArmorDuration = 90; 

                target.AddBuff(BuffID.BrokenArmor, brokenArmorDuration);
            }
        }
    }
}