using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using ReLogic.Content;
using TheSanity.Buff;
using TheSanity.Projectiles;

namespace TheSanity.Items.Whips
{
    public class FeatherWireWhip : ModItem
    {
        public override void SetDefaults() {
            // Damage 24, Knockback 2, Ukuran 4
            Item.DefaultToWhip(ModContent.ProjectileType<FeatherWireWhipProj>(), 24, 2f, 5f); 
            Item.rare = ItemRarityID.Green;
            Item.value = Item.sellPrice(0, 1, 50, 0);
        }

        public override void AddRecipes() {
            CreateRecipe()
                .AddIngredient(ItemID.Feather, 10)
                .AddIngredient(ItemID.ShadowScale,6)
                .AddIngredient(ItemID.BlandWhip,1)
                .AddTile(TileID.Anvils)
                .Register();
        }
    }

    public class FeatherWireWhipProj : ModProjectile
    {
        public override void SetStaticDefaults() {
            ProjectileID.Sets.IsAWhip[Type] = true;
        }

        public override void SetDefaults() {
            Projectile.DefaultToWhip();
            Projectile.WhipSettings.Segments = 14; 
            Projectile.WhipSettings.RangeMultiplier = 1f;
        }

      public override bool PreDraw(ref Color lightColor) {
    List<Vector2> list = new List<Vector2>();
    Projectile.FillWhipControlPoints(Projectile, list);

    SpriteEffects flip = Projectile.spriteDirection < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
    
    string path = "TheSanity/Items/Whips/";
    
    Asset<Texture2D> seqAsset = ModContent.Request<Texture2D>(path + "FeatherWireWhip_Segment");
    Asset<Texture2D> tipAsset = ModContent.Request<Texture2D>(path + "FeatherWireWhip_Tip");

    if (!seqAsset.IsLoaded || !tipAsset.IsLoaded) return false;

    Texture2D textureSegment = seqAsset.Value;
    Texture2D textureTip = tipAsset.Value;

    Vector2 pos = list[0];

    for (int i = 0; i < list.Count - 1; i++) {
        Texture2D textureToDraw = (i == list.Count - 2) ? textureTip : textureSegment;
        
        Rectangle frame = textureToDraw.Frame();
        Vector2 origin = frame.Size() / 2;

        Vector2 element = list[i];
        Vector2 diff = list[i + 1] - element;

        float rotation = diff.ToRotation() - MathHelper.PiOver2; 
        
        // PERBAIKAN WARNA: Gunakan Lighting.GetColor untuk mendapatkan warna lingkungan
        // Atau gunakan Color.White jika ingin warna sprite asli keluar 100% tanpa bayangan
        Color drawColor = Color.White; 

        Main.EntitySpriteDraw(textureToDraw, pos - Main.screenPosition, frame, drawColor, rotation, origin, 1f, flip, 0);

        pos += diff;
    }
    return false;
}

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) {
            Player player = Main.player[Projectile.owner];
            
            // Memberikan buff FeatherFrenzy (+10% Speed)
            player.AddBuff(ModContent.BuffType<FeatherFrenzy>(), 240);
            
            // Fokuskan minion ke musuh yang dipukul
            player.MinionAttackTargetNPC = target.whoAmI;
            
            // Efek partikel saat kena
            for (int i = 0; i < 5; i++) {
                Dust.NewDust(target.position, target.width, target.height, DustID.Electric);
            }
        }
    }
}