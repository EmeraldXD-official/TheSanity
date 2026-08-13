using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.Ambarium
{
    // ==========================================
    // 🔮 1. SUMMONER SENTINEL (SKULL MELAYANG)
    // ==========================================
   public class AmbariumSentinelProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/Armor/Ambarium/AmbariumSentinel";

        public override void SetStaticDefaults()
        {
            Main.projFrames[Type] = 6; // 6 Frame Animasi
        }

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.ignoreWater = true;
        
            Projectile.scale = 0.5f; // Skala ukuran Sentinel
        }
        public override bool? CanHitNPC(NPC target)
        {
            return false;
        }

        public override void AI()
        {
            Player player = Main.player[Projectile.owner];

            // 🛑 Hancurkan jika player mati / tidak aktif / tidak pakai Summoner Set
            if (!player.active || player.dead || !player.GetModPlayer<AmbariumArmorPlayer>().summonerSet)
            {
                Projectile.Kill();
                return;
            }

            // 🧹 BERSIHKAN DUPLIKAT: Pastikan hanya ada 1 Sentinel aktif
            for (int i = 0; i < Main.maxProjectiles; i++)
            {
                Projectile p = Main.projectile[i];
                if (p.active && p.owner == Projectile.owner && p.type == Type && p.whoAmI != Projectile.whoAmI)
                {
                    p.Kill();
                }
            }

            // 🎯 KUNCI POSISI: Lurus di atas kepala player (MountedCenter)
            Vector2 targetPos = player.MountedCenter + new Vector2(0f, -50f);

            // Teleport langsung jika jaraknya terlalu jauh (misal saat spawn/teleport)
            if (Vector2.Distance(Projectile.Center, targetPos) > 200f)
            {
                Projectile.Center = targetPos;
            }
            else
            {
                Projectile.Center = Vector2.Lerp(Projectile.Center, targetPos, 0.3f);
            }

            Lighting.AddLight(Projectile.Center, 0.5f, 0.1f, 0.7f);

            // 🎬 Animasi Sprite 6 Frame
            Projectile.frameCounter++;
            if (Projectile.frameCounter >= 6)
            {
                Projectile.frameCounter = 0;
                Projectile.frame = (Projectile.frame + 1) % Main.projFrames[Type];
            }

            // ⚡ EFEK MENEMBAK SHARD JIKA GAUGE PENUH
            if (Projectile.ai[0] >= 100f)
            {
                Projectile.ai[0] = 0f;
                SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.2f }, Projectile.Center);

                if (Projectile.owner == Main.myPlayer)
                {
                    Vector2 baseDir = (Main.MouseWorld - Projectile.Center).SafeNormalize(Vector2.UnitY);

                    for (int i = -1; i <= 1; i++)
                    {
                        Vector2 shootVel = baseDir.RotatedBy(MathHelper.ToRadians(i * 15f)) * 12f;

                        Projectile.NewProjectile(
                            Projectile.GetSource_FromThis(),
                            Projectile.Center,
                            shootVel,
                            ModContent.ProjectileType<AmbariumSentinelShardProj>(),
                            (int)(Projectile.damage * 0.5f),
                            Projectile.knockBack,
                            Projectile.owner
                        );
                    }
                }
            }
        }

        // 🎨 METODE MENGGAMBAR PRESISI (PRESISI DITENGAH KEPALA)
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            int frameHeight = texture.Height / Main.projFrames[Type];
            Rectangle sourceRect = new Rectangle(0, frameHeight * Projectile.frame, texture.Width, frameHeight);
            
            // Mengunci titik tengah sprite
            Vector2 origin = sourceRect.Size() * 0.5f;

            // Efek melayang naik-turun halus (hover animation)
            float hoverOffset = (float)Math.Sin(Main.GameUpdateCount * 0.08f) * 4f;
            
            // 💡 JIKA SPRITE GAMBAR KAMU MASIH TERLALU KE KANAN DI KANVAS PNG:
            // Kamu bisa menambah/mengurangi nilai X di bawah ini (misal: new Vector2(-10f, hoverOffset))
            Vector2 drawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, hoverOffset);

            Main.EntitySpriteDraw(
                texture,
                drawPos,
                sourceRect,
                lightColor,
                0f,
                origin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false; // Matikan gambar standar Terraria
        }
    }

    // ==========================================
    // 💎 2. SUMMONER SHARD PROJEKTIL (DARI SENTINEL)
    // ==========================================
    public class AmbariumSentinelShardProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/Armor/Ambarium/AmbariumShard";

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Lighting.AddLight(Projectile.Center, 0.7f, 0.2f, 0.9f);

            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.1f);
            dust.noGravity = true;

            // 🎯 HOMING KE MUSUH TERDEKAT
            NPC target = FindNearestTarget(600f);
            if (target != null)
            {
                Vector2 targetDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetDir * 15f, 0.14f);
            }
        }

        private NPC FindNearestTarget(float range)
        {
            NPC closest = null;
            float closestDist = range;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy(this) && Vector2.Distance(Projectile.Center, npc.Center) < closestDist)
                {
                    closestDist = Vector2.Distance(Projectile.Center, npc.Center);
                    closest = npc;
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.ShadowFlame, 180);
        }
    }

    // ==========================================
    // 🪄 3. MAGIC SUPERNOVA CORE
    // ==========================================
    public class AmbariumSupernovaCore : ModProjectile
    {
        public override string Texture => "TheSanity/Items/Armor/Ambarium/AmbariumShard";

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.friendly = false;
            Projectile.timeLeft = 30;
            Projectile.tileCollide = false;
            Projectile.scale = 1.8f;
        }

        public override void AI()
        {
            Projectile.rotation += 0.25f;
            Lighting.AddLight(Projectile.Center, 0.8f, 0.2f, 1.0f);

            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.2f);
            dust.noGravity = true;
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);

            if (Projectile.owner == Main.myPlayer)
            {
                for (int i = 0; i < 8; i++)
                {
                    float angle = MathHelper.TwoPi / 8f * i;
                    Vector2 vel = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * 9f;

                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        vel,
                        ModContent.ProjectileType<AmbariumShardProj>(),
                        Projectile.damage/2,
                        Projectile.knockBack,
                        Projectile.owner
                    );
                }
            }
        }
    }

    // ==========================================
    // 💎 4. MAGIC HOMING SHARD
    // ==========================================
    public class AmbariumShardProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/Armor/Ambarium/AmbariumShard";

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 0.9f);
            dust.noGravity = true;

            NPC target = FindNearestTarget(500f);
            if (target != null)
            {
                Vector2 targetDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetDir * 14f, 0.12f);
            }
        }

        private NPC FindNearestTarget(float range)
        {
            NPC closest = null;
            float closestDist = range;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy(this) && Vector2.Distance(Projectile.Center, npc.Center) < closestDist)
                {
                    closestDist = Vector2.Distance(Projectile.Center, npc.Center);
                    closest = npc;
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.ShadowFlame, 120);
        }
    }

    // ==========================================
    // 🏹 5. RANGER TARGET MARKER
    // ==========================================
    public class AmbariumTargetMarker : ModProjectile
    {
        public override string Texture => "Terraria/Images/Glow_70";

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.timeLeft = 300;
            Projectile.tileCollide = false;
        }

        public override void AI()
        {
            int npcIndex = (int)Projectile.ai[0];
            if (npcIndex >= 0 && npcIndex < Main.maxNPCs && Main.npc[npcIndex].active)
            {
                NPC target = Main.npc[npcIndex];
                Projectile.Center = target.Center;

                Lighting.AddLight(target.Center, 1.0f, 0.2f, 0.8f);

                Dust dust = Dust.NewDustDirect(target.position, target.width, target.height, DustID.PurpleCrystalShard, 0f, 0f, 100, default, 1.2f);
                dust.noGravity = true;
            }
            else
            {
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawOrigin = texture.Size() * 0.5f;
            Color auraColor = new Color(220, 80, 255, 0) * 0.7f;

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                auraColor,
                0f,
                drawOrigin,
                1.2f,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }

    // ==========================================
    // 👥 6. RANGER SHADOW CLONE
    // ==========================================
    public class AmbariumShadowCloneProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Glow_70";

        private bool firedShot = false;

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.timeLeft = 22;
            Projectile.tileCollide = false;
            Projectile.alpha = 255;
        }

        public override void AI()
        {
            if (Projectile.timeLeft > 12)
            {
                Projectile.alpha -= 35;
                if (Projectile.alpha < 70) Projectile.alpha = 70;
            }
            else
            {
                Projectile.alpha += 30;
            }

            Lighting.AddLight(Projectile.Center, 0.6f, 0.1f, 0.8f);
            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, Projectile.alpha, default, 1.1f);
            dust.noGravity = true;

            if (!firedShot && Projectile.timeLeft <= 16)
            {
                firedShot = true;
                SoundEngine.PlaySound(SoundID.Item5 with { Pitch = 0.2f }, Projectile.Center);

                int targetIndex = (int)Projectile.ai[0];
                Vector2 shootVel = Projectile.velocity;

                if (targetIndex >= 0 && targetIndex < Main.maxNPCs && Main.npc[targetIndex].active)
                {
                    shootVel = (Main.npc[targetIndex].Center - Projectile.Center).SafeNormalize(Vector2.UnitX) * 16f;
                }

                if (Projectile.owner == Main.myPlayer)
                {
                    Projectile.NewProjectile(
                        Projectile.GetSource_FromThis(),
                        Projectile.Center,
                        shootVel,
                        ModContent.ProjectileType<AmbariumShadowShotProj>(),
                        Projectile.damage,
                        Projectile.knockBack,
                        Projectile.owner
                    );
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Vector2 drawOrigin = texture.Size() * 0.5f;
            Color shadowColor = new Color(180, 50, 255, 0) * ((255 - Projectile.alpha) / 255f);

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                shadowColor,
                0f,
                drawOrigin,
                1.3f,
                SpriteEffects.None,
                0
            );

            return false;
        }
    }

    // ==========================================
    // 🏹 7. SHADOW SHOT
    // ==========================================
    public class AmbariumShadowShotProj : ModProjectile
    {
        public override string Texture => "TheSanity/Items/Armor/Ambarium/AmbariumShard";

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 180;
            Projectile.tileCollide = true;
            Projectile.extraUpdates = 1;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            Dust dust = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.2f);
            dust.noGravity = true;

            NPC target = FindNearestTarget(500f);
            if (target != null)
            {
                Vector2 targetDir = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, targetDir * 18f, 0.15f);
            }
        }

        private NPC FindNearestTarget(float range)
        {
            NPC closest = null;
            float closestDist = range;
            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy(this) && Vector2.Distance(Projectile.Center, npc.Center) < closestDist)
                {
                    closestDist = Vector2.Distance(Projectile.Center, npc.Center);
                    closest = npc;
                }
            }
            return closest;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.ShadowFlame, 180);
        }
    }
}