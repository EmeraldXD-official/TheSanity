using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class AntlionChargerRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void PostAI(NPC npc)
        {
            // --- 1. CEK ID (Charger & Larva) ---
            bool isChargerOrLarva = npc.type == 508 || npc.type == 580 || npc.type == 582;
            if (!isChargerOrLarva) return;

            Player target = null; 
            if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (target == null || !target.active || target.dead) return;

            // --- 2. LOGIKA DETEKSI TEMBOK DI DEPAN ---
            // Kita cek posisi 1-2 block di depan arah gerak NPC
            Vector2 checkDirection = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
            Vector2 checkPos = npc.Center + (checkDirection * 32f); // Cek 2 block ke depan

            // Cek apakah ada ubin solid di posisi tersebut
            bool isWallInFront = SharpCollisionCheck(checkPos);
            
            // Cek juga apakah target ada jauh di atas (biar dia bisa manjat tembok)
            bool targetHighAbove = target.position.Y < npc.position.Y - 100f && Math.Abs(target.Center.X - npc.Center.X) < 160f;

            // --- 3. EKSEKUSI TEMBUS (SMOOTH MODE) ---
            if (isWallInFront || targetHighAbove)
            {
                // HANYA tembus jika memang ada tembok, tapi tetap beri kecepatan ke atas agar tidak merosot
                npc.noTileCollide = true;
                
                // Dorong ke arah player agar tidak "stuck" di dalam block
                Vector2 velocityToTarget = (target.Center - npc.Center).SafeNormalize(Vector2.Zero) * 5f;
                npc.velocity = Vector2.Lerp(npc.velocity, velocityToTarget, 0.1f);

                if (Main.rand.NextBool(5))
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Sand, 0, 0, 150, default, 0.8f);
                }
            }
            else
            {
                // Jika depannya udara atau jalanan biasa, balik jadi solid biar gak jatuh ke void
                npc.noTileCollide = false;
            }
        }

        // Fungsi pembantu untuk cek ubin solid secara presisi
        private bool SharpCollisionCheck(Vector2 pos)
        {
            Point tilePos = pos.ToTileCoordinates();
            if (WorldGen.InWorld(tilePos.X, tilePos.Y))
            {
                // 🔥 FIX: Menggunakan Terraria.Tile agar tidak bentrok dengan nama folder/namespace Tile kamu
                Terraria.Tile tile = Main.tile[tilePos.X, tilePos.Y];
                return tile.HasTile && Main.tileSolid[tile.TileType] && !tile.IsActuated;
            }
            return false;
        }
    }
}