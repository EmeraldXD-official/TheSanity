using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Terraria.ID;
using Terraria.Audio;
using Terraria.DataStructures;
using System;
using System.Collections.Generic;
using Terraria.ModLoader.IO;

namespace TheSanity
{
    public class SanityPlayer : ModPlayer
    {
        public float SanityCurrent = 0;
        public float SanityMax = 100;
        
        private int internalCounter = 0;
        private const int Threshold = 30000; 

        private int soundTimer = 0;
        private int damageTimer = 0;
        private int chatTimer = 0; 

        public bool isNeutralizing = false;
        private int neutralizingTimer = 0;

        private float? originalMusicVol = null;
        private float? originalSoundVol = null;
        private float? originalAmbientVol = null;

        public static string ActiveSplashText = "";
        public static int SplashTextTimer = 0;

        private readonly string[] sanityChats = new string[] {
            "Did you remember to lock your front door?",
            "Is someone standing right behind your chair?",
            "They are listening to you play.",
            "Your family thinks you are just playing a game.",
            "Take a deep breath. Can you hear the breathing from the corner?",
            "You've been staring at this screen for too long.",
            "It knows you are awake.",
            "Are those whispers really coming from the mod?",
            "That wasn't the sound of your own footsteps.",
            "The deeper you dig, the closer you get to a meaningless end."
        };

        private readonly HashSet<int> insanityDebuffs = new HashSet<int> {
            30, 20, 24, 70, 22, 80, 35, 23, 31, 32, 197, 33, 36, 195, 196, 38, 39, 69, 44, 46, 47, 
            149, 156, 164, 163, 144, 145, 68, 67, 120, 334, 333, 72, 137, 153, 203, 189, 183, 186, 
            344, 160, 324, 323, 104
        };

        private bool IsFishingProjectilesActive() {
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (proj.active && proj.owner == Player.whoAmI && proj.bobber) return true;
            }
            return false;
        }

        // =========================================================
        // JALUR PENYIMPANAN DATA (DENGAN SAVE VOLUME BAWAAN)
        // =========================================================
        public override void SaveData(TagCompound tag) {
            tag["SanityCurrent"] = SanityCurrent;
            tag["SanityInternalCounter"] = internalCounter;
            
            // Menyimpan volume asli sebelum diubah oleh mod agar tidak hilang saat exit world
            if (originalMusicVol.HasValue) tag["OriginalMusicVol"] = originalMusicVol.Value;
            if (originalSoundVol.HasValue) tag["OriginalSoundVol"] = originalSoundVol.Value;
            if (originalAmbientVol.HasValue) tag["OriginalAmbientVol"] = originalAmbientVol.Value;
        }

        public override void LoadData(TagCompound tag) {
            if (tag.ContainsKey("SanityCurrent")) {
                SanityCurrent = tag.GetFloat("SanityCurrent");
            }
            if (tag.ContainsKey("SanityInternalCounter")) {
                internalCounter = tag.GetInt("SanityInternalCounter");
            }
            
            // Mengambil kembali volume asli pemain saat masuk ke world kembali
            if (tag.ContainsKey("OriginalMusicVol")) originalMusicVol = tag.GetFloat("OriginalMusicVol");
            if (tag.ContainsKey("OriginalSoundVol")) originalSoundVol = tag.GetFloat("OriginalSoundVol");
            if (tag.ContainsKey("OriginalAmbientVol")) originalAmbientVol = tag.GetFloat("OriginalAmbientVol");
        }
        // =========================================================

        public override void PostUpdateMiscEffects() {
            if (isNeutralizing) {
                neutralizingTimer++;
                if (neutralizingTimer >= 6) {
                    SanityCurrent -= 1f;
                    neutralizingTimer = 0;
                }
                if (SanityCurrent <= 0) {
                    SanityCurrent = 0;
                    isNeutralizing = false;
                }
            }

            int gainPoints = 0;
            int lossPoints = 0;

            if (Player.ZoneCrimson || Player.ZoneCorrupt) gainPoints++;
            if (Player.ZoneUnderworldHeight) gainPoints++;
            if (Main.bloodMoon || Main.eclipse) gainPoints++;

            float multiplier = 1.0f;
            if (!Main.dayTime && Player.ZoneOverworldHeight) multiplier += 0.5f;

            for (int i = 0; i < Player.MaxBuffs; i++) {
                int bType = Player.buffType[i];
                if (bType > 0 && insanityDebuffs.Contains(bType)) multiplier += 0.01f; 
            }

            if (Main.dayTime && Player.ZoneOverworldHeight) lossPoints++;
            if (Player.HeldItem.fishingPole > 0 && IsFishingProjectilesActive()) lossPoints++;
            if (Player.HasBuff(BuffID.Campfire)) lossPoints++;
            if (!string.IsNullOrEmpty(Main.npcChatText)) lossPoints++;
            if (Main.raining && Player.ZoneOverworldHeight) lossPoints++;

            bool inSafeHouse = Main.dayTime && Player.ZoneOverworldHeight && Player.behindBackWall;

            if (gainPoints >= lossPoints && gainPoints > 0) {
                float netGain = (gainPoints - lossPoints + 0.5f) * multiplier; 
                if (AnyBossActive()) netGain *= 0.5f;
                internalCounter += (int)(netGain * 100);
            }
            else if (lossPoints > gainPoints) {
                float netLoss = (lossPoints - gainPoints);
                if (Player.HasBuff(BuffID.Sunflower)) netLoss *= 1.25f; 
                internalCounter -= (int)(netLoss * 100);
            }

            if (inSafeHouse && !(Player.ZoneCrimson || Player.ZoneCorrupt) && gainPoints <= 0) {
                if (lossPoints <= 1) internalCounter = 0; 
            }

            if (internalCounter >= Threshold) {
                SanityCurrent += 1f;
                internalCounter = 0;
            }
            else if (internalCounter <= -Threshold) {
                SanityCurrent -= 1f;
                internalCounter = 0;
            }

            SanityCurrent = MathHelper.Clamp(SanityCurrent, 0, SanityMax);
            ApplySanityEffects();
            
            if (SanityCurrent >= 10) {
                chatTimer++;
                if (chatTimer >= 3600) {
                    if (Main.myPlayer == Player.whoAmI) {
                        string chosenChat = Main.rand.Next(sanityChats);
                        Main.NewText(chosenChat, 140, 30, 30); 
                        if (SanityCurrent >= 100) {
                            ActiveSplashText = chosenChat;
                            SplashTextTimer = 120;
                        }
                    }
                    chatTimer = 0;
                }
            }
            else {
                chatTimer = 0;
            }

            if (SplashTextTimer > 0) {
                SplashTextTimer--;
                if (SplashTextTimer <= 0) {
                    ActiveSplashText = "";
                }
            }
        }

        public override void UpdateLifeRegen() {
            if (SanityCurrent >= 100) {
                if (Player.lifeRegen > 0) Player.lifeRegen = 0;
                Player.lifeRegenTime = 0;
                Player.bleed = true; 
            }
        }

        public override void PostUpdateRunSpeeds() {
            if (SanityCurrent >= 100) {
                Player.GetDamage(DamageClass.Generic) *= 0.5f;
                Player.statDefense /= 2;
                Player.endurance = 0f;
            }
        }

        public override void OnHurt(Player.HurtInfo info) {
            SanityCurrent += 1f;
        }

        private bool AnyBossActive() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].active && (Main.npc[i].boss || Main.npc[i].type == NPCID.EaterofWorldsHead)) return true;
            }
            return false;
        }

        private void ApplySanityEffects() {
            if (SanityCurrent >= 70) {
                // Proteksi ekstra: hanya rekam volume player saat ini jika volume asli belum terisi / bukan 0 akibat bug
                if (originalMusicVol == null || (originalMusicVol == 0f && Main.musicVolume > 0f)) originalMusicVol = Main.musicVolume;
                if (originalSoundVol == null || (originalSoundVol == 0f && Main.soundVolume > 0f)) originalSoundVol = Main.soundVolume;
                if (originalAmbientVol == null || (originalAmbientVol == 0f && Main.ambientVolume > 0f)) originalAmbientVol = Main.ambientVolume;

                float dropFactor = MathHelper.Clamp(1f - ((SanityCurrent - 70f) / 30f), 0f, 1f);
                
                Main.musicVolume = originalMusicVol.Value * dropFactor;
                Main.soundVolume = originalSoundVol.Value * dropFactor;

                float riseFactor = MathHelper.Clamp((SanityCurrent - 70f) / 30f, 0f, 1f);
                Main.ambientVolume = MathHelper.Lerp(originalAmbientVol.Value, 1f, riseFactor);

                if (SanityCurrent >= 100) {
                    damageTimer++;
                    if (damageTimer >= 30) {
                        Player.statLife -= 1;
                        if (Player.statLife <= 0) {
                            string deathMessage = "";
                            int deathRoll = Main.rand.Next(11);
                            switch (deathRoll) {
                                case 0: deathMessage = Player.name + " collapsed. Did you hear that faint noise down the hallway just now?"; break;
                                case 1: deathMessage = "While you watched " + Player.name + " decay, how long has it been since you checked on your family?"; break;
                                case 2: deathMessage = Player.name + " clawed through their own skull to let the voices out."; break;
                                case 3: deathMessage = "The things in the dark finally fed on " + Player.name + "'s mind."; break;
                                case 4: deathMessage = Player.name + "'s head exploded."; break;
                                case 5: deathMessage = "Look away from the screen, " + Player.name + " is no longer the one playing."; break;
                                case 6: deathMessage = Player.name + " died alone. Someone in your real house is waiting for you to log off."; break;
                                case 7: deathMessage = Player.name + " can respawn, but the years you wasted away from them will never return."; break;
                                case 8: deathMessage = "You can save " + Player.name + "'s world, but you can't save your family from reality."; break;
                                case 9: deathMessage = Player.name + " surrendered. One day, a text like this will announce your own end to them."; break;
                                case 10: deathMessage = "It's just a game, right? Then why is your heart beating so fast in that empty room?"; break;
                            }
                            Player.KillMe(PlayerDeathReason.ByCustomReason(deathMessage), 1.0, 0);
                        }
                        damageTimer = 0;
                    }
                }
            } 
            else if (originalMusicVol != null) {
                Main.musicVolume = originalMusicVol.Value;
                Main.soundVolume = originalSoundVol.Value;
                Main.ambientVolume = originalAmbientVol.Value; 
                originalMusicVol = null;
                originalSoundVol = null;
                originalAmbientVol = null;
            }

            if (SanityCurrent >= 80) {
                soundTimer++;
                int spawnRate = (int)MathHelper.Lerp(350, 120, (SanityCurrent - 80f) / 20f);
                if (soundTimer > Main.rand.Next(spawnRate / 2, spawnRate)) {
                    int roll = Main.rand.Next(46);
                    string chosen = roll == 0 ? "Sanity" : "Sanity" + roll;
                    SoundStyle whisper = new SoundStyle("TheSanity/Sounds/" + chosen) {
                        Volume = 1f,
                        Pitch = Main.rand.NextFloat(-0.4f, 0.4f),
                        Type = SoundType.Ambient
                    };
                    if (!Main.dedServ) SoundEngine.PlaySound(whisper, Player.Center + new Vector2(Main.rand.Next(-600, 601), Main.rand.Next(-400, 401)));
                    soundTimer = 0;
                }
            }
        }
    }
}