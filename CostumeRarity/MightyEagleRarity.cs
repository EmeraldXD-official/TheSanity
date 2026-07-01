using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ModLoader;

namespace TheSanity.CostumeRarity
{
    public class MightyEagleRarity : ModRarity
    {
        public override Color RarityColor
        {
            get
            {
                // Kecepatan dikalikan 12f agar kedipannya secepat kobaran api asli
                float waktu = Main.GlobalTimeWrappedHourly * 12f; 
                
                // Trik Matematika Api: Menggabungkan Sinus dan Kosinus dengan frekuensi berbeda
                // Ini menciptakan efek 'keriut' acak (chaotic flicker) agar tidak terlihat seperti robot
                float keriutApi = (float)Math.Sin(waktu) * 0.5f + (float)Math.Cos(waktu * 2.5f) * 0.5f;
                
                // Satukan hasilnya ke rentang nilai aman 0f sampai 1f
                float progress = MathHelper.Clamp((keriutApi + 1f) / 2f, 0f, 1f);

                // Tiga kombinasi warna untuk menyusun gradasi kobaran api
                Color baraArang = new Color(130, 0, 0);     // Merah gelap (dasar bara)
                Color merahInti = new Color(255, 10, 0);    // Merah terang menyala (suhu panas)
                Color lidahApi  = new Color(255, 140, 0);   // Oranye kekuningan (ujung jilatan api)

                // Campur warna secara dinamis berdasarkan grafik keriut api
                if (progress < 0.4f)
                {
                    // Transisi dari merah gelap ke merah menyala
                    return Color.Lerp(baraArang, merahInti, progress / 0.4f);
                }
                else
                {
                    // Transisi dari merah menyala ke jilatan oranye hangat
                    return Color.Lerp(merahInti, lidahApi, (progress - 0.4f) / 0.6f);
                }
            }
        }
    }
}