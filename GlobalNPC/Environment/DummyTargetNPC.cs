using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.Audio;
using TheSanity.Buff;

namespace TheSanity.NPCs
{
    // ==================== SYSTEM CENTRAL ANTI-LAG & CHAT CLEANER ====================
    // Kita satukan di file ini agar rapi. Sistem ini mengawasi Boss secara global sembari menghitung total Dummy.
    public class DummyAntiBossSystem : ModSystem
    {
        public override void PreUpdateNPCs() {
            bool bossActive = false;
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].active && Main.npc[i].boss) {
                    bossActive = true;
                    break;
                }
            }

            if (bossActive) {
                int dummyCount = 0;
                int dummyType = ModContent.NPCType<DummyTargetNPC>();

                for (int i = 0; i < Main.maxNPCs; i++) {
                    if (Main.npc[i].active && Main.npc[i].type == dummyType) {
                        Main.npc[i].active = false; // Lenyapkan Dummy
                        dummyCount++;
                    }
                }

                // Eksekusi ini CUMA BERJALAN 1 KALI untuk seluruh Dummy yang aktif
                if (dummyCount > 0) {
                    if (Main.netMode != NetmodeID.Server) {
                        var uiInstance = ModContent.GetInstance<DebuffUISystem>()?.DebuffUI;
                        if (uiInstance != null) {
                            uiInstance.SelectedBuffID = 0; 
                            uiInstance.SelectedNPCID = 0;  
                            uiInstance.PopulateList(); // Refresh list cuma 1x, LAG HILANG TOTAL!
                        }
                        
                        // Menampilkan teks rapi sesuai request format: (Jumlah Seluruh Dummy)
                        Main.NewText($"⚠️ Boss Detected! All Dummies auto-cleared ({dummyCount})", Color.Red);
                    }
                }
            }
        }
    }

    // ==================== DUMMY TARGET NPC CODE ====================
    public class DummyTargetNPC : ModNPC
    {
        public override string Texture => "TheSanity/GlobalNPC/Environment/DummyTargetNPC";

        public override void SetStaticDefaults() {
            NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, value);
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults() {
            NPC.width = 34;
            NPC.height = 48;
            NPC.damage = 0; 
            NPC.defense = 0;
            NPC.lifeMax = 10000000; 
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.HitSound = null; 
        }

        public override void AI() {
            var uiInstance = ModContent.GetInstance<DebuffUISystem>()?.DebuffUI;

            // 🛠️ FIX: Proteksi boss lama yang bikin lag di sini sudah dihapus total 
            // karena sudah di-handle oleh DummyAntiBossSystem di atas secara terpusat.

            NPC.velocity = Vector2.Zero; 

            // ==================== DYNAMIC STAT MIMIC SYSTEM ====================
            if (uiInstance != null && uiInstance.SelectedNPCID > 0) {
                NPC sampleNPC = new NPC();
                sampleNPC.SetDefaults(uiInstance.SelectedNPCID);

                int targetMaxLife = sampleNPC.lifeMax;
                int targetDefense = sampleNPC.defense;

                if (NPC.lifeMax != targetMaxLife) {
                    NPC.lifeMax = targetMaxLife;
                    NPC.life = targetMaxLife;
                }
                NPC.defense = targetDefense;
            } else {
                if (NPC.lifeMax != 10000000) {
                    NPC.lifeMax = 10000000;
                    NPC.life = 10000000;
                    NPC.defense = 0;
                }
            }

            NPC.damage = DebuffUISystem.ContactDamageEnabled ? 1 : 0;

            // ==================== BYPASS FORCE HEAL SYSTEM ====================
            if (NPC.life < NPC.lifeMax) {
                NPC.life += 16666; 
                if (NPC.life > NPC.lifeMax) {
                    NPC.life = NPC.lifeMax;
                }
            }
        }

        public override void UpdateLifeRegen(ref int damage) {
            // Kosong
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (Main.netMode != NetmodeID.Server) {
                SoundStyle selectedSound = Main.rand.Next(3) switch {
                    0 => SoundID.NPCHit15,
                    1 => SoundID.NPCHit16,
                    _ => SoundID.NPCHit17
                };
                SoundEngine.PlaySound(selectedSound, NPC.Center);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            var uiInstance = ModContent.GetInstance<DebuffUISystem>()?.DebuffUI;
            if (uiInstance != null) {
                int assignedBuffID = uiInstance.SelectedBuffID;
                if (assignedBuffID > 0) {
                    target.AddBuff(assignedBuffID, 300);
                }
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (TextureAssets.Npc[Type] == null || !TextureAssets.Npc[Type].IsLoaded)
                return true;

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = texture.Frame(1, 1, 0, 0);
            Vector2 origin = new Vector2(frame.Width / 2f, frame.Height / 2f);
            Vector2 drawPos = new Vector2(NPC.Center.X, NPC.Bottom.Y - frame.Height / 2f) - screenPos;

            spriteBatch.Draw(texture, drawPos, frame, drawColor, NPC.rotation, origin, NPC.scale, NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);

            var uiInstance = ModContent.GetInstance<DebuffUISystem>()?.DebuffUI;

            // ==================== VISUAL ENCHANT 1: ICON SELEKSI PADA BODY DUMMY ====================
            if (DebuffUISystem.ContactDamageEnabled && uiInstance != null && uiInstance.SelectedBuffID > 0) {
                int selectedBuffID = uiInstance.SelectedBuffID;
                if (selectedBuffID < TextureAssets.Buff.Length && TextureAssets.Buff[selectedBuffID] != null && TextureAssets.Buff[selectedBuffID].IsLoaded) {
                    Texture2D bodyBuffTex = TextureAssets.Buff[selectedBuffID].Value;
                    Vector2 bodyBuffOrigin = bodyBuffTex.Size() / 2f;
                    Vector2 bodyBuffDrawPos = NPC.Center - screenPos;

                    float pulseScale = 1f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.08f;
                    Color auraColor = Color.White * (0.5f + (float)Math.Sin(Main.GlobalTimeWrappedHourly * 5f) * 0.15f);

                    spriteBatch.Draw(bodyBuffTex, bodyBuffDrawPos, null, auraColor * 0.35f, NPC.rotation, bodyBuffOrigin, NPC.scale * 0.95f * pulseScale, SpriteEffects.None, 0f);
                    spriteBatch.Draw(bodyBuffTex, bodyBuffDrawPos, null, Color.White * 0.8f, NPC.rotation, bodyBuffOrigin, NPC.scale * 0.75f, SpriteEffects.None, 0f);
                }
            }

            // ==================== VISUAL ENCHANT 2: DISPLAY DEBUFF AKTIF DI ATAS KEPALA ====================
            List<int> activeDebuffs = new List<int>();
            for (int i = 0; i < NPC.maxBuffs; i++) {
                if (NPC.buffType[i] > 0) {
                    activeDebuffs.Add(NPC.buffType[i]);
                }
            }

            if (activeDebuffs.Count > 0) {
                int maxIconsPerRow = 2;       
                float iconScale = 0.7f;       
                int iconSize = (int)(32 * iconScale);
                int gapX = 3;                 
                int gapY = 4;                 

                float startY = NPC.Top.Y - 18f - screenPos.Y;
                int totalRows = (int)Math.Ceiling((double)activeDebuffs.Count / maxIconsPerRow);

                for (int row = 0; row < totalRows; row++) {
                    int startIndex = row * maxIconsPerRow;
                    int countInRow = Math.Min(maxIconsPerRow, activeDebuffs.Count - startIndex);

                    float rowWidth = (countInRow * iconSize) + ((countInRow - 1) * gapX);
                    float startX = NPC.Center.X - (rowWidth / 2f) - screenPos.X;
                    float currentRowY = startY - (row * (iconSize + gapY));

                    for (int j = 0; j < countInRow; j++) {
                        int buffID = activeDebuffs[startIndex + j];
                        if (buffID >= TextureAssets.Buff.Length || TextureAssets.Buff[buffID] == null || !TextureAssets.Buff[buffID].IsLoaded)
                            continue;

                        Texture2D buffTexture = TextureAssets.Buff[buffID].Value;
                        Vector2 iconDrawPos = new Vector2(startX + j * (iconSize + gapX), currentRowY);

                        Rectangle bgRect = new Rectangle((int)iconDrawPos.X, (int)iconDrawPos.Y, iconSize, iconSize);
                        spriteBatch.Draw(TextureAssets.MagicPixel.Value, bgRect, Color.Black * 0.45f);

                        spriteBatch.Draw(buffTexture, iconDrawPos, null, Color.White * 0.9f, 0f, Vector2.Zero, iconScale, SpriteEffects.None, 0f);
                    }
                }
            }

            return false; 
        }
    }
}