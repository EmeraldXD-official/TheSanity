using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.Audio; // Diperlukan untuk SoundEngine
using System.Collections.Generic;

namespace TheSanity
{
    public class CorruptCrimsonShooter : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        public int shootTimer = 0;
        public int recoilTimer = 0;

        public override void PostAI(NPC npc)
        {
            bool isEater = (npc.type == 6 || npc.type == -11 || npc.type == -12);
            bool isCrimera = (npc.type == 173 || npc.type == -22 || npc.type == -23);

            if (isEater || isCrimera)
            {
                if (recoilTimer > 0)
                {
                    recoilTimer--;
                    if (recoilTimer < 10) npc.velocity *= 0.8f;
                    return; 
                }

                shootTimer++;

                if (shootTimer >= 180)
                {
                    Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
                    if (target.active && !target.dead && npc.HasPlayerTarget && npc.Distance(target.Center) < 550f)
                    {
                        int projType = isEater ? ProjectileID.CursedBullet : ProjectileID.IchorBullet;
                        int damageValue = Main.masterMode ? 4 : (Main.expertMode ? 6 : 13);
                        Vector2 targetDir = Vector2.Normalize(target.Center - npc.Center);
                        
                        // --- SUARA TEMBAKAN ACAK ---
                        // Memilih antara Item63, 64, atau 65 secara acak
                        int randomSoundID = Main.rand.Next(63, 66); 
                        SoundEngine.PlaySound(new SoundStyle($"Terraria/Sounds/Item_{randomSoundID}"), npc.Center);

                        for (int i = 0; i < 3; i++)
                        {
                            Vector2 perturbedSpeed = (targetDir * 9f).RotatedBy(MathHelper.Lerp(-0.2f, 0.2f, i / 2f));
                            
                            int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, perturbedSpeed, projType, damageValue, 1f, Main.myPlayer);
                            
                            if (p != Main.maxProjectiles) 
                            {
                                Main.projectile[p].hostile = true;
                                Main.projectile[p].friendly = false;
                                Main.projectile[p].netUpdate = true;
                                Main.projectile[p].scale = 1.4f;
                            }
                        }

                        npc.velocity = -targetDir * 8f;
                        recoilTimer = 25; 
                        
                        for (int j = 0; j < 6; j++)
                        {
                            Dust.NewDust(npc.position, npc.width, npc.height, isEater ? DustID.CursedTorch : DustID.IchorTorch, -npc.velocity.X * 0.5f, -npc.velocity.Y * 0.5f);
                        }
                    }
                    shootTimer = 0; 
                }
            }
        }
    }

    public class BulletRework : GlobalProjectile
    {
        public override bool? CanHitNPC(Projectile projectile, NPC target)
        {
            if (projectile.hostile && (projectile.type == ProjectileID.CursedBullet || projectile.type == ProjectileID.IchorBullet))
            {
                return false; 
            }
            return null;
        }

        public override void AI(Projectile projectile)
        {
            if (projectile.hostile && (projectile.type == ProjectileID.CursedBullet || projectile.type == ProjectileID.IchorBullet))
            {
                if (Main.rand.NextBool(3))
                {
                    int dustType = (projectile.type == ProjectileID.CursedBullet) ? DustID.CursedTorch : DustID.IchorTorch;
                    Dust d = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, dustType, 0, 0, 100, default, 1.1f);
                    d.noGravity = true;
                }
            }
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (projectile.hostile)
            {
                if (projectile.type == ProjectileID.CursedBullet) target.AddBuff(39, 420);
                else if (projectile.type == ProjectileID.IchorBullet) target.AddBuff(69, 420);
            }
        }
    }
}