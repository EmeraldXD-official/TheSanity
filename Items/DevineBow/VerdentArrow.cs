using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.DevineBow
{
    public class VerdentArrow : ModProjectile
    {
        // Warna hijau terang utama dipakai untuk tint sprite, trail, cahaya, dan dust
        private static readonly Color VerdentGreen = new Color(110, 255, 130);

        public override void SetStaticDefaults()
        {
            // Aktifkan trail bawaan tModLoader supaya Projectile.oldPos terisi otomatis tiap tick
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 10;
            Projectile.height = 10;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 600;
        }

        public override void AI()
        {
            // Cahaya hijau terang di sekitar panah (lampu dinamis)
            Lighting.AddLight(Projectile.Center, VerdentGreen.ToVector3() * 0.9f);

            // Efek visual trail partikel (lebih rapat & lebih terang dari versi sebelumnya)
            if (Main.rand.NextBool(2))
            {
                int d = Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GreenTorch, 0f, 0f, 50, default, 1.4f);
                Main.dust[d].noGravity = true;
                Main.dust[d].velocity *= 0.3f;
            }

            // ai[0] menentukan mode gerak panah:
            //   0f = non-homing biasa (dipakai burst 3 panah)
            //   1f = homing instan sejak ditembak (mode lama, tetap didukung)
            //   2f = menyebar dulu, baru homing setelah delay (dipakai volley 5 panah pasca-charge)
            if (Projectile.ai[0] == 1f)
            {
                HomingLogic(400f, 8f);
            }
            else if (Projectile.ai[0] == 2f)
            {
                if (Projectile.ai[1] > 0f)
                {
                    // Fase "menyebar": panah masih terbang lurus mengikuti arah tembak awal
                    Projectile.ai[1] -= 1f;

                    // Sedikit efek kilau ekstra selama fase menyebar biar animasinya terasa
                    if (Main.rand.NextBool(2))
                    {
                        Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GreenTorch, 0f, 0f, 50, default, 1.6f);
                    }
                }
                else
                {
                    // Delay habis, mulai homing (radius & kecepatan sedikit lebih besar dari mode 1f)
                    HomingLogic(500f, 10f);
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            if (!TextureAssets.Projectile[Projectile.type].IsLoaded)
            {
                return false;
            }

            Texture2D tex = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = tex.Size() / 2f;

            // Trail hijau menyala di belakang panah (memudar & mengecil ke belakang)
            for (int i = 0; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero)
                {
                    continue;
                }

                float progress = i / (float)Projectile.oldPos.Length;
                float trailAlpha = (1f - progress) * 0.5f;
                Vector2 drawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

                Color trailColor = VerdentGreen * trailAlpha;
                trailColor.A = 0; // gambar additive-ish supaya trail terlihat menyala, bukan solid

                Main.EntitySpriteDraw(tex, drawPos, null, trailColor, Projectile.rotation, origin,
                    Projectile.scale * (1f - progress * 0.3f), SpriteEffects.None, 0);
            }

            // Panah utama dengan tint hijau terang di atas warna cahaya normal
            Color mainColor = Color.Lerp(lightColor, VerdentGreen, 0.6f);
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(tex, mainDrawPos, null, mainColor, Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Ledakan bintang-bintang hijau saat panah kena musuh
            for (int i = 0; i < 10; i++)
            {
                Vector2 starVel = (MathHelper.TwoPi * i / 10f).ToRotationVector2() * Main.rand.NextFloat(2f, 4.5f);
                int d = Dust.NewDust(target.position, target.width, target.height, DustID.Firework_Green, starVel.X, starVel.Y, 0, default, 1.5f);
                Main.dust[d].noGravity = true;
            }

            // Kilau tambahan di titik tengah musuh biar lebih "meledak"
            for (int i = 0; i < 4; i++)
            {
                int d = Dust.NewDust(target.position, target.width, target.height, DustID.GemEmerald, 0f, -2f, 0, default, 1.8f);
                Main.dust[d].noGravity = true;
            }

            // SFX saat kena musuh. Ganti SoundID.Item29 sesuai selera / pakai sound custom sendiri
            // (lihat contoh SoundStyle kustom di komentar VerdentBow.cs)
            SoundEngine.PlaySound(SoundID.Item29, target.Center);
        }

        private void HomingLogic(float maxDetectRadius, float homingSpeed)
        {
            NPC closestNPC = FindClosestNPC(maxDetectRadius);
            if (closestNPC != null)
            {
                // Menuju target secara perlahan (tidak langsung snap, biar terasa halus)
                Vector2 move = closestNPC.Center - Projectile.Center;
                move.Normalize();
                move *= homingSpeed;
                Projectile.velocity = (Projectile.velocity * 20f + move) / 21f;
            }
        }

        public NPC FindClosestNPC(float maxDetectDistance)
        {
            NPC closestNPC = null;
            float sqrMaxDetectDistance = maxDetectDistance * maxDetectDistance;

            for (int k = 0; k < Main.maxNPCs; k++)
            {
                NPC target = Main.npc[k];
                if (target.CanBeChasedBy())
                {
                    float sqrDistanceToTarget = Vector2.DistanceSquared(target.Center, Projectile.Center);
                    if (sqrDistanceToTarget < sqrMaxDetectDistance)
                    {
                        closestNPC = target;
                        sqrMaxDetectDistance = sqrDistanceToTarget;
                    }
                }
            }
            return closestNPC;
        }
    }
}
