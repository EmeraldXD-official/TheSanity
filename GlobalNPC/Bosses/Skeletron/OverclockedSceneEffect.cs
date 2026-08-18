using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Skeletron
{
    // === BGM custom Skeletron: Overclocked ===
    // GlobalNPC gak punya hook "UpdateMusic" di tModLoader versi ini (itu API
    // lama yang udah dibuang, makanya kemarin CS0115 "no suitable method
    // found to override"). Cara yang BENER buat override musik boss sekarang
    // adalah bikin kelas terpisah turunan ModSceneEffect kayak di bawah ini.
    // ModSceneEffect otomatis ke-load sendiri sama tModLoader (gak perlu
    // didaftarin manual), tinggal aktif begitu IsSceneEffectActive() true.
    public class OverclockedSceneEffect : ModSceneEffect
    {
        // Slot musik di-cache statis & di-load LAZY (baru query pas pertama
        // kali dibutuhin) — MusicLoader.GetMusicSlot baru aman dipanggil
        // sesudah semua musik mod ini selesai di-load.
        //
        // File asetnya HARUS ada di:
        //   TheSanity/Sounds/Music/Overclocked.mp3
        static int? musicSlot;

        public override int Music => musicSlot ??= MusicLoader.GetMusicSlot(Mod, "Music/Overclocked");

        // BossHigh biar ngalahin musik biome/environment normal selama
        // boss ini aktif di layar.
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        // Aktif selama SkeletronHead ada & alive di dunia. Cukup cek 1 NPC
        // type ini aja karena seluruh fight rework di-detour lewat head
        // (lihat AppliesToEntity di SkeletronReworkGlobalNPC — tangan gak
        // pernah dispawn terpisah).
        public override bool IsSceneEffectActive(Player player) => NPC.AnyNPCs(NPCID.SkeletronHead);
    }
}
