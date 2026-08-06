using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class MeatballHealingOrb : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_58";

        public override void SetDefaults()
        {
            Projectile.width = 12;
            Projectile.height = 12;
            Projectile.aiStyle = -1;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 300;
            Projectile.scale = 0.8f;
        }

        public override void AI()
        {
            // PARTIKEL VAMPIRE KNIVES HEAL
            if (Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(
                    Projectile.Center, 
                    DustID.VampireHeal, // Menggunakan ID Partikel Vampire Knives
                    Main.rand.NextVector2Circular(1.5f, 1.5f), 
                    0, 
                    default, 
                    1.2f
                );
                d.noGravity = true;
            }

            // Smart Target Player HP Terendah
            Player targetPlayer = null;
            float lowestHealthRatio = 1f;

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead)
                {
                    float healthRatio = (float)p.statLife / p.statLifeMax2;
                    if (targetPlayer == null || healthRatio < lowestHealthRatio)
                    {
                        lowestHealthRatio = healthRatio;
                        targetPlayer = p;
                    }
                }
            }

            if (targetPlayer != null)
            {
                Vector2 direction = Vector2.Normalize(targetPlayer.Center - Projectile.Center);
                float speed = 10f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, direction * speed, 0.08f);

                // Rotasi bagian atas sprite Heart menghadap ke player
                if (Projectile.velocity != Vector2.Zero)
                {
                    Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
                }

                if (Projectile.Hitbox.Intersects(targetPlayer.Hitbox))
                {
                    targetPlayer.Heal(1);
                    Projectile.Kill();
                }
            }
        }
    }
}