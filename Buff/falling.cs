using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    public class falling : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.debuff[Type] = true;          // Menandakan ini adalah debuff (bingkai merah otomatis)
            Main.pvpBuff[Type] = true;         // Efek bekerja dalam mode PvP
            Main.buffNoSave[Type] = true;      // Otomatis hilang saat keluar-masuk world
            Main.buffNoTimeDisplay[Type] = true;
            // -------------------------------------------------------------------------
            // [DEBUFF PROTECTION BALANCING]: 
            // Player tidak bisa menghilangkan debuff ini dengan klik kanan mouse
            // -------------------------------------------------------------------------
            BuffID.Sets.LongerExpertDebuff[Type] = true; // Durasi bertambah lama di Expert/Master Mode
        }

        // =========================================================================
        // [DEBUFF EFFECT LOCATION]: DISABLE HOOK, MOUNT & FORCE DROP THROUGH PLATFORMS
        // =========================================================================
        public override void Update(Player player, ref int buffIndex)
        {
            // 1. PAKSA TEMBUS PLATFORM
            // Mengakali input game seolah-olah player selalu menekan tombol BAWAH.
            // Ini membuat player otomatis jatuh menembus kayu/platform apa pun.
            player.controlDown = true;

            // 2. MATIKAN & CANCEL SEMUA GRAPPLING HOOK
            // Memutus tali hook yang sedang menempel dan menggagalkan peluncuran hook baru.
            player.RemoveAllGrapplingHooks();

            // =========================================================================
            // [MOUNT DISABLE LOCATION]: PAKSA TURUN DARI MOUNT & BLOKIR PENGGUNAANNYA
            // =========================================================================
            // Jika status mount player sedang aktif (baik Minecart, UFO, maupun hewan tunggangan),
            // jalankan fungsi Dismount untuk memaksa player turun secara instan.
            if (player.mount.Active)
            {
                player.mount.Dismount(player);
            }

            // --- BONUS EFEK VISUAL PARTIKEL (ESTETIKA JATUH) ---
            // Munculkan hembusan angin putih di sekeliling player agar efek jatuh bebasnya terasa deras
            if (Main.rand.NextBool(3))
            {
                Dust d = Dust.NewDustDirect(player.position, player.width, player.height, DustID.Cloud, 0f, -2f, 100, default, 0.8f);
                d.noGravity = true;
                d.velocity.X *= 0.1f; // Biar partikelnya tetap mengumpul di badan player
            }
        }
    }
}