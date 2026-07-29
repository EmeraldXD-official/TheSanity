using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Tiles
{
    // =========================================================================
    // BASE CLASS LOGIC (INDUK) - MENANGANI SEMUA MEKANIK UTAMA
    // =========================================================================
    public abstract class BaseGemRelicTile : ModTile
    {
        public abstract int VanillaTileOnID { get; }
        public abstract int VanillaTileOffID { get; }
        public abstract int LargeGemItemID { get; }
        public abstract Vector3 LightColor { get; }

        public override string Texture => "Terraria/Images/Tiles_582";

        public override void SetStaticDefaults() {
            Main.tileFrameImportant[Type] = true; 
            Main.tileLighted[Type] = true;        
            Main.tileLavaDeath[Type] = true;
            TileID.Sets.DisableSmartCursor[Type] = true;

            AddMapEntry(new Color(130, 130, 130));
        }

        // MEKANIK KABEL (WIRE INTERACTION)
        public override void HitWire(int i, int j) {
            Tile tile = Main.tile[i, j];

            tile.TileFrameX = (short)(tile.TileFrameX == 0 ? 18 : 0);

            Wiring.SkipWire(i, j);

            if (Main.netMode != NetmodeID.SinglePlayer) {
                NetMessage.SendTileSquare(-1, i, j, 1);
            }
        }

        // SUNTIKAN CAHAYA (LIGHT EMISSION)
        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b) {
            Tile tile = Main.tile[i, j];
            if (tile.TileFrameX == 0) { 
                r = LightColor.X;
                g = LightColor.Y;
                b = LightColor.Z;
            }
        }

        // MENGGAMBAR TEMPAT DUDUKAN (PRE-DRAW TILE BASE)
        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile tile = Main.tile[i, j];
            
            int targetVanillaID = (tile.TileFrameX == 0) ? VanillaTileOnID : VanillaTileOffID;

            Main.instance.LoadTiles(targetVanillaID);
            Texture2D tileTex = TextureAssets.Tile[targetVanillaID].Value;

            // Dudukan kotak lurus sempurna
            Rectangle sourceRect = new Rectangle(162, 54, 16, 16);

            Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange, Main.offScreenRange);
            Vector2 drawPosition = new Vector2(i * 16, j * 16) - Main.screenPosition + zero;

            Color renderColor = (tile.TileFrameX == 0) ? Color.White : Lighting.GetColor(i, j);

            spriteBatch.Draw(tileTex, drawPosition, sourceRect, renderColor, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
            
            return false; 
        }

        // MENGGAMBAR VISUAL TERBANG (POST-DRAW LARGE GEM EFFECT)
        public override void PostDraw(int i, int j, SpriteBatch spriteBatch) {
            Tile tile = Main.tile[i, j];
            if (tile.TileFrameX != 0) return; 

            Texture2D gemTex = TextureAssets.Item[LargeGemItemID].Value;

            Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange, Main.offScreenRange);

            float hoverOffset = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.5f + (i * 0.15f)) * 4f;

            // FIX VISUAL: Diubah dari -26 menjadi -38 agar posisi melayang kristalnya lebih tinggi ke atas
            Vector2 gemPosition = new Vector2(i * 16 + 8, j * 16 + 8 - 38 + hoverOffset) - Main.screenPosition + zero;
            Vector2 origin = gemTex.Size() / 2f;

            spriteBatch.Draw(gemTex, gemPosition, null, Color.White, 0f, origin, 1.2f, SpriteEffects.None, 0f);
        }
    }

    // =========================================================================
    // IMPLEMENTASI KELOMPOK GEM (ANAK TURUNAN)
    // =========================================================================

    // 1. AMETHYST
    public class AmethystGemsparkRelic : BaseGemRelicTile
    {
        public override int VanillaTileOnID => TileID.AmethystGemspark;
        public override int VanillaTileOffID => TileID.AmethystGemsparkOff;
        public override int LargeGemItemID => ItemID.LargeAmethyst;
        public override Vector3 LightColor => new Vector3(0.73f, 0.30f, 0.82f); 
    }

    // 2. TOPAZ
    public class TopazGemsparkRelic : BaseGemRelicTile
    {
        public override int VanillaTileOnID => TileID.TopazGemspark;
        public override int VanillaTileOffID => TileID.TopazGemsparkOff;
        public override int LargeGemItemID => ItemID.LargeTopaz;
        public override Vector3 LightColor => new Vector3(0.88f, 0.55f, 0.10f); 
    }

    // 3. SAPPHIRE
    public class SapphireGemsparkRelic : BaseGemRelicTile
    {
        public override int VanillaTileOnID => TileID.SapphireGemspark;
        public override int VanillaTileOffID => TileID.SapphireGemsparkOff;
        public override int LargeGemItemID => ItemID.LargeSapphire;
        public override Vector3 LightColor => new Vector3(0.12f, 0.33f, 0.85f); 
    }

    // 4. EMERALD
    public class EmeraldGemsparkRelic : BaseGemRelicTile
    {
        public override int VanillaTileOnID => TileID.EmeraldGemspark;
        public override int VanillaTileOffID => TileID.EmeraldGemsparkOff;
        public override int LargeGemItemID => ItemID.LargeEmerald;
        public override Vector3 LightColor => new Vector3(0.10f, 0.78f, 0.22f); 
    }

    // 5. RUBY
    public class RubyGemsparkRelic : BaseGemRelicTile
    {
        public override int VanillaTileOnID => TileID.RubyGemspark;
        public override int VanillaTileOffID => TileID.RubyGemsparkOff;
        public override int LargeGemItemID => ItemID.LargeRuby;
        public override Vector3 LightColor => new Vector3(0.87f, 0.12f, 0.15f); 
    }

    // 6. DIAMOND
    public class DiamondGemsparkRelic : BaseGemRelicTile
    {
        public override int VanillaTileOnID => TileID.DiamondGemspark;
        public override int VanillaTileOffID => TileID.DiamondGemsparkOff;
        public override int LargeGemItemID => ItemID.LargeDiamond;
        public override Vector3 LightColor => new Vector3(0.63f, 0.83f, 0.90f); 
    }
}