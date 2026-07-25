using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    public class PlutoHook : ModNPC
    {
        public static Asset<Texture2D> ChainTexture;
        public static Asset<Texture2D> HookTexture;

        private Vector2 anchorPos = Vector2.Zero;
        private int anchorTimer = 0;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoPart/PlutoHook";

        public override void SetStaticDefaults() {
            NPCID.Sets.NoMultiplayerSmoothingByType[NPC.type] = true;

            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);

            Main.npcFrameCount[Type] = 2; 

            if (!Main.dedServ) {
                ChainTexture = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoPart/PlutoChain");
                HookTexture = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoPart/PlutoHook");
            }
        }

        public override void SetDefaults() {
            NPC.damage = 0; 
            NPC.aiStyle = -1;
            NPC.width = 30;
            NPC.height = 30;
            NPC.lifeMax = 10000;
            NPC.dontTakeDamage = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.behindTiles = true; 
            NPC.scale = 1.35f; 
        }

        public override void AI() {
            int parentIdx = (int)NPC.ai[0];
            
            if (parentIdx < 0 || parentIdx >= Main.maxNPCs || !Main.npc[parentIdx].active || 
               (Main.npc[parentIdx].type != ModContent.NPCType<PlutoHead>() && Main.npc[parentIdx].type != ModContent.NPCType<PlutoBody>())) {
                NPC.active = false;
                NPC.netUpdate = true;
                return;
            }

            NPC parent = Main.npc[parentIdx];
            float tentacleIndex = NPC.ai[1]; 
            float parentSpeed = parent.velocity.Length(); 

            // Cek jarak dari cakar ke tubuh penopangnya saat ini
            float distToParent = Vector2.Distance(parent.Center, NPC.Center);

            // ==================================================================================
            // 🛠️ BALANCING VISUAL: DETEKSI TELEPORT INSTAN (SNAP DISTANCE)
            // Diturunkan dari 1500f ke 1200f agar cakar langsung ikut berpindah tempat secara instan
            // begitu Pluto melakukan teleportasi atau dash super cepat tanpa jeda visual.
            // ==================================================================================
            if (distToParent > 1200f) {
                anchorPos = FindAnchorPoint(parent, tentacleIndex);
                NPC.Center = anchorPos;
                NPC.velocity = Vector2.Zero;
                NPC.ai[2] = 1f; // Tancapkan seketika di posisi baru
                NPC.netUpdate = true;
            }

            if (anchorPos == Vector2.Zero) {
                anchorPos = FindAnchorPoint(parent, tentacleIndex);
            }

            if (NPC.ai[2] == 0f) {
                // STATE 0: MELANGKAH KE TITIK JANGKAR (STEPPING)
                Vector2 dirToAnchor = anchorPos - NPC.Center;
                float dist = dirToAnchor.Length();

                // ==================================================================================
                // 🛠️ BALANCING VISUAL: KECEPATAN DYNAMIC STEPPING (SANGAT EKSTREM)
                // Minimal kecepatan langkah dinaikkan drastis ke 300f dan pengali kecepatan menjadi 30.0f.
                // Ini menjamin Hook akan berpindah dari posisi lama ke baru dalam hitungan 1-2 frame saja!
                // ==================================================================================
                float stepSpeed = Math.Max(parentSpeed * 30.0f, 300f);

                if (dist <= stepSpeed || dist < 15f) { 
                    NPC.Center = anchorPos;
                    NPC.velocity = Vector2.Zero;
                    NPC.ai[2] = 1f; 
                    anchorTimer = 0;
                    NPC.netUpdate = true;
                } else {
                    NPC.velocity = dirToAnchor.SafeNormalize(Vector2.Zero) * stepSpeed;
                }
            } 
            else if (NPC.ai[2] == 1f) {
                // STATE 1: TERTANCAP DIAM (ANCHORED)
                NPC.Center = anchorPos;
                NPC.velocity = Vector2.Zero;
                anchorTimer++;

                Vector2 parentForward = parent.rotation.ToRotationVector2();
                Vector2 parentToHook = NPC.Center - parent.Center;
                float dotProduct = Vector2.Dot(parentForward, parentToHook);

                NPC segmentBehind = null;
                int targetBehindIndex = (int)parent.ai[0] + 1;

                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC pot = Main.npc[i];
                    if (pot.active && pot.ai[3] == parent.ai[3]) { 
                        if ((pot.type == ModContent.NPCType<PlutoBody>() || pot.type == ModContent.NPCType<PlutoTail>()) && (int)pot.ai[0] == targetBehindIndex) {
                            segmentBehind = pot;
                            break;
                        }
                    }
                }

                float dotProductBehind = 0f;
                if (segmentBehind != null) {
                    Vector2 behindToHook = NPC.Center - segmentBehind.Center;
                    dotProductBehind = Vector2.Dot(parentForward, behindToHook);
                }

                NPC sisterHook = null;
                for (int i = 0; i < Main.maxNPCs; i++) {
                    NPC other = Main.npc[i];
                    if (other.active && other.type == NPC.type && other.whoAmI != NPC.whoAmI && other.ai[0] == parentIdx) {
                        sisterHook = other;
                        break;
                    }
                }

                bool sisterIsAnchored = (sisterHook == null || sisterHook.ai[2] == 1f);
                float sisterDot = 0f;

                if (sisterHook != null && sisterIsAnchored) {
                    Vector2 sisterToParent = sisterHook.Center - parent.Center;
                    sisterDot = Vector2.Dot(parentForward, sisterToParent);
                } else if (sisterHook == null) {
                    sisterIsAnchored = true; 
                }

                bool needsToStep = false;

                if (sisterIsAnchored) {
                    bool leftBehind = (segmentBehind != null) ? (dotProductBehind < 10f) : (dotProduct < -40f);

                    bool sisterIsParallel = (sisterHook != null && sisterDot < 50f && sisterDot >= -20f);
                    bool sisterIsAheadOfUs = (sisterHook != null && sisterDot > dotProduct + 30f);

                    if (leftBehind) {
                        needsToStep = true;
                    }
                    else if (sisterIsParallel && !sisterIsAheadOfUs) {
                        needsToStep = true;
                    }

                    if (sisterHook != null && dotProduct > 120f && sisterDot > 120f) {
                        if (tentacleIndex == 0 && dotProduct < 150f) {
                            needsToStep = true;
                        }
                    }
                }

                // ==================================================================================
                // 🛠️ BALANCING VISUAL: BATAS MAKSIMUM RANTAI MELAR (MAX DISTANCE THRESHOLD)
                // Diperketat dari maksimal 2500f menjadi hanya 1200f. Jika Pluto melesat cepat, 
                // cakar tidak akan lagi diam menonton dari kejauhan, melainkan langsung dipaksa melangkah!
                // Batas durasi menempel (anchorTimer) juga dipotong dari 180 tick menjadi 90 tick (1.5 detik).
                // ==================================================================================
                float maxDistanceThreshold = MathHelper.Clamp(350f + parentSpeed * 5.0f, 350f, 1200f);
                bool forceStep = (distToParent > maxDistanceThreshold) || (anchorTimer > 90);

                if (needsToStep || forceStep) {
                    anchorPos = FindAnchorPoint(parent, tentacleIndex);
                    NPC.ai[2] = 0f; 
                    anchorTimer = 0;
                    NPC.netUpdate = true;
                }
            }

            Vector2 toParent = parent.Center - NPC.Center;
            NPC.rotation = toParent.ToRotation() + MathHelper.PiOver2;
        }

        private Vector2 FindAnchorPoint(NPC parent, float index) {
            Vector2 forward = parent.rotation.ToRotationVector2();
            float sideAngle = parent.rotation + (index == 0 ? -MathHelper.PiOver2 : MathHelper.PiOver2);
            Vector2 side = sideAngle.ToRotationVector2();

            float parentSpeed = parent.velocity.Length();
            
            // Jangkauan deteksi ke depan diperbesar agar langkahnya memproyeksikan area pijak yang jauh lebih maju
            float targetForwardReach = MathHelper.Clamp(250f + parentSpeed * 8f, 250f, 1800f);
            float sideWidth = 100f; 

            float maxScanDistance = targetForwardReach + 200f;

            for (float d = 40f; d < maxScanDistance; d += 12f) {
                Vector2 scanPos = parent.Center + (forward * d) + (side * sideWidth);
                int x = (int)(scanPos.X / 16f);
                int y = (int)(scanPos.Y / 16f);

                if (WorldGen.InWorld(x, y)) {
                    Tile tile = Main.tile[x, y];
                    if (tile.HasTile && Main.tileSolid[tile.TileType] && !Main.tileSolidTop[tile.TileType]) {
                        return scanPos; 
                    }
                }
            }

            float sway = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f + index + parent.whoAmI) * 8f;
            return parent.Center + (forward * targetForwardReach) + (side * (sideWidth + sway));
        }

        public override void FindFrame(int frameHeight) {
            NPC.frame.Width = 44;
            NPC.frame.Height = 44;
            NPC.frame.Y = 0;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }

        public override bool CheckActive() => false;
    }
}