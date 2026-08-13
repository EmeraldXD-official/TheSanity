using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.OreBar.Ambarium;

namespace TheSanity.Items.Hook
{
    // ==========================================
    // 1. CLASS ITEM HOOK
    // ==========================================
    public class AmbariumHook : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 1;
        }

        public override void SetDefaults()
        {
            Item.CloneDefaults(ItemID.AmethystHook);

            Item.shoot = ModContent.ProjectileType<AmbahookTip>(); 
            Item.shootSpeed = 20f; // ⚡ DIPERCEPAT: Tembakan kait meluncur lebih kencang
            Item.value = Item.sellPrice(0, 0, 35, 0);
            Item.rare = ItemRarityID.Blue;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<AmbariumBar>(12)
                .AddIngredient(1273,1)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    // ==========================================
    // 2. CLASS PROYEKTIL KEPALA & EFEK VISUAL
    // ==========================================
    public class AmbahookTip : ModProjectile
    {
        public override string Texture => "TheSanity/Items/Hook/AmbahookTip";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.SingleGrappleHook[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.CloneDefaults(ProjectileID.GemHookAmethyst);
        }

        // 🎯 1. JARAK MAKSIMUM (620f = ~39 Blok, jauh lebih panjang dari sebelumnya)
        public override float GrappleRange() => 620f;

        public override void NumGrappleHooks(Player player, ref int numHooks)
        {
            numHooks = 1;
        }

        // 🎯 2. KECEPATAN RETRACT (Saat meleset kembali ke pemain)
        public override void GrappleRetreatSpeed(Player player, ref float speed)
        {
            speed = 22f;
        }

        // 🎯 3. KECEPATAN TARIK PLAYER (19f = Sangat cepat menarik pemain ke dinding)
        public override void GrapplePullSpeed(Player player, ref float speed)
        {
            speed = 19f;
        }

        // ==========================================
        // 🔮 LOGIKA EFEK VISUAL & PARTIKEL (AI)
        // ==========================================
        public override void AI()
        {
            // Cahaya ungu di ujung hook
            Lighting.AddLight(Projectile.Center, 0.67f * 0.8f, 0.10f * 0.8f, 0.96f * 0.8f);

            // Jejak partikel saat meluncur
            if (Projectile.ai[0] == 0f && Main.rand.NextBool(2))
            {
                Dust dust = Dust.NewDustDirect(
                    Projectile.position, 
                    Projectile.width, 
                    Projectile.height, 
                    DustID.PurpleCrystalShard, 
                    Projectile.velocity.X * 0.2f, 
                    Projectile.velocity.Y * 0.2f, 
                    100, 
                    default, 
                    1.2f
                );
                dust.noGravity = true;
                dust.velocity *= 0.3f;
            }

            // Aura saat menancap di dinding
            if (Projectile.ai[0] == 2f && Main.rand.NextBool(3))
            {
                Dust dust = Dust.NewDustDirect(
                    Projectile.Center - new Vector2(8, 8), 
                    16, 
                    16, 
                    DustID.Shadowflame, 
                    Main.rand.NextFloat(-1f, 1f), 
                    Main.rand.NextFloat(-1f, 1f), 
                    150, 
                    default, 
                    0.9f
                );
                dust.noGravity = true;
            }
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            for (int i = 0; i < 10; i++)
            {
                Vector2 speed = Main.rand.NextVector2Circular(3f, 3f);
                Dust dust = Dust.NewDustDirect(
                    Projectile.Center, 
                    0, 
                    0, 
                    DustID.PurpleCrystalShard, 
                    speed.X, 
                    speed.Y, 
                    50, 
                    default, 
                    1.3f
                );
                dust.noGravity = true;
            }

            return base.OnTileCollide(oldVelocity);
        }

        // ==========================================
        // 🔗 LOGIKA MENGGAMBAR RANTAI
        // ==========================================
        public override bool PreDrawExtras()
        {
            Texture2D chainTexture = ModContent.Request<Texture2D>("TheSanity/Items/Hook/AmbahookChain").Value;

            Vector2 playerCenter = Main.player[Projectile.owner].MountedCenter;
            Vector2 position = Projectile.Center;
            Vector2 directionToPlayer = playerCenter - position;

            float rotation = directionToPlayer.ToRotation() - MathHelper.PiOver2;
            float distance = directionToPlayer.Length();

            while (distance > chainTexture.Height && !float.IsNaN(distance))
            {
                position += Vector2.Normalize(directionToPlayer) * chainTexture.Height;
                directionToPlayer = playerCenter - position;
                distance = directionToPlayer.Length();

                Lighting.AddLight(position, 0.3f, 0.05f, 0.4f);

                Color drawColor = Lighting.GetColor((int)(position.X / 16f), (int)(position.Y / 16f));

                Main.EntitySpriteDraw(
                    chainTexture,
                    position - Main.screenPosition,
                    null,
                    drawColor,
                    rotation,
                    chainTexture.Size() * 0.5f,
                    1f,
                    SpriteEffects.None,
                    0
                );
            }

            return true;
        }
    }
}