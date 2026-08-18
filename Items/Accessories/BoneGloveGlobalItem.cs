using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Accessories
{
    // === Rework Bone Glove (accessory drop Skeletron vanilla) ===
    // Nambahin efek: tiap kali nyerang musuh, ada chance kecil munculin
    // BonePortal mini (versi FRIENDLY & di-scale-down dari BonePortal +
    // BigBoneSpike punya boss Skeletron sendiri) yang nusuk musuh itu dari
    // tanah — "ngeplak" pattern 1 boss-nya dalam skala accessory.
    //
    // Ditarget langsung ke ItemID.BoneGlove VANILLA (bukan ModItem baru) —
    // pola sama kayak SkeletronOverride (GlobalNPC) yang nempel ke
    // NPCID.SkeletonHead vanilla di file SkelyDollGif.cs.
    //
    // GlobalItem ini SENGAJA cuma nyalain flag ("HasBoneGlove") tiap frame
    // player pakai item ini — efek beneran (roll chance, spawn portal, dll)
    // dijalanin di BoneGloveModPlayer.OnHitNPCWithItem/OnHitNPCWithProj.
    // Ini pola standar tModLoader buat rework accessory: GlobalItem gak
    // dapet notifikasi langsung pas player berhasil hit, jadi harus numpang
    // ke ModPlayer yang emang punya hook itu.
    public class BoneGloveGlobalItem : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
            => entity.type == ItemID.BoneGlove;

        // Dipanggil vanilla tiap frame SELAMA accessory ini ke-equip (slot
        // accessory ATAU dipakai lewat Loadout/vanity dgn accessory-nya
        // tetep di-toggle aktif). hideVisual sengaja diabaikan — efek combat
        // begini tetep harus jalan meski player nyembunyiin slot-nya secara
        // visual (vanity-only hide, bukan disable beneran).
        public override void UpdateAccessory(Item item, Player player, bool hideVisual)
        {
            player.GetModPlayer<BoneGloveModPlayer>().HasBoneGlove = true;
        }

        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips)
        {
            tooltips.Add(new TooltipLine(Mod, "BoneGloveProc",
                "Serangan memiliki kesempatan kecil memanggil tulang tajam dari tanah di bawah musuh"));
        }
    }
}
