using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ModLoader;

namespace TheSanity.Items.ArmorBoss.Betsy.Players
{
    // Sosok "mata" Betsy (Extra_78) -- melayang jiggle DI DEPAN player, di atas kepala, di atas
    // portal (BetsyPortalLayer digambar di belakang player, layer ini digambar paling depan/atas
    // dari semua layer lewat AfterLastVanillaLayer).
    public class BetsyNpcLayer : PlayerDrawLayer
    {
        // Extra_78.png: sprite sheet vertikal, 8 frame @ 136x120 per frame.
        private const int NpcFrameWidth = 136;
        private const int NpcFrameHeight = 120;
        private const int NpcFrameCount = 8;

        private const int TicksPerFrame = 4;

        public override Position GetDefaultPosition() => PlayerDrawLayers.AfterLastVanillaLayer;

        public override bool GetDefaultVisibility(PlayerDrawSet drawInfo)
        {
            return drawInfo.drawPlayer.GetModPlayer<BetsyArmorPlayer>().fullSetActive;
        }

        protected override void Draw(ref PlayerDrawSet drawInfo)
        {
            Player player = drawInfo.drawPlayer;

            var npcTexture = ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>(
                "BetsyArmor/Effects/Extra_78").Value;

            int frame = (int)(Main.GameUpdateCount / TicksPerFrame) % NpcFrameCount;
            Rectangle source = new Rectangle(0, frame * NpcFrameHeight, NpcFrameWidth, NpcFrameHeight);
            Vector2 origin = new Vector2(NpcFrameWidth / 2f, NpcFrameHeight / 2f);

            // Jiggle -- cuma sosok ini yang goyang, biar keliatan melayang hidup, bukan kaku.
            float wobbleTime = Main.GameUpdateCount * 0.05f + player.whoAmI * 10f;
            float rotation = (float)Math.Sin(wobbleTime * 1.1f) * MathHelper.ToRadians(7f);
            Vector2 jiggle = new Vector2(
                (float)Math.Sin(wobbleTime * 1.6f) * 2.5f,
                (float)Math.Cos(wobbleTime * 1.2f) * 3f);

            // Portal (BetsyPortalLayer) tingginya 204px, anchor-nya digeser -0.2*204 = -40.8 dari
            // player.Center, jadi tepi ATAS portal itu ada di sekitar -142.8 dari player.Center.
            // Mata ini digeser lebih tinggi lagi dari situ (+ setengah tinggi mata + sedikit jarak)
            // supaya jelas keliatan DI ATAS portal, bukan numpuk di bagian tengah/atasnya.
            Vector2 position = player.Center - Main.screenPosition
                + new Vector2(0f, -190f) + jiggle;

            DrawData data = new DrawData(
                npcTexture,
                position,
                source,
                Lighting.GetColor(player.Center.ToTileCoordinates()),
                rotation,
                origin,
                1f,
                player.direction == -1
                    ? Microsoft.Xna.Framework.Graphics.SpriteEffects.FlipHorizontally
                    : Microsoft.Xna.Framework.Graphics.SpriteEffects.None,
                0);

            drawInfo.DrawDataCache.Add(data);
        }
    }
}