using System;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using TheSanity.Projectiles;

namespace TheSanity.GlobalNPCs
{
    public class GolemPhase1Override : global::Terraria.ModLoader.GlobalNPC
    {
        public override bool InstancePerEntity => true;

        // Tracker status lompatan & serangan
        private bool wasAirborne = false;
        public int jumpCounter = 0;
        public int fireballCount = 0;
        public int nextReplaceCount = 5;

        // Wave Management
        private int waveTimer = 0;
        private int waveCounter = 0;
        private Vector2 impactOrigin;

        public override void PostAI(NPC npc)
        {
            if (npc.type == NPCID.Golem)
            {
                // Jika masuk Phase 2, matikan Phase 1 total
                if (NPC.FindFirstNPC(NPCID.GolemHeadFree) != -1)
                    return;

                // DETEKSI LOMPATAN
                if (npc.velocity.Y > 0f)
                {
                    wasAirborne = true;
                }
                // DETEKSI MENDARAT
                else if (npc.velocity.Y == 0f && wasAirborne)
                {
                    wasAirborne = false;
                    OnGolemLand(npc);
                }

                // Jalankan perambatan gelombang jika aktif
                if (waveCounter > 0) 
                {
                    HandleExplosionWave(npc);
                }
            }
        }

        private void OnGolemLand(NPC npc)
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // MEKANIK A: Mengeluarkan DD2OgreSmash tepat di Center tubuh Golem
                Projectile.NewProjectile(
                    npc.GetSource_FromAI(), 
                    npc.Center, 
                    Vector2.Zero, 
                    ProjectileID.DD2OgreSmash, 
                    45, 
                    0f, 
                    Main.myPlayer
                );

                // MEKANIK B: Melempar DeerclopsRangedProjectile ke atas sebanyak 5-7 buah secara acak
                int deerCount = Main.rand.Next(5, 8);
                for (int i = 0; i < deerCount; i++)
                {
                    Vector2 launchVel = new Vector2(Main.rand.NextFloat(-5f, 5f), Main.rand.NextFloat(-12f, -7f));
                    
                    int spike = Projectile.NewProjectile(
                        npc.GetSource_FromAI(), 
                        npc.Bottom + new Vector2(0, -16), 
                        launchVel, 
                        ProjectileID.DeerclopsRangedProjectile, 
                        39, 
                        2f, 
                        Main.myPlayer
                    );

                    if (spike != Main.maxProjectiles)
                    {
                        Main.projectile[spike].GetGlobalProjectile<GolemProjectileMod>().isFromGolem = true;
                    }
                }
            }

            // Inisialisasi gelombang ledakan merambat di lantai
            waveCounter = 1;
            waveTimer = 0;
            impactOrigin = npc.Bottom;
            SoundEngine.PlaySound(SoundID.Item14, npc.position);

            // Mekanik D: Healing saat kedua tangan putus
            if (!NPC.AnyNPCs(NPCID.GolemFistLeft) && !NPC.AnyNPCs(NPCID.GolemFistRight))
            {
                jumpCounter++;
                if (jumpCounter >= 2)
                {
                    jumpCounter = 0;
                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        Projectile.NewProjectile(npc.GetSource_FromAI(), npc.Center, Vector2.Zero, ProjectileID.DD2DarkMageHeal, 0, 0f, Main.myPlayer);
                        npc.life += 2000;
                        if (npc.life > npc.lifeMax) npc.life = npc.lifeMax;
                        npc.netUpdate = true;
                    }
                }
            }
        }

        private void HandleExplosionWave(NPC npc)
        {
            waveTimer++;
            if (waveTimer >= 5) 
            {
                float offset = (waveCounter == 1) ? 0 : (waveCounter - 1) * 75f;
                
                void SpawnExpAtSurface(float xOffset) 
                {
                    Vector2 checkPos = impactOrigin + new Vector2(xOffset, 0);
                    int tileX = (int)(checkPos.X / 16f);
                    int startTileY = (int)(checkPos.Y / 16f);
                    float finalY = checkPos.Y - 20;

                    for (int y = startTileY - 8; y <= startTileY + 8; y++)
                    {
                        if (!WorldGen.InWorld(tileX, y)) continue;

                        Tile tile = Main.tile[tileX, y];
                        Tile tileAbove = Main.tile[tileX, y - 1];

                        // 🔥 FIX UTAMA: Mengubah !tile.inActive() menjadi !tile.IsActuated
                        if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType] && !tile.IsActuated && !tileAbove.HasTile)
                        {
                            finalY = (y * 16f) - 20;
                            break;
                        }
                    }

                    Vector2 finalPos = new Vector2(checkPos.X, finalY);
                    
                    int p = Projectile.NewProjectile(
                        npc.GetSource_FromAI(), 
                        finalPos, 
                        Vector2.Zero, 
                        ProjectileID.DD2ExplosiveTrapT2Explosion, 
                        50, 
                        5f, 
                        Main.myPlayer
                    );

                    if (p != Main.maxProjectiles)
                    {
                        Main.projectile[p].friendly = false;
                        Main.projectile[p].hostile = true;
                    }
                }

                SpawnExpAtSurface(offset);
                if (offset != 0) SpawnExpAtSurface(-offset);

                waveTimer = 0;
                waveCounter++;
                if (waveCounter > 7) waveCounter = 0;
            }
        }
    }
}