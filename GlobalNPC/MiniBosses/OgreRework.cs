using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.DataStructures; // Wajib untuk IEntitySource

namespace TheSanity.GlobalNPC.Enemy
{
    // =========================================================================
    // KELAS 1: REWORK PROYEKTIL (SPIT SPREAD & SMASH ERUPTION)
    // =========================================================================
    public class OgreProjectileRework : GlobalProjectile
    {
        // Fungsi ini akan berjalan tepat saat sebuah proyektil muncul di game
        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            // Cek apakah proyektil ini dilahirkan oleh sebuah NPC (Musuh)
            if (source is EntitySource_Parent parentSource && parentSource.Entity is NPC npc)
            {
                // Cek secara spesifik apakah NPC yang melahirkannya adalah Ogre T2 atau T3
                if (npc.type == NPCID.DD2OgreT2 || npc.type == NPCID.DD2OgreT3)
                {
                    // -----------------------------------------------------------------
                    // [MEKANIK 1: 20% CHANCE OGRE SPIT MENJADI 5 (SPREAD)]
                    // -----------------------------------------------------------------
                    // projectile.ai[1] == 0f adalah pengaman agar Spit tiruannya tidak 
                    // ikut membelah diri lagi (mencegah game crash/infinite loop).
                    if (projectile.type == ProjectileID.DD2OgreSpit && projectile.ai[1] == 0f)
                    {
                        if (Main.rand.Next(100) < 20) // Peluang 20%
                        {
                            // Kita spawn 4 proyektil tambahan (karena yang 1 original sudah ada)
                            for (int i = -2; i <= 2; i++)
                            {
                                if (i == 0) continue; // Skip angka 0, karena itu jalurnya spit original

                                // =========================================================================
                                // [GUIDE & BALANCING LOKASI: SUDUT SPREAD SPIT]
                                // Angka 12f adalah lebar derajat renggangan antar ludah. 
                                // =========================================================================
                                Vector2 spreadVel = projectile.velocity.RotatedBy(MathHelper.ToRadians(i * 12f));
                                
                                // Spawn dengan ai[1] diset 1f sebagai penanda bahwa ini adalah "Clone"
                                int p = Projectile.NewProjectile(source, projectile.Center, spreadVel, projectile.type, projectile.damage, projectile.knockBack, projectile.owner, projectile.ai[0], 1f);
                                
                                Main.projectile[p].hostile = true;
                                Main.projectile[p].friendly = false;
                            }
                        }
                    }

                    // -----------------------------------------------------------------
                    // [MEKANIK 2: OGRE SMASH MEMUNCULKAN DEBRIS & DINAMIT]
                    // -----------------------------------------------------------------
                    if (projectile.type == ProjectileID.DD2OgreSmash)
                    {
                        int dynamiteProjType = ModContent.ProjectileType<HostileDynamite>();

                        // 1. Muncratkan Deerclops Debris
                        // =========================================================================
                        // [GUIDE & BALANCING LOKASI: JUMLAH DEBRIS]
                        // =========================================================================
                        int debrisCount = Main.rand.Next(6, 9); // Acak 6 sampai 8 debris
                        for (int i = 0; i < debrisCount; i++)
                        {
                            // =========================================================================
                            // [GUIDE & BALANCING LOKASI: ARAH MUNCROTAN DEBRIS]
                            // X (-7 ke 7) itu arah kiri-kanan. Y (-12 ke -6) itu arah lemparan ke atas.
                            // =========================================================================
                            Vector2 debrisVel = new Vector2(Main.rand.NextFloat(-7f, 7f), Main.rand.NextFloat(-12f, -6f));
                            
                            int p = Projectile.NewProjectile(source, projectile.Center, debrisVel, ProjectileID.DeerclopsRangedProjectile, projectile.damage, projectile.knockBack, projectile.owner);
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                        }

                        // 2. Muncratkan Hostile Dynamite bersamaan
                        // =========================================================================
                        // [GUIDE & BALANCING LOKASI: JUMLAH DINAMIT]
                        // =========================================================================
                        int dynCount = Main.rand.Next(5, 8); // Acak 5 sampai 7 dinamit
                        for (int i = 0; i < dynCount; i++)
                        {
                            // =========================================================================
                            // [GUIDE & BALANCING LOKASI: ARAH MUNCROTAN DINAMIT]
                            // Sengaja dibikin sedikit lebih tinggi (Y = -14) agar terbang melampaui debris
                            // =========================================================================
                            Vector2 dynVel = new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-14f, -8f));
                            
                            // Base damage dinamit erupsi ini kita patok di angka 85
                            int p = Projectile.NewProjectile(source, projectile.Center, dynVel, dynamiteProjType, 85, 0f, projectile.owner);
                            Main.projectile[p].hostile = true;
                            Main.projectile[p].friendly = false;
                        }
                    }
                }
            }
        }
    }

    // =========================================================================
    // KELAS 2: REWORK STATS NPC (SIZE & REGEN TIER 3)
    // =========================================================================
    public class OgreReworkNPC : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        
        public int regenTimer = 0;

        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.DD2OgreT3)
            {
                // =========================================================================
                // [GUIDE & BALANCING LOKASI: UKURAN FISIK & HITBOX OGRE TIER 3]
                // =========================================================================
                npc.scale = 2f; // Visual ukurannya dikali 2
                npc.width = (int)(npc.width * 1f); // Hitbox ditabrak player dikali 2
                npc.height = (int)(npc.height * 1f); 
            }
        }

        public override void PostAI(NPC npc)
        {
            if (npc.type == NPCID.DD2OgreT3)
            {
                regenTimer++;
                
                // =========================================================================
                // [GUIDE & BALANCING LOKASI: KECEPATAN & JUMLAH REGEN]
                // 180 frame = 3 detik.
                // =========================================================================
                if (regenTimer >= 180)
                {
                    if (npc.life < npc.lifeMax) // Cek agar tidak nambah darah melebihi maksimum
                    {
                        npc.life += 20; // Tambah 20 HP
                        if (npc.life > npc.lifeMax) npc.life = npc.lifeMax;
                        
                        // Memunculkan angka healing warna hijau di atas kepala Ogre ala game RPG
                        npc.HealEffect(20);
                    }
                    regenTimer = 0; // Reset timer
                }
            }
        }
    }
}