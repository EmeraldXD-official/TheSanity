using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System.Collections.Generic;

namespace TheSanity
{
    public class SkeletonDeathExplosion : global::Terraria.ModLoader.GlobalNPC
    {
        private static readonly HashSet<int> SkeletonIDs = new HashSet<int>
        {
            21, 449, -46, -47, 201, 450, -48, -49, 202, 451, -50, -51, 203, 452, -52, -53, 322, 323, 324, 
            31, -13, -14, 294, 295, 296, 273, 274, 275, 276, 77, -15, 32, 287, 34, 285, 286, 289, 
            277, 278, 279, 280, 481, 566, 567, 283, 284, 281, 282, 172, 269, 270, 271, 272, 
            110, 293, 291, 36, 35, 635, 292, 45, 44, 167, 
            68 // Dungeon Guardian
        };

        public override void OnKill(NPC npc)
        {
            if (SkeletonIDs.Contains(npc.type))
            {
                int boneCount = Main.rand.Next(20, 36);
                bool isGuardian = (npc.type == NPCID.DungeonGuardian || npc.type == 68);

                // Damage 10rb buat Guardian, 25 buat skeleton biasa
                int damage = isGuardian ? 10000 : 25; 

                for (int i = 0; i < boneCount; i++)
                {
                    Vector2 velocity = new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-12f, -18f));
                    
                    int p = Projectile.NewProjectile(npc.GetSource_Death(), npc.Center, velocity, ProjectileID.SkeletonBone, damage, 10f, Main.myPlayer);
                    
                    if (p != Main.maxProjectiles)
                    {
                        Projectile proj = Main.projectile[p];
                        proj.hostile = true;
                        proj.friendly = false;
                        proj.ai[1] = Main.LocalPlayer.position.Y;

                        if (isGuardian)
                        {
                            proj.ArmorPenetration = 999;
                            // Menandai proyektil ini milik Guardian
                            proj.GetGlobalProjectile<BoneBehavior>().isGuardianBone = true;
                        }
                    }
                }
            }
        }
    }

    public class BoneBehavior : GlobalProjectile
    {
        public override bool InstancePerEntity => true;
        public bool isGuardianBone = false;

        public override void AI(Projectile projectile)
        {
            if (projectile.type == ProjectileID.SkeletonBone && projectile.hostile)
            {
                if (projectile.position.Y < projectile.ai[1])
                {
                    projectile.tileCollide = false;
                }
                else
                {
                    projectile.tileCollide = true;
                }

                projectile.rotation += 0.25f;
            }
        }

        // --- CARA ALTERNATIF: SetMaxDamage ---
        public override void ModifyHitPlayer(Projectile projectile, Player target, ref Player.HurtModifiers modifiers)
        {
            if (isGuardianBone)
            {
                // Jika ini adalah tulang Guardian, kita paksa damage minimum yang masuk 
                // adalah 10.000. Ini biasanya akan melompati logika dodge pada banyak versi.
                modifiers.SetMaxDamage(10000);
            }
        }
    }
}