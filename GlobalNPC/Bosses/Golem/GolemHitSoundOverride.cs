using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;

namespace TheSanity.GlobalNPCs
{
    public class GolemHitSoundOverride : global::Terraria.ModLoader.GlobalNPC
    {
        // Karena kita mau memodifikasi semua Golem (Badan, Tangan, Kepala), kita pakai true
        public override bool InstancePerEntity => true;

        public override void SetDefaults(NPC npc)
        {
            // Deteksi apakah NPC yang sedang di-load adalah salah satu dari bagian tubuh Golem
            if (npc.type == NPCID.Golem ||          // Badan Golem (Phase 1) / Golem Gembong
                npc.type == NPCID.GolemHead ||      // Kepala Golem (Saat masih menempel di badan)
                npc.type == NPCID.GolemHeadFree ||  // Kepala Golem Terbang (Phase 2)
                npc.type == NPCID.GolemFistLeft ||  // Tangan Kiri Original Golem
                npc.type == NPCID.GolemFistRight)   // Tangan Kanan Original Golem
            {
                // [PANDUAN BALANCING & AUDIO REWORK]
                // Mengubah HitSound bawaan vanilla Golem (yang aslinya suara daging/stone biasa)
                // Menjadi SoundID.Item69 agar terdengar jauh lebih kokoh, berat, dan seperti batu solid!
                npc.HitSound = SoundID.NPCHit41;
            }
        }
    }
}