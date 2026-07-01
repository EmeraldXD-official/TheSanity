using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class GoblinThiefRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        private int dashCooldown = 0;
        private bool isDashing = false;
        private Vector2 dashTargetPos;

        public override void PostAI(NPC npc)
        {
            if (npc.type != NPCID.GoblinThief) return;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead) return;

            float distance = Vector2.Distance(npc.Center, target.Center);

            if (dashCooldown > 0) dashCooldown--;

            // --- 1. PEMICU DASH (JARAK 10-15 BLOCK) ---
            if (dashCooldown <= 0 && !isDashing && distance >= 160f && distance <= 240f)
            {
                isDashing = true;
                dashCooldown = 180; // 3 detik biar ngga terlalu rusuh

                Vector2 direction = target.Center - npc.Center;
                direction.Normalize();
                // Target 5 block di belakang player
                dashTargetPos = target.Center + (direction * 80f); 

                for (int i = 0; i < 15; i++)
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Smoke, 0, 0, 100, default, 1f);
                }
            }

            // --- 2. EKSEKUSI DASH ---
            if (isDashing)
            {
                Vector2 moveDir = dashTargetPos - npc.Center;
                float speed = 14f;

                if (moveDir.Length() > speed)
                {
                    moveDir.Normalize();
                    npc.velocity = moveDir * speed;
                    npc.noGravity = true; // Supaya dash lurus menembus player
                }
                else
                {
                    isDashing = false;
                    npc.velocity *= 0.2f; 
                    npc.noGravity = false;
                }
            }
        }

        // --- 3. DAMAGE & DEBUFF MENGGUNAKAN CONTACT DAMAGE ---
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            if (npc.type == NPCID.GoblinThief && isDashing)
            {
                // Berikan debuff saat tertabrak dash
                target.AddBuff(BuffID.Bleeding, 300); // 5 detik
                target.AddBuff(BuffID.Midas, 300);    // 5 detik

                // Visual koin emas
                for (int i = 0; i < 8; i++)
                {
                    Dust.NewDust(target.position, target.width, target.height, DustID.GoldCoin, 0, 0, 100, default, 1f);
                }
            }
        }
    }
}