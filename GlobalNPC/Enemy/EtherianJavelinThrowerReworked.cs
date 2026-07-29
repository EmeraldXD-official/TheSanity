using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC GLOBAL REWORK]: ETERNIA JAVELIN THROWER TIER 1, 2, & 3 (STATS)
    // =========================================================================
    public class EterniaJavelinRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.DD2JavelinstT1 || 
                   entity.type == NPCID.DD2JavelinstT2 || 
                   entity.type == NPCID.DD2JavelinstT3;
        }

        public override void SetDefaults(NPC npc)
        {
            // TIER 2: 60% Imun Knockback (0.4f)
            if (npc.type == NPCID.DD2JavelinstT2)
            {
                npc.knockBackResist = 0.4f; 
            }
            // TIER 3: 95% Imun Knockback (0.05f)
            else if (npc.type == NPCID.DD2JavelinstT3)
            {
                npc.knockBackResist = 0.05f; 
            }
        }
    }

    // =========================================================================
    // [PROJECTILE REWORK]: JAVELIN MULTI-SHOT, UPGRADE, & EXPLOSION SYSTEM
    // =========================================================================
    public class JavelinProjectileModifier : global::Terraria.ModLoader.GlobalProjectile
    {
        // Safety Lock untuk mencegah infinite loop saat spawn Javelin tambahan
        public static bool isSpawningExtraJavelin = false;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (isSpawningExtraJavelin) return;

            // Memastikan proyektil adalah Javelin musuh
            if (projectile.type == ProjectileID.DD2JavelinHostile || projectile.type == ProjectileID.DD2JavelinHostileT3)
            {
                if (source is EntitySource_Parent parentSource && parentSource.Entity is NPC npc)
                {
                    bool triggerMultishot = false;
                    int javelinTypeToSpawn = projectile.type;

                    // TIER 1 LOGIC
                    if (npc.type == NPCID.DD2JavelinstT1)
                    {
                        if (Main.rand.NextFloat() < 0.20f) triggerMultishot = true; // 20% Chance
                    }
                    // TIER 2 LOGIC
                    else if (npc.type == NPCID.DD2JavelinstT2)
                    {
                        // 5% Chance mengganti javelin biasa menjadi T3
                        if (projectile.type == ProjectileID.DD2JavelinHostile && Main.rand.NextFloat() < 0.05f)
                        {
                            projectile.type = ProjectileID.DD2JavelinHostileT3;
                            javelinTypeToSpawn = ProjectileID.DD2JavelinHostileT3;
                        }

                        if (Main.rand.NextFloat() < 0.25f) triggerMultishot = true; // 25% Chance
                    }
                    // TIER 3 LOGIC
                    else if (npc.type == NPCID.DD2JavelinstT3)
                    {
                        if (Main.rand.NextFloat() < 0.20f) triggerMultishot = true; // 20% Chance
                    }

                    // EKSEKUSI MULTI-SHOT (MENEMBAK 3 SEKALIGUS)
                    if (triggerMultishot)
                    {
                        isSpawningExtraJavelin = true;

                        // Vanilla AI sudah melempar 1, kita tambahkan 2 lagi agar totalnya 3
                        for (int i = 0; i < 2; i++)
                        {
                            // Menyebarkan arah lemparan (Rotasi acak sekitar 15 derajat)
                            Vector2 spreadVelocity = projectile.velocity.RotatedByRandom(MathHelper.ToRadians(15));
                            
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(),
                                projectile.Center,
                                spreadVelocity,
                                javelinTypeToSpawn,
                                projectile.damage,
                                projectile.knockBack,
                                Main.myPlayer
                            );
                        }

                        isSpawningExtraJavelin = false;
                    }
                }
            }
        }

        // =========================================================================
        // [ON KILL EVENT]: LEDAKAN JAVELIN T3 SAAT MENGENAI TARGET ATAU BLOCK
        // =========================================================================
        public override void OnKill(Projectile projectile, int timeLeft)
        {
            // Hanya aktif untuk Javelin T3
            if (projectile.type == ProjectileID.DD2JavelinHostileT3)
            {
                // Visual Effect: Mengeluarkan partikel api saat meledak
                for (int i = 0; i < 25; i++)
                {
                    Dust.NewDust(projectile.position, projectile.width, projectile.height, DustID.Torch, Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-4f, 4f), 100, default, 2.5f);
                }

                // Kalkulasi AoE (Area of Effect) Damage
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    float blastRadius = 120f; // Jangkauan area ledakan

                    foreach (Player targetPlayer in Main.player)
                    {
                        if (targetPlayer.active && !targetPlayer.dead && projectile.Distance(targetPlayer.Center) <= blastRadius)
                        {
                            // Scaling Damage: 200+ di Master Mode, 150 Expert, 100 Normal
                            int explosionDamage = Main.masterMode ? Main.rand.Next(200, 250) : (Main.expertMode ? 150 : 100);
                            
                            // Memberikan damage area ke player
                            targetPlayer.Hurt(PlayerDeathReason.ByProjectile(-1, projectile.whoAmI), explosionDamage, 0);
                        }
                    }
                }
            }
        }
    }
}