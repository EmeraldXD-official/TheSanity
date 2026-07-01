using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic; // Dibutuhkan untuk IDictionary di Spawn Pool
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Projectiles; 

namespace TheSanity.NPCs
{
    public class JungleMimicRework : global::Terraria.ModLoader.GlobalNPC
    {
        // Menentukan bahwa script ini HANYA berjalan dan memodifikasi Jungle Mimic vanilla
        public override bool AppliesToEntity(NPC entity, bool lateInstantiation)
        {
            return entity.type == NPCID.BigMimicJungle;
        }

        public override void EditSpawnPool(IDictionary<int, float> pool, NPCSpawnInfo spawnInfo)
        {
            // Cek kondisi: Harus di dalam Dunia Hardmode, Player berada di Biome Jungle, DAN berada di Underground (Bawah Permukaan)
            if (Main.hardMode && spawnInfo.Player.ZoneJungle && spawnInfo.SpawnTileY > Main.worldSurface)
            {
                // --- LOKASI BALANCING: PERSENTASE SPAWN CHANCE (1%) ---
                float spawnChance = 0.01f; 

                if (!pool.ContainsKey(NPCID.BigMimicJungle))
                {
                    pool.Add(NPCID.BigMimicJungle, spawnChance);
                }
                else
                {
                    pool[NPCID.BigMimicJungle] = spawnChance;
                }
            }
        }

        public override void AI(NPC npc)
        {
            // Mencari target player terdekat
            Player targetPlayer = Main.player[npc.target];
            if (targetPlayer == null || !targetPlayer.active || targetPlayer.dead) return;

            // Menggunakan npc.localAI[1] sebagai global attack timer kita
            npc.localAI[1]++;

            // =========================================================================
            // LOKASI BALANCING TIMING: MEKANIK SERANGAN BERGANTIAN (ANTI-TUMPANG TINDIH)
            // =========================================================================
            
            // ---------------------------------------------------------------------
            // TIMING 1 REVISI: MELEMPAR 5 TENTAKEL SEGALA ARAH + 1 TARGET AIM PLAYER
            // ---------------------------------------------------------------------
            if (npc.localAI[1] == 600) 
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // --- LOKASI BALANCING: KECEPATAN & DAMAGE TENTAKEL PULLER ---
                    float hookSpeed = 15f; // Kecepatan luncur tentakel
                    int tentacleDamage = 1; // Disarankan 1 jika hanya ingin efek tarikan, naikkan jika ingin memberikan damage konstan

                    // 1. TENTAKEL KHUSUS AIM: Mengarah langsung secara presisi ke target player
                    Vector2 aimVelocity = (targetPlayer.Center - npc.Center).SafeNormalize(Vector2.UnitY) * hookSpeed;
                    Projectile.NewProjectile(
                        npc.GetSource_FromAI(),
                        npc.Center,
                        aimVelocity,
                        ModContent.ProjectileType<SoulFisherHook>(),
                        tentacleDamage, 
                        0f,
                        Main.myPlayer,
                        0f,
                        npc.whoAmI 
                    );

                    // 2. 5 TENTAKEL SEGALA ARAH (RANDOM ANTI-TUMPANG TINDIH):
                    // Membagi 360 derajat (2 * Pi) menjadi 5 segmen tetap agar tidak saling menimpa
                    float baseSegment = MathHelper.TwoPi / 5f; 
                    
                    // Membuat awalan sudut rotasi acak universal agar polanya selalu berubah setiap kali dikeluarkan
                    float randomStartOffset = Main.rand.NextFloat(0f, MathHelper.TwoPi);

                    for (int i = 0; i < 5; i++)
                    {
                        // Menghitung sudut dasar per segmen + geseran acak kecil di dalam ruang segmennya sendiri (-12 hingga +12 derajat)
                        float currentAngle = randomStartOffset + (i * baseSegment) + Main.rand.NextFloat(-0.2f, 0.2f);
                        Vector2 burstVelocity = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle)) * hookSpeed;

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            burstVelocity,
                            ModContent.ProjectileType<SoulFisherHook>(),
                            tentacleDamage, 
                            0f,
                            Main.myPlayer,
                            0f,
                            npc.whoAmI 
                        );
                    }
                }
                SoundEngine.PlaySound(SoundID.Item17, npc.Center); // Suara lemparan tali/tentakel hantu
            }

            // ---------------------------------------------------------------------
            // TIMING 2: SPAM SEMBURAN "GoldenShowerHostile" (Jeda 2 Detik Setelah Kail / Frame 720)
            // ---------------------------------------------------------------------
            else if (npc.localAI[1] == 720) 
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // --- LOKASI BALANCING: DAMAGE SEMBURAN GOLDEN SHOWER ---
                    int goldenShowerDamage = 18; 

                    for (int i = 0; i < 5; i++)
                    {
                        Vector2 showerVelocity = (targetPlayer.Center - npc.Center).SafeNormalize(Vector2.UnitY);
                        showerVelocity = showerVelocity.RotatedBy(Main.rand.NextFloat(-0.15f, 0.15f)) * Main.rand.NextFloat(9f, 13f);

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            showerVelocity,
                            ProjectileID.GoldenShowerHostile, 
                            goldenShowerDamage,
                            1f,
                            Main.myPlayer
                        );
                    }
                }
                SoundEngine.PlaySound(SoundID.Item13, npc.Center); // Suara semburan cairan
            }

            // ---------------------------------------------------------------------
            // TIMING 3: MELEMPARKAN "QueenSlimeGelAttack" KE 7 ARAH (Jeda 2 Detik Setelah Semburan / Frame 840)
            // ---------------------------------------------------------------------
            else if (npc.localAI[1] == 840) 
            {
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    // --- LOKASI BALANCING: DAMAGE GEL QUEEN SLIME ---
                    int gelDamage = 25; 
                    float gelSpeed = 8f;

                    float baseRotation = (targetPlayer.Center - npc.Center).ToRotation();
                    float spreadAngle = MathHelper.ToRadians(15f); 

                    for (int i = -3; i <= 3; i++)
                    {
                        float finalRotation = baseRotation + (i * spreadAngle);
                        Vector2 gelVelocity = new Vector2((float)Math.Cos(finalRotation), (float)Math.Sin(finalRotation)) * gelSpeed;

                        Projectile.NewProjectile(
                            npc.GetSource_FromAI(),
                            npc.Center,
                            gelVelocity,
                            ProjectileID.QueenSlimeGelAttack, 
                            gelDamage,
                            1.5f,
                            Main.myPlayer
                        );
                    }
                }
                SoundEngine.PlaySound(SoundID.Roar, npc.Center); // Suara mengaum tanda serangan puncak selesai
            }

            // ---------------------------------------------------------------------
            // SIKLUS RESET TIMER (Frame 870 / Setengah detik setelah serangan terakhir)
            // ---------------------------------------------------------------------
            else if (npc.localAI[1] >= 870)
            {
                npc.localAI[1] = 0; // Kembalikan ke 0 untuk mengulang hitungan
            }

            // =========================================================================
            // REKUES FASE: HP DI BAWAH 60% (REGEN, DEFENSE UP, GLOBAL DAMAGE UP)
            // =========================================================================
            float healthRatio = (float)npc.life / npc.lifeMax;

            if (healthRatio <= 0.60f)
            {
                if (Main.GameUpdateCount % 6 == 0)
                {
                    if (npc.life < npc.lifeMax)
                    {
                        npc.life += 1;
                        npc.HealEffect(1, true); 
                    }
                }

                // --- BEAUTIFIER: EFEK PARTIKEL MERAH DAN BIRU DI SEKITAR BADAN ---
                if (Main.rand.NextBool(3))
                {
                    int dustType = (Main.GameUpdateCount % 2 == 0) ? DustID.Torch : DustID.MagicMirror;
                    Dust d = Dust.NewDustDirect(npc.position, npc.width, npc.height, dustType, 0f, -2f, 100, default, 1.2f);
                    d.noGravity = true;
                    d.velocity *= 0.5f;
                }
            }
        }

        public override void ModifyHoverBoundingBox(NPC npc, ref Rectangle boundingBox)
        {
            float healthRatio = (float)npc.life / npc.lifeMax;
            if (healthRatio <= 0.60f)
            {
                // --- LOKASI BALANCING BONUS DEFENSE ---
                npc.defense = 50; 
            }
            else
            {
                // --- LOKASI BALANCING DEFENSE DEFAULT ---
                npc.defense = 30; 
            }
        }

        public override void ModifyHitPlayer(NPC npc, Player target, ref Player.HurtModifiers modifiers)
        {
            float healthRatio = (float)npc.life / npc.lifeMax;
            if (healthRatio <= 0.60f)
            {
                // --- LOKASI BALANCING: GLOBAL DAMAGE UP +10% ---
                modifiers.SourceDamage *= 1.10f; 
            }
        }

        // =========================================================================
        // PERBAIKAN & BEAUTIFIER: GLOWING EFFECT WARNA MERAH & BIRU DI MIMIC VANILLA
        // =========================================================================
        public override bool PreDraw(NPC npc, SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            float healthRatio = (float)npc.life / npc.lifeMax;

            if (healthRatio <= 0.60f)
            {
                Texture2D texture = TextureAssets.Npc[npc.type].Value;
                Vector2 drawOrigin = npc.frame.Size() * 0.5f;
                Vector2 drawPos = npc.Center - screenPos;

                float pulse = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.5f + 0.5f;
                Color glowColor = Color.Lerp(Color.Red, Color.Cyan, pulse);
                glowColor.A = 0; 

                for (int i = 0; i < 4; i++)
                {
                    Vector2 offsetPos = new Vector2(2f, 0f).RotatedBy(i * MathHelper.PiOver2);
                    spriteBatch.Draw(
                        texture,
                        drawPos + offsetPos,
                        npc.frame,
                        glowColor * 0.6f, 
                        npc.rotation,
                        drawOrigin,
                        npc.scale,
                        npc.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                        0f
                    );
                }
            }

            return true; 
        }
    }
}