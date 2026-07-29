using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;
using Microsoft.Xna.Framework;

namespace TheSanity
{
    public class ReworkedWerewolf : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer untuk mengatur cooldown dash (3 detik = 180 frame)
        private int dashCooldownTimer = 0;
        
        // Timer durasi saat Werewolf sedang melakukan hentakan dash
        private int dashActiveTimer = 0;
        private bool isDashing = false;
        private Vector2 dashDirection = Vector2.Zero;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Werewolf;
        }

        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.Werewolf) return;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active) return;

            // Hitung jarak antara Werewolf dan Player (1 Block = 16 Pixel)
            float distanceToPlayer = Vector2.Distance(npc.Center, target.Center);
            float twentyBlocks = 20f * 16f; // 320 Pixel

            if (dashCooldownTimer > 0)
            {
                dashCooldownTimer--;
            }

            // =========================================================================
            // [RAM DASH TRIGGER LOCATION]: AKTIVASI TERJANGAN DETEKSI 20 BLOCK
            // =========================================================================
            if (!isDashing && dashCooldownTimer == 0 && distanceToPlayer <= twentyBlocks)
            {
                isDashing = true;
                dashActiveTimer = 0;
                
                dashDirection = target.Center - npc.Center;
                dashDirection.Normalize(); 
                
                npc.netUpdate = true;
            }

            // =========================================================================
            // [DASH EXECUTION LOCATION]: KECEPATAN & JARAK TERJANGAN (40 BLOCK)
            // =========================================================================
            if (isDashing)
            {
                dashActiveTimer++;

                // Kecepatan melesat kencang (14f)
                npc.velocity.X = dashDirection.X * 14f;
                npc.velocity.Y = dashDirection.Y * 2f; 

                if (Main.rand.NextBool(3))
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Blood, npc.velocity.X * 0.5f, npc.velocity.Y * 0.5f);
                }

                // Jarak jangkauan 40 block ke belakang target
                if (dashActiveTimer >= 45) 
                {
                    isDashing = false;
                    npc.velocity.X *= 0.2f; 
                    dashCooldownTimer = 180; // Cooldown 3 detik
                }

                npc.ai[0] = 0; 
            }
        }

        // =========================================================================
        // [INFECTION MECHANIC LOCATION]: CHANCE 35% MENULARKAN BUFF WEREWOLF (ID 28)
        // =========================================================================
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            if (npc.type == NPCID.Werewolf)
            {
                // 35% peluang menularkan kutukan lewat gigitan
                if (Main.rand.NextFloat() <= 0.35f)
                {
                    target.AddBuff(BuffID.Werewolf, 36000);
                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Zombie21, target.position); 
                }
            }
        }
    }
}