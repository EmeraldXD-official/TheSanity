using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.UI.Chat;

namespace TheSanity.CostumeRarity
{
    public class SanityRarityDrawer : GlobalItem
    {
        // Deklarasi 8 arah mata angin untuk membuat outline border yang tebal dan halus
        private static readonly Vector2[] offsetsMataAngin = new Vector2[]
        {
            new Vector2(0, -1),   // Utara
            new Vector2(1, -1),  // Timur Laut
            new Vector2(1, 0),   // Timur
            new Vector2(1, 1),   // Tenggara
            new Vector2(0, 1),   // Selatan
            new Vector2(-1, 1),  // Barat Daya
            new Vector2(-1, 0),  // Barat
            new Vector2(-1, -1)  // Barat Laut
        };

        public override bool PreDrawTooltipLine(Item item, DrawableTooltipLine line, ref int y)
        {
            if (line.Name == "ItemName" && item.rare == ModContent.RarityType<SanityRarity>())
            {
                string teks = line.Text;

                // =================================================================
                // 0. PRE-CALCULATION MATRIKS DIMENSI HURUF & STRUKTUR WAKTU
                // =================================================================
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

                // TIMING MANAGEMENT: 5 Stage x 5 Detik = 25 Detik Total Siklus Loop
                float durasiPerStage = 5.0f;
                float totalWaktuSiklus = durasiPerStage * 5f;
                float waktuBerjalan = Main.GlobalTimeWrappedHourly % totalWaktuSiklus;

                int stageAktif = (int)(waktuBerjalan / durasiPerStage); // 0: Ocean, 1: Sunset/Noon, 2: Blood, 3: Leaf, 4: Fall to Reality
                float progressStage = waktuBerjalan % durasiPerStage;
                float rentangTransisi = 0.8f; 

                // DEKLARASI PALET WARNA DASAR
                Color txtOcean = new Color(18, 66, 242);     Color outOcean = new Color(12, 46, 166);
                Color txtNoon = new Color(247, 243, 18);      Color outNoon = new Color(177, 174, 7); 
                Color txtBlood = new Color(211, 19, 9);       Color outBlood = new Color(141, 13, 7);
                Color txtLeaf = new Color(26, 240, 52);       Color outLeaf = new Color(9, 144, 25);
                Color txtFall = new Color(223, 152, 6);       Color outFall = new Color(142, 97, 6);

                Color glowOcean = new Color(100, 180, 255);
                Color glowNoon = new Color(255, 230, 100);
                Color glowBlood = new Color(255, 40, 30);
                Color glowLeaf = new Color(130, 255, 160);
                Color glowFall = new Color(255, 255, 255); 

                Color warnaTeksDasar = txtOcean;
                Color warnaBorderDasar = outOcean;
                Color warnaGlowAktif = glowOcean;

                if (stageAktif == 1) { warnaTeksDasar = txtNoon; warnaBorderDasar = outNoon; warnaGlowAktif = glowNoon; }
                else if (stageAktif == 2) { warnaTeksDasar = txtBlood; warnaBorderDasar = outBlood; warnaGlowAktif = glowBlood; }
                else if (stageAktif == 3) { warnaTeksDasar = txtLeaf; warnaBorderDasar = outLeaf; warnaGlowAktif = glowLeaf; }
                else if (stageAktif == 4) { warnaTeksDasar = txtFall; warnaBorderDasar = outFall; warnaGlowAktif = glowFall; }

                // Lerp interpolasi warna text saat transisi normal
                if (progressStage > (durasiPerStage - rentangTransisi))
                {
                    float rasioBlend = (progressStage - (durasiPerStage - rentangTransisi)) / rentangTransisi;
                    Color targetText = txtOcean; Color targetOut = outOcean; Color targetGlow = glowOcean;

                    if (stageAktif == 0) { targetText = txtNoon; targetOut = outNoon; targetGlow = glowNoon; }
                    else if (stageAktif == 1) { targetText = txtBlood; targetOut = outBlood; targetGlow = glowBlood; }
                    else if (stageAktif == 2) { targetText = txtLeaf; targetOut = outLeaf; targetGlow = glowLeaf; }
                    else if (stageAktif == 3) { targetText = txtFall; targetOut = outFall; targetGlow = glowFall; }

                    warnaTeksDasar = Color.Lerp(warnaTeksDasar, targetText, rasioBlend);
                    warnaBorderDasar = Color.Lerp(warnaBorderDasar, targetOut, rasioBlend);
                    warnaGlowAktif = Color.Lerp(warnaGlowAktif, targetGlow, rasioBlend);
                }

                // =================================================================
                // 1. GLOWING SWEEP EFFECT SYSTEM (Kiri -> Kanan)
                // =================================================================
                float progresSapuGlimmer = (Main.GlobalTimeWrappedHourly * 0.75f) % 1.4f;
                Color[] finalTxtColors = new Color[teks.Length];
                Color[] finalOutColors = new Color[teks.Length];
                float[] charGlowFactors = new float[teks.Length];

                for (int i = 0; i < teks.Length; i++)
                {
                    float rasioHuruf = charPositions[i] / (totalWidth > 0 ? totalWidth : 1f);
                    float jarakKeGlimmer = Math.Abs(rasioHuruf - progresSapuGlimmer);
                    float rentangGlowLembut = 0.23f;

                    finalTxtColors[i] = warnaTeksDasar;
                    finalOutColors[i] = warnaBorderDasar;
                    charGlowFactors[i] = 0f;

                    if (progresSapuGlimmer <= 1.1f && jarakKeGlimmer < rentangGlowLembut)
                    {
                        float intensitasKilau = 1f - (jarakKeGlimmer / rentangGlowLembut);
                        charGlowFactors[i] = intensitasKilau;

                        finalTxtColors[i] = Color.Lerp(finalTxtColors[i], warnaGlowAktif, intensitasKilau * 0.90f);
                        finalOutColors[i] = Color.Lerp(finalOutColors[i], warnaGlowAktif, intensitasKilau * 0.90f);
                    }
                }

                Vector2[] finalOffsetsHuruf = new Vector2[teks.Length];
                float detakJantungSiklus = 0f; // Penyimpan efek detak jantung Stage 2

                // =================================================================
                // 2. LAYER AMBIENT BACKGROUND EFFECTS & FIXED STAGES
                // =================================================================
                
                // --- STAGE 0: DEEP OCEAN ---
                if (stageAktif == 0)
                {
                    float waktuAir = Main.GlobalTimeWrappedHourly * 6f;
                    float tinggiGarisAir = line.Y + totalHeight + 2f;

                    for (int i = 0; i < teks.Length; i++)
                    {
                        finalOffsetsHuruf[i].Y = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f + i * 0.4f) * 2.5f;
                    }

                    for (float xPos = 0; xPos < totalWidth + 10f; xPos += 2f)
                    {
                        float sinWave = MathF.Sin(waktuAir + (xPos * 0.1f)) * 3f;
                        Vector2 posAir = new Vector2(line.X - 5f + xPos, tinggiGarisAir + sinWave);
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, posAir, new Rectangle(0, 0, 1, 1), outOcean * 0.7f, 0f, Vector2.Zero, new Vector2(2f, 8f), SpriteEffects.None, 0f);
                    }
                }

                // --- STAGE 1: SUNSET / NOON (FIXED: Tanpa getaran Heat, Teks Bergelombang, Bintik Matahari) ---
                if (stageAktif == 1)
                {
                    Vector2 posMatahari = pusatTeks;
                    Color warnaMatahari = new Color(240, 130, 10);

                    // Gambar Inti Matahari Tengah
                    Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, posMatahari, new Rectangle(0, 0, 1, 1), warnaMatahari * 0.25f, Main.GlobalTimeWrappedHourly * 0.2f, new Vector2(0.5f, 0.5f), new Vector2(24f, 24f), SpriteEffects.None, 0f);

                    // Efek Gelombang Sinar Surya Luar
                    float pulseProg = (Main.GlobalTimeWrappedHourly * 1.3f) % 1.0f;
                    Color waveColor = warnaMatahari * (1f - pulseProg) * 0.3f;
                    waveColor.A = 0;

                    for (float r = 0; r < MathF.PI * 2f; r += 0.5f)
                    {
                        Vector2 targetArah = new Vector2(MathF.Cos(r), MathF.Sin(r));
                        Vector2 posSinar = posMatahari + targetArah * (pulseProg * 45f);
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, posSinar, new Rectangle(0, 0, 1, 1), waveColor, r, new Vector2(0.5f, 0.5f), new Vector2(5.5f, 1.5f), SpriteEffects.None, 0f);
                    }

                    // FIX TAMBAHAN: Efek bintik-bintik kecil matahari mengorbit mengelilingi inti
                    int jumlahBintik = 12;
                    for (int b = 0; b < jumlahBintik; b++)
                    {
                        float seedB = b * 45.32f;
                        float jarakBintik = ((seedB + Main.GlobalTimeWrappedHourly * 12f) % 32f);
                        float sudutBintik = seedB + Main.GlobalTimeWrappedHourly * 0.7f;
                        Vector2 posBintik = posMatahari + new Vector2(MathF.Cos(sudutBintik), MathF.Sin(sudutBintik)) * jarakBintik;
                        float alphaBintik = MathF.Sin((jarakBintik / 32f) * MathF.PI);
                        
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, posBintik, new Rectangle(0, 0, 1, 1), txtNoon * alphaBintik * 0.65f, 0f, new Vector2(0.5f, 0.5f), 2f, SpriteEffects.None, 0f);
                    }

                    // FIX: Tetap bergelombang naik turun secara estetik tanpa jitter/patah-patah
                    for (int i = 0; i < teks.Length; i++)
                    {
                        finalOffsetsHuruf[i].Y = MathF.Sin(Main.GlobalTimeWrappedHourly * 3.2f + i * 0.45f) * 2.8f * line.BaseScale.Y;
                        finalOffsetsHuruf[i].X = 0f; 
                    }
                }

                // --- STAGE 2: THE BLOOD REALITY (FIXED: Detak Jantung Dinamis Irregular 1x, 2x, 3x) ---
                if (stageAktif == 2)
                {
                    // Algoritma Pola Detak Jantung Menggunakan Pembagi Sisa Waktu Siklus
                    float pengukurWaktuDetak = Main.GlobalTimeWrappedHourly * 1.4f; 
                    int jenisSiklusDetak = (int)(pengukurWaktuDetak % 3); // Hasilnya berputar 0 (1 detak), 1 (2 detak), 2 (3 detak)
                    float fraksiDetak = pengukurWaktuDetak % 1f;

                    if (jenisSiklusDetak == 0) // Pola 1x Detakan Tunggal Kuat
                    {
                        if (fraksiDetak < 0.25f)
                            detakJantungSiklus = MathF.Sin((fraksiDetak / 0.25f) * MathF.PI) * 0.22f;
                    }
                    else if (jenisSiklusDetak == 1) // Pola 2x Detakan Ganda (Lub-Dub)
                    {
                        if (fraksiDetak < 0.22f)
                            detakJantungSiklus = MathF.Sin((fraksiDetak / 0.22f) * MathF.PI) * 0.22f;
                        else if (fraksiDetak > 0.32f && fraksiDetak < 0.54f)
                            detakJantungSiklus = MathF.Sin(((fraksiDetak - 0.32f) / 0.22f) * MathF.PI) * 0.15f;
                    }
                    else // Pola 3x Detakan Beruntun Cepat
                    {
                        if (fraksiDetak < 0.18f)
                            detakJantungSiklus = MathF.Sin((fraksiDetak / 0.18f) * MathF.PI) * 0.22f;
                        else if (fraksiDetak > 0.24f && fraksiDetak < 0.42f)
                            detakJantungSiklus = MathF.Sin(((fraksiDetak - 0.24f) / 0.18f) * MathF.PI) * 0.16f;
                        else if (fraksiDetak > 0.48f && fraksiDetak < 0.66f)
                            detakJantungSiklus = MathF.Sin(((fraksiDetak - 0.48f) / 0.18f) * MathF.PI) * 0.11f;
                    }

                    // Efek darah menetes & cipratan bawaan
                    int jumlahTetesan = 14;
                    for (int d = 0; d < jumlahTetesan; d++)
                    {
                        float seedD = d * 19.84f;
                        float dropProg = (Main.GlobalTimeWrappedHourly * 0.75f + seedD) % 1.0f;
                        float alphaDrop = MathF.Sin(dropProg * MathF.PI);
                        float dropX = line.X + (totalWidth * ((seedD * 23f % 100) / 100f));
                        float dropY = (line.Y + totalHeight * 0.4f) + (dropProg * 28f);
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(dropX, dropY), new Rectangle(0, 0, 1, 1), new Color(150, 0, 0) * alphaDrop * 0.85f, 0f, Vector2.Zero, new Vector2(1.5f, 5.0f) * line.BaseScale.X, SpriteEffects.None, 0f);
                    }

                    int jumlahSplatters = 10;
                    for (int s = 0; s < jumlahSplatters; s++)
                    {
                        float seedS = s + (s * 17.31f); 
                        float intervalSplat = (Main.GlobalTimeWrappedHourly * 0.9f + seedS) % 1.2f;
                        float alphaSplat = MathF.Sin(intervalSplat * MathF.PI);
                        float splatX = line.X + (totalWidth * ((seedS * 73f % 100) / 100f));
                        float splatY = line.Y + (totalHeight * ((seedS * 37f % 100) / 100f)) - 2f;
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(splatX, splatY), new Rectangle(0, 0, 1, 1), txtBlood * alphaSplat * 0.75f, intervalSplat * 5f, new Vector2(0.5f, 0.5f), (2.5f + intervalSplat * 3.5f) * line.BaseScale.X, SpriteEffects.None, 0f);
                    }
                }

                // --- STAGE 3: THE HOPELESS LEAF (FIXED: Goyang Kanan Kiri Serempak) ---
                if (stageAktif == 3)
                {
                    int jumlahDaunVortex = 8;
                    for (int l = 0; l < jumlahDaunVortex; l++)
                    {
                        float sudut = Main.GlobalTimeWrappedHourly * 2.4f + (l * MathF.PI * 2f / jumlahDaunVortex);
                        float radX = totalWidth / 2f + 14f;
                        float radY = totalHeight / 2f + 5f;
                        Vector2 posOrbitDaun = pusatTeks + new Vector2(MathF.Cos(sudut) * radX, MathF.Sin(sudut) * radY * 0.6f);
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, posOrbitDaun, new Rectangle(0, 0, 1, 1), txtLeaf * 0.5f, sudut, new Vector2(0.5f, 0.5f), new Vector2(4.5f, 2f) * line.BaseScale.X, SpriteEffects.None, 0f);
                    }

                    int jumlahDaun = 6;
                    for (int l = 0; l < jumlahDaun; l++)
                    {
                        float seedL = (l * 23.45f);
                        float tDaun = (Main.GlobalTimeWrappedHourly * 0.7f + seedL) % 1.5f;
                        float dX = line.X + (totalWidth * 0.5f) + MathF.Sin(tDaun * 4f + seedL) * 20f;
                        float dY = (line.Y - 20f) + (tDaun * (totalHeight + 35f));
                        float alphaDaun = MathF.Sin((tDaun / 1.5f) * MathF.PI);
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(dX, dY), new Rectangle(0, 0, 1, 1), txtLeaf * alphaDaun * 0.6f, tDaun * 5f, new Vector2(0.5f, 0.5f), new Vector2(4f, 2f) * line.BaseScale.X, SpriteEffects.None, 0f);
                    }

                    // FIX: Seluruh komponen teks bergoyang stabil ke kanan dan kiri layaknya terhempas angin
                    float goyanganKananKiri = MathF.Sin(Main.GlobalTimeWrappedHourly * 3.5f) * 4.5f * line.BaseScale.X;
                    for (int i = 0; i < teks.Length; i++)
                    {
                        finalOffsetsHuruf[i].X = goyanganKananKiri;
                        finalOffsetsHuruf[i].Y = 0f;
                    }
                }

                // --- STAGE 4: FALL OF THE REALITY ---
                bool petirMenyambar = false;
                if (stageAktif == 4)
                {
                    Color ambientGelap = new Color(0, 0, 0, 120);
                    Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(line.X - 20f, line.Y - 15f), new Rectangle(0, 0, 1, 1), ambientGelap, 0f, Vector2.Zero, new Vector2(totalWidth + 40f, totalHeight + 30f), SpriteEffects.None, 0f);

                    int jumlahHujanMencekam = 15;
                    for (int p = 0; p < jumlahHujanMencekam; p++)
                    {
                        float seedP = p * 27.51f;
                        float hProg = (Main.GlobalTimeWrappedHourly * 1.1f + seedP) % 1.0f;
                        float alphaHujan = MathF.Sin(hProg * MathF.PI);
                        float hujanX = line.X + (totalWidth * ((seedP * 17f % 100) / 100f));
                        float hujanY = (line.Y - 16f) + (hProg * (totalHeight + 28f));
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(hujanX, hujanY), new Rectangle(0, 0, 1, 1), Color.White * alphaHujan * 0.65f, 0f, Vector2.Zero, 1.8f * line.BaseScale.X, SpriteEffects.None, 0f);
                    }

                    int seedPetir = (int)(Main.GlobalTimeWrappedHourly * 6.5f);
                    if (seedPetir % 4 == 0) 
                    {
                        petirMenyambar = true; 
                        float petirX = line.X + (totalWidth * ((seedPetir * 29 % 100) / 100f));
                        Vector2 posAwalPetir = new Vector2(petirX, line.Y - 18f);

                        for (int s = 0; s < 4; s++)
                        {
                            Vector2 posAkhirPetir = posAwalPetir + new Vector2(MathF.Sin(seedPetir + s) * 11f, (totalHeight + 28f) / 4f);
                            float jarakSegmen = Vector2.Distance(posAwalPetir, posAkhirPetir);
                            float rotasiSegmen = MathF.Atan2(posAkhirPetir.Y - posAwalPetir.Y, posAkhirPetir.X - posAwalPetir.X);
                            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, posAwalPetir, new Rectangle(0, 0, 1, 1), glowFall * 0.7f, rotasiSegmen, Vector2.Zero, new Vector2(jarakSegmen, 1.8f), SpriteEffects.None, 0f);
                            posAwalPetir = posAkhirPetir;
                        }
                    }

                    int daunAbuAbu = 5;
                    for (int g = 0; g < daunAbuAbu; g++)
                    {
                        float seedG = (g * 31.82f);
                        float tGrey = (Main.GlobalTimeWrappedHourly * 0.9f + seedG) % 1.3f;
                        float gX = line.X - 10f + (totalWidth + 20f) * ((seedG * 41f % 100) / 100f) + MathF.Sin(tGrey * 3f) * 8f;
                        float gY = (line.Y - 15f) + (tGrey * (totalHeight + 25f));
                        Color warnaDaunAbu = new Color(170, 170, 170) * MathF.Sin((tGrey / 1.3f) * MathF.PI) * 0.4f;
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(gX, gY), new Rectangle(0, 0, 1, 1), warnaDaunAbu, tGrey * 2f, new Vector2(0.5f, 0.5f), new Vector2(3.5f, 1.8f), SpriteEffects.None, 0f);
                    }
                }

                // =================================================================
                // 3. TIMING LAYER TRANSISI KHUSUS
                // =================================================================
                if (progressStage > (durasiPerStage - rentangTransisi))
                {
                    float progTransisi = (progressStage - (durasiPerStage - rentangTransisi)) / rentangTransisi;

                    if (stageAktif == 0) // Ocean -> Noon
                    {
                        Color sinarKuning = new Color(243, 251, 4, 0);
                        float skalaSinar = progTransisi * (totalWidth + 30f);
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, pusatTeks, new Rectangle(0, 0, 1, 1), sinarKuning * (1f - progTransisi), 0f, new Vector2(0.5f, 0.5f), new Vector2(skalaSinar, 3f * line.BaseScale.Y), SpriteEffects.None, 0f);
                    }
                    else if (stageAktif == 1) // Noon -> Blood
                    {
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(line.X - 15f, line.Y - 10f), new Rectangle(0, 0, 1, 1), Color.Black * progTransisi * 0.6f, 0f, Vector2.Zero, new Vector2(totalWidth + 30f, totalHeight + 20f), SpriteEffects.None, 0f);
                        float tinggiDarah = MathHelper.Lerp(line.Y + totalHeight + 15f, line.Y - 20f, progTransisi);
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(line.X - 15f, tinggiDarah), new Rectangle(0, 0, 1, 1), outBlood * 0.75f, 0f, Vector2.Zero, new Vector2(totalWidth + 30f, (line.Y + totalHeight + 15f) - tinggiDarah), SpriteEffects.None, 0f);
                    }
                    else if (stageAktif == 2) // Blood -> Leaf (FIXED: Daun masuk horizontal dari Pinggir Kiri & Kanan ke Tengah)
                    {
                        Color warnaDaunTrans = new Color(14, 175, 31, 0);
                        int kelebatanDaun = 26;
                        for (int d = 0; d < kelebatanDaun; d++)
                        {
                            float rSeed = d * 8.54f;
                            float localProg = (progTransisi + (rSeed % 0.25f)) % 1.0f;
                            
                            float daunX;
                            if (d % 2 == 0) // Setengah jumlah daun datang dari SISI KIRI menuju tengah
                            {
                                daunX = MathHelper.Lerp(line.X - 45f, line.X + totalWidth / 2f, localProg);
                            }
                            else // Setengah jumlah daun datang dari SISI KANAN menuju tengah
                            {
                                daunX = MathHelper.Lerp(line.X + totalWidth + 45f, line.X + totalWidth / 2f, localProg);
                            }

                            // Ketinggian daun sejajar garis horizontal tengah teks dengan sedikit efek wave kosinus
                            float daunY = line.Y + (totalHeight * 0.5f) + MathF.Cos(localProg * 6f + rSeed) * 6f;

                            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(daunX, daunY), new Rectangle(0, 0, 1, 1), warnaDaunTrans * MathF.Sin(localProg * MathF.PI), localProg * 5f, new Vector2(0.5f, 0.5f), new Vector2(5.5f, 2f) * line.BaseScale.X, SpriteEffects.None, 0f);
                        }
                    }
                    else if (stageAktif == 3) // Leaf -> Fall to Reality
                    {
                        int partikelPutih = 15;
                        for (int p = 0; p < partikelPutih; p++)
                        {
                            float pSeed = p * 13.9f;
                            float pX = line.X + (totalWidth * ((pSeed * 19f % 100) / 100f));
                            float targetY = (line.Y + totalHeight) - (MathF.Sin(progTransisi * MathF.PI) * 25f) + (progTransisi * 10f);
                            Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, new Vector2(pX, targetY), new Rectangle(0, 0, 1, 1), Color.White * MathF.Sin(progTransisi * MathF.PI), 0f, new Vector2(0.5f, 0.5f), 2.5f * line.BaseScale.X, SpriteEffects.None, 0f);
                        }
                    }
                    else if (stageAktif == 4) // Fall to Reality -> Ocean Loop (FIXED: DIGUYUR TSUNAMI DARI ATAS KE BAWAH)
                    {
                        // Menghitung posisi koordinat Y hantaman ombak raksasa
                        float batasAtasTsunami = line.Y - 25f;
                        float batasBawahTsunami = line.Y + totalHeight + 25f;
                        float posisiYOmbakArahBawah = MathHelper.Lerp(batasAtasTsunami, batasBawahTsunami, progTransisi);

                        // 1. Gambar Massa Air Belakang Tsunami (mengisi bidang atas yang sudah terlewati ombak)
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, 
                            new Vector2(line.X - 25f, batasAtasTsunami), 
                            new Rectangle(0, 0, 1, 1), 
                            outOcean * (progTransisi * 0.65f), 
                            0f, Vector2.Zero, new Vector2(totalWidth + 50f, posisiYOmbakArahBawah - batasAtasTsunami), SpriteEffects.None, 0f);

                        // 2. Gambar Kepala Ombak Tsunami Tebal (Crest Wave Front) di baris depan hantaman air
                        Main.spriteBatch.Draw(TextureAssets.MagicPixel.Value, 
                            new Vector2(line.X - 25f, posisiYOmbakArahBawah), 
                            new Rectangle(0, 0, 1, 1), 
                            txtOcean * MathF.Sin(progTransisi * MathF.PI), 
                            0f, Vector2.Zero, new Vector2(totalWidth + 50f, 14f * line.BaseScale.Y), SpriteEffects.None, 0f);
                    }
                }

                // =================================================================
                // 4. LAYER RENDER AKHIR OUTLINE TEBAL & FOREGROUND TEXT UTAMA
                // =================================================================
                for (int i = 0; i < teks.Length; i++)
                {
                    string karakter = teks[i].ToString();
                    Vector2 rawCharSize = line.Font.MeasureString(karakter);
                    Vector2 baseOrigin = rawCharSize / 2f;

                    Vector2 offsetTambahan = finalOffsetsHuruf[i];
                    if (stageAktif == 4 && petirMenyambar)
                    {
                        float waveLoncat = MathF.Sin(((Main.GlobalTimeWrappedHourly * 6.5f) % 1f) * MathF.PI);
                        if (waveLoncat > 0f)
                        {
                            offsetTambahan.Y -= 6.5f * waveLoncat * line.BaseScale.Y;
                        }
                    }

                    Vector2 posisiKarakterFix = new Vector2(line.X + charPositions[i], line.Y) + (baseOrigin * line.BaseScale) + offsetTambahan;

                    // FIX SIKLUS DETAK: Modifikasi skala dasar text berdasarkan variabel detak jantung dinamis Stage 2
                    Vector2 skalaFinalKarakter = line.BaseScale;
                    if (stageAktif == 2)
                    {
                        skalaFinalKarakter *= (1f + detakJantungSiklus);
                    }

                    // A. INSPIRASI CELESTIAL: RENDER LAPISAN ADITIF GLOW
                    if (charGlowFactors[i] > 0f)
                    {
                        Color warnaNeonBloom = finalOutColors[i] * (0.15f + charGlowFactors[i] * 0.45f);
                        warnaNeonBloom.A = 0; 
                        float skalaExtraGlow = 1.3f + (charGlowFactors[i] * 0.25f);

                        ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, karakter, posisiKarakterFix, warnaNeonBloom, line.Rotation, baseOrigin, skalaFinalKarakter * skalaExtraGlow);
                    }

                    // B. RENDER SOLID OUTLINE BORDER 8 ARAH MATA ANGIN
                    Color warnaOutlineFix = finalOutColors[i];
                    warnaOutlineFix.A = 255;

                    foreach (Vector2 offset in offsetsMataAngin)
                    {
                        ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, karakter, posisiKarakterFix + offset, warnaOutlineFix, line.Rotation, baseOrigin, skalaFinalKarakter);
                    }

                    // C. RENDER UTAMA ISI FOREGROUND TEKS PALING DEPAN
                    ChatManager.DrawColorCodedString(Main.spriteBatch, line.Font, karakter, posisiKarakterFix, finalTxtColors[i], line.Rotation, baseOrigin, skalaFinalKarakter);
                }

                return false; 
            }

            return true;
        }
    }
}