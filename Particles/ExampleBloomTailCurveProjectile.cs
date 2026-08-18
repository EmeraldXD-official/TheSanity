using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using TheSanity.Particles;

namespace TheSanity.Projectiles
{
    /// <summary>
    /// CONTOH PEMAKAIAN BloomTailCurve - tail BENERAN MELENGKUNG ngikutin
    /// lintasan gerak (bukan cuma rotate garis lurus kayak
    /// ExampleBloomTailProjectile). Buat ini, kita BUTUH riwayat posisi
    /// (trailPoints), sama kayak ExampleDualLineTrailProjectile - satu quad
    /// gak akan pernah cukup buat melengkung, ini batas geometri, bukan soal
    /// smoothing.
    /// </summary>
    public class ExampleBloomTailCurveProjectile : ModProjectile
    {
        private const float TailMaxLength = 180f;
        private const float TailWidth = 20f; // samain sama lebar asli BloomTail.png

        private const float HomingDetectRadius = 700f;
        private const float HomingTurnStrength = 0.06f;

        // Riwayat posisi buat sumber kurva - identik alasannya sama
        // ExampleDualLineTrailProjectile (butuh lebih banyak titik daripada
        // Projectile.oldPos bawaan biar tail tetep keisi penuh).
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
            // Insert titik kepala DI SINI (posisi final tick ini), sama kayak
            // fix di ExampleDualLineTrailProjectile - biar kepala tail selalu
            // sinkron persis sama posisi projectile yang lagi digambar.
            trailPoints.Insert(0, Projectile.Center);

            const int rawPointCap = 200;
            if (trailPoints.Count > rawPointCap)
                trailPoints.RemoveRange(rawPointCap, trailPoints.Count - rawPointCap);

            Main.spriteBatch.End();
            Main.spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

            BloomTailCurve.Draw(
                trailPoints,
                // Lebar KONSTAN - biarin taper visualnya diurus sama gambar
                // BloomTail.png sendiri (yang di-stretch penuh sepanjang kurva).
                widthFunc: progress => TailWidth,
                colorFunc: progress => Color.White * Projectile.Opacity,
                maxLength: TailMaxLength
            );

            Main.spriteBatch.End();
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
