using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class SummonItemOverride : GlobalItem
    {
        // =========================================================================
        // A. SYSTEM UNIVERSAL FILTER (Multi-Tiered Cross-Mod Filter)
        // =========================================================================
        private bool IsBossSummon(Item item) {
            int t = item.type;

            // 1. Pengecekan Dasar: Tidak menaruh Block/Dinding & harus memiliki kegunaan (useStyle)
            if (item.createTile >= 0 || item.createWall >= 0 || item.useStyle == 0) {
                return false;
            }

            // 2. Selalu izinkan item kustom SatterdStars (Twinkle) milikmu sendiri
            if (t == ModContent.ItemType<GlobalNPC.Bosses.Twinkle.SatterdStars>()) {
                return true;
            }

            // 3. BLACKLIST DASAR: Blokir amunisi, umpan, dan koin vanilla agar tidak bug
            if (item.ammo > 0 || item.bait > 0 || t == ItemID.CopperCoin || t == ItemID.SilverCoin || t == ItemID.GoldCoin || t == ItemID.PlatinumCoin) {
                return false;
            }

            // 4. BLACKLIST PERMANENT UPGRADE (Vanilla): Menghindari konsumsi tak terbatas pada item peningkat stat
            if (t == ItemID.LifeCrystal || t == ItemID.LifeFruit || t == ItemID.ManaCrystal || 
                t == ItemID.DemonHeart || t == ItemID.TorchGodsFavor || t == ItemID.AegisCrystal || 
                t == ItemID.ArcaneCrystal || t == ItemID.Ambrosia || t == ItemID.AegisFruit || 
                t == ItemID.GalaxyPearl || t == ItemID.GummyWorm || t == ItemID.CombatBook || 
                t == ItemID.PeddlersSatchel || t == ItemID.ArtisanLoaf || t == ItemID.MinecartPowerup) {
                return false;
            }

            // 5. JALUR RESMI TMODLOADER (Utama): Mod besar selalu mendaftarkan Boss Summon mereka di sini
            if (ItemID.Sets.SortingPriorityBossSpawns[t] != -1) {
                return true; 
            }

            // 6. JALUR FALLBACK LINTAS MOD: Scan nama internal untuk mod yang lupa mendaftarkan Priority
            if (item.ModItem != null) {
                string internalName = item.ModItem.Name.ToLower();

                // Kata kunci wajib untuk Boss Summon modded
                if (internalName.Contains("summon") || internalName.Contains("boss") || 
                    internalName.Contains("sigil") || internalName.Contains("badge") ||
                    internalName.Contains("spawner") || internalName.Contains("call") ||
                    internalName.Contains("beacon") || internalName.Contains("effigy") ||
                    internalName.Contains("idol") || internalName.Contains("token") ||
                    internalName.Contains("artifact")) {
                    
                    // Proteksi Lintas Mod Tambahan: Jika mengandung kata peningkat stat / ramuan / makanan, otomatis TOLAK
                    if (internalName.Contains("heart") || internalName.Contains("fruit") || 
                        internalName.Contains("crystal") || internalName.Contains("potion") || 
                        internalName.Contains("food") || internalName.Contains("upgrade") ||
                        internalName.Contains("orange") || internalName.Contains("berry") ||
                        internalName.Contains("jelly") || internalName.Contains("elixir") || 
                        item.buffType > 0) {
                        return false;
                    }

                    return true;
                }
            }

            return false;
        }

        // =========================================================================
        // 1. SETTING DEFAULT SIFAT ITEM (Khusus Hardcoded Vanilla Resmi)
        // =========================================================================
        public override void SetDefaults(Item item) {
            if (item.type == ItemID.GuideVoodooDoll) {
                item.useStyle = ItemUseStyleID.HoldUp; 
                item.useTime = 30;
                item.useAnimation = 30;
                item.UseSound = SoundID.Item44;
            }

            if (item.type == ItemID.LihzahrdPowerCell) {
                item.consumable = false;
            }
        }

        // =========================================================================
        // 2. JEBOL SYARAT BIOMA & LAYER (Bisa dipakai di mana saja)
        // =========================================================================
        public override bool CanUseItem(Item item, Player player) {
            if (item.type == ItemID.GuideVoodooDoll) {
                return player.ZoneUnderworldHeight && !NPC.AnyNPCs(NPCID.WallofFlesh);
            }

            if (item.type == ItemID.BloodySpine) {
                return !NPC.AnyNPCs(NPCID.BrainofCthulhu);
            }

            return base.CanUseItem(item, player);
        }

        // =========================================================================
        // 3. PAKSA SPAWN BOSS SAAT ITEM DIGUNAKAN (Bypass batasan Bioma Vanilla)
        // =========================================================================
        public override bool? UseItem(Item item, Player player) {
            if (Main.netMode != NetmodeID.MultiplayerClient) {
                if (item.type == ItemID.GuideVoodooDoll) {
                    NPC.SpawnWOF(player.position);
                    return true;
                }

                if (item.type == ItemID.WormFood) {
                    NPC.SpawnOnPlayer(player.whoAmI, NPCID.EaterofWorldsHead);
                    return true;
                }

                if (item.type == ItemID.BloodySpine) {
                    NPC.SpawnOnPlayer(player.whoAmI, NPCID.BrainofCthulhu);
                    return true;
                }
            }
            return base.UseItem(item, player);
        }

        // =========================================================================
        // 4. SISTEM UNLIMITED / ANTI BERKURANG (CONSUME ITEM)
        // =========================================================================
        public override bool ConsumeItem(Item item, Player player) {
            if (item.type == ItemID.GuideVoodooDoll || item.type == ItemID.LihzahrdPowerCell) {
                return false; 
            }

            if (IsBossSummon(item)) {
                return false; 
            }

            return base.ConsumeItem(item, player);
        }

        // =========================================================================
        // 5. KUSTOMISASI DESKRIPSI TOOLTIP UNIVERSAL
        // =========================================================================
        public override void ModifyTooltips(Item item, List<TooltipLine> tooltips) {
            // Bersihkan teks "Consumable" bawaan untuk semua Boss Summon yang lolos filter
            if (IsBossSummon(item) || item.type == ItemID.LihzahrdPowerCell || item.type == ItemID.GuideVoodooDoll) {
                tooltips.RemoveAll(line => line.Mod == "Terraria" && line.Name == "Consumable");
            }

            // Spesifik Tooltip: Guide Voodoo Doll
            if (item.type == ItemID.GuideVoodooDoll) {
                TooltipLine unconsumableLine = new TooltipLine(Mod, "UnconsumableVoodooText", "[c/00FF7F:Unconsumable] (Sanity Rework)");
                TooltipLine guideLine = new TooltipLine(Mod, "VoodooReworkDesc", "[c/FF4500:Can be used directly from your hand while in Hell to summon the Wall of Flesh.]\n[c/A9A9A9:(Does not require or kill the Guide)]");
                tooltips.Add(unconsumableLine);
                tooltips.Add(guideLine);
            }
            // Spesifik Tooltip: Worm Food & Bloody Spine
            else if (item.type == ItemID.WormFood || item.type == ItemID.BloodySpine) {
                TooltipLine unconsumableLine = new TooltipLine(Mod, "UnconsumableSummonText", "[c/00FF7F:Unconsumable] (Sanity Rework)");
                TooltipLine anywhereLine = new TooltipLine(Mod, "AnywhereDesc", "[c/FF4500:Can be used anywhere, in any biome or layer]");
                tooltips.Add(unconsumableLine);
                tooltips.Add(anywhereLine);
            }
            // Spesifik Tooltip: Lihzahrd Power Cell
            else if (item.type == ItemID.LihzahrdPowerCell) {
                TooltipLine unconsumableLine = new TooltipLine(Mod, "UnconsumableCellText", "[c/00FF7F:Unconsumable] (Sanity Rework)");
                tooltips.Add(unconsumableLine);
            }
            // Tooltip Otomatis Lintas Mod untuk Item Boss Summon lainnya
            else if (IsBossSummon(item)) {
                if (!tooltips.Exists(line => line.Name == "UnconsumableSummonText")) {
                    TooltipLine unconsumableLine = new TooltipLine(Mod, "UnconsumableSummonText", "[c/00FF7F:Unconsumable] (Sanity Rework)");
                    tooltips.Add(unconsumableLine);
                }
            }
        }
    }
}