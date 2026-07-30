using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    [AutoloadBossHead]
    public partial class PlutoHead : ModNPC
    {
        private bool spawnedSegments = false; 
        private ReLogic.Utilities.SlotId idleSoundSlot; 

        private bool initialized = false; 
        private int maxDashes = 0; 
        private float dashDuration = 0f; 

        private bool projSequenceActive = false; 
        private int projWaveCount = 0; 
        private int projSegmentIndex = 0; 
        private int projDelayTimer = 0; 
        private bool triggeredProjThisDash = false; 

        // 🛑 [PATTERN 6 - PROBE SWARM V3] State buat pattern probe -- lihat ProbeSwarmDash.cs
        // buat detail penggunaannya lengkap. Sengaja bukan pakai ai[1..3] karena slot itu sudah
        // dipakai penuh sama ExecuteDashPattern punya NormalDash.cs yang di-reuse buat gerakan
        // Pluto di pattern ini.
        private int probeSwarmPatternTimer = 0; 
        private int[] probeMissingDelayTimer = new int[Main.maxPlayers]; 

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoPart/PlutoHead"; 

        public override void SetStaticDefaults() {
            NPCID.Sets.TrailCacheLength[NPC.type] = 5; 
            NPCID.Sets.TrailingMode[NPC.type] = 1; 
            NPCID.Sets.MPAllowedEnemies[Type] = true; 
            NPCID.Sets.NoMultiplayerSmoothingByType[NPC.type] = true; 
        }

        public override void SetDefaults() {
            NPC.width = 60; 
            NPC.height = 60; 
            NPC.defense = 40;   
            NPC.lifeMax = 500000; 
            
            // ==========================================
            // LOKASI BALANCING: DAMAGE UTAMA BOSS PLUTO
            // ==========================================
            NPC.damage = 166;   
            
            NPC.HitSound = null; 
            NPC.DeathSound = SoundID.NPCDeath14; 
            NPC.noGravity = true; 
            NPC.noTileCollide = true; 
            NPC.knockBackResist = 0f; 
            NPC.boss = true; 
            // 🛑 [FIX RENDER ORDER] behindTiles DIHAPUS -- lihat komentar senada di PlutoBody.cs.
            // Head sekarang digambar di pass NPC normal (setelah PostDrawTiles yang gambar border),
            // jadi Head otomatis di ATAS border, bukan ketutup lagi.
            NPC.value = Item.buyPrice(0, 15, 0, 0); 
            Music = MusicID.Boss1; 
            NPC.scale = 1.5f; 
        }

        public override void SendExtraAI(BinaryWriter writer) {
            writer.Write(initialized); 
            writer.Write(maxDashes); 
            writer.Write(dashDuration); 
        }

        public override void ReceiveExtraAI(BinaryReader reader) {
            initialized = reader.ReadBoolean(); 
            maxDashes = reader.ReadInt32(); 
            dashDuration = reader.ReadSingle(); 
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), 300); 
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (NPC.life > 0) { 
                int randomHit = Main.rand.Next(1, 5); 
                SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/PlutoHit{randomHit}"), NPC.Center); 
            }
        }

        // 🛑 [LOKASI DAMAGE REDUCTION SAAT CHARGE] Selama Stage 2 (Charge) pattern Electro Nova,
        // Head kena reduction 90% dari SEMUA damage (lihat ElectroNovaChargeDamageReduction &
        // IsElectroNovaCharging di ElectroNovaDash.cs). Body & Tail dapet reduction yang sama lewat
        // PlutoBody.ModifyIncomingHit, yang ngecek status charging ini dari referensi Head-nya.
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            if (IsElectroNovaCharging) {
                modifiers.FinalDamage *= (1f - ElectroNovaChargeDamageReduction);
            }
        }

        public override void AI() {
            if (!SoundEngine.TryGetActiveSound(idleSoundSlot, out _)) { 
                idleSoundSlot = SoundEngine.PlaySound(new SoundStyle("TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/IdleLoop") { IsLooped = true, Volume = 0.4f }, NPC.Center); 
            }
            if (SoundEngine.TryGetActiveSound(idleSoundSlot, out var activeSound)) { 
                activeSound.Position = NPC.Center; 
            }

            if (!spawnedSegments) { 
                spawnedSegments = true; 
                NPC.TargetClosest(true); 
                if (Main.netMode != NetmodeID.MultiplayerClient) { 
                    int prevIdx = NPC.whoAmI; 
                    int bodyCount = 11; 
                    int totalSegmentsCount = bodyCount + 1; 
                    
                    for (int i = 0; i < totalSegmentsCount; i++) { 
                        bool isTail = (i == bodyCount); 
                        int type = isTail ? ModContent.NPCType<PlutoTail>() : ModContent.NPCType<PlutoBody>(); 
                        int spawned = NPC.NewNPC(NPC.GetSource_FromAI(), (int)NPC.Center.X, (int)NPC.Center.Y, type, NPC.whoAmI); 
                        
                        if (spawned != Main.maxNPCs) { 
                            Main.npc[spawned].ai[0] = i; 
                            Main.npc[spawned].ai[1] = prevIdx; 
                            Main.npc[spawned].ai[3] = NPC.whoAmI; 
                            Main.npc[spawned].realLife = NPC.whoAmI; 
                            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, spawned); 
                            
                            if (!isTail && i % 2 != 0) { 
                                for (int h = 0; h < 2; h++) { 
                                    int bodyHook = NPC.NewNPC(NPC.GetSource_FromAI(), (int)Main.npc[spawned].Center.X, (int)Main.npc[spawned].Center.Y, ModContent.NPCType<PlutoHook>(), spawned); 
                                    if (bodyHook != Main.maxNPCs) { 
                                        Main.npc[bodyHook].ai[0] = spawned; 
                                        Main.npc[bodyHook].ai[1] = h; 
                                        Main.npc[bodyHook].ai[2] = 0f; 
                                        if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, bodyHook); 
                                    }
                                }
                            }
                            prevIdx = spawned; 
                        }
                    }
                }
            }

            bool anyPlayerAlive = false; 
            for (int i = 0; i < Main.maxPlayers; i++) { 
                Player p = Main.player[i]; 
                if (p.active && !p.dead) { anyPlayerAlive = true; break; } 
            }

            if (anyPlayerAlive) { 
                NPC.timeLeft = 3600; 
            } else {
                NPC.velocity.Y += 0.8f; 
                NPC.timeLeft = 10; 
                return; 
            }

            Player player = Main.player[NPC.target]; 
            if (!player.active || player.dead) { 
                NPC.TargetClosest(true); 
                return; 
            }

            // 🛑 [PATTERN 8 - PEMBUKA PHASE 2] Dicek TIAP TICK, independen dari pattern apapun
            // yang lagi jalan sekarang -- biar momen HP nembus 50% kecatet PERSIS saat itu juga,
            // walau attack yang lagi jalan belum tentu abis di tick yang sama. Begitu kecatet,
            // pattern-picker di bawah (blok NPC.ai[0]==0f) bakal MAKSA Meteor Storm Dash jalan
            // duluan sebagai pembuka phase 2 -- bukan nunggu ke-roll RNG kayak biasa. Lihat
            // CheckMeteorPhase2Transition() di MeteorStormDash.cs.
            CheckMeteorPhase2Transition();

            if (!initialized) { 
                initialized = true; 
                // 🛑 [PATTERN 9 - SPAWN ANIMATION] Dulu langsung lompat ke Pattern 1 (Normal Dash).
                // Sekarang lompat ke Pattern 9 dulu (SATU KALI doang, seumur hidup NPC ini, makanya
                // di-gate lewat `initialized` -- BUKAN bagian dari pool gacha NPC.ai[0]==0f sama
                // sekali). Begitu animasinya kelar, ExecuteSpawnAnimationPattern() sendiri yang
                // "nembak" Pattern 1 secara langsung (lihat PlutoSpawnDash.cs). 
                NPC.ai[0] = 9f; 
                NPC.ai[1] = 0f; 
                NPC.ai[2] = 0f; 
                NPC.ai[3] = 0f; 
                NPC.netUpdate = true; 
            }

            // ==================================================================================
            // 🛠️ LOGIKA INVINCIBILITY (KEBAL) SAAT AIMING TELEPORT (PATTERN 3, STAGE 0)
            // Pluto tidak bisa diserang/ditembak saat sedang membidik (Pattern 3, Stage 0 / Aiming), 
            // tapi akan kembali bisa diserang saat dash dimulai (Stage 1 dst) atau saat berada di pattern lain.
            // ==================================================================================
            // 🛑 [PATTERN 9 - SPAWN ANIMATION] Kebal TOTAL selama seluruh durasi animasi spawn
            // (bukan cuma 1 stage doang kayak aiming Teleport Dash) -- SESUAI REQUEST, Pluto
            // (Head/Body/Tail, lihat sinkronisasinya di PlutoBody.cs) ga bisa kena hit apapun
            // selagi dia lagi kamera-pull-in / lari masuk / roar / kamera balik normal.
            if ((NPC.ai[0] == 3f && NPC.ai[1] == 0f) || NPC.ai[0] == 9f) {
                NPC.dontTakeDamage = true;
            } else {
                NPC.dontTakeDamage = false;
            }

            if (NPC.ai[0] == 0f) { 
                // 🛑 [PATTERN 8 DITAMBAHKAN] Meteor Storm Dash (lihat MeteorStormDash.cs) CUMA
                // ikut masuk pool kalau HP Pluto udah turun ke 50% ke bawah (CanRollMeteorShowerPattern).
                // Sebelum itu, pool tetep cuma 1-7 kayak biasa.
                //
                // 🛑 [PEMBUKA PHASE 2] TAPI kalau phase2MeteorOpenerPending lagi true (artinya HP
                // baru aja nembus 50% -- lihat CheckMeteorPhase2Transition), pattern-picker ini
                // SKIP random roll sama sekali dan LANGSUNG maksa Pattern 8 jalan sekarang juga,
                // persis di belakang attack yang barusan selesai. Abis dipakai sekali, flag-nya
                // langsung di-consume (false lagi) supaya sisanya pattern 8 balik jadi salah satu
                // opsi acak biasa di pool (bukan dipaksa terus-terusan).
                int nextPattern; 
                if (phase2MeteorOpenerPending) { 
                    nextPattern = 8; 
                    phase2MeteorOpenerPending = false; 
                } else { 
                    int patternPoolMax = CanRollMeteorShowerPattern ? 9 : 8; 
                    nextPattern = Main.rand.Next(1, patternPoolMax); 
                } 
                NPC.ai[0] = nextPattern; 
                NPC.ai[1] = 0f; 
                NPC.ai[2] = 0f; 
                NPC.ai[3] = 0f; 
                NPC.alpha = 0;   
                
                if (NPC.ai[0] == 1f) { 
                    maxDashes = Main.rand.Next(5, 9); 
                }
                else if (NPC.ai[0] == 2f) { 
                    maxDashes = Main.rand.Next(3, 6); 
                    projSequenceActive = false; 
                }
                else if (NPC.ai[0] == 3f) {
                    maxDashes = Main.rand.Next(3, 6); 
                }
                // Pattern 4 (Electro Nova) cuma sekali lempar bola per giliran, jadi tidak perlu maxDashes
                // Pattern 5 (Arena Bomb) juga tidak pakai maxDashes -- durasinya sendiri diatur di
                // ArenaBombDash.cs (ArenaOrbitDuration + nunggu semua PlutoBomb bersih)
                else if (NPC.ai[0] == 6f) {
                    // 🛑 [PATTERN 6 - PROBE SWARM V3] Gerakan Pluto masih reuse ExecuteDashPattern
                    // dari NormalDash.cs (ai[1..3] dipakai PERSIS kayak Pattern 1). Durasi GILIRAN
                    // SERANGAN ini tetap timer tetap ~20 detik (probeSwarmPatternTimer, lihat
                    // ProbeSwarmDash.cs) -- makanya maxDashes sengaja di-set kelewat gede biar dash-
                    // nya gak pernah abis duluan sebelum timer 20 detik kita yang motong. Timer &
                    // delay-array probe di-reset seger tiap kali giliran ini dimulai lagi -- TAPI
                    // probe-nya sendiri TIDAK di-reset/di-kill (mereka persisten lintas pattern).
                    maxDashes = 999;
                    probeSwarmPatternTimer = 0;
                    for (int i = 0; i < probeMissingDelayTimer.Length; i++) probeMissingDelayTimer[i] = 0;
                }
                // 🛑 [PATTERN 7 - CRYSTAL DIVE] Sama gayanya kayak Pattern 1/2/3 (pakai maxDashes
                // buat ngitung berapa kali menukik sebelum giliran serangan ini abis). Lihat
                // CrystalDivePattern.cs buat detail lengkap perilaku pattern-nya.
                else if (NPC.ai[0] == 7f) {
                    maxDashes = Main.rand.Next(4, 9); // minimal 4x, maksimal 8x dash -- SESUAI REQUEST
                    crystalSpawnDelayTimer = 0;
                }
                // 🛑 [PATTERN 8 - METEOR STORM DASH] Sama gayanya kayak Pattern 1 (pakai maxDashes
                // buat ngitung berapa kali dash sebelum giliran serangan ini abis, 5-9x). State
                // batch meteor/nuke-nya di-reset seger tiap kali giliran ini dimulai lagi lewat
                // ResetMeteorShowerState() -- lihat MeteorStormDash.cs buat detail lengkapnya.
                else if (NPC.ai[0] == 8f) {
                    maxDashes = Main.rand.Next(5, 9);
                    ResetMeteorShowerState();
                }
                NPC.netUpdate = true; 
            }

            if (NPC.ai[0] == 1f) { 
                ExecuteDashPattern(player); 
            }
            else if (NPC.ai[0] == 2f) { 
                ExecuteTrickDashPattern(player); 
            }
            else if (NPC.ai[0] == 3f) {
                ExecuteTeleportDashPattern(player);
            }
            else if (NPC.ai[0] == 4f) {
                ExecuteElectroNovaPattern(player);
            }
            else if (NPC.ai[0] == 5f) {
                ExecuteArenaBombPattern(player);
            }
            else if (NPC.ai[0] == 6f) {
                ExecuteProbeSwarmPattern(player);
            }
            else if (NPC.ai[0] == 7f) {
                ExecuteCrystalDivePattern(player);
            }
            else if (NPC.ai[0] == 8f) {
                ExecuteMeteorShowerPattern(player);
            }
            else if (NPC.ai[0] == 9f) {
                ExecuteSpawnAnimationPattern(player);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            // 🛑 [FIX RENDER ORDER - TAKE 4] Border SEKARANG digambar lewat hook global
            // PlutoArenaBorderSystem.PostDrawTiles() (lihat file itu), yang jalan TIAP FRAME tanpa
            // syarat -- independen dari NPC manapun lagi on-screen atau nggak. Makanya panggilan
            // manual DrawBorderRing() yang dulu ada di sini DIHAPUS (udah gak perlu & malah bisa
            // dobel-gambar kalau dibiarin). Border juga udah pasti kegambar di BAWAH Pluto karena
            // PostDrawTiles jalan sebelum pass NPC normal (lihat behindTiles yang dihapus di
            // PlutoHead & PlutoBody).
            //
            // 🛑 [FIX AIM LASER - PINDAH KE GLOBAL HOOK] Laser aim (Pattern 3, Stage 0) DULU digambar
            // manual di sini, tapi PreDraw() ini cuma dipanggil Terraria kalau HITBOX Pluto sendiri
            // masih onscreen -- bukan berdasarkan seberapa jauh laser-nya digambar. Akibatnya laser
            // ikut hilang total begitu Pluto (titik pusatnya) keluar dari area culling, padahal
            // secara visual dia harusnya masih nembus layar. Makanya sekarang dipindah ke
            // PlutoAimLaserSystem.PostDrawTiles() (lihat file itu) -- jalan TIAP FRAME tanpa syarat,
            // sama pola-nya kayak border arena, dan panjangnya dihitung dinamis (bukan hardcode
            // 2600f lagi) supaya selalu cukup nembus seluruh layar berapapun jauhnya kamera.

            if (NPC.alpha >= 255) {
                return false;
            }

            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC hook = Main.npc[i]; 
                if (hook.active && hook.type == ModContent.NPCType<PlutoHook>()) { 
                    int parentIdx = (int)hook.ai[0]; 
                    if (parentIdx >= 0 && parentIdx < Main.maxNPCs) { 
                        NPC parentNPC = Main.npc[parentIdx]; 
                        if (parentNPC.active && (parentIdx == NPC.whoAmI || parentNPC.realLife == NPC.whoAmI || parentNPC.ai[3] == NPC.whoAmI)) { 
                            if (PlutoHook.ChainTexture != null && PlutoHook.ChainTexture.IsLoaded) { 
                                Vector2 chainDrawPos = hook.Center; 
                                Vector2 toParent = parentNPC.Center - chainDrawPos; 
                                float chainRotation = toParent.ToRotation() - MathHelper.PiOver2; 
                                float chainStep = 24f * 1.35f; 
                                float distanceToParent = toParent.Length(); 

                                while (distanceToParent > chainStep) { 
                                    toParent.Normalize(); 
                                    chainDrawPos += toParent * chainStep; 
                                    toParent = parentNPC.Center - chainDrawPos; 
                                    distanceToParent = toParent.Length(); 
                                    spriteBatch.Draw(PlutoHook.ChainTexture.Value, chainDrawPos - screenPos, null, Color.White, chainRotation, 
                                        new Vector2(PlutoHook.ChainTexture.Value.Width * 0.5f, PlutoHook.ChainTexture.Value.Height * 0.5f), 1.35f, SpriteEffects.None, 0f); 
                                }
                            }
                            if (PlutoHook.HookTexture != null && PlutoHook.HookTexture.IsLoaded) { 
                                Vector2 hookOrigin = new Vector2(22f, 22f); 
                                spriteBatch.Draw(PlutoHook.HookTexture.Value, hook.Center - screenPos, hook.frame, Color.White, hook.rotation, hookOrigin, hook.scale, SpriteEffects.None, 0f); 
                            }
                        }
                    }
                }
            }

            int maxSegIndex = -1; 
            for (int j = 0; j < Main.maxNPCs; j++) { 
                NPC pot = Main.npc[j]; 
                if (pot.active && pot.ai[3] == NPC.whoAmI) { 
                    if (pot.type == ModContent.NPCType<PlutoBody>() || pot.type == ModContent.NPCType<PlutoTail>()) { 
                        int segIndex = (int)pot.ai[0]; 
                        if (segIndex > maxSegIndex) maxSegIndex = segIndex; 
                    }
                }
            }

            if (maxSegIndex >= 0) { 
                int arraySize = maxSegIndex + 1; 
                NPC[] segments = new NPC[arraySize]; 
                for (int j = 0; j < Main.maxNPCs; j++) { 
                    NPC pot = Main.npc[j]; 
                    if (pot.active && pot.ai[3] == NPC.whoAmI) { 
                        if (pot.type == ModContent.NPCType<PlutoBody>() || pot.type == ModContent.NPCType<PlutoTail>()) { 
                            int segIndex = (int)pot.ai[0]; 
                            if (segIndex >= 0 && segIndex < arraySize) segments[segIndex] = pot; 
                        }
                    }
                }

                for (int j = arraySize - 1; j >= 0; j--) { 
                    NPC seg = segments[j]; 
                    if (seg != null && seg.active) { 
                        if (seg.type == ModContent.NPCType<PlutoTail>()) { 
                            Texture2D texMain = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoPart/PlutoTail").Value; 
                            Texture2D texGlow = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoPart/PlutoTailGlow").Value; 
                            Vector2 origMain = texMain.Size() / 2f; Vector2 origGlow = texGlow.Size() / 2f; 
                            DrawPartWithElectroRimLight(spriteBatch, texMain, seg.Center - screenPos + new Vector2(0f, seg.gfxOffY), drawColor, seg.rotation, origMain, seg.scale, seg.Center);
                            Main.EntitySpriteDraw(texGlow, seg.Center - screenPos + new Vector2(0f, seg.gfxOffY), null, Color.White, seg.rotation, origGlow, seg.scale, SpriteEffects.None, 0); 
                        }
                        else if (seg.type == ModContent.NPCType<PlutoBody>()) { 
                            Texture2D texMain = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoPart/PlutoBody").Value; 
                            Texture2D texGlow = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoPart/PlutoBodyGlow").Value; 
                            Vector2 origMain = texMain.Size() / 2f; Vector2 origGlow = texGlow.Size() / 2f; 
                            DrawPartWithElectroRimLight(spriteBatch, texMain, seg.Center - screenPos + new Vector2(0f, seg.gfxOffY), drawColor, seg.rotation, origMain, seg.scale, seg.Center);
                            Main.EntitySpriteDraw(texGlow, seg.Center - screenPos + new Vector2(0f, seg.gfxOffY), null, Color.White, seg.rotation, origGlow, seg.scale, SpriteEffects.None, 0); 
                        }
                    }
                }
            }

            Texture2D textureMain = ModContent.Request<Texture2D>(Texture).Value; 
            Texture2D textureGlow = ModContent.Request<Texture2D>("TheSanity/GlobalNPC/Bosses/Pluto/PlutoPart/PlutoHeadGlow").Value; 
            Vector2 originMain = textureMain.Size() / 2f; Vector2 originGlow = textureGlow.Size() / 2f; 

            DrawPartWithElectroRimLight(spriteBatch, textureMain, NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY), drawColor, NPC.rotation, originMain, NPC.scale, NPC.Center);
            Main.EntitySpriteDraw(textureGlow, NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY), null, Color.White, NPC.rotation, originGlow, NPC.scale, SpriteEffects.None, 0); 
            return false; 
        }

        // =========================================================================
        // 🛑 [LOKASI EFEK RIM-LIGHT ELECTROBALL] Semua logic buat efek "kena sinar
        // ElectroBall" ada di 2 method di bawah ini. Lihat juga:
        // - Effects/PlutoElectroRimLight.fx      (shader HLSL-nya)
        // - PlutoElectroRimLightSystem.cs         (loader/register shader-nya)
        // =========================================================================

        // Radius MINIMUM (px, world-space) dari ElectroBall biar tetep kerasa efeknya walau
        // bolanya masih kecil banget (awal-awal Charging).
        private const float ElectroRimLightMinRadius = 260f;

        // 🛑 [LOKASI FIX SCALING] Jangkauan cahaya ngikutin ukuran bola SAAT ITU JUGA (radius
        // bola * angka ini), BUKAN angka tetap -- soalnya bolanya beneran membesar sampai 5x
        // selama Charging, jadi jangkauan & kekuatan cahayanya harus ikut membesar juga.
        private const float ElectroRimLightRadiusMultiplier = 8f;

        // Guard atas biar ga kebablasan pas bola udah di puncak gede-gedenya / auranya
        private const float ElectroRimLightMaxRadius = 3200f;

        // 🛑 [LOKASI WARNA CAHAYA] Sekarang MERAH nyala, ngikutin warna asli si bola (lihat
        // Color.Red yang dipakai buat lapisan solidBaseColor-nya di PlutoElectroBall.cs),
        // bukan oranye kayak sebelumnya.
        private static readonly Color ElectroRimLightColor = new Color(255, 25, 20);

        // 🛑 [LOKASI FLASH LEDAKAN] Radius & kekuatan KHUSUS buat momen bola meledak -- jauh
        // lebih gede & lebih terang daripada rim-light biasa, soalnya aura ledakannya sendiri
        // bisa sampai 10x diameter bola (lihat ExplosionAuraSizeMultiplier di PlutoElectroBall.cs).
        private const float ExplosionFlashRadiusMultiplier = 26f;
        private const float ExplosionFlashMaxRadius = 8000f;

        // Nyari PlutoElectroBall AKTIF TERDEKAT dari suatu titik. Sengaja TANPA batas radius di
        // sini (radius efektifnya baru bisa dihitung SETELAH tau ukuran bolanya, lihat
        // DrawPartWithElectroRimLight) -- ga masalah performa-wise soalnya jumlah ElectroBall
        // yang hidup bareng di map praktis cuma 0-1 (charge/homing Pattern 4 doang).
        private static Projectile FindNearestActiveElectroBall(Vector2 fromCenter, out float distance) {
            Projectile closest = null;
            distance = float.MaxValue;

            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile proj = Main.projectile[i];
                if (!proj.active || proj.type != ModContent.ProjectileType<PlutoElectroBall>()) continue;

                float d = Vector2.Distance(fromCenter, proj.Center);
                if (d < distance) {
                    distance = d;
                    closest = proj;
                }
            }

            return closest;
        }

        // Gambar SATU part Pluto (texture UTAMA, bukan layer glow-nya -- layer glow biarin tetep
        // full-bright terus kayak semula, itu emissive map bawaan, bukan hasil kena cahaya luar)
        // pakai shader rim-light kalau ada ElectroBall aktif di deket situ. Kalau nggak ada /
        // shader belum sempet ke-load / bolanya kejauhan, gambar biasa (fallback EntitySpriteDraw
        // normal, TIDAK PERNAH nge-skip gambar part-nya).
        //
        // worldCenter dipisah dari drawPos: worldCenter buat ngukur jarak & arah cahaya
        // (world-space), drawPos buat posisi gambar aktual di layar (udah dikurang
        // screenPos + gfxOffY dari pemanggilnya).
        private static void DrawPartWithElectroRimLight(SpriteBatch spriteBatch, Texture2D tex, Vector2 drawPos, Color drawColor, float rotation, Vector2 origin, float scale, Vector2 worldCenter) {
            Effect shader = PlutoElectroRimLightSystem.RimLightEffect;
            if (shader == null) {
                Main.EntitySpriteDraw(tex, drawPos, null, drawColor, rotation, origin, scale, SpriteEffects.None, 0);
                return;
            }

            Projectile ball = FindNearestActiveElectroBall(worldCenter, out float distance);

            if (ball == null) {
                Main.EntitySpriteDraw(tex, drawPos, null, drawColor, rotation, origin, scale, SpriteEffects.None, 0);
                return;
            }

            float ballRadius = ball.width * 0.5f;

            // 🛑 [LOKASI FLASH LEDAKAN] Kalau bolanya udah masuk window "mau meledak" (termasuk
            // SEBELUM ledakannya beneran kejadian -- lihat PreExplosionFlashWindow di
            // PlutoElectroBall.cs) sampai visual ledakannya abis, efeknya diganti total: bukan
            // rim-light bertahap kayak biasa, tapi FLASH yang nge-ramp naik dulu (telegraph)
            // sebelum boom, lalu decay CEPAT sesudahnya (mirip shockwave cahaya).
            PlutoElectroBall electroBall = ball.ModProjectile as PlutoElectroBall;
            bool isFlashActive = electroBall != null && electroBall.IsFlashActive;

            float effectiveRadius;
            float intensity;
            float ambient;
            float rimSharpness;

            if (isFlashActive) {
                effectiveRadius = MathHelper.Clamp(ballRadius * ExplosionFlashRadiusMultiplier, ElectroRimLightMinRadius, ExplosionFlashMaxRadius);

                if (distance > effectiveRadius) {
                    Main.EntitySpriteDraw(tex, drawPos, null, drawColor, rotation, origin, scale, SpriteEffects.None, 0);
                    return;
                }

                float distFalloff = 1f - MathHelper.Clamp(distance / effectiveRadius, 0f, 1f);
                distFalloff = distFalloff * distFalloff * (3f - 2f * distFalloff);

                // flashProgress: 0 = mulai window pre-explosion, 0.5 = PAS meledak, 1 = visual
                // ledakan abis. Sebelum 0.5 -> nge-ramp NAIK (build-up/telegraph, + flicker kecil
                // biar berasa kayak listrik yg mau meledak). Setelah 0.5 -> decay CEPAT pangkat 3
                // (kilat sesaat, bukan fade halus berlama-lama).
                float flashProgress = electroBall.FlashProgress;
                float flashPower;
                if (flashProgress < 0.5f) {
                    float buildT = flashProgress / 0.5f;
                    float flicker = 0.85f + 0.15f * (float)Math.Sin(Main.GlobalTimeWrappedHourly * 40f);
                    flashPower = buildT * buildT * flicker;
                }
                else {
                    float decayT = (flashProgress - 0.5f) / 0.5f;
                    float fade = 1f - decayT;
                    flashPower = fade * fade * fade;
                }

                intensity = flashPower * distFalloff * 6.5f;
                ambient = flashPower * distFalloff * 1.4f;
                rimSharpness = flashPower;
            }
            else {
                // Radius (jangkauan) & kekuatan cahaya dihitung dari ukuran bola SAAT INI
                // (ball.width udah otomatis kebawa scale-nya, lihat UpdateHitboxToScale() di
                // PlutoElectroBall.cs) -- jadi pas bola lagi kecil di awal Charging, jangkauannya
                // pendek & lembut; pas udah gede penuh (5x), jangkauannya jauh & terang banget.
                effectiveRadius = MathHelper.Clamp(ballRadius * ElectroRimLightRadiusMultiplier, ElectroRimLightMinRadius, ElectroRimLightMaxRadius);

                if (distance > effectiveRadius) {
                    Main.EntitySpriteDraw(tex, drawPos, null, drawColor, rotation, origin, scale, SpriteEffects.None, 0);
                    return;
                }

                // Smoothstep (bukan cuma dikuadratin) biar transisi nyala/redup-nya lebih mulus,
                // ga ada "patahan" pas nyebrang batas radius.
                float t = 1f - MathHelper.Clamp(distance / effectiveRadius, 0f, 1f);
                float falloff = t * t * (3f - 2f * t);

                // Makin GEDE bolanya, makin "menyilaukan" efeknya -- bukan cuma jangkauannya
                // doang yang nambah, tapi kekuatan cahayanya juga.
                float sizeBoost = MathHelper.Clamp(ballRadius / 90f, 1f, 3.2f);

                intensity = falloff * sizeBoost * 2.2f;
                ambient = falloff * sizeBoost * 0.4f;
                rimSharpness = falloff;
            }

            // Arah dari part ini KE bola, di world-space (Y ke bawah, standar XNA/Terraria).
            Vector2 worldDir = ball.Center - worldCenter;
            if (worldDir == Vector2.Zero) worldDir = -Vector2.UnitY;
            worldDir.Normalize();

            // 🛑 [PENTING] Di-unrotate balik (-rotation) supaya arahnya sesuai orientasi
            // TEXTURE SEBELUM dirotate pas digambar -- UV/pixel texture itu "diem" di
            // tempat, yang muter cuma vertex-nya doang pas EntitySpriteDraw motret si
            // texture dengan rotasi. Tanpa ini, rim-nya bakal keliatan "salah sisi" tiap
            // kali Pluto muter/miring.
            Vector2 localDir = worldDir.RotatedBy(-rotation);

            shader.Parameters["uLightDir"].SetValue(localDir);
            shader.Parameters["uLightColor"].SetValue(ElectroRimLightColor.ToVector3());
            shader.Parameters["uIntensity"].SetValue(intensity);
            // Ambient fill: seluruh sisi yg kesorot ikut nyala, ga cuma tepinya doang -- ini
            // yang bikin efeknya kerasa "gede"/niat (dan pas meledak, kerasa "diguyur" flash).
            shader.Parameters["uAmbient"].SetValue(ambient);
            // Rim makin TEBAL pas makin deket / makin gede bolanya / makin awal fase ledakan.
            Vector2 rimWidth = new Vector2(3f / tex.Width, 3f / tex.Height) * MathHelper.Lerp(1.5f, 6f, rimSharpness);
            shader.Parameters["uRimWidth"].SetValue(rimWidth);
            shader.CurrentTechnique.Passes[0].Apply();

            // Sama kayak pola DrawExplosionAura() di PlutoElectroBall.cs: End() batch
            // Deferred yang lagi jalan, Begin() ulang pakai Immediate + effect custom
            // buat 1 draw call ini doang, lalu balik ke Deferred normal biar draw call
            // lain sesudahnya (hook, chain, glow layer, dst) ga ikut kena shader ini.
            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, shader, Main.GameViewMatrix.TransformationMatrix);

            Main.EntitySpriteDraw(tex, drawPos, null, drawColor, rotation, origin, scale, SpriteEffects.None, 0);

            spriteBatch.End();
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);
        }
    }
}