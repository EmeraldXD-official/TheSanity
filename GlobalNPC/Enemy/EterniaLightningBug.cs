using Microsoft.Xna.Framework;
using System.Collections.Generic;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [PROJECTILE GLOBAL REWORK]: LIGHTNING BUG ZAP (CHAIN MECHANIC)
    // =========================================================================
    public class LightningBugZapChainer : global::Terraria.ModLoader.GlobalProjectile
    {
        // WAJIB: Setiap proyektil memiliki memori targetnya sendiri
        public override bool InstancePerEntity => true;

        // Penanda apakah proyektil ini berhak melakukan Chain (Hanya T3)
        public bool isChainable = false;

        // Memori untuk menyimpan entitas yang sudah terkena serangan
        public List<int> hitPlayers = new List<int>();
        public List<int> hitNPCs = new List<int>();

        // Pengaturan batas Chain agar tidak OP dan menghindari lag
        public int chainCount = 0;
        public int maxChains = 4; // Maksimal memantul 4 kali
        public float chainRadius = 400f; // Jarak radius mencari target baru (400 pixel)

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        {
            return entity.type == ProjectileID.DD2LightningBugZap;
        }

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            // Mengecek apakah tembakan pertama ini murni berasal dari Lightning Bug T3
            if (source is EntitySource_Parent parentSource && parentSource.Entity is NPC npc)
            {
                if (npc.type == NPCID.DD2LightningBugT3)
                {
                    isChainable = true;
                }
            }
        }

        // Memaksa proyektil musuh agar bisa mengenai Target Dummy & NPC Friendly
        public override bool? CanHitNPC(Projectile projectile, NPC target)
        {
            if (!isChainable) return null; // Jika bukan dari T3, biarkan mekanik vanilla berjalan

            // Izinkan mengenai Target Dummy
            if (target.type == NPCID.TargetDummy) return true;

            // Izinkan mengenai Town NPC / Friendly NPC (TAPI KECUALI CRITTER)
            if (target.friendly && !target.CountsAsACritter) return true;

            return base.CanHitNPC(projectile, target);
        }

        // Saat mengenai Player
        public override void OnHitPlayer(Projectile projectile, Player target, Player.HurtInfo info)
        {
            if (!isChainable) return;

            hitPlayers.Add(target.whoAmI); // Catat ID Player agar tidak terkena lagi
            TriggerChain(projectile, target.Center);
        }

        // Saat mengenai NPC (Friendly atau Dummy)
        public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (!isChainable) return;

            hitNPCs.Add(target.whoAmI); // Catat ID NPC agar tidak terkena lagi
            TriggerChain(projectile, target.Center);
        }

        // Logika untuk memantulkan proyektil ke target baru
        private void TriggerChain(Projectile projectile, Vector2 hitPosition)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            if (chainCount >= maxChains) return; // Stop jika sudah mencapai batas maksimum pantulan

            Entity nextTarget = null;
            float closestDistance = chainRadius;
            bool isTargetPlayer = false;

            // 1. CARI PLAYER TERDEKAT (Yang belum terkena Hit)
            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead && !hitPlayers.Contains(p.whoAmI))
                {
                    float dist = Vector2.Distance(hitPosition, p.Center);
                    if (dist < closestDistance)
                    {
                        closestDistance = dist;
                        nextTarget = p;
                        isTargetPlayer = true;
                    }
                }
            }

            // 2. CARI NPC TERDEKAT (Yang belum terkena Hit, dan memenuhi syarat)
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC n = Main.npc[i];
                if (n.active && !hitNPCs.Contains(n.whoAmI))
                {
                    // Syarat: Harus Target Dummy ATAU (Friendly NPC tapi bukan Critter)
                    bool isValidNPC = n.type == NPCID.TargetDummy || (n.friendly && !n.CountsAsACritter);

                    if (isValidNPC)
                    {
                        float dist = Vector2.Distance(hitPosition, n.Center);
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            nextTarget = n;
                            isTargetPlayer = false;
                        }
                    }
                }
            }

            // 3. JIKA TARGET DITEMUKAN -> SPAWN CHAIN PROYEKIL BARU
            if (nextTarget != null)
            {
                Vector2 targetCenter = isTargetPlayer ? ((Player)nextTarget).Center : ((NPC)nextTarget).Center;
                
                // Menghitung arah pantulan dengan mempertahankan kecepatan (velocity) asli proyektil
                Vector2 newVelocity = (targetCenter - hitPosition).SafeNormalize(Vector2.Zero) * projectile.velocity.Length();

                int projID = Projectile.NewProjectile(
                    projectile.GetSource_FromThis(),
                    hitPosition,      // Mulai dari titik benturan
                    newVelocity,      // Menuju target baru
                    projectile.type,  // Tetap proyektil Zap
                    projectile.damage, 
                    projectile.knockBack,
                    projectile.owner
                );

                // 4. TRANSFER MEMORI KE PROYEKTIL BARU AGAR TIDAK MENGULANG TARGET
                if (Main.projectile[projID].TryGetGlobalProjectile(out LightningBugZapChainer nextChainer))
                {
                    nextChainer.isChainable = true; // Pastikan proyektil anak tetap bisa nge-chain
                    nextChainer.chainCount = this.chainCount + 1; // Tambah jumlah urutan chain
                    
                    // Copy daftar entitas yang sudah di-hit
                    nextChainer.hitPlayers = new List<int>(this.hitPlayers);
                    nextChainer.hitNPCs = new List<int>(this.hitNPCs);
                }
            }
        }
    }
}