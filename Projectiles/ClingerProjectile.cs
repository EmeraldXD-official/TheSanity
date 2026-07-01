using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Projectiles
{
    // =========================================================================
    // PART 1: MENCEGAT ITEM CLINGER STAFF VANILLA AGAR MEMANGGIL PROYEKTIL KUSTOM
    // =========================================================================
    public class ClingerStaffItemRework : GlobalItem
    {
        public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        {
            // Memastikan modifikasi ini hanya berlaku pada Item Clinger Staff asli
            return entity.type == ItemID.ClingerStaff;
        }

        public override bool Shoot(Item item, Player player, Terraria.DataStructures.EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            // Mengganti proyektil vanilla (ClingerStaffWall) menjadi Proyektil Kustom kita sendiri (ClingerStaffVisualRework)
            int customProjectile = ModContent.ProjectileType<ClingerStaffVisualRework>();
            
            // Memunculkan proyektil kustom tepat di posisi kursor/target yang ditunjuk player
            Projectile.NewProjectile(source, Main.MouseWorld, Vector2.Zero, customProjectile, damage, knockback, player.whoAmI);
            
            return false; // Mengembalikan false agar proyektil asli vanilla TIDAK IKUT MUNCUL
        }
    }

    // =========================================================================
    // PART 2: PROYEKTIL KUSTOM YANG MEMINJAM SPRITE INTERNAL TERRARIA
    // =========================================================================
    public class ClingerStaffVisualRework : ModProjectile
    {
        // --- TRIK MEMINJAM SPRITE INTERNAL TERRARIA ---
        // Kita meminjam sprite internal milik dinding api terkutuk bawaan vanilla Terraria
        public override string Texture => $"Terraria/Images/Item_{ItemID.ClingerStaff}";

        public override void SetDefaults()
        {
            Projectile.width = 32;
            Projectile.height = 32;
            Projectile.aiStyle = -1; // Menggunakan kendali AI kustom kita sepenuhnya
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1; // -1 berarti proyektil tidak bisa hancur karena menabrak musuh
            Projectile.tileCollide = false; // Tembus dinding agar penempatannya rapi di tanah
            Projectile.ignoreWater = true;
        }

        public override void AI()
        {
            // Menggunakan ai[0] sebagai timer internal
            Projectile.ai[0]++;

            // 3 detik = 180 frame (Terraria berjalan di 60 FPS)
            float delayBeforeAttack = 180f;
            
            // Durasi serangan 2 detik = 120 frame, jadi total waktu aktif adalah 180 + 120 = 300 frame
            float attackEndDuration = delayBeforeAttack + 120f;

            // ========================================================
            // FASE: TEMBAK EYEFIRE (Setelah 3 Detik, Aktif Selama 2 Detik)
            // ========================================================
            if (Projectile.ai[0] >= delayBeforeAttack && Projectile.ai[0] < attackEndDuration)
            {
                // Menembakkan proyektil setiap 6 frame sekali (~10x per detik) agar semburan apinya rapat
                if (Projectile.ai[0] % 6 == 0)
                {
                    SoundEngine.PlaySound(SoundID.Item34, Projectile.Center); // Efek suara semburan api Cursed Flamethrower

                    if (Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        // --- LOKASI BALANCING: DAMAGE & SPEED EYEFIRE ---
                        int eyeFireDamage = 55; // Ganti angka ini untuk mengubah damage semburan api
                        float fireSpeed = 9f;   // Ganti angka ini untuk mengubah kecepatan luncur semburan api

                        // Menentukan arah gerakan proyektil: Ke atas (0, -1) dan Ke bawah (0, 1)
                        Vector2 velocityUp = new Vector2(0f, -fireSpeed);
                        Vector2 velocityDown = new Vector2(0f, fireSpeed);

                        // Menambahkan sedikit efek acak horizontal agar semburan tidak terlalu kaku dan lurus
                        velocityUp.X += Main.rand.NextFloat(-0.8f, 0.8f);
                        velocityDown.X += Main.rand.NextFloat(-0.8f, 0.8f);

                        // Spawn proyektil EyeFire internal ke arah ATAS
                        Projectile.NewProjectile(
                            Projectile.GetSource_FromAI(),
                            Projectile.Center,
                            velocityUp,
                            ProjectileID.EyeFire, 
                            eyeFireDamage,
                            Projectile.knockBack,
                            Projectile.owner
                        );

                        // Spawn proyektil EyeFire internal ke arah BAWAH
                        Projectile.NewProjectile(
                            Projectile.GetSource_FromAI(),
                            Projectile.Center,
                            velocityDown,
                            ProjectileID.EyeFire,
                            eyeFireDamage,
                            Projectile.knockBack,
                            Projectile.owner
                        );
                    }
                }

                // --- BEAUTIFIER (EFEK PARTIKEL) ---
                // Mengeluarkan partikel api hijau terkutuk saat sedang menembak aktif
                if (Main.rand.NextBool(2))
                {
                    Dust d = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 20f), DustID.CursedTorch, Vector2.Zero, 100, default, 1.4f);
                    d.noGravity = true;
                    d.velocity *= 0.3f;
                }
            }

            // ========================================================
            // FASE: HILANG (Setelah Total Waktu Selesai)
            // ========================================================
            if (Projectile.ai[0] >= attackEndDuration)
            {
                Projectile.Kill(); // Hancurkan proyektil secara permanen
            }
        }

        public override void OnKill(int timeLeft)
        {
            // Efek visual partikel hancur saat proyektil menghilang/mati
            for (int i = 0; i < 20; i++)
            {
                Vector2 dustVel = Main.rand.NextVector2Circular(5f, 5f);
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.CursedTorch, dustVel, 80, default, 1.3f);
                d.noGravity = true;
            }
        }

        // Menggambar sprite internal yang dipinjam secara presisi tepat di titik koordinat proyektil
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            Main.EntitySpriteDraw(
                texture,
                Projectile.Center - Main.screenPosition,
                null,
                lightColor,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );
            return false;
        }
    }
}