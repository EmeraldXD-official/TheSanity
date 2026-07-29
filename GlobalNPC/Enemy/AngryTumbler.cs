using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class AngryTumblerRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void SetDefaults(NPC npc)
        {
            // --- 1. KASIH DAMAGE KECIL (Agar OnHitPlayer jalan) ---
            if (npc.type == 546)
            {
                // LOKASI DAMAGE: Set ke 1 agar tidak terasa tapi debuff tetap masuk
                npc.damage = 1; 
            }
        }

        public override void PostAI(NPC npc)
        {
            // --- 2. PAKSA DAMAGE TETAP KECIL ---
            // Kita paksa di PostAI agar tidak naik drastis saat Expert/Master Mode
            if (npc.type == 546)
            {
                npc.damage = 1;
            }
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            // --- 3. INFLICT WEBBED DEBUFF (ID 149) ---
            if (npc.type == 546)
            {
                // LOKASI DURASI: 180 frame = 3 Detik
                target.AddBuff(149, 180);

                // VISUAL EFFECT JARING
                for (int i = 0; i < 15; i++)
                {
                    Dust d = Dust.NewDustDirect(target.position, target.width, target.height, DustID.Web, 0, 0, 100, default, 1.2f);
                    d.velocity *= 1.5f;
                    d.noGravity = true;
                }
            }
        }
    }
}