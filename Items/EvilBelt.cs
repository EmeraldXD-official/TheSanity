using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;

namespace TheSanity.Items
{
    // =========================================================================
    // 1. DATA ITEM & LOGIKA STATS "EVIL BELT"
    // =========================================================================
    public class EvilBelt : ModItem
    {
        public override void SetDefaults() {
            Item.width = 30;
            Item.height = 28;
            Item.accessory = true;
            Item.rare = ModContent.RarityType<CostumeRarity.SanityRarity>(); 
            Item.value = Item.sellPrice(0, 7, 50, 0);
            Item.defense = 5; 
        }

        // =========================================================================
        // 🌟 LOGIKA TOOLTIP DINAMIS
        // =========================================================================
        public override void ModifyTooltips(List<TooltipLine> tooltips) {
            string keyText = "[unbound]";
            bool isBound = false;

            if (SanityKeybinds.DoubleTapOverrideKey != null) {
                var assignedKeys = SanityKeybinds.DoubleTapOverrideKey.GetAssignedKeys();
                if (assignedKeys.Count > 0) {
                    keyText = assignedKeys[0]; 
                    isBound = true;
                }
            }

            string textToShow = isBound ? $"Dash Key {keyText}" : $"Dash key {keyText}";

            TooltipLine dashKeyLine = new TooltipLine(Mod, "EvilBeltDashHotkey", textToShow) {
                OverrideColor = new Color(135, 206, 250) 
            };

            tooltips.Add(dashKeyLine);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) {
            player.brainOfConfusionItem = Item;
            player.endurance += 0.17f;

            EvilBeltPlayer beltPlayer = player.GetModPlayer<EvilBeltPlayer>();
            beltPlayer.hasEvilBelt = true;
            
            if (!hideVisual) {
                player.shield = ContentSamples.ItemsByType[ItemID.EoCShield].shieldSlot;
                player.neck = ContentSamples.ItemsByType[ItemID.WormScarf].neckSlot;
                player.back = ContentSamples.ItemsByType[ItemID.WormScarf].backSlot; 
            }
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.BrainOfConfusion)
                .AddIngredient(ItemID.WormScarf)
                .AddIngredient(ItemID.EoCShield)
                .AddIngredient(ItemID.TissueSample, 5)
                .AddIngredient(ItemID.ShadowScale, 5)
                .AddTile(TileID.TinkerersWorkbench)
                .Register();
        }
    }

    // =========================================================================
    // 2. LOGIKA RAM DASH KUSTOM SESUAI ARAH GERAKAN (FIXED)
    // =========================================================================
    public class EvilBeltPlayer : ModPlayer
    {
        public bool hasEvilBelt = false;

        public int dashTimer = 0;
        public int dashDir = 0;
        public int dashCooldown = 0;
        private List<int> hitNPCs = new List<int>();

        private int leftTapTimer = 0;
        private int rightTapTimer = 0;
        private bool leftReleased = true;
        private bool rightReleased = true;

        public override void ResetEffects() {
            hasEvilBelt = false;
        }

        public override void PreUpdateMovement() {
            if (!hasEvilBelt) return;

            if (dashCooldown > 0) dashCooldown--;

            // Cek Universal Hotkey
            if (SanityKeybinds.DoubleTapOverrideKey != null && SanityKeybinds.DoubleTapOverrideKey.GetAssignedKeys().Count > 0) {
                Player.dashTime = 0;
                
                if (SanityKeybinds.DoubleTapOverrideKey.JustPressed && dashTimer <= 0 && dashCooldown <= 0) {
                    // PERBAIKAN UTAMA: Deteksi arah tombol input gerak, bukan arah muka wajah
                    int movementDirection = Player.direction; // Cadangan default kalau diam
                    
                    if (Player.controlLeft) {
                        movementDirection = -1;
                    }
                    else if (Player.controlRight) {
                        movementDirection = 1;
                    }

                    StartBeltDash(movementDirection);
                }
            } else {
                // Jika Hotkey kosong, gunakan Double-Tap Manual bawaan (sudah otomatis ikut arah tombol)
                HandleDoubleTap();
            }

            if (Player.dashType == 2) {
                Player.dashType = 0;
            }

            // EKSEKUSI RAM DASH KUSTOM
            if (dashTimer > 0) {
                dashTimer--;

                float blocksToDash = Main.hardMode ? 15f : 11f;
                float totalFrames = Main.hardMode ? 15f : 11f; 
                
                float dashSpeed = (blocksToDash * 16f) / totalFrames; 
                Player.velocity.X = dashDir * dashSpeed;

                // Memaksa arah hadap mata mengikuti arah dash agar visualnya sinkron saat nge-dash
                Player.direction = dashDir;

                Player.immune = true;
                Player.immuneTime = Math.Max(Player.immuneTime, 2);
                Player.fallStart = (int)(Player.position.Y / 16f);

                if (Main.rand.NextBool(2)) {
                    Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.Blood, dashDir * -2f, 0f, 100, default, 1.2f).noGravity = true;
                    Dust.NewDustDirect(Player.position, Player.width, Player.height, DustID.Shadowflame, dashDir * -2f, 0f, 100, default, 1.0f).noGravity = true;
                }

                Rectangle playerRect = Player.getRect();
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC npc = Main.npc[i];
                    if (npc.active && !npc.friendly && npc.damage > 0 && !npc.dontTakeDamage) {
                        if (playerRect.Intersects(npc.getRect())) {
                            if (!hitNPCs.Contains(npc.whoAmI)) {
                                hitNPCs.Add(npc.whoAmI);

                                int baseDamage = ContentSamples.ItemsByType[ItemID.EoCShield].damage;
                                float highestModifier = Math.Max(
                                    Math.Max(Player.GetTotalDamage(DamageClass.Melee).ApplyTo(1f), Player.GetTotalDamage(DamageClass.Ranged).ApplyTo(1f)),
                                    Math.Max(Player.GetTotalDamage(DamageClass.Magic).ApplyTo(1f), Player.GetTotalDamage(DamageClass.Summon).ApplyTo(1f))
                                );
                                int finalDamage = (int)(baseDamage * highestModifier);

                                Player.ApplyDamageToNPC(npc, finalDamage, 0f, dashDir, false);
                                SoundEngine.PlaySound(SoundID.DD2_WitherBeastDeath, Player.Center);
                            }
                        }
                    }
                }

                if (dashTimer == 0) {
                    dashCooldown = 30; 
                    Player.velocity.X *= 0.70f; 
                }
            }
        }

        private void HandleDoubleTap() {
            if (leftTapTimer > 0) leftTapTimer--;
            if (rightTapTimer > 0) rightTapTimer--;

            if (Player.controlLeft) {
                if (leftReleased && leftTapTimer > 0 && dashCooldown <= 0 && dashTimer <= 0) StartBeltDash(-1);
                else if (leftReleased) leftTapTimer = 15;
                leftReleased = false;
            } else leftReleased = true;

            if (Player.controlRight) {
                if (rightReleased && rightTapTimer > 0 && dashCooldown <= 0 && dashTimer <= 0) StartBeltDash(1);
                else if (rightReleased) rightTapTimer = 15;
                rightReleased = false;
            } else rightReleased = true;
        }

        private void StartBeltDash(int direction) {
            dashDir = direction;
            dashTimer = Main.hardMode ? 15 : 11; 
            hitNPCs.Clear();

            SoundEngine.PlaySound(SoundID.Item1, Player.Center);
        }
    }
}