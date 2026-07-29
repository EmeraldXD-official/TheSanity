using Terraria;
using Terraria.ModLoader;
using Terraria.ID;
using Microsoft.Xna.Framework;

namespace TheSanity.Buff
{
    public class SardineMarkBuff : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = false;
            Main.debuff[Type] = true; // Ditandai sebagai debuff musuh
        }

        public override void Update(NPC npc, ref int buffIndex)
        {
            // ====== VISUAL MARK DI ATAS KEPALA MUSUH ======
            if (Main.rand.NextBool(2))
            {
                // Memunculkan partikel tepat 25 pixel di atas kepala musuh
                Vector2 posisiMark = new Vector2(npc.Center.X + Main.rand.Next(-15, 16), npc.Top.Y - 25);
                
                // PERBAIKAN: Mengubah AquaBolt menjadi DustID.Water agar tidak error lagi
                int dust = Dust.NewDust(posisiMark, 0, 0, DustID.Water, 0f, -0.8f, 100, default, 1.3f);
                
                Main.dust[dust].noGravity = true; // Membuat efeknya melayang stabil
                Main.dust[dust].velocity *= 0.3f;
            }
        }
    }
}