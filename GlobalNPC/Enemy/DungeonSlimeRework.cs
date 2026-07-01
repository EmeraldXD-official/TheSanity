using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class DungeonSlimeRework : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Variabel pengunci agar serangan hanya keluar 1x tepat saat lepas landas loncat
        private bool hasFiredOnJump = false;

        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.DungeonSlime;
        }

        // =========================================================================
        // [AI REWORK LOCATION]: ADJUSTMENT PENYEBAB DAN KECEPATAN PEMBIDIK JARK 50 BLOK
        // =========================================================================
        public override void AI(NPC npc)
        {
            if (npc.type != NPCID.DungeonSlime) return;
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            npc.TargetClosest(true);
            Player target = null; if (npc.target >= 0 && npc.target < Main.maxPlayers) { target = Main.player[npc.target]; }

            if (target.dead || !target.active) return;

            // --- DETEKSI KONDISI TANAH DAN UDARA ---
            if (npc.velocity.Y == 0f)
            {
                hasFiredOnJump = false; // Reset kunci saat mendarat
            }
            else if (npc.velocity.Y < -2f && !hasFiredOnJump)
            {
                hasFiredOnJump = true; // Kunci agar tidak terjadi loop spam peluru

                int keyProjectileType = ModContent.ProjectileType<GoldenKeyProjectile>();
                int finalDamage = 28; // Balancing damage

                // -------------------------------------------------------------------------
                // [FIX DISTANCE & SPEED BALANCING]: Jangkauan 50 Blok & Kecepatan Super Cepat
                // 50 Blok * 16 Pixel = 800 Pixel Maksimum Jarak Deteksi
                // -------------------------------------------------------------------------
                float maxDetectDistance = 50f * 16f;
                float currentDistance = Vector2.Distance(npc.Center, target.Center);

                if (currentDistance <= maxDetectDistance)
                {
                    // 1. PROJECTILE UTAMA: PEMBIDIK SUPER CEPAT LANGSUNG KE PLAYER
                    Vector2 directDirection = (target.Center - npc.Center).SafeNormalize(Vector2.Zero);
                    
                    // --- SEKARANG DISET JAUH LEBIH CEPAT (Speed: 14f) ---
                    float fastDirectSpeed = 14f; 

                    int pDirect = Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        directDirection * fastDirectSpeed,
                        keyProjectileType,
                        finalDamage,
                        1.5f,
                        Main.myPlayer
                    );

                    if (pDirect != Main.maxProjectiles)
                    {
                        Main.projectile[pDirect].hostile = true;
                        Main.projectile[pDirect].friendly = false;
                    }
                }

                // -------------------------------------------------------------------------
                // 2. PROJECTILE SISA: 5X MUNCRAT KE ATAS (SEBARAN DIRAPATKAN)
                // -------------------------------------------------------------------------
                int spikeCount = 5;
                
                // Sudut dasar murni menghadap ke atas langit vertikal (-90 derajat)
                float baseAngle = MathHelper.ToRadians(-90f); 
                
                // --- AJUSTMENT SPREAD: Dipersempit menjadi hanya 10 derajat agar tidak terlalu melebar ---
                float tightSpread = MathHelper.ToRadians(10f); 

                for (int i = 0; i < spikeCount; i++)
                {
                    // Formasi membagi sudut simetris dari titik tengah atas (-2, -1, 0, 1, 2)
                    float angleOffset = baseAngle + (i - (spikeCount - 1) / 2f) * tightSpread;
                    Vector2 spikeVelocity = new Vector2((float)Math.Cos(angleOffset), (float)Math.Sin(angleOffset));
                    
                    // Kecepatan dorong ke atas
                    float randomSpeed = Main.rand.NextFloat(6f, 9f);
                    Vector2 finalSpikeVel = spikeVelocity * randomSpeed;

                    int pSpike = Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        finalSpikeVel,
                        keyProjectileType,
                        finalDamage,
                        1.2f,
                        Main.myPlayer
                    );

                    if (pSpike != Main.maxProjectiles)
                    {
                        Main.projectile[pSpike].hostile = true;
                        Main.projectile[pSpike].friendly = false;
                    }
                }

                // Efek suara slime
                Terraria.Audio.SoundEngine.PlaySound(SoundID.Item21, npc.Center);

                npc.netUpdate = true;
            }
        }
    }
}