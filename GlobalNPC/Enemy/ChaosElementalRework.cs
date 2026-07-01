using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff; // Memanggil folder Buff agar bisa membaca debuff DistruptedTime

namespace TheSanity.NPCs
{
    public class ChaosElementalRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Memastikan efek ini HANYA berjalan pada Chaos Elemental vanilla
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.ChaosElemental;
        }

        // =========================================================================
        // FIX UTAMA: MENAMBAHKAN DEBUFF SAAT PLAYER TERKENA HIT FISIK (CONTACT DAMAGE)
        // =========================================================================
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            // --- PANDUAN BALANCING DURASI DEBUFF ---
            // 3 detik * 60 frame = 180 frame durasi debuff
            int debuffDuration = 180;

            // Berikan debuff kustom DistruptedTime ke player yang terkena tabrak
            target.AddBuff(ModContent.BuffType<DistruptedTime>(), debuffDuration);

            // Efek kosmetik tambahan: Munculkan partikel dust pelangi di tubuh player 
            // saat terinfeksi biar visualnya kelihatan instan dan keren
            for (int i = 0; i < 15; i++)
            {
                int dustIndex = Dust.NewDust(target.position, target.width, target.height, DustID.RainbowMk2, 0f, 0f, 100, default(Color), 1.3f);
                Main.dust[dustIndex].noGravity = true;
                Main.dust[dustIndex].velocity *= 2f;
            }
        }
    }
}