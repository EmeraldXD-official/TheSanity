using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;

namespace TheSanity.Projectiles
{
    public class CelestialImpalerProj : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/CelestialImpalerProj";

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.aiStyle = -1;
            Projectile.tileCollide = true;
        }

        public override void AI()
        {
            if (Projectile.ai[0] == 0) // State terbang
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            }
            else // State menancap
            {
                Projectile.velocity = Vector2.Zero;

                NPC target = Main.npc[(int)Projectile.ai[1]];
                if (!target.active) { Projectile.Kill(); return; }

                Projectile.Center = target.Center;

                Projectile.localAI[0]++;
                if (Projectile.localAI[0] >= 300) // 5 detik
                {
                    // Spawn 3 bintang dari atas target (efek tambahan)
                    SpawnStars(target.Center, Projectile.damage * 2, Projectile.owner);
                    Projectile.Kill();
                }
            }
        }

        // Fungsi untuk spawn 3 bintang dari atas menuju posisi target
        private void SpawnStars(Vector2 targetPos, int damage, int owner)
        {
            for (int i = 0; i < 3; i++)
            {
                bool isSuper = Main.rand.NextFloat() < 0.20f;
                int projType = isSuper ? ModContent.ProjectileType<SuperStarProj>() : ModContent.ProjectileType<StarProj>();

                Vector2 spawnPos = new Vector2(
                    targetPos.X + Main.rand.Next(-250, 250),
                    targetPos.Y - Main.rand.Next(400, 700) - (i * 80)
                );

                Vector2 dir = targetPos - spawnPos;
                dir.Normalize();
                float speed = 12f + Main.rand.NextFloat(4f);
                Vector2 vel = dir * speed;

                Projectile.NewProjectile(Projectile.GetSource_FromThis(), spawnPos, vel, projType, damage, 0f, owner);
            }
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            // Saat mengenai musuh, spawn 3 bintang dari atas
            SpawnStars(target.Center, Projectile.damage, Projectile.owner);

            // Masuk ke state menancap
            Projectile.ai[0] = 1;
            Projectile.ai[1] = target.whoAmI;
            Projectile.netUpdate = true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, texture.Size() / 2, 1f, SpriteEffects.None, 0);
            return false;
        }
    }
}