using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.TorchGods;
using TheSanity.Systems;

namespace TheSanity.GlobalNPCs
{
    /// <summary>
    /// Nempelin arena border KOTAK ke fight TrueTorchGod, polanya sama persis
    /// kayak TwinsArenaGlobalNPC (reuse ArenaBorderSystem + ArenaBorderColliderNPC),
    /// bedanya:
    ///  - Border-nya KOTAK (ArenaBorderColliderNPC.ColliderShape.Square), bukan lingkaran.
    ///  - Visualnya pakai spritesheet "TorchBorder.png" sendiri (frame animasi +
    ///    crossfade), bukan AuraOut.png yang di-tint merah/hijau.
    ///  - Begitu TorchGod spawn: posisinya di-PAKSA pas di titik center player
    ///    yang men-spawn-nya (SEKALI, saat itu doang - abis itu TorchGod bebas
    ///    gerak sesuai AI vanilla-nya), dan player itu langsung dilempar
    ///    (di-knockback) ke arah acak.
    /// </summary>
    public class TorchGodArenaGlobalNPC : global::Terraria.ModLoader.GlobalNPC
    {
        // Border aktif untuk fight TorchGod saat ini (null kalau tidak sedang fight).
        private static ArenaBorderSystem.Border torchGodBorder = null;

        // Ukuran border KOTAK: 100 BLOCK x 100 BLOCK (1 block = 16px, jadi
        // 100 block = 1600px sisi) -> setengah sisi = 800.
        // (Kemarin salah pakai satuan pixel langsung, jadinya cuma ~6 block.)
        private const float ArenaHalfSize = 100f * 16f / 2f; // = 800

        // Berapa tick animasi "timbul" pas border baru pertama kali muncul
        // (dari 0 membesar ke ArenaHalfSize) - dan animasi "mengecil + fade"
        // pas mau hilang udah otomatis ditangani ArenaBorderSystem (ShrinkDurationTicks).
        private const int ArenaAppearDurationTicks = 20; // ~0.33 detik

        // Seberapa kencang player "dilempar" pas TorchGod spawn.
        private const float ThrowSpeed = 12f;

        // Player yang keluar (halfSize - buffer) bakal langsung ditarik masuk lagi.
        // Dinaikin dikit dari sebelumnya karena arena-nya sekarang jauh lebih
        // besar (12px kerasa gak ada apa-apanya di arena 1600px).
        private const float PullBuffer = 48f;

        // Radius dianggap "praktis 0" buat keperluan nunggu collider ikut kelar
        // animasi mengecil sebelum beneran dihapus.
        private const float ColliderRemovalRadiusThreshold = 4f;

        // === Kustomisasi visual border TorchBorder.png ===
        // Path relatif ke sprite: TheSanity/GlobalNPC/Bosses/TorchGods/TorchBorder
        private const string BorderTexturePath = "TheSanity/GlobalNPC/Bosses/TorchGods/TorchBorder";

        // ASUMSI: TorchBorder.png = 1600x400, disusun horizontal 1 baris, dan
        // tiap frame PERSEGI (400x400) -> 1600 / 400 = 4 frame.
        // Kalau ternyata jumlah frame aslinya beda, GANTI CUMA ANGKA INI.
        private const int BorderFrameCount = 4;

        // Tiap frame ditahan berapa tick, dan berapa tick terakhirnya dipakai
        // buat crossfade ke frame berikutnya (fade cepat tapi kelihatan).
        private const int BorderTicksPerFrame = 8;
        private const int BorderCrossfadeTicks = 4;

        private static bool IsTorchGod(NPC npc) => npc.type == ModContent.NPCType<TrueTorchGod>();

        // Cari player "pemilik" spawn ini: prioritas ke npc.target kalau valid,
        // fallback ke player terdekat dari posisi NPC saat spawn.
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

        // Versi KOTAK dari "narik player yang keluar arena" - clamp posisi
        // player per-axis (X dan Y independen) ke dalam kotak, bukan ke lingkaran.
        private static void PullPlayersInsideSquare(Vector2 center, float halfSize)
        {
            float maxDist = MathHelper.Max(0f, halfSize - PullBuffer);

            foreach (Player p in Main.player)
            {
                if (!p.active || p.dead)
                    continue;

                Vector2 offset = p.Center - center;
                float clampedX = MathHelper.Clamp(offset.X, -maxDist, maxDist);
                float clampedY = MathHelper.Clamp(offset.Y, -maxDist, maxDist);

                if (clampedX == offset.X && clampedY == offset.Y)
                    continue; // udah di dalam, gak perlu ditarik

                p.position = (center + new Vector2(clampedX, clampedY)) - p.Size * 0.5f;

                if (clampedX != offset.X)
                    p.velocity.X = 0f;
                if (clampedY != offset.Y)
                    p.velocity.Y = 0f;
            }
        }

        public override void OnSpawn(NPC npc, IEntitySource source)
        {
            if (!IsTorchGod(npc))
                return;

            // Guard terhadap referensi stale (sama kayak TwinsArenaGlobalNPC) -
            // kalau world sempat di-unload selagi border ini masih aktif.
            if (torchGodBorder != null && !ArenaBorderSystem.ActiveBorders.Contains(torchGodBorder))
                torchGodBorder = null;

            if (torchGodBorder != null)
                return;

            Player anchor = GetAnchorPlayer(npc);
            Vector2 center = anchor != null ? anchor.Center : npc.Center;

            // TorchGod dipaksa tepat di center player SAAT SPAWN INI DOANG -
            // setelah ini dia bebas gerak sesuai AI vanilla-nya sendiri.
            npc.Center = center;
            npc.netUpdate = true;

            // Lempar player yang men-spawn ke arah acak.
            if (anchor != null)
            {
                float randomAngle = Main.rand.NextFloat(MathHelper.TwoPi);
                Vector2 throwDir = randomAngle.ToRotationVector2();

                anchor.velocity = throwDir * ThrowSpeed;
                // Catatan: Player gak punya field netUpdate kayak NPC - posisi/velocity
                // player disinkronisasi otomatis lewat sistem sync bawaan tiap tick,
                // jadi gak perlu di-trigger manual di sini.
            }

            // Border kotak, "timbul" dari 0 ke ArenaHalfSize, pakai spritesheet
            // TorchBorder dengan animasi frame + crossfade (bukan tint merah/hijau).
            ArenaBorderSystem.Border border = new ArenaBorderSystem.Border
            {
                Center = center,
                Radius = 0f,
                RemovalCondition = () => !npc.active,
                TexturePath = BorderTexturePath,
                FrameCount = BorderFrameCount,
                TicksPerFrame = BorderTicksPerFrame,
                CrossfadeTicks = BorderCrossfadeTicks,
                UseColorPulse = false,
                Opacity = 0.85f,
            };
            border.AnimateRadiusTo(ArenaHalfSize, ArenaAppearDurationTicks);

            torchGodBorder = border;
            ArenaBorderSystem.ActiveBorders.Add(border);

            // NPC "hantu" collider (Impact Library) buat bikin border kotak solid.
            int colliderIndex = NPC.NewNPC(npc.GetSource_FromThis(), (int)center.X, (int)center.Y, ModContent.NPCType<ArenaBorderColliderNPC>());
            ArenaBorderColliderNPC colliderNpc = Main.npc[colliderIndex].ModNPC as ArenaBorderColliderNPC;

            colliderNpc?.UpdatePosition(center, border.Radius, ArenaBorderColliderNPC.ColliderShape.Square);

            if (colliderNpc != null)
            {
                colliderNpc.RemovalCondition = () => border.IsRemoving && border.Radius <= ColliderRemovalRadiusThreshold;
            }

            border.Tick = (b) =>
            {
                colliderNpc?.UpdatePosition(b.Center, b.Radius, ArenaBorderColliderNPC.ColliderShape.Square);

                if (!b.IsRemoving)
                    PullPlayersInsideSquare(b.Center, b.Radius);
            };

            border.OnFullyRemoved = () =>
            {
                if (torchGodBorder == border)
                    torchGodBorder = null;
            };
        }
    }
}
