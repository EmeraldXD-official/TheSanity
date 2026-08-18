using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using YourModName.Content.Items;
using YourModName.Content.NPCs;
using YourModName.Content.Buffs;

namespace YourModName.Content.Players
{
    // ==========================================
    // MEKANIK OPTICAL WRENCH:
    //  1. Mana -10 tiap kali Twins-ally menyerang (dipanggil dari
    //     TwinsAllySpazmatism/TwinsAllyRetinazer lewat ConsumeAttackMana).
    //  2. Kalau mana abis SAAT staff dipegang -> damage senjata staff -10%.
    //  3. Selama staff dipegang -> natural life regen di-cut 50%.
    //  4. Selama staff dipegang -> damage staff naik, "memakan" 10% Defense
    //     player jadi bonus damage flat (defense player ikut berkurang
    //     sebesar itu juga - "semakin banyak def semakin sakit").
    //  5. Kalau staff DILEPAS (gak lagi dipegang) SAAT Twins lagi
    //     menghit/mengejar target -> semua damage player (segala Class &
    //     senjata) diminus 50% selama 1 menit (TwinsBacklashDebuff).
    // ==========================================
    public class TwinsStaffPlayer : ModPlayer
    {
        public bool HoldingStaff;
        private bool WasHoldingStaff;
        private bool WasTwinsEngaged;
        private float DefenseConsumedThisFrame;
        private bool OutOfManaPenalty;

        public override void ResetEffects()
        {
            HoldingStaff = Player.HeldItem?.ModItem is OpticalWrench;
        }

        public override void PostUpdateEquips()
        {
            bool twinsEngaged = HoldingStaff && IsTwinsEngaged();

            if (HoldingStaff)
            {
                // (3) natural regen di-cut 50%.
                if (Player.lifeRegen > 0)
                    Player.lifeRegen = (int)(Player.lifeRegen * 0.5f);

                // (4) "makan" 10% Defense jadi bonus damage staff, defense
                // player ikut berkurang segitu juga di frame ini.
                DefenseConsumedThisFrame = Player.statDefense * 0.1f;
                Player.statDefense -= (int)DefenseConsumedThisFrame;

                // (2) mana abis -> flag penalty damage staff -10%.
                OutOfManaPenalty = Player.statMana <= 0;
            }
            else
            {
                DefenseConsumedThisFrame = 0f;
                OutOfManaPenalty = false;
            }

            // (5) deteksi momen pelepasan staff SAAT Twins lagi aktif ngejar/nghit.
            if (WasHoldingStaff && !HoldingStaff && WasTwinsEngaged)
            {
                Player.AddBuff(ModContent.BuffType<TwinsBacklashDebuff>(), 60 * 60); // 1 menit
            }

            WasHoldingStaff = HoldingStaff;
            WasTwinsEngaged = twinsEngaged;
        }

        public override void ModifyWeaponDamage(Item item, ref StatModifier damage)
        {
            if (HoldingStaff && item.ModItem is OpticalWrench)
            {
                damage.Flat += DefenseConsumedThisFrame;
                if (OutOfManaPenalty)
                    damage *= 0.9f;
            }

            // Backlash: semua Class & senjata (termasuk staff ini) -50% selama buff aktif.
            if (Player.HasBuff(ModContent.BuffType<TwinsBacklashDebuff>()))
                damage *= 0.5f;
        }

        // Dipanggil dari fire logic Spaz-ally/Ret-ally tiap kali mereka menyerang.
        public void ConsumeAttackMana(Player player)
        {
            if (player.statMana >= 10)
                player.statMana -= 10;
            else
                player.statMana = 0;
        }

        // Damage multiplier tambahan buat proyektil ally (biar konsisten kena
        // penalty out-of-mana / backlash juga, bukan cuma damage.Flat/melee).
        public float CurrentDamageMultiplier(Player player)
        {
            float mult = 1f;
            if (OutOfManaPenalty)
                mult *= 0.9f;
            if (player.HasBuff(ModContent.BuffType<TwinsBacklashDebuff>()))
                mult *= 0.5f;
            return mult;
        }

        private bool IsTwinsEngaged()
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Player.whoAmI && p.ModProjectile is TwinsAllySpazmatism spaz)
                    return spaz.IsChasingOrDashing;
            }
            return false;
        }
    }
}
