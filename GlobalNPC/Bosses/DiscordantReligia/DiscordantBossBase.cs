using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Bosses.DiscordantReligia
{
    /// <summary>
    /// Base class bersama untuk kedua paruh DiscordantReligia (ChronoReligia / PlagueReligia).
    /// Sebelumnya ChronoReligia.cs dan PlagueReligia.cs punya ~150 baris identik: wing loading,
    /// hit-flash, target acquisition, deteksi partner + enrage, shield "menunggu partner phase 2"
    /// beserta dust-nya, dan pesan OnKill "something went wrong in the Sky". Kalau ada bug di
    /// salah satu bagian itu, gampang lupa fix di boss satunya.
    ///
    /// State machine tiap boss (ChronoState/PlagueState, attack pool, method Attack_*, dan visual
    /// PreDraw) sengaja TIDAK dipindah ke sini karena itu memang beda per boss - base class ini
    /// cuma menampung yang benar-benar identik.
    /// </summary>
    public abstract class DiscordantBossBase : ModNPC
    {
        public ref float AI_Timer => ref NPC.ai[1];
        public ref float AI_Phase2Flag => ref NPC.ai[2];
        public ref float AI_AttackCounter => ref NPC.ai[3];

        public bool IsPhase2 => AI_Phase2Flag >= 1f;
        public bool IsEnraged { get; protected set; }

        protected int hitFlashTimer = 0;

        // Instance field (bukan static) supaya slot wing Chrono & Plague tidak saling menimpa
        // kalau base class ini dipakai lebih dari satu tipe boss dengan wing berbeda.
        private int wingSlot = -1;
        protected int WingSlot => wingSlot;

        /// <summary>ItemID wing yang dipakai untuk render boss ini (mis. DemonWings, AngelWings).</summary>
        protected abstract int WingItemType { get; }

        /// <summary>ModNPC type dari paruh pasangannya - dipakai untuk phase-sync dan pesan OnKill.</summary>
        protected abstract int PartnerNPCType { get; }

        /// <summary>Dust untuk partikel idle yang di-spawn tiap tick (fizzle trail).</summary>
        protected abstract int AmbientDustType { get; }

        /// <summary>Dust untuk ring shield saat menunggu partner menyusul ke phase 2.</summary>
        protected abstract int ShieldDustType { get; }

        /// <summary>Dust untuk burst hit-flash di HitEffect.</summary>
        protected abstract int HitDustType { get; }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
        }

        protected void EnsureWingSlot() {
            if (wingSlot <= 0) {
                wingSlot = ContentSamples.ItemsByType[WingItemType].wingSlot;
            }
        }

        /// <summary>
        /// Bagian awal AI() yang identik di kedua boss: target acquisition/validity, deteksi
        /// partner + flag enrage, trigger phase 2 di setengah HP, shield "menunggu partner
        /// phase 2" + dust-nya, dan timer bersama (AI_Timer, hitFlashTimer).
        ///
        /// Return null berarti tidak ada target valid - caller harus langsung `return` dari
        /// AI() miliknya sendiri, sama seperti behavior asli (dorong ke atas lalu berhenti).
        /// Rotasi sprite (NPC.rotation) sengaja TIDAK di-set di sini karena multiplier-nya beda
        /// tipis antara Chrono (0.02f) dan Plague (0.03f) - subclass yang set setelah manggil ini.
        /// </summary>
        protected Player RunSharedAI() {
            NPC.timeLeft = 3600;

            NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];

            if (NPC.target < 0 || NPC.target == 255 || target.dead || !target.active) {
                NPC.velocity.Y -= 0.3f;
                return null;
            }

            int partnerIndex = NPC.FindFirstNPC(PartnerNPCType);
            IsEnraged = partnerIndex < 0;

            if (NPC.life <= (NPC.lifeMax / 2) && AI_Phase2Flag == 0f) {
                AI_Phase2Flag = 1f;
                OnEnterPhase2();
            }

            bool waitingForPartnerPhase2 = false;
            if (partnerIndex >= 0 && Main.npc[partnerIndex].ModNPC is DiscordantBossBase partner) {
                waitingForPartnerPhase2 = IsPhase2 && !partner.IsPhase2;
            }
            NPC.dontTakeDamage = waitingForPartnerPhase2;

            if (waitingForPartnerPhase2 && Main.rand.NextBool(6)) {
                for (int i = 0; i < 3; i++) {
                    Vector2 shieldPos = NPC.Center + Main.rand.NextVector2CircularEdge(NPC.width * 0.9f, NPC.height * 0.9f);
                    Dust d = Dust.NewDustDirect(shieldPos, 0, 0, ShieldDustType, 0, 0, 100, default, 1.6f);
                    d.noGravity = true;
                    d.velocity = Vector2.Zero;
                }
            }

            AI_Timer++;
            if (hitFlashTimer > 0) hitFlashTimer--;

            NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, AmbientDustType, 0, 0, 100, default, 1.4f);
                d.noGravity = true;
            }

            return target;
        }

        /// <summary>Dipanggil sekali saat NPC.life turun ke setengah HP. Subclass set State/AI_Timer-nya sendiri di sini.</summary>
        protected abstract void OnEnterPhase2();

        public override void HitEffect(NPC.HitInfo hit) {
            hitFlashTimer = 8;
            for (int i = 0; i < 4; i++) {
                Dust d = Dust.NewDustDirect(NPC.position, NPC.width, NPC.height, HitDustType, hit.HitDirection, -1f, 100, default, 1.2f);
                d.noGravity = true;
            }
        }

        public override void OnKill() {
            // Cuma umumkan kalau KEDUA paruh DiscordantReligia sudah mati. Kalau partner masih
            // hidup, ini baru salah satu yang mati duluan - diam saja.
            bool partnerAlive = false;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == PartnerNPCType) {
                    partnerAlive = true;
                    break;
                }
            }

            if (!partnerAlive) {
                ReligiaSkyOreGen.SpawnSkyPlanetoid();

                if (Main.netMode == NetmodeID.SinglePlayer) {
                    Main.NewText("something went wrong in the Sky", 173, 216, 230);
                }
                else if (Main.netMode == NetmodeID.Server) {
                    ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral("something went wrong in the Sky"), Color.LightBlue);
                }
            }
        }
    }
}
