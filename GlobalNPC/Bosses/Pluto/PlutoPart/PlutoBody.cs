using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart
{
    public class PlutoBody : ModNPC
    {
        private ReLogic.Utilities.SlotId idleSoundSlot;

        // 🛑 [LOKASI DAMAGE REDUCTION SAAT CHARGE] Referensi ke NPC PlutoHead utama (di-cache tiap
        // AI() lewat ai[3]), dipakai di ModifyIncomingHit buat ngecek apakah Head-nya lagi di Stage 2
        // (Charge) Pattern Electro Nova -- kalau iya, Body/Tail ini ikut dapet reduction 90% damage
        // yang sama kayak Head-nya.
        private NPC cachedMainHead;

        // ==================================================================================
        // 🧠 SMART PLACE SEARCH
        // Nandain apakah rotasi segmen ini udah pernah di-set sekali. Dipakai biar pas baru
        // spawn nggak langsung dianggap "nikuk tajam" gara-gara NPC.rotation masih default 0.
        // ==================================================================================
        private bool hasInitializedFacing = false;

        // Di bawah sudut ini (per tick) dianggap belokan biasa -> segmen jalan kek biasa aja
        // (chain langsung ngikut posisi frontSegment, sama kayak behavior lama, tanpa delay).
        private const float SharpTurnThresholdDegrees = 45f;

        // Kecepatan minimal segmen "mengejar" sudut target pas lagi nikuk tajam & diem/pelan,
        // biar body/tail geser menyesuaikan lokasinya secara halus (bukan nyentak instan) pas
        // kepala muter sampe 160° atau lebih.
        private const float BaseTurnRateDegreesPerTick = 12f;

        // ==================================================================================
        // 🏎️ Tambahan kecepatan catch-up rotation per unit kecepatan frontSegment. Dipakai biar
        // pas pattern lagi cepet (misal Normal Dash / Trick Dash ~44 px/tick), body nggak
        // ketinggalan muter dan keliatan "miring" pas ikutan meluncur.
        // ==================================================================================
        private const float TurnRateSpeedMultiplier = 1.8f;

        // Batas atas biar catch-up-nya tetep ada rasa "geser", bukan langsung snap instan sekalipun
        // frontSegment-nya lagi ngebut banget.
        private const float MaxTurnRateCapDegreesPerTick = 100f;

        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoPart/PlutoBody";

        public override void SetStaticDefaults() {
            NPCID.Sets.NoMultiplayerSmoothingByType[NPC.type] = true;
            NPCID.Sets.CantTakeLunchMoney[Type] = true;
            NPCID.Sets.ImmuneToAllBuffs[Type] = true;

            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);
        }

        public override void SetDefaults() {
            NPC.width = 48; 
            NPC.height = 48;
            NPC.defense = 40;   
            NPC.damage = 66;    
            NPC.lifeMax = 500000;
            NPC.HitSound = null; 
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            // 🛑 [FIX RENDER ORDER] behindTiles DIHAPUS. Sebelumnya Body/Tail digambar di pass
            // "NPC di belakang tiles" (yang jalan PALING AWAL, sebelum tiles solid & sebelum border
            // digambar lewat PostDrawTiles hook) -- jadi border yang skrg digambar lewat hook global
            // itu ketimpa kegambar SETELAH Body/Tail, bikin border nongol di ATAS Pluto (kebalik dari
            // yang diinginkan). Sekarang Body/Tail digambar di pass NPC normal (SETELAH tiles &
            // SETELAH PostDrawTiles), jadi otomatis nongol DI ATAS border, seperti PlutoHead.
            NPC.dontCountMe = true; 
            NPC.scale = 1.5f; 
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

        public override void AI() {
            if (!SoundEngine.TryGetActiveSound(idleSoundSlot, out _)) {
                idleSoundSlot = SoundEngine.PlaySound(new SoundStyle("TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/IdleLoop") { IsLooped = true, Volume = 0.4f }, NPC.Center);
            }
            if (SoundEngine.TryGetActiveSound(idleSoundSlot, out var activeIdleSound)) {
                activeIdleSound.Position = NPC.Center;
            }

            int frontIndex = (int)NPC.ai[1];
            NPC frontSegment = null;
            if (frontIndex >= 0 && frontIndex < Main.maxNPCs) {
                NPC potFront = Main.npc[frontIndex];
                if (potFront.active && (potFront.type == ModContent.NPCType<PlutoHead>() || potFront.type == ModContent.NPCType<PlutoBody>())) {
                    frontSegment = potFront;
                }
            }

            int headIndex = (int)NPC.ai[3];
            NPC mainHead = null;
            if (headIndex >= 0 && headIndex < Main.maxNPCs) {
                NPC potHead = Main.npc[headIndex];
                if (potHead.active && potHead.type == ModContent.NPCType<PlutoHead>()) {
                    mainHead = potHead;
                }
            }
            
            if (frontSegment == null || mainHead == null) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    NPC.active = false; NPC.HitEffect();
                    NetMessage.SendData(MessageID.SyncNPC, -1, -1, null, NPC.whoAmI);
                }
                return;
            }

            // 🛑 [LOKASI DAMAGE REDUCTION SAAT CHARGE] Simpan referensi Head-nya buat dipakai nanti
            // di ModifyIncomingHit (method itu dipanggil terpisah dari AI(), jadi butuh field ini).
            cachedMainHead = mainHead;

            // ==================================================================================
            // 🛠️ FIX: SINKRONISASI INVINCIBILITY DENGAN HEAD
            // Sebelumnya cuma PlutoHead yang di-set dontTakeDamage pas fase Aiming Teleport Dash
            // (Pattern 3 / ai[0]==3f, Stage 0 / ai[1]==0f -- lagi transparent total, NPC.alpha=255),
            // jadi Body & Tail tetep bisa kena damage walaupun keliatannya invisible. Sekarang
            // Body & Tail ikut kebal persis di fase yang sama kayak Head.
            // ==================================================================================
            // 🛑 [PATTERN 9 - SPAWN ANIMATION] Body & Tail ikut kebal TOTAL selama seluruh durasi
            // animasi spawn (bukan cuma 1 stage doang kayak Teleport Dash) -- SESUAI REQUEST.
            bool isTeleportInvinciblePhase = (mainHead.ai[0] == 3f && mainHead.ai[1] == 0f) || mainHead.ai[0] == 9f;
            NPC.dontTakeDamage = isTeleportInvinciblePhase;

            // ==================================================================================
            // 🎯 Selain invincibility, Smart Place Search juga di-nonaktifin buat SELURUH pattern
            // Teleport Dash (aiming + dash-nya, ai[0]==3f), bukan cuma pas invincible doang -- soalnya
            // kalau smart-clamp ikut jalan di dash super cepat pattern ini, hasilnya keliatan goofy.
            // Pattern lain (Normal Dash, Trick Dash, Electro Nova) tetep pakai Smart Body seperti biasa.
            // ==================================================================================
            // 🛑 [PATTERN 9 - SPAWN ANIMATION] Sama kayak Teleport Dash, gerakan lari-masuknya cepat
            // & posisinya di-snap manual (lihat PlutoSpawnDash.cs) -- kalau smart-clamp ikut jalan
            // di sini, hasilnya bakal keliatan "ketarik" aneh pas Pluto baru nongol dari luar layar.
            bool isTeleportDashPattern = mainHead.ai[0] == 3f || mainHead.ai[0] == 9f;

            float spacingDistance = 38f * NPC.scale; 
            Vector2 directionToFront = frontSegment.Center - NPC.Center;
            
            if (directionToFront != Vector2.Zero) {
                float targetAngle = directionToFront.ToRotation();
                float finalAngle;

                // ==================================================================================
                // 🧠 SMART PLACE SEARCH
                // - Selisih sudut ke frontSegment kecil (belokan biasa), atau segmen baru pertama
                //   kali update, atau lagi di pattern Teleport Dash (aiming maupun dash-nya)
                //   -> langsung ikut kek biasa (chain instan, sama kayak behavior lama).
                // - Selisihnya udah "nikuk tajam" (misal kepala muter sampe 160°) di attack lain
                //   -> sudut di-clamp per tick, jadi body & tail geser menyesuaikan lokasinya
                //      secara halus ngikutin lengkungan, bukan langsung nyentak instan.
                // ==================================================================================
                if (!hasInitializedFacing || isTeleportDashPattern) {
                    finalAngle = targetAngle;
                } else {
                    float angleDiff = MathHelper.WrapAngle(targetAngle - NPC.rotation);
                    float angleDiffDegrees = Math.Abs(MathHelper.ToDegrees(angleDiff));

                    if (angleDiffDegrees <= SharpTurnThresholdDegrees) {
                        finalAngle = targetAngle;
                    } else {
                        float frontSpeed = frontSegment.velocity.Length();
                        float dynamicTurnRateDegrees = MathHelper.Clamp(
                            BaseTurnRateDegreesPerTick + frontSpeed * TurnRateSpeedMultiplier,
                            BaseTurnRateDegreesPerTick, MaxTurnRateCapDegreesPerTick);

                        float maxStepRad = MathHelper.ToRadians(dynamicTurnRateDegrees);
                        float clampedDelta = MathHelper.Clamp(angleDiff, -maxStepRad, maxStepRad);
                        finalAngle = NPC.rotation + clampedDelta;
                    }
                }

                NPC.rotation = finalAngle;
                NPC.Center = frontSegment.Center - finalAngle.ToRotationVector2() * spacingDistance;
                hasInitializedFacing = true;
            }

            NPC.timeLeft = frontSegment.timeLeft;
        }

        public override bool CheckActive() => false;
        public override bool? DrawHealthBar(byte hbPosition, ref float scale, ref Vector2 position) => false;
        // 🛑 [LOKASI DAMAGE REDUCTION SAAT CHARGE] Selain DisableCrit() yang udah ada dari awal,
        // sekarang ikut ngecek status charging Head-nya (lewat cachedMainHead) -- kalau Head lagi
        // di Stage 2 (Charge) Pattern Electro Nova, Body ini (dan Tail, karena PlutoTail : PlutoBody
        // ga nge-override method ini) ikut kena reduction 90% damage yang sama.
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers) {
            modifiers.DisableCrit();

            if (cachedMainHead != null && cachedMainHead.active && cachedMainHead.ModNPC is PlutoHead headMod && headMod.IsElectroNovaCharging) {
                modifiers.FinalDamage *= (1f - PlutoHead.ElectroNovaChargeDamageReduction);
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            return false;
        }
    }
}
