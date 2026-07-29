using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.Audio; // Diperlukan untuk SoundStyle

namespace TheSanity.Projectiles
{
    public class RainbowRocket : ModProjectile
    {
        public override string Texture => "TheSanity/Projectiles/RainbowRocket";

        // Deklarasi sound kustom ambatublow.mp3 dari folder Sounds
        public static readonly SoundStyle AmbatublowSound = new SoundStyle("TheSanity/Sounds/ambatublow");

        public override void SetStaticDefaults()
        {
            // --- PANDUAN VISUAL: PANJANG EKOR ROKET ---
            ProjectileID.Sets.TrailCacheLength[Projectile.type] = 25;
            ProjectileID.Sets.TrailingMode[Projectile.type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;       
            Projectile.height = 20;      
            Projectile.aiStyle = -1;     
            
            Projectile.hostile = true;   
            Projectile.friendly = false;
            Projectile.tileCollide = true; 
            
            // Set penetrasi ke 1 agar proyektil langsung hancur begitu menyentuh target
            Projectile.penetrate = 1; 
            
            Projectile.timeLeft = 600; // 10 detik = 600 frame
            Projectile.scale = 1.0f;     
        }

        // =========================================================================
        // BLOKIR TOTAL DAMAGE TABRAKAN FISIK BAWAAN TERRARIA
        // =========================================================================
        public override bool CanHitPlayer(Player target)
        {
            return false;
        }

        // =========================================================================
        // LOGIKA PERGERAKAN: AI HOMING AGRESIF & DETEKSI MELEDAK MANUAL
        // =========================================================================
        public override void AI()
        {
            if (Projectile.velocity != Vector2.Zero)
            {
                Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2; 
            }

            Player targetPlayer = null;
            float maxTrackingDistance = 1000f; 

            for (int i = 0; i < Main.maxPlayers; i++)
            {
                Player p = Main.player[i];
                if (p.active && !p.dead)
                {
                    float distance = Vector2.Distance(Projectile.Center, p.Center);
                    
                    // DETEKSI TABRAKAN MANUAL SEBAGAI PENGGANTI ONHITPLAYER
                    if (distance < 16f)
                    {
                        p.AddBuff(BuffID.WitheredWeapon, 600); // Berikan debuff langsung
                        Projectile.Kill();
                        return;
                    }

                    if (distance < maxTrackingDistance)
                    {
                        maxTrackingDistance = distance;
                        targetPlayer = p;
                    }
                }
            }

            if (targetPlayer != null)
            {
                Vector2 directionToTarget = targetPlayer.Center - Projectile.Center;
                directionToTarget.Normalize();

                // --- LOKASI BALANCING: KECEPATAN & KELINCAHAN ROKET ---
                float rocketSpeed = 7f;      
                float homingInertia = 30f;   

                Projectile.velocity = (Projectile.velocity * (homingInertia - 1f) + directionToTarget * rocketSpeed) / homingInertia;
            }

            float colorOffset = Main.GlobalTimeWrappedHourly * 3f;
            Color rgbLight = Main.hslToRgb(colorOffset % 1f, 1f, 0.6f);
            Lighting.AddLight(Projectile.Center, rgbLight.ToVector3() * 0.6f);
        }

        // =========================================================================
        // LOGIKA KEMATIAN: EFEK PARTIKEL SEAKAN-AKAN MELEDAK & DAMAGE VARIANCE ACAK
        // =========================================================================
        public override void OnKill(int timeLeft)
        {
            // 1. Trigger suara ledakan roket hancur bawaan game
            SoundEngine.PlaySound(SoundID.Item14, Projectile.Center);

            // 2. PARTIKEL SEAKAN-AKAN MELEDAK (KOSMETIK RAINBOW DUST SHATTER)
            for (int i = 0; i < 40; i++)
            {
                Vector2 dustVelocity = Main.rand.NextVector2Unit() * Main.rand.NextFloat(3f, 8f);
                int dustIndex = Dust.NewDust(Projectile.Center, 0, 0, DustID.RainbowMk2, dustVelocity.X, dustVelocity.Y, 100, default(Color), 1.8f);
                Main.dust[dustIndex].noGravity = true; 
            }

            // 3. DAMAGE AREA MANUAL DENGAN VARIANCE RANDOM & RARE SOUND
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                // --- LOKASI BALANCING: RADIUS LEDAKAN MANUAL (120 pixel) ---
                float explosionRadius = 120f; 

                int baseDamage = Projectile.damage; 

                if (baseDamage <= 25) 
                {
                    baseDamage = 25; 
                    if (Main.masterMode) baseDamage = (int)(baseDamage * 4f); 
                    else if (Main.expertMode) baseDamage = (int)(baseDamage * 2.5f);
                }

                for (int i = 0; i < Main.maxPlayers; i++)
                {
                    Player p = Main.player[i];
                    
                    if (p.active && !p.dead && Vector2.Distance(Projectile.Center, p.Center) < explosionRadius)
                    {
                        int hitDirection = (p.Center.X > Projectile.Center.X) ? 1 : -1;

                        // VARIANCE RANDOM DAMAGE AMBANG ±15%
                        float randomFactor = Main.rand.NextFloat(0.85f, 1.15f);
                        int finalRandomizedDamage = (int)(baseDamage * randomFactor);

                        // --- FIX UTAMA: 5% CHANCE RARE AUDIO ---
                        // Main.rand.Next(100) akan menghasilkan angka acak 0 sampai 99.
                        // Jika angka yang keluar kurang dari 5 (yaitu: 0, 1, 2, 3, 4), suara akan terputar (5% peluang).
                        if (Main.rand.Next(100) < 5)
                        {
                            SoundEngine.PlaySound(AmbatublowSound, p.Center);
                        }

                        // Eksekusi damage ke player lewat death reason kustom
                        p.Hurt(PlayerVisuals.GetPlayerHurtInfo(p, finalRandomizedDamage, hitDirection, false, 4f));
                    }
                }
            }
        }

        // =========================================================================
        // LOGIKA GAMBAR: MANIPULASI WARNA RGB PELANGI & TRAIL KOSMETIK
        // =========================================================================
        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
            Vector2 drawOrigin = new Vector2(texture.Width * 0.5f, texture.Height * 0.5f);

            // 1. DRAW EKOR TRAIL PELANGI
            for (int k = Projectile.oldPos.Length - 1; k > 0; k--)
            {
                if (Projectile.oldPos[k] == Vector2.Zero) continue;

                Vector2 trailDrawPos = Projectile.oldPos[k] + (Projectile.Size * 0.5f) - Main.screenPosition;
                float trailOpacity = 1f - ((float)k / Projectile.oldPos.Length);

                float trailColorOffset = (Main.GlobalTimeWrappedHourly * 3f) - (k * 0.04f);
                Color trailRainbow = Main.hslToRgb(trailColorOffset % 1f, 1f, 0.6f);
                
                Color finalTrailColor = trailRainbow * trailOpacity * 0.6f; 
                finalTrailColor.A = 0; 

                float trailScale = Projectile.scale * trailOpacity * 0.9f;

                Main.EntitySpriteDraw(
                    texture,
                    trailDrawPos,
                    null,
                    finalTrailColor,
                    Projectile.rotation, 
                    drawOrigin,
                    trailScale,
                    SpriteEffects.None,
                    0
                );
            }

            // 2. DRAW UTAMA KEPALA ROKET RGB
            Vector2 mainDrawPos = Projectile.Center - Main.screenPosition;
            
            float mainColorOffset = Main.GlobalTimeWrappedHourly * 3f;
            Color mainRainbow = Main.hslToRgb(mainColorOffset % 1f, 1f, 0.6f);
            
            Color finalMainColor = mainRainbow * 1.0f;
            finalMainColor.A = 0; 

            Main.EntitySpriteDraw(
                texture,
                mainDrawPos,
                null,
                finalMainColor,
                Projectile.rotation,
                drawOrigin,
                Projectile.scale,
                SpriteEffects.None,
                0
            );

            return false; 
        }
    }

    public static class PlayerVisuals
    {
        public static Player.HurtInfo GetPlayerHurtInfo(Player target, int damage, int hitDirection, bool pvp, float knockback)
        {
            Player.HurtInfo info = new Player.HurtInfo();
            info.Damage = damage;
            info.HitDirection = hitDirection;
            info.PvP = pvp;
            info.Knockback = knockback;
            info.DamageSource = PlayerDeathReason.ByCustomReason(target.name + " was Ambatublow by a Rainbow Rocket.");
            return info;
        }
    }
}