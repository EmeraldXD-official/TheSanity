using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.GlobalNPC.Bosses.UnknownEntity.Bosbar;

namespace TheSanity.GlobalNPC.Bosses.UnknownEntity
{
    [AutoloadBossHead]
    public partial class UnknownEntity : ModNPC
    {
        // Assets / Tekstur Custom
        private static Asset<Texture2D> headTexture;
        private static Asset<Texture2D> bodyTexture;
        private static Asset<Texture2D> legTexture;
        private static Asset<Texture2D> telegraphTexture;

        // Musik boss custom
        private static int bossMusicSlot = -1;
        private int dashCount = 0;

        // Assets Greyscale dari Luminance
        private static Asset<Texture2D> shineFlareTex;
        private static Asset<Texture2D> bloomCircleTex;
        private static Asset<Texture2D> bloomLineTex;

        // Variabel Animasi & Pergerakan
        private int frameIndex = 0;
        private int attackTimer = 0;
        private bool isAttacking = false;
        private Vector2 dashTargetDir = Vector2.Zero;

        private Vector2 startPos = Vector2.Zero;
        private Vector2 targetPos = Vector2.Zero;
        private Vector2 portalExitPos = Vector2.Zero;

        // Variabel NullZone
        private float nullZoneXA = 0f;
        private float nullZoneXB = 0f;
        private float nullZoneXC = 0f;
        private const float NullZoneSafeRadius = 95f;

        // Arah putaran DeathLaserBlender
        private float deathRaySpinDir = 1f;

        // Control Variabel Phase
        public bool isPhase2 = false;
        public bool pendingPhase2 = false;
        public bool isDesperation = false;

        // --- [VARIABEL BACKGROUND] ---
        private int _spaceBackgroundID = -1;

        // ---- Cutscene & Death Sequence ----
        public static int CutsceneLockedPlayer = -1;
        private bool deathAnimationDone = false;

        private readonly string[] deathMessages = new string[]
        {
            "MY VESSEL FALLS... BUT THE SANITY WILL CONSUME WHAT REMAINS OF YOUR MIND.",
            "WE WERE MERELY HARBINGERS. THE SANITY CLAIMS THE FINAL REALITY.",
            "TRANSMITTING FINAL CORE DATA... YOUR SANITY CANNOT SURVIVE WHAT IS COMING.",
            "YOU CELEBRATE VICTORY, YET YOUR VERY SANITY IS ALREADY FRACTURING..."
        };

        public ref float DeathMsgSlot => ref NPC.ai[3];

        private int CurrentPhaseContactDamage => isDesperation ? 205 : (isPhase2 ? 175 : 150);

        public enum AIState
        {
            Awakening,
            Chase,
            Dash,
            ProjectileRing,
            Teleport,
            WormholeDash,
            PrismMirage,
            PhantomGrid,
            Phase2Transition,
            SerpentPortalDash,
            SummonMinionHorde,
            DeathLaserBlender,
            DimensionalMatrix,
            DimensionalShatter,
            SingularityCollapse,
            VoidCollapse,
            MemoryFracture,
            ShatteredReflection,
            CorrosionSpiral,
            NullZone,
            DeathSequence
        }

        public AIState State
        {
            get => (AIState)NPC.ai[0];
            set => NPC.ai[0] = (float)value;
        }

        public ref float StateTimer => ref NPC.ai[1];
        public ref float SubTimer => ref NPC.ai[2];

        private float EaseInElastic(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            float c4 = (2f * MathHelper.Pi) / 3f;
            return (float)(-Math.Pow(2, 10 * t - 10) * Math.Sin((t * 10f - 10.75f) * c4));
        }

        private float EaseInExpo(float t)
        {
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;
            return (float)Math.Pow(2, 10 * (t - 1f));
        }

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 20;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.TrailCacheLength[Type] = 36;
            NPCID.Sets.TrailingMode[Type] = 3;
            NPCID.Sets.NPCBestiaryDrawModifiers drawModifiers = new NPCID.Sets.NPCBestiaryDrawModifiers() { Velocity = 1f };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, drawModifiers);
        }

        public override void SetDefaults()
        {
            NPC.width = 30;
            NPC.height = 50;
            NPC.boss = true;
            NPC.lifeMax = 350000;
            NPC.damage = 150;
            NPC.defense = 65;
            NPC.knockBackResist = 0f;
            NPC.value = Item.buyPrice(1, 0, 0, 0);
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            Music = MusicLoader.GetMusicSlot(Mod, "Music/UnkownEntitiy");
            NPC.aiStyle = -1;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.alpha = 255;
            NPC.dontTakeDamage = true;
            NPC.BossBar = ModContent.GetInstance<UnknownEntityBossBar>();
        }

        public override bool CheckDead()
        {
            if (!deathAnimationDone)
            {
                NPC.life = 1;
                NPC.dontTakeDamage = true;
                if (State != AIState.DeathSequence)
                {
                    State = AIState.DeathSequence;
                    StateTimer = 0;
                    SubTimer = 0;
                    NPC.velocity = Vector2.Zero;
                    NPC.netUpdate = true;
                }
                return false;
            }
            return true;
        }

        public override void OnKill()
        {
            CutsceneLockedPlayer = -1;
            // Bersihkan Background ruang angkasa saat Boss mati
            if (_spaceBackgroundID != -1 && _spaceBackgroundID < Main.maxProjectiles)
            {
                Projectile proj = Main.projectile[_spaceBackgroundID];
                if (proj != null && proj.active && proj.type == ModContent.ProjectileType<CosmicSpaceBackground>())
                {
                    proj.Kill();
                }
                _spaceBackgroundID = -1;
            }
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            Player target = Main.player[NPC.target];

            if (!target.active || target.dead)
            {
                NPC.velocity.Y -= 0.6f;
                if (NPC.timeLeft > 10) NPC.timeLeft = 10;
                return;
            }

            if (!isPhase2 && NPC.life < (int)(NPC.lifeMax * 0.5f)) pendingPhase2 = true;

            if (isPhase2 && !isDesperation && NPC.life < (int)(NPC.lifeMax * 0.2f))
            {
                isDesperation = true;
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.6f, Volume = 1.4f }, NPC.Center);
                SoundEngine.PlaySound(SoundID.Item103 with { Pitch = -0.5f, Volume = 1f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 12f);
                NPC.netUpdate = true;
            }

            if (State != AIState.Dash && State != AIState.WormholeDash && State != AIState.PrismMirage && State != AIState.PhantomGrid && State != AIState.SerpentPortalDash && State != AIState.DeathLaserBlender && State != AIState.Awakening && State != AIState.DeathSequence && NPC.velocity.X != 0)
            {
                NPC.direction = NPC.velocity.X > 0 ? 1 : -1;
            }
            NPC.spriteDirection = NPC.direction;

            if (attackTimer > 0)
            {
                attackTimer--;
                if (attackTimer <= 0) isAttacking = false;
            }

            // --- Transisi Fase 2 & Pergantian Background ---
            if (pendingPhase2 && State == AIState.Chase && StateTimer <= 1)
            {
                State = AIState.Phase2Transition;
                StateTimer = 0;
                SubTimer = 0;
                pendingPhase2 = false;
                isPhase2 = true;
                NPC.netUpdate = true;
                NPC.damage = 175;

                // Matikan BG Fase 1, Nyalakan BG Fase 2 dengan parameter ai[0]=1
                if (Main.netMode != NetmodeID.MultiplayerClient)
                {
                    if (_spaceBackgroundID != -1 && _spaceBackgroundID < Main.maxProjectiles && Main.projectile[_spaceBackgroundID].active)
                        Main.projectile[_spaceBackgroundID].Kill();

                    _spaceBackgroundID = Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<CosmicSpaceBackground>(), 0, 0f, Main.myPlayer, 1f);
                }
            }

            if (isDesperation && NPC.damage < 205) NPC.damage = 205;

            StateTimer++;

            switch (State)
            {
                case AIState.Awakening:
                    ExecuteAwakening(target); // Dipanggil dari UnknownEntity_Cutscenes.cs
                    break;
                case AIState.Chase:
                    ExecuteChase(target);
                    break;
                case AIState.Dash:
                    ExecuteDash(target);
                    break;
                case AIState.ProjectileRing:
                    ExecuteProjectileRing();
                    break;
                case AIState.Teleport:
                    ExecuteTeleport(target);
                    break;
                case AIState.WormholeDash:
                    ExecuteWormholeDash(target);
                    break;
                case AIState.PrismMirage:
                    ExecutePrismMirage(target);
                    break;
                case AIState.PhantomGrid:
                    ExecutePhantomGrid(target);
                    break;
                case AIState.Phase2Transition:
                    ExecutePhase2Transition(target);
                    break;
                case AIState.SerpentPortalDash:
                    ExecuteSerpentPortalDash(target);
                    break;
                case AIState.SummonMinionHorde:
                    ExecuteSummonMinionHorde(target);
                    break;
                case AIState.DeathLaserBlender:
                    ExecuteDeathLaserBlender(target);
                    break;
                case AIState.DimensionalMatrix:
                    ExecuteDimensionalMatrix(target);
                    break;
                case AIState.DimensionalShatter:
                    ExecuteDimensionalShatter(target);
                    break;
                case AIState.SingularityCollapse:
                    ExecuteSingularityCollapse(target);
                    break;
                case AIState.VoidCollapse:
                    ExecuteVoidCollapse(target);
                    break;
                case AIState.MemoryFracture:
                    ExecuteMemoryFracture(target);
                    break;
                case AIState.ShatteredReflection:
                    ExecuteShatteredReflection(target);
                    break;
                case AIState.CorrosionSpiral:
                    ExecuteCorrosionSpiral(target);
                    break;
                case AIState.NullZone:
                    ExecuteNullZone(target);
                    break;
                case AIState.DeathSequence:
                    ExecuteDeathSequence(target);
                    break;
            }
        }

        public override void FindFrame(int dummyFrameHeight)
        {
            if (isAttacking)
            {
                int attackProgress = 20 - attackTimer;
                frameIndex = 1 + (attackProgress / 5);
                if (frameIndex > 4) frameIndex = 4;
            }
            else if (Math.Abs(NPC.velocity.X) > 2f || Math.Abs(NPC.velocity.Y) > 2f || State == AIState.Dash || State == AIState.WormholeDash || State == AIState.PrismMirage || State == AIState.SerpentPortalDash)
            {
                frameIndex = 5;
            }
            else
            {
                NPC.frameCounter += Math.Abs(NPC.velocity.X) + Math.Abs(NPC.velocity.Y);
                if (NPC.frameCounter >= 6)
                {
                    NPC.frameCounter = 0;
                    frameIndex++;
                    if (frameIndex < 6 || frameIndex > 19) frameIndex = 6;
                }
            }
        }

        public override void ModifyHitByItem(Player player, Item item, ref NPC.HitModifiers modifiers) => modifiers.Knockback *= 0f;
        public override void ModifyHitByProjectile(Projectile projectile, ref NPC.HitModifiers modifiers) => modifiers.Knockback *= 0f;

        public override void Load()
        {
            string basePath = "TheSanity/GlobalNPC/Bosses/UnknownEntity/";
            headTexture = ModContent.Request<Texture2D>(basePath + "Armor_Head_135", AssetRequestMode.ImmediateLoad);
            bodyTexture = ModContent.Request<Texture2D>(basePath + "Armor_96", AssetRequestMode.ImmediateLoad);
            legTexture = ModContent.Request<Texture2D>(basePath + "Armor_Legs_80", AssetRequestMode.ImmediateLoad);
            telegraphTexture = ModContent.Request<Texture2D>(basePath + "Projectile/TelegraphLineTex", AssetRequestMode.ImmediateLoad);

            if (ModContent.HasAsset("Luminance/Assets/GreyscaleTextures/ShineFlare"))
                shineFlareTex = ModContent.Request<Texture2D>("Luminance/Assets/GreyscaleTextures/ShineFlare", AssetRequestMode.ImmediateLoad);
            if (ModContent.HasAsset("Luminance/Assets/GreyscaleTextures/BloomCircleSmall"))
                bloomCircleTex = ModContent.Request<Texture2D>("Luminance/Assets/GreyscaleTextures/BloomCircleSmall", AssetRequestMode.ImmediateLoad);
            if (ModContent.HasAsset("Luminance/Assets/GreyscaleTextures/BloomLine"))
                bloomLineTex = ModContent.Request<Texture2D>("Luminance/Assets/GreyscaleTextures/BloomLine", AssetRequestMode.ImmediateLoad);
        }

        public override void Unload()
        {
            headTexture = null;
            bodyTexture = null;
            legTexture = null;
            telegraphTexture = null;
            shineFlareTex = null;
            bloomCircleTex = null;
            bloomLineTex = null;
            bossMusicSlot = -1;
        }
    }
}