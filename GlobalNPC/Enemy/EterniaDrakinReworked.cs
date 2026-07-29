using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC GLOBAL REWORK]: ETERNIA DRAKIN TIER 2 & 3 (JUMP & STOMP MECHANIC)
    // =========================================================================
    public class EterniaDrakinRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        public int jumpCooldownTimer = 0;
        public bool isJumping = false;
        public int airTime = 0; 

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.DD2DrakinT2 || 
                   entity.type == NPCID.DD2DrakinT3;
        }

        public override void AI(NPC npc)
        {
            if (!isJumping)
            {
                jumpCooldownTimer++;

                // 5 Detik = 300 tick
                if (jumpCooldownTimer >= 300)
                {
                    // Cek apakah dia sedang Stand Still (Diam di tempat)
                    bool isStationary = Math.Abs(npc.velocity.X) < 0.1f && Math.Abs(npc.velocity.Y) < 0.1f;

                    if (isStationary)
                    {
                        npc.velocity.Y = -8f; 
                        npc.velocity.X = 0f;  
                        isJumping = true;
                        airTime = 0;
                        jumpCooldownTimer = 0; 
                    }
                }
            }
            else
            {
                airTime++;

                if (airTime > 15 && Math.Abs(npc.velocity.Y) == 0f)
                {
                    isJumping = false; 

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // =========================================================================
                        // Limitasi Damage Ogre Smash (15 hingga 25)
                        // =========================================================================
                        int fixedSmashDamage = Main.rand.Next(15, 26);

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            Vector2.Zero, 
                            ProjectileID.DD2OgreSmash,
                            fixedSmashDamage, 
                            8f, 
                            Main.myPlayer
                        );
                    }
                }
            }
        }
    }

    // =========================================================================
    // [PROJECTILE GLOBAL REWORK]: DRAKIN SHOT EXPLOSION (KHUSUS TIER 3)
    // =========================================================================
    public class DrakinShotRework : global::Terraria.ModLoader.GlobalProjectile
    {
        public override bool InstancePerEntity => true;

        // Penanda khusus untuk memastikan apakah ini tembakan dari Tier 3
        public bool isFiredByT3 = false;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return entity.type == ProjectileID.DD2DrakinShot;
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            // Mengecek siapa yang menembakkan proyektil ini
            if (source is EntitySource_Parent parentSource && parentSource.Entity is NPC npc)
            {
                // Jika yang menembak adalah T3, nyalakan status ledakannya!
                if (npc.type == NPCID.DD2DrakinT3)
                {
                    isFiredByT3 = true;
                }
            }
        }

        // Event OnKill berjalan saat proyektil hancur (Kena player, NPC, atau Tembok)
        public override void OnKill(Projectile projectile, int timeLeft)
        {
            // Jika BUKAN dari T3, hentikan kode di sini (tidak meledak)
            if (!isFiredByT3) return;

            // Visual Effect: Ledakan partikel Ungu
            for (int i = 0; i < 25; i++)
            {
                Dust.NewDust(projectile.position, projectile.width, projectile.height, DustID.PurpleCrystalShard, Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-5f, 5f), 100, default, 2f);
                Dust.NewDust(projectile.position, projectile.width, projectile.height, DustID.Shadowflame, Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-3f, 3f), 100, default, 1.5f);
            }

            // Area of Effect (AoE) Damage: Memberikan tepat 37 Damage
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                float blastRadius = 90f; // Jarak ledakan (sedikit lebih kecil dari bom goblin)

                foreach (Player targetPlayer in Main.player)
                {
                    // Jika player aktif, belum mati, dan berada di dalam jangkauan radius ledakan
                    if (targetPlayer.active && !targetPlayer.dead && projectile.Distance(targetPlayer.Center) <= blastRadius)
                    {
                        // Berikan Fix Damage 37
                        targetPlayer.Hurt(PlayerDeathReason.ByProjectile(-1, projectile.whoAmI), 37, 0);
                    }
                }
            }
        }
    }
}