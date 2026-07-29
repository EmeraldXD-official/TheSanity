using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    public class EmeraldSpellEnraged : ModBuff
    {
        // 🎨 Meminjam gambar EmeraldSpell.png secara langsung agar kamu gak perlu copas gambar lagi
        public override string Texture => "TheSanity/Buff/EmeraldSpell";

        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex) {
            // 💔 LOGIKA PEMBANTAI HP (-400 HP / detik)
            if (player.lifeRegen > 0) {
                player.lifeRegen = 0;
            }
            player.lifeRegenTime = 0;
            player.lifeRegen -= 800; // 800 poin di tModLoader = -400 HP per detik nyata

            // 🚨 Efek Visual Stage Maut (Partikel lebih rapat & cepat)
            if (Main.rand.NextBool(3)) {
                int dustIndex = Dust.NewDust(player.position, player.width, player.height, DustID.GemEmerald, 0f, 0f, 100, default, 1.5f);
                Main.dust[dustIndex].noGravity = true;
                Main.dust[dustIndex].velocity *= 0.8f;
            }

            // Membuat aura cahaya berkedip warna merah-hijau tanda bahaya ekstrem
            float r = Main.rand.NextBool(2) ? 1.2f : 0f;
            Lighting.AddLight(player.Center, r, 1.3f, 0f);
        }
    }
}