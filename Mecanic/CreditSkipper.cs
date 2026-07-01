using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Graphics.Effects; // Diperlukan untuk mengakses SkyManager

namespace TheSanity
{
    public class CreditSkipper : ModSystem
    {
        public override void PostUpdateEverything()
        {
            // Cek apakah player menyalakan fitur Skip di Config
            if (ModContent.GetInstance<SanityConfig>().SkipCredits)
            {
                // Memeriksa apakah efek langit "CreditsRoll" sedang aktif di game
                if (SkyManager.Instance["CreditsRoll"] != null && SkyManager.Instance["CreditsRoll"].IsActive())
                {
                    // =========================================================================
                    // [CREDITS SKIP TOGGLE LOCATION]
                    // =========================================================================
                    // Paksa matikan efek visual kredit secara instan dari layar
                    SkyManager.Instance.Deactivate("CreditsRoll");

                    // =========================================================================
                    // [MUSIC RESET LOCATION]
                    // =========================================================================
                    // FIX SILENCE BUG: Jangan paksa Main.curMusic = 0 karena akan membekukan audio engine.
                    // Kita cukup paksa volume (fade) spesifik dari lagu Kredit milik Moon Lord menjadi 0.
                    // Dengan cara ini, Terraria akan langsung melakukan cross-fade ke musik bioma terdekat secara instan dan normal.
                    Main.musicFade[MusicID.Credits] = 0f;
                }
            }
        }
    }
}