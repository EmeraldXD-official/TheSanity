using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.GameContent; 
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Buff; // PENTING: Memanggil namespace folder Buff tempat debuff barumu berada

namespace TheSanity.Projectiles
{
    public class HostileBrokenHeroSword : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.BrokenHeroSword;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2; 
        }

        public override void SetDefaults()
        {
            Projectile.width = 30;
            Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.hostile = true; 
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 3600;
        }

        public override void AI()
        {
            Projectile.ai[0] += 0.05f; 
            
            // -------------------------------------------------------------------------
            // LOKASI BALANCING: DAMAGE PROYEKTIL (Di-set ke 100 untuk Master Mode)
            // -------------------------------------------------------------------------
            // Mengubah nilai asal 500 menjadi 100 sesuai request-mu untuk balancing.
            // Nilai ini dikunci di sini agar konisten murni sebesar 100 sebelum dipotong defense.
            Projectile.damage = 30; 
            
            // -------------------------------------------------------------------------
            // LOKASI BALANCING VISUAL: KECEPATAN PUTARAN PEDANG SAAT DILEMPAR
            // -------------------------------------------------------------------------
            Projectile.rotation += 0.15f; 
        }

        public override bool CanHitPlayer(Player target)
        {
            return true;
        }

        // =========================================================================
        // MEKANIK BARU: SENSOR SAAT PROYEKTIL MENGENAI PLAYER (MEMICU DEBUFF RESMI)
        // =========================================================================
        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // -------------------------------------------------------------------------
            // LOKASI BALANCING DURASI: DEBUFF LOCKOUT MELEE (30 DETIK)
            // -------------------------------------------------------------------------
            // Game Terraria berjalan di 60 frame per detik.
            // 30 detik x 60 frame = 1800 frame.
            // -> Ubah angka '1800' di bawah jika ingin durasi ban senjata Melee lebih cepat/lama.
            // -------------------------------------------------------------------------
            int lockoutDuration = 1800; 

            // FIX: Sekarang langsung memberikan debuff resmi MeleeLockout ke player.
            // Ini otomatis memicu pemblokiran senjata di MeleeLockoutPlayer dan menampilkan icon PNG-mu!
            target.AddBuff(ModContent.BuffType<MeleeLockout>(), lockoutDuration);

            // Berikan indikator teks visual melayang di atas player agar mereka tidak bingung
            CombatText.NewText(target.getRect(), Color.Red, "Melee Disabled!", true, false);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width / 2, texture.Height / 2);
            Vector2 drawPos = Projectile.Center - Main.screenPosition + new Vector2(0f, MathF.Sin(Projectile.ai[0]) * 4f);

            for (int k = 0; k < Projectile.oldPos.Length; k++)
            {
                Vector2 trailPos = Projectile.oldPos[k] + drawOrigin - Main.screenPosition + new Vector2(0f, MathF.Sin(Projectile.ai[0]) * 4f);
                Color trailColor = new Color(139, 69, 19) * 0.5f * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length); 
                Main.EntitySpriteDraw(texture, trailPos, null, trailColor, Projectile.rotation + MathHelper.ToRadians(45f), drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Main.EntitySpriteDraw(texture, drawPos, null, lightColor, Projectile.rotation + MathHelper.ToRadians(45f), drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}