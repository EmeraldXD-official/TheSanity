using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using System.Collections.Generic;

namespace TheSanity.Items
{
    public class SoulofForgottenSnow : ModItem
    {
        public override void SetStaticDefaults()
        {
            // =========================================================================
            // [GUIDE & BALANCING LOKASI: KECEPAN ANIMASI (SMOOTH)]
            // Angka 3 = Durasi tiap frame (ticks). Semakin kecil, animasi semakin cepat & smooth!
            // Angka 4 = Total jumlah frame vertikal pada sprite sheet.
            // =========================================================================
            Main.RegisterItemAnimation(Item.type, new DrawAnimationVertical(3, 4));
            
            ItemID.Sets.AnimatesAsSoul[Item.type] = true; 
            ItemID.Sets.ItemIconPulse[Item.type] = true; 
            ItemID.Sets.ItemNoGravity[Item.type] = true; 

            // Kategori Journey Mode
            Item.ResearchUnlockCount = 25; 
        }

        public override void SetDefaults()
        {
            // Ukuran hitbox fisik item di dunia game tetap memakai ukuran 1 frame murni
            Item.width = 22;
            Item.height = 22; // Pas dengan ukuran bodi asli per frame (22x22)
            
            Item.maxStack = Item.CommonMaxStack; 
            Item.value = Item.sellPrice(0, 0, 20, 0); 
            Item.rare = ItemRarityID.Orange; 
        }

        // =========================================================================
        // [FIX VISUAL INVENTORY]: MENANGANI SPRITE OFFSET DI DALAM TAS / SLOT UI
        // =========================================================================
        public override bool PreDrawInInventory(SpriteBatch spriteBatch, Vector2 position, Rectangle frame, Color drawColor, Color itemColor, Vector2 origin, float scale)
        {
            // Mengambil texture gambar item kita
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

            int singleFrameHeight = 22;
            // Ambil frame UI inventory saat ini secara dinamis
            int currentFrameIndex = Main.itemAnimations[Item.type].Frame;

            // Hitung ulang titik koordinat potong Y murni dengan bypass jeda 1 pixel
            int drawStartY = (currentFrameIndex * singleFrameHeight) + currentFrameIndex;

            // Buat pembatas baru khusus untuk merender frame di UI tas
            Rectangle customFrame = new Rectangle(0, drawStartY, texture.Width, singleFrameHeight);

            // Gambar item di slot inventory menggunakan frame kustom kita
            spriteBatch.Draw(texture, position, customFrame, drawColor, 0f, origin, scale, SpriteEffects.None, 0f);

            return false; // Return false agar tModLoader tidak menimpa dengan gambar default yang offset
        }

        // =========================================================================
        // [GUIDE & BALANCING LOKASI: EFEK TRANSPARAN & FIX OFFSET SPRITE DI TANAH]
        // =========================================================================
        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int drawRectSize)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

            int singleFrameHeight = 22; 
            int currentFrameIndex = Main.itemAnimations[Item.type].Frame; 

            int drawStartY = (currentFrameIndex * singleFrameHeight) + currentFrameIndex; 

            Rectangle frame = new Rectangle(0, drawStartY, texture.Width, singleFrameHeight);
            Vector2 origin = frame.Size() / 2f;
            
            Vector2 drawPos = new Vector2(
                Item.position.X - Main.screenPosition.X + (float)(Item.width / 2), 
                Item.position.Y - Main.screenPosition.Y + (float)(Item.height / 2)
            );

            drawPos.Y += 2f; // Offset visual melayang di tanah

            // Balancing Transparansi saat tergeletak di dunia (60% Opacity)
            float opacity = 0.6f;
            Color transparentColor = lightColor * opacity;

            spriteBatch.Draw(texture, drawPos, frame, transparentColor, rotation, origin, scale, SpriteEffects.None, 0f);

            return false; 
        }

        // Mengubah warna teks nama item menjadi Cyan lewat sistem Tooltip
        public override void ModifyTooltips(List<TooltipLine> tooltips)
        {
            foreach (TooltipLine line in tooltips)
            {
                if (line.Mod == "Terraria" && line.Name == "ItemName")
                {
                    line.OverrideColor = new Color(0, 255, 255);
                    break;
                }
            }
        }

        // Membuat item memancarkan cahaya Cyan di sekitarnya saat melayang di dunia game
        public override void PostUpdate()
        {
            Lighting.AddLight(Item.Center, 0.2f, 0.8f, 1.0f);
        }
    }
}