using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Systems;

namespace TheSanity.GlobalNPCs
{
    /// <summary>
    /// Menempelkan ArenaBorderSystem.Border (versi LINGKARAN, full solo mod) ke
    /// fight boss vanilla The Twins (Retinazer & Spazmatism).
    ///
    /// BEDA dari versi kotak lama:
    /// - Border sekarang LINGKARAN, digambar pakai tekstur AuraOut.png yang
    ///   warnanya dianimasikan merah <-> hijau berbasis waktu (lihat
    ///   ArenaBorderSystem.GetAnimatedColor) - BUKAN berdasarkan health Twins.
    /// - Border dibuat SEKALI saat salah satu dari mereka spawn, dipusatkan di
    ///   posisi PLAYER saat itu, dan TIDAK PERNAH dipindah lagi setelahnya -
    ///   sama kayak versi kotak paling awal, cuma sekarang bentuknya lingkaran.
    ///   Twins boleh muter-muter di dalam/luar area, border-nya tetap diem.
    /// - Kalau ada player yang ketemu di LUAR lingkaran (misal ketiban
    ///   knockback), player itu langsung ditarik/dipaksa masuk lagi ke dalam
    ///   radius tiap tick.
    /// - Collision solid-nya (biar gak bisa ditembus + bisa nempel/grapple)
    ///   tetap dari Impact Library (CollisionLib), lewat ArenaBorderColliderNPC,
    ///   cuma sekarang bentuknya polygon banyak sisi biar mirip lingkaran.
    /// </summary>
    public class TwinsArenaGlobalNPC : global::Terraria.ModLoader.GlobalNPC
    {
        // Border aktif untuk fight Twins saat ini (null kalau tidak sedang fight).
        private static ArenaBorderSystem.Border arenaBorder = null;

        private const float ArenaRadius = 700f;

        // Player yang keluar radius ini (radius - buffer) bakal langsung ditarik masuk.
        private const float PullBuffer = 24f;

        // Berapa tick animasi "timbul" pas arena baru pertama kali muncul (dari radius 0
        // membesar ke ArenaRadius) - BUKAN instan langsung muncul penuh lagi.
        private const int ArenaAppearDurationTicks = 30; // ~0.5 detik

        // Radius dianggap "praktis 0" buat keperluan nunggu collider ikut kelar animasi
        // mengecil sebelum beneran dihapus (disamakan sama ArenaBorderSystem.RemovalRadiusThreshold).
        private const float ColliderRemovalRadiusThreshold = 4f;

        private static bool IsTwin(NPC npc) => npc.type == NPCID.Retinazer || npc.type == NPCID.Spazmatism;

        private static bool AnyTwinAlive()
        {
            foreach (NPC n in Main.npc)
            {
                if (n.active && IsTwin(n))
                    return true;
            }
            return false;
        }

        // Cari player terdekat dari npc. Dipakai buat nentuin titik pusat awal
        // arena (sebelum border mulai ngikutin posisi Twins tiap tick).
        private static Player GetAnchorPlayer(NPC npc)
        {
            if (npc.target >= 0 && npc.target < Main.maxPlayers)
            {
                Player p = Main.player[npc.target];
                if (p != null && p.active)
                    return p;
            }

            float closestDist = float.MaxValue;
            Player closest = null;
            foreach (Player p in Main.player)
            {
                if (!p.active)
                    continue;

                float dist = Vector2.DistanceSquared(p.Center, npc.Center);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = p;
                }
            }

            return closest;
        }

        // Cek semua player, kalau ada yang di luar (radius - buffer), paksa
        // masuk lagi: posisi di-snap ke titik terdekat di dalam lingkaran, dan
        // komponen velocity yang ngarah keluar di-nol-in biar gak langsung
        // ke-dorong keluar lagi tick berikutnya.
        //
        // Catatan: ini logic singleplayer-simple (posisi di-set langsung).
        // Kalau mod ini dipakai di multiplayer, idealnya reposisi kayak gini
        // cuma dilakuin di server terus di-sync, biar gak desync sama client lain.
        private static void PullPlayersInside(Vector2 center, float radius)
        {
            float maxDist = radius - PullBuffer;

            foreach (Player p in Main.player)
            {
                if (!p.active || p.dead)
                    continue;

                Vector2 offset = p.Center - center;
                float dist = offset.Length();

                if (dist <= maxDist)
                    continue;

                Vector2 dir = dist > 0.0001f ? offset / dist : Vector2.UnitY;
                Vector2 targetCenter = center + dir * maxDist;

                p.position = targetCenter - p.Size * 0.5f;

                float outwardSpeed = Vector2.Dot(p.velocity, dir);
                if (outwardSpeed > 0f)
                    p.velocity -= dir * outwardSpeed;
            }
        }

        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            if (!IsTwin(npc))
                return;

            // Guard terhadap referensi arenaBorder yang STALE: kalau world sempat di-unload
            // (exit ke main menu, dsb) SELAGI border ini masih aktif, ArenaBorderSystem.OnWorldUnload
            // ngosongin ActiveBorders duluan tanpa sempet manggil OnFullyRemoved border ini (yang
            // harusnya nge-reset arenaBorder balik ke null) - alhasil arenaBorder nyangkut non-null
            // SELAMANYA dan border baru gak akan pernah ke-spawn lagi pas player refight Twins,
            // walau udah rejoin world. Fix: kalau arenaBorder ternyata udah gak ada lagi di
            // ActiveBorders (stale), anggap aja kayak null dan lanjut bikin border baru.
            if (arenaBorder != null && !ArenaBorderSystem.ActiveBorders.Contains(arenaBorder))
                arenaBorder = null;

            if (arenaBorder != null)
                return;

            Player anchor = GetAnchorPlayer(npc);
            // Pusat arena diambil dari posisi PLAYER saat Twins muncul,
            // bukan posisi Twins itu sendiri, dan nilai ini di-set SEKALI di sini
            // saja - border ini TIDAK ngikutin Twins.
            Vector2 center = anchor != null ? anchor.Center : npc.Center;

            // Border MULAI DARI RADIUS 0 dan animasi MEMBESAR ke ArenaRadius ("timbul") -
            // bukan langsung muncul penuh instan lagi (lihat AnimateRadiusTo di bawah).
            ArenaBorderSystem.Border border = new ArenaBorderSystem.Border
            {
                Center = center,
                Radius = 0f,
                RemovalCondition = () => !AnyTwinAlive()
            };
            border.AnimateRadiusTo(ArenaRadius, ArenaAppearDurationTicks);

            arenaBorder = border;
            ArenaBorderSystem.ActiveBorders.Add(border);

            // NPC "hantu" yang nampung CollisionSurface (Impact Library) buat
            // bikin border lingkaran beneran solid (player berdiri/nempel, hook nempel).
            int colliderIndex = NPC.NewNPC(npc.GetSource_FromThis(), (int)center.X, (int)center.Y, ModContent.NPCType<ArenaBorderColliderNPC>());
            ArenaBorderColliderNPC colliderNpc = Main.npc[colliderIndex].ModNPC as ArenaBorderColliderNPC;

            // ==========================================
            // REQUEST: NPC "arena" ini (collider ghost doang, bukan musuh beneran) harus IMUN
            // ke SEMUA debuff - TERUTAMA Confuse (kalau ke-confuse, collision/pathing internal
            // vanilla-nya bisa keganggu meski dia gak gerak sendiri, dan gak masuk akal secara
            // gameplay musuh "invisible wall" ini kena debuff apapun). Loop di seluruh
            // buffImmune[] (bukan cuma daftar debuff yang keliatan jelas) biar future-proof
            // kalau ada debuff baru ditambahin mod lain nanti - collider ini tetap kebal.
            // ==========================================
            for (int i = 0; i < Main.npc[colliderIndex].buffImmune.Length; i++)
            {
                Main.npc[colliderIndex].buffImmune[i] = true;
            }

            colliderNpc?.UpdatePosition(center, border.Radius);

            if (colliderNpc != null)
            {
                // Collider baru boleh dianggap "selesai" (dan boleh dihapus) begitu
                // border-nya SENDIRI udah masuk proses hapus (IsRemoving) DAN radius-nya
                // udah nyaris 0 - biar hitbox-nya ilang BARENGAN sama visualnya yang lagi
                // mengecil, bukan ngilang duluan/instan sebelum animasi shrink-nya kelar.
                colliderNpc.RemovalCondition = () => border.IsRemoving && border.Radius <= ColliderRemovalRadiusThreshold;
            }

            // Border butuh Tick tiap frame buat 2 hal sekarang:
            //  1. Nge-sync ulang hitbox collider ke Radius TERKINI (yang lagi dianimasikan -
            //     membesar pas muncul, membesar 40% pas Phase 2, mengecil pas mau hilang),
            //     BUKAN cuma sekali di-set pas spawn kayak sebelumnya - jadi pijakan/hitbox-nya
            //     gak pernah "ketinggalan" stuck di ukuran/posisi lama.
            //  2. Narik balik player mana pun yang ketemu di luar radius (misal habis kena
            //     knockback keras dari Twins) - DIHENTIKAN begitu border lagi proses mau
            //     hilang (IsRemoving), biar player gak "ke-tarik ke tengah" pas arena-nya
            //     memang lagi sengaja dibuang abis fight kelar.
            border.Tick = (b) =>
            {
                colliderNpc?.UpdatePosition(b.Center, b.Radius);

                if (!b.IsRemoving)
                    PullPlayersInside(b.Center, b.Radius);
            };

            // Beresin referensi statis begitu border ini BENERAN kelar dihapus dari
            // ActiveBorders (animasi mengecilnya sudah selesai) - biar OnSpawn boss
            // berikutnya bisa bikin border baru lagi.
            border.OnFullyRemoved = () =>
            {
                if (arenaBorder == border)
                    arenaBorder = null;
            };
        }
    }
}
