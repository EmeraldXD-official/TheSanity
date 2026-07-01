using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums; 
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace TheSanity.Tiles // Alamat dikunci ke 'Tiles' agar klop dengan file item
{
    public class TwinkleRelicTile : ModTile
    {
        public override string Texture => "TheSanity/CostumeTile/TwinkleRelicBase";

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true;
            Main.tileNoAttach[Type] = true;
            Main.tileLavaDeath[Type] = false;

            // BALIK KE AWAL: Setingan manual 3x1 tanpa ganggu gugat Style vanilla
            TileObjectData.newTile.Width = 3;
            TileObjectData.newTile.Height = 1;
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinateHeights = new int[] { 16 };
            TileObjectData.newTile.CoordinatePadding = 0; 
            
            TileObjectData.newTile.Origin = new Point16(1, 0); 
            TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile | AnchorType.SolidWithTop | AnchorType.SolidSide, TileObjectData.newTile.Width, 0);
            
            TileObjectData.newTile.DrawYOffset = 2; 
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(233, 207, 94), Language.GetText("MapObject.Relic"));
            DustType = DustID.GoldFlame;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile tile = Main.tile[i, j];

            if (tile.TileFrameX == 0 && tile.TileFrameY == 0) {
                Texture2D baseTex = ModContent.Request<Texture2D>("TheSanity/CostumeTile/TwinkleRelicBase").Value;
                Texture2D topTex = ModContent.Request<Texture2D>("TheSanity/CostumeTile/TwinkleRelicTop").Value;

                Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange, Main.offScreenRange);
                
                float centerX = i * 16 + 24 - Main.screenPosition.X;
                float bottomY = j * 16 + 16 - Main.screenPosition.Y; 
                Vector2 baseAnchor = new Vector2(centerX, bottomY) + zero;

                // 1. DRAW TWINKLE BASE (TATAKAN EMAS 48x16)
                Vector2 baseOrigin = new Vector2(24f, 16f); 
                spriteBatch.Draw(baseTex, baseAnchor, null, Lighting.GetColor(i, j), 0f, baseOrigin, 1f, SpriteEffects.None, 0f);

                // 2. DRAW TWINKLE TOP (STAR ANIMASI)
                float floatingOffset = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.0f) * 3f; 
                float pulseScale = 1.0f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.0f) * 0.05f;
                float topYOffset = -54f; 

                Vector2 topDrawPos = baseAnchor + new Vector2(0f, topYOffset + floatingOffset);
                Vector2 topOrigin = new Vector2(32f, 30f);

                spriteBatch.Draw(topTex, topDrawPos, null, Color.White * 0.95f, 0f, topOrigin, pulseScale, SpriteEffects.None, 0f);

                Color glowColor = Color.Yellow * 0.25f;
                spriteBatch.Draw(topTex, topDrawPos, null, glowColor, 0f, topOrigin, pulseScale * 1.1f, SpriteEffects.None, 0f);
            }

            return false; 
        }
    }
}