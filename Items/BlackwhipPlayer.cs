using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio; 
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace TheSanity
{
    public class BlackwhipPlayer : ModPlayer
    {
        public bool isTethered = false;
        public int tetheredNPCIndex = -1;
        public float currentTetherLength = 0f;

        public bool isWebWalking = false;
        public Vector2[] legAnchors = new Vector2[4];
        public bool[] legAnchored = new bool[4];
        private float[] legAngles = new float[] { -2.35f, -0.78f, 2.35f, 0.78f }; 
        private int legStepTimer = 0; 

        public int selectedWhipTagType = ItemID.BlandWhip;

        public override void SaveData(TagCompound tag) {
            tag["selectedWhipTagType"] = selectedWhipTagType;
        }

        public override void LoadData(TagCompound tag) {
            if (tag.ContainsKey("selectedWhipTagType")) {
                selectedWhipTagType = tag.GetInt("selectedWhipTagType");
            }
        }

        public override void ProcessTriggers(TriggersSet triggersSet) {
            if (Player.HeldItem.type != ModContent.ItemType<Items.BlackwhipItem>()) {
                if (isTethered) BreakTether();
                if (isWebWalking) {
                    isWebWalking = false;
                    SoundEngine.PlaySound(SoundID.Item16, Player.Center); 
                }
                return;
            }

            if (BlackwhipSystem.TetherKeybind.JustPressed) {
                if (isWebWalking) isWebWalking = false; 
                if (isTethered) BreakTether();
                else TryStartTether();
            }

            if (BlackwhipSystem.WebWalkKeybind.JustPressed) {
                if (isTethered) BreakTether(); 
                isWebWalking = !isWebWalking;

                if (isWebWalking) {
                    for (int i = 0; i < 4; i++) {
                        legAnchors[i] = Player.Center + legAngles[i].ToRotationVector2() * 100f;
                        legAnchored[i] = false;
                    }
                    legStepTimer = 0;
                    SoundEngine.PlaySound(SoundID.Item17, Player.Center);
                }
                else {
                    SoundEngine.PlaySound(SoundID.Item16, Player.Center);
                }
            }
        }

        private void TryStartTether() {
            float maxDist = 50f * 16f;
            int closestNPC = -1;
            float closestDist = maxDist;

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && !npc.friendly && npc.damage > 0 && !npc.dontTakeDamage) {
                    float dist = Vector2.Distance(Player.Center, npc.Center);
                    if (dist < closestDist) {
                        closestDist = dist;
                        closestNPC = i;
                    }
                }
            }

            if (closestNPC != -1) {
                isTethered = true;
                tetheredNPCIndex = closestNPC;
                currentTetherLength = closestDist; 

                SoundEngine.PlaySound(SoundID.Item17 with { Pitch = -0.1f, Volume = 0.85f }, Player.Center);

                if (Main.myPlayer == Player.whoAmI) {
                    Projectile.NewProjectile(Player.GetSource_FromThis(), Player.Center, Vector2.Zero, ModContent.ProjectileType<Projectiles.BlackwhipTetherProjectile>(), 0, 0f, Player.whoAmI);
                }
            }
        }

        public void BreakTether() {
            if (isTethered) {
                SoundEngine.PlaySound(SoundID.Item16 with { Pitch = 0.1f, Volume = 0.7f }, Player.Center);
            }
            isTethered = false;
            tetheredNPCIndex = -1;
        }

        public override void PreUpdateMovement() {
            if (Player.HeldItem.type != ModContent.ItemType<Items.BlackwhipItem>()) {
                BreakTether();
                isWebWalking = false;
                return;
            }

            if (isWebWalking) {
                Player.RemoveAllGrapplingHooks();
                Player.gravity = 0f; 
                Player.stepSpeed = 0f; // PERBAIKAN 1: Matikan fitur step-up otomatis agar tidak tersangkut di pinggir platform

                bool actualUp = Player.controlUp;
                bool actualDown = Player.controlDown;

                // PERBAIKAN 2: Jika menekan tombol bawah, bimbing koordinat Y pemain agar menembus platform
                if (actualDown) {
                    int tileXStart = (int)(Player.position.X / 16f);
                    int tileXEnd = (int)((Player.position.X + Player.width) / 16f);
                    int tileY = (int)((Player.position.Y + Player.height + 1f) / 16f); // Area tepat di bawah kaki

                    bool standingOnPlatform = false;
                    for (int x = tileXStart; x <= tileXEnd; x++) {
                        if (WorldGen.InWorld(x, tileY)) {
                            Tile tile = Main.tile[x, tileY];
                            if (tile.HasTile && Main.tileSolidTop[tile.TileType]) {
                                standingOnPlatform = true;
                                break;
                            }
                        }
                    }

                    if (standingOnPlatform) {
                        Player.position.Y += 4f; // Dorong sedikit ke bawah melewati batas solid top platform
                    }
                }

                Vector2 move = Vector2.Zero;
                if (actualUp) move.Y -= 1f;
                if (actualDown) move.Y += 1f;
                if (Player.controlLeft) move.X -= 1f;
                if (Player.controlRight) move.X += 1f;

                if (move != Vector2.Zero) {
                    move.Normalize();
                    float speed = 8f; 
                    Player.velocity = Vector2.Lerp(Player.velocity, move * speed, 0.15f);
                }
                else {
                    Player.velocity = Vector2.Lerp(Player.velocity, Vector2.Zero, 0.2f); 
                }

                UpdateDocOckLegs();
                Player.fallStart = (int)(Player.position.Y / 16f);
                Player.controlDown = true; 

                if (Main.myPlayer == Player.whoAmI && Player.ownedProjectileCounts[ModContent.ProjectileType<Projectiles.BlackwhipWebWalkProjectile>()] == 0) {
                    Projectile.NewProjectile(Player.GetSource_FromThis(), Player.Center, Vector2.Zero, ModContent.ProjectileType<Projectiles.BlackwhipWebWalkProjectile>(), 0, 0f, Player.whoAmI);
                }
                return; 
            }

            if (isTethered) {
                NPC npc = Main.npc[tetheredNPCIndex];
                if (!npc.active || npc.friendly) {
                    BreakTether();
                    return;
                }

                Player.RemoveAllGrapplingHooks();
                Vector2 offset = Player.Center - npc.Center;
                float angle = offset.ToRotation();

                if (Player.controlUp) currentTetherLength += 7f;   
                if (Player.controlDown) currentTetherLength -= 7f; 
                currentTetherLength = MathHelper.Clamp(currentTetherLength, 40f, 800f);

                float orbitSpeed = 6f / (currentTetherLength * 0.05f); 
                orbitSpeed = MathHelper.Clamp(orbitSpeed, 0.015f, 0.06f); 

                if (Player.controlLeft) angle -= orbitSpeed;
                if (Player.controlRight) angle += orbitSpeed;

                Vector2 targetPos = npc.Center + angle.ToRotationVector2() * currentTetherLength;
                Player.velocity = targetPos - Player.Center;
                Player.fallStart = (int)(Player.position.Y / 16f);
            }
        }

        private void UpdateDocOckLegs() {
            if (legStepTimer > 0) legStepTimer--; 

            float maxScanReach = 50f * 16f; 
            float openAirReach = 1500f; 

            // Cek apakah player berniat turun (menekan tombol Down sebelum di-override di akhir PreUpdateMovement)
            bool pressingDown = Player.controlDown;

            for (int i = 0; i < 4; i++) {
                Vector2 scanDir = legAngles[i].ToRotationVector2();
                if (Player.velocity != Vector2.Zero) {
                    scanDir = Vector2.Normalize(scanDir + Player.velocity * 0.2f);
                }

                Vector2 closestTilePos = Vector2.Zero;
                bool foundTile = false;

                for (float d = 32f; d < maxScanReach; d += 16f) {
                    Vector2 checkPos = Player.Center + scanDir * d;
                    int tileX = (int)(checkPos.X / 16f);
                    int tileY = (int)(checkPos.Y / 16f);

                    if (WorldGen.InWorld(tileX, tileY)) {
                        Tile tile = Main.tile[tileX, tileY];
                        if (tile.HasTile && Main.tileSolid[tile.TileType]) {
                            // PERBAIKAN 3: Jika ubin adalah platform dan player menekan tombol bawah, abaikan ubin ini agar kaki tidak mengunci di sana
                            if (Main.tileSolidTop[tile.TileType] && pressingDown) {
                                continue;
                            }

                            closestTilePos = new Vector2(tileX * 16 + 8, tileY * 16 + 8);
                            foundTile = true;
                            break; 
                        }
                    }
                }

                float currentAnchorDist = Vector2.Distance(Player.Center, legAnchors[i]);

                if (legAnchored[i] && currentAnchorDist > maxScanReach) {
                    legAnchored[i] = false;
                }

                if (!legAnchored[i] && foundTile) {
                    legAnchors[i] = closestTilePos;
                    legAnchored[i] = true;
                    SoundEngine.PlaySound(SoundID.Item54 with { Pitch = 0.1f, Volume = 0.6f }, closestTilePos);
                }
                else if (legAnchored[i] && foundTile) {
                    float newTileDist = Vector2.Distance(Player.Center, closestTilePos);

                    if (currentAnchorDist > newTileDist + 64f && legStepTimer <= 0) {
                        legAnchors[i] = closestTilePos;
                        legStepTimer = 8; 
                        SoundEngine.PlaySound(SoundID.Item17 with { Pitch = 0.6f, Volume = 0.35f }, closestTilePos);
                        break; 
                    }
                }
                else if (!foundTile && legAnchored[i]) {
                    legAnchored[i] = false;
                }

                if (!legAnchored[i]) {
                    Vector2 openAirTarget = Player.Center + legAngles[i].ToRotationVector2() * openAirReach;
                    legAnchors[i] = Vector2.Lerp(legAnchors[i], openAirTarget, 0.15f);
                }
            }
        }
    }
}