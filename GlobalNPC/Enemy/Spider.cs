using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class SpiderRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;
        // Timer untuk laba-laba yang aslinya tidak bisa menembak
        public int shootTimer = 0;

        public override void PostAI(NPC npc)
        {
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }
            
            // FIX: Ditambahkan pengaman 'target == null' agar Portal OOA dan NPC pasif tidak memicu NullReferenceException
            if (target == null || !target.active || target.dead || npc.Distance(target.Center) > 600f) return;

            // --- 1. LOGIKA MENEMBAK UNTUK BLOOD CRAWLER & WALL CREEPER ---
            // ID: 240, 239 (Blood) | 164, 165 (Wall)
            bool isBasicSpider = npc.type == 239 || npc.type == 240 || npc.type == 164 || npc.type == 165;

            if (isBasicSpider)
            {
                shootTimer++;
                // LOKASI SPEED TEMBAK: 120 frame = 2 detik sekali
                if (shootTimer >= 120) 
                {
                    if (Collision.CanHit(npc.position, npc.width, npc.height, target.position, target.width, target.height))
                    {
                        Vector2 shootVel = (target.Center - npc.Center).SafeNormalize(Vector2.Zero) * 7f;
                        // Tembak Projectile 472 (Web Spit)
                        int p = Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, 472, 10, 1f, Main.myPlayer);
                        Main.projectile[p].hostile = true;
                        Main.projectile[p].friendly = false;
                    }
                    shootTimer = 0;
                }
            }

            // --- 2. KHUSUS SAND POACHER (530, 531) ---
            // Dia juga kita buat menembak karena aslinya cuma lari
            if (npc.type == 530 || npc.type == 531)
            {
                shootTimer++;
                if (shootTimer >= 100) // Sedikit lebih cepat dari laba-laba biasa
                {
                    Vector2 shootVel = (target.Center - npc.Center).SafeNormalize(Vector2.Zero) * 8f;
                    Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, shootVel, 472, 20, 1f, Main.myPlayer);
                    shootTimer = 0;
                }
            }
        }
    }

    public class SpiderProjectileRework : GlobalProjectile
    {
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo hurtInfo)
        {
            // --- 3. CEK PROJECTILE WEB SPIT (472) ---
            if (projectile.type == 472)
            {
                // Cari siapa pemilik proyektil ini (NPC apa yang nembak)
                NPC owner = null;
                foreach (NPC n in Main.npc)
                {
                    if (n.active && n.Distance(projectile.Center) < 1000f) // Cari NPC terdekat yang mungkin nembak
                    {
                        owner = n;
                        break;
                    }
                }

                if (owner != null)
                {
                    // Blood Crawler (239, 240) -> Bleeding (30) selama 5 detik
                    if (owner.type == 239 || owner.type == 240)
                    {
                        target.AddBuff(30, 300);
                    }
                    // Wall Creeper (164, 165) -> Poisoned (20)
                    else if (owner.type == 164 || owner.type == 165)
                    {
                        target.AddBuff(20, 300);
                    }
                    // Black Recluse (163, 238), Jungle Creeper (236, 237), Sand Poacher (530, 531) -> Venom (70)
                    else if (owner.type == 163 || owner.type == 238 || owner.type == 236 || owner.type == 237 || owner.type == 530 || owner.type == 531)
                    {
                        target.AddBuff(70, 300);
                    }
                }
            }
        }
    }
}