using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.GlobalNPC.Enemy
{
    // =========================================================================
    // PROJECTILE 1: THE SPINNING MAIN WEAPON (SABIT UTAMA BERPUTAR)
    // =========================================================================
    public class DeathSickleWeapon : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_1327";

        private bool isSpecialAttack = false;
        private int attackTimer = 0;
        private bool initialized = false;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;     
        }

        public override void SetDefaults()
        {
            Projectile.width = 48;
            Projectile.height = 48;
            Projectile.hostile = true;     
            Projectile.friendly = false;
            Projectile.tileCollide = false; 
            Projectile.scale = 0.9f;       
            Projectile.aiStyle = -1;       
        }

        public override void AI()
        {
            if (!initialized)
            {
                // [BALANCING LOCATION 1: CHANCE SPECIAL SKILL (10% CHANCE)]
                if (Main.rand.NextFloat() < 0.10f)
                {
                    isSpecialAttack = true;
                    Projectile.timeLeft = 90; 
                }
                else
                {
                    isSpecialAttack = false;
                    Projectile.timeLeft = 60; 
                }
                initialized = true;
            }

            attackTimer++;

            // [BALANCING LOCATION 2: KECEPATAN ROTASI SWING SABIT UTAMA]
            float spinSpeed = 0.25f;

            if (!isSpecialAttack)
            {
                // JALUR 1: BASIC ATTACK (Bolak-balik atas bawah)
                if (attackTimer <= 30)
                {
                    Projectile.rotation += spinSpeed; 
                    if (attackTimer == 15) FireSickleProjectile(); 
                }
                else
                {
                    Projectile.rotation -= spinSpeed; 
                    if (attackTimer == 45) FireSickleProjectile(); 
                }
            }
            else
            {
                // JALUR 2: SPECIAL SKILL (Nova Ring menyebar 360 derajat)
                if (attackTimer <= 45)
                {
                    Projectile.rotation += spinSpeed * 1.5f; 
                }
                else
                {
                    Projectile.rotation -= spinSpeed * 1.5f; 
                }

                // FIXED: Membagi gelombang tembakan dan menyuntikkan offset sudut ke gelombang ke-2
                if (attackTimer == 30) 
                {
                    FireSpecialRing(0f); // Gelombang 1: Sudut normal murni
                }
                if (attackTimer == 60) 
                {
                    // =========================================================================
                    // [BALANCING LOCATION 3: OFFSET SUDUT GELOMBANG KEDUA (15 DERAJAT)]
                    // - Mengubah MathHelper.ToRadians(15f) ke angka lain jika ingin menggeser celah aman lebih ekstrem.
                    // =========================================================================
                    FireSpecialRing(MathHelper.ToRadians(15f)); // Gelombang 2: Bergeser silang menutup celah
                }
            }

            if (Main.rand.NextBool(4))
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Shadowflame, 0f, 0f, 100, default, 1.0f);
            }
        }

        // MEKANIK: TEMBAKAN BIASA & SERANGAN SPREAD KIPAS
        private void FireSickleProjectile()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            int targetIndex = Player.FindClosest(Projectile.Center, 1, 1);
            if (targetIndex == -1) return;
            Player player = Main.player[targetIndex];

            Vector2 launchDirection = player.Center - Projectile.Center;
            launchDirection.Normalize();

            SoundEngine.PlaySound(SoundID.Item71, Projectile.Center);

            // =========================================================================
            // [BALANCING LOCATION 4: DAMAGE & SPEED JALUR SERANGAN BIASA]
            // - Base 35 -> Master Mode = 105 Damage!
            // =========================================================================
            int basicDamage = 35; 
            float basicSpeed = 13f;     

            // =========================================================================
            // [BALANCING LOCATION 5: DAMAGE & SPEED JALUR 25% CHANCE SPREAD ATTACK]
            // - Base 70 -> Master Mode = 210 Damage!
            // =========================================================================
            int spreadDamage = 70;
            float spreadSpeed = 14f;

            if (Main.rand.NextFloat() < 0.25f) 
            {
                float spreadAngle = MathHelper.ToRadians(20f); 
                for (int i = -1; i <= 1; i++)
                {
                    Vector2 spreadVelocity = launchDirection.RotatedBy(i * spreadAngle) * spreadSpeed;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, spreadVelocity, ModContent.ProjectileType<HostileDeathSickle>(), spreadDamage, 1f, Main.myPlayer);
                }
            }
            else
            {
                Vector2 normalVelocity = launchDirection * basicSpeed;
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, normalVelocity, ModContent.ProjectileType<HostileDeathSickle>(), basicDamage, 1f, Main.myPlayer);
            }
        }

        // =========================================================================
        // MEKANIK: SPECIAL SKILL RING RADIAL DENGAN PARAMETER OFFSET SUDUT
        // =========================================================================
        private void FireSpecialRing(float angleOffset)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;

            SoundEngine.PlaySound(SoundID.Item71, Projectile.Center);

            // =========================================================================
            // [BALANCING LOCATION 6: STATS DAMAGE & SPACING SPECIAL SKILL NOVA]
            // - Base 35 -> Master Mode = 105 Damage!
            // =========================================================================
            int specialDamage = 35; 
            float specialSpeed = 8f;     
            int projectileCount = 12;   

            for (int i = 0; i < projectileCount; i++)
            {
                // FIXED: Menambahkan parameter angleOffset ke dalam kalkulasi putaran lingkaran peluru
                float angle = (MathHelper.TwoPi / projectileCount * i) + angleOffset;
                Vector2 launchVelocity = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * specialSpeed;

                Projectile.NewProjectile(
                    Projectile.GetSource_FromThis(), 
                    Projectile.Center, 
                    launchVelocity, 
                    ModContent.ProjectileType<HostileDeathSickle>(), 
                    specialDamage, 
                    1f, 
                    Main.myPlayer
                );
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(0f, texture.Height); 

            for (int k = Projectile.oldPos.Length - 1; k > 0; k--)
            {
                Vector2 drawPos = Projectile.oldPos[k] + Projectile.Size / 2f - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);
                Color shadowColor = new Color(130, 10, 230, 0) * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length);
                Main.EntitySpriteDraw(texture, drawPos, null, shadowColor, Projectile.oldRot[k], drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(texture, mainDrawPos, null, Projectile.GetAlpha(lightColor), Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);

            return false; 
        }
    }

    // =========================================================================
    // PROJECTILE 2: THE LAUNCHED SICKLE (PELURU TERBANG)
    // =========================================================================
    public class HostileDeathSickle : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_274";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 5; 
            ProjectileID.Sets.TrailingMode[Projectile.type] = 2;     
        }

        public override void SetDefaults()
        {
            Projectile.width = 34;
            Projectile.height = 34;
            Projectile.hostile = true;     
            Projectile.friendly = false;
            Projectile.tileCollide = false; 

            // [BALANCING LOCATION 7: DURASI JELAJAH PELURU DI UDARA]
            Projectile.timeLeft = 300;     
            Projectile.aiStyle = -1;       
        }

        public override void AI()
        {
            Projectile.rotation += 0.35f;

            if (Main.rand.NextBool(4))
            {
                Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.DemonTorch, 0f, 0f, 120, default, 0.9f);
            }
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            // [BALANCING LOCATION 8: DURASI DEBUFF SHADOWFLAME PADA PLAYER]
            int debuffDuration = 180;
            target.AddBuff(BuffID.ShadowFlame, debuffDuration);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width / 2f, texture.Height / 2f);

            for (int k = Projectile.oldPos.Length - 1; k > 0; k--)
            {
                Vector2 drawPos = Projectile.oldPos[k] + Projectile.Size / 2f - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);
                Color shadowColor = new Color(160, 30, 255, 0) * ((Projectile.oldPos.Length - k) / (float)Projectile.oldPos.Length) * 0.6f;
                Main.EntitySpriteDraw(texture, drawPos, null, shadowColor, Projectile.oldRot[k], drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            }

            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            Main.EntitySpriteDraw(texture, mainDrawPos, null, Projectile.GetAlpha(lightColor), Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);

            return false;
        }
    }
}