using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Common
{
    // =========================================================================
    // [GUIDE SYSTEM: ANTI-INFIGHTING & SWARM FACTION CONTROL]
    // - Menghentikan total damage antar sesama enemy (Fix Bug Faksi Vanilla).
    // - Memutus rantai salah target (Aggro Glitch) pada lebah kecil.
    // - Memberikan efek anti-clumping (lebah tidak akan menyatu/menempel di badan boss).
    // =========================================================================
    
    public class EnemyInfightingFix : global::Terraria.ModLoader.GlobalNPC
    {
        // 1. MENGONTROL KONTAK FISIK DAMAGE ANTAR ENEMY
        public override bool CanHitNPC(NPC npc, NPC target)
        {
            if (!npc.friendly && !target.friendly)
            {
                return false; // Blokir total! Musuh tidak boleh menyakiti sesama musuh.
            }
            return true;
        }

        // 2. MEMUTUS LOCK AGGRO NYASAR PADA LEBAH
        public override bool PreAI(NPC npc)
        {
            // Cek spesifik untuk lebah kecil hostile (lebah vanilla / lebah dari bom)
            if ((npc.type == NPCID.Bee || npc.type == NPCID.BeeSmall || npc.type == NPCID.Hornet) && !npc.friendly)
            {
                // -------------------------------------------------------------
                // [GUIDE SYSTEM: FORCED PLAYER TARGET]
                // - Memaksa lebah untuk selalu mengunci Player terdekat setiap frame.
                // - Ini mencegah AI-nya terdistraksi atau nge-lock enemy lain saat terluka.
                // -------------------------------------------------------------
                npc.TargetClosest(true);
            }
            return true; // Izinkan AI dasar berjalan setelah dikunci ke player
        }

        // 3. SISTEM SOCIAL DISTANCING (ANTI MENYATU / CLUMPING)
        public override void PostAI(NPC npc)
        {
            // Cek jika ini adalah kawanan lebah musuh
            if ((npc.type == NPCID.Bee || npc.type == NPCID.BeeSmall || npc.type == NPCID.Hornet) && !npc.friendly)
            {
                // Lakukan scanning ke seluruh NPC yang sedang aktif di map
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC other = Main.npc[i];

                    // Jika menemukan NPC lain (bukan dirinya sendiri) dan sesama makhluk musuh
                    if (other.active && other.whoAmI != npc.whoAmI && !other.friendly)
                    {
                        float distance = Vector2.Distance(npc.Center, other.Center);
                        
                        // -------------------------------------------------------------
                        // [GUIDE BALANCING: RADIUS SOSIAL DISTANCING LEBAH]
                        // - minDistance : Jarak minimal lebah dengan enemy/boss lain (dalam piksel).
                        // - Jika targetnya Queen Bee, beri jarak lebih besar (85px) biar lebah 
                        //   tidak masuk/menempel ke dalam sprite Queen Bee yang besar.
                        // -------------------------------------------------------------
                        float minDistance = 40f; 
                        if (other.type == NPCID.QueenBee)
                        {
                            minDistance = 85f; 
                        }

                        // Jika lebah terlalu dekat atau mau menyatu dengan enemy tersebut
                        if (distance < minDistance && distance > 0f)
                        {
                            // Kalkulasi arah dorong menjauh dari pusat enemy tersebut
                            Vector2 pushAway = (npc.Center - other.Center).SafeNormalize(Vector2.Zero);
                            
                            // Tambahkan sedikit velocity menjauh agar lebah menyebar secara natural
                            npc.velocity += pushAway * 0.28f; 
                        }
                    }
                }
            }
        }
    }

    public class ProjectileInfightingFix : GlobalProjectile
    {
        // MENGONTROL DAMAGE PROYEKTIL ANTAR ENEMY
        public override bool? CanHitNPC(Projectile projectile, NPC target)
        {
            if (!target.friendly)
            {
                if (projectile.hostile)
                {
                    return false; // Peluru/serangan musuh tembus lewat sesama musuh tanpa melukai.
                }
            }
            return null;
        }
    }
}