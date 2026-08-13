using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Bestiary;
using Terraria.GameContent.ItemDropRules;
using Terraria.Graphics.CameraModifiers;
using Terraria.Graphics.Effects;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.TvHead.Projectiles;

namespace TheSanity.GlobalNPC.Bosses.TvHead
{
    [AutoloadBossHead]
    public class TvHead : ModNPC
    {
        // ================= STATE MANAGEMENT =================
        public TvHeadState State {
            get => (TvHeadState)(int)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        public ref float AI_Timer => ref NPC.ai[1];
        public ref float AI_Phase2Flag => ref NPC.ai[2];

        public bool IsPhase2 => AI_Phase2Flag >= 1f;
        private int hitFlashTimer = 0;
        public static bool downedTvHead = false;

        // Custom Sound Definitions
        public static readonly SoundStyle GlitchSound = new SoundStyle("TheSanity/GlobalNPC/Bosses/TvHead/Assets/TvHead_Glitch") { Volume = 0.8f };
        public static readonly SoundStyle LaserSound = new SoundStyle("TheSanity/GlobalNPC/Bosses/TvHead/Assets/TvHead_LaserBeam") { Volume = 0.85f };

        // ================= TEXTURE CACHE =================
        private static Asset<Texture2D> headTexture;
        private static Asset<Texture2D> bodyTexture;
        private static Asset<Texture2D> legsTexture;
        private static Asset<Texture2D> armTexture;

        // ===== OFFSET KOMPOSISI =====
        private const float BodyOffsetY = -6f;
        private const float HeadOffsetY = -20f;
        private const float WingOffsetY = -10f;
        private const float LegsWaistRatio = 0.6f;
        private const float LegsWaistTuck = 10f;

        // ===== SAYAP =====
        private const int WingFrameCount = 4;
        private static int wingSlot = -1;
        private static bool wingSlotResolved = false;
        private int wingFrame = 0;
        private int wingFrameCounter = 0;

        // ===== ROTASI TANGAN =====
        private float leftArmRot = 0f;
        private float rightArmRot = 0f;

        public override void Unload() {
            headTexture = null;
            bodyTexture = null;
            legsTexture = null;
            armTexture = null;
        }

        private static void EnsureWingSlot() {
            if (wingSlotResolved) return;
            wingSlotResolved = true;

            for (int i = 1; i < ItemID.Count; i++) {
                Item sample = ContentSamples.ItemsByType[i];
                if (sample != null && sample.wingSlot > 0 && sample.Name == "Fledgling Wings") {
                    wingSlot = sample.wingSlot;
                    break;
                }
            }
        }

        public override void SetStaticDefaults() {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);

            if (!Main.dedServ) {
                headTexture = ModContent.Request<Texture2D>("Terraria/Images/Item_5061");
                bodyTexture = ModContent.Request<Texture2D>("Terraria/Images/Item_5062");
                legsTexture = ModContent.Request<Texture2D>("Terraria/Images/Armor_Legs_227");
                armTexture = ModContent.Request<Texture2D>("Terraria/Images/Armor/Armor_240");
            }
        }

        public override void SetDefaults() {
            NPC.width = 40;
            NPC.height = 56;
            NPC.damage = 18;
            NPC.defense = 15;
            NPC.lifeMax = 2750;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = null;
            NPC.value = Item.buyPrice(0, 1, 50, 0);
            Music = MusicLoader.GetMusicSlot(Mod, "Music/TvHead_Music");
            NPC.knockBackResist = 0.95f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.boss = true;
            NPC.aiStyle = -1;
            NPC.netAlways = true;

            NPC.buffImmune[BuffID.Poisoned] = true;
            NPC.buffImmune[BuffID.OnFire] = true;
            NPC.buffImmune[BuffID.Confused] = true;
            NPC.buffImmune[BuffID.Venom] = true;

            NPC.oldPos = new Vector2[5];
        }

        public override void OnSpawn(IEntitySource source) {
            SoundEngine.PlaySound(GlitchSound, NPC.Center);
            for (int i = 0; i < 30; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, 0f, 0f, 100, default, 1.8f);
            }

            if (!Main.dedServ) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    NPC.Center, Vector2.Zero, 6f, 6f, 8, 100f, "TvHeadSpawn"));
            }
        }

        public override bool CheckDead() {
            if (State != TvHeadState.DeathCutscene) {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                SwitchState(TvHeadState.DeathCutscene);
                return false;
            }
            return true;
        }

        public override void AI() {
            EnsureWingSlot();

            NPC.timeLeft = 3600;

            for (int i = NPC.oldPos.Length - 1; i > 0; i--) {
                NPC.oldPos[i] = NPC.oldPos[i - 1];
            }
            NPC.oldPos[0] = NPC.position;

            if (State == TvHeadState.DeathCutscene) {
                DoDeathCutscene();
                return;
            }

            NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];

            if (NPC.target < 0 || NPC.target == 255 || target.dead || !target.active) {
                NPC.velocity.Y -= 0.2f;
                return;
            }

            if (NPC.life <= (NPC.lifeMax / 2) && AI_Phase2Flag == 0f) {
                AI_Phase2Flag = 1f;
                TriggerPhase2Transition();
                return;
            }

            AI_Timer++;

            NPC.rotation = MathHelper.Clamp(NPC.velocity.X * 0.03f, -0.25f, 0.25f);
            NPC.spriteDirection = target.Center.X > NPC.Center.X ? 1 : -1;

            Lighting.AddLight(NPC.Center, 0.4f, 0.6f, 0.8f);

            if (Main.rand.NextBool(20)) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, 0f, 0f, 150, default, 0.8f);
            }

            if (wingSlot > 0) {
                if (NPC.velocity.LengthSquared() > 0.1f) {
                    wingFrameCounter++;
                    if (wingFrameCounter >= 6) {
                        wingFrameCounter = 0;
                        wingFrame++;
                        if (wingFrame >= WingFrameCount) wingFrame = 1;
                    }
                } else {
                    wingFrame = 0;
                    wingFrameCounter = 0;
                }
            }

            if (hitFlashTimer > 0) hitFlashTimer--;

            UpdateArmAnimations(target);

            if (!Main.dedServ && IsPhase2) {
                try {
                    var filter = Terraria.Graphics.Effects.Filters.Scene["TheSanity:TvHeadCRT"];
                    if (filter != null && !filter.IsActive()) {
                        filter.Activate(NPC.Center);
                    }
                } catch {
                    // Mencegah crash jika shader tidak ada
                }
            }

            switch (State) {
                case TvHeadState.NoSignalGlare:
                    Attack_NoSignalGlare(target);
                    break;
                case TvHeadState.ChannelSurfing:
                    Attack_ChannelSurfing(target);
                    break;
                case TvHeadState.ScreenTearing:
                    Attack_ScreenTearing(target);
                    break;
                case TvHeadState.TVSnow:
                    if (IsPhase2) Attack_TVSnow(target);
                    else SwitchState(TvHeadState.NoSignalGlare);
                    break;
                case TvHeadState.FullBroadcast:
                    if (IsPhase2) Attack_FullBroadcast(target);
                    else SwitchState(TvHeadState.NoSignalGlare);
                    break;
                case TvHeadState.CRTScanline:
                    if (IsPhase2) Attack_CRTScanline(target);
                    else SwitchState(TvHeadState.NoSignalGlare);
                    break;
                default:
                    SwitchState(TvHeadState.NoSignalGlare);
                    break;
            }
        }

        private void SwitchState(TvHeadState newState) {
            State = newState;
            AI_Timer = 0f;
            NPC.netUpdate = true;
        }

        private void UpdateArmAnimations(Player target) {
            float idleSway = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 4f) * 0.15f;

            switch (State) {
                case TvHeadState.NoSignalGlare:
                    Vector2 dirToTarget = Vector2.Normalize(target.Center - NPC.Center);
                    float targetAngle = dirToTarget.ToRotation();
                    rightArmRot = MathHelper.Lerp(rightArmRot, targetAngle, 0.2f);
                    leftArmRot = MathHelper.Lerp(leftArmRot, idleSway + 0.3f, 0.1f);
                    break;
                case TvHeadState.ChannelSurfing:
                    rightArmRot = MathHelper.Lerp(rightArmRot, 0.8f * NPC.spriteDirection, 0.2f);
                    leftArmRot = MathHelper.Lerp(leftArmRot, -0.8f * NPC.spriteDirection, 0.2f);
                    break;
                case TvHeadState.FullBroadcast:
                    rightArmRot = MathHelper.Lerp(rightArmRot, -MathHelper.PiOver2 - 0.4f, 0.15f);
                    leftArmRot = MathHelper.Lerp(leftArmRot, -MathHelper.PiOver2 + 0.4f, 0.15f);
                    break;
                default:
                    rightArmRot = MathHelper.Lerp(rightArmRot, idleSway + 0.2f, 0.1f);
                    leftArmRot = MathHelper.Lerp(leftArmRot, -idleSway - 0.2f, 0.1f);
                    break;
            }
        }

        private static readonly TvHeadState[] Phase1Pool = { TvHeadState.NoSignalGlare, TvHeadState.ChannelSurfing, TvHeadState.ScreenTearing };
        private static readonly TvHeadState[] Phase2Pool = { TvHeadState.NoSignalGlare, TvHeadState.ChannelSurfing, TvHeadState.ScreenTearing, TvHeadState.TVSnow, TvHeadState.FullBroadcast, TvHeadState.CRTScanline };

        private void GoToNextAttack() {
            TvHeadState[] pool = IsPhase2 ? Phase2Pool : Phase1Pool;
            TvHeadState next;
            int guard = 0;
            do {
                next = pool[Main.rand.Next(pool.Length)];
                guard++;
            } while (next == State && guard < 10);

            SwitchState(next);
        }

        private void TriggerPhase2Transition() {
            NPC.velocity = Vector2.Zero;
            NPC.TargetClosest(true);
            NPC.netUpdate = true;

            SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
            for (int i = 0; i < 50; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, 0f, 0f, 0, default, 2.5f);
            }

            if (Main.netMode != NetmodeID.MultiplayerClient) {
                CombatText.NewText(NPC.Hitbox, Color.Red, "SIGNAL LOST", true);
            }

            if (!Main.dedServ) {
                Main.instance.CameraModifiers.Add(new PunchCameraModifier(
                    NPC.Center, Vector2.Zero, 12f, 8f, 15, 100f, "TvHeadPhase2"));
            }

            State = TvHeadState.NoSignalGlare;
            AI_Timer = 0f;
        }

        private void Attack_NoSignalGlare(Player target) {
            Vector2 targetPos = target.Center + new Vector2(0, -180);
            Vector2 move = targetPos - NPC.Center;
            NPC.velocity = move * 0.05f;

            int chargeDuration = IsPhase2 ? 50 : 70;

            if (AI_Timer == 1 && Main.netMode != NetmodeID.MultiplayerClient) {
                Projectile.NewProjectile(
                    NPC.GetSource_FromAI(), 
                    NPC.Center, 
                    Vector2.Zero, 
                    ModContent.ProjectileType<TvHeadTelegraphLine>(), 
                    0, 
                    0, 
                    Main.myPlayer, 
                    NPC.whoAmI, 
                    chargeDuration
                );
            }

            if (AI_Timer < chargeDuration && AI_Timer % 3 == 0) {
                Vector2 dustPos = NPC.Center + Main.rand.NextVector2CircularEdge(60f, 60f);
                Vector2 dustVel = Vector2.Normalize(NPC.Center - dustPos) * 4f;
                Dust d = Dust.NewDustPerfect(dustPos, DustID.Electric, dustVel, 100, default, 1.2f);
                d.noGravity = true;
            }

            if (AI_Timer >= chargeDuration) {
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 dir = target.Center - NPC.Center;
                    if (dir == Vector2.Zero) dir = -Vector2.UnitY;
                    Vector2 shootVelocity = Vector2.Normalize(dir) * 9f;

                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, shootVelocity, ModContent.ProjectileType<CustomTvLaser>(), 12, 1f, Main.myPlayer);
                }

                SoundEngine.PlaySound(LaserSound, NPC.Center);
                GoToNextAttack();
            }
        }

        private void Attack_ChannelSurfing(Player target) {
            if (AI_Timer == 30) {
                for (int i = 0; i < 20; i++) {
                    Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, 0, 0, 100, default, 1.5f);
                }

                float offsetX = Main.rand.Next(-200, 200);
                float offsetY = Main.rand.Next(-120, -40);
                NPC.Center = target.Center + new Vector2(offsetX, offsetY);
                NPC.netUpdate = true;

                SoundEngine.PlaySound(GlitchSound, NPC.Center);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Vector2 dir = target.Center - NPC.Center;
                    if (dir == Vector2.Zero) dir = -Vector2.UnitY;
                    Vector2 baseVel = Vector2.Normalize(dir) * 7.5f;
                    int spreadCount = IsPhase2 ? 5 : 3;

                    for (int i = 0; i < spreadCount; i++) {
                        float rotation = MathHelper.ToRadians(15 * (i - (spreadCount - 1) / 2f));
                        Vector2 perturbedSpeed = baseVel.RotatedBy(rotation);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, perturbedSpeed, ProjectileID.Nail, 10, 1f, Main.myPlayer);
                    }
                }
            }

            if (AI_Timer >= 80) GoToNextAttack();
        }

        private void Attack_ScreenTearing(Player target) {
            if (AI_Timer == 1) {
                float side = Main.rand.NextBool() ? -1f : 1f;
                NPC.Center = target.Center + new Vector2(side * 350f, -40f);
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    Projectile.NewProjectile(
                        NPC.GetSource_FromAI(), 
                        NPC.Center, 
                        Vector2.Zero, 
                        ModContent.ProjectileType<TvHeadTelegraphLine>(), 
                        0, 
                        0, 
                        Main.myPlayer, 
                        NPC.whoAmI, 
                        20
                    );
                }
            }

            if (AI_Timer < 20) {
                NPC.velocity *= 0.9f;
            } else if (AI_Timer == 20) {
                float dir = target.Center.X > NPC.Center.X ? 1f : -1f;
                NPC.velocity = new Vector2(dir * 16f, 0f);
                SoundEngine.PlaySound(SoundID.Item9, NPC.Center);
            } else if (AI_Timer > 20 && AI_Timer < 55) {
                NPC.velocity.Y = MathHelper.Lerp(NPC.velocity.Y, (target.Center.Y - NPC.Center.Y) * 0.05f, 0.1f);
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, 0, 0, 100, default, 1.3f);
            }

            if (AI_Timer >= 75) GoToNextAttack();
        }

        private void Attack_TVSnow(Player target) {
            Vector2 targetPos = target.Center + new Vector2(0, -220f);
            NPC.velocity = (targetPos - NPC.Center) * 0.04f;

            if (AI_Timer % 12 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                float spawnX = target.Center.X + Main.rand.Next(-350, 350);
                Vector2 spawnPos = new Vector2(spawnX, target.Center.Y - 350);
                Vector2 fallVel = new Vector2(0, 5.5f);

                Projectile.NewProjectile(NPC.GetSource_FromAI(), spawnPos, fallVel, ModContent.ProjectileType<StaticSnowProj>(), 8, 0f, Main.myPlayer);
            }

            if (AI_Timer >= 120) GoToNextAttack();
        }

        private void Attack_CRTScanline(Player target) {
            if (AI_Timer == 1) {
                NPC.Center = new Vector2(target.Center.X, target.Center.Y - 300f);
                NPC.velocity = Vector2.Zero;
                NPC.netUpdate = true;
                SoundEngine.PlaySound(GlitchSound, NPC.Center);
            }

            if (AI_Timer >= 15 && AI_Timer < 95) {
                NPC.velocity.Y = 3.5f;
                NPC.velocity.X = MathHelper.Lerp(NPC.velocity.X, (target.Center.X - NPC.Center.X) * 0.03f, 0.1f);

                if (AI_Timer % 10 == 0 && Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = -3; i <= 3; i++) {
                        Vector2 vel = new Vector2(i * 3f, 1f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ProjectileID.Nail, 9, 0.5f, Main.myPlayer);
                    }
                }
            }

            if (AI_Timer >= 110) GoToNextAttack();
        }

        private void Attack_FullBroadcast(Player target) {
            Vector2 targetPos = target.Center + new Vector2(0, -180f);
            NPC.velocity = (targetPos - NPC.Center) * 0.05f;

            if (AI_Timer == 1) {
                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
            }

            if (AI_Timer == 60) {
                SoundEngine.PlaySound(LaserSound, NPC.Center);

                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    const int count = 16;
                    for (int i = 0; i < count; i++) {
                        float angle = MathHelper.TwoPi / count * i;
                        Vector2 vel = angle.ToRotationVector2() * 7.5f;
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, vel, ModContent.ProjectileType<CustomTvLaser>(), 14, 1f, Main.myPlayer);
                    }
                }
            }

            if (AI_Timer >= 90) GoToNextAttack();
        }

        private void DoDeathCutscene() {
            NPC.velocity *= 0.85f;
            NPC.dontTakeDamage = true;
            NPC.position += Main.rand.NextVector2Circular(3f, 3f);

            rightArmRot = Main.rand.NextFloat(-2f, 2f);
            leftArmRot = Main.rand.NextFloat(-2f, 2f);

            AI_Timer++;

            if (AI_Timer % 12 == 0) {
                SoundEngine.PlaySound(GlitchSound, NPC.Center);
            }

            if (AI_Timer == 120) {
                SoundEngine.PlaySound(SoundID.Item14, NPC.Center);
                if (Main.netMode != NetmodeID.MultiplayerClient) {
                    for (int i = 0; i < 8; i++) {
                        Vector2 goreVel = Main.rand.NextVector2Circular(8f, 8f);
                        Gore.NewGore(NPC.GetSource_Death(), NPC.Center, goreVel, Main.rand.Next(GoreID.Smoke1, GoreID.Smoke3));
                    }
                }
            }

            if (AI_Timer >= 150) {
                try {
                    if (!Main.dedServ && Terraria.Graphics.Effects.Filters.Scene["TheSanity:TvHeadCRT"].IsActive()) {
                        Terraria.Graphics.Effects.Filters.Scene.Deactivate("TheSanity:TvHeadCRT");
                    }
                } catch {}

                downedTvHead = true;
                NPC.life = 0;
                NPC.HitEffect(new NPC.HitInfo());
                NPC.checkDead();
            }
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor) {
            if (headTexture == null || bodyTexture == null || legsTexture == null) return false;

            EnsureWingSlot();

            float bob = (float)Math.Sin(Main.GlobalTimeWrappedHourly * 2.2f) * 4f;
            Vector2 drawPos = NPC.Center - screenPos + new Vector2(0, bob);
            SpriteEffects effects = NPC.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipHorizontally;

            Color lightColor = NPC.GetAlpha(drawColor);
            if (lightColor.R < 100 && lightColor.G < 100 && lightColor.B < 100) {
                lightColor = new Color(150, 150, 150, 255);
            }

            Texture2D headTex = headTexture.Value;
            Texture2D bodyTex = bodyTexture.Value;
            Texture2D legsTex = legsTexture.Value;

            Vector2 headOrigin = headTex.Size() / 2f;
            Vector2 bodyOrigin = bodyTex.Size() / 2f;

            for (int i = 0; i < NPC.oldPos.Length; i++) {
                float trailAlpha = (1f - (i / (float)NPC.oldPos.Length)) * 0.35f;
                Vector2 trailPos = NPC.oldPos[i] + NPC.Size / 2f - screenPos + new Vector2(0, bob);
                Color trailColor = new Color(120, 200, 255) * trailAlpha;
                spriteBatch.Draw(headTex, trailPos, null, trailColor, NPC.rotation, headOrigin, NPC.scale * 0.95f, effects, 0f);
            }

            if (wingSlot > 0) {
                Texture2D wingTex = TextureAssets.Wings[wingSlot].Value;
                Rectangle wingRect = wingTex.Frame(1, WingFrameCount, 0, wingFrame);
                Vector2 wingOrigin = new Vector2(wingRect.Width / 2f, wingRect.Height * 0.42f);
                Vector2 wingPos = drawPos + new Vector2(0f, BodyOffsetY + WingOffsetY);
                spriteBatch.Draw(wingTex, wingPos, wingRect, lightColor, NPC.rotation, wingOrigin, NPC.scale, effects, 0f);
            }

            Rectangle legsFrame = legsTex.Frame(1, 20, 0, 0);
            float jacketBottom = drawPos.Y + BodyOffsetY + (bodyTex.Height / 2f);
            Vector2 legsOrigin = new Vector2(legsFrame.Width / 2f, legsFrame.Height * LegsWaistRatio);
            Vector2 legsPos = new Vector2(drawPos.X, jacketBottom - LegsWaistTuck);
            spriteBatch.Draw(legsTex, legsPos, legsFrame, lightColor, NPC.rotation, legsOrigin, NPC.scale, effects, 0f);

            spriteBatch.Draw(bodyTex, drawPos + new Vector2(0, BodyOffsetY), null, lightColor, NPC.rotation, bodyOrigin, NPC.scale, effects, 0f);

            if (armTexture != null) {
                Texture2D armTex = armTexture.Value;
                Rectangle sleeveFrame = new Rectangle(20, 0, 12, 18);
                Vector2 armOrigin = new Vector2(6f, 4f);

                Vector2 leftShoulder = drawPos + new Vector2(-12f * NPC.spriteDirection, BodyOffsetY - 2f);
                Vector2 rightShoulder = drawPos + new Vector2(12f * NPC.spriteDirection, BodyOffsetY - 2f);

                spriteBatch.Draw(armTex, leftShoulder, sleeveFrame, lightColor, leftArmRot, armOrigin, NPC.scale, effects, 0f);
                spriteBatch.Draw(armTex, rightShoulder, sleeveFrame, lightColor, rightArmRot, armOrigin, NPC.scale, effects, 0f);
            }

            spriteBatch.Draw(headTex, drawPos + new Vector2(0, HeadOffsetY), null, lightColor, NPC.rotation, headOrigin, NPC.scale, effects, 0f);

            return false;
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) {
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.BossDrop.BrokenTv>(), 1, 5, 12));
            npcLoot.Add(ItemDropRule.Common(ItemID.IronBar, 1, 5, 10));
            npcLoot.Add(ItemDropRule.Common(ModContent.ItemType<Items.Summon.GlitchControler>(), 5, 1, 1));
        }

        public override void HitEffect(NPC.HitInfo hit) {
            hitFlashTimer = 8;
            for (int i = 0; i < 4; i++) {
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Electric, hit.HitDirection, -1f, 100, default, 0.8f);
            }
        }

        public override void SetBestiary(BestiaryDatabase database, BestiaryEntry bestiaryEntry) {
            bestiaryEntry.Info.AddRange(new IBestiaryInfoElement[] {
                new FlavorTextBestiaryInfoElement("An old television possessed by a malevolent signal, broadcasting lethal static interference.")
            });
        }
    }
}