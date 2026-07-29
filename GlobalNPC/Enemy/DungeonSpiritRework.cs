using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class DungeonSpiritRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer untuk mengatur cooldown serangan (5 detik = 300 ticks)
        private int attackTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.DungeonSpirit;
        }

        // =========================================================================
        // [AI REWORK LOCATION]: POLA TEMBAKAN SEMBURAN DAN EFEK RECOIL
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.DungeonSpirit) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active) return;

            attackTimer++;

            // -------------------------------------------------------------------------
            // [AI COOLDOWN BALANCING]: 300 Ticks = 5 Detik Cooldown
            // -------------------------------------------------------------------------
            if (attackTimer >= 300)
            {
                attackTimer = 0; // Reset Timer

                Vector2 shootDir = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                
                // Kecepatan peluru dasar
                float projectileSpeed = 6f; 

                // 1. TEMBAKAN TENGAH: 1x ShadowBeamHostile
                Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootDir * projectileSpeed, ProjectileID.ShadowBeamHostile, 25, 1f, Main.myPlayer);

                // Sudut penyebaran dalam radian (15 derajat untuk Inferno, 30 derajat untuk Lost Soul)
                float spreadInferno = MathHelper.ToRadians(15);
                float spreadLostSoulInner = MathHelper.ToRadians(15);
                float spreadLostSoulOuter = MathHelper.ToRadians(30);

                // 2. TEMBAKAN KANAN KIRI: 2x InfernoHostileBlast
                for (int i = -1; i <= 1; i += 2) // i = -1 (Kiri), i = 1 (Kanan)
                {
                    Vector2 infernoVel = shootDir.RotatedBy(spreadInferno * i) * projectileSpeed;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, infernoVel, ProjectileID.InfernoHostileBlast, 30, 1f, Main.myPlayer);
                }

                // 3. TEMBAKAN SISI LUAR: 4x LostSoulHostile
                // 2 Tembakan di posisi yang sama dengan inferno (Kiri & Kanan inner)
                for (int i = -1; i <= 1; i += 2)
                {
                    Vector2 soulInnerVel = shootDir.RotatedBy(spreadLostSoulInner * i) * (projectileSpeed * 1.2f); // Sedikit lebih cepat
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, soulInnerVel, ProjectileID.LostSoulHostile, 20, 1f, Main.myPlayer);
                }
                // 2 Tembakan di samping luarnya lagi (Kiri & Kanan outer)
                for (int i = -1; i <= 1; i += 2)
                {
                    Vector2 soulOuterVel = shootDir.RotatedBy(spreadLostSoulOuter * i) * (projectileSpeed * 1.2f);
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, soulOuterVel, ProjectileID.LostSoulHostile, 20, 1f, Main.myPlayer);
                }

                // --- EFEK AUDIO SAAT MENEMBAK ---
                Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCDeath6, npc.Center);

                // -------------------------------------------------------------------------
                // [RECOIL EFFECT LOCATION]: Menghentakkan si roh ke belakang sedikit
                // -------------------------------------------------------------------------
                Vector2 recoilDirection = -shootDir; 
                float recoilForce = 3.5f; // Kekuatan ketukan mundur
                npc.velocity = recoilDirection * recoilForce;

                npc.netUpdate = true;
            }
        }
    }
}