using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Tiles
{
    public class ReligiaOreTile : ModTile
    {
        public override void SetStaticDefaults() {
            Main.tileSolid[Type] = true;
            Main.tileMergeDirt[Type] = false;
            Main.tileBlockLight[Type] = true;
            Main.tileOreFinderPriority[Type] = 520;
            Main.tileShine2[Type] = true;
            Main.tileShine[Type] = 1200;
            Main.tileSpelunker[Type] = true;

            TileID.Sets.Ore[Type] = true;

            AddMapEntry(new Color(180, 140, 230));

            DustType = DustID.PurpleTorch;
            MineResist = 3f;
            // NOTE: sesuaikan MinPick dengan progression pickaxe yang kamu mau
            // (mis. 210 = Pickaxe Axe, 225 = Picksaw/Solar Era)
            MinPick = 110;
            HitSound = SoundID.Tink;
        }

        // Glow overlay pakai ReligiaOreTile_Glow.png, digambar full-bright
        // di atas tile aslinya (terlepas dari lighting di sekitarnya).
        public override void PostDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile tile = Main.tile[i, j];
            if (tile == null || !tile.HasTile) return;

            Texture2D glowTex = ModContent.Request<Texture2D>("TheSanity/Tiles/ReligiaOreTile_Glow").Value;

            Vector2 offset = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange, Main.offScreenRange);
            Vector2 drawPos = new Vector2(i * 16 - (int)Main.screenPosition.X, j * 16 - (int)Main.screenPosition.Y) + offset;
            Rectangle sourceRect = new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16);

            spriteBatch.Draw(glowTex, drawPos, sourceRect, Color.White);
        }
    }
}