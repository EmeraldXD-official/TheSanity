using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent;

namespace TheSanity.Items
{
    public class SoulOfTheSun : ModItem
    {
        public override string Texture => "TheSanity/Items/IdolicSoul";

        public override void SetStaticDefaults()
        {
            // =========================================================================
            // MEKANIK TERBANG / ANTI-GRAVITASI (SOUL BEHAVIOR)
            // =========================================================================
            // Kode di bawah ini membuat item AKAN TETAP TERBANG/MELAYANG di udara saat drop,
            // tidak akan jatuh ke tanah akibat gravitasi, persis seperti Soul asli Terraria.
            ItemID.Sets.ItemNoGravity[Item.type] = true;
        }

        public override void SetDefaults()
        {
            // PENGATURAN UKURAN ITEM (87x87)
            Item.width = 87;
            Item.height = 87;
            
            Item.maxStack = Item.CommonMaxStack; 
            Item.value = Item.sellPrice(0, 2, 0, 0); 
            Item.rare = ItemRarityID.Yellow; 
            Item.material = true; 
        }

        public override void PostUpdate()
        {
            // =========================================================================
            // LOKASI PENGATURAN CAHAYA (GLOW LIGHT) - SUDAH DI-REDUKSI
            // =========================================================================
            // Karena spritemu ada bagian semi-transparan, nilainya diturunkan drastis 
            // agar cahayanya lembut (soft glow) dan tidak merusak detail visual sprite.
            // Nilai maksimal 1f (sangat terang), di bawah ini disetel sangat tipis:
            float nilaiMerah = 0.25f;
            float nilaiHijau = 0.15f;
            float nilaiBiru = 0.05f;
            Lighting.AddLight(Item.Center, nilaiMerah, nilaiHijau, nilaiBiru);
        }

        public override bool PreDrawInWorld(SpriteBatch spriteBatch, Color lightColor, Color alphaColor, ref float rotation, ref float scale, int whoAmI)
        {
            Texture2D texture = TextureAssets.Item[Item.type].Value;

            // =========================================================================
            // LOKASI PENGATURAN KECEPATAN ROTASI & PULSASI
            // =========================================================================
            
            // 1. KECEPATAN ROTASI (MUTAR LAMBAT)
            float kecepatanPutaran = 0.012f; // Sedikit diperlambat dari kemarin
            rotation = (float)Main.time * kecepatanPutaran;

            // 2. EFEK MEMBESAR MENGECIL (PULSING EFFECT)
            float kecepatanPulse = 0.03f; 
            float jarakPulse = 0.10f; // Gelombang detak dikecilkan agar halus     
            float sinWavePulse = MathF.Sin((float)Main.time * kecepatanPulse);
            scale = 1f + (sinWavePulse * jarakPulse);

            // 3. EFEK MENGAMBANG NAIK TURUN (FLOATING EFFECT)
            float kecepatanMelayang = 0.025f;
            float jarakMelayang = 6f; // Jarak naik turun dikurangi agar tidak terlalu liar
            float offsetMelayangY = MathF.Sin((float)Main.time * kecepatanMelayang) * jarakMelayang;

            // Hitung posisi di layar
            Vector2 posisiDraw = Item.Center - Main.screenPosition + new Vector2(0f, offsetMelayangY);
            Vector2 originPusat = texture.Size() / 2f;

            // =========================================================================
            // BACKGROUND EFFECT (AURA) - DISESUAIKAN DENGAN SPRITE SEMI-TRANSPARAN
            // =========================================================================
            // Opacity di sini diturunkan menjadi 0.25f (25% Transparansi) agar tipis 
            // dan menyatu dengan keunikan sprite kamu tanpa menutupi bentuk aslinya.
            Color warnaAura = new Color(100, 180, 255, 0) * 0.25f; 
            
            // Menggambar aura tipis di belakang item
            for (int i = 0; i < 4; i++)
            {
                Vector2 offsetAura = Vector2.UnitX.RotatedBy(i * MathHelper.PiOver2) * 4f; // 4f ketebalan aura
                spriteBatch.Draw(texture, posisiDraw + offsetAura, null, warnaAura, rotation, originPusat, scale, SpriteEffects.None, 0f);
            }

            // 4. GAMBAR SPRITE UTAMA (Mendukung semi-transparansi bawaan file .png)
            spriteBatch.Draw(texture, posisiDraw, null, lightColor, rotation, originPusat, scale, SpriteEffects.None, 0f);

            // Return false agar sprite default tModLoader yang kaku tidak ikut tergambar
            return false;
        }
    }
}