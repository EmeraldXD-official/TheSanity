using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using System;
using System.Collections.Generic;

namespace TheSanity.GlobalNPC.Enemy
{
    public class VampireHealMechanism : global::Terraria.ModLoader.GlobalNPC
    {
        // Wajib diset true agar setiap individu Vampire memiliki list orb-nya sendiri-sendiri
        public override bool InstancePerEntity => true;

        // Structure data orb buatanmu yang super ringan
        private class HealingOrb
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public NPC TargetNPC;     
            public int HealAmount;
            public bool IsToSelf;      
        }

        private List<HealingOrb> activeOrbs = new List<HealingOrb>();

        // =========================================================================
        // 1. DETEKSI CONTACT DAMAGE (ON HIT PLAYER)
        // =========================================================================
        public override void OnHitPlayer(NPC npc, Player target, Player.HurtInfo hurtInfo)
        {
            // Filter hanya untuk Vampire manusia dan Vampire kelelawar bawaan game
            if (npc.type == NPCID.Vampire || npc.type == NPCID.VampireBat)
            {
                float detectionRadius = 500f; // Jarak scan musuh lain terdekat
                List<NPC> injuredEnemies = new List<NPC>();

                // Scan musuh lain di sekitar yang darahnya sedang sekarat/berkurang
                for (int i = 0; i < Main.maxNPCs; i++)
                {
                    NPC possibleEnemy = Main.npc[i];
                    if (possibleEnemy.active && !possibleEnemy.friendly && possibleEnemy.whoAmI != npc.whoAmI && possibleEnemy.lifeMax > 5)
                    {
                        if (possibleEnemy.life < possibleEnemy.lifeMax)
                        {
                            if (Vector2.Distance(npc.Center, possibleEnemy.Center) <= detectionRadius)
                            {
                                injuredEnemies.Add(possibleEnemy);
                            }
                        }
                    }
                }

                // [BALANCING LOCATION 1: JUMLAH ORB & NILAI HEAL]
                // Sesuai request: Muncrat 3 orb per contact damage, masing-masing menyembuhkan 3 HP
                for (int i = 0; i < 3; i++)
                {
                    Vector2 burstVelocity = Main.rand.NextVector2Circular(5f, 5f);
                    HealingOrb orb = new HealingOrb
                    {
                        Position = npc.Center,
                        Velocity = burstVelocity,
                        HealAmount = 3 
                    };

                    // KONDISI SMART HEAL: Jika HP diri sendiri sudah penuh saat menabrak player, 
                    // langsung alihkan target orb ke musuh lain yang membutuhkan
                    if (npc.life >= npc.lifeMax && injuredEnemies.Count > 0)
                    {
                        orb.TargetNPC = injuredEnemies[Main.rand.Next(injuredEnemies.Count)];
                        orb.IsToSelf = false;
                    }
                    else
                    {
                        orb.TargetNPC = npc;
                        orb.IsToSelf = true;
                    }

                    activeOrbs.Add(orb);
                }
            }
        }

        // =========================================================================
        // 2. RUNNING UPDATE ORB (POST AI TICK)
        // =========================================================================
        public override void PostAI(NPC npc)
        {
            if (npc.type == NPCID.Vampire || npc.type == NPCID.VampireBat)
            {
                UpdateVampireOrbs(npc);
            }
        }

        private void UpdateVampireOrbs(NPC npc)
        {
            for (int i = activeOrbs.Count - 1; i >= 0; i--)
            {
                HealingOrb orb = activeOrbs[i];

                // Jika target aslinya keburu mati di tengah jalan, pulangkan orb ke diri sendiri
                if (orb.TargetNPC == null || !orb.TargetNPC.active)
                {
                    orb.TargetNPC = npc;
                    orb.IsToSelf = true;
                }

                // DYNAMIC SWITCHING: Jika orb awalnya mau kesini (IsToSelf), tapi di tengah jalan HP diri sendiri 
                // mendadak penuh (akibat kiriman orb sebelumnya), belokkan orb secara realtime ke musuh lain!
                if (orb.IsToSelf && npc.life >= npc.lifeMax)
                {
                    NPC alternativeTarget = FindNearbyInjuredEnemy(npc, 500f);
                    if (alternativeTarget != null)
                    {
                        orb.TargetNPC = alternativeTarget;
                        orb.IsToSelf = false;
                    }
                }

                // Pergerakan Homing mengejar TargetNPC
                Vector2 desiredDirection = orb.TargetNPC.Center - orb.Position;
                float distance = desiredDirection.Length();

                if (distance > 0f)
                {
                    desiredDirection.Normalize();
                    
                    // [BALANCING LOCATION 2: KECEPATAN TERBANG ORB]
                    float speed = 7.5f; 
                    orb.Velocity = Vector2.Lerp(orb.Velocity, desiredDirection * speed, 0.15f);
                }

                orb.Position += orb.Velocity;

                // Efek visual partikel merah terang (VampireHeal)
                Dust d = Dust.NewDustPerfect(orb.Position, DustID.VampireHeal, Vector2.Zero, 0, Color.Red, 1.3f);
                d.noGravity = true;

                // [BALANCING LOCATION 3: KECERAHAN CAHAYA ORB]
                // Menambahkan pendaran cahaya merah konstan di area koordinat posisi orb (R, G, B)
                Lighting.AddLight(orb.Position, 0.9f, 0.0f, 0.0f);

                // Hitbox bersentuhan dengan target (jarak di bawah 14 pixel)
                if (distance < 14f)
                {
                    NPC finalTarget = orb.TargetNPC;

                    finalTarget.life += orb.HealAmount;
                    if (finalTarget.life > finalTarget.lifeMax)
                    {
                        finalTarget.life = finalTarget.lifeMax;
                    }

                    // Teks indikator hijau khas penambahan HP (+3)
                    finalTarget.HealEffect(orb.HealAmount);

                    // Efek cipratan darah saat berhasil diserap masuk ke tubuh musuh
                    for (int j = 0; j < 5; j++)
                    {
                        Dust.NewDust(finalTarget.position, finalTarget.width, finalTarget.height, DustID.CrimsonSpray, 0f, -1.5f);
                    }

                    activeOrbs.RemoveAt(i);
                }
            }
        }

        // Fungsi pembantu pencarian target alternatif secara realtime
        private NPC FindNearbyInjuredEnemy(NPC npc, float radius)
        {
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC target = Main.npc[i];
                if (target.active && !target.friendly && target.whoAmI != npc.whoAmI && target.life < target.lifeMax && target.lifeMax > 5)
                {
                    if (Vector2.Distance(npc.Center, target.Center) <= radius)
                    {
                        return target;
                    }
                }
            }
            return null;
        }

        // =========================================================================
        // 3. MEMBERSIHKAN LIST SAAT NPC MATI
        // =========================================================================
        public override void HitEffect(NPC npc, NPC.HitInfo hit)
        {
            if ((npc.type == NPCID.Vampire || npc.type == NPCID.VampireBat) && npc.life <= 0)
            {
                activeOrbs.Clear(); // Menghapus sisa cache peluru agar tidak bocor memori
            }
        }
    }
}