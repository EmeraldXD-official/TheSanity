using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ModLoader;
using Terraria.ID;

namespace TheSanity.Projectiles
{
    public class DesertBlockProj : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_0";

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.timeLeft = 600;
            Projectile.penetrate = 3;
            Projectile.tileCollide = true;
            
        }

        public override void AI()
        {
            Projectile.rotation += 0.4f;
            Projectile.velocity.Y += 0.2f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            int itemID = (int)Projectile.ai[0];
            string texturePath = GetTexturePath(itemID);

            // Memuat tekstur dari folder aset manual
            Texture2D texture = ModContent.Request<Texture2D>("TheSanity/Items/DesertBlocks/" + texturePath).Value;

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                Color.White,
                Projectile.rotation,
                texture.Size() / 2,
                1f,
                SpriteEffects.None,
                0
            );

            return false;
        }

        private string GetTexturePath(int itemID)
        {
            // Memetakan ID Terraria ke nama file asetmu
            return itemID switch
            {
                ItemID.SandBlock => "SandBlock",
                ItemID.EbonsandBlock => "EbonsandBlock",
                ItemID.CrimsandBlock => "CrimsandBlock",
                ItemID.PearlsandBlock => "PearlsandBlock",
                // Tambahkan mapping untuk ID lainnya jika ada
                _ => "SandBlock" // Default ke SandBlock jika ID tidak terdaftar
            };
        }
    }
}