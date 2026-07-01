using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class ShadowflameReworkPlayer : ModPlayer
    {
        public bool shadowflameCustom = false;

        public override void ResetEffects()
        {
            shadowflameCustom = false;
        }

        public override void UpdateLifeRegen()
        {
            if (Player.HasBuff(BuffID.ShadowFlame))
            {
                shadowflameCustom = true;

                // =========================================================================
                // CONFIG: REWORK DAMAGE PER SECOND (DAMAGE & SPEED LOCATION)
                // =========================================================================
                // Nilai -10 berarti player akan terkena damage sebesar 5 HP per detik 
                // (Engine Terraria otomatis membagi nilai ini dengan 2).
                // Mekanik penonaktifan Life Regen kaku sebelumnya sudah dihapus agar player tetap bisa healing alami.
                Player.lifeRegen -= 10;
            }
        }

        public override void DrawEffects(Terraria.DataStructures.PlayerDrawSet drawInfo, ref float r, ref float g, ref float b, ref float a, ref bool fullBright)
        {
            if (shadowflameCustom)
            {
                if (Main.rand.NextBool(2)) 
                {
                    Dust dust = Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.Shadowflame, 0f, -2f, 100, default, 1.5f);
                    
                    dust.noGravity = true; 
                    dust.velocity.X *= 0.5f; 
                    dust.velocity.Y -= 1.5f; 
                    dust.fadeIn = 1.2f; 
                }

                r *= 0.7f;
                g *= 0.2f;
                b *= 0.9f;
            }
        }
    }
}