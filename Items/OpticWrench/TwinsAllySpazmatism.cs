using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // ALLY SPAZMATISM — versi 1:1 ukuran Spazmatism asli, FRIENDLY, dipanggil
    // selama player pegang OpticalWrench (lihat OpticalWrench.HoldItem).
    //
    // STATE MACHINE:
    //   Circling      -> muter ngelilingin target, "nyari timing" selama 1 detik
    //                     (TimingWindowTicks), abis itu masuk Dashing.
    //   Dashing       -> spam dash 10-15x (DashCount), tiap dash teleport ke
    //                     titik random di sekeliling target lalu nge-charge
    //                     LEWAT target (MURNI visual/positioning - Spaz-ally
    //                     GAK nembak proyektil apa pun, damage 100% dari
    //                     Retinazer/DeathLaser), abis semua dash kelar balik
    //                     ke Circling (timing baru).
    //   CirclingPlayer-> dipakai kalau target mati & TIDAK ada musuh lain di
    //                     sekitar - muter ngelilingin PLAYER sampai ada musuh
    //                     baru kedeteksi.
    //
    // RETARGET SEAMLESS: kalau target mati SAAT masih ada musuh lain "di
    // sekitar" (NearbyEnemyRange), langsung ganti target TANPA reset Timer -
    // "melanjutkan timing yang tersisa" sesuai request.
    // ==========================================
    public class TwinsAllySpazmatism : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Spazmatism;

        private enum SpazState { Circling, Dashing, CirclingPlayer }

        // ---- State (TIDAK di-network manual - sama seperti caveat di
        // TwinsReworkOverride, cukup buat singleplayer/testing untuk saat ini) ----
        private SpazState State = SpazState.Circling;
        private int Timer;
        private int TargetWhoAmI = -1;
        private int DashesRemaining;
        private int DashSubTimer;
        private bool DashCharging; // fase telegraph singkat sebelum charge lewat target
        private Vector2 DashStartPos;
        private Vector2 DashThroughPos;
        private float CircleAngle;

        public readonly List<TwinsAfterimageSnapshot> Trail = new List<TwinsAfterimageSnapshot>();
        private const int TrailMaxLength = 10;
        private int AnimFrameIndex; // 0,1,2 -> map ke row (3 + index), plek ketiplek TwinsRework.FindFrame

        // Terraria.Projectile.frame is just an int (frame index), not a
        // Rectangle like NPC.frame - so we track our own source-rect here
        // and use it for both drawing and the afterimage trail.
        private Rectangle CurrentFrame;

        // ---- Tunable knobs ----
        private const int TimingWindowTicks = 60;      // "1 detik" nyari timing
        private const float CirclingRadius = 400f;      // 25 block * 16px
        private const float PlayerCirclingRadius = 400f; // 25 block * 16px
        private const float CircleSpeed = 0.045f;

        private const int DashTelegraphTicks = 10;
        private const int DashChargeTicks = 22;
        private const int DashGapTicks = 8;
        private const float DashChargeSpeed = 30f;
        private const float DashSpawnDistance = 340f; // seberapa jauh titik random di sekeliling target

        public NPC PublicTargetForRet => (TargetWhoAmI != -1 && Main.npc[TargetWhoAmI].active) ? Main.npc[TargetWhoAmI] : null;
        public bool IsChasingOrDashing => State == SpazState.Dashing || (State == SpazState.Circling && TargetWhoAmI != -1);

        // BARU: dibaca Retinazer (TwinsAllyRetinazer.cs) buat nyamain sudut orbit-nya
        // SELALU 180 derajat berlawanan dari Spaz ("diujung-ujung", anti nempel/dempet -
        // sekaligus fix bug "Chain12" keliatan kayak blok putih lebar, soalnya itu gara-gara
        // dua ally kependekan jaraknya jadi link rantai numpuk).
        public float PublicCircleAngle => CircleAngle;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 26; // approx bounding box vanilla Spazmatism, ukuran render tetap 1:1 lewat draw manual
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2; // di-refresh terus tiap tick oleh OpticalWrench.HoldItem
            Projectile.netImportant = true;
            Projectile.DamageType = DamageClass.Generic;
        }

        public override bool? CanDamage() => false; // Spaz-ally MURNI visual/positioning sekarang, gak ada damage sumber apa pun dari dia (semua damage dari Retinazer)

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            UpdateAnimationFrame();

            // Validasi target lama
            bool targetValid = TargetWhoAmI != -1 && Main.npc[TargetWhoAmI].active && Main.npc[TargetWhoAmI].CanBeChasedBy();

            switch (State)
            {
                case SpazState.Circling:
                    TickCircling(owner, targetValid);
                    break;
                case SpazState.Dashing:
                    TickDashing(owner, targetValid);
                    break;
                case SpazState.CirclingPlayer:
                    TickCirclingPlayer(owner);
                    break;
            }

            RecordTrail();
        }

        private void TickCircling(Player owner, bool targetValid)
        {
            if (!targetValid)
            {
                NPC replacement = TwinsAllyTargeting.FindTarget(owner, Projectile.Center, TwinsAllyTargeting.NearbyEnemyRange);
                if (replacement != null)
                {
                    // Retarget SEAMLESS - Timer TIDAK direset, "melanjutkan timing yang tersisa".
                    TargetWhoAmI = replacement.whoAmI;
                    targetValid = true;
                }
                else
                {
                    State = SpazState.CirclingPlayer;
                    Timer = 0;
                    return;
                }
            }

            NPC target = Main.npc[TargetWhoAmI];
            CircleAngle += CircleSpeed;
            Vector2 desiredPos = target.Center + CircleAngle.ToRotationVector2() * CirclingRadius;
            Projectile.Center = Vector2.Lerp(Projectile.Center, desiredPos, 0.08f);
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = (target.Center - Projectile.Center).ToRotation() - MathHelper.PiOver2;

            Timer++;
            if (Timer >= TimingWindowTicks)
            {
                DashesRemaining = Main.rand.Next(10, 16); // 10-15x
                Timer = 0;
                State = SpazState.Dashing;
                BeginNextDash(target);
            }
        }

        private void TickDashing(Player owner, bool targetValid)
        {
            if (!targetValid)
            {
                NPC replacement = TwinsAllyTargeting.FindTarget(owner, Projectile.Center, TwinsAllyTargeting.NearbyEnemyRange);
                if (replacement != null)
                {
                    // Target mati di tengah sesi dash - lanjut ke musuh baru, sisa
                    // DashesRemaining TETAP dipakai (melanjutkan, bukan reset).
                    TargetWhoAmI = replacement.whoAmI;
                }
                else
                {
                    State = SpazState.CirclingPlayer;
                    Timer = 0;
                    Projectile.velocity = Vector2.Zero;
                    return;
                }
            }

            NPC target = Main.npc[TargetWhoAmI];
            DashSubTimer++;

            if (DashCharging)
            {
                // Telegraph singkat, diem di titik spawn random, ngarah ke target.
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation = (target.Center - Projectile.Center).ToRotation() - MathHelper.PiOver2;

                if (DashSubTimer >= DashTelegraphTicks)
                {
                    DashCharging = false;
                    DashSubTimer = 0;
                    Vector2 dir = (DashThroughPos - Projectile.Center).SafeNormalize(-Vector2.UnitY);
                    Projectile.velocity = dir * DashChargeSpeed;
                }
            }
            else
            {
                // BARU: Spaz-ally UDAH GAK NEMBAK PROYEKTIL APA PUN LAGI (request) - dash
                // lewat target murni buat visual/positioning, damage sepenuhnya dari
                // Retinazer (DeathLaser barrage). Blok FireEyeFire yang lama DIHAPUS di sini.
                Projectile.rotation = Projectile.velocity.ToRotation() - MathHelper.PiOver2;

                if (DashSubTimer >= DashChargeTicks)
                {
                    DashesRemaining--;
                    DashSubTimer = 0;

                    if (DashesRemaining <= 0)
                    {
                        Projectile.velocity *= 0.3f;
                        State = SpazState.Circling;
                        Timer = 0;
                        CircleAngle = Projectile.velocity.ToRotation();
                    }
                    else
                    {
                        Projectile.velocity *= 0.4f;
                        DashCharging = true; // jeda singkat sebelum charge berikutnya (DashGapTicks via telegraph)
                        BeginNextDash(target);
                    }
                }
            }
        }

        private void BeginNextDash(NPC target)
        {
            // Titik acak di sekeliling target ("dash dari Direction random"), lalu
            // charge-nya LEWAT titik berlawanan di sisi lain target.
            float angle = Main.rand.NextFloat(MathHelper.TwoPi);
            DashStartPos = target.Center + angle.ToRotationVector2() * DashSpawnDistance;
            DashThroughPos = target.Center - angle.ToRotationVector2() * DashSpawnDistance;

            Projectile.Center = DashStartPos;
            DashCharging = true;
            DashSubTimer = 0;
        }

        private void TickCirclingPlayer(Player owner)
        {
            CircleAngle += CircleSpeed * 1.4f;
            Vector2 desiredPos = owner.Center + CircleAngle.ToRotationVector2() * PlayerCirclingRadius;
            Projectile.Center = Vector2.Lerp(Projectile.Center, desiredPos, 0.1f);
            Projectile.velocity = Vector2.Zero;
            Projectile.rotation = CircleAngle - MathHelper.PiOver2;

            Timer++;
            if (Timer >= 20) // rescan tiap ~0.33s, gak perlu tiap tick
            {
                Timer = 0;
                NPC found = TwinsAllyTargeting.FindTarget(owner, Projectile.Center, TwinsAllyTargeting.NearbyEnemyRange);
                if (found != null)
                {
                    TargetWhoAmI = found.whoAmI;
                    State = SpazState.Circling;
                    Timer = 0;
                }
            }
        }

        private void RecordTrail()
        {
            Trail.Add(new TwinsAfterimageSnapshot
            {
                Center = Projectile.Center,
                Rotation = Projectile.rotation,
                Frame = CurrentFrame,
                SpriteDirection = Projectile.spriteDirection
            });
            if (Trail.Count > TrailMaxLength)
                Trail.RemoveAt(0);
        }

        // ==========================================
        // FRAME — samain persis ke frame "Phase 2" vanilla Spazmatism (row 3,
        // animasi 3/4/5), plek ketiplek TwinsRework.FindFrame.
        //
        // Terraria.Projectile.frame is just an int (frame index), not a
        // Rectangle like NPC.frame, so we build/track our own source-rect
        // (CurrentFrame) instead of assigning into Projectile.frame directly.
        // ==========================================
        private void UpdateAnimationFrame()
        {
            Texture2D texture = TextureAssets.Npc[NPCID.Spazmatism].Value;
            int frameHeight = texture.Height / Main.npcFrameCount[NPCID.Spazmatism];

            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5)
            {
                Projectile.frameCounter = 0;
                AnimFrameIndex = (AnimFrameIndex + 1) % 3;
            }

            CurrentFrame = new Rectangle(0, frameHeight * (3 + AnimFrameIndex), texture.Width, frameHeight); // row 3.. = frame "Phase 2"
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Npc[NPCID.Spazmatism].Value;
            Rectangle frame = CurrentFrame;

            Vector2 origin = new Vector2(frame.Width * 0.5f, frame.Height * 0.5f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            TwinsAllyDrawHelpers.DrawAfterimageTrail(Main.spriteBatch, texture, Trail, Projectile.scale, Main.screenPosition, new Color(90, 255, 100));
            TwinsAllyDrawHelpers.DrawRimGlow(Main.spriteBatch, texture, frame, drawPos, Projectile.rotation, origin, Projectile.scale, effects, new Color(90, 255, 100), 0.9f);

            Main.spriteBatch.Draw(texture, drawPos, frame, lightColor, Projectile.rotation, origin, Projectile.scale, effects, 0f);

            return false;
        }
    }
}
