using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class PlanteraGraspProjectile : ModProjectile
    {
        private bool Returning
        {
            get => Projectile.localAI[0] == 1f;
            set => Projectile.localAI[0] = value ? 1f : 0f;
        }

        public bool IsSecondForm
        {
            get => Projectile.localAI[1] == 1f;
            private set => Projectile.localAI[1] = value ? 1f : 0f;
        }

        // Timer 20 Detik Stage 2 (1200 ticks)
        private float Stage2Timer
        {
            get => Projectile.localAI[2];
            set => Projectile.localAI[2] = value;
        }

        private bool CanEnterStage2 = true;

        // Status Dash Yoyo
        public bool IsDashing { get; private set; } = false;

        private const float BaseMaxRange = 410f; 
        private const float ReturnSpeed = 16f; 
        private const int TentacleCount = 4;

        private static readonly Vector2 HandChainOffset = new Vector2(0f, 0f);
        private static readonly Vector2 HeadChainOffsetFirstForm = new Vector2(0f, -6f);
        private static readonly Vector2 HeadChainOffsetSecondForm = new Vector2(0f, -2f);

        private const int TrailLength = 5;

        // Variabel Pola Tembakan Form 1
        private int shootTimer = 0;
        private int shootPhase = 0; // 0 = 10x Slow, 1 = 15x Fast, 2 = 20x VeryFast, 3 = ThornBall, 4 = Cooldown
        private int seedsShot = 0;

        // Variabel AI Dash Stage 2
        private int dashState = 0; // 0 = Incar, 1 = Dash, 2 = Recovery
        private int dashTimer = 0;
        private Vector2 dashTargetPos = Vector2.Zero;
        private int targetNPCIndex = -1;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.YoyosLifeTimeMultiplier[Projectile.type] = 3f;
            ProjectileID.Sets.YoyosMaximumRange[Projectile.type] = 410f;
            ProjectileID.Sets.YoyosTopSpeed[Projectile.type] = 14f;

            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = TrailLength;
        }

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.scale = 1.25f;

            Projectile.aiStyle = ProjAIStyleID.Yoyo; 
            
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.MeleeNoSpeed;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * 60;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;

            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 12; 

            Projectile.rotation = 0f;
        }

        public override bool PreAI()
        {
            Player player = Main.player[Projectile.owner];

            if (player.dead || !player.active)
            {
                Projectile.Kill();
                return false;
            }

            player.heldProj = Projectile.whoAmI;
            player.itemTime = 2;
            player.itemAnimation = 2;

            Vector2 handPos = player.RotatedRelativePoint(player.MountedCenter, true);

            if (!player.channel || player.dead)
            {
                Returning = true;
            }

            // --- SISTEM TIMER 20 DETIK STAGE 2 ---
            if (IsSecondForm)
            {
                Stage2Timer--;
                if (Stage2Timer <= 0)
                {
                    IsSecondForm = false;
                    Stage2Timer = 0;
                    IsDashing = false;
                    CanEnterStage2 = false; 
                    DespawnMyTentacles();
                    ResetShootingPattern();
                }
            }

            float effectiveMaxRange = BaseMaxRange + (player.yoyoString ? 80f : 0f);

            if (!Returning)
            {
                if (IsSecondForm)
                {
                    UpdateStage2DashAI();
                }
                else
                {
                    IsDashing = false;
                    Vector2 toMouse = Main.MouseWorld - handPos;
                    if (toMouse.Length() > effectiveMaxRange)
                    {
                        toMouse.Normalize();
                        toMouse *= effectiveMaxRange;
                    }

                    Vector2 targetPos = handPos + toMouse;
                    Vector2 dirToTarget = targetPos - Projectile.Center;
                    float distToTarget = dirToTarget.Length();
                    float topSpeed = 14f + (player.yoyoString ? 2f : 0f);

                    if (distToTarget < 16f)
                    {
                        Projectile.velocity *= 0.84f;
                    }
                    else
                    {
                        dirToTarget.Normalize();
                        Vector2 desiredVelocity = dirToTarget * topSpeed;
                        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.12f);
                    }

                    Projectile.position += Projectile.velocity;

                    NPC target = FindNearestNPC(Projectile.Center, 500f);
                    Projectile.rotation = (target != null ? target.Center - Projectile.Center : Main.MouseWorld - Projectile.Center).ToRotation() + MathHelper.PiOver2;

                    if (Main.myPlayer == Projectile.owner)
                    {
                        UpdateForm1Shooting();
                    }
                }
            }
            else
            {
                IsDashing = false;
                Vector2 dir = handPos - Projectile.Center;
                float dist = dir.Length();
                if (dist <= ReturnSpeed + 4f)
                {
                    Projectile.Kill();
                    return false;
                }
                dir.Normalize();
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * ReturnSpeed, 0.2f);
                Projectile.position += Projectile.velocity;
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            }

            SpawnTentaclesIfNeeded();

            return false; 
        }

        private void UpdateStage2DashAI()
        {
            NPC cursorTarget = FindNPCNearCursor(320f); // Radius 20 Block dari Cursor

            if (cursorTarget != null)
            {
                IsDashing = true;

                if (dashState == 0) // Lock-on / Ancang-ancang
                {
                    targetNPCIndex = cursorTarget.whoAmI;
                    dashTimer++;

                    Projectile.rotation = (cursorTarget.Center - Projectile.Center).ToRotation() + MathHelper.PiOver2;
                    Projectile.velocity *= 0.85f;

                    if (dashTimer >= 8) 
                    {
                        dashState = 1;
                        dashTimer = 0;

                        Vector2 dashDir = cursorTarget.Center - Projectile.Center;
                        if (dashDir == Vector2.Zero) dashDir = new Vector2(0, -1);
                        dashDir.Normalize();

                        dashTargetPos = cursorTarget.Center + (dashDir * 160f); // Tembus 10 block ke belakang musuh
                    }
                }
                else if (dashState == 1) // Meluncur Dash
                {
                    Vector2 dirToDash = dashTargetPos - Projectile.Center;
                    float dist = dirToDash.Length();

                    if (dist < 24f || dashTimer > 25) 
                    {
                        dashState = 2;
                        dashTimer = 0;
                    }
                    else
                    {
                        dashTimer++;
                        dirToDash.Normalize();
                        Projectile.velocity = dirToDash * 22f; 
                        Projectile.position += Projectile.velocity;

                        // Rotasi pandangan lurus searah dash
                        Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                    }
                }
                else if (dashState == 2) // Recovery Singkat
                {
                    dashTimer++;
                    Projectile.velocity *= 0.8f;

                    if (dashTimer >= 8)
                    {
                        dashState = 0; 
                        dashTimer = 0;
                    }
                }
            }
            else
            {
                IsDashing = false;
                dashState = 0;
                dashTimer = 0;

                Player player = Main.player[Projectile.owner];
                Vector2 handPos = player.RotatedRelativePoint(player.MountedCenter, true);
                Vector2 toMouse = Main.MouseWorld - handPos;
                if (toMouse.Length() > BaseMaxRange)
                {
                    toMouse.Normalize();
                    toMouse *= BaseMaxRange;
                }

                Vector2 targetPos = handPos + toMouse;
                Vector2 dirToTarget = targetPos - Projectile.Center;
                if (dirToTarget.Length() > 16f)
                {
                    dirToTarget.Normalize();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, dirToTarget * 14f, 0.12f);
                }
                else
                {
                    Projectile.velocity *= 0.84f;
                }
                Projectile.position += Projectile.velocity;
                Projectile.rotation = (Main.MouseWorld - Projectile.Center).ToRotation() + MathHelper.PiOver2;
            }
        }

        private void UpdateForm1Shooting()
        {
            NPC target = FindNearestNPC(Projectile.Center, 600f);
            if (target == null)
                return;

            shootTimer++;

            int interval = 22;
            int maxSeeds = 10;

            if (shootPhase == 0) { interval = 22; maxSeeds = 10; }       
            else if (shootPhase == 1) { interval = 12; maxSeeds = 15; }  
            else if (shootPhase == 2) { interval = 6; maxSeeds = 20; }   

            if (shootPhase <= 2)
            {
                if (shootTimer >= interval)
                {
                    shootTimer = 0;
                    ShootSeed(target);
                    seedsShot++;

                    if (seedsShot >= maxSeeds)
                    {
                        seedsShot = 0;
                        shootPhase++;
                    }
                }
            }
            else if (shootPhase == 3)
            {
                ShootThornBall(target);
                shootPhase = 4;
                shootTimer = 0;
            }
            else if (shootPhase == 4) 
            {
                if (shootTimer >= 60)
                {
                    ResetShootingPattern();
                    CanEnterStage2 = true; 
                }
            }
        }

        private void ShootSeed(NPC target)
        {
            Vector2 vel = target.Center - Projectile.Center;
            if (vel == Vector2.Zero) vel = new Vector2(0, -1);
            vel.Normalize();
            vel *= 12f;

            bool isPoison = Main.rand.NextFloat() < 0.10f; 
            int projType = isPoison 
                ? ModContent.ProjectileType<PlanteraPoisonSeedProjectile>() 
                : ModContent.ProjectileType<PlanteraSeedProjectile>();

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                vel,
                projType,
                Projectile.damage,
                Projectile.knockBack * 0.5f,
                Projectile.owner
            );
        }

        private void ShootThornBall(NPC target)
        {
            Vector2 toTarget = target.Center - Projectile.Center;
            float distance = toTarget.Length();

            float T = MathHelper.Clamp(distance / 12f, 15f, 60f);
            float gravity = 0.2f;

            float vx = toTarget.X / T;
            float vy = (toTarget.Y - 0.5f * gravity * T * T) / T;

            Vector2 vel = new Vector2(vx, vy);

            Projectile.NewProjectile(
                Projectile.GetSource_FromThis(),
                Projectile.Center,
                vel,
                ModContent.ProjectileType<PlanteraThornBallProjectile>(),
                Projectile.damage,
                Projectile.knockBack * 0.8f,
                Projectile.owner
            );
        }

        private void ResetShootingPattern()
        {
            shootTimer = 0;
            shootPhase = 0;
            seedsShot = 0;
        }

        private void DespawnMyTentacles()
        {
            int tentacleType = ModContent.ProjectileType<PlanteraTentacleProjectile>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == tentacleType && (int)p.ai[0] == Projectile.whoAmI)
                {
                    p.Kill();
                }
            }
        }

        private NPC FindNPCNearCursor(float maxDistance)
        {
            NPC nearest = null;
            float minDistance = maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy())
                {
                    float dist = Vector2.Distance(Main.MouseWorld, npc.Center);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearest = npc;
                    }
                }
            }
            return nearest;
        }

        private NPC FindNearestNPC(Vector2 center, float maxDistance)
        {
            NPC nearest = null;
            float minDistance = maxDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy())
                {
                    float dist = Vector2.Distance(center, npc.Center);
                    if (dist < minDistance)
                    {
                        minDistance = dist;
                        nearest = npc;
                    }
                }
            }
            return nearest;
        }

        public Vector2 GetHeadChainAttachPoint()
        {
            return Projectile.Center + (IsSecondForm ? HeadChainOffsetSecondForm : HeadChainOffsetFirstForm);
        }

        private int GetMyTentacleCount()
        {
            int count = 0;
            int tentacleType = ModContent.ProjectileType<PlanteraTentacleProjectile>();

            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == tentacleType && (int)p.ai[0] == Projectile.whoAmI)
                {
                    count++;
                }
            }
            return count;
        }

        private void SpawnTentaclesIfNeeded()
        {
            if (!IsSecondForm)
                return;

            if (Main.myPlayer != Projectile.owner)
                return; 

            int currentTentacles = GetMyTentacleCount();
            if (currentTentacles >= TentacleCount)
                return;

            int needed = TentacleCount - currentTentacles;
            for (int i = 0; i < needed; i++)
            {
                float angleOffset = MathHelper.TwoPi / TentacleCount * (currentTentacles + i);

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(),
                    Projectile.Center,
                    Vector2.Zero,
                    ModContent.ProjectileType<PlanteraTentacleProjectile>(),
                    (int)(Projectile.damage * 0.5f),
                    Projectile.knockBack * 0.5f,
                    Projectile.owner,
                    Projectile.whoAmI,
                    angleOffset         
                );
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Transisi Stage 1 ke Stage 2 jika sudah menyelesaikan 1 putaran tembakan
            if (!IsSecondForm && CanEnterStage2)
            {
                IsSecondForm = true;
                Stage2Timer = 1200; // Timer 20 detik
                dashState = 0;
                dashTimer = 0;
            }

            if (IsSecondForm)
            {
                target.AddBuff(BuffID.Poisoned, 60 * 4);

                // --- PEMICU SPORE CLOUD PAS HIT MUSUH SAAT DASH ---
                if (IsDashing && Main.myPlayer == Projectile.owner)
                {
                    // 1. Spawn SporeCloud tepat di lokasi musuh yang di-hit
                    Vector2 cloudVel = Main.rand.NextVector2Circular(2f, 2f);
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        target.Center,
                        cloudVel,
                        ModContent.ProjectileType<PlanteraSporeCloudProjectile>(),
                        (int)(Projectile.damage * 0.6f),
                        0f,
                        Projectile.owner
                    );

                    // 2. Memicu semua Tentakel untuk menyemburkan SporeCloud ke luar
                    TriggerTentacleSporeClouds();
                }
            }
        }

        private void TriggerTentacleSporeClouds()
        {
            int tentacleType = ModContent.ProjectileType<PlanteraTentacleProjectile>();
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.type == tentacleType && (int)p.ai[0] == Projectile.whoAmI)
                {
                    if (p.ModProjectile is PlanteraTentacleProjectile tentacle)
                    {
                        tentacle.EmitSporeCloud();
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            DrawChainToPlayer();

            Texture2D texture = IsSecondForm
                ? ModContent.Request<Texture2D>("TheSanity/Projectiles/PlanteraGraspSecondForm").Value
                : TextureAssets.Projectile[Projectile.type].Value;

            Vector2 origin = new Vector2(texture.Width / 2f, texture.Height / 2f);

            // Trail
            for (int i = Projectile.oldPos.Length - 1; i >= 0; i--)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                    continue;

                float progress = 1f - (i / (float)Projectile.oldPos.Length); 
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = lightColor * (progress * 0.35f); 

                Main.EntitySpriteDraw(
                    texture, drawPos, null, trailColor,
                    Projectile.oldRot[i], origin, Projectile.scale,
                    SpriteEffects.None, 0
                );
            }

            // Sprite Utama
            Main.EntitySpriteDraw(
                texture, Projectile.Center - Main.screenPosition, null, lightColor,
                Projectile.rotation, origin, Projectile.scale,
                SpriteEffects.None, 0
            );

            return false; 
        }

        private void DrawChainToPlayer()
        {
            Player player = Main.player[Projectile.owner];
            Vector2 start = player.RotatedRelativePoint(player.MountedCenter, true) + HandChainOffset;
            Vector2 end = GetHeadChainAttachPoint();
            DrawChainSegment(start, end);
        }

        public static void DrawChainSegment(Vector2 start, Vector2 end, float scale = 1f, float edgeTrimStart = 0f, float edgeTrimEnd = 0f)
        {
            Texture2D chainTex = TextureAssets.Chain27.Value;

            Vector2 fullDir = end - start;
            float fullDist = fullDir.Length();
            if (fullDist < 2f) return;
            Vector2 dirNorm = fullDir / fullDist;

            Vector2 trimmedStart = start + dirNorm * edgeTrimStart;
            Vector2 trimmedEnd = end - dirNorm * edgeTrimEnd;
            Vector2 dir = trimmedEnd - trimmedStart;
            float dist = dir.Length();
            if (dist < 2f) return;

            float rot = dir.ToRotation() + MathHelper.PiOver2; 
            float linkLength = chainTex.Height * scale;
            int linkCount = (int)(dist / linkLength) + 1;
            Vector2 step = dir / linkCount;
            Vector2 origin = new Vector2(chainTex.Width / 2f, chainTex.Height / 2f);

            for (int i = 0; i < linkCount; i++)
            {
                Vector2 pos = trimmedStart + step * (i + 0.5f);
                Color color = Lighting.GetColor(pos.ToTileCoordinates());
                Main.EntitySpriteDraw(
                    chainTex, pos - Main.screenPosition, null, color,
                    rot, origin, scale, SpriteEffects.None, 0
                );
            }
        }
    }
}