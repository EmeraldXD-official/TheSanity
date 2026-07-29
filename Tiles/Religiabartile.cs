using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace TheSanity.Tiles
{
    public class ReligiaBarTile : ModTile
    {
        public override void SetStaticDefaults() {
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileSolidTop[Type] = true;
            Main.tileShine2[Type] = true;
            Main.tileShine[Type] = 900;

            // Bar block cuma punya 1 frame (bukan sheet variasi random kayak Ore),
            // jadi harus didaftarkan sebagai frame-important 1x1 style, bukan tile solid biasa.
            Main.tileFrameImportant[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.addTile(Type);

            AddMapEntry(new Color(180, 140, 230));

            DustType = DustID.PurpleTorch;
            MineResist = 2.5f;
            MinPick = 0; // bar block umumnya bisa ditambang pickaxe apapun
            HitSound = SoundID.Tink;
        }
    }
}