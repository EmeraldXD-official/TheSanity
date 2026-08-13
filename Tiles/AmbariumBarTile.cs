using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace TheSanity.Tiles
{
    public class AmbariumBarTile : ModTile
    {
        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = true;
            Main.tileSolidTop[Type] = true;
            Main.tileShine[Type] = 1100;
            Main.tileFrameImportant[Type] = true;

            TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
            TileObjectData.addTile(Type);

            HitSound = SoundID.Tink;
            DustType = DustID.PurpleCrystalShard;
            AddMapEntry(new Color(255, 120, 255), CreateMapEntryName());
        }
    }
}