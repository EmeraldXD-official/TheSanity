using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Luminance.Core.Graphics;

namespace TheSanity.GlobalNPC.Bosses.EmpressRework
{
    // Proyektil yang ditembakkan tiap fairy mini Empress pas fase MiniEmpressCombo,
    // dan juga dipakai EmpressLiteClone buat nembak versi "lite"-nya pas Clone Assault.
    public class MiniEmpressBolt : ModProjectile
    {
        // Sesuaikan path ini ke texture kamu sendiri
        public override string Texture => "TheSanity/GlobalNPC/Bosses/EmpressRework/MiniEmpressBolt";

        // True kalau bolt ini ditembakkan EmpressLiteClone (bukan mini fairy asli).
        // Dipakai buat bedain tampilan (lebih transparan/kecil) & mencegah clone ikut split-spam.
        public bool isLiteClone = false;

        // True buat bolt dari mini fairy fase 3: pecah jadi beberapa shard kecil pas mati (variasi + brutal).
        public bool canSplitOnExpire = false;

        private const int SplitShardCount = 3;

        public override void SetStaticDefaults()
        {
            // Trail lebih panjang & lebih mulus dibanding default (5 posisi), biar bolt keliatan ngebut/brutal
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.friendly = false;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
            Projectile.alpha = 0;
            Projectile.extraUpdates = 1; // gerak lebih mulus & responsif buat proyektil kecil-cepat
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            // Sedikit percepatan progresif di awal perjalanan biar bolt terasa "ngegas", bukan speed datar terus
            if (!isLiteClone && Projectile.ai[0] < 20f)
            {
                Projectile.ai[0]++;
                Projectile.velocity *= 1.01f;
            }

            int dustFreq = isLiteClone ? 4 : 3;
            if (Main.netMode != NetmodeID.Server && Main.rand.NextBool(dustFreq))
            {
                Dust d = Dust.NewDustDirect(Projectile.Center, 2, 2, DustID.RainbowMk2, 0f, 0f, 150, default, isLiteClone ? 0.8f : 1f);
                d.noGravity = true;
                d.velocity *= 0.3f;
            }
        }

        public override void OnKill(int timeLeft)
        {
            if (Main.netMode != NetmodeID.Server)
            {
                int dustCount = isLiteClone ? 6 : 10;
                for (int i = 0; i < dustCount; i++)
                {
                    Vector2 dustVel = Main.rand.NextVector2Circular(3f, 3f);
                    Dust d = Dust.NewDustDirect(Projectile.Center, 2, 2, DustID.RainbowMk2, dustVel.X, dustVel.Y, 150, default, 1.3f);
                    d.noGravity = true;
                }
            }

            // Efek Luminance: sedikit shake pas kena/mati, clone lebih halus daripada bolt asli
            ScreenShakeSystem.StartShake(isLiteClone ? 1.5f : 2f, MathHelper.TwoPi, null);

            // Variasi baru: bolt fase-brutal (mini fairy fase 3) pecah jadi shard kecil pas mati.
            // isLiteClone dipakai ganda sebagai penanda "jangan split lagi" biar shard-nya gak split beranak-pinak.
            if (canSplitOnExpire && !isLiteClone && Main.myPlayer == Projectile.owner)
            {
                for (int i = 0; i < SplitShardCount; i++)
                {
                    float angle = MathHelper.TwoPi * (i / (float)SplitShardCount) + Main.rand.NextFloat(-0.3f, 0.3f);
                    Vector2 shardVel = angle.ToRotationVector2() * Main.rand.NextFloat(4f, 6f);
                    int p = Projectile.NewProjectile(Projectile.GetSource_Death(), Projectile.Center, shardVel, ModContent.ProjectileType<MiniEmpressBolt>(), (int)(Projectile.damage * 0.4f), 0f, Projectile.owner);
                    if (p != Main.maxProjectiles)
                    {
                        var shard = Main.projectile[p].ModProjectile as MiniEmpressBolt;
                        if (shard != null)
                        {
                            shard.isLiteClone = true; // dipakai buat bikin shard lebih kecil & anti-split
                            Main.projectile[p].timeLeft = 40;
                            Main.projectile[p].scale = 0.6f;
                        }
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>(Texture).Value;

            float hue = Main.GlobalTimeWrappedHourly * 0.4f % 1f;
            Color pastelColor = Main.hslToRgb(hue, 0.6f, 0.8f);
            pastelColor.A = 0;

            float baseOpacity = isLiteClone ? 0.7f : 1f;
            float baseScale = isLiteClone ? 0.8f : 1f;

            Vector2 drawPos = Projectile.Center - Main.screenPosition;

            // Glow halo lembut di belakang bolt, nambah kesan "chromatic" pas ngebut
            Color glowColor = pastelColor * 0.4f * baseOpacity;
            glowColor.A = 0;
            Main.EntitySpriteDraw(tex, drawPos, null, glowColor, Projectile.rotation, tex.Size() / 2f, baseScale * 1.8f, SpriteEffects.None, 0);

            // Trail lebih panjang (TrailCacheLength 14) dengan gradasi hue tipis sepanjang ekornya,
            // jadi bukan cuma fade polos tapi keliatan "chromatic streak"
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                float trailFactor = 1f - (i / (float)Projectile.oldPos.Length);
                Vector2 trailPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                float trailHue = (hue + i * 0.015f) % 1f;
                Color trailColor = Main.hslToRgb(trailHue, 0.6f, 0.8f) * trailFactor * 0.5f * baseOpacity;
                trailColor.A = 0;

                Main.EntitySpriteDraw(tex, trailPos, null, trailColor, Projectile.oldRot[i], tex.Size() / 2f, baseScale * trailFactor, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(tex, drawPos, null, pastelColor * baseOpacity, Projectile.rotation, tex.Size() / 2f, baseScale, SpriteEffects.None, 0);
            return false;
        }
    }
}
