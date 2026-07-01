using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class AntlionSwarmerRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void PostAI(NPC npc)
        {
            // --- 1. CEK ID SWARMER (509 & 581) ---
            if (npc.type == 509 || npc.type == 581)
            {
                Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
                
                if (target.active && !target.dead)
                {
                    // --- 2. PAKSA AGGRO (Tembus Pandang) ---
                    // npc.target adalah index player yang dia incar.
                    // Kita pastikan npc.ai[0] (biasanya status serangan) tetap aktif mengejar
                    npc.noTileCollide = true;
                    
                    // Jika jaraknya terlalu jauh atau dia mulai kehilangan minat (karena tembok)
                    // Kita paksa arah kecepatannya menuju ke pusat player
                    float distance = Vector2.Distance(npc.Center, target.Center);
                    
                    if (distance < 1000f) // Hanya mengejar jika player masih dalam jangkauan 1000 pixel
                    {
                        // LOKASI SPEED: 0.15f adalah seberapa kuat dia dipaksa belok ke player
                        // Jika dia masih "malu-malu" tembus block, naikkan angka ini (misal 0.25f)
                        Vector2 direction = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                        npc.velocity = Vector2.Lerp(npc.velocity, direction * 6f, 0.05f); 
                    }
                }

                // Visual saat berada di dalam dinding
                if (Collision.SolidCollision(npc.position, npc.width, npc.height))
                {
                    if (Main.rand.NextBool(3))
                    {
                        Dust.NewDust(npc.position, npc.width, npc.height, DustID.Sand, 0, 0, 100, default, 0.7f);
                    }
                }
            }
        }
    }
}