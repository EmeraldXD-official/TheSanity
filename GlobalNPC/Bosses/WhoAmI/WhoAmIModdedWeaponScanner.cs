using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Core;

namespace TheSanity.GlobalNPC.Bosses.WhoAmI
{
    public class WhoAmIModdedWeaponScanner : ModSystem
    {
        private const string ManifestRelativeDir = "TheSanity";
        private const string ManifestFileName = "ModdedWeaponManifest.json";

        public override void PostSetupContent()
        {
            try
            {
                string saveDir = Path.Combine(Main.SavePath, ManifestRelativeDir);
                Directory.CreateDirectory(saveDir);
                string manifestPath = Path.Combine(saveDir, ManifestFileName);

                JArray manifestArray = new JArray();
                var existingMap = new Dictionary<string, JObject>(StringComparer.OrdinalIgnoreCase);
                if (File.Exists(manifestPath))
                {
                    try
                    {
                        var txt = File.ReadAllText(manifestPath);
                        manifestArray = JArray.Parse(txt);
                        foreach (var tok in manifestArray.OfType<JObject>())
                        {
                            string modName = tok.Value<string>("modName") ?? "";
                            string internalName = tok.Value<string>("internalName") ?? "";
                            string key = modName + "::" + internalName;
                            if (!existingMap.ContainsKey(key)) existingMap[key] = tok;
                        }
                    }
                    catch (Exception ex)
                    {
                        Mod.Logger.WarnFormat("Failed to parse existing ModdedWeaponManifest.json: {0}", ex.Message);
                        manifestArray = new JArray();
                        existingMap.Clear();
                    }
                }

                int newCount = 0;
                int newNeedsManual = 0;

                foreach (var mod in ModLoader.Mods)
                {
                    if (mod == this.Mod) continue;
                    if (string.Equals(mod.Name, "Terraria", StringComparison.OrdinalIgnoreCase)) continue;
                    if (string.Equals(mod.Name, "ModLoader", StringComparison.OrdinalIgnoreCase)) continue;

                    try
                    {
                        foreach (var mi in mod.GetContent<ModItem>())
                        {
                            try
                            {
                                Item item = mi.Item;
                                if (!WhoAmI.IsWeaponItem(item)) continue;

                                string modName = mod.Name;
                                string internalName = mi.Name ?? item.Name ?? "";
                                string key = modName + "::" + internalName;
                                if (existingMap.ContainsKey(key)) continue; // already catalogued

                                string damageClass = "Unknown";
                                if (item.CountsAsClass(DamageClass.Melee)) damageClass = "Melee";
                                else if (item.CountsAsClass(DamageClass.Ranged)) damageClass = "Ranged";
                                else if (item.CountsAsClass(DamageClass.Magic)) damageClass = "Magic";
                                else if (item.CountsAsClass(DamageClass.Summon)) damageClass = "Summon";

                                var arche = WhoAmI.ResolveArchetypeForItem(item);
                                string archeStr = arche.ToString();

                                bool hasSimpleShoot = item.shoot > 0 && item.shoot != Terraria.ID.ProjectileID.PurificationPowder;
                                bool needsManual = !hasSimpleShoot && (arche == WhoAmI.WeaponArchetype.Ranged || arche == WhoAmI.WeaponArchetype.Magic || arche == WhoAmI.WeaponArchetype.Whip || arche == WhoAmI.WeaponArchetype.Boomerang || arche == WhoAmI.WeaponArchetype.Yoyo);

                                var obj = new JObject
                                {
                                    ["modName"] = modName,
                                    ["internalName"] = internalName,
                                    ["itemID"] = item.type,
                                    ["damageClass"] = damageClass,
                                    ["archetype"] = archeStr,
                                    ["hasSimpleShoot"] = hasSimpleShoot,
                                    ["shootProjectileID"] = hasSimpleShoot ? item.shoot : 0,
                                    ["needsManualReview"] = needsManual,
                                    ["firstSeenUtc"] = DateTime.UtcNow.ToString("o"),
                                    ["manualNotes"] = ""
                                };

                                manifestArray.Add(obj);
                                existingMap[key] = obj;
                                newCount++;
                                if (needsManual) newNeedsManual++;
                            }
                            catch (Exception exInner)
                            {
                                Mod.Logger.WarnFormat("Failed scanning item in mod {0}: {1}", mod.Name, exInner.Message);
                            }
                        }
                    }
                    catch (Exception exMod)
                    {
                        Mod.Logger.WarnFormat("Failed enumerating items for mod {0}: {1}", mod.Name, exMod.Message);
                    }
                }

                try
                {
                    File.WriteAllText(manifestPath, manifestArray.ToString(Formatting.Indented));
                }
                catch (Exception ex)
                {
                    Mod.Logger.WarnFormat("Failed to write ModdedWeaponManifest.json: {0}", ex.Message);
                }

                Mod.Logger.InfoFormat("WhoAmI: Modded weapon scan finished — {0} new weapons, {1} need manual review.", newCount, newNeedsManual);
            }
            catch (Exception ex)
            {
                Mod.Logger.WarnFormat("WhoAmIModdedWeaponScanner.PostSetupContent failed: {0}", ex.Message);
            }
        }
    }

    // Debug command to generate commented stub lines for manual override registration
    public class WhoAmI_GenerateStubsCommand : ModCommand
    {
        public override CommandType Type => CommandType.Chat;
        public override string Command => "whoami_gen_stubs";
        public override string Usage => "/whoami_gen_stubs - write stub registration lines for needsManualReview weapons to Saves/TheSanity/WhoAmI_CustomOverridesStubs.txt";
        public override string Description => "Generate stub lines for WhoAmI custom weapon overrides (debug).";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            string saveDir = Path.Combine(Main.SavePath, "TheSanity");
            string manifestPath = Path.Combine(saveDir, "ModdedWeaponManifest.json");
            string outPath = Path.Combine(saveDir, "WhoAmI_CustomOverridesStubs.txt");

            if (!File.Exists(manifestPath))
            {
                caller.Reply("ModdedWeaponManifest.json not found.", Color.OrangeRed);
                return;
            }

            try
            {
                var arr = JArray.Parse(File.ReadAllText(manifestPath));
                var lines = new List<string>();
                lines.Add("// WhoAmI custom weapon override stubs (generated). Paste the uncommented lines into WhoAmI_ModdedWeaponOverrides.cs RegisterCustomWeaponOverrides().");
                foreach (var tok in arr.OfType<JObject>())
                {
                    bool needs = tok.Value<bool>("needsManualReview");
                    if (!needs) continue;
                    string modName = tok.Value<string>("modName");
                    string internalName = tok.Value<string>("internalName");
                    int id = tok.Value<int>("itemID");
                    lines.Add($"// TODO (needs manual review — see ModdedWeaponManifest.json): {modName} / {internalName} ({id})");
                    lines.Add($"// CustomWeaponFireOverrides[{id}] = (boss, target) => {{ /* spawn {modName}/{internalName} real projectile here */ }};\n");
                }

                File.WriteAllLines(outPath, lines);
                caller.Reply($"Wrote stubs to {outPath}", Color.LightGreen);
            }
            catch (Exception ex)
            {
                caller.Reply($"Failed to generate stubs: {ex.Message}", Color.OrangeRed);
            }
        }
    }
}
