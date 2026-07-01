using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class LycanthropyPlayer : ModPlayer
    {
        // Variabel penanda status rahasia khusus UNTUK PER PLAYER (Aman dari Bug Multiplayer)
        public bool isLycanCurseActive = false;

        public override void ResetEffects()
        {
            // Reset efek setiap frame khusus untuk player yang bersangkutan saja
            isLycanCurseActive = false;
        }

        // =========================================================================
        // [MULTI-CLIENT SAFE CORE]: KUNCI AMAN SEBELUM GAME UPDATE
        // =========================================================================
        public override void PreUpdate()
        {
            // Pastikan kode ini hanya memproses player lokal (Client masing-masing) agar tidak bug net
            if (Player.whoAmI == Main.myPlayer)
            {
                // Jika masih malam hari dan player terinfeksi, paksa buff-nya REPEAT secara konstan!
                if (!Main.dayTime && isLycanCurseActive)
                {
                    // Jika engine vanilla menghapusnya, suntik mati-matian di setiap frame agar UI visualnya tetap muncul
                    if (!Player.HasBuff(BuffID.Werewolf))
                    {
                        Player.AddBuff(BuffID.Werewolf, 36000);
                    }
                }
            }
        }

        public override void PreUpdateBuffs()
        {
            if (!Main.dayTime)
            {
                // Cek status infeksi secara mandiri per individu player
                if (Player.HasBuff(BuffID.Werewolf))
                {
                    isLycanCurseActive = true;
                }
            }
            else
            {
                // Bersihkan kutukan secara adil saat fajar menyingsing
                isLycanCurseActive = false;
                if (Player.HasBuff(BuffID.Werewolf))
                {
                    Player.ClearBuff(BuffID.Werewolf);
                }
            }
        }

        public override void PostUpdateBuffs()
        {
            // =========================================================================
            // [SUPER FORCE VISUAL & STATS]: PAKSA WUJUD DAN UI TETAP NYALA
            // =========================================================================
            if (isLycanCurseActive && !Main.dayTime)
            {
                // Paksa variabel wujud internal player agar visual tubuh serigalanya aktif di server maupun client
                Player.wereWolf = true;

                // Cari indeks slot kotak buff visual player saat ini secara realtime
                for (int i = 0; i < Player.MaxBuffs; i++)
                {
                    if (Player.buffType[i] == BuffID.Werewolf)
                    {
                        // Paksa kunci durasi waktunya di 10 menit agar tidak menyusut atau berkedip hilang
                        Player.buffTime[i] = 36000;
                        break; 
                    }
                }

                // =========================================================================
                // [STAT BALANCING LOCATION]: SUNTIKAN BONUS STAT AKURAT
                // =========================================================================
                Player.GetDamage(DamageClass.Melee) += 0.051f;     // +5.1% Melee Damage
                Player.GetCritChance(DamageClass.Melee) += 2f;     // +2% Melee Critical Strike Chance
                Player.GetAttackSpeed(DamageClass.Melee) += 0.051f; // +5.1% Melee Speed
                Player.moveSpeed += 0.05f;                         // +5% Movement Speed
                Player.statDefense += 3;                           // +3 Defense

                // Kapasitas Lompatan Werewolf
                Player.jumpHeight += 2;
                Player.jumpSpeed += 0.2f;

                // Mekanik HP Regen (1 lifeRegen = 0.5 HP/detik)
                Player.lifeRegen += 1;
            }
        }
    }
}