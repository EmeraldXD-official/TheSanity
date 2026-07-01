using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using System;

namespace TheSanity
{
    public class DemonRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Timer custom untuk mengatur jeda serangan Scythe baru kita
        private int scytheAttackTimer = 0;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            // Berlaku untuk Demon biasa (62) dan Voodoo Demon (66)
            return entity.type == NPCID.Demon || entity.type == NPCID.VoodooDemon;
        }

        public override void SetDefaults(NPC npc)
        {
            if (npc.type == NPCID.Demon || npc.type == NPCID.VoodooDemon)
            {
                // Membuat tubuh Demon bisa menembus block/dinding seutuhnya
                npc.noTileCollide = true;
            }
        }

        // --- SISTEM DROP ITEM CUSTOM (25% Peluang Drop Item ID: 267) ---
        public override void ModifyNPCLoot(NPC npc, NPCLoot npcLoot)
        {
            if (npc.type == NPCID.Demon)
            {
                // ItemID.GuideVoodooDoll adalah ID 267
                // BALANCING GUIDE: 4 artinya 1 dari 4 kesempatan (25%), berjumlah 1 biji
                npcLoot.Add(Terraria.GameContent.ItemDropRules.ItemDropRule.Common(ItemID.GuideVoodooDoll, 4, 1, 1));
            }
        }

        public override bool PreAI(NPC npc)
        {
            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (!target.active || target.dead) return true;

            // Jalankan AI pergerakan terbang vanilla agar mereka tetap mengejar player dengan luwes
            // Tapi kita paksa velocity-nya agar tidak terhambat block karena noTileCollide aktif
            npc.aiAction = 0; 

            // --- OVERRIDE ATTACK: 6-WAY DEMON SCYTHE ---
            scytheAttackTimer++;
            
            // BALANCING GUIDE: Jeda waktu tembak (120 frame = setiap 2 detik sekali)
            if (scytheAttackTimer >= 120)
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // BALANCING GUIDE: Kecepatan laju proyektil Scythe (5f) dan Damage (22)
                    float scytheSpeed = 5f;
                    int damage = 22; 

                    // Membagi lingkaran (360 derajat) menjadi 6 arah secara presisi
                    for (int i = 0; i < 6; i++)
                    {
                        float angle = i * (MathHelper.TwoPi / 6f);
                        Vector2 velocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * scytheSpeed;

                        // Tembakkan Demon Scythe (ProjectileID.DemonScythe)
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, velocity, ProjectileID.DemonSickle, damage, 1f, Main.myPlayer);
                    }
                }

                scytheAttackTimer = 0;
            }

            // Mematikan kode serangan bawaan (ai[1]) milik vanilla agar dia tidak menembakkan Scythe normalnya secara acak
            if (npc.ai[1] > 0f)
            {
                npc.ai[1] = 0f; 
            }

            return true; // Tetap return true agar AI terbang bawaan underworld-nya tidak hilang
        }
    }
}