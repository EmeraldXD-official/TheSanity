using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System.Collections.Generic;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using YourModName.Content.Players;

namespace YourModName.Content.NPCs
{
    // ==========================================
    // ALLY RETINAZER — 1:1 ukuran Retinazer asli, FRIENDLY. Pairing OTOMATIS
    // ke target-nya Spaz-ally (lihat cari Spaz di AI): kalau Spaz punya target,
    // Ret ikut lock musuh yang sama & orbit di jarak 25 block (400px). Kalau
    // Spaz gak punya target, Ret ikut idle orbit di sekitar player (radius sama,
    // 25 block).
    //
    // "DIUJUNG-UJUNG" (anti nempel/dempet): OrbitAngle Ret SEKARANG SELALU
    // ngikutin CircleAngle milik Spaz-ally + 180 derajat (lihat
    // TwinsAllySpazmatism.PublicCircleAngle), BUKAN nge-increment sendiri-sendiri
    // kayak versi lama - berlaku di DUA-DUANYA mode (orbit musuh MAUPUN orbit
    // player), jadi Ret SELALU persis di sisi berlawanan lingkaran dari Spaz,
    // gak akan pernah numpuk/nempel jadi 1 titik. Ini juga yang fix bug visual
    // "Chain12" (rantai penghubung, lihat TwinsAllySupport.cs) yang dulu bisa
    // keliatan kayak blok putih lebar solid - itu kejadian kalau jarak dua ally
    // kependekan (gara-gara numpuk), link-link rantainya (lebar tetap ~12.6px)
    // jadi saling overlap.
    //
    // BARRAGE: 3 stage berurutan (5 tembakan -> 10 -> 15), tiap stage diselingi
    // jeda 0,2 detik (12 tick), lalu ulang siklus dari stage 5 lagi selama
    // target masih hidup. Tiap DeathLaser: FRIENDLY, ignore defense/DR, inflict
    // Ichor 15 detik.
    // ==========================================
    public class TwinsAllyRetinazer : ModProjectile
    {
        public override string Texture => "Terraria/Images/NPC_" + NPCID.Retinazer;

        private static Asset<Texture2D> RetinazerGlowTexture;

        private enum RetState { Orbiting, Firing, StageGap }
        private RetState State = RetState.Orbiting;
        private int Timer;
        private int ShotIntervalTimer;
        private int ShotsFiredThisStage;
        private int StageIndex; // 0 -> 5 tembakan, 1 -> 10, 2 -> 15
        private static readonly int[] StageShotCounts = { 5, 10, 15 };

        public readonly List<TwinsAfterimageSnapshot> Trail = new List<TwinsAfterimageSnapshot>();
        private const int TrailMaxLength = 10;
        private int AnimFrameIndex;

        // Terraria.Projectile.frame is just an int (frame index), not a
        // Rectangle like NPC.frame - so we track our own source-rect here
        // and use it for both drawing and the afterimage trail.
        private Rectangle CurrentFrame;

        // ---- Tunable knobs ----
        private const float OrbitRadius = 400f;       // 25 block * 16px (orbit musuh)
        private const float PlayerOrbitRadius = 400f; // 25 block * 16px (idle orbit player)
        private const float OrbitFallbackSpeed = 0.05f; // CUMA dipakai fallback kalau Spaz-ally somehow gak ketemu
        private float OrbitAngle;

        private const int ShotIntervalTicks = 4;   // spam dalam 1 stage, plek ketiplek TwinsLaserBarrage
        private const int StageGapTicks = 12;       // 0.2 detik jeda antar stage
        private const float ShotSpeed = 15f;
        public const int IchorDebuffTicks = 60 * 15; // 15 detik

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2; // di-refresh terus oleh OpticalWrench.HoldItem
            Projectile.netImportant = true;
            Projectile.DamageType = DamageClass.Generic;

            if (RetinazerGlowTexture == null)
            {
                try { RetinazerGlowTexture = ModContent.Request<Texture2D>("Terraria/Images/Eye_Laser", AssetRequestMode.ImmediateLoad); }
                catch { RetinazerGlowTexture = null; }
            }
        }

        public override bool? CanDamage() => false; // damage dari DeathLaser, badan Ret-ally sendiri gak nabrak

        public override void AI()
        {
            Player owner = Main.player[Projectile.owner];
            if (!owner.active || owner.dead)
            {
                Projectile.Kill();
                return;
            }

            UpdateAnimationFrame();

            TwinsAllySpazmatism spazAlly = FindSpazAlly(owner);
            NPC pairedTarget = spazAlly?.PublicTargetForRet;

            // "Diujung-ujung" / anti-nempel (lihat komentar header): OrbitAngle SELALU
            // ngikutin CircleAngle Spaz + 180 derajat, DI DUA-DUANYA mode (musuh & player) -
            // fallback ke increment sendiri CUMA kalau Spaz-ally somehow gak ketemu sama
            // sekali (harusnya gak pernah kejadian, dua-duanya selalu spawn bareng).
            if (spazAlly != null)
                OrbitAngle = spazAlly.PublicCircleAngle + MathHelper.Pi;
            else
                OrbitAngle += OrbitFallbackSpeed;

            if (pairedTarget == null)
            {
                // Gak ada target dari Spaz - idle orbit di sekitar player, reset barrage.
                Vector2 idlePos = owner.Center + OrbitAngle.ToRotationVector2() * PlayerOrbitRadius;
                Projectile.Center = Vector2.Lerp(Projectile.Center, idlePos, 0.1f);
                Projectile.velocity = Vector2.Zero;
                Projectile.rotation = OrbitAngle - MathHelper.PiOver2;

                State = RetState.Orbiting;
                StageIndex = 0;
                ShotsFiredThisStage = 0;
                Timer = 0;
                RecordTrail();
                return;
            }

            Vector2 desiredPos = pairedTarget.Center + OrbitAngle.ToRotationVector2() * OrbitRadius;
            Projectile.Center = Vector2.Lerp(Projectile.Center, desiredPos, 0.09f);
            Projectile.velocity = Vector2.Zero;

            // Lock musuh - rotation selalu ngarah ke target.
            Projectile.rotation = (pairedTarget.Center - Projectile.Center).ToRotation() - MathHelper.PiOver2;

            switch (State)
            {
                case RetState.Orbiting:
                    Timer++;
                    if (Timer >= 20)
                    {
                        Timer = 0;
                        State = RetState.Firing;
                        ShotsFiredThisStage = 0;
                        ShotIntervalTimer = 0;
                    }
                    break;

                case RetState.Firing:
                    ShotIntervalTimer++;
                    if (ShotIntervalTimer >= ShotIntervalTicks)
                    {
                        ShotIntervalTimer = 0;
                        FireDeathLaser(pairedTarget);
                        ShotsFiredThisStage++;
                    }

                    if (ShotsFiredThisStage >= StageShotCounts[StageIndex])
                    {
                        Timer = 0;
                        State = RetState.StageGap;
                    }
                    break;

                case RetState.StageGap:
                    Timer++;
                    if (Timer >= StageGapTicks)
                    {
                        Timer = 0;
                        StageIndex = (StageIndex + 1) % StageShotCounts.Length; // 5 -> 10 -> 15 -> ulang
                        ShotsFiredThisStage = 0;
                        State = RetState.Firing;
                    }
                    break;
            }

            RecordTrail();
        }

        private void UpdateAnimationFrame()
        {
            Texture2D texture = TextureAssets.Npc[NPCID.Retinazer].Value;
            int frameHeight = texture.Height / Main.npcFrameCount[NPCID.Retinazer];

            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 5)
            {
                Projectile.frameCounter = 0;
                AnimFrameIndex = (AnimFrameIndex + 1) % 3;
            }

            CurrentFrame = new Rectangle(0, frameHeight * (3 + AnimFrameIndex), texture.Width, frameHeight); // Phase 2 row
        }

        // Dulu "FindPairedTarget" (cuma balikin NPC target-nya) - SEKARANG balikin instance
        // TwinsAllySpazmatism itu sendiri, soalnya AI() butuh baca PublicCircleAngle-nya
        // juga (buat sinkronisasi "diujung-ujung"), gak cuma PublicTargetForRet lagi.
        private TwinsAllySpazmatism FindSpazAlly(Player owner)
        {
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.ModProjectile is TwinsAllySpazmatism spaz)
                {
                    return spaz;
                }
            }
            return null;
        }

        private void FireDeathLaser(NPC target)
        {
            Vector2 direction = (target.Center - Projectile.Center).SafeNormalize(-Vector2.UnitY);
            Vector2 muzzlePos = Projectile.Center + direction * 30f;

            SoundEngine.PlaySound(SoundID.Item12, Projectile.Center);

            if (Main.myPlayer != Projectile.owner)
                return; // biaya mana & spawn cukup dari client pemilik proyektil

            Player owner = Main.player[Projectile.owner];
            var staffPlayer = owner.GetModPlayer<TwinsStaffPlayer>();
            staffPlayer.ConsumeAttackMana(owner);

            int damage = (int)(Projectile.damage * staffPlayer.CurrentDamageMultiplier(owner));
            int idx = Projectile.NewProjectile(Projectile.GetSource_FromThis(), muzzlePos, direction * ShotSpeed,
                ProjectileID.DeathLaser, damage, 1.5f, Projectile.owner);
            Main.projectile[idx].friendly = true;
            Main.projectile[idx].hostile = false;
            Main.projectile[idx].tileCollide = false;
            Main.projectile[idx].DamageType = DamageClass.Generic;

            TwinsAllyGlobalProjectile.Configure(Main.projectile[idx], BuffID.Ichor, IchorDebuffTicks);
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

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Npc[NPCID.Retinazer].Value;

            // CurrentFrame is kept up to date every AI tick by UpdateAnimationFrame(),
            // since Terraria.Projectile.frame is just an int, not a Rectangle.
            Rectangle frame = CurrentFrame;

            Vector2 origin = new Vector2(frame.Width * 0.5f, frame.Height * 0.5f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            SpriteEffects effects = Projectile.spriteDirection == -1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            TwinsAllyDrawHelpers.DrawAfterimageTrail(Main.spriteBatch, texture, Trail, Projectile.scale, Main.screenPosition, new Color(255, 70, 40));
            TwinsAllyDrawHelpers.DrawRimGlow(Main.spriteBatch, texture, frame, drawPos, Projectile.rotation, origin, Projectile.scale, effects, new Color(255, 70, 40), 0.9f);

            Main.spriteBatch.Draw(texture, drawPos, frame, lightColor, Projectile.rotation, origin, Projectile.scale, effects, 0f);

            // Glowmask mata ("Eye_Laser"), phase 2 khas Retinazer, full-bright.
            if (RetinazerGlowTexture?.Value != null)
            {
                Main.spriteBatch.Draw(RetinazerGlowTexture.Value, drawPos, frame, Color.White, Projectile.rotation, origin, Projectile.scale, effects, 0f);
            }

            return false;
        }
    }
}
