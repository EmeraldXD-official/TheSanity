using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.AnotherReworked
{
    public class CreatureFromTheDeepImprovement : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // =========================================================================
        // 1. MODIFIKASI HIT DARI SENJATA JARAK JAUH / ITEM (RANGED CLASS)
        // =========================================================================
        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.CreatureFromTheDeep)
            {
                // -------------------------------------------------------------------------
                // LOKASI BALANCING 1: REDUKSI DAMAGE TIPE RANGE (RANGED DAMAGE CLASS)
                // Karena kulit bersisiknya sangat tebal, senjata Range tidak begitu mempan.
                // Pengali 0.20f artinya dia hanya menerima 20% damage (Reduksi/Pertahanan sebesar 80%!).
                // Silakan ubah desimalnya jika ingin di-balance (contoh: 0.40f untuk hanya menahan 60% damage).
                // -------------------------------------------------------------------------
                if (item.DamageType == DamageClass.Ranged)
                {
                    modifiers.FinalDamage *= 0.20f;
                }

                // -------------------------------------------------------------------------
                // LOKASI BALANCING 2: PENGALI DAMAGE SAAT TERKENA DEBUFF RACUN (POISON/VENOM)
                // Sesuai kelemahannya, jika menderita status racun, pertahanan kulitnya jebol.
                // Semua pukulan masuk dari senjata apa pun akan dikalikan 2.0f (2x Lipat Lebih Sakit!).
                // -------------------------------------------------------------------------
                if (npc.HasBuff(BuffID.Poisoned) || npc.HasBuff(BuffID.Venom))
                {
                    modifiers.FinalDamage *= 2.0f;
                }
            }
        }

        // =========================================================================
        // 2. MODIFIKASI HIT DARI PROYEKTIL (PELURU, PANAH, ROCKET, SHARKTEETH, DLL)
        // =========================================================================
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.CreatureFromTheDeep)
            {
                // Reduksi damage sebesar 80% jika diserang proyektil tipe Ranged
                if (projectile.DamageType == DamageClass.Ranged)
                {
                    modifiers.FinalDamage *= 0.20f;
                }

                // Menerima damage 2x lipat dari proyektil jika tubuhnya sedang keracunan
                if (npc.HasBuff(BuffID.Poisoned) || npc.HasBuff(BuffID.Venom))
                {
                    modifiers.FinalDamage *= 2.0f;
                }
            }
        }

        // =========================================================================
        // 3. MULTIPLIER DAMAGE OVER TIME (DOT) - SIKLUS PENGURANGAN DARAH KARENA RACUN
        // =========================================================================
        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            if (npc.type == NPCID.CreatureFromTheDeep)
            {
                // -------------------------------------------------------------------------
                // LOKASI BALANCING 3: DAMAGE TICK RACUN ALAMI (POISONED) & RACUN JAHAT (VENOM)
                // Di bawah ini kita melipatgandakan efek racun yang menguras sisa HP-nya per detik.
                // npc.lifeRegen dikali 2 membuat HP-nya merosot 2x lebih cepat saat keracunan.
                // damage dikali 2 membuat angka popup ungu yang melayang di atas kepalanya ikut dikali 2.
                // -------------------------------------------------------------------------
                
                // Jika terkena Poisoned biasa (misal dari Poison Dart atau Blade of Grass)
                if (npc.HasBuff(BuffID.Poisoned))
                {
                    npc.lifeRegen *= 2;
                    damage *= 2;
                }

                // Jika terkena Venom (Racun mematikan tingkat lanjut)
                if (npc.HasBuff(BuffID.Venom))
                {
                    npc.lifeRegen *= 2;
                    damage *= 2;
                }
            }
        }
    }
}