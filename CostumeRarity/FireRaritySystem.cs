using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI.Chat;
using ReLogic.Content;

namespace BossIzh.Systems
{
    public class FireRaritySystem : GlobalItem
    {
        private static Asset<Texture2D> eagleSpriteAsset;

        public override void Load()
        {
            if (!Main.dedServ) 
            {
                eagleSpriteAsset = ModContent.Request<Texture2D>("TheSanity/CostumeRarity/MightyEagleEffect");
            }
        }

        private float GetConsistentRandom(int seed, float pengali)
        {
            return (float)(Math.Abs(Math.Sin(seed * pengali)) % 1f);
        }

        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int yOffset)
        {
            if (item.rare == ModContent.RarityType<TheSanity.CostumeRarity.MightyEagleRarity>() && line.Mod == "Terraria" && line.Name == "ItemName")
            {
                string text = line.Text;
                Vector2 posisiDasar = new Vector2(line.X, line.Y); 
                Vector2 textSize = line.Font.MeasureString(text);
                SpriteBatch spriteBatch = Main.spriteBatch;
                float waktu = Main.GlobalTimeWrappedHourly;
                Texture2D pixelTex = TextureAssets.MagicPixel.Value;

                // =========================================================================
                // CONFIG ANIMASI FRAME (6 Frame @ 60x60px)
                // =========================================================================
                int totalFrames = 6;       
                float kecepatanKepak = 18f; // Disesuaikan sedikit agar kepakan sayap pas dengan kecepatan baru

                // =========================================================================
                // A. LOGIKAL MIGHTY EAGLE (UI KANAN -> TABRAK NAMA -> LEDAKAN)
                // =========================================================================
                float durasiSiklus = 5.0f; // Total siklus dinaikkan ke 5 detik agar ada jeda diam yang pas
                float t = waktu % durasiSiklus; 

                // --- PENGATUR KECEPATAN TERBANG ---
                float durasiMelesat = 1.5f; // <--- SEKARANG JADI 1.5 DETIK (Lebih lambat & enjoy di mata)
                float durasiLedakan = 0.4f; 

                float startX = posisiDasar.X + 650f;             
                float endX = posisiDasar.X + (textSize.X / 2f);  
                float flyY = posisiDasar.Y + (textSize.Y * 0.25f); 
                Vector2 pusatTeks = new Vector2(endX, flyY);

                // --- FASE 1: ELANG MELESAT TERBANG ---
                if (t < durasiMelesat && eagleSpriteAsset != null)
                {
                    Texture2D eagleTex = eagleSpriteAsset.Value;
                    
                    int frameHeight = eagleTex.Height / totalFrames; 
                    int currentFrame = (int)(waktu * kecepatanKepak) % totalFrames;
                    Rectangle sourceRect = new Rectangle(0, currentFrame * frameHeight, eagleTex.Width, frameHeight);

                    float baseSkala = 0.85f; 
                    float progressMenerjang = t / durasiMelesat;

                    // Diubah ke linear (tanpa kuadrat) supaya kecepatannya stabil dari kanan ke kiri
                    float progressMelesatAlami = progressMenerjang;

                    float flyX = MathHelper.Lerp(startX, endX, progressMelesatAlami);
                    Vector2 posisiEagle = new Vector2(flyX, flyY);

                    // SQUASH & STRETCH: Diperhalus efek kemulurannya karena jalurnya sudah lambat
                    float stretchX = 1f + (progressMelesatAlami * 0.1f);
                    float squashY = 1f - (progressMelesatAlami * 0.04f);
                    Vector2 skalaFinal = new Vector2(baseSkala * stretchX, baseSkala * squashY) * line.BaseScale.X;

                    // PARTIKEL EKOR (TRAIL API)
                    for (int j = 1; j <= 4; j++)
                    {
                        float progressTrail = progressMelesatAlami - (j * 0.02f);
                        if (progressTrail >= 0f)
                        {
                            float trailX = MathHelper.Lerp(startX, endX, progressTrail);
                            Vector2 posisiTrail = new Vector2(trailX, flyY + Main.rand.NextFloat(-2f, 2f));
                            float skalaTrail = (1f - (j * 0.18f)) * 4.0f * line.BaseScale.X;
                            Color warnaTrail = Color.Lerp(Color.Yellow, Color.Red * 0f, j / 4f) * 0.5f;

                            spriteBatch.Draw(pixelTex, posisiTrail, new Rectangle(0, 0, 1, 1), warnaTrail, 0f, new Vector2(0.5f, 0.5f), skalaTrail, SpriteEffects.None, 0f);
                        }
                    }

                    SpriteEffects efekGambar = SpriteEffects.None; 
                    float opasitasEagle = 1f;
                    if (progressMenerjang < 0.08f) opasitasEagle = progressMenerjang / 0.08f;

                    Vector2 originEagle = new Vector2(eagleTex.Width / 2f, frameHeight / 2f);

                    // Gambar Elang dengan Potongan Frame Aktif 60x60
                    spriteBatch.Draw(
                        eagleTex, 
                        posisiEagle, 
                        sourceRect, 
                        Color.White * opasitasEagle, 
                        0f, 
                        originEagle, 
                        skalaFinal, 
                        efekGambar, 
                        0f
                    );
                }
                // --- FASE 2: BENTURAN & LEDAKAN HANCUR ---
                else if (t >= durasiMelesat && t < (durasiMelesat + durasiLedakan))
                {
                    float tLedakan = (t - durasiMelesat) / durasiLedakan; 

                    // 1. Shockwave Lingkaran
                    float skalaLedakan = tLedakan * 55f * line.BaseScale.X;
                    Color warnaLedakan = Color.Lerp(Color.White, Color.Orange * 0f, tLedakan);
                    spriteBatch.Draw(pixelTex, pusatTeks, new Rectangle(0, 0, 1, 1), warnaLedakan, tLedakan * 3f, new Vector2(0.5f, 0.5f), skalaLedakan, SpriteEffects.None, 0f);

                    // 2. 12 Serpihan Pecahan Api Melingkar
                    for (int p = 0; p < 12; p++)
                    {
                        float sudutPartikel = p * (MathHelper.TwoPi / 12f) + (waktu * 4f);
                        Vector2 offsetLedakan = new Vector2((float)Math.Cos(sudutPartikel), (float)Math.Sin(sudutPartikel)) * (tLedakan * 45f);
                        
                        float ukuranSerpihan = (1f - tLedakan) * 6f * line.BaseScale.X;
                        Color warnaSerpihan = Color.Lerp(Color.Yellow, Color.Red * 0f, tLedakan);

                        spriteBatch.Draw(pixelTex, pusatTeks + offsetLedakan, new Rectangle(0, 0, 1, 1), warnaSerpihan, sudutPartikel, new Vector2(0.5f, 0.5f), ukuranSerpihan, SpriteEffects.None, 0f);
                    }
                }

                // =========================================================================
                // B. PARTIKEL BACKGROUND API (DI TEKS NAMA)
                // =========================================================================
                int jumlahPartikel = 20;
                for (int i = 0; i < jumlahPartikel; i++)
                {
                    float rawX = (float)Math.Abs(Math.Sin(i * 23.45f)) % 1f;
                    float variasiKecepatan = 0.8f + (float)Math.Sin(i * 7.1f) * 0.4f;
                    float lifetime = (waktu * 1.4f * variasiKecepatan + (i * 0.13f)) % 1f;

                    float sway = (float)Math.Sin(waktu * 5f + i) * 4f;
                    float pX = posisiDasar.X + (rawX * textSize.X) + sway;
                    float pY = posisiDasar.Y + (textSize.Y * 0.6f) - (lifetime * 40f);

                    Vector2 posisiPartikel = new Vector2(pX, pY);
                    float ukuran = (1f - lifetime) * 3.5f * line.BaseScale.X;
                    if (ukuran < 0) ukuran = 0;

                    Color warnaPartikel = lifetime < 0.3f ? Color.Lerp(Color.Yellow, Color.Orange, lifetime / 0.3f) : Color.Lerp(Color.Orange, Color.Red * 0f, (lifetime - 0.3f) / 0.7f);
                    warnaPartikel *= (1f - lifetime) * 0.7f;

                    spriteBatch.Draw(pixelTex, posisiPartikel, new Rectangle(0, 0, 1, 1), warnaPartikel, i * 0.5f, new Vector2(0.5f, 0.5f), ukuran, SpriteEffects.None, 0f);
                }

                // =========================================================================
                // C. OUTLINE API BERGERAK MENGALIR
                // =========================================================================
                int totalTitikOutline = 8;
                for (int i = 0; i < totalTitikOutline; i++)
                {
                    float sudut = (i * (MathHelper.TwoPi / totalTitikOutline)) + (waktu * 5f);
                    float ketebalanAura = 2.0f + (float)Math.Sin(waktu * 10f + i) * 0.4f;
                    
                    Vector2 offsetAura = new Vector2((float)Math.Cos(sudut), (float)Math.Sin(sudut)) * ketebalanAura;
                    offsetAura.Y -= 0.5f;

                    Vector2 posisiOutlineApi = posisiDasar + offsetAura;
                    Color warnaAura = Color.Lerp(Color.Red, Color.Orange, (float)(Math.Sin(waktu * 4f + i) + 1f) / 2f) * 0.85f;

                    ChatManager.DrawColorCodedString(spriteBatch, line.Font, text, posisiOutlineApi, warnaAura, line.Rotation, line.Origin, line.BaseScale);
                }

                // =========================================================================
                // D. TEKS UTAMA (SOLID DIAM)
                // =========================================================================
                ChatManager.DrawColorCodedStringShadow(spriteBatch, line.Font, text, posisiDasar, Color.Black * 0.95f, line.Rotation, line.Origin, line.BaseScale);

                float transisiWarna = (float)(Math.Sin(waktu * 4f) + 1f) / 2f;
                Color warnaTeksDepan = Color.Lerp(new Color(255, 120, 0), new Color(255, 235, 0), transisiWarna);

                ChatManager.DrawColorCodedString(spriteBatch, line.Font, text, posisiDasar, warnaTeksDepan, line.Rotation, line.Origin, line.BaseScale);

                return false; 
            }

            return true;
        }
    }
}