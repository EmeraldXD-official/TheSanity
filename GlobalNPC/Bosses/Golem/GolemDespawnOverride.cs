using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPCs
{
    public class GolemDespawnOverride : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private Vector2 altarPosition = Vector2.Zero;
        private bool trackedAltar = false;

        public override bool PreAI(NPC npc)
        {
            // Logika anti-cheese ini berlaku untuk Golem Utama (Badan) dan Kepala Terbangnya
            if (npc.type == NPCID.Golem || npc.type == NPCID.GolemHeadFree)
            {
                // 1. Ambil koordinat fisik ubin Altar Golem asli di dalam arena
                if (!trackedAltar)
                {
                    trackedAltar = true;
                    
                    Point npcTilePos = npc.Center.ToTileCoordinates();
                    bool altarFound = false;

                    // Menyisir radius area untuk mengunci koordinat absolut Lihzahrd Altar
                    for (int x = -150; x <= 150; x++)
                    {
                        for (int y = -150; y <= 150; y++)
                        {
                            int targetX = npcTilePos.X + x;
                            int targetY = npcTilePos.Y + y;

                            if (WorldGen.InWorld(targetX, targetY))
                            {
                                Tile tile = Main.tile[targetX, targetY];
                                if (tile.HasTile && tile.TileType == TileID.LihzahrdAltar)
                                {
                                    altarPosition = new Vector2(targetX, targetY);
                                    altarFound = true;
                                    break;
                                }
                            }
                        }
                        if (altarFound) break;
                    }

                    // Backup plan: jika ubin altar tidak terbaca, pakai posisi koordinat spawn NPC
                    if (!altarFound)
                    {
                        altarPosition = new Vector2(npcTilePos.X, npcTilePos.Y);
                    }
                }

                // 2. DEFINE BATAS BOX ARENA BARU (SUDAH DISESUAIKAN DENGAN UKURAN ARENA USER)
                // [PANDUAN ADJUSTMENT UKURAN STRUKTUR ARENA]
                int arenaWidthLeft = 88;    // REWORK: Jarak batas kiri dari Altar (88 blok)
                int arenaWidthRight = 88;   // REWORK: Jarak batas kanan dari Altar (88 blok)
                int arenaHeightUp = 112;    // REWORK: Jarak batas atas dari Altar (112 blok)
                int arenaHeightDown = 6;    // Batas bawah: 5 blok struktur menanjak + 1 blok ekstra penahan bug

                int minTileX = (int)altarPosition.X - arenaWidthLeft;
                int maxTileX = (int)altarPosition.X + arenaWidthRight;
                int minTileY = (int)altarPosition.Y - arenaHeightUp;
                int maxTileY = (int)altarPosition.Y + arenaHeightDown;

                // Konversi Box koordinat ubin menjadi koordinat fisik pixel dunia Terraria (dikali 16)
                Rectangle arenaBounds = new Rectangle(
                    minTileX * 16,
                    minTileY * 16,
                    (maxTileX - minTileX) * 16,
                    (maxTileY - minTileY) * 16
                );

                // 3. SCAN APAKAH ADA PLAYER YANG AKTIF DI DALAM AREA KOTAK ARENA
                bool anyPlayerInArena = false;
                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player player = Main.player[i];
                    
                    // Player harus hidup dan hitbox badannya berada di dalam area bounds arena
                    if (player.active && !player.dead && arenaBounds.Intersects(player.Hitbox))
                    {
                        anyPlayerInArena = true;
                        break; // Batalkan despawn jika minimal ada 1 player di dalam ruangan
                    }
                }

                // 4. JIKA ARENA BENAR-BENAR KOSONG (Player kabur keluar / semuanya mati) -> PAKSA GOLEM DESPAWN
                if (!anyPlayerInArena)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // Efek debu partikel batu purba hancur saat Golem buyar keluar arena
                        for (int d = 0; d < 40; d++)
                        {
                            Dust.NewDust(npc.position, npc.width, npc.height, DustID.Stone, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f), 100, default, 1.3f);
                        }

                        // Menghapus eksistensi Golem secara instan dan aman dari server game
                        npc.active = false;
                        npc.netUpdate = true;
                    }

                    return false; // Hentikan AI bawaan agar boss langsung lenyap seketika
                }
            }

            return true; // Tetap lanjutkan pertarungan normal jika player berada di dalam arena
        }
    }
}