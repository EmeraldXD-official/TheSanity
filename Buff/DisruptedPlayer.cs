using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures; 

namespace TheSanity.Buff
{
    public class DisruptedPlayer : ModPlayer
    {
        // Variabel penanda apakah debuff sedang aktif
        public bool HasDisruptedTime = false;
        
        // Timer internal untuk menghitung 60 frame (1 detik)
        public int TeleportTimer = 60;

        public override void ResetEffects()
        {
            HasDisruptedTime = false;
        }

        // =========================================================================
        // LOGIKA VISUAL: TRAIL PELANGI SAAT JALAN
        // =========================================================================
        public override void DrawEffects(PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
        {
            if (HasDisruptedTime)
            {
                // Efek trail partikel pelangi saat player berjalan
                if (Player.velocity != Vector2.Zero && Main.rand.NextBool(3)) 
                {
                    int dustIndex = Dust.NewDust(Player.position, Player.width, Player.height, DustID.RainbowMk2, 0f, 0f, 150, default(Color), 1.2f);
                    Main.dust[dustIndex].noGravity = true; 
                    Main.dust[dustIndex].velocity *= 0.2f;  
                }
            }
        }

        // =========================================================================
        // FIX UTAMA: KELAP-KELIP STROBO PELANGI CEPAT & NON-STOP TANPA JEDA REDUP
        // =========================================================================
        public override void ModifyDrawInfo(ref PlayerDrawSet drawInfo)
        {
            if (HasDisruptedTime)
            {
                // 1. HITUNG LOGIKA STRUKTUR KEDIP TANPA JEDA
                // Menggunakan fungsi fraksi (waktu % 1) dikali kecepatan tinggi (15f) agar transisinya instan dan konstan
                float flashTimer = (Main.GlobalTimeWrappedHourly * 15f) % 1f;
                
                // Hitung warna pelangi RGB transparan menyala (Alpha = 0 untuk efek Neon Additive)
                float colorOffset = Main.GlobalTimeWrappedHourly * 4f; // Kecepatan perubahan warna pelangi
                Color glowColor = Main.hslToRgb(colorOffset % 1f, 1f, 0.6f);
                glowColor.A = 0; 

                // --- BALANCING INTENSITAS STROBO ---
                // Kita kunci nilainya di angka tinggi (0.5f sampai 0.9f) agar saat berkedip tidak pernah menyentuh warna normal player
                float intensity = MathHelper.Lerp(0.5f, 0.9f, flashTimer);
                
                // 2. TIMPA SEMUA LAYER TUBUH PLAYER SECARA SINKRON
                drawInfo.colorArmorHead = Color.Lerp(drawInfo.colorArmorHead, glowColor, intensity);
                drawInfo.colorArmorBody = Color.Lerp(drawInfo.colorArmorBody, glowColor, intensity);
                drawInfo.colorArmorLegs = Color.Lerp(drawInfo.colorArmorLegs, glowColor, intensity);
                drawInfo.colorHair = Color.Lerp(drawInfo.colorHair, glowColor, intensity);
                drawInfo.colorBodySkin = Color.Lerp(drawInfo.colorBodySkin, glowColor, intensity);
                drawInfo.colorShirt = Color.Lerp(drawInfo.colorShirt, glowColor, intensity);
                drawInfo.colorUnderShirt = Color.Lerp(drawInfo.colorUnderShirt, glowColor, intensity);
                drawInfo.colorPants = Color.Lerp(drawInfo.colorPants, glowColor, intensity);
                drawInfo.colorShoes = Color.Lerp(drawInfo.colorShoes, glowColor, intensity);
            }
        }

        // =========================================================================
        // LOGIKA TELEPORTASI: VALIDASI AREA KOSONG MINIMAL 2x3 BLOCK
        // =========================================================================
        public void TeleportPlayerRandomly()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int maxTileRadius = 25; 
            
            int playerTileX = (int)(Player.position.X / 16f);
            int playerTileY = (int)(Player.position.Y / 16f);

            bool foundSafeSpot = false;
            Vector2 targetSpawnPosition = Vector2.Zero;

            for (int attempt = 0; attempt < 50; attempt++)
            {
                int randomTileX = playerTileX + Main.rand.Next(-maxTileRadius, maxTileRadius);
                int randomTileY = playerTileY + Main.rand.Next(-maxTileRadius, maxTileRadius);

                if (randomTileX < 10 || randomTileX > Main.maxTilesX - 10 || randomTileY < 10 || randomTileY > Main.maxTilesY - 10)
                    continue;

                if (IsAreaSafe(randomTileX, randomTileY))
                {
                    targetSpawnPosition = new Vector2(randomTileX * 16f, randomTileY * 16f);
                    foundSafeSpot = true;
                    break; 
                }
            }

            if (foundSafeSpot)
            {
                TeleportEffects(Player.Center);

                Player.Teleport(targetSpawnPosition, TeleportationStyleID.RodOfDiscord, 0);
                
                if (Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendData(65, -1, -1, null, Player.whoAmI, 0, targetSpawnPosition.X, targetSpawnPosition.Y, 3);
                }

                TeleportEffects(Player.Center);
            }
        }

        private bool IsAreaSafe(int startTileX, int startTileY)
        {
            for (int x = 0; x < 2; x++) 
            {
                for (int y = 0; y < 3; y++) 
                {
                    Tile tile = Main.tile[startTileX + x, startTileY + y];
                    
                    if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType])
                    {
                        return false; 
                    }
                    if (tile.LiquidAmount > 0 && tile.LiquidType == LiquidID.Lava)
                    {
                        return false; 
                    }
                }
            }
            return true; 
        }

        private void TeleportEffects(Vector2 position)
        {
            for (int i = 0; i < 20; i++)
            {
                int d = Dust.NewDust(position - new Vector2(16, 16), 32, 48, DustID.RainbowMk2, 0f, 0f, 100, default(Color), 1.5f);
                Main.dust[d].velocity *= 1.5f;
                Main.dust[d].noGravity = true;
            }
        }
    }
}