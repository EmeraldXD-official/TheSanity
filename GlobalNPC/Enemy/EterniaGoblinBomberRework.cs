using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    // =========================================================================
    // [NPC GLOBAL REWORK]: ETERNIA GOBLIN BOMBER TIER 1, 2, & 3 (STATS)
    // =========================================================================
    public class EterniaGoblinBomberRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.DD2GoblinBomberT1 || 
                   entity.type == NPCID.DD2GoblinBomberT2 || 
                   entity.type == NPCID.DD2GoblinBomberT3;
        }

        public override void SetDefaults(NPC npc)
        {
            // =========================================================================
            // [GUIDE & BALANCING LOKASI: KNOCKBACK RESISTANCE (KETAHANAN DI-PENTAL)]
            // =========================================================================
            // Perhitungan Knockback Vanilla (dihitung terbalik):
            // 1.0f = 0% Imun (Pental penuh)
            // 0.7f = 30% Imun
            // 0.4f = 60% Imun
            // =========================================================================

            if (npc.type == NPCID.DD2GoblinBomberT2)
            {
                // TIER 2: Imun 30% Knockback
                npc.knockBackResist = 0.7f; 
            }
            else if (npc.type == NPCID.DD2GoblinBomberT3)
            {
                // TIER 3: Imun 60% Knockback
                npc.knockBackResist = 0.4f; 
            }
        }
    }

    // =========================================================================
    // [PROJECTILE REWORK]: MULTI-BOMB SPREAD SYSTEM (SYNC DENGAN TIMING VANILLA)
    // =========================================================================
    public class BomberProjectileDuplicator : global::Terraria.ModLoader.GlobalProjectile
    {
        // Variabel keamanan (Safety Lock) agar tidak terjadi Infinite Loop 
        // saat mod kita membuat bomb tambahan
        public static bool isSpawningExtra = false;

        public override void OnSpawn(Projectile projectile, IEntitySource source)
        {
            if (isSpawningExtra) return;

            // Memastikan yang sedang dibuat oleh game adalah Bomb dari Goblin DD2
            if (projectile.type == ProjectileID.DD2GoblinBomb)
            {
                // Mengecek apakah entitas yang melempar bomb tersebut adalah seorang NPC
                if (source is EntitySource_Parent parentSource && parentSource.Entity is NPC npc)
                {
                    // =========================================================================
                    // [GUIDE & BALANCING LOKASI: JUMLAH EXTRA BOMB]
                    // =========================================================================
                    // Karena Vanilla AI sudah pasti melempar 1 bomb bawaan, kita hanya 
                    // perlu menambahkan "sisa" kekurangannya agar totalnya pas.
                    // T1: Target 2 Bomb (Vanilla 1 + Extra 1)
                    // T2: Target 3 Bomb (Vanilla 1 + Extra 2)
                    // T3: Target 4 Bomb (Vanilla 1 + Extra 3) <-- Ubah di sini jika ingin 5 atau 6
                    // =========================================================================
                    
                    int extraBombs = 0;
                    if (npc.type == NPCID.DD2GoblinBomberT1) extraBombs = 1;
                    else if (npc.type == NPCID.DD2GoblinBomberT2) extraBombs = 2;
                    else if (npc.type == NPCID.DD2GoblinBomberT3) extraBombs = 3; 

                    if (extraBombs > 0)
                    {
                        // Kunci pintu pelacakan agar Extra Bomb tidak menggandakan dirinya sendiri
                        isSpawningExtra = true;

                        for (int i = 0; i < extraBombs; i++)
                        {
                            // =========================================================================
                            // [GUIDE & BALANCING LOKASI: KECEPATAN & SPREAD (SEBARAN LEMPARAN)]
                            // =========================================================================
                            // Jika velocity tidak diubah, semua bomb akan bertumpuk menyatu menjadi 1 gambar.
                            // Kita tambahkan sedikit dorongan acak agar bomb tersebar di udara (spread).
                            // Ubah batas angka NextFloat untuk melebarkan/menyempitkan tebaran bomb.
                            // =========================================================================
                            
                            Vector2 spreadVelocity = projectile.velocity;
                            
                            // Menambahkan geseran arah kiri/kanan secara acak
                            spreadVelocity.X += Main.rand.NextFloat(-2.0f, 2.0f);
                            
                            // Menambahkan sedikit pantulan agar ada yang terlempar lebih tinggi/rendah
                            spreadVelocity.Y += Main.rand.NextFloat(-1.5f, 0.5f); 

                            // Meluncurkan ekstra bomb baru!
                            Projectile.NewProjectile(
                                npc.GetSource_FromAI(), // Sumber entitas (Goblin)
                                projectile.Center,      // Muncul persis di tengah bomb original (di tangan)
                                spreadVelocity,         // Kecepatan yang sudah diselewengkan
                                ProjectileID.DD2GoblinBomb, 
                                projectile.damage, 
                                projectile.knockBack, 
                                Main.myPlayer
                            );
                        }

                        // Buka kembali kuncinya untuk lemparan berikutnya di masa depan
                        isSpawningExtra = false;
                    }
                }
            }
        }
    }
}