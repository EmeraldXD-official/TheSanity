using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class GhoulAuraRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void PostAI(NPC npc)
        {
            // --- 1. SETTING VARIANT ---
            Color auraColor;
            int debuffID;
            bool isValid = true;

            switch (npc.type)
            {
                case 524: auraColor = new Color(255, 215, 0); debuffID = 67; break;   // Normal
                case 525: auraColor = new Color(138, 43, 226); debuffID = 39; break;  // Corrupt
                case 526: auraColor = new Color(255, 0, 0); debuffID = 69; break;     // Crimson
                case 527: auraColor = new Color(255, 105, 180); debuffID = 31; break; // Hallow (Confuse)
                default: isValid = false; return;
            }

            if (!isValid) return;

            float auraRadius = 160f; // 10 Blocks

            // --- 2. VISUAL AURA (Optimized) ---
            if (Main.netMode != NetmodeID.Server && !Main.gamePaused)
            {
                if (Main.rand.NextBool(3))
                {
                    Vector2 offset = Main.rand.NextVector2CircularEdge(auraRadius, auraRadius);
                    Dust d = Dust.NewDustPerfect(npc.Center + offset, 267, Vector2.Zero, 100, auraColor, 1.1f);
                    d.noGravity = true;
                    d.velocity = offset.RotatedBy(1.57).SafeNormalize(Vector2.Zero) * 1.2f;
                }
            }

            // --- 3. LOGIKA DEBUFF & AUTO-CLEANSE ---
            foreach (Player player in Main.player)
            {
                if (player.active && !player.dead)
                {
                    float distance = Vector2.Distance(npc.Center, player.Center);

                    if (distance < auraRadius)
                    {
                        // SAAT DI DALAM AREA: Beri debuff terus menerus (Refresh durasi ke 2 detik)
                        player.AddBuff(debuffID, 120);
                    }
                    else if (distance >= auraRadius && distance < auraRadius + 50f)
                    {
                        // SAAT KELUAR AREA: Jika player punya debuff tersebut dan durasinya kelamaan...
                        int buffIndex = player.FindBuffIndex(debuffID);
                        if (buffIndex != -1 && player.buffTime[buffIndex] > 60)
                        {
                            // LOKASI RESET: Paksa durasi sisa jadi 1 detik (60 frame)
                            player.buffTime[buffIndex] = 60;
                        }
                    }
                }
            }
        }
    }
}