using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.AnotherReworked
{
    public class FrankensteinImprovement : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // =========================================================================
        // 1. SETTING DEFAULTS: MODIFIKASI STATUS DASAR (KNOCKBACK & IMMUNITY)
        // =========================================================================
        public override void SetDefaults(NPC npc)
        {
            // Mengecek apakah NPC yang di-spawn adalah Frankenstein bawaan game
            if (npc.type == NPCID.Frankenstein)
            {
                // -------------------------------------------------------------------------
                // LOKASI BALANCING 1: KNOCKBACK RESISTANCE (KEBAL EFEK DORONGAN)
                // Nilai dipatok kaku ke 0f (artinya 100% Kebal / Tidak bisa didorong sama sekali).
                // Nilai bawaan normal biasanya 0.4f s/d 0.8f. Jika ingin bisa didorong sedikit, naikkan ke 0.2f.
                // -------------------------------------------------------------------------
                npc.knockBackResist = 0f;

                // -------------------------------------------------------------------------
                // LOKASI BALANCING 2: IMMUNITY SYSTEM (KEBAL SEMUA DEBUFF KECUALI API)
                // Mengulang semua daftar buff di dalam database Terraria.
                // Kita kecualikan debuff bertema api (OnFire, Hellfire, CursedInferno, ShadowFlame)
                // agar monster ini bisa dibakar dan menerima efek kelemahan 2x damage-nya!
                // -------------------------------------------------------------------------
                for (int i = 1; i < BuffLoader.BuffCount; i++)
                {
                    if (i != BuffID.OnFire && 
                        i != BuffID.OnFire3 &&       // Hellfire (Debuff api tingkat lanjut)
                        i != BuffID.CursedInferno && // Api Kutukan Corruption
                        i != BuffID.ShadowFlame)     // Api Bayangan
                    {
                        npc.buffImmune[i] = true; // Set menjadi true untuk kebal
                    }
                }
            }
        }

        // =========================================================================
        // 2. MODIFIKASI HIT DARI SENJATA JARAK DEKAT / ITEM (MELEE, DLL)
        // =========================================================================
        public override void ModifyHitByItem(NPC npc, Player player, Item item, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.Frankenstein)
            {
                // -------------------------------------------------------------------------
                // LOKASI BALANCING 3: REDUKSI DAMAGE TIPE MAGE (MAGIC DAMAGE CLASS)
                // Jika diserang menggunakan senjata tipe Mage, damage akan dipotong besar-besaran.
                // Pengali 0.15f artinya dia hanya menerima 15% damage (Reduksi/Pertahanan sebesar 85%!).
                // Silakan ubah angka desimalnya untuk balancing (contoh: 0.30f untuk hanya menerima 30% damage).
                // -------------------------------------------------------------------------
                if (item.DamageType == DamageClass.Magic)
                {
                    modifiers.FinalDamage *= 0.15f; 
                }

                // -------------------------------------------------------------------------
                // LOKASI BALANCING 4: PENGALI DAMAGE SAAT TERKENA STATUS API (PYROPHOBIA)
                // Sesuai dengan kelemahannya, jika Frankenstein sedang menderita debuff api,
                // pukulan masuk dari item/senjata apa pun akan dikalikan 2.0f (2x Lipat Lebih Sakit!).
                // -------------------------------------------------------------------------
                if (npc.HasBuff(BuffID.OnFire) || npc.HasBuff(BuffID.OnFire3) || npc.HasBuff(BuffID.CursedInferno) || npc.HasBuff(BuffID.ShadowFlame))
                {
                    modifiers.FinalDamage *= 2.0f;
                }
            }
        }

        // =========================================================================
        // 3. MODIFIKASI HIT DARI PROYEKTIL (PELURU, SIHIR, PANAH, MINI-SHARK, DLL)
        // =========================================================================
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.Frankenstein)
            {
                // Reduksi damage besar-besaran jika proyektil berasal dari serangan kelas Mage
                if (projectile.DamageType == DamageClass.Magic)
                {
                    modifiers.FinalDamage *= 0.15f; // Hanya menerima 15% damage dari sihir Mage
                }

                // Menerima damage 2x lipat dari segala proyektil jika tubuhnya sedang terbakar debuff api
                if (npc.HasBuff(BuffID.OnFire) || npc.HasBuff(BuffID.OnFire3) || npc.HasBuff(BuffID.CursedInferno) || npc.HasBuff(BuffID.ShadowFlame))
                {
                    modifiers.FinalDamage *= 2.0f;
                }
            }
        }

        // =========================================================================
        // 4. MULTIPLIER DAMAGE OVER TIME (DOT) - SIKLUS PENGURANGAN DARAH KARENA API
        // =========================================================================
        public override void UpdateLifeRegen(NPC npc, ref int damage)
        {
            if (npc.type == NPCID.Frankenstein)
            {
                // -------------------------------------------------------------------------
                // LOKASI BALANCING 5: DAMAGE TICK DEBUFF API
                // Di bawah ini kita melipatgandakan damage 'bakar' yang menguras darahnya per detik.
                // npc.lifeRegen dikali 2 membuat HP-nya merosot 2x lebih cepat saat terbakar.
                // damage dikali 2 membuat angka orange/merah tik yang melayang di atas kepalanya ikut dikali 2.
                // -------------------------------------------------------------------------
                
                // Jika terkena On Fire! biasa
                if (npc.HasBuff(BuffID.OnFire))
                {
                    npc.lifeRegen *= 2; 
                    damage *= 2;        
                }
                
                // Jika terkena Hellfire (Api neraka tingkat lanjut)
                if (npc.HasBuff(BuffID.OnFire3))
                {
                    npc.lifeRegen *= 2;
                    damage *= 2;
                }

                // Jika terkena CursedInferno
                if (npc.HasBuff(BuffID.CursedInferno))
                {
                    npc.lifeRegen *= 2;
                    damage *= 2;
                }
            }
        }
    }
}