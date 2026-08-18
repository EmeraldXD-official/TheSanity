using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ObjectData;

namespace TheSanity.CostumeTile
{
    public class SoulCollectorAltar : ModTile
    {
        // Kalau sprite altar (70x50) ukurannya beda dari kotak placement (5x3 = 80x48),
        // atur offset ini biar posisinya pas / center secara visual.
        // X: (80 - 70) / 2 = 5   |   Y: (48 - 50) / 2 = -1 (naik 1px)
        private static readonly Vector2 DrawOffset = new Vector2(5f, -1f);

        // ---- Pengaturan visual "SoulEyeBlue" ----
        // Spritesheet 34x224, 4 frame ditumpuk vertikal -> tiap frame 34x56
        internal const int EyeFrameWidth = 34;
        internal const int EyeFrameHeight = 56;
        private const int EyeFrameCount = 4;
        private const int EyeTicksPerFrame = 4; // makin kecil makin cepat animasinya

        // X: (80 - 34) / 2 = 23  -> center horizontal di area 5x16 = 80px
        // Y: -44 -> titik tengah sprite (56/2 = 28) jatuh di antara blok virtual 4 dan 5
        //           DI ATAS altar (y = -16 relatif dari atas altar)
        // internal (bukan private) karena dipakai juga sama SoulCollectorAltarEntity
        // buat ngitung posisi dunia dari eye ini (target homing si item Souls).
        internal static readonly Vector2 EyeOffset = new Vector2(23f, -44f);

        // Ukuran satu "step" frame multitile = CoordinateWidth/Height (16) + CoordinatePadding (2)
        internal const int FrameStepX = 18;
        internal const int FrameStepY = 18;

        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = false;
            Main.tileNoAttach[Type] = true;
            Main.tileFrameImportant[Type] = true;
            Main.tileLighted[Type] = true;

            TileID.Sets.HasOutlines[Type] = true;

            TileObjectData.newTile.Width = 5;
            TileObjectData.newTile.Height = 3;

            // Ini cuma grid untuk PLACEMENT/COLLISION (bukan untuk motong gambar).
            TileObjectData.newTile.CoordinateWidth = 16;
            TileObjectData.newTile.CoordinateHeights = new int[] { 16, 16, 16 };
            TileObjectData.newTile.CoordinatePadding = 2;

            TileObjectData.newTile.UsesCustomCanPlace = true;
            TileObjectData.newTile.AnchorBottom = new AnchorData(
                AnchorType.SolidTile | AnchorType.SolidWithTop, TileObjectData.newTile.Width, 0);

            // Daftarin TileEntity (SoulCollectorAltarEntity) yang otomatis ke-place
            // bareng tile-nya. Ini yang nyimpen SoulCount per-altar.
            TileObjectData.newTile.HookPostPlaceMyPlayer = new PlacementHook(
                ModContent.GetInstance<SoulCollectorAltarEntity>().Hook_AfterPlacement, -1, 0, true);

            TileObjectData.addTile(Type);

            DustType = DustID.PurpleTorch; // ganti sesuai tema "Sanity"
            AddMapEntry(new Color(120, 60, 150), CreateMapEntryName());
        }

        // Stack terbesar per-item Souls (samain sama Item.maxStack di Souls.SetDefaults).
        // Dipakai buat mecah drop soul jadi beberapa stack, biar ga ada 1 item stack
        // 75.000+ yang aneh / berpotensi masalah render-nya.
        private const int SoulDropMaxStack = 9999;
        private const float SoulDropPercent = 0.75f;

        // Wajib: pas tile-nya dihancurin, TileEntity-nya juga harus ikut kehapus,
        // kalau ngga bakal jadi "hantu" data yang nyangkut di dunia.
        public override void KillMultiTile(int i, int j, int frameX, int frameY)
        {
            int topLeftX = i - frameX / FrameStepX;
            int topLeftY = j - frameY / FrameStepY;

            // Ambil SoulCount SEBELUM Kill(), karena abis di-Kill TileEntity-nya
            // udah kehapus dari TileEntity.ByPosition (ga bisa diakses lagi).
            if (Main.netMode != NetmodeID.MultiplayerClient
                && TileEntity.ByPosition.TryGetValue(new Point16(topLeftX, topLeftY), out TileEntity te)
                && te is SoulCollectorAltarEntity altarEntity)
            {
                int dropAmount = (int)(altarEntity.SoulCount * SoulDropPercent);
                DropSouls(topLeftX, topLeftY, dropAmount);

                // Kalau lagi ada Soul Token yang nangkring di slot convert-nya,
                // jangan sampe ikut hilang percuma pas altar-nya dihancurin --
                // lempar keluar juga ke dunia, biar player masih bisa mungutnya.
                if (!altarEntity.TokenSlotItem.IsAir)
                {
                    var dropArea = new Rectangle(topLeftX * 16, topLeftY * 16, 80, 48);
                    Item.NewItem(new EntitySource_TileBreak(topLeftX, topLeftY),
                        dropArea, altarEntity.TokenSlotItem.type, altarEntity.TokenSlotItem.stack);
                }
            }

            ModContent.GetInstance<SoulCollectorAltarEntity>().Kill(i, j);
        }

        // Spawn 75% Soul yang tersimpan sebagai item Souls di lokasi altar yang hancur.
        // Dipecah jadi beberapa stack (max SoulDropMaxStack per stack) biar ga numpuk
        // jadi 1 item raksasa. Item Souls yang jatuh ini akan otomatis "nyari" altar
        // TERDEKAT LAIN (lihat Souls.cs) -> kalau ga ada altar lain dalam radius,
        // dia cuma melayang santai terus hilang setelah 60 detik seperti biasa.
        private static void DropSouls(int topLeftX, int topLeftY, int totalAmount)
        {
            if (totalAmount <= 0)
                return;

            int soulType = ModContent.ItemType<Souls>();

            // Titik tengah area altar (5x3 tile = 80x48px) buat posisi spawn drop.
            var dropArea = new Rectangle(topLeftX * 16, topLeftY * 16, 80, 48);

            int remaining = totalAmount;
            while (remaining > 0)
            {
                int chunk = System.Math.Min(remaining, SoulDropMaxStack);
                Item.NewItem(new EntitySource_TileBreak(topLeftX, topLeftY),
                    dropArea, soulType, chunk);
                remaining -= chunk;
            }
        }

        // Klik kanan altar -> buka GUI Soul Collector.
        public override bool RightClick(int i, int j)
        {
            Tile tile = Main.tile[i, j];

            int topLeftX = i - tile.TileFrameX / FrameStepX;
            int topLeftY = j - tile.TileFrameY / FrameStepY;

            if (TileEntity.ByPosition.TryGetValue(new Point16(topLeftX, topLeftY), out TileEntity te)
                && te is SoulCollectorAltarEntity altarEntity)
            {
                SoulCollectorUISystem.Instance.ToggleAltar(altarEntity);
            }
            else
            {
                // TEMPORARY DEBUG: if this message shows up (orange), it means the click WAS
                // detected, but no TileEntity was found for this altar. Usually happens when
                // the altar was placed BEFORE the latest update (no entity attached yet).
                // Fix: destroy this altar and place a fresh one.
                // Remove this else block once it's no longer needed.
                Main.NewText($"[DEBUG] Soul Collector Altar: click detected at ({topLeftX}, {topLeftY}) " +
                    $"but no TileEntity was found. Try destroying and re-placing the altar.",
                    255, 165, 0);
            }

            return true;
        }

        // Menggerakkan frame animasi mata (dibaca lewat Main.tileFrame[Type] saat menggambar)
        public override void AnimateTile(ref int frame, ref int frameCounter)
        {
            frameCounter++;
            if (frameCounter >= EyeTicksPerFrame)
            {
                frameCounter = 0;
                frame = (frame + 1) % EyeFrameCount;
            }
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
        {
            Tile tile = Main.tile[i, j];

            // Gambar HANYA sekali, waktu posisi ini adalah kotak kiri-atas
            // dari multitile (frame 0,0). Kotak lainnya tidak digambar sama sekali.
            if (tile.TileFrameX != 0 || tile.TileFrameY != 0)
                return false;

            Vector2 zero = Main.drawToScreen
                ? Vector2.Zero
                : new Vector2(Main.offScreenRange, Main.offScreenRange);

            Vector2 basePos = new Vector2(
                i * 16 - (int)Main.screenPosition.X,
                j * 16 - (int)Main.screenPosition.Y) + zero;

            Color color = Lighting.GetColor(i, j);

            // 1. SoulEyeBlue digambar DULU -> jadi ada di paling belakang
            // Hard-swap tiap frame (ga di-crossfade), biar animasinya keliatan cepet & tegas.
            Texture2D eye = ModContent.Request<Texture2D>(
                "TheSanity/CostumeTile/SoulEyeBlue").Value;

            int eyeFrame = Main.tileFrame[Type] % EyeFrameCount;
            Rectangle eyeSourceCurrent = new Rectangle(0, eyeFrame * EyeFrameHeight, EyeFrameWidth, EyeFrameHeight);

            spriteBatch.Draw(eye, basePos + EyeOffset, eyeSourceCurrent, Color.White, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            // 2. Badan altar (70x50) digambar SETELAH eye -> menutupi eye di belakangnya
            Texture2D tex = TextureAssets.Tile[Type].Value;
            spriteBatch.Draw(tex, basePos + DrawOffset, null, color, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            return false; // matikan drawing default sepenuhnya (semua kotak)
        }

        public override void PostDraw(int i, int j, SpriteBatch spriteBatch)
        {
            Tile tile = Main.tile[i, j];

            if (tile.TileFrameX != 0 || tile.TileFrameY != 0)
                return;

            Vector2 zero = Main.drawToScreen
                ? Vector2.Zero
                : new Vector2(Main.offScreenRange, Main.offScreenRange);

            Vector2 basePos = new Vector2(
                i * 16 - (int)Main.screenPosition.X,
                j * 16 - (int)Main.screenPosition.Y) + zero;

            // Ambil TileEntity-nya lewat "as" (bukan pattern var di tengah &&) biar
            // altarEntity-nya masih bisa dipakai lagi di bawah (buat gambar ritual
            // soul-nya) tanpa ambigu soal definite-assignment.
            TileEntity.ByPosition.TryGetValue(new Point16(i, j), out TileEntity te);
            SoulCollectorAltarEntity altarEntity = te as SoulCollectorAltarEntity;
            bool ritualActive = altarEntity != null && altarEntity.IsRitualActive;

            // 3. Glow badan altar tetap di paling depan (efek cahaya di atas semuanya).
            // Normal: fade in/fade out halus (kelap-kelip pelan, kek "napas").
            // Pas lagi ritual revive: glow-nya FULL terus tanpa kelap-kelip,
            // biar keliatan jelas altar-nya "lagi aktif/kerja".
            float glowAlpha;
            if (ritualActive)
            {
                glowAlpha = 1f;
            }
            else
            {
                // Offset fase pake posisi tile-nya biar altar yang beda-beda ga
                // "napas" bareng serempak semua (keliatan lebih hidup/natural).
                float phase = (i * 7 + j * 13) * 0.3f;
                glowAlpha = 0.55f + 0.45f * (float)System.Math.Sin(Main.GameUpdateCount * 0.05f + phase);
            }

            Texture2D glow = ModContent.Request<Texture2D>(
                "TheSanity/CostumeTile/SoulCollectorAltarGlow").Value;
            spriteBatch.Draw(glow, basePos + DrawOffset, null, Color.White * glowAlpha, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);

            // NOTE: Soul-soul ritual DULU digambar manual di sini (poin 4, pakai
            // SpriteBatch yang sama biar transform-nya identik sama altar/eye/glow).
            // Sekarang soul-soul ritual adalah SoulVisualProjectile beneran
            // (di-spawn dari SoulCollectorAltarEntity.TickRitualSouls()), jadi
            // gambarnya udah otomatis lewat jalur draw Projectile bawaan game --
            // ga perlu hook manual di sini lagi. Lihat SoulVisualProjectile.cs.
        }
    }
}
