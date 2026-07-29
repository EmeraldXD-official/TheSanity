using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.GameContent.Events; // Untuk mendeteksi progress Old One's Army
using System;
using TheSanity.MainMenu.UI; // Namespace tempat SanityShopAPI berada

namespace TheSanity.Systems
{
    public class SunCultistShopSystem : ModSystem
    {
        public override void PostSetupContent() {
            // 1. Ambil ID dari NPC SunCultist kamu
            int targetNPCID = ModContent.NPCType<GlobalNPC.TownNPCs.SunCultist>();

            // 2. FUNGSI PEMBANTU (HARGA RATA 90 SILVER & PEMBELIAN VANILLA ECERAN)
            void RegisterSunItem(int itemId, Func<bool> condition = null) {
                SanityShopAPI.RegisterDLCShopItem(targetNPCID, () => {
                    if (condition != null && !condition()) {
                        return new Item(); 
                    }

                    Item item = new Item();
                    item.SetDefaults(itemId);
                    item.shopCustomPrice = Item.buyPrice(silver: 90); // Kunci mati rata 90 Silver
                    return item;
                });
            }

            // =========================================================================
            // SEKTOR DAFTAR ITEM YANG DIJUAL (URUT BERDASARKAN KATEGORI & PROGRESSION)
            // =========================================================================

            // -------------------------------------------------------------------------
            // [ KATEGORI 1: ANY TIME (POLOS / TANPA SYARAT) ]
            // -------------------------------------------------------------------------
            RegisterSunItem(ItemID.MagmaStone);
            RegisterSunItem(ItemID.ObsidianSkull);
            RegisterSunItem(ItemID.PanicNecklace);
            RegisterSunItem(ItemID.Shackle);
            RegisterSunItem(ItemID.SharkToothNecklace);
            RegisterSunItem(ItemID.FeralClaws);
            RegisterSunItem(ItemID.ObsidianRose);
            RegisterSunItem(ItemID.Aglet);
            RegisterSunItem(ItemID.ClimbingClaws);
            RegisterSunItem(ItemID.CloudinaBottle);
            RegisterSunItem(ItemID.FartinaJar);           
            RegisterSunItem(ItemID.FrogLeg);
            RegisterSunItem(ItemID.HermesBoots);
            RegisterSunItem(ItemID.LavaCharm);
            RegisterSunItem(ItemID.LuckyHorseshoe);
            RegisterSunItem(ItemID.Magiluminescence);
            RegisterSunItem(ItemID.ShinyRedBalloon);
            RegisterSunItem(ItemID.PortableStool);          
            RegisterSunItem(ItemID.TsunamiInABottle);
            RegisterSunItem(ItemID.CreativeWings);          
            RegisterSunItem(ItemID.PlatinumWatch);
            RegisterSunItem(ItemID.DepthMeter);
            RegisterSunItem(ItemID.Compass);
            RegisterSunItem(ItemID.Radar);
            RegisterSunItem(ItemID.LifeformAnalyzer);
            RegisterSunItem(ItemID.MetalDetector);
            RegisterSunItem(ItemID.Stopwatch);
            RegisterSunItem(ItemID.DPSMeter);
            RegisterSunItem(ItemID.FishermansGuide);
            RegisterSunItem(ItemID.WeatherRadio);
            RegisterSunItem(ItemID.Sextant);
            RegisterSunItem(ItemID.BandofRegeneration);     
            RegisterSunItem(ItemID.BandofStarpower);        
            RegisterSunItem(ItemID.CelestialMagnet);
            RegisterSunItem(ItemID.NaturesGift);
            RegisterSunItem(ItemID.AdhesiveBandage);
            RegisterSunItem(ItemID.Bezoar);
            RegisterSunItem(ItemID.Nazar);
            RegisterSunItem(ItemID.HandWarmer);
            RegisterSunItem(ItemID.Toolbox);
            RegisterSunItem(ItemID.PaintSprayer);
            RegisterSunItem(ItemID.ExtendoGrip);
            RegisterSunItem(ItemID.PortableCementMixer);
            RegisterSunItem(ItemID.BrickLayer);
            RegisterSunItem(ItemID.ActuationAccessory);         
            RegisterSunItem(ItemID.WhiteString);
            RegisterSunItem(ItemID.BlackCounterweight);
            RegisterSunItem(ItemID.YellowCounterweight);
            RegisterSunItem(ItemID.BlueCounterweight);
            RegisterSunItem(ItemID.RedCounterweight);
            RegisterSunItem(ItemID.PurpleCounterweight);
            RegisterSunItem(ItemID.GreenCounterweight);
            RegisterSunItem(ItemID.CordageGuide);
            RegisterSunItem(ItemID.JellyfishNecklace);

            // -------------------------------------------------------------------------
            // [ KATEGORI 2: VISITE BIOME (PEMAIN HARUS BERADA DI BIOME TERSEBUT) ]
            // -------------------------------------------------------------------------
            RegisterSunItem(ItemID.AnkletoftheWind, () => Main.LocalPlayer.ZoneJungle); 
            RegisterSunItem(ItemID.FlowerBoots, () => Main.LocalPlayer.ZoneJungle);
            
            RegisterSunItem(ItemID.BalloonPufferfish, () => Main.LocalPlayer.ZoneBeach);
            RegisterSunItem(ItemID.Flipper, () => Main.LocalPlayer.ZoneBeach);
            RegisterSunItem(ItemID.FloatingTube, () => Main.LocalPlayer.ZoneBeach);   
            RegisterSunItem(ItemID.SailfishBoots, () => Main.LocalPlayer.ZoneBeach);
            RegisterSunItem(ItemID.WaterWalkingBoots, () => Main.LocalPlayer.ZoneBeach);
            RegisterSunItem(ItemID.HighTestFishingLine, () => Main.LocalPlayer.ZoneBeach);
            RegisterSunItem(ItemID.AnglerEarring, () => Main.LocalPlayer.ZoneBeach);
            RegisterSunItem(ItemID.TackleBox, () => Main.LocalPlayer.ZoneBeach);
            RegisterSunItem(ItemID.FishingBobber, () => Main.LocalPlayer.ZoneBeach);

            RegisterSunItem(ItemID.BlizzardinaBottle, () => Main.LocalPlayer.ZoneSnow);
            RegisterSunItem(ItemID.FlurryBoots, () => Main.LocalPlayer.ZoneSnow);
            RegisterSunItem(ItemID.IceSkates, () => Main.LocalPlayer.ZoneSnow);

            RegisterSunItem(ItemID.SandBoots, () => Main.LocalPlayer.ZoneDesert);
            RegisterSunItem(ItemID.FlyingCarpet, () => Main.LocalPlayer.ZoneDesert);
            RegisterSunItem(ItemID.SandstorminaBottle, () => Main.LocalPlayer.ZoneDesert);
            RegisterSunItem(ItemID.AncientChisel, () => Main.LocalPlayer.ZoneDesert);

            RegisterSunItem(ItemID.GuideVoodooDoll, () => Main.LocalPlayer.ZoneUnderworldHeight);
            RegisterSunItem(ItemID.LavaFishingHook, () => Main.LocalPlayer.ZoneUnderworldHeight || Main.LocalPlayer.ZoneBeach);

            // -------------------------------------------------------------------------
            // [ KATEGORI 3: PRE-HARDMODE PROGRESSION (BOSS & EVENT) ]
            // -------------------------------------------------------------------------
            RegisterSunItem(ItemID.RoyalGel, () => NPC.downedSlimeKing);
            RegisterSunItem(ItemID.EoCShield, () => NPC.downedBoss1); 
            
            // Menggunakan deteksi Bestiary Kill Count bawaan vanilla
            RegisterSunItem(ItemID.WormScarf, () => {
                string stringId = ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[NPCID.EaterofWorldsHead];
                return Main.BestiaryTracker.Kills.GetKillCount(stringId) > 0;
            }); 

            RegisterSunItem(ItemID.BrainOfConfusion, () => {
                string stringId = ContentSamples.NpcBestiaryCreditIdsByNpcNetIds[NPCID.BrainofCthulhu];
                return Main.BestiaryTracker.Kills.GetKillCount(stringId) > 0;
            }); 

            RegisterSunItem(ItemID.HoneyComb, () => NPC.downedQueenBee);
            RegisterSunItem(ItemID.PygmyNecklace, () => NPC.downedQueenBee);
            RegisterSunItem(ItemID.HiveBackpack, () => NPC.downedQueenBee);

            RegisterSunItem(ItemID.CobaltShield, () => NPC.downedBoss3);
            RegisterSunItem(ItemID.TallyCounter, () => NPC.downedBoss3);
            RegisterSunItem(ItemID.MechanicalLens, () => NPC.downedBoss3);
            RegisterSunItem(ItemID.ArmorPolish, () => NPC.downedBoss3);
            RegisterSunItem(ItemID.ClothierVoodooDoll, () => NPC.downedBoss3);
            RegisterSunItem(ItemID.TreasureMagnet, () => NPC.downedBoss3);
            RegisterSunItem(ItemID.BoneGlove, () => NPC.downedBoss3);

            RegisterSunItem(ItemID.BoneHelm, () => NPC.downedDeerclops);
            RegisterSunItem(ItemID.RocketBoots, () => NPC.downedGoblins);
            RegisterSunItem(ItemID.Toolbelt, () => NPC.downedGoblins);

            RegisterSunItem(ItemID.ApprenticeScarf, () => DD2Event.DownedInvasionT1); 
            RegisterSunItem(ItemID.SquireShield, () => DD2Event.DownedInvasionT1);     

            // -------------------------------------------------------------------------
            // [ KATEGORI 4: HARDMODE PROGRESSION (EARLY TO MID GAME) ]
            // -------------------------------------------------------------------------
            RegisterSunItem(ItemID.MoonCharm, () => Main.hardMode);
            RegisterSunItem(ItemID.CrossNecklace, () => Main.hardMode);
            RegisterSunItem(ItemID.FleshKnuckles, () => Main.hardMode);
            RegisterSunItem(ItemID.FrozenTurtleShell, () => Main.hardMode);
            RegisterSunItem(ItemID.MagicQuiver, () => Main.hardMode);
            RegisterSunItem(ItemID.MoonStone, () => Main.hardMode);
            RegisterSunItem(ItemID.PutridScent, () => Main.hardMode);
            RegisterSunItem(ItemID.RangerEmblem, () => Main.hardMode);
            RegisterSunItem(ItemID.RifleScope, () => Main.hardMode);
            RegisterSunItem(ItemID.SorcererEmblem, () => Main.hardMode);
            RegisterSunItem(ItemID.StarCloak, () => Main.hardMode);
            RegisterSunItem(ItemID.SummonerEmblem, () => Main.hardMode);
            RegisterSunItem(ItemID.TitanGlove, () => Main.hardMode);
            RegisterSunItem(ItemID.WarriorEmblem, () => Main.hardMode);
            RegisterSunItem(ItemID.PhilosophersStone, () => Main.hardMode);
            RegisterSunItem(ItemID.Blindfold, () => Main.hardMode);
            RegisterSunItem(ItemID.FastClock, () => Main.hardMode);
            RegisterSunItem(ItemID.Megaphone, () => Main.hardMode);
            RegisterSunItem(ItemID.Vitamins, () => Main.hardMode);
            RegisterSunItem(ItemID.TrifoldMap, () => Main.hardMode);
            RegisterSunItem(ItemID.PocketMirror, () => Main.hardMode);
            RegisterSunItem(ItemID.YoYoGlove, () => Main.hardMode);

            RegisterSunItem(ItemID.VolatileGelatin, () => NPC.downedQueenSlime);

            RegisterSunItem(ItemID.HuntressBuckler, () => NPC.downedMechBossAny);
            RegisterSunItem(ItemID.MonkBelt, () => NPC.downedMechBossAny);

            bool DownedAllMechs() => NPC.downedMechBoss1 && NPC.downedMechBoss2 && NPC.downedMechBoss3;
            RegisterSunItem(ItemID.AnkhShield, () => DownedAllMechs());
            RegisterSunItem(ItemID.AvengerEmblem, () => DownedAllMechs());

            // -------------------------------------------------------------------------
            // [ KATEGORI 5: LATE-HARDMODE TO ENDGAME PROGRESSION ]
            // -------------------------------------------------------------------------
            RegisterSunItem(ItemID.BlackBelt, () => NPC.downedPlantBoss);
            RegisterSunItem(ItemID.PaladinsShield, () => NPC.downedPlantBoss);
            RegisterSunItem(ItemID.HerculesBeetle, () => NPC.downedPlantBoss);
            RegisterSunItem(ItemID.Tabi, () => NPC.downedPlantBoss);
            RegisterSunItem(ItemID.SpectreGoggles, () => NPC.downedPlantBoss);
            RegisterSunItem(ItemID.SporeSac, () => NPC.downedPlantBoss);

            RegisterSunItem(ItemID.EyeoftheGolem, () => NPC.downedGolemBoss);

            RegisterSunItem(ItemID.EmpressFlightBooster, () => NPC.downedEmpressOfLight);
            RegisterSunItem(ItemID.GravityGlobe, () => NPC.downedMoonlord);

            // -------------------------------------------------------------------------
            // [ KATEGORI 6: HARDMODE EVENTS ]
            // -------------------------------------------------------------------------
            RegisterSunItem(ItemID.DiscountCard, () => NPC.downedPirates);
            RegisterSunItem(ItemID.GoldRing, () => NPC.downedPirates);
            RegisterSunItem(ItemID.LuckyCoin, () => NPC.downedPirates);

            RegisterSunItem(ItemID.NeptunesShell, () => DownedAllMechs());
            RegisterSunItem(ItemID.NecromanticScroll, () => NPC.downedHalloweenTree);
        }
    }
}