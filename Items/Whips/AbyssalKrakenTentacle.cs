using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using ReLogic.Content;
using TheSanity.CostumeRarity;

namespace TheSanity.Items.Whips
{
    // ==========================================
    // 1. KELAS ITEM (SENJATA)
    // ==========================================
    public class AbyssalKrakenTentacle : ModItem
    {
        public override void SetDefaults() {
            Item.DefaultToWhip(ModContent.ProjectileType<AbyssalKrakenTentacleProj>(), 40, 6f, 4f); 
            Item.rare = ModContent.RarityType<UnknownRarity>();
            Item.value = Item.sellPrice(0, 15, 0, 0);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(4679, 1)
                .AddIngredient(4911, 1)
                .AddIngredient(ItemID.SharkFin, 5)
                .AddIngredient(ItemID.FlaskofIchor, 20)
                .AddCondition(Condition.NearShimmer)
                .Register();
        }
    }

    // ==========================================
    // 2. KELAS PROYEKTIL CAMBUK UTAMA
    // ==========================================
    public class AbyssalKrakenTentacleProj : ModProjectile
    {
        public override void SetStaticDefaults() {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.DefaultToWhip();
            Projectile.WhipSettings.Segments = 20; 
            Projectile.WhipSettings.RangeMultiplier = 2.5f;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            if (Main.rand.NextBool(6)) {
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, Vector2.Zero, 
                    ModContent.ProjectileType<AbyssalMaw>(), Projectile.damage * 2, 5f, Projectile.owner);
            }

            for (int i = 0; i < 3; i++) {
                Vector2 launchVel = new Vector2(Main.rand.NextFloat(-5, 5), Main.rand.NextFloat(-5, 5));
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center, launchVel, 
                    ModContent.ProjectileType<MiniTentacleProj>(), (int)(Projectile.damage * 0.6f), 2f, Projectile.owner);
            }

            Main.player[Projectile.owner].MinionAttackTargetNPC = target.whoAmI;
        }

        public override bool PreDraw(ref Color lightColor) {
            List<Vector2> list = new List<Vector2>();
            Projectile.FillWhipControlPoints(Projectile, list);
            SpriteEffects flip = Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            
            string path = "TheSanity/Items/Whips/";
            Texture2D textureSegment = ModContent.Request<Texture2D>(path + "AbyssalKrakenTentacle_Segment").Value;
            Texture2D textureTip = ModContent.Request<Texture2D>(path + "AbyssalKrakenTentacle_Tip").Value;

            Vector2 pos = list[0];
            for (int i = 0; i < list.Count - 1; i++) {
                Texture2D textureToDraw = (i == list.Count - 2) ? textureTip : textureSegment;
                Vector2 element = list[i];
                Vector2 diff = list[i + 1] - element;
                float rotation = diff.ToRotation() - MathHelper.PiOver2; 
                Color drawColor = Color.White; 

                Main.EntitySpriteDraw(textureToDraw, pos - Main.screenPosition, textureToDraw.Frame(), drawColor, rotation, textureToDraw.Frame().Size() / 2, 1f, flip, 0);
                pos += diff;
            }
            return false;
        }
    }

    // ==========================================
    // 3. KELAS MINI TENTACLE (HOMING & VISIBLE)
    // ==========================================
    public class MiniTentacleProj : ModProjectile
    {
        public override void SetDefaults() {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.timeLeft = 200;
            Projectile.penetrate = 1;
            Projectile.tileCollide = false;
        }

        public override void AI() {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;

            if (Main.rand.NextBool(3)) {
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, DustID.Water, 0, 0, 100, Color.DeepSkyBlue, 0.8f);
                d.noGravity = true;
            }

            NPC target = null;
            float maxRange = 700f;
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC npc = Main.npc[i];
                if (npc.CanBeChasedBy()) {
                    float dist = Vector2.Distance(npc.Center, Projectile.Center);
                    if (dist < maxRange) {
                        maxRange = dist;
                        target = npc;
                    }
                }
            }

            if (target != null) {
                Vector2 desiredVel = (target.Center - Projectile.Center).SafeNormalize(Vector2.Zero) * 9f;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVel, 0.12f);
            }
        }

        // PAKSA GAMBAR TERLIHAT (Glow Effect)
        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, Projectile.height * 0.5f);
            
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }

    // ==========================================
    // 4. KELAS ABYSSAL MAW (VISIBLE)
    // ==========================================
    public class AbyssalMaw : ModProjectile
    {
        public override void SetDefaults() {
            Projectile.width = 60;
            Projectile.height = 60;
            Projectile.friendly = true;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 45;
            Projectile.DamageType = DamageClass.Summon;
        }

        public override void AI() {
            Projectile.alpha += 5;
            Projectile.scale += 0.02f;
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, Color.White * Projectile.Opacity, Projectile.rotation, texture.Size() / 2, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            target.AddBuff(BuffID.Ichor, 300);
        }
    }
}