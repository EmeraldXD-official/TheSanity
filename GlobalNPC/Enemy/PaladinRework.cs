using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC REWORK SYSTEM]: PALADIN CONTACT DAMAGE & INFERNO SKILL
    // =========================================================================
    public class PaladinRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        private int infernoTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Paladin;
        }

        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.Paladin) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active) return;

            infernoTimer++;

            if (infernoTimer >= 600)
            {
                infernoTimer = 0;
                float blastSpeed = 6f;
                int blastDamage = 40; 

                Vector2[] directions = new Vector2[]
                {
                    new Vector2(1, 0),   
                    new Vector2(0, 1),   
                    new Vector2(-1, 0),  
                    new Vector2(0, -1)   
                };

                for (int i = 0; i < 4; i++)
                {
                    Vector2 velocity = directions[i] * blastSpeed;

                    int p = Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        velocity,
                        ProjectileID.InfernoHostileBlast,
                        blastDamage,
                        4f,
                        Main.myPlayer
                    );

                    if (p != Main.maxProjectiles)
                    {
                        Main.projectile[p].hostile = true;
                        Main.projectile[p].friendly = false;
                    }
                }

                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item20, npc.Center);
                npc.netUpdate = true;
            }
        }

        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            if (npc.type == NPCID.Paladin)
            {
                int debuffDuration = 900; 

                if (Main.masterMode)
                {
                    debuffDuration = 900 / 3; 
                }
                else if (Main.expertMode)
                {
                    debuffDuration = 900 / 2; 
                }

                target.AddBuff(BuffID.BrokenArmor, debuffDuration);
                target.AddBuff(ModContent.BuffType<Buff.icantfly>(), debuffDuration);
                target.AddBuff(ModContent.BuffType<Buff.falling>(), debuffDuration);
            }
        }

        // =========================================================================
        // [GLOBAL SUMMON NERF LOCATION]: MEMOTONG 80% DAMAGE MINION SAAT LAWAN PALADIN
        // =========================================================================
        public override void ModifyHitByProjectile(NPC npc, Projectile projectile, ref NPC.HitModifiers modifiers)
        {
            if (npc.type == NPCID.Paladin)
            {
                if (projectile.minion || projectile.sentry || projectile.DamageType == DamageClass.Summon || ProjectileID.Sets.MinionShot[projectile.type])
                {
                    modifiers.FinalDamage *= 0.20f;
                }
            }
        }
    }

    // =========================================================================
    // [PROJECTILE REWORK SYSTEM]: HAMMER MODIFIER & GLOBAL PROJECTILE REFLECT
    // =========================================================================
    public class PaladinsHammerModifier : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        private bool hasBeenReflected = false;
        private bool hasRolledReflect = false; // Pengunci mekanik pemicu agar peluang murni 10%

        public override bool AppliesToEntity(Projectile projectile, bool lateInstantiation)
        {
            return projectile.type == ProjectileID.PaladinsHammerHostile || projectile.friendly;
        }

        // =========================================================================
        // [REFLECT & HOMING REMOVAL MECHANICAL]: LOGIKA BALIK SERANGAN & ANTI-BUG
        // =========================================================================
        public override bool PreAI(Projectile projectile)
        {
            if (projectile.hostile || hasBeenReflected) return true;

            if (projectile.minion || projectile.sentry || projectile.DamageType == DamageClass.Summon || ProjectileID.Sets.MinionShot[projectile.type]) 
                return true;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == NPCID.Paladin)
                {
                    Rectangle paladinShieldArea = new Rectangle((int)npc.position.X - 10, (int)npc.position.Y - 10, npc.width + 20, npc.height + 20);
                    
                    if (projectile.Hitbox.Intersects(paladinShieldArea))
                    {
                        if (projectile.aiStyle == 9 || projectile.aiStyle == 122) 
                        {
                            projectile.Kill();
                            return false;
                        }

                        // Memastikan pengundian chance hanya terjadi SATU KALI per proyektil
                        if (!hasRolledReflect)
                        {
                            hasRolledReflect = true;

                            // =========================================================================
                            // [GUIDE & BALANCING LOKASI: PERSENTASE CHANCE REFLECT / PANTULAN]
                            // =========================================================================
                            // Menggunakan NextFloat() agar kalkulasi persentase desimal jauh lebih presisi.
                            // 0.10f = 10% Peluang (Sama seperti Shimmer Slime / Legendary Mode Bosses)
                            // Jika ingin diubah ke 25%, silakan ganti angkanya menjadi 0.25f.
                            // =========================================================================
                            if (Main.rand.NextFloat() < 0.10f)
                            {
                                hasBeenReflected = true;

                                // 1. BALIKKAN VEKTOR GERAK
                                projectile.velocity = -projectile.velocity;

                                // 2. BAJAK OTORITAS TIM MUSUH
                                projectile.friendly = false;
                                projectile.hostile = true;

                                // -------------------------------------------------------------------------
                                // [DAMAGE NERF CONTROL LOCATION]: 
                                // Mengunci base damage pantulan ke angka 20 agar player tidak mati instan!
                                // -------------------------------------------------------------------------
                                projectile.damage = 20; 

                                Terraria.Audio.SoundEngine.PlaySound(SoundID.NPCHit4, npc.Center);
                                
                                for (int d = 0; d < 10; d++)
                                {
                                    Dust dust = Dust.NewDustDirect(projectile.position, projectile.width, projectile.height, DustID.GoldCoin, 0f, 0f, 100, default, 1.2f);
                                    dust.velocity = Main.rand.NextVector2Circular(4f, 4f);
                                    dust.noGravity = true;
                                }

                                projectile.netUpdate = true;
                                return true; 
                            }
                        }
                    }
                }
            }

            return true;
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (projectile.type == ProjectileID.PaladinsHammerHostile)
            {
                int debuffDuration = 900; 

                if (Main.masterMode)
                {
                    debuffDuration = 900 / 3; 
                }
                else if (Main.expertMode)
                {
                    debuffDuration = 900 / 2; 
                }

                target.AddBuff(BuffID.BrokenArmor, debuffDuration);
                target.AddBuff(ModContent.BuffType<Buff.icantfly>(), debuffDuration);
                target.AddBuff(ModContent.BuffType<Buff.falling>(), debuffDuration);
            }
        }
    }
}