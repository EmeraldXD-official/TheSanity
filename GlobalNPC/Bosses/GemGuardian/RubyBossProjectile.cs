using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff;

namespace TheSanity.GlobalNPC.Bosses.GemGuardian
{
    public class RubyBossProjectile : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.Ruby;

        public bool IsHoming => Projectile.ai[0] == 1f;
        private int trackingTimer = 0;

        public override void SetStaticDefaults() {
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.TrailCacheLength[Type] = 15; 
        }

        public override void SetDefaults() {
            Projectile.width = 14;
            Projectile.height = 14;
            Projectile.hostile = true;   
            Projectile.friendly = false; 
            Projectile.penetrate = 1;
            Projectile.tileCollide = false; 
            Projectile.ignoreWater = true;
            Projectile.timeLeft = 360; 
        }

        public override void AI() {
            Projectile.rotation += 0.18f;

            if (IsHoming) {
                trackingTimer++;
                if (trackingTimer > 10) { 
                    Player player = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
                    Vector2 targetDir = player.Center - Projectile.Center;
                    targetDir.Normalize();

                    float turnSpeed = 0.05f; 
                    float moveSpeed = 5.0f;  

                    Projectile.velocity = Vector2.Normalize(Vector2.Lerp(Projectile.velocity, targetDir * moveSpeed, turnSpeed)) * moveSpeed;
                }
            }

            if (Main.rand.NextBool(4)) {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.GemRuby, 0f, 0f, 150, default, 0.9f);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) {
            // Sesuai request tetap menghasilkan debuff EmeraldSpell jika terkena player (kecuali contact damage)
            int buffType = ModContent.BuffType<EmeraldSpell>();
            int buffIndex = target.FindBuffIndex(buffType);

            if (buffIndex != -1) {
                target.buffTime[buffIndex] += 60; 
            } else {
                target.AddBuff(buffType, 300); 
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = texture.Size() / 2f;
            Color glowColor = new Color(255, 30, 30, 200); 

            for (int i = 0; i < Projectile.oldPos.Length; i++) {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                Vector2 trailDrawPos = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;
                Color trailColor = glowColor * ((Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length) * 0.35f;

                Main.spriteBatch.Draw(texture, trailDrawPos, null, trailColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0f);
            }

            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            Main.spriteBatch.Draw(texture, mainDrawPos, null, Color.White, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0f);

            return false; 
        }
    }
}