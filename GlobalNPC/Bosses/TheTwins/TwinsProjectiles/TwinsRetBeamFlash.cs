using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // TwinsRetBeamFlash — ModSystem overlay "flashbang" MERAH non-solid di layar, dipicu SETIAP
    // KALI TwinsRetBeam lepas tembakan (transisi Aiming -> FullBeam, lihat TwinsRetBeam.Tick).
    //
    // Dipisah jadi ModSystem sendiri (bukan digambar manual di TwinsReworkOverride.PreDraw)
    // karena ini overlay LAYAR PENUH, bukan sesuatu yang nempel ke posisi NPC/proyektil —
    // jadi cocoknya digambar lewat hook PostDrawInterface, yang jalan SETELAH semua render
    // dunia/game kelar (di atas segalanya, termasuk boss health bar & UI game), sama kayak
    // efek vignette/flash lain yang biasa dipakai boss mod-mod besar.
    //
    // PENTING: overlay ini SENGAJA DI-CAP alpha-nya (FlashMaxAlpha < 1) supaya TIDAK PERNAH
    // nutupin 100% opacity — player tetap bisa lihat layar walau lagi "kesilau", cuma
    // dikasih tint merah sesaat. Fade-nya juga ease-out cepat (langsung terang pas awal,
    // terus turun cepat ke 0), bukan konstan lalu ilang mendadak, biar berasa kayak
    // "kedip" bukan "layar berubah warna".
    // ==========================================
    public class TwinsRetBeamFlash : ModSystem
    {
        private const int FlashDuration = 18;        // ~0.3 detik ("sepersekian detik")
        private const float FlashMaxAlpha = 0.4f;    // DI-CAP biar gak pernah nutup 100% opacity
        private static readonly Color FlashColor = new Color(255, 20, 20);

        private static int flashStartTick = -99999;

        // Dipanggil dari TwinsRetBeam.cs tiap kali beam-nya beneran lepas tembak.
        public static void Trigger()
        {
            flashStartTick = (int)Main.GameUpdateCount;
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (Main.gamePaused || Main.dedServ)
                return;

            int elapsed = (int)Main.GameUpdateCount - flashStartTick;
            if (elapsed < 0 || elapsed >= FlashDuration)
                return;

            float t = elapsed / (float)FlashDuration; // 0..1 sepanjang durasi flash

            // Ease-out: alpha langsung deket puncak pas awal, terus jatuh cepat ke 0 -
            // formula (1 - t^2) biar penurunannya kerasa "kedip cepat", bukan linear datar.
            float alpha = FlashMaxAlpha * (1f - t * t);

            Texture2D pixel = TextureAssets.MagicPixel.Value;
            Rectangle screenRect = new Rectangle(0, 0, Main.screenWidth, Main.screenHeight);

            spriteBatch.Draw(pixel, screenRect, FlashColor * alpha);
        }
    }
}
