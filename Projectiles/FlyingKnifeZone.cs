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
    public class FlyingKnifeZone : ModProjectile
    {
        // Meminjam aset gambar Flying Knife asli dari vanilla Terraria
        public override string Texture => $"Terraria/Images/Projectile_{ProjectileID.FlyingKnife}";

        private float currentAuraRadius = 160f; // Start awal 10 block
        private Vector2 auraCenter;
        private bool isCenterInitialized = false;
        private int dashTimer = 0;

        public override void SetDefaults()
        {
            Projectile.width = 14;               
            Projectile.height = 14;
            Projectile.aiStyle = -1;             
            Projectile.friendly = false;         
            Projectile.hostile = true;           
            Projectile.penetrate = -1;           
            Projectile.tileCollide = false;      // Tembus block
            Projectile.ignoreWater = true;
            
            // --- LOKASI BALANCING: DURASI HIDUP PROYEKTIL ---
            Projectile.timeLeft = 600;           // 10 Detik aktif

            Projectile.scale = 1.3f;             
        }

        public override void AI()
        {
            if (!isCenterInitialized)
            {
                isCenterInitialized = true;
                auraCenter = Projectile.Center; // Mengunci lokasi spawn pertama sebagai pusat aura awal
            }

            // =========================================================================
            // 1. LOGIKA PELEBARAN AURA SECARA PERLAHAN (10 -> 30 BLOCK)
            // =========================================================================
            float maxAuraRadius = 480f; // 30 block
            
            if (currentAuraRadius < maxAuraRadius)
            {
                currentAuraRadius += 1.2f; 
            }

            Player targetPlayer = Main.player[Player.FindClosest(auraCenter, 1, 1)];
            float distancePlayerToAura = Vector2.Distance(auraCenter, targetPlayer.Center);

            // =========================================================================
            // 2. LOGIKA ROTASI PISAU (BERPUTAR SAAT DASH / PATROLI)
            // =========================================================================
            if (Projectile.ai[0] == 1 && Projectile.velocity != Vector2.Zero)
            {
                // ---------------------------------------------------------------------
                // MODE SERANG: Berputar kencang saat melakukan Dash
                // ---------------------------------------------------------------------
                Projectile.rotation += 0.6f; // Berputar kencang
            }
            else
            {
                // ---------------------------------------------------------------------
                // MODE PATROLI/KEMBALI: Menghadap Diagonal Proporsional
                // ---------------------------------------------------------------------
                // Cek kecepatan untuk menentukan arah hadap sprite diagonal
                if (Projectile.velocity != Vector2.Zero)
                {
                    if (Projectile.velocity.X > 0)
                    {
                        Projectile.rotation = MathHelper.PiOver4; // Menghadap Kanan Atas
                    }
                    else
                    {
                        Projectile.rotation = MathHelper.PiOver4 * 3f; // Menghadap Kiri Atas
                    }
                }
                else
                {
                    // Menghadap Diagonal biasa saat diam
                    Projectile.rotation = MathHelper.PiOver4;
                }
            }

            // =========================================================================
            // 3. KONDISI CEK: PLAYER MASUK ATAU KELUAR AURA
            // =========================================================================
            if (distancePlayerToAura <= currentAuraRadius && targetPlayer.active && !targetPlayer.dead)
            {
                // ---------------------------------------------------------------------
                // MODE SERANG: Player berada di dalam area jangkauan aura
                // ---------------------------------------------------------------------
                Projectile.ai[0] = 1; // Mengubah status AI ke Mode Serang
                dashTimer++;

                // --- LOKASI BALANCING: JEDA SERANGAN DASH ---
                int dashDelay = 45;

                if (dashTimer >= dashDelay)
                {
                    dashTimer = 0;
                    
                    Vector2 dashDirection = targetPlayer.Center - Projectile.Center;
                    dashDirection.Normalize();

                    // --- LOKASI BALANCING: KECEPATAN DASH PISAU ---
                    float dashSpeed = 18f; 
                    Projectile.velocity = dashDirection * dashSpeed;

                    SoundEngine.PlaySound(SoundID.Item1, Projectile.Center);
                }
                else
                {
                    // Efek friksi/gesekan perlahan (mengurangi kecepatan setelah hentakan dash)
                    Projectile.velocity *= 0.93f; 
                }
            }
            else
            {
                // ---------------------------------------------------------------------
                // MODE PATROLI / KEMBALI: Player di luar aura atau mati
                // ---------------------------------------------------------------------
                Projectile.ai[0] = 0; // Kembalikan ke mode patroli
                dashTimer = 0;

                Vector2 returnDirection = auraCenter - Projectile.Center;
                float distanceToCenter = returnDirection.Length();

                if (distanceToCenter > 10f)
                {
                    returnDirection.Normalize();
                    
                    // --- LOKASI BALANCING: KECEPATAN PISAU KEMBALI KE PUSAT ---
                    float returnSpeed = 8f; 
                    Projectile.velocity = returnDirection * returnSpeed;
                }
                else
                {
                    // Jika sudah sangat dekat dengan pusat, pisau mengunci posisinya tepat di tengah aura
                    Projectile.Center = auraCenter;
                    Projectile.velocity = Vector2.Zero;
                }

                // Perlahan ikut menggeser aura mendekati player secara lambat
                Vector2 moveTowardsPlayer = targetPlayer.Center - auraCenter;
                moveTowardsPlayer.Normalize();
                
                // --- LOKASI BALANCING: KECEPATAN PERGESERAN SELURUH ZONA AURA ---
                float auraMoveSpeed = 1.0f; 
                auraCenter += moveTowardsPlayer * auraMoveSpeed;
                
                // Saat mode patroli, posisi pisau otomatis ikut terseret mengikuti pusat aura
                if (Projectile.Center == auraCenter)
                {
                    Projectile.velocity = moveTowardsPlayer * auraMoveSpeed;
                }
            }

            // =========================================================================
            // BEAUTIFIER: EFEK VISUAL LINGKARAN AURA PEMBATAS
            // =========================================================================
            int dustAmount = 3;
            for (int i = 0; i < dustAmount; i++)
            {
                double angle = Main.rand.NextDouble() * Math.PI * 2;
                Vector2 dustOffset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * currentAuraRadius;
                
                Dust d = Dust.NewDustPerfect(auraCenter + dustOffset, DustID.MagicMirror, Vector2.Zero, 150, default, 1.2f);
                d.noGravity = true;
                
                if (Projectile.ai[0] == 0)
                {
                    d.velocity = targetPlayer.Center - auraCenter;
                    d.velocity.Normalize();
                    d.velocity *= 1.0f;
                }
            }

            // Partikel ekor pisau saat melakukan gerakan dash menyerang
            if (Projectile.ai[0] == 1 && Main.rand.NextBool(2))
            {
                Dust d = Dust.NewDustPerfect(Projectile.Center, DustID.MagicMirror, -Projectile.velocity * 0.2f, 100, default, 1.0f);
                d.noGravity = true;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            // Fix Visual Flip: Diagonal OK, rotasi berputar OK
            Main.EntitySpriteDraw(texture, Projectile.Center - Main.screenPosition, null, lightColor, Projectile.rotation, drawOrigin, Projectile.scale, SpriteEffects.None, 0);
            return false;
        }
    }
}