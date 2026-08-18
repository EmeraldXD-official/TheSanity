using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPCs
{
    // ==========================================
    // TwinsDisplayNameGlobalNPC — ganti nama yang ditampilkan (boss health bar,
    // chat kill message, bestiary card, dll) buat Retinazer & Spazmatism JADI
    // "Twinlamitas Clone" buat KEDUANYA (satu nama yang sama buat dua-duanya).
    //
    // Dipisah jadi file sendiri (bukan ditumpuk di TwinsArenaGlobalNPC) biar gampang
    // dicari/diubah kalau nanti mau di-tweak lagi, dan biar tanggung jawabnya jelas:
    // file ini CUMA ngurus nama tampilan, gak nyentuh logic lain sama sekali.
    //
    // ModifyTypeName dipanggil vanilla buat resolve string nama NPC yang dipakai di
    // banyak tempat (health bar boss, bestiary, chat), jadi cukup override di sini
    // aja - gak perlu sentuh SetStaticDefaults / DisplayName.SetDefault manapun.
    // ==========================================
    public class TwinsDisplayNameGlobalNPC : global::Terraria.ModLoader.GlobalNPC
    {
        // Nama pengganti buat kedua Twin (Retinazer & Spazmatism sama-sama pakai ini).
        private const string OverrideDisplayName = "Twinlamitas Clone";

        public override void ModifyTypeName(NPC npc, ref string typeName)
        {
            if (npc.type == NPCID.Retinazer || npc.type == NPCID.Spazmatism)
            {
                typeName = OverrideDisplayName;
            }
        }
    }
}
