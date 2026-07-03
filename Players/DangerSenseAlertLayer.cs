using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace TheSanity.Players
{
    public class DangerSenseAlertLayer : PlayerDrawLayer
    {
        private Asset<Texture2D> alertTexture;

        // KUNCI PERBAIKAN: Mengubah PlayerDrawLayers.ArmOverBody menjadi PlayerDrawLayers.HeldItem
        // Ini memastikan seluruh bagian tubuh, armor, kosmetik, dan senjata sudah masuk ke dalam cache gambar
        public override Position GetDefaultPosition() => new AfterParent(PlayerDrawLayers.HeldItem);

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            var modPlayer = drawInfo.drawPlayer.GetModPlayer<DangerSensePlayer>();
            if (!modPlayer.dangerSenseEquipped || !modPlayer.isTimeSlowed)
            {
                return;
            }

            // --- 1. LOGIKA RENDERING 16 BAYANGAN PLAYER MENGGUNAKAN AFTERIMAGE ---
            // Mengambil snapshot tiruan dari apa saja yang sudah digambar di tubuh player hingga detik ini
            List<DrawData> playerSprites = new List<DrawData>(drawInfo.DrawDataCache);

            foreach (Vector2 point in modPlayer.escapePoints)
            {
                // Hitung selisih jarak dari koordinat asli player ke titik koordinat luar lingkaran
                Vector2 worldOffset = point - drawInfo.drawPlayer.Center;

                foreach (DrawData data in playerSprites)
                {
                    if (data.texture == null) continue;

                    // Salin data gambar asli player
                    DrawData shadowData = data;
                    
                    // Geser koordinat posisinya secara presisi ke titik lingkaran luar
                    shadowData.position += worldOffset;
                    
                    // Warnai menjadi bayangan Cyan transparan murni tanpa partikel debu
                    shadowData.color = Color.Cyan * 0.35f;

                    // Masukkan bayangan ke dalam antrean render layar
                    drawInfo.DrawDataCache.Add(shadowData);
                }
            }

            // --- 2. RENDERING GAMBAR PETIR (DANGER ALERT) DI ATAS KEPALA PLAYER ASLI ---
            if (alertTexture == null)
            {
                alertTexture = ModContent.Request<Texture2D>("TheSanity/Players/dangeralert");
            }

            Player drawPlayer = drawInfo.drawPlayer;
            Texture2D texture = alertTexture.Value;

            float animationDuration = 30f; 
            float progress = 1f;

            if (modPlayer.alertAnimationTimer < animationDuration)
            {
                progress = modPlayer.alertAnimationTimer / animationDuration;
            }

            float currentScale = MathHelper.Lerp(0.1f, 1.3f, progress);

            Vector2 drawPos = drawInfo.Position - Main.screenPosition;
            drawPos.X += drawPlayer.width / 2f; 
            drawPos.Y -= 55f; 

            float shakeX = (float)Math.Sin(Main.GameUpdateCount * 0.9f) * 2.5f;
            drawPos.X += shakeX;

            Vector2 origin = new Vector2(texture.Width / 2f, 0f);

            DrawData alertData = new DrawData(
                texture,
                new Vector2((int)drawPos.X, (int)drawPos.Y), 
                null,
                Color.White * 0.95f, 
                0f, 
                origin,
                currentScale, 
                SpriteEffects.None,
                0
            );

            drawInfo.DrawDataCache.Add(alertData);
        }
    }
}