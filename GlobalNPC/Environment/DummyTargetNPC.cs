using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.Audio; // REQUIRED: Untuk SoundEngine dan SoundStyle
using TheSanity.Buff;

namespace TheSanity.NPCs
{
    public class DummyTargetNPC : ModNPC
    {
        // Jalur sprite kustom milikmu (Pastikan filenya ada di folder NPCs dengan nama DummyTargetNPC.png)
        public override string Texture => "TheSanity/GlobalNPC/Environment/DummyTargetNPC";

        public override void SetStaticDefaults() {
            NPCID.Sets.NPCBestiaryDrawModifiers value = new NPCID.Sets.NPCBestiaryDrawModifiers() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, value);

            // Tegaskan ke game kalau NPC ini cuma punya 1 frame statis
            Main.npcFrameCount[Type] = 1;
        }

        public override void SetDefaults() {
            NPC.width = 34;
            NPC.height = 48;
            NPC.damage = 1; 
            NPC.defense = 0;
            NPC.lifeMax = 10000000; 
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;

            // FIX SUARA: Set jadi null agar tidak tabrakan dengan suara kustom acak yang kita buat di bawah
            NPC.HitSound = null; 
        }

        public override void AI() {
            var uiInstance = ModContent.GetInstance<DebuffUISystem>()?.DebuffUI;

            // ==================== ANTI-BOSS PROTECTION SYSTEM ====================
            if (AnyBossActive()) {
                NPC.active = false; // Lenyap seketika

                if (Main.netMode != NetmodeID.Server && uiInstance != null) {
                    uiInstance.SelectedBuffID = 0; 
                    uiInstance.SelectedNPCID = 0;  
                    uiInstance.PopulateList();     
                    
                    Main.NewText("?? Boss Detected!", Color.Red);
                }
                return; // Stop AI di sini agar tidak memproses kode di bawahnya
            }

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

            NPC.damage = 1;

            // ==================== AUTO-DESPAWN & GUI RESET (SCREEN LIMIT) ====================
            Rectangle screenRect = new Rectangle((int)Main.screenPosition.X, (int)Main.screenPosition.Y, Main.screenWidth, Main.screenHeight);
            
            if (!NPC.Hitbox.Intersects(screenRect)) {
                NPC.active = false; 
                
                if (Main.netMode != NetmodeID.Server && uiInstance != null) {
                    uiInstance.SelectedBuffID = 0; 
                    uiInstance.SelectedNPCID = 0;  
                    uiInstance.PopulateList();     
                    
                    Main.NewText("?? Dummy left the screen! Control Dashboard settings have been auto-reset.", Color.Orange);
                }
            }
        }

        public override void UpdateLifeRegen(ref int damage) {
            if (NPC.life < NPC.lifeMax) {
                NPC.lifeRegen += 500000; 
            }
        }

        // ==================== RANDOM HIT SOUND MECHANIC ====================
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
                    // FIX: Pengecekan kustom ControllCurse dihapus total. 
                    // Sekarang semua debuff durasinya disamakan rata (300 ticks = 5 detik)
                    target.AddBuff(assignedBuffID, 300);
                }
            }
        }

        // ==================== FIX KUSTOM RENDER SYSTEM ====================
        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (TextureAssets.Npc[Type] == null || !TextureAssets.Npc[Type].IsLoaded)
                return true;

            Texture2D texture = TextureAssets.Npc[Type].Value;
            Rectangle frame = texture.Frame(1, 1, 0, 0);

            Vector2 origin = new Vector2(frame.Width / 2f, frame.Height / 2f);
            Vector2 drawPos = new Vector2(NPC.Center.X, NPC.Bottom.Y - frame.Height / 2f) - screenPos;

            spriteBatch.Draw(
                texture,
                drawPos,
                frame,
                drawColor,
                NPC.rotation,
                origin,
                NPC.scale,
                NPC.spriteDirection == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
                0f
            );

            return false; 
        }

        // ==================== HELPER METHOD: DETEKSI BOSS ====================
        private bool AnyBossActive() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].active && Main.npc[i].boss) {
                    return true;
                }
            }
            return false;
        }
    }
}