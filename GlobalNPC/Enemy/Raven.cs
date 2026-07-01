using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;
using System.Collections.Generic; // WAJIB: Untuk IDictionary

namespace TheSanity
{
    public class ReworkedRaven : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        private int attackTimer = 0;
        private bool isCharging = false;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.Raven;
        }

        // =========================================================================
        // [SPAWN RATE POOL LOCATION]: SPAWN DI SETIAP MALAM & SEMUA BIOME / EVENT
        // =========================================================================
        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            // Syarat mutlak: Hanya perlu waktu MALAM HARI, bebas biome dan bebas event apa pun
            if (!Main.dayTime)
            {
                // Peluang spawn di malam hari
                float ravenSpawnChance = 0.04f;

                // Masukkan Raven ke dalam pool jika belum terdaftar di biome/event aktif tersebut
                if (!pool.ContainsKey(NPCID.Raven))
                {
                    pool.Add(NPCID.Raven, ravenSpawnChance);
                }
            }
        }

        // =========================================================================
        // [AI LOCATION]: CHARGING PARTICLES, 3-SHOT SPREAD, RECOIL
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.Raven) return;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active) return;

            attackTimer++;

            // Setiap rentang 4 detik sekali (240 frame), bersiap menembak
            if (attackTimer >= 240 && !isCharging)
            {
                isCharging = true;
                attackTimer = 0; 
                npc.netUpdate = true;
            }

            // FASE 1: PROSES CHARGING (0.5 DETIK / 30 FRAME)
            if (isCharging)
            {
                npc.velocity *= 0.9f;

                // Memunculkan partikel asap Shadowflame ungu terisap ke Raven
                for (int i = 0; i < 2; i++)
                {
                    Dust d = Dust.NewDustPerfect(npc.Center + Main.rand.NextVector2Circular(15, 15), DustID.Shadowflame, Vector2.Zero, 100, default, 1.3f);
                    d.noGravity = true;
                    d.velocity = (npc.Center - d.position).SafeNormalize(Vector2.Zero) * 2f; 
                }

                // FASE 2: DETIK EKSEKUSI 3 TEMBAKAN
                if (attackTimer >= 30)
                {
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Vector2 shootDir = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                        float bulletSpeed = 8f; 

                        // Tembak 3 bulu menyebar secara simetris (15 derajat)
                        float spreadAngle = MathHelper.ToRadians(15);
                        for (int i = -1; i <= 1; i++)
                        {
                            Vector2 finalVelocity = shootDir.RotatedBy(spreadAngle * i) * bulletSpeed;
                            
                            // Spawn Proyektil bulu hitam kustom
                            Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, finalVelocity, ModContent.ProjectileType<BlackHarpyFeather>(), 15, 1f, Main.myPlayer);
                        }
                    }

                    Terraria.Audio.SoundEngine.PlaySound(SoundID.Item8, npc.position);

                    // FASE 3: RECOIL KNOCKBACK UNTUK RAVEN
                    Vector2 recoilVector = (npc.Center - target.Center).SafeNormalize(Vector2.Zero);
                    npc.velocity += recoilVector * 6f; 

                    isCharging = false;
                    attackTimer = 0; 
                    npc.netUpdate = true;
                }
            }
        }

        // =========================================================================
        // [CONTACT DAMAGE MODIFIER]: BLOKIR DEBUFF ICHOR DARI TABRAKAN FISIK
        // =========================================================================
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            if (npc.type == NPCID.Raven)
            {
                // Paksa hapus debuff Ichor (BuffID 69) dari player jika tabrakan badan menghasilkan debuff gara-gara efek eksternal
                target.ClearBuff(BuffID.Ichor);
            }
        }
    }
}