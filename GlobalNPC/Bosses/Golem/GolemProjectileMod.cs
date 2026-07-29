using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;

namespace TheSanity.Projectiles
{
    public class GolemProjectileMod : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        
        public bool isFromGolem = false;
        private float baseSpeed = 0f;
        private int waveTimer = 0;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (source is EntitySource_Parent parentSource && parentSource.Entity is NPC npc)
            {
                if (npc.type == NPCID.Golem || npc.type == NPCID.GolemHead || npc.type == NPCID.GolemHeadFree)
                {
                    isFromGolem = true;
                }
            }

            // Mekanik C: Pengganti Fireball Golem Kuil menjadi Inferno Bolt (Chance serangan ke 5-7)
            if (isFromGolem && projectile.type == ProjectileID.Fireball)
            {
                int golemIndex = NPC.FindFirstNPC(NPCID.Golem);
                if (golemIndex != -1)
                {
                    var golemPhase1 = Main.npc[golemIndex].GetGlobalNPC<GlobalNPCs.GolemPhase1Override>();
                    golemPhase1.fireballCount++;

                    if (golemPhase1.fireballCount >= golemPhase1.nextReplaceCount)
                    {
                        golemPhase1.fireballCount = 0;
                        golemPhase1.nextReplaceCount = Main.rand.Next(5, 8);

                        projectile.type = ProjectileID.InfernoHostileBolt;
                        projectile.SetDefaults(ProjectileID.InfernoHostileBolt);
                        projectile.hostile = true;
                        projectile.friendly = false;
                    }
                }
            }
        }

        public override void AI(Projectile projectile)
        {
            if (!isFromGolem) return;

            // Logika Gelombang Kecepatan EyeBeam
            if (projectile.type == ProjectileID.EyeBeam)
            {
                if (baseSpeed == 0f)
                {
                    baseSpeed = projectile.velocity.Length();
                    if (baseSpeed == 0f) baseSpeed = 6f;
                }

                waveTimer++;
                
                float sinWave = (float)Math.Sin(waveTimer * 0.15f);
                float speedFactor = MathHelper.Lerp(0.20f, 1.0f, (sinWave + 1f) / 2f);

                if (projectile.velocity != Vector2.Zero)
                {
                    projectile.velocity = Vector2.Normalize(projectile.velocity) * baseSpeed * speedFactor;
                }
            }
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            // Mengubah warna es duri Deerclops menjadi Oranye Tua kuil Lihzahrd
            if (isFromGolem && projectile.type == ProjectileID.DeerclopsRangedProjectile)
            {
                lightColor = new Color(230, 90, 15) * projectile.Opacity;
            }
            return true;
        }
    }
}