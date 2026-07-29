using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace TheSanity.GlobalNPC.Bosses.Twinkle
{
    public class StarmerangStar : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.FallingStar;

        public override void SetDefaults() {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 180; 
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            // EFEK GLOWING: Memberikan pancaran aura cahaya kosmik di sekeliling bintang saat melesat
            Lighting.AddLight(Projectile.Center, 0.4f, 0.7f, 1.0f);

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.YellowTorch, Projectile.velocity * 0.2f, 100, Color.White, 0.8f);
                d.noGravity = true;
            }

            Projectile.rotation += 0.15f;

            int targetNPCIndex = (int)Projectile.ai[0];
            if (targetNPCIndex >= 0 && targetNPCIndex < Main.maxNPCs) {
                NPC target = Main.npc[targetNPCIndex];

                if (target.active && target.CanBeChasedBy(Projectile)) {
                    Vector2 desiredVelocity = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 9.5f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, 0.12f);
                }
            }
        }

        public override void Kill(int timeLeft) {
            for (int i = 0; i < 5; i++) {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.YellowTorch, Main.rand.NextVector2Circular(2f, 2f), 0, Color.White, 1f);
                d.noGravity = true;
            }
        }

        // BARU: Menambahkan fungsi draw manual agar bintang bersinar glowing tanpa terpengaruh kegelapan malam/gua
        public override bool PreDraw(ref Color drawColor) {
            Texture2D texture = Terraria.GameContent.TextureAssets.Projectile[Projectile.type].Value;
            Vector2 origin = texture.Size() / 2f;

            Main.spriteBatch.Draw(texture, Projectile.Center - Main.screenPosition, null, Color.White, 
                Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0f);
            
            return false;
        }
    }
}