using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio;
using Terraria.GameContent;

using TheSanity.Buff;
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoProjectile; // buat PinkFlame
using TheSanity.GlobalNPC.Bosses.Pluto.PlutoPart; // buat cek Pluto masih idup apa nggak (safety-kill)

namespace TheSanity.GlobalNPC.Bosses.Pluto.PlutoMinion
{
    // ==========================================================================================
    // PLUTO PROBE (REWRITE V3 - GERAK BEBAS + SHOTGUN)
    // Minion Pluto yang muncul di Pattern 6 (Probe Swarm, lihat ProbeSwarmDash.cs). BISA DIBUNUH
    // (lifeMax 1000, defense 20). Gak orbit muter-muter lagi -- sekarang gerakannya BEBAS lewat
    // siklus 3 fase yang looping terus:
    //
    //   Fase FIRE     -- nembak 3 PinkFlame sekaligus dalam pola sebar simetris ("W"/shotgun 3),
    //                     terus KEDORONG MUNDUR (recoil/knockback) dari efek nembaknya sendiri.
    //   Fase DASH     -- dash cepat ke arah BEBAS (random, dengan sedikit "leash" ngebiasin balik
    //                     kalau kejauhan dari player) -- ninggalin shadow/afterimage di belakangnya.
    //   Fase COOLDOWN -- ngambang santai (dikit-dikit ngedeketin player kalau kejauhan), nunggu
    //                     sebelum nembak lagi.
    //   -> balik ke FIRE, looping terus selama probe-nya masih idup.
    //
    // 🛑 [PENTING] Probe ini SEKARANG GAK ILANG kalau Pluto ganti pattern (persisten). Dia cuma
    // mati kalau: (a) kehabisan HP kena damage player, atau (b) Pluto-nya beneran udah gak ada
    // (mati/despawn). Lihat IsPlutoStillAlive().
    //
    // ai[0] = fase saat ini (0 Fire, 1 Dash, 2 Cooldown)
    // ai[1] = timer fase berjalan
    // ai[2] = whoAmI player yang jadi target probe ini
    // ai[3] = tidak dipakai lagi (dibiarkan 0)
    // ==========================================================================================
    public class PlutoProbe : ModNPC
    {
        public override string Texture => "TheSanity/GlobalNPC/Bosses/Pluto/PlutoPart/PlutoProbe";
        private const string GlowTexturePath = "TheSanity/GlobalNPC/Bosses/Pluto/PlutoPart/PlutoProbeGlow";

        private const int StateFire = 0;
        private const int StateDash = 1;
        private const int StateCooldown = 2;

        // 🛑 [LOKASI BALANCING TEMBAKAN] 3 PinkFlame sekali tembak, sebar simetris kek pola "W".
        private const int FireVolleyCount = 3;
        private const float FireSpreadAngle = 18f; // derajat antar proyektil
        private const int FireStateDuration = 12; // jeda singkat abis nembak sebelum lanjut Dash
        private const float PinkFlameSpeed = 9f;
        private const float PinkFlameScale = 0.45f; // 🛑 [UKURAN DIKECILIN] dari versi Pluto utama

        // 🛑 [LOKASI BALANCING RECOIL] kedorong mundur abis nembak.
        private const float RecoilKnockbackSpeed = 7f;

        // 🛑 [LOKASI BALANCING DASH BEBAS]
        private const int DashDuration = 20;
        private const float DashSpeed = 16f;
        private const float LeashDistance = 900f; // kalau lebih jauh dari ini, dash-nya di-bias balik ke player

        // 🛑 [LOKASI BALANCING COOLDOWN & DRIFT SANTAI]
        private const int CooldownDuration = 70;
        private const float CooldownComfortDistance = 400f; // di bawah jarak ini dia diem aja, gak ngedeketin lagi
        private const float CooldownMaxDriftSpeed = 6f;
        private const float CooldownDriftLerpRate = 0.08f;

        // 🛑 [LOKASI BALANCING FACING] seberapa cepat "muka" probe muter ngadep ke player.
        private const float FacingLerpRate = 0.15f;

        // 🛑 [LOKASI BALANCING SHADOW AFTERIMAGE] cuma nampak pas lagi fase Dash.
        private const int TrailLength = 8;
        private const float TrailMaxOpacity = 0.35f;

        // Non-network -- aman karena cuma soal visual (trail) & arah dash bebas yang dikoreksi
        // otomatis lewat sinkronisasi posisi NPC bawaan Terraria (server tetap otoritatif).
        private Vector2 dashDirection;
        private Vector2[] trailPositions = new Vector2[TrailLength];
        private float[] trailRotations = new float[TrailLength];
        private bool trailInitialized = false;

        public override void SetStaticDefaults() {
            NPCID.Sets.NoMultiplayerSmoothingByType[NPC.type] = true;
        }

        public override void SetDefaults() {
            NPC.width = 30;
            NPC.height = 28;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0.1f;

            // 🛑 [LOKASI BALANCING HP & DEFENSE]
            NPC.lifeMax = 1000;
            NPC.defense = 20;
            NPC.dontTakeDamage = false;

            // 🛑 [LOKASI BALANCING DAMAGE KONTAK] di-override lagi per difficulty di AI() bawah.
            NPC.damage = 15;

            NPC.HitSound = null; // suara kena hit di-handle manual lewat HitEffect() di bawah
            NPC.DeathSound = SoundID.NPCDeath6;
            NPC.aiStyle = -1; // full custom AI
            NPC.npcSlots = 0f; // ga makan slot batas spawn musuh biasa (dia minion boss)
            NPC.value = 0f; // gak ngedrop koin
            NPC.scale = 1f;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo hurtInfo) {
            target.AddBuff(ModContent.BuffType<ElectrictDischarge>(), 2 * 60);

            int randomHit = Main.rand.Next(1, 5);
            SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/PlutoHit{randomHit}"), NPC.Center);
        }

        public override void HitEffect(NPC.HitInfo hit) {
            if (NPC.life > 0) {
                int randomHit = Main.rand.Next(1, 5);
                SoundEngine.PlaySound(new SoundStyle($"TheSanity/GlobalNPC/Bosses/Pluto/PlutoSound/PlutoHit{randomHit}"), NPC.Center);
            }
        }

        public override void OnKill() {
            for (int i = 0; i < 12; i++) {
                Dust d = Dust.NewDustPerfect(NPC.Center, DustID.Electric, Main.rand.NextVector2Circular(4f, 4f));
                d.noGravity = true;
                d.color = Color.HotPink;
                d.scale = Main.rand.NextFloat(1f, 1.6f);
            }
        }

        public override void AI() {
            // =========================================================================
            // OVERRIDE DAMAGE KONTAK BERDASARKAN DIFFICULTY
            // =========================================================================
            if (Main.masterMode) {
                NPC.damage = 10;
            }
            else if (Main.expertMode) {
                NPC.damage = 12;
            }
            else {
                NPC.damage = 15;
            }

            int targetIndex = (int)NPC.ai[2];
            if (targetIndex < 0 || targetIndex >= Main.maxPlayers || !Main.player[targetIndex].active || Main.player[targetIndex].dead) {
                NPC.active = false;
                return;
            }
            Player target = Main.player[targetIndex];

            // =========================================================================
            // 🛑 [SAFETY] Probe ini SEKARANG GAK auto-mati kalau Pluto ganti pattern -- cuma mati
            // kalau Pluto-nya beneran udah gak ada (mati/despawn), biar gak ada yang nyangkut
            // abadi kalau boss-nya sendiri udah gak eksis.
            // =========================================================================
            if (!IsPlutoStillAlive()) {
                NPC.active = false;
                return;
            }

            RunBehaviorState(target);
            UpdateTrail();

            // 🛑 [FACING] Muka probe selalu ngadep ke player.
            UpdateFacing(target);
        }

        // ======================================================================
        // STATE MACHINE: Fire -> Dash -> Cooldown -> Fire -> ... (looping selama probe idup)
        // ======================================================================
        private void RunBehaviorState(Player target) {
            int state = (int)NPC.ai[0];
            int timer = (int)NPC.ai[1];

            switch (state) {
                default:
                case StateFire: {
                    if (timer == 0) {
                        FireShotgunVolley(target);

                        // Recoil -- kedorong mundur (kebalikan arah nembak, yang mana = arah ke player)
                        Vector2 fireDir = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                        NPC.velocity = -fireDir * RecoilKnockbackSpeed;
                    }
                    else {
                        NPC.velocity *= 0.9f; // redam momentum recoil-nya pelan-pelan
                    }

                    timer++;
                    if (timer >= FireStateDuration) { state = StateDash; timer = 0; }
                    break;
                }

                case StateDash: {
                    if (timer == 0) {
                        dashDirection = PickFreeDashDirection(target);
                    }
                    NPC.velocity = dashDirection * DashSpeed;

                    timer++;
                    if (timer >= DashDuration) { state = StateCooldown; timer = 0; }
                    break;
                }

                case StateCooldown: {
                    Vector2 toTarget = target.Center - NPC.Center;
                    float dist = toTarget.Length();
                    Vector2 dir = toTarget.SafeNormalize(Vector2.Zero);

                    float driftSpeed = MathHelper.Clamp((dist - CooldownComfortDistance) * 0.02f, 0f, CooldownMaxDriftSpeed);
                    NPC.velocity = Vector2.Lerp(NPC.velocity, dir * driftSpeed, CooldownDriftLerpRate);

                    timer++;
                    if (timer >= CooldownDuration) { state = StateFire; timer = 0; }
                    break;
                }
            }

            NPC.ai[0] = state;
            NPC.ai[1] = timer;
        }

        // ======================================================================
        // GERAK BEBAS: arah dash full random, TAPI kalau probe kejauhan dari player (lewat
        // LeashDistance), arahnya di-bias balik biar gak makin ngambang jauh ninggalin fight.
        // ======================================================================
        private Vector2 PickFreeDashDirection(Player target) {
            float distToPlayer = Vector2.Distance(NPC.Center, target.Center);

            if (distToPlayer > LeashDistance) {
                Vector2 towardPlayer = (target.Center - NPC.Center).SafeNormalize(Vector2.UnitX);
                float biasAngle = towardPlayer.ToRotation() + MathHelper.ToRadians(Main.rand.NextFloat(-40f, 40f));
                return biasAngle.ToRotationVector2();
            }

            float randomAngle = Main.rand.NextFloat(0f, MathHelper.TwoPi);
            return randomAngle.ToRotationVector2();
        }

        // ======================================================================
        // TEMBAKAN: 3 PinkFlame sekaligus, sebar simetris (kiri/tengah/kanan) -- kesannya kayak
        // shotgun 3 peluru / pola "W" kalau lintasannya digambar bareng-bareng.
        // ======================================================================
        private void FireShotgunVolley(Player target) {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            Vector2 aimDir = (target.Center - NPC.Center).SafeNormalize(-Vector2.UnitY);
            float baseAngle = aimDir.ToRotation();

            for (int i = 0; i < FireVolleyCount; i++) {
                float offsetIndex = i - (FireVolleyCount - 1) / 2f; // -1, 0, 1 buat 3 peluru
                float angle = baseAngle + MathHelper.ToRadians(FireSpreadAngle * offsetIndex);
                Vector2 vel = angle.ToRotationVector2() * PinkFlameSpeed;

                // Damage-nya sendiri di-override di dalam PinkFlame.AI() berdasarkan difficulty --
                // angka di sini cuma placeholder. Suara tembak juga udah otomatis muter sendiri
                // per-proyektil dari dalam PinkFlame.AI(), gak perlu di-trigger manual di sini.
                int index = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<PinkFlame>(), 1, 0f, Main.myPlayer);
                if (index >= 0 && index < Main.maxProjectiles) {
                    // 🛑 [UKURAN PINKFLAME DIKECILIN] biar gak segede versi yang ditembak Pluto sendiri.
                    Main.projectile[index].scale = PinkFlameScale;
                }
            }
        }

        // ======================================================================
        // SHADOW AFTERIMAGE: nyimpen histori posisi & rotasi buat digambar transparan di
        // PreDraw() pas lagi fase Dash.
        // ======================================================================
        private void UpdateTrail() {
            if (!trailInitialized) {
                for (int i = 0; i < trailPositions.Length; i++) {
                    trailPositions[i] = NPC.Center;
                    trailRotations[i] = NPC.rotation;
                }
                trailInitialized = true;
            }

            for (int i = trailPositions.Length - 1; i > 0; i--) {
                trailPositions[i] = trailPositions[i - 1];
                trailRotations[i] = trailRotations[i - 1];
            }
            trailPositions[0] = NPC.Center;
            trailRotations[0] = NPC.rotation;
        }

        // 🛑 [FACING] Muka probe selalu menghadap player -- MURNI lewat rotasi 360 derajat bebas
        // (kayak probe-nya Destroyer), gak pake flip sprite sama sekali. Jadi dari sudut manapun
        // player nge-agro dia, bagian depan sprite-nya otomatis nyesuain sendiri.
        // 🛑 [OFFSET 180°] Sprite dasarnya (rotation = 0) ngadep KIRI di gambar aslinya, bukan
        // kanan -- makanya sudutnya di-tambah MathHelper.Pi biar "muka" yang beneran itu yang
        // ngarah ke player, bukan bagian belakangnya.
        private void UpdateFacing(Player target) {
            float angleToTarget = (target.Center - NPC.Center).ToRotation() + MathHelper.Pi;
            NPC.rotation = NPC.rotation.AngleLerp(angleToTarget, FacingLerpRate);
        }

        // 🛑 [SAFETY CHECK] Nyari NPC PlutoHead yang lagi aktif di dunia -- kalau gak ketemu sama
        // sekali (Pluto udah mati/despawn), probe wajib ikutan mati.
        private bool IsPlutoStillAlive() {
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.active && npc.type == ModContent.NPCType<PlutoHead>()) {
                    return true;
                }
            }
            return false;
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            Texture2D bodyTex = TextureAssets.Npc[NPC.type].Value;
            Vector2 bodyOrigin = bodyTex.Size() * 0.5f;

            // ---------------- SHADOW AFTERIMAGE (SELALU ada, gak cuma pas Dash) ----------------
            for (int i = trailPositions.Length - 1; i >= 1; i--) {
                float trailProgress = (float)i / trailPositions.Length;
                float alpha = TrailMaxOpacity * (1f - trailProgress);
                Vector2 trailDrawPos = trailPositions[i] - screenPos;

                spriteBatch.Draw(bodyTex, trailDrawPos, null, Color.HotPink * alpha, trailRotations[i], bodyOrigin, NPC.scale, SpriteEffects.None, 0f);
            }

            Vector2 drawPos = NPC.Center - screenPos;

            // ---------------- BADAN UTAMA ----------------
            // 🛑 [FIX FACING] Gak pake SpriteEffects flip lagi -- NPC.rotation aja yang muter bebas
            // 360 derajat (lihat UpdateFacing()), jadi otomatis selalu ngadep depan ke player dari
            // sudut manapun, gak ada lagi sisi yang "kebalik" nampilin belakang.
            spriteBatch.Draw(bodyTex, drawPos, null, drawColor, NPC.rotation, bodyOrigin, NPC.scale, SpriteEffects.None, 0f);

            // ---------------- GLOWMASK (selalu fullbright, gak kena gelap malam/goa) ----------------
            Texture2D glowTex = ModContent.Request<Texture2D>(GlowTexturePath).Value;
            if (glowTex != null) {
                spriteBatch.Draw(glowTex, drawPos, null, Color.White, NPC.rotation, bodyOrigin, NPC.scale, SpriteEffects.None, 0f);
            }

            return false;
        }
    }
}
