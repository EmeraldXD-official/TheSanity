using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class ArmoredSkeletonRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        public int attackTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == 77 || entity.type == NPCID.ArmoredSkeleton;
        }

        public override void AI(NPC npc)
        {
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead) return;

            float dist = Vector2.Distance(npc.Center, target.Center);

            if (dist <= 50f * 16f)
            {
                attackTimer++;
                if (attackTimer > 240) 
                {
                    Dust.NewDust(npc.position, npc.width, npc.height, DustID.Firework_Red, 0, 0, 100, default, 1f);
                }
                if (attackTimer >= 300) 
                {
                    SpawnSwordBeams(npc);
                    attackTimer = 0;
                }
            }
            else { attackTimer = 0; }
        }

        private void SpawnSwordBeams(NPC npc)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            for (int i = 0; i < 4; i++)
            {
                float angle = MathHelper.TwoPi * i / 4;
                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ProjectileID.SwordBeam, 35, 2f, Main.myPlayer);
                
                if (p < Main.maxProjectiles)
                {
                    Projectile proj = Main.projectile[p];
                    proj.hostile = true;
                    proj.friendly = false;
                    proj.ai[0] = 0;
                    proj.ai[1] = angle; 
                    proj.localAI[0] = 0; 
                    proj.localAI[1] = npc.whoAmI + 1; 
                }
            }
        }
    }

    public class SwordBeamBehavior : global::Terraria.ModLoader.GlobalProjectile
    {
        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => entity.type == ProjectileID.SwordBeam;

        public override bool PreAI(Projectile projectile)
        {
            if (projectile.localAI[1] == 0) return true;
            
            int ownerIndex = (int)projectile.localAI[1] - 1;
            if (ownerIndex < 0 || ownerIndex >= Main.maxNPCs || !Main.npc[ownerIndex].active)
            {
                projectile.Kill();
                return false;
            }
            
            NPC owner = Main.npc[ownerIndex];
            Player target = Main.player[owner.target];

            // --- LOGIKA TEMBUS BLOK (Aktif selama 5 detik pertama) ---
            projectile.localAI[0]++;
            if (projectile.localAI[0] <= 300)
            {
                projectile.tileCollide = false;
            }
            else
            {
                projectile.tileCollide = true;
            }

            // State 0: Orbiting Sambil Aiming
            if (projectile.ai[0] == 0)
            {
                projectile.Center = owner.Center + projectile.ai[1].ToRotationVector2() * 160f;
                
                Vector2 dir = target.Center - projectile.Center;
                projectile.rotation = dir.ToRotation() + MathHelper.PiOver4;
                
                if (projectile.localAI[0] > 180) projectile.ai[0] = 2;
            }
            // State 2: Dash (Melesat)
            else if (projectile.ai[0] == 2)
            {
                projectile.velocity = (target.Center - projectile.Center).SafeNormalize(Vector2.Zero) * 12f;
                projectile.ai[0] = 3;
            }
            return false;
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.OnFire, 300, true);
        }
    }
}