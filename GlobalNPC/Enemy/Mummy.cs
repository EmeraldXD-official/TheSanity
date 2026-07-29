using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class MummyAuraRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public override void PostAI(NPC npc)
        {
            // --- 1. SETTING VARIANT MUMMY ---
            Color auraColor;
            int debuffID;
            bool isValid = true;

            switch (npc.type)
            {
                case 78: // Regular Mummy
                    auraColor = new Color(255, 215, 0); // Kuning Gold
                    debuffID = 67; // Burning
                    break;
                case 79: // Corrupt Mummy
                    auraColor = new Color(138, 43, 226); // Ungu Corrupt
                    debuffID = 39; // Cursed Flames
                    break;
                case 630: // Crimson Mummy
                    auraColor = new Color(255, 0, 0); // Merah Darah
                    debuffID = 69; // Ichor
                    break;
                case 80: // Hallow Mummy
                    auraColor = new Color(255, 105, 180); // Pink Cerah
                    debuffID = 31; // Confused
                    break;
                default:
                    isValid = false;
                    return;
            }

            if (!isValid) return;

            float auraRadius = 160f; // 10 Blocks

            // --- 2. VISUAL AURA (Optimized) ---
            if (Main.netMode != NetmodeID.Server && !Main.gamePaused)
            {
                if (Main.rand.NextBool(3))
                {
                    Vector2 offset = Main.rand.NextVector2CircularEdge(auraRadius, auraRadius);
                    // Dust 267 (Dust yang bisa diwarnai)
                    Dust d = Dust.NewDustPerfect(npc.Center + offset, 267, Vector2.Zero, 100, auraColor, 1.1f);
                    d.noGravity = true;
                    d.velocity = offset.RotatedBy(1.57).SafeNormalize(Vector2.Zero) * 1.2f;
                }
            }

            // --- 3. LOGIKA DEBUFF & AUTO-CLEANSE (1 DETIK) ---
            foreach (Player player in Main.player)
            {
                if (player.active && !player.dead)
                {
                    float distance = Vector2.Distance(npc.Center, player.Center);

                    if (distance < auraRadius)
                    {
                        // SAAT DI DALAM AREA: Beri debuff terus menerus
                        player.AddBuff(debuffID, 120);
                    }
                    else if (distance >= auraRadius && distance < auraRadius + 50f)
                    {
                        // SAAT KELUAR AREA: Paksa sisa durasi jadi 1 detik (60 frame)
                        int buffIndex = player.FindBuffIndex(debuffID);
                        if (buffIndex != -1 && player.buffTime[buffIndex] > 60)
                        {
                            player.buffTime[buffIndex] = 60;
                        }
                    }
                }
            }
        }
    }
}