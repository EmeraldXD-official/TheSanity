using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Buff
{
    public class EmeraldSpell : ModBuff
    {
        public override void SetStaticDefaults() {
            Main.debuff[Type] = true;
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false; // Detik tetap tampil biar player panik
        }

        public override void Update(Player player, ref int buffIndex) {
            // 🔒 PROTEKSI MUTLAK: Jika player sedang dalam mode Enraged (Debuff ke-2), 
            // tendang debuff penumpuk ini secara instan agar tidak bisa nempel!
            if (player.HasBuff(ModContent.BuffType<EmeraldSpellEnraged>())) {
                player.DelBuff(buffIndex);
                buffIndex--;
                return;
            }

            // 🚨 KONDISI REPLACE: Jika tumpukan waktu menyentuh/melewati 50 detik (50 * 60 frame = 3000)
            if (player.buffTime[buffIndex] >= 3000) {
                player.DelBuff(buffIndex); // Hapus debuff penumpuk waktu ini
                buffIndex--;

                // Langsung timpa dan panggil Debuff Stage Maut (Debuff ke-2) selama 5 detik (300 frame)
                player.AddBuff(ModContent.BuffType<EmeraldSpellEnraged>(), 300);
                return;
            }

            // ✨ Efek Visual Partikel Emerald Standar
            if (Main.rand.NextBool(4)) {
                int dustIndex = Dust.NewDust(player.position, player.width, player.height, DustID.GemEmerald, 0f, 0f, 100, default, 1.3f);
                Main.dust[dustIndex].noGravity = true;
                Main.dust[dustIndex].velocity *= 0.5f;
            }
        }
    }
}