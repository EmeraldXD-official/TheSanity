using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace TheSanity.NPCs 
{
    public class FlareGlobalNPC : Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        public bool sunMarked = false;

        public override void ResetEffects(NPC npc) {
            if (!npc.active) {
                sunMarked = false;
            }
        }

        // MENGGAMBAR RETIKEL TARGET DI ATAS KEPALA MUSUH
        public override void PostDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (sunMarked && npc.active) {
                // FIX: Mengambil langsung file retikel Lock-On bawaan Terraria dari internal asset repository
                Texture2D reticleTex = Main.Assets.Request<Texture2D>("Images/UI/LockOn").Value; 

                // Posisi retikel melayang di atas musuh dengan efek animasi naik-turun (sin wave)
                Vector2 drawPos = npc.Top - screenPos + new Vector2(0, -25f + MathF.Sin(Main.GlobalTimeWrappedHourly * 6f) * 4f);
                
                Color reticleColor = Color.Yellow * 0.9f;
                float pulseScale = 0.9f + MathF.Sin(Main.GlobalTimeWrappedHourly * 10f) * 0.1f;

                // LAYER 1: Gambar retikel utama (berdenyut membesar-mengecil)
                spriteBatch.Draw(reticleTex, drawPos, null, reticleColor, 0f, reticleTex.Size() / 2f, pulseScale, SpriteEffects.None, 0f);
                
                // LAYER 2: Gambar retikel dalam (berputar perlahan dengan warna oranye solar)
                spriteBatch.Draw(reticleTex, drawPos, null, Color.Orange * 0.7f, Main.GlobalTimeWrappedHourly * 2f, reticleTex.Size() / 2f, pulseScale * 0.7f, SpriteEffects.None, 0f);
            }
        }
    }
}