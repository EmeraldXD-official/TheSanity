using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace TheSanity.CostumeTile
{
    public class CoalOre : ModTile
    {
        public override void SetStaticDefaults()
        {
            // Properti Dasar Ore & Tile
            TileID.Sets.Ore[Type] = true;
            Main.tileSpelunker[Type] = true; // Terlihat oleh Spelunker Potion
            Main.tileOreFinderPriority[Type] = 200; // Terdeteksi Metal Detector
            Main.tileShine2[Type] = true; // Efek kilatan ore
            Main.tileShine[Type] = 975;
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;

            // Warna Tile saat terlihat di Map (Hitam/Abu Tua)
            LocalizedText name = CreateMapEntryName();
            AddMapEntry(new Color(40, 40, 40), name);

            // Menentukan item drop saat tile dihancurkan (Coal / ID 1922)
            RegisterItemDrop(ItemID.Coal);

            // Suara & Ketahanan saat ditambang
            HitSound = SoundID.Tink;
            MineResist = 1.5f;
            MinPick = 35;

            // Menghubungkan (merge) tile ini dengan semua block dasar Terraria
            for (int i = 0; i < TileID.Count; i++)
            {
                Main.tileMerge[Type][i] = true;
            }
        }

        // Mengacak efek partikel antara Vanilla Dust ID 175 dan 191 saat dipukul/dihancurkan
        public override bool CreateDust(int i, int j, ref int type)
        {
            type = Main.rand.NextBool() ? 175 : 191;
            return true;
        }
    }
}