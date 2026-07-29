using Terraria;
using Terraria.ModLoader;
using TheSanity.Buff; // Memanggil namespace folder Buff kamu

namespace TheSanity
{
    public class MeleeLockoutPlayer : ModPlayer
    {
        // =========================================================================
        // HOOK 1: OPSI NUKLIR - MEMBEKUKAN ANIMASI DAN MELENYAPKAN PROYEKTIL MELEE
        // =========================================================================
        public override void PreUpdate()
        {
            // Cek apakah player memiliki debuff MeleeLockout
            if (Player.HasBuff(ModContent.BuffType<MeleeLockout>()))
            {
                // A. JIKA SEDANG MEMEGANG SENJATA MELEE:
                // Paksa reset animasi ke 0 agar ayunan/putaran Zenith langsung macet total
                if (Player.HeldItem.DamageType == DamageClass.Melee)
                {
                    Player.itemAnimation = 0; // Memaksa animasi ayunan tangan berhenti detik ini juga
                    Player.itemTime = 0;      // Memaksa jeda cooldown senjata kembali ke nol
                    Player.controlUseItem = false; 
                    Player.channel = false;        
                }

                // B. PEMBERSIHAN TOTAL PROYEKTIL MELEE (SOLUSI UTAMA YOYO & ZENITH):
                // Kita scan seluruh map, jika ada proyektil melee milik player ini, HANCURKAN!
                // Ini akan langsung menghapus piringan Zenith dan lingkaran Yoyo dari eksistensi.
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    Projectile p = Main.projectile[i];
                    
                    // Syarat: Proyektil harus aktif, milik player ini, dan tipenya murni Melee
                    if (p.active && p.owner == Player.whoAmI && p.DamageType == DamageClass.Melee)
                    {
                        p.Kill(); // Lenyapkan proyektil secara instan tanpa ampun
                    }
                }
            }
        }

        // =========================================================================
        // HOOK 2: PROTEKSI BACKUP (AGAR TIDAK BISA KLIK BARU)
        // =========================================================================
        public override bool CanUseItem(Item item)
        {
            if (Player.HasBuff(ModContent.BuffType<MeleeLockout>()) && item.DamageType == DamageClass.Melee)
            {
                return false; 
            }

            return base.CanUseItem(item);
        }
    }
}