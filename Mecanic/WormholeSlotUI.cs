using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;
using Terraria.GameContent;

namespace TheSanity.Mecanic
{
    public class WormholeSlotUI : UIState
    {
        private CustomItemSlot _slotElement;

        public override void OnInitialize()
        {
            // Membuat elemen slot kustom
            _slotElement = new CustomItemSlot();
            
            // [GUI POSITION BALANCING LOCATION]
            // Left diubah ke 585f (geser ke kanan sedikit dari 545f)
            // Top diubah ke 175f (naik ke atas lumayan banyak dari 335f)
            // Ukuran element UI disesuaikan jadi 52x52 mengikuti ukuran slot vanilla
            _slotElement.Left.Set(585f, 0f); 
            _slotElement.Top.Set(115f, 0f); 
            _slotElement.Width.Set(52f, 0f);
            _slotElement.Height.Set(52f, 0f);

            Append(_slotElement);
        }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);

            // GUI ini HANYA terlihat/muncul saat player sedang membuka Inventory
            if (!Main.playerInventory || Main.recBigList)
            {
                _slotElement.Left.Set(-9999f, 0f); // Singkirkan jauh-jauh dari layar jika inv tutup
            }
            else
            {
                // [CRITICAL] Pastikan nilai koordinat X ini selalu sama dengan yang ada di OnInitialize!
                _slotElement.Left.Set(585f, 0f); 
            }
        }
    }

    // Kelas khusus untuk menghandle interaksi klik dan visual tekstur WormholeSlot.png
    public class CustomItemSlot : UIElement
    {
        protected override void DrawSelf(SpriteBatch spriteBatch)
        {
            Player player = Main.LocalPlayer;
            var modPlayer = player.GetModPlayer<WormholeSlotPlayer>();

            // Mengambil tekstur WormholeSlot.png milikmu dari folder Mecanic
            Texture2D slotTexture = ModContent.Request<Texture2D>("TheSanity/Mecanic/WormholeSlot").Value;
            Vector2 position = GetDimensions().Position();

            // =========================================================================
            // LOKASI AUTOMATIC SPRITE SCALE (PERLEBAR GAMBAR 32x32 -> 52x52)
            // =========================================================================
            // Menggunakan Target Rectangle agar gambar 32x32 kamu otomatis ditarik melar secara sempurna
            // menjadi seukuran 52x52 piksel mengikuti resolusi UI asli Terraria.
            Rectangle targetBounds = new Rectangle((int)position.X, (int)position.Y, 48, 48);
            
            // Menggunakan null untuk sourceRectangle (menggambar keseluruhan tekstur), 
            // dan menggunakan PointClamp agar pixel art-nya tidak nge-blur saat ditarik melar.
            spriteBatch.Draw(slotTexture, targetBounds, null, Color.White);

            if (ContainsPoint(Main.MouseScreen))
            {
                Main.LocalPlayer.mouseInterface = true;

                // Batasi agar slot kustom ini HANYA bisa menerima/berinteraksi dengan Wormhole Potion
                if (Main.mouseItem.IsAir || Main.mouseItem.type == ItemID.WormholePotion)
                {
                    // Fungsi klik kiri bawaan Terraria untuk memindahkan item
                    ItemSlot.Handle(ref modPlayer.WormholeSlotItem, ItemSlot.Context.InventoryItem);
                }
            }

            // Gambar icon Wormhole Potion di atas slot jika itemnya tersedia
            if (!modPlayer.WormholeSlotItem.IsAir)
            {
                float oldScale = Main.inventoryScale;
                
                // Sedikit penyesuaian skala icon ramuan agar pas di tengah-tengah slot baru
                Main.inventoryScale = 0.85f; 

                // Gunakan fungsi draw bawaan Terraria agar animasi item dan jumlah stack terlihat natural
                // Kita geser sedikit posisi taruh itemnya (+2, +2) agar presisi di tengah slot berukuran 52x52
                Vector2 itemPosition = position + new Vector2(2f, 2f);
                ItemSlot.Draw(spriteBatch, ref modPlayer.WormholeSlotItem, ItemSlot.Context.InventoryItem, itemPosition);

                Main.inventoryScale = oldScale;
            }
        }
    }
}