using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Mecanic
{
    public class ForcedDifficultySystem : ModSystem
    {
        // =========================================================================
        // DIFFICULTY LOCK BALANCING LOCATION
        // =========================================================================
        private void EnforceMasterOrLegendary()
        {
            // PENGECUALIAN: Kalau world ini emang dibuat sebagai Journey Mode, jangan diapa-apain.
            // Main.GameMode sendiri yang jadi penentu apakah ini world Journey (Creative), jadi
            // dicek duluan sebelum dipaksa, supaya world Journey tetap Journey.
            if (Main.GameMode == GameModeID.Creative)
            {
                return;
            }

            // Cek apakah world saat ini menggunakan seed rahasia 'For The Worthy' atau 'Get Fixed Boi'
            if (Main.getGoodWorld || Main.zenithWorld)
            {
                // Paksa Game Mode ke ID 3 (Di dalam internal Terraria, Master + Special Seed = Legendary Difficulty)
                Main.GameMode = GameModeID.Master; 
                
                // Catatan Internal Engine: Terraria otomatis mengubah tampilan UI dan stat monster 
                // menjadi Legendary jika Main.GameMode bernilai Master (2) pada seed khusus ini.
            }
            else
            {
                // Jika world biasa (Classic/Expert), paksa dan kunci langsung ke Master Mode (ID 2)
                // Journey Mode sudah di-skip di atas.
                Main.GameMode = GameModeID.Master;
            }
        }

        // Pemicu 1: Saat player baru selesai membuat World baru di menu
        public override void PostWorldGen()
        {
            EnforceMasterOrLegendary();
        }

        // Pemicu 2: Setiap kali World dimuat/buka masuk ke dalam game (Singleplayer & Multiplayer)
        public override void OnWorldLoad()
        {
            EnforceMasterOrLegendary();
        }

        // Pemicu 3: Setiap frame di dalam game untuk mencegah player mengubah difficulty lewat mod lain (Cheat Sheet/Hero's Mod)
        public override void PostUpdateEverything()
        {
            EnforceMasterOrLegendary();
        }
    }
}