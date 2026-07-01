using Terraria;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    public class DistruptedTime : ModBuff
    {
        // --- FIX UTAMA: Menggunakan properti override read-only untuk menentukan path gambar ikon debuff ---
        public override string Texture => "TheSanity/Buff/DistruptedTime";

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;      // Debuff hilang kalau player keluar game
            Main.buffNoTimeDisplay[Type] = false; // Durasi debuff terlihat di layar
            Main.debuff[Type] = true;          // Ditetapkan sebagai debuff (tidak bisa di-klik kanan untuk hapus)
        }

        public override void Update(Player player, ref int buffIndex)
        {
            // Menghubungkan debuff ini ke sistem ModPlayer kustom kita
            var disruptedPlayer = player.GetModPlayer<DisruptedPlayer>();
            disruptedPlayer.HasDisruptedTime = true;

            // --- PANDUAN TIMEOUT: LOGIKA HITUNG MUNDUR 1 DETIK (60 FRAME) ---
            disruptedPlayer.TeleportTimer--;

            if (disruptedPlayer.TeleportTimer <= 0)
            {
                // Panggil fungsi teleportasi acak yang aman dari DisruptedPlayer.cs
                disruptedPlayer.TeleportPlayerRandomly();
                
                // Reset timer kembali ke 1 detik (60 frame = 1 detik)
                disruptedPlayer.TeleportTimer = 60;
            }
        }
    }
}