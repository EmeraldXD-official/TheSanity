using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization; // 👈 TAMBAHKAN UNTUK REGISTRASI NAMA KUSTOM
using Terraria.ModLoader;

namespace TheSanity.Tiles
{
    public class AmbariumOreTile : ModTile
    {
        public override void SetStaticDefaults()
        {
            TileID.Sets.Ore[Type] = true;
            Main.tileSpelunker[Type] = true;

            // Prioritas Metal Detector
            Main.tileOreFinderPriority[Type] = 550;

            Main.tileShine2[Type] = true;
            Main.tileShine[Type] = 975;
            Main.tileMergeDirt[Type] = true;

            // Menyatu dengan batu (Stone)
            Main.tileMerge[Type][TileID.Stone] = true;
            Main.tileMerge[TileID.Stone][Type] = true;

            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;

            HitSound = SoundID.Tink;
            DustType = DustID.PurpleCrystalShard;

            // 🔹 UBAH DARI "Ambarium Ore Tile" MENJADI "Ambarium Ore"
            AddMapEntry(new Color(170, 25, 245), Language.GetOrRegister("Mods.TheSanity.Tiles.AmbariumOreTile.MapEntry", () => "Ambarium Ore"));
            AddMapEntry(new Color(128, 128, 128), Language.GetOrRegister("Mods.TheSanity.Tiles.AmbariumOreTile.MapEntryStone", () => "Stone"));

            MineResist = 1.5f;
        }

        public override ushort GetMapOption(int i, int j)
        {
            if (!Main.bloodMoon)
            {
                return 1; // Menyamar jadi "Stone"
            }
            return 0; // Tampil sebagai "Ambarium Ore"
        }

        public override bool CanKillTile(int i, int j, ref bool blockDamaged)
        {
            if (Main.bloodMoon)
            {
                Player player = Main.LocalPlayer;
                if (player.HeldItem.pick < 100)
                {
                    blockDamaged = false;
                    return false;
                }
            }
            return true;
        }

        public override IEnumerable<Item> GetItemDrops(int i, int j)
        {
            if (Main.bloodMoon)
            {
                yield return new Item(ModContent.ItemType<Items.OreBar.Ambarium.AmbariumOre>());
            }
            else
            {
                yield return new Item(ItemID.StoneBlock);
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
        {
            if (!Main.bloodMoon)
            {
                Tile tile = Main.tile[i, j];
                Texture2D stoneTexture = TextureAssets.Tile[TileID.Stone].Value;

                Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange, Main.offScreenRange);
                Vector2 drawPos = new Vector2(i * 16, j * 16) - Main.screenPosition + zero;
                Rectangle frame = new Rectangle(tile.TileFrameX, tile.TileFrameY, 16, 16);

                spriteBatch.Draw(stoneTexture, drawPos, frame, Lighting.GetColor(i, j), 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
                return false;
            }
            return true;
        }

        public override void NearbyEffects(int i, int j, bool closer)
        {
            if (!Main.bloodMoon) return;
            if (!closer) return;

            Player player = Main.LocalPlayer;
            if (!player.active || player.dead) return;

            Rectangle tileRect = new Rectangle(i * 16, j * 16, 16, 16);
            Rectangle playerTouchBox = new Rectangle(
                (int)player.position.X - 1,
                (int)player.position.Y - 1,
                player.width + 2,
                player.height + 2
            );

            if (playerTouchBox.Intersects(tileRect))
            {
                player.AddBuff(BuffID.Obstructed, 30);
                player.AddBuff(BuffID.Venom, 30);
            }
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            if (!Main.bloodMoon) return;

            float pulse = (float)System.Math.Sin(Main.GameUpdateCount * 0.05f) * 0.25f + 0.75f;
            r = 0.67f * pulse;
            g = 0.10f * pulse;
            b = 0.96f * pulse;
        }
    }
}