using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class EvilPenguinRework : global::Terraria.ModLoader.GlobalNPC
    {
        // Menggunakan OnHitPlayer untuk mendeteksi serangan sentuhan badan (Contact Damage)
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo info)
        {
            // LOKASI TIMING DEBUFF: 300 frame = 5 Detik (60 frame = 1 detik)
            int debuffDuration = 300;

            // --- PROTEKSI CRITTER BIASA (ANTI SALAH SASAR) ---
            // Kita cek apakah NPC ini disusupi oleh GlobalNPC PassiveReworked dan sedang dalam kondisi mengamuk
            if (npc.TryGetGlobalNPC<PassiveReworked>(out var passiveNpc) && passiveNpc.isEnraged)
            {
                // Jika ini kelinci biasa yang lagi ngamuk, hentikan kode di sini agar tidak memberikan debuff kutukan
                return; 
            }

            // --- 1. GOLONGAN CORRUPTION (Cursed Inferno + Blind) ---
            // Hanya berlaku untuk Corrupt Penguin (168) dan Corrupt Bunny Asli (46)
            if (npc.type == 168 || npc.type == 46)
            {
                // Menginfeksi Cursed Inferno (ID 39) selama 5 detik
                target.AddBuff(39, debuffDuration);

                // Menginfeksi Blind (ID 22) selama 5 detik
                target.AddBuff(22, debuffDuration);
            }

            // --- 2. GOLONGAN CRIMSON (Ichor + Bleeding) ---
            // Hanya berlaku untuk Vicious Penguin (470) dan Vicious Bunny Asli (301)
            if (npc.type == 470 || npc.type == 301)
            {
                // Menginfeksi Ichor (ID 69) selama 5 detik
                target.AddBuff(69, debuffDuration);

                // Menginfeksi Bleeding (ID 30) selama 5 detik
                target.AddBuff(30, debuffDuration);
            }
        }
    }
}