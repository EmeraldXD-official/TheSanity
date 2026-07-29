using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC GLOBAL REWORK]: ETERNIA SKELETON TIER 1 & 3 (CUSTOM SUMMON SIGN)
    // =========================================================================
    public class EterniaSkeletonRework : global::Terraria.ModLoader.GlobalNPC
    {
        // WAJIB: Agar setiap skeleton memiliki timer cooldown masing-masing secara independen
        public override bool InstancePerEntity => true;

        // Variabel untuk menghitung waktu (dalam hitungan tick)
        public int summonCooldownTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.DD2SkeletonT1 || 
                   entity.type == NPCID.DD2SkeletonT3;
        }

        public override void AI(NPC npc)
        {
            // Menambah timer setiap frame game berjalan
            summonCooldownTimer++;

            // TIER 1: Cooldown 7 Detik (420 tick) -> Panggil 1 Sign
            if (npc.type == NPCID.DD2SkeletonT1)
            {
                if (summonCooldownTimer >= 420)
                {
                    SpawnSummonSign(npc, 1);
                    summonCooldownTimer = 0; // Reset Cooldown setelah memanggil
                }
            }
            // TIER 3: Cooldown 15 Detik (900 tick) -> Panggil 3 Sign numpuk
            else if (npc.type == NPCID.DD2SkeletonT3)
            {
                if (summonCooldownTimer >= 900)
                {
                    SpawnSummonSign(npc, 3);
                    summonCooldownTimer = 0; // Reset Cooldown setelah memanggil
                }
            }
        }

        // Metode khusus untuk memunculkan projectile
        private void SpawnSummonSign(NPC npc, int amount)
        {
            // Mencegah duplikasi tembakan saat dimainkan di Multiplayer
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            // Kordinat lokasi: Persis di Center NPC, dinaikkan ke atas (minus Y) sekitar 60 pixel
            Vector2 spawnPosition = npc.Center - new Vector2(0, 60f);

            for (int i = 0; i < amount; i++)
            {
                // Memanggil custom projectile milikmu
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(),
                    spawnPosition,
                    Vector2.Zero, // Velocity 0 agar diam mengambang di tempat
                    ModContent.ProjectileType<DarkMageSummonSign>(), // Referensi ke file projectile custom-mu
                    0, // Damage (bisa disesuaikan jika proyektilnya punya efek damage dasar)
                    0, // Knockback
                    Main.myPlayer
                );
            }

            // [Opsional] Visual Effect: Sedikit efek debu agar terlihat seperti melakukan sihir
            for (int d = 0; d < 15; d++)
            {
                Dust.NewDust(spawnPosition - new Vector2(15, 15), 30, 30, DustID.Shadowflame, 0f, -2f, 150, default, 1.5f);
            }
        }
    }
}