using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class NymphRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public int boulderTimer = 0;
        public int beamTimer = 0;
        public int beamSequence = 0;

        public override void PostAI(NPC npc)
        {
            if (npc.type != NPCID.Nymph && npc.type != NPCID.LostGirl) return;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead) return;

            // --- 1. NYMPH NORMAL ---
            npc.noTileCollide = false;

            // --- 2. BOULDER DYNAMIC COLLISION ---
            boulderTimer++;
            if (boulderTimer >= 120)
            {
                Vector2 spawnPos = new Vector2(target.Center.X, target.Center.Y - 350); 
                int p = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, new Vector2(0, 6f), ProjectileID.LifeCrystalBoulder, 80, 6f, Main.myPlayer);
                if (p != Main.maxProjectiles) {
                    Main.projectile[p].hostile = true;
                    Main.projectile[p].friendly = false;
                    Main.projectile[p].ai[1] = 888f; // Tag khusus Boulder Nymph
                }
                boulderTimer = 0;
            }

            // --- 3. LIGHT BEAM (COOLDOWN 4 DETIK) ---
            beamTimer++;
            if (beamTimer >= 240) 
            {
                if (beamTimer % 10 == 0)
                {
                    float offX = (npc.direction == 1) ? -60 : 60;
                    float offY = -60 + (beamSequence * 40);
                    Vector2 spawnPos = npc.Center + new Vector2(offX, offY);

                    Vector2 shootVel = target.Center - spawnPos;
                    shootVel.Normalize();
                    shootVel *= 16f; 

                    int b = Projectile.NewProjectile(npc.GetSource_FromAI(), spawnPos, shootVel, ProjectileID.LightBeam, 10, 4f, Main.myPlayer);
                    if (b != Main.maxProjectiles)
                    {
                        Projectile proj = Main.projectile[b];
                        proj.hostile = true;
                        proj.friendly = false;
                        proj.tileCollide = false;
                        proj.ai[1] = 777f; // Tag Pedang
                        
                        // --- FIX ROTASI PEDANG ---
                        // Saya kurangi rotasinya. Kalau pojok kanan atas adalah ujungnya, 
                        // kita pakai PiOver4 (45 derajat) saja tanpa tambahan PiOver2.
                        proj.rotation = proj.velocity.ToRotation() + MathHelper.PiOver4;
                        proj.netUpdate = true;
                    }

                    beamSequence++;
                    if (beamSequence >= 4) 
                    {
                        beamSequence = 0;
                        beamTimer = 0; 
                    }
                }
            }
        }
    }

    public class NymphProjectileLogic : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        public override void AI(Projectile projectile)
        {
            // --- LOGIKA PEDANG ---
            if (projectile.type == ProjectileID.LightBeam && projectile.ai[1] == 777f)
            {
                if (projectile.velocity != Vector2.Zero)
                {
                    // Gunakan 45 derajat saja agar pojok kanan atas lurus ke depan
                    projectile.rotation = projectile.velocity.ToRotation() + MathHelper.PiOver4;
                }
                Lighting.AddLight(projectile.Center, 0.7f, 0.7f, 0.3f);
            }

            // --- LOGIKA BOULDER DYNAMIC COLLISION ---
            if (projectile.type == ProjectileID.LifeCrystalBoulder && projectile.ai[1] == 888f)
            {
                Player target = Main.player[projectile.owner]; // Owner di sini biasanya player karena projectile.NewProjectile
                // Cari player terdekat karena projectile.owner di NPC bisa ribet
                Player nearestPlayer = Main.player[Player.FindClosest(projectile.position, projectile.width, projectile.height)];

                if (nearestPlayer.active && !nearestPlayer.dead)
                {
                    // Jika posisi Boulder masih di atas player, tembus block
                    // Jika sudah sejajar atau di bawah player, jadi padat (tileCollide = true)
                    if (projectile.Center.Y < nearestPlayer.Center.Y - 10f)
                    {
                        projectile.tileCollide = false;
                    }
                    else
                    {
                        projectile.tileCollide = true;
                    }
                }
            }
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (projectile.type == ProjectileID.LightBeam && projectile.ai[1] == 777f)
            {
                target.AddBuff(320, 600); 
                target.AddBuff(353, 600); 
            }
        }
    }

    public class NymphDebuffLogic : ModPlayer
    {
        public override void PostUpdateBuffs()
        {
            if (Player.HasBuff(353))
            {
                if (!Player.HasBuff(320)) Player.AddBuff(320, 2);
            }
            else
            {
                if (Player.HasBuff(320))
                {
                    int index = Player.FindBuffIndex(320);
                    if (index != -1) Player.DelBuff(index);
                }
            }
        }
    }
}