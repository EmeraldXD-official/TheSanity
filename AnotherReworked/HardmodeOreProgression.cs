using System.IO;
using Terraria;
using Terraria.Chat;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;
using Microsoft.Xna.Framework;
using Terraria.DataStructures;

namespace TheSanity
{
    public class HardmodeOreProgression : ModSystem
    {
        public bool spawnedTier1;
        public bool spawnedTier2;
        public bool spawnedTier3;
        public bool spawnedPlanteraLoot;
        public bool mech3MessageSent;

        // 1. MEMBAJAK SISTEM ALTAR VANILLA
        public override void Load()
        {
            On_WorldGen.SmashAltar += CustomSmashAltar;
        }

        public override void Unload()
        {
            On_WorldGen.SmashAltar -= CustomSmashAltar;
        }

        private void CustomSmashAltar(On_WorldGen.orig_SmashAltar orig, int i, int j)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            if (!WorldGen.noTileActions && !WorldGen.gen)
            {
                // Hancurkan altar secara manual tanpa memanggil gen Ore/Text Vanilla
                WorldGen.KillTile(i, j, false, false, false);
                if (!Main.tile[i, j].HasTile && Main.netMode == NetmodeID.Server)
                {
                    NetMessage.SendData(MessageID.TileManipulation, -1, -1, null, 0, i, j);
                }

                // Tetap munculkan Wraith seperti game normal agar player tidak bingung
                if (Main.hardMode)
                {
                    int wraithCount = Main.rand.Next(1, 3);
                    for (int w = 0; w < wraithCount; w++)
                    {
                        int npcIndex = NPC.NewNPC(new EntitySource_TileBreak(i, j), i * 16 + 16, j * 16 + 16, NPCID.Wraith);
                        if (Main.netMode == NetmodeID.Server && npcIndex < Main.maxNPCs)
                        {
                            NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, npcIndex);
                        }
                    }
                }
            }
        }

        public override void ClearWorld()
        {
            spawnedTier1 = false;
            spawnedTier2 = false;
            spawnedTier3 = false;
            spawnedPlanteraLoot = false;
            mech3MessageSent = false;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            if (spawnedTier1) tag["spawnedTier1"] = true;
            if (spawnedTier2) tag["spawnedTier2"] = true;
            if (spawnedTier3) tag["spawnedTier3"] = true;
            if (spawnedPlanteraLoot) tag["spawnedPlanteraLoot"] = true;
            if (mech3MessageSent) tag["mech3MessageSent"] = true;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            spawnedTier1 = tag.ContainsKey("spawnedTier1");
            spawnedTier2 = tag.ContainsKey("spawnedTier2");
            spawnedTier3 = tag.ContainsKey("spawnedTier3");
            spawnedPlanteraLoot = tag.ContainsKey("spawnedPlanteraLoot");
            mech3MessageSent = tag.ContainsKey("mech3MessageSent");
        }

        public override void PostUpdateWorld()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int mechCount = 0;
            if (NPC.downedMechBoss1) mechCount++;
            if (NPC.downedMechBoss2) mechCount++;
            if (NPC.downedMechBoss3) mechCount++;

            // RATE ORE DINAIKKAN SEDIKIT (Dari 2.5 menjadi 3.0)
            if (Main.hardMode && !spawnedTier1)
            {
                spawnedTier1 = true;
                SpawnOrePatch(TileID.Cobalt, 3.0);
                SpawnOrePatch(TileID.Palladium, 3.0);
                BroadcastMessage("Your world has been blessed with [c/FF8800:Palladium] and [c/0055FF:Cobalt]!", new Color(50, 255, 50));
            }

            if (mechCount >= 1 && !spawnedTier2)
            {
                spawnedTier2 = true;
                SpawnOrePatch(TileID.Mythril, 3.0);
                SpawnOrePatch(TileID.Orichalcum, 3.0);
                BroadcastMessage("Your world has been blessed with [c/00AA00:Mythril] and [c/FF66FF:Orichalcum]!", new Color(50, 255, 50));
            }

            if (mechCount >= 2 && !spawnedTier3)
            {
                spawnedTier3 = true;
                SpawnOrePatch(TileID.Titanium, 3.0);
                SpawnOrePatch(TileID.Adamantite, 3.0);
                BroadcastMessage("Your world has been blessed with [c/CCCCCC:Titanium] and [c/AA0000:Adamantite]!", new Color(50, 255, 50));
            }

            if (mechCount >= 3 && !mech3MessageSent)
            {
                mech3MessageSent = true;
                BroadcastMessage("The Purified Bars now drop from the mechanical beasts...", new Color(255, 255, 100));
            }

            if (NPC.downedPlantBoss && !spawnedPlanteraLoot)
            {
                spawnedPlanteraLoot = true;
                BroadcastMessage("The ancient flora of the jungle awakens with vibrant life...", new Color(50, 255, 50));
                
                // RATE CHLOROPHYTE DITURUNKAN (Dari 0.5 menjadi 0.2)
                SpawnJungleOrePatch(TileID.Chlorophyte, 0.2); 
                SpawnLifeFruits(3.0); 
            }
        }

        // ==============================================================================
        // METHOD HELPER: CEK BLACKLIST (DUNGEON DAN JUNGLE TEMPLE)
        // ==============================================================================
        private bool IsInDungeonOrTemple(int x, int y)
        {
            Tile tile = Main.tile[x, y];
            ushort type = tile.TileType;
            ushort wall = tile.WallType;

            return type == TileID.BlueDungeonBrick || type == TileID.GreenDungeonBrick || type == TileID.PinkDungeonBrick ||
                   type == TileID.Spikes || type == TileID.WoodenSpikes ||
                   type == TileID.LihzahrdBrick || type == TileID.LihzahrdAltar ||
                   wall == WallID.BlueDungeonUnsafe || wall == WallID.GreenDungeonUnsafe || wall == WallID.PinkDungeonUnsafe ||
                   wall == WallID.LihzahrdBrickUnsafe;
        }

        private void SpawnOrePatch(ushort tileType, double rateMultiplier)
        {
            int numOres = (int)(Main.maxTilesX * Main.maxTilesY * 0.00025 * rateMultiplier);
            int spawned = 0;
            int attempts = 0;

            while (spawned < numOres && attempts < numOres * 5)
            {
                attempts++;
                int x = WorldGen.genRand.Next(100, Main.maxTilesX - 100);
                // DIGANTI: Menggunakan Main.rockLayer agar tidak bisa spawn di Surface ke atas
                int y = WorldGen.genRand.Next((int)Main.rockLayer, Main.maxTilesY - 150);
                
                Tile tile = Main.tile[x, y];
                if (tile.HasTile)
                {
                    if (IsInDungeonOrTemple(x, y)) continue;

                    ushort type = tile.TileType;
                    bool inRestrictedBiome = type == TileID.SnowBlock || type == TileID.IceBlock || type == TileID.CorruptIce || type == TileID.FleshIce ||
                                             type == TileID.Sand || type == TileID.HardenedSand || type == TileID.Sandstone || type == TileID.Ebonsand || type == TileID.Crimsand ||
                                             type == TileID.Mud || type == TileID.JungleGrass;
                    
                    if (inRestrictedBiome && !WorldGen.genRand.NextBool(3)) continue;
                }

                double strength = WorldGen.genRand.Next(4, 9);
                int steps = WorldGen.genRand.Next(4, 9);
                WorldGen.TileRunner(x, y, strength, steps, tileType);
                spawned++;
            }
        }

        private void SpawnJungleOrePatch(ushort tileType, double rateMultiplier)
        {
            int numOres = (int)(Main.maxTilesX * Main.maxTilesY * 0.00025 * rateMultiplier);
            int spawned = 0;
            int attempts = 0;

            while (spawned < numOres && attempts < numOres * 20)
            {
                attempts++;
                int x = WorldGen.genRand.Next(100, Main.maxTilesX - 100);
                // DIGANTI: Menggunakan Main.rockLayer agar tidak bisa spawn di Surface ke atas
                int y = WorldGen.genRand.Next((int)Main.rockLayer, Main.maxTilesY - 150);
                
                Tile tile = Main.tile[x, y];

                // Cek Blacklist (Dungeon / Lihzahrd Temple)
                if (IsInDungeonOrTemple(x, y)) continue;

                // Hanya akan menimpa MUD (Lumpur), memastikan dia benar-benar spawn di Jungle
                if (tile.HasTile && tile.TileType == TileID.Mud)
                {
                    WorldGen.TileRunner(x, y, WorldGen.genRand.Next(5, 10), WorldGen.genRand.Next(5, 10), tileType);
                    spawned++;
                }
            }
        }

        private void SpawnLifeFruits(double rateMultiplier)
        {
            int numFruits = (int)(Main.maxTilesX * Main.maxTilesY * 0.0001 * rateMultiplier);
            int spawned = 0;
            int attempts = 0;
            
            while (spawned < numFruits && attempts < numFruits * 20)
            {
                attempts++;
                int x = WorldGen.genRand.Next(100, Main.maxTilesX - 100);
                // Life Fruit juga di-lock di underground (rockLayer) ke bawah
                int y = WorldGen.genRand.Next((int)Main.rockLayer, Main.maxTilesY - 150);
                
                Tile tile = Main.tile[x, y];

                if (IsInDungeonOrTemple(x, y)) continue;

                if (tile.HasTile && tile.TileType == TileID.JungleGrass)
                {
                    if (WorldGen.PlaceTile(x, y - 1, TileID.LifeFruit, mute: true, forced: false))
                    {
                        spawned++;
                    }
                }
            }
        }

        private void BroadcastMessage(string text, Color color)
        {
            if (Main.netMode == NetmodeID.Server) ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(text), color);
            else if (Main.netMode == NetmodeID.SinglePlayer) Main.NewText(text, color);
        }
    }
}