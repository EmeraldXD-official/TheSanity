using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using TheSanity.Particles;

namespace TheSanity.Projectiles
{
    /// <summary>
    /// CONTOH PEMAKAIAN BurstFlameCurve - pola sama persis kayak
    /// ExampleBloomTailCurveProjectile, cuma ganti ke tekstur+blend BurstFlame.
    /// </summary>
    public class ExampleBurstFlameCurveProjectile : ModProjectile
    {
        private const float TailMaxLength = 220f;
        // Lebar disesuaikan proporsi BurstFlame.png (446 lebar : 961 tinggi) -
        // tinggal tuning angka ini sesuai selera, gak harus sama persis rasio PNG.
        private const float TailWidth = 40f;

        // Ujung depan BurstFlame.png gak rata (bentuk api, bukan tepi lurus),
        // jadi titik kepala trail digeser MAJU sejauh ini (pixel, searah arah
        // gerak) dari Projectile.Center - biar bagian yang gak rata itu "nongol"
        // di depan projectile, bukan numpuk pas di tengahnya.
        private const float HeadForwardOffset = 16f;

        private const float HomingDetectRadius = 700f;
        private const float HomingTurnStrength = 0.06f;

        private readonly List<Vector2> trailPoints = new();

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            NPC target = FindClosestEnemy(HomingDetectRadius);
            if (target != null)
            {
                Vector2 toTarget = target.Center - Projectile.Center;
                if (toTarget != Vector2.Zero)
                {
                    float speed = Projectile.velocity.Length();
                    Vector2 desiredDir = Vector2.Normalize(toTarget);
                    Vector2 currentDir = Projectile.velocity == Vector2.Zero
                        ? desiredDir
                        : Vector2.Normalize(Projectile.velocity);

                    float dot = Vector2.Dot(currentDir, desiredDir);
                    if (dot < 0.999f)
                    {
                        Vector2 newDir = Vector2.Normalize(Vector2.Lerp(currentDir, desiredDir, HomingTurnStrength));
                        Projectile.velocity = newDir * speed;
                    }
                }
            }

            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Titik kepala = Projectile.Center digeser MAJU sejauh
            // HeadForwardOffset searah velocity (bukan pas di Center) - lihat
            // catatan HeadForwardOffset di atas. Kalau velocity kebetulan nol
            // (projectile lagi diem), gak ada arah buat digeser jadi ya taruh
            // pas di Center aja (offset nol).
            Vector2 forwardDir = Projectile.velocity != Vector2.Zero
                ? Vector2.Normalize(Projectile.velocity)
                : Vector2.Zero;
            trailPoints.Insert(0, Projectile.Center + forwardDir * HeadForwardOffset);

            const int rawPointCap = 200;
            if (trailPoints.Count > rawPointCap)
                trailPoints.RemoveRange(rawPointCap, trailPoints.Count - rawPointCap);

            Main.spriteBatch.End();

            BurstFlameCurve.Draw(
                trailPoints,
                widthFunc: progress => TailWidth,
                // Additive butuh fade lewat KECERAHAN (RGB), bukan cuma alpha -
                // makanya Color.White * opacity, bukan new Color(255,255,255,alpha).
                colorFunc: progress => Color.White * Projectile.Opacity,
                maxLength: TailMaxLength
            );

            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            return true;
        }

        private NPC FindClosestEnemy(float maxDetectDistance)
        {
            NPC closest = null;
            float closestDist = maxDetectDistance;

            for (int i = 0; i < Main.maxNPCs; i++)
            {
                NPC npc = Main.npc[i];
                if (!npc.CanBeChasedBy(Projectile))
                    continue;

                float dist = Vector2.Distance(npc.Center, Projectile.Center);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = npc;
                }
            }

            return closest;
        }
    }
}
