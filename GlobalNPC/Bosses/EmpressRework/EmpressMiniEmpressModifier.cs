using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.EmpressRework
{
    // Proyektil vanilla ID 895 = "Fairy Princess" (pet dari item Jewel of Light, didrop Empress of Light Master Mode).
    // Dipakai di sini murni buat visual "mini Empress" yang di-summon Empress asli sebagai bagian dari combo attack.
    //
    // PENTING: karena ID 895 juga dipakai pemain yang beneran punya pet Jewel of Light, semua override di sini
    // SELALU dijaga oleh flag isMiniEmpressSummon supaya pet asli pemain lain tidak ikut kena efek ini.
    public class EmpressMiniEmpressModifier : GlobalProjectile
    {
        public const int MiniEmpressProjectileID = 895;

        public override bool InstancePerEntity => true;

        public bool isMiniEmpressSummon = false;
        public int bossNPCIndex = -1;
        public int summonIndex = 0;
        public int totalSummons = 4;
        public int attackPhase = 1; // diisi EmpressReworkAdvanced.SpawnMiniEmpresses, dipakai buat nge-scale burst/damage/split

        private const int WindupTime = 50;    // durasi muncul + muter2 sebelum nembak (tick), base fase 1
        private const int FadeOutTime = 15;   // durasi menghilang setelah nembak (tick)
        private const float OrbitRadius = 90f;

        private int localTimer = 0;
        private bool hasFired = false;

        // Makin tinggi fase, makin cepat fairy-nya nembak (windup dipangkas, minimal 30 tick biar tetap kebaca player)
        private int EffectiveWindup => Math.Max(30, WindupTime - (attackPhase - 1) * 10);

        public override bool PreAI(Projectile projectile)
        {
            if (projectile.type == MiniEmpressProjectileID && isMiniEmpressSummon)
            {
                RunCustomAI(projectile);
                return false; // skip AI vanilla (pet-follow player), semua gerakan kita handle manual
            }
            return true;
        }

        private void RunCustomAI(Projectile projectile)
        {
            projectile.friendly = false;
            projectile.hostile = false; // fairy-nya sendiri gak nyentuh damage; yang nyerang adalah MiniEmpressBolt
            projectile.tileCollide = false;

            if (bossNPCIndex < 0 || bossNPCIndex >= Main.maxNPCs || !Main.npc[bossNPCIndex].active || Main.npc[bossNPCIndex].type != NPCID.HallowBoss)
            {
                projectile.Kill();
                return;
            }

            NPC boss = Main.npc[bossNPCIndex];
            Player player = Main.player[projectile.owner];

            localTimer++;

            // Posisi orbit di sekitar boss, tiap fairy dapat sudut awal beda + pelan-pelan berputar
            float angle = MathHelper.TwoPi * (summonIndex / (float)Math.Max(totalSummons, 1)) + localTimer * 0.05f;
            float spawnProgress = MathHelper.Clamp(localTimer / 12f, 0f, 1f); // fairy membesar pas baru muncul
            float radius = OrbitRadius * spawnProgress;

            Vector2 orbitOffset = angle.ToRotationVector2() * radius;
            Vector2 targetCenter = boss.Center + orbitOffset;

            projectile.Center = Vector2.Lerp(projectile.Center, targetCenter, 0.35f);
            projectile.velocity = Vector2.Zero;
            projectile.rotation += 0.15f;
            projectile.scale = spawnProgress;

            // Sparkle kecil selagi orbit, biar hidup
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(6))
            {
                Dust d = Dust.NewDustDirect(projectile.Center, 2, 2, DustID.RainbowMk2, 0f, 0f, 150, default, 0.8f);
                d.noGravity = true;
                d.velocity *= 0.2f;
            }

            if (localTimer == EffectiveWindup && !hasFired)
            {
                FireMiniBolt(projectile, boss, player);
                hasFired = true;
            }

            if (hasFired)
            {
                float fadeProgress = MathHelper.Clamp((localTimer - EffectiveWindup) / (float)FadeOutTime, 0f, 1f);
                projectile.scale = MathHelper.Lerp(1f, 0f, fadeProgress);
                if (fadeProgress >= 1f) projectile.Kill();
            }
        }

        private void FireMiniBolt(Projectile projectile, NPC boss, Player player)
        {
            Vector2 shootDir = Vector2.Normalize(player.Center - projectile.Center);
            const float baseSpeed = 8f;
            int damage = 15 + (attackPhase - 1) * 3;

            // Variasi baru: fase 1 nembak 1 bolt (seperti dulu), fase 2 jadi 2-way spread, fase 3 jadi 3-way spread
            int burstCount = attackPhase >= 3 ? 3 : (attackPhase == 2 ? 2 : 1);
            const float spreadStep = 14f;

            for (int i = 0; i < burstCount; i++)
            {
                float offsetDeg = burstCount == 1 ? 0f : (-spreadStep * (burstCount - 1) / 2f) + spreadStep * i;
                Vector2 dir = shootDir.RotatedBy(MathHelper.ToRadians(offsetDeg));
                float speed = baseSpeed + i * 0.3f;

                int p = Projectile.NewProjectile(boss.GetSource_FromAI(), projectile.Center, dir * speed, ModContent.ProjectileType<MiniEmpressBolt>(), damage, 0f, Main.myPlayer);
                if (p != Main.maxProjectiles)
                {
                    var bolt = Main.projectile[p].ModProjectile as MiniEmpressBolt;
                    // Fase 3: tiap bolt mini fairy pecah jadi shard kecil pas mati/expire, nambah tekanan (brutal)
                    if (bolt != null && attackPhase >= 3) bolt.canSplitOnExpire = true;
                }
            }

            Terraria.Audio.SoundEngine.PlaySound(SoundID.Item9, projectile.Center);

            // Efek Luminance: sedikit screen shake tiap fairy nembak, biar berasa combo-nya "nendang" tanpa berlebihan
            ScreenShakeSystem.StartShake(3f, MathHelper.TwoPi, null);
        }

        public override bool PreDraw(Projectile projectile, ref Color lightColor)
        {
            if (projectile.type != MiniEmpressProjectileID || !isMiniEmpressSummon) return true;

            Texture2D texture = TextureAssets.Projectile[projectile.type].Value;
            int frameCount = Math.Max(Main.projFrames[projectile.type], 1);
            int frameHeight = texture.Height / frameCount;
            int startY = frameHeight * projectile.frame;
            Rectangle sourceRectangle = new Rectangle(0, startY, texture.Width, frameHeight);
            Vector2 origin = sourceRectangle.Size() / 2f;

            float hue = (Main.GlobalTimeWrappedHourly * 0.3f + summonIndex / (float)Math.Max(totalSummons, 1)) % 1f;
            Color pastelColor = Main.hslToRgb(hue, 0.6f, 0.8f) * projectile.scale;
            pastelColor.A = 0;

            Vector2 drawPos = projectile.Center - Main.screenPosition;

            // Glow lembut di belakang fairy
            Color glowColor = pastelColor * 0.35f;
            glowColor.A = 0;
            Main.EntitySpriteDraw(texture, drawPos, sourceRectangle, glowColor, projectile.rotation, origin, projectile.scale * 1.4f, SpriteEffects.None, 0);

            Main.EntitySpriteDraw(texture, drawPos, sourceRectangle, Color.White * projectile.scale, projectile.rotation, origin, projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}
