using Terraria;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.Skeletron;

namespace TheSanity.Items.Accessories
{
    // Partner ModPlayer buat BoneGloveGlobalItem — lihat catatan di file itu
    // soal kenapa efeknya dipisah ke sini (GlobalItem gak punya hook
    // "on hit" langsung, cuma bisa titip flag lewat UpdateAccessory tiap
    // frame, ModPlayer ini yang beneran baca flag itu & jalanin efeknya).
    //
    // Dipasang di 2 hook biar proc-nya jalan APAPUN jenis senjata yang lagi
    // dipakai player, bukan cuma melee:
    //   - OnHitNPCWithItem -> senjata non-projectile (pedang manual-swing, dll)
    //   - OnHitNPCWithProj -> proyektil (ranged/magic/rogue/minion, termasuk
    //     senjata melee yang nembak projectile kayak boomerang/yoyo)
    //
    // CATATAN: nama hook ini (OnHitNPCWithItem/OnHitNPCWithProj) sama kayak
    // catatan version-dependent lain di file-file boss ini — cek dokumentasi
    // ModPlayer terkini kalau ternyata signature-nya beda pas compile.
    public class BoneGloveModPlayer : ModPlayer
    {
        public bool HasBoneGlove;

        // === Tuning ===
        const float ProcChance = 0.08f;     // ~8%, sesuai request user ("kecil, 7-10%")
        const int ProcCooldown = 45;        // ~0.75 detik, biar gak numpuk portal pas attack speed tinggi
        const float PortalScaleMul = 0.55f; // portal & BigBoneSpike versi accessory jauh lebih kecil dari boss

        int cooldownTimer;

        public override void ResetEffects()
        {
            // FIX: reset tiap frame SEBELUM UpdateAccessory jalan — kalau
            // player lepas accessory-nya, flag ini otomatis balik false
            // (gak ke-set ulang jadi true di frame itu), jadi proc otomatis
            // berhenti tanpa perlu logic detect "unequip" manual.
            HasBoneGlove = false;
        }

        public override void PostUpdate()
        {
            if (cooldownTimer > 0)
                cooldownTimer--;
        }

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
        {
            TryProc(target);
        }

        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            // FIX: skip kalau yang barusan ngehit itu JUSTRU BigBoneSpike
            // hasil proc accessory ini sendiri — biar spike yang lagi nusuk
            // target (fase Holding, ngedamage beberapa tick) gak ikut
            // ngetrigger proc baru tiap kali dia ngasih damage, yang bisa
            // bikin portal numpuk beranak-pinak gak kekontrol.
            if (proj.type == ModContent.ProjectileType<BigBoneSpike>())
                return;

            TryProc(target);
        }

        void TryProc(NPC target)
        {
            if (!HasBoneGlove) return;
            if (cooldownTimer > 0) return;
            if (!target.active || target.friendly) return; // jaga2 gak proc ke NPC kawan/town NPC
            if (Main.rand.NextFloat() > ProcChance) return;

            cooldownTimer = ProcCooldown;

            // CATATAN: GetSource_OnHit / nama method IEntitySource yang
            // paling "tepat" secara semantik bisa beda-beda antar versi
            // tModLoader — pakai GetSource_Misc sebagai fallback yang aman
            // & selalu ada, cek dokumentasi Player.GetSource_* terkini
            // kalau mau yang lebih spesifik pas compile.
            // FIX: dulu ngirim target.Center langsung -> portal selalu
            // nongol persis di titik itu. Sekarang ngirim target NPC-nya
            // sendiri, biar BonePortal.SpawnOnHit yang nentuin posisi
            // random di sisi atas/bawah/kiri/kanan badan target (lihat
            // catatan GetRandomOffsetPosition di BonePortal.cs).
            BonePortal.SpawnOnHit(Player.GetSource_Misc("BoneGlove"), target, Player, PortalScaleMul);
        }
    }
}