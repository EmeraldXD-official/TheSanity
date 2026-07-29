using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class UndeadMinerRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        private int bombTimer = 0;

        public override void PostAI(NPC npc)
        {
            if (npc.type != NPCID.UndeadMiner) return;

            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            if (!target.active || target.dead) return;

            // --- LOKASI SPEED: 180 (3 DETIK) ---
            bombTimer++;

            if (bombTimer >= 180) 
            {
                if (Collision.CanHit(npc.position, npc.width, npc.height, target.position, target.width, target.height))
                {
                    Vector2 velocity = Vector2.Normalize(target.Center - npc.Center) * 8f;
                    velocity.Y -= 2.5f; // Sedikit lengkungan natural

                    // --- LOKASI DAMAGE: 70 ---
                    int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velocity, ProjectileID.Grenade, 70, 3f, Main.myPlayer);
                    
                    if (p != Main.maxProjectiles)
                    {
                        Projectile proj = Main.projectile[p];
                        
                        // META KEJAM: Kunci Identitas
                        proj.friendly = false;
                        proj.hostile = true;
                        proj.penetrate = 1;     // HILANGKAN PIERCING (Hanya 1 hit)
                        proj.scale = 0.7f;      // Ukuran Kecil
                        proj.ai[1] = 666f;      // Tag Setan untuk Force AI
                        
                        proj.netUpdate = true;
                    }

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item1, npc.Center); 
                    bombTimer = 0; 
                }
            }
        }
    }

    public class MinerGrenadeForce : GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        // PRE-AI: KITA PAKSA STATUSNYA SEBELUM VANILLA SEMPAT MENGUBAHNYA
        public override bool PreAI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.Grenade && projectile.ai[1] == 666f)
            {
                projectile.hostile = true;
                projectile.friendly = false;
                projectile.damage = 70; // Paksa damage tetap konsisten
            }
            return true;
        }

        // COLLISION META: PAKSA MATI SAAT NYENTUH PLAYER
        public override bool CanHitPlayer(Projectile projectile, Player target)
        {
            if (projectile.type == ProjectileID.Grenade && projectile.ai[1] == 666f)
            {
                // Cek Hitbox secara manual (Pixel Perfect)
                if (projectile.Hitbox.Intersects(target.Hitbox))
                {
                    projectile.Kill(); // LANGSUNG MELEDAK DI TEMPAT
                    return true;
                }
            }
            return base.CanHitPlayer(projectile, target);
        }

        public override void OnKill(Projectile projectile, int timeLeft)
        {
            if (projectile.type == ProjectileID.Grenade && projectile.ai[1] == 666f)
            {
                // Suara Ledakan
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item14, projectile.Center);

                // Partikel Ledakan Brutal
                for (int i = 0; i < 25; i++)
                {
                    Dust d = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.Smoke, 0f, 0f, 100, default, 1.5f);
                    d.velocity *= 3f;
                    Dust d2 = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.Torch, 0f, 0f, 100, default, 2f);
                    d2.noGravity = true;
                    d2.velocity *= 4f;
                }
            }
        }
    }
}