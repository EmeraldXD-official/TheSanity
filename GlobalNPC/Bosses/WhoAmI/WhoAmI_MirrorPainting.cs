using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    // ============================================================================================
    // PERFECT MIRROR - PAINTING (rewrite bersih, gantiin WhoAmI_MirrorTile.cs yang LAMA - file itu
    // udah DIHAPUS, jangan dipake lagi / hapus dari project kalian).
    // ============================================================================================
    // KENAPA DITULIS ULANG DARI NOL:
    // Versi lama gambar sendiri manual (custom PreDraw + hitung origin manual dari TileFrameX/Y +
    // texture kosong/lengkap yang ganti-gantian) - itu yang jadi sumber bug (offset salah, render
    // ilang, dsb). Versi ini SEPENUHNYA pasrah ke sistem auto-framing bawaan tModLoader lewat
    // TileObjectData custom (persis kayak painting vanilla nge-render dirinya sendiri) - JADI
    // TIDAK ADA method PreDraw / GetFrameOrigin custom SAMA SEKALI di file ini.
    //
    // UPDATE: footprint diperbesar dari 3x3 -> 7x7 tile supaya art aslinya (116x116px) kepakai
    // hampir tanpa downscale (7x7 tile x 16px = 112x112, cuma beda ~3% dari 116x116 - jauh lebih
    // deket dibanding versi 3x3 lama yang kepaksa di-downscale ke 48x48). Karena bukan lagi 3x3,
    // Style3x3 preset bawaan tModLoader gak dipakai lagi - kita bikin TileObjectData custom sendiri
    // (lihat FootprintWidth/FootprintHeight/CellPadding di bawah), TAPI konsepnya identik: masih
    // 100% auto-framing, gak ada PreDraw manual.
    //
    // ART REQUIREMENT (WAJIB biar auto-framing-nya jalan bener):
    //   - Painting ini footprint 7x7 tile. Spritesheet HARUS 112x112px persis (7 kolom x 7 baris,
    //     masing2 cell 16x16px, TANPA padding/gap di antara cell - beda dari painting vanilla yang
    //     biasanya ada 2px padding, di sini sengaja 0 padding biar seluruh 112x112px kepakai penuh
    //     buat gambar, gak ada garis grout/mortar transparan yang makan ruang art).
    //   - Taruh file itu di: Content/TheSanity/GlobalNPC/Bosses/WhoAmI/Tiles/PerfectMirror.png
    //   - Item icon (PerfectMirrorItem.png, yang dipegang di tangan/inventory) BEDA file & BEBAS
    //     ukuran berapa aja (dikonfigurasi di PerfectMirrorItem.SetDefaults, WhoAmI_MirrorItems.cs).
    // ============================================================================================
    public class WhoAmIMirrorPaintingTile : ModTile
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/WhoAmI/Tiles/PerfectMirror";

        // Footprint di dunia (dalam jumlah tile) & ukuran cell spritesheet - dipakai bareng juga
        // sama FindPaintingNear/TrySummon di bawah, jadi kalau mau di-tweak lagi nanti (misal jadi
        // 6x6 atau 8x8) cukup ubah 3 angka ini + CoordinateHeights di SetStaticDefaults, gak perlu
        // ubah rumus di tempat lain.
        public const int FootprintWidth = 7;
        public const int FootprintHeight = 7;
        public const int CellSize = 16; // CoordinateWidth, TANPA padding (CoordinatePadding = 0)

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileLavaDeath[Type] = false;
            Main.tileNoAttach[Type] = true;

            // TileObjectData custom 7x7 (bukan preset Style3x3 lagi) - tetap auto-framing bawaan
            // tModLoader, cuma footprint & cell size-nya kita atur manual biar match art 116x116.
            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.Width = FootprintWidth;
            TileObjectData.newTile.Height = FootprintHeight;
            TileObjectData.newTile.CoordinateWidth = CellSize;
            TileObjectData.newTile.CoordinateHeights = new int[] { CellSize, CellSize, CellSize, CellSize, CellSize, CellSize, CellSize };
            TileObjectData.newTile.CoordinatePadding = 0; // 0 = full 112x112 kepakai buat art, gak ada garis grout
            TileObjectData.newTile.AnchorWall = true; // butuh wall di belakangnya, kayak painting vanilla
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(140, 110, 200), Terraria.Localization.Language.GetText("Perfect Mirror"));

            DustType = DustID.Silver;
        }

        // ---------------- Query helper dipakai BloodBagItem.UseItem (WhoAmI_MirrorItems.cs) ----------------

        // Cari lukisan Perfect Mirror terdekat dari titik dunia yang dikasih (biasanya
        // Main.MouseWorld), tapi cuma dianggap valid kalau juga masih dalam jangkauan `range` dari
        // posisi player.
        //
        // CATATAN ORIGIN: sekarang CoordinatePadding = 0, jadi tiap cell di spritesheet persis
        // CellSize (16) px, gak ada tambahan padding kayak Style3x3 lama - origin (sub-cell
        // top-left) dihitung dari TileFrameX/Y dibagi CellSize.
        public static Point16? FindPaintingNear(Vector2 mouseWorld, Vector2 playerCenter, float range)
        {
            if (Vector2.Distance(mouseWorld, playerCenter) > range) return null;

            int tileX = (int)(mouseWorld.X / 16f);
            int tileY = (int)(mouseWorld.Y / 16f);

            // Scan area di sekitar titik mouse (bukan cuma 1 tile persis) biar toleran terhadap
            // presisi klik player. Radius scan digedein jadi 7 (dari 2 sebelumnya) karena
            // footprint painting sekarang 7x7 - kalau player klik di ujung footprint yang jauh
            // dari origin (pojok kiri-atas), tetap harus kejangkau sampai balik ke origin-nya.
            for (int dx = -FootprintWidth; dx <= FootprintWidth; dx++)
            {
                for (int dy = -FootprintHeight; dy <= FootprintHeight; dy++)
                {
                    int cx = tileX + dx;
                    int cy = tileY + dy;

                    // FIX: Main.tile[x, y] does NOT bounds-check in tModLoader - indexing with a
                    // negative coordinate or one beyond the world size (both reachable here since
                    // the scan freely walks +-FootprintWidth/Height tiles out from the mouse
                    // position, e.g. clicking near the edge of the world) throws
                    // IndexOutOfRangeException ("Index was outside the bounds of the array").
                    // Guard against that explicitly before touching the tile array.
                    if (cx < 0 || cy < 0 || cx >= Main.maxTilesX || cy >= Main.maxTilesY) continue;

                    Tile t = Main.tile[cx, cy];
                    if (t == null || !t.HasTile || t.TileType != ModContent.TileType<WhoAmIMirrorPaintingTile>()) continue;

                    var origin = new Point16(cx - t.TileFrameX / CellSize, cy - t.TileFrameY / CellSize);
                    Vector2 originWorld = new Vector2(origin.X * 16f, origin.Y * 16f);
                    if (Vector2.Distance(originWorld, playerCenter) > range) continue;

                    return origin;
                }
            }

            return null;
        }

        // Dipanggil dari BloodBagItem.UseItem() begitu player klik lukisan Perfect Mirror yang
        // valid sambil pegang blood bag. Balikin false kalau summon gagal dimulai (misal boss udah
        // ada, atau lukisannya kebetulan udah dihancurkan barusan) supaya item-nya nggak jadi
        // dikonsumsi percuma.
        public static bool TrySummon(Point16 paintingPos, Player player)
        {
            if (NPC.AnyNPCs(ModContent.NPCType<WhoAmI>())) return false;

            Tile t = Main.tile[paintingPos.X, paintingPos.Y];
            if (t == null || !t.HasTile || t.TileType != ModContent.TileType<WhoAmIMirrorPaintingTile>())
                return false;

            // Titik spawn: tepat di depan bagian bawah-tengah lukisan, tapi bisa ditarik
            // sedikit ke atas kalau boss terlalu rendah.
            const float BossSpawnVerticalOffset = -32f; // negatif = lebih atas, positif = lebih bawah
            Vector2 spawnPos = new Vector2(
                paintingPos.X * 16f + (FootprintWidth * 16f) / 2f,
                paintingPos.Y * 16f + (FootprintHeight * 16f) - 4f + BossSpawnVerticalOffset);
            int facing = player.Center.X < spawnPos.X ? -1 : 1;

            WhoAmI.PendingMirrorSpawnPoint = spawnPos;
            WhoAmI.PendingMirrorFacingDirection = facing;

            // CATATAN MULTIPLAYER: aman buat singleplayer & host-and-play (host yang klik blood
            // bag-nya). Kalau dimainkan di DEDICATED SERVER dan yang klik klien remote (bukan
            // host), NPC.NewNPC di sini cuma jalan lokal di klien itu dan TIDAK sinkron ke
            // server/pemain lain - butuh 1 mod packet tambahan (klien kirim posisi lukisan ke
            // server, server yang panggil NPC.NewNPC & broadcast). Belum diimplementasikan di sini
            // karena butuh referensi ke class Mod utama proyek ini yang belum diketahui.
            // Mirror "shattering" VFX/SFX pas boss keluar - dulu summon-nya senyap total (gak ada
            // feedback sama sekali selain boss nongol). Ungu-magenta, nyamain tema warna aura boss
            // (lihat GetAuraColor di WhoAmI_VFX.cs) biar kerasa nyambung sama identitas visualnya
            // dari detik pertama dia muncul, bukan cuma pas nyerang doang.
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Shatter, spawnPos);
            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item29, spawnPos);
            for (int i = 0; i < 30; i++)
            {
                Vector2 shardVel = Main.rand.NextVector2Circular(7f, 7f) - new Vector2(0f, 2f);
                Dust d = Dust.NewDustPerfect(spawnPos, DustID.SilverCoin, shardVel, 0, new Color(190, 150, 240), 1.6f);
                d.noGravity = Main.rand.NextBool();
            }

            int npcIndex = NPC.NewNPC(player.GetSource_FromThis(), (int)spawnPos.X, (int)spawnPos.Y, ModContent.NPCType<WhoAmI>());
            if (Main.netMode == NetmodeID.Server && npcIndex >= 0 && npcIndex < Main.maxNPCs)
                NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);

            return true;
        }
    }
}