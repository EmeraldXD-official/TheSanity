using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class SkeletonArcherRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int attackCooldown = 0;
        private int burstCount = 0;
        private int burstTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.SkeletonArcher;
        }

        public override bool PreAI(NPC npc)
        {
            if (!npc.active) return true;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (!target.active || target.dead) return true;

            // Mematikan tembakan asli bawaan game
            if (npc.ai[1] > 30f) 
            {
                npc.ai[1] = 0f; 
            }

            float distanceToPlayer = Vector2.Distance(npc.Center, target.Center);

            if (attackCooldown > 0)
            {
                attackCooldown--;
            }

            // Deteksi jarak 45 blok dan pastikan tidak terhalang dinding
            if (distanceToPlayer <= 45f * 16f && Collision.CanHitLine(npc.position, npc.width, npc.height, target.position, target.width, target.height))
            {
                if (attackCooldown <= 0 && burstCount == 0)
                {
                    burstCount = 3;
                    burstTimer = 0;
                    attackCooldown = 180;
                }
            }

            if (burstCount > 0)
            {
                burstTimer++;

                if (burstTimer >= 12) 
                {
                    burstTimer = 0;
                    burstCount--;

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // Kecepatan dasar diturunkan ke 7.5f agar tidak terlalu cepat
                        Vector2 shootVelocity = (target.Center - npc.Center);
                        shootVelocity.Normalize();
                        shootVelocity *= 7.5f; 

                        float spreadRotation = 0f;

                        if (burstCount == 2) spreadRotation = -0.15f; 
                        else if (burstCount == 1) spreadRotation = 0f;
                        else if (burstCount == 0) spreadRotation = 0.15f;

                        shootVelocity = shootVelocity.RotatedBy(spreadRotation);

                        int arrowProj = Projectile.NewProjectile(
                            npc.GetSource_FromAI(), 
                            npc.Center, 
                            shootVelocity, 
                            ProjectileID.BoneArrow, 
                            22, 
                            2f, 
                            Main.myPlayer
                        );

                        if (arrowProj >= 0 && arrowProj < Main.maxProjectiles)
                        {
                            Projectile p = Main.projectile[arrowProj];
                            p.hostile = true;
                            p.friendly = false;
                            
                            // Mengecilkan ukuran visual (60%)
                            p.scale = 0.6f;
                            // Mengecilkan hitbox (60%)
                            p.width = (int)(p.width * 0.6f);
                            p.height = (int)(p.height * 0.6f);
                            // Sedikit perlambatan tambahan pada proyektil
                            p.velocity *= 0.9f; 
                        }
                    }
                    npc.netUpdate = true;
                }
            }
            return true;
        }
    }

    public class ArcherDebuffHandler : global::Terraria.ModLoader.GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (projectile.type == ProjectileID.BoneArrow && projectile.hostile)
            {
                target.AddBuff(BuffID.CursedInferno, 180, true);
                target.AddBuff(BuffID.Ichor, 300, true);
            }
        }
    }
}