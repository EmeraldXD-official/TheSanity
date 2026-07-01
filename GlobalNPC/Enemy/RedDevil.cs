using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class RedDevilRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer internal untuk meluncurkan 5 trisula di punggungnya
        private int attackTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == 156;
        }

        public override bool PreAI(NPC npc)
        {
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (!target.active || target.dead) 
            {
                return true; 
            }

            // [FORCE WALL COLLISION ON]
            npc.noTileCollide = true;

            // --- LOGIKA SPAWN & SERANGAN 5 TRISULA BELAKANG ---
            attackTimer++;

            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                int ownedTridents = 0;
                for (int i = 0; i < Main.maxProjectiles; i++)
                {
                    if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<RedDevilTrident>() && Main.projectile[i].ai[0] == npc.whoAmI)
                    {
                        ownedTridents++;
                    }
                }

                if (ownedTridents < 5)
                {
                    for (int i = 0; i < 5; i++)
                    {
                        // LOKASI DAMAGE TOMBAK KUSTOM: Diatur di angka 18
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ModContent.ProjectileType<RedDevilTrident>(), 18, 1f, Main.myPlayer, npc.whoAmI, i);
                    }
                }
            }

            // BALANCING GUIDE: Mengatur jeda meluncurkan trisula kustom (3 sampai 5 detik)
            int randomLaunchTime = Main.rand.Next(180, 301);
            if (attackTimer >= randomLaunchTime)
            {
                attackTimer = 0;

                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    for (int i = 0; i < Main.maxProjectiles; i++)
                    {
                        if (Main.projectile[i].active && Main.projectile[i].type == ModContent.ProjectileType<RedDevilTrident>() && Main.projectile[i].ai[0] == npc.whoAmI)
                        {
                            Main.projectile[i].localAI[0] = 1f; 
                        }
                    }
                }
            }

            return true; 
        }
    }

    // =========================================================
    // CUSTOM PROJECTILE CODE (5 Trisula Pengawal Punggung)
    // =========================================================
    public class RedDevilTrident : ModProjectile
    {
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.UnholyTridentHostile}";

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.aiStyle = -1; 
            Projectile.penetrate = 1;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false; 
            Projectile.ignoreWater = true;
            Projectile.scale = 0.85f; 
        }

        public override void AI()
        {
            NPC owner = Main.npc[(int)Projectile.ai[0]];
            int slotIndex = (int)Projectile.ai[1]; 

            if (!owner.active || owner.type != 156)
            {
                Projectile.Kill();
                return;
            }

            Player target = Main.player[owner.target];
            if (target == null || !target.active || target.dead) return;

            if (Projectile.localAI[0] == 0f)
            {
                float baseRotation = (owner.direction == 1) ? MathHelper.Pi : 0f;
                float spreadAngle = MathHelper.ToRadians(25f);
                float finalRotation = baseRotation + (spreadAngle * (slotIndex - 2f));

                Vector2 offset = new Vector2((float)Math.Cos(finalRotation), (float)Math.Sin(finalRotation)) * 48f;
                Projectile.Center = owner.Center + offset;

                Vector2 aimVector = target.Center - Projectile.Center;
                Projectile.rotation = aimVector.ToRotation() + MathHelper.PiOver4;

                Projectile.timeLeft = 300; 
            }
            else
            {
                if (Projectile.velocity == Vector2.Zero)
                {
                    int launchDelay = slotIndex * 5;
                    if (Projectile.timeLeft > (300 - launchDelay))
                    {
                        Vector2 holdAim = target.Center - Projectile.Center;
                        Projectile.rotation = holdAim.ToRotation() + MathHelper.PiOver4;
                        return;
                    }

                    Vector2 launchVel = target.Center - Projectile.Center;
                    launchVel.Normalize();
                    
                    // [TRIDENT LAUNCH SPEED LOCATION]
                    launchVel *= 10f; 
                    Projectile.velocity = launchVel;

                    SoundEngine.PlaySound(SoundID.Item8, Projectile.Center); 
                }

                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;

                if (Main.rand.NextBool(3))
                {
                    Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, Projectile.velocity.X * 0.2f, Projectile.velocity.Y * 0.2f);
                }
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.ShadowFlame, 180);
        }
    }

    // =========================================================
    // GLOBAL PROJECTILE: TOTAL NERF HARDCODED BALANCING
    // =========================================================
    public class RedDevilVanillaTridentNerf : GlobalProjectile
    {
        public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
        {
            // Memburu langsung UnholyTridentHostile murni saat menyentuh hitbox player
            if (projectile.type == ProjectileID.UnholyTridentHostile)
            {
                // [CRITICAL VANILLA DAMAGE NERF LOCATION]
                // Kalikan dengan 0 agar perkalian expert/master bawaan vanilla hancur total, 
                // lalu diisi nilai mutlak sebesar 14 (18 dikurangi 20%).
                modifiers.FinalDamage *= 0f;
                modifiers.FinalDamage += 14f;
            }
        }

        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (projectile.type == ProjectileID.UnholyTridentHostile)
            {
                // [VANILLA DEBUFF BALANCING LOCATION]
                target.AddBuff(BuffID.ShadowFlame, 180);
            }
        }
    }
}