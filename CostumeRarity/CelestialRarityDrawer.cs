using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace TheSanity.CostumeRarity
{
    public class CelestialRarityDrawer : GlobalItem
    {
        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int y)
        {
            // Cek apakah baris tooltip adalah Nama Item dan memiliki kelangkaan CelestialRarity
            if (line.Name == "ItemName" && item.rare == ModContent.RarityType<CelestialRarity>())
            {
                string teks = line.Text;

                // PRE-CALCULATION: Hitung dimensi teks per karakter demi akurasi animasi per baris
                float[] charWidths = new float[teks.Length];
                float[] charPositions = new float[teks.Length];
                float totalWidth = 0f;

                for (int i = 0; i < teks.Length; i++)
                {
                    charWidths[i] = line.Font.MeasureString(teks[i].ToString()).X * line.BaseScale.X;
                    charPositions[i] = totalWidth;
                    totalWidth += charWidths[i];
                }

                float totalHeight = line.Font.MeasureString(teks).Y * line.BaseScale.Y;
                Vector2 pusatTeks = new Vector2(line.X + totalWidth / 2f, line.Y + totalHeight / 2f);

                // =================================================================
                // TIMING SYSTEM (5 DETIK PER STAGE, TOTAL 20 DETIK SATU SIKLUS)
                // =================================================================
                float durasiPerStage = 5.0f;
                float totalWaktuSiklus = durasiPerStage * 4f; // 20 Detik total loop
                float waktuBerjalan = Main.GlobalTimeWrappedHourly % totalWaktuSiklus;

                int stageAktif = (int)(waktuBerjalan / durasiPerStage); // 0: Vortex, 1: Solar, 2: Nebula, 3: Stardust
                float progressStage = waktuBerjalan % durasiPerStage;    // 0.0f sampai 5.0f detik
                float rentangTransisi = 0.6f;                             // Durasi waktu efek transisi (0.6 detik terakhir)

                // =================================================================
                // KONFIGURASI PALET WARNA CELESTIAL STAGES
                // =================================================================
                // Warna Utama Isi Teks
                Color warnaVortexText = new Color(0, 240, 132);
                Color warnaSolarText = new Color(213, 86, 7);
                Color warnaNebulaText = new Color(194, 5, 146);
                Color warnaStardustText = new Color(63, 168, 238);

                // Warna Lapisan Border/Outline
                Color warnaVortexOut = new Color(0, 158, 86);
                Color warnaSolarOut = new Color(147, 60, 6);
                Color warnaNebulaOut = new Color(147, 6, 107);
                Color warnaStardustOut = new Color(13, 52, 191);

                // Warna Glow Premium (Versi Neon Super Cerah dari teks/border aktif)
                Color glowVortex = new Color(130, 255, 200);
                Color glowSolar = new Color(255, 160, 50);
                Color glowNebula = new Color(255, 100, 220);
                Color glowStardust = new Color(160, 225, 255);

                // Menentukan warna dasar frame berdasarkan stage aktif saat ini
                Color warnaTeksDasar = warnaVortexText;
                Color warnaBorderDasar = warnaVortexOut;
                Color warnaGlowAktif = glowVortex;

                if (stageAktif == 1) { warnaTeksDasar = warnaSolarText; warnaBorderDasar = warnaSolarOut; warnaGlowAktif = glowSolar; }
                else if (stageAktif == 2) { warnaTeksDasar = warnaNebulaText; warnaBorderDasar = warnaNebulaOut; warnaGlowAktif = glowNebula; }
                else if (stageAktif == 3) { warnaTeksDasar = warnaStardustText; warnaBorderDasar = warnaStardustOut; warnaGlowAktif = glowStardust; }

                // INTERPOLASI HALUS PERGANTIAN WARNA ANTAR STAGE (Kecuali Solar ke Nebula karena Glitch)
                if (progressStage > (durasiPerStage - rentangTransisi) && stageAktif != 1)
                {
                    float rasioBlend = (progressStage - (durasiPerStage - rentangTransisi)) / rentangTransisi;
                    Color targetText = warnaVortexText;
                    Color targetOut = warnaVortexOut;
                    Color targetGlow = glowVortex;

                    if (stageAktif == 0) { targetText = warnaSolarText; targetOut = warnaSolarOut; targetGlow = glowSolar; }
                    else if (stageAktif == 2) { targetText = warnaStardustText; targetOut = warnaStardustOut; targetGlow = glowStardust; }

                    warnaTeksDasar = Color.Lerp(warnaTeksDasar, targetText, rasioBlend);
                    warnaBorderDasar = Color.Lerp(warnaBorderDasar, targetOut, rasioBlend);
                    warnaGlowAktif = Color.Lerp(warnaGlowAktif, targetGlow, rasioBlend);
                }

                // =================================================================
                // PRE-CALCULATION LAYER EFEK & GELOMBANG GLIMMER SWEEP SWIPE
                // =================================================================
                float progresSapuGlimmer = (Main.GlobalTimeWrappedHourly * 0.7f) % 1.3f;
                Color[] finalTxtColors = new Color[teks.Length];
                Color[] finalOutColors = new Color[teks.Length];
                float[] charGlowFactors = new float[teks.Length];

                for (int i = 0; i < teks.Length; i++)
                {
                    float rasioHuruf = charPositions[i] / (totalWidth > 0 ? totalWidth : 1f);
                    float jarakKeGlimmer = Math.Abs(rasioHuruf - progresSapuGlimmer);
                    float rentangGlowLembut = 0.22f;

                    finalTxtColors[i] = warnaTeksDasar;
                    finalOutColors[i] = warnaBorderDasar;
                    charGlowFactors[i] = 0f;

                    // MEKANIK GLOWING ISI DAN BORDER (Ikut menyala super cerah saat disapu gelombang)
                    if (progresSapuGlimmer <= 1.0f && jarakKeGlimmer < rentangGlowLembut)
                    {
                        float intensitasKilau = 1f - (jarakKeGlimmer / rentangGlowLembut);
                        charGlowFactors[i] = intensitasKilau;

                        finalTxtColors[i] = Color.Lerp(finalTxtColors[i], warnaGlowAktif, intensitasKilau * 0.90f);
                        finalOutColors[i] = Color.Lerp(finalOutColors[i], warnaGlowAktif, intensitasKilau * 0.90f);
                    }
                }

                // =================================================================
                // BACKGROUND VISUAL EFFECT PER STAGE & TRANSISI KHUSUS
                // =================================================================
                
                // --- STAGE 1: VORTEX STAGE (Efek Petir Latar Belakang) ---
                if (stageAktif == 0)
                {
                    int seedPetir = (int)(Main.GlobalTimeWrappedHourly * 7f);
                    if (seedPetir % 3 == 0) // Menembakkan petir prosedural secara berkala
                    {
                        float petirX = line.X + (totalWidth * ((seedPetir * 17 % 100) / 100f));
                        Vector2 posAwalPetir = new Vector2(petirX, line.Y - 14f);
                        for (int s = 0; s < 4; s++)
                        {
                            Vector2 posAkhirPetir = posAwalPetir + new Vector2(MathF.Sin(seedPetir + s) * 10f, (totalHeight + 24f) / 4f);
                            float jarakSegmen = Vector2.Distance(posAwalPetir, posAkhirPetir);
                            float rotasiSegmen = MathF.Atan2(posAkhirPetir.Y - posAwalPetir.Y, posAkhirPetir.X - posAwalPetir.X);
                            
                            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, posAwalPetir, new Rectangle(0, 0, 1, 1), warnaVortexText * 0.35f, rotasiSegmen, Vector2.Zero, new Vector2(jarakSegmen, 1.5f), SpriteEffects.None, 0f);
                            posAwalPetir = posAkhirPetir;
                        }
                    }
                }

                // --- TRANSISI 1 -> 2: AURA MERAH MERAYAP NAIK ---
                if (stageAktif == 0 && progressStage > (durasiPerStage - rentangTransisi))
                {
                    float transisiProg = (progressStage - (durasiPerStage - rentangTransisi)) / rentangTransisi;
                    Color warnaAura = new Color(219, 95, 0, 0); // Transparan aditif
                    float tinggiAuraY = MathHelper.Lerp(line.Y + totalHeight + 10f, line.Y - 10f, transisiProg);

                    Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(line.X - 8f, tinggiAuraY), new Rectangle(0, 0, 1, 1), warnaAura * 0.4f, 0f, Vector2.Zero, new Vector2(totalWidth + 16f, (line.Y + totalHeight + 10f) - tinggiAuraY), SpriteEffects.None, 0f);
                }

                // --- STAGE 2: SOLAR STAGE (Efek Partikel Api Membara Naik) ---
                if (stageAktif == 1)
                {
                    int jumlahApi = 12;
                    for (int f = 0; f < jumlahApi; f++)
                    {
                        float rFactor = (f * 13.73f) % 1.0f;
                        float tSpeed = 0.4f + (f % 3) * 0.15f;
                        float tProg = (Main.GlobalTimeWrappedHourly * tSpeed + rFactor) % 1.0f;

                        float apiX = line.X + (totalWidth * rFactor) + MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + f) * 5f;
                        float apiY = (line.Y + totalHeight + 12f) - (tProg * (totalHeight + 24f));
                        float alphaApi = MathF.Sin(tProg * MathF.PI);

                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(apiX, apiY), new Rectangle(0, 0, 1, 1), warnaSolarText * alphaApi * 0.6f, tProg * 2f, new Vector2(0.5f, 0.5f), (2.5f + alphaApi * 1.5f) * line.BaseScale.X, SpriteEffects.None, 0f);
                    }
                }

                // --- STAGE 3: NEBULA STAGE (Partikel Mengorbit Mengelilingi Seluruh Teks) ---
                if (stageAktif == 2)
                {
                    int jumlahOrbit = 14;
                    for (int o = 0; o < jumlahOrbit; o++)
                    {
                        float sudut = Main.GlobalTimeWrappedHourly * 2.2f + (o * MathF.PI * 2f / jumlahOrbit);
                        float radiusX = totalWidth / 2f + 18f;
                        float radiusY = totalHeight / 2f + 8f;

                        Vector2 posisiOrbit = pusatTeks + new Vector2(MathF.Cos(sudut) * radiusX, MathF.Sin(sudut) * radiusY);
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, posisiOrbit, new Rectangle(0, 0, 1, 1), warnaNebulaText * 0.7f, sudut, new Vector2(0.5f, 0.5f), 3.0f * line.BaseScale.X, SpriteEffects.None, 0f);
                    }
                }

                // --- TRANSISI 3 -> 4: PARTIKEL CYAN MEMBELAH KE KANAN & KIRI ---
                if (stageAktif == 2 && progressStage > (durasiPerStage - rentangTransisi))
                {
                    float transisiProg = (progressStage - (durasiPerStage - rentangTransisi)) / rentangTransisi;
                    Color warnaCyanPartikel = new Color(46, 209, 255, 0);
                    float jarakSamping = (totalWidth / 2f + 25f) * transisiProg;

                    Vector2 posKiri = pusatTeks - new Vector2(jarakSamping, 0f);
                    Vector2 posKanan = pusatTeks + new Vector2(jarakSamping, 0f);

                    Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, posKiri, new Rectangle(0, 0, 1, 1), warnaCyanPartikel * 0.8f, Main.GlobalTimeWrappedHourly * 5f, new Vector2(0.5f, 0.5f), 6.0f * line.BaseScale.X, SpriteEffects.None, 0f);
                    Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, posKanan, new Rectangle(0, 0, 1, 1), warnaCyanPartikel * 0.8f, -Main.GlobalTimeWrappedHourly * 5f, new Vector2(0.5f, 0.5f), 6.0f * line.BaseScale.X, SpriteEffects.None, 0f);
                }

                // --- TRANSISI 4 -> 1: SAMBARAN PETIR CEPAT DARI KANAN KIRI ---
                if (stageAktif == 3 && progressStage > (durasiPerStage - rentangTransisi))
                {
                    float transisiProg = (progressStage - (durasiPerStage - rentangTransisi)) / rentangTransisi;
                    if ((int)(Main.GlobalTimeWrappedHourly * 15f) % 2 == 0)
                    {
                        // Sisi Kiri masuk ke tengah
                        float xKiri = MathHelper.Lerp(line.X - 30f, pusatTeks.X, transisiProg);
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(xKiri, pusatTeks.Y + MathF.Sin(xKiri) * 6f), new Rectangle(0, 0, 1, 1), glowVortex, 0.4f, Vector2.Zero, new Vector2(15f, 2f), SpriteEffects.None, 0f);
                        
                        // Sisi Kanan masuk ke tengah
                        float xKanan = MathHelper.Lerp(line.X + totalWidth + 30f, pusatTeks.X, transisiProg);
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(xKanan, pusatTeks.Y + MathF.Cos(xKanan) * 6f), new Rectangle(0, 0, 1, 1), glowVortex, -0.4f, Vector2.Zero, new Vector2(-15f, 2f), SpriteEffects.None, 0f);
                    }
                }

                // =================================================================
                // RENDER OUTLINE BORDER SOLID (8 ARAH MATA ANGIN) & TEXT UTAMA
                // =================================================================
                for (int i = 0; i < teks.Length; i++)
                {
                    string karakter = teks[i].ToString();
                    Vector2 rawCharSize = line.Font.MeasureString(karakter);
                    Vector2 baseOrigin = rawCharSize / 2f;

                    // POSISI ANIMASI DEFAULT / IDLE OFFSET PER HURUF
                    Vector2 finalCharOffset = Vector2.Zero;

                    // --- TRANSISI 2 -> 3: MODIFIER EFFECT GLITCH JUMP (SOLAR TO NEBULA) ---
                    if (stageAktif == 1 && progressStage > (durasiPerStage - rentangTransisi))
                    {
                        float glitchProg = (progressStage - (durasiPerStage - rentangTransisi)) / rentangTransisi;
                        
                        // Efek Getar Frantic & Loncat Atas-Bawah Progresif
                        float seedGlitch = Main.GlobalTimeWrappedHourly * 90f + i;
                        finalCharOffset.X = MathF.Sin(seedGlitch) * 4.5f;
                        finalCharOffset.Y = MathHelper.Lerp(-14f, 14f, glitchProg) + MathF.Cos(seedGlitch * 1.5f) * 4f;

                        // Efek Kedip Warna saat Glitch Hancur
                        if ((int)(Main.GlobalTimeWrappedHourly * 25f) % 2 == 0)
                        {
                            finalTxtColors[i] = warnaNebulaText;
                            finalOutColors[i] = warnaNebulaOut;
                        }
                    }

                    // --- STAGE 4: STARDUST STAGE (Idle Animation Teks Loncat Berurutan) ---
                    if (stageAktif == 3 && progressStage <= (durasiPerStage - rentangTransisi))
                    {
                        // Membuat gelombang loncat sekuensial mengalir dari tengah keluar menuju ujung kanan-kiri
                        float jarakDariPusat = Math.Abs(charPositions[i] - totalWidth / 2f);
                        float gelombangLoncat = MathF.Sin(Main.GlobalTimeWrappedHourly * 6.5f - (jarakDariPusat * 0.04f));
                        
                        if (gelombangLoncat > 0f)
                        {
                            finalCharOffset.Y = -gelombangLoncat * 6.5f * line.BaseScale.Y;
                        }
                    }

                    Vector2 posisiKarakterFix = new Vector2(line.X + charPositions[i], line.Y) + (baseOrigin * line.BaseScale) + finalCharOffset;

                    // 1. RENDERING LAYER ADITIF GLOW NEON (Membesar halus di belakang jika disapu glimmer)
                    if (charGlowFactors[i] > 0f)
                    {
                        Color warnaNeonBloom = finalOutColors[i] * (0.25f + charGlowFactors[i] * 0.35f);
                        warnaNeonBloom.A = 0;
                        float skalaExtraGlow = 1.4f + (charGlowFactors[i] * 0.3f);

                        ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, karakter, posisiKarakterFix, warnaNeonBloom, line.Rotation, baseOrigin, line.BaseScale * skalaExtraGlow);
                    }

                    // 2. RENDERING SOLID OUTLINE BORDER TEBAL (8 PENJUURU)
                    Color warnaOutlineFix = finalOutColors[i];
                    warnaOutlineFix.A = 255;

                    Vector2[] offsetsMataAngin = new Vector2[] {
                        new Vector2(-1.5f, 0f),    new Vector2(1.5f, 0f),
                        new Vector2(0f, -1.5f),    new Vector2(0f, 1.5f),
                        new Vector2(-1.2f, -1.2f), new Vector2(1.2f, -1.2f),
                        new Vector2(-1.2f, 1.2f),  new Vector2(1.2f, 1.2f)
                    };

                    foreach (Vector2 offset in offsetsMataAngin)
                    {
                        ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, karakter, posisiKarakterFix + offset, warnaOutlineFix, line.Rotation, baseOrigin, line.BaseScale);
                    }

                    // 3. RENDERING LAYER UTAMA ISIAN FOREGROUND TEKS
                    ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, karakter, posisiKarakterFix, finalTxtColors[i], line.Rotation, baseOrigin, line.BaseScale);
                }

                return false; // Mematikan engine gambar standard vanilla Terraria
            }

            return true;
        }
    }
}