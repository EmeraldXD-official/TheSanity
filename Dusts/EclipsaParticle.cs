using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.Audio; 

namespace TheSanity.Dusts
{
    public class EclipsaParticle : ModDust
    {
        public override string Texture => "TheSanity/Dusts/EclipsaParticle";

        public override void OnSpawn(Dust dust)
        {
            dust.noGravity = true;
            dust.alpha = 50;
            dust.frame = new Rectangle(0, 0, 50, 50);
            dust.scale = 0.3f;
            
        }

        public override bool Update(Dust dust)
        {
            dust.position += dust.velocity;
            dust.scale *= 0.98f;

            // GANTI DENGAN INI: Gunakan 'dust.customData' untuk menyimpan counter
            // Karena Dust tidak punya frameCounter bawaan
            if (dust.customData == null) dust.customData = 0;
            int counter = (int)dust.customData;
            counter++;

            if (counter >= 5) 
            {
                counter = 0;
                dust.frame.Y += 50;
                if (dust.frame.Y >= 300) dust.frame.Y = 0;
            }
            
            dust.customData = counter; // Simpan kembali nilainya

            if (dust.scale < 0.2f) dust.active = false;
            return false;
        }
    }
}