using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    public class GemShardProjectile : ModProjectile
    {
        // Parameter AI shorthand untuk mempermudah pembacaan kode
        public float HomingTimer { get => Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int GemIndex { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }

        public override string Texture => "TheSanity/GlobalNPC/Bosses/GemGuardian/EmeraldBoss"; // Jalur fallback default

        public override void SetStaticDefaults()
        {
            // Mendaftarkan penyimpanan posisi lama untuk rendering After-Image Shadow
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 6;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 360;
            Projectile.extraUpdates = 1; // Membuat pergerakan terkesan lebih halus berkecepatan tinggi
        }

        public override void AI()
        {
            // Proyektil berputar estetik saat meluncur bebas
            Projectile.rotation += 0.08f;

            // Efek partikel sangat minim agar tidak memicu limit display engine Terraria
            if (Main.rand.NextBool(8))
            {
                int dustType = GetDustTypeByGem(GemIndex);
                Dust d = Dust.NewDustDirect(Projectile.position, Projectile.width, Projectile.height, dustType, 0f, 0f, 150, default, 0.8f);
                d.noGravity = true;
                d.velocity *= 0.1f;
            }

            // Logika homing tertunda bawaan (Digunakan otomatis oleh Emerald dan Topaz)
            if (HomingTimer > 0)
            {
                HomingTimer--;
            }
            else
            {
                Player target = Main.player[Player.FindClosest(Projectile.Center, 1, 1)];
                if (target != null && target.active && !target.dead)
                {
                    // Hanya Emerald (0) dan Topaz (6) yang melakukan pengejaran homing kustom aktif
                    if (GemIndex == 0 || GemIndex == 6)
                    {
                        // [BALANCE: PROJECTILE HOMING STRENGTH & SPEED LOCATION]
                        float homingStrength = 0.06f;
                        float maxSpeed = 8f;
                        
                        Vector2 desiredVelocity = (target.Center - Projectile.Center);
                        desiredVelocity.Normalize();
                        desiredVelocity *= maxSpeed;

                        Projectile.velocity = Vector2.Lerp(Projectile.velocity, desiredVelocity, homingStrength);
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            // Mengambil aset tekstur Boss asli secara dinamis berdasarkan indeks giliran aktif
            string texturePath = GetTexturePathByGem(GemIndex);
            Texture2D texture = ModContent.Request<Texture2D>(texturePath).Value;
            
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            Color gemColor = GetColorByGem(GemIndex);

            // [BALANCE: PROJECTILE VISUAL SCALE LOCATION]
            float customScale = 0.80f; // Diperkecil menjadi 25% dari ukuran asli boss

            // 1. AFTER-IMAGE SHADOW EFFECT (Memanfaatkan data koordinat posisi lama proyektil)
            for (int i = 1; i < Projectile.oldPos.Length; i++)
            {
                if (Projectile.oldPos[i] == Vector2.Zero) continue;
                Vector2 oldDrawPos = Projectile.oldPos[i] + new Vector2(Projectile.width / 2, Projectile.height / 2) - Main.screenPosition;
                Color shadowColor = gemColor * 0.18f * ((float)(Projectile.oldPos.Length - i) / Projectile.oldPos.Length);
                Main.EntitySpriteDraw(texture, oldDrawPos, null, shadowColor, Projectile.rotation, drawOrigin, customScale, SpriteEffects.None, 0);
            }

            // 2. SHINING OUTLINE EFFECT (Metode menggambar duplikat bergeser ke 4 penjuru arah luar)
            Color outlineColor = gemColor * 0.5f;
            float outlineThickness = 1.5f;
            for (int k = 0; k < 4; k++)
            {
                Vector2 offset = new Vector2(outlineThickness, 0f).RotatedBy(k * MathHelper.PiOver2);
                Main.EntitySpriteDraw(texture, drawPos + offset, null, outlineColor, Projectile.rotation, drawOrigin, customScale, SpriteEffects.None, 0);
            }

            // 3. GLOW IN THE DARK EFFECT (Menggunakan warna putih murni agar kebal dari kegelapan malam/gua)
            Main.EntitySpriteDraw(texture, drawPos, null, Color.White, Projectile.rotation, drawOrigin, customScale, SpriteEffects.None, 0);

            return false; // Matikan gambar bawaan agar visual kustom kita bekerja sempurna
        }

        public static Color GetColorByGem(int index)
        {
            return index switch
            {
                0 => Color.LimeGreen,
                1 => Color.LightCyan,
                2 => Color.Red,
                3 => Color.Purple,
                4 => Color.DeepSkyBlue,
                5 => Color.Orange,
                6 => Color.Gold,
                _ => Color.White
            };
        }

        private int GetDustTypeByGem(int index)
        {
            return index switch
            {
                0 => DustID.GemEmerald,
                1 => DustID.GemDiamond,
                2 => DustID.GemRuby,
                3 => DustID.GemAmethyst,
                4 => DustID.GemSapphire,
                5 => DustID.GemAmber,
                6 => DustID.GemTopaz,
                _ => DustID.WhiteTorch
            };
        }

        private string GetTexturePathByGem(int index)
        {
            return index switch
            {
                0 => "TheSanity/GlobalNPC/Bosses/GemGuardian/EmeraldBoss",
                1 => "TheSanity/GlobalNPC/Bosses/GemGuardian/DiamondBoss",
                2 => "TheSanity/GlobalNPC/Bosses/GemGuardian/RubyBoss",
                3 => "TheSanity/GlobalNPC/Bosses/GemGuardian/AmethystBoss",
                4 => "TheSanity/GlobalNPC/Bosses/GemGuardian/SapphireBoss",
                5 => "TheSanity/GlobalNPC/Bosses/GemGuardian/AmberBoss",
                6 => "TheSanity/GlobalNPC/Bosses/GemGuardian/TopazBoss",
                _ => "TheSanity/GlobalNPC/Bosses/GemGuardian/EmeraldBoss"
            };
        }
    }
}