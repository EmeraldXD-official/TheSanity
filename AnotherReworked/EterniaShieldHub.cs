using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity
{
    public class EterniaShieldHub : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.CultistRitual;

        public int MaxBlockCount { get => (int)Projectile.ai[0]; set => Projectile.ai[0] = value; }
        public int CurrentBlockCount { get => (int)Projectile.ai[1]; set => Projectile.ai[1] = value; }

        public float orbitRadius = 0f;          
        public float targetOrbitRadius = 128f; 
        public float rotationSpeed = 0.02f;       
        public float projectileHitboxTolerance = 12f; 

        public float currentRotation = 0f;
        public int cooldownTimer = 0;
        public bool isCooldown = false;
        
        private float visualRadius = 0f; 
        private float hitShrinkOffset = 0f;
        private float pulseScaleOffset = 0f; 

        public override void SetDefaults() {
            Projectile.width = 256; 
            Projectile.height = 256;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 99999; 
            Projectile.alpha = 255; 
            Projectile.hide = false; 
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) {
            orbitRadius = 0f;        
            visualRadius = 0f;
        }

        public override void AI() {
            NPC crystal = null;
            for (int i = 0; i < Main.maxNPCs; i++) {
                if (Main.npc[i].active && Main.npc[i].type == NPCID.DD2EterniaCrystal) {
                    crystal = Main.npc[i];
                    break;
                }
            }

            if (crystal == null || !crystal.active) {
                Projectile.Kill();
                return;
            }

            Projectile.Center = crystal.Center;

            // =========================================================================
            // [LOGIKA AUTO-DETEKSI TIER & SETTING CAP]
            // =========================================================================
            if (Projectile.ai[2] == 0f) {
                Projectile.ai[2] = 1f; 
                if (crystal.lifeMax >= 10000) MaxBlockCount = 1000;      // Tier 3
                else if (crystal.lifeMax >= 5000) MaxBlockCount = 500;   // Tier 2
                else MaxBlockCount = 100;                                // Tier 1
                CurrentBlockCount = MaxBlockCount;
            }

            if (isCooldown) {
                orbitRadius = MathHelper.Lerp(orbitRadius, 0f, 0.08f);
                visualRadius = MathHelper.Lerp(visualRadius, orbitRadius, 0.1f);
                Projectile.alpha += 10;
                if (Projectile.alpha > 255) Projectile.alpha = 255;
                cooldownTimer++;
                if (cooldownTimer >= 1800) {
                    cooldownTimer = 0;
                    CurrentBlockCount = MaxBlockCount; 
                    isCooldown = false;
                    SoundEngine.PlaySound(SoundID.DD2_EtherianPortalOpen, Projectile.Center);
                }
                return; 
            }

            if (Projectile.alpha > 0) { Projectile.alpha -= 15; if (Projectile.alpha < 0) Projectile.alpha = 0; }
            if (orbitRadius < targetOrbitRadius) { orbitRadius += 4f; if (orbitRadius > targetOrbitRadius) orbitRadius = targetOrbitRadius; }

            hitShrinkOffset = MathHelper.Lerp(hitShrinkOffset, 0f, 0.05f);
            pulseScaleOffset = MathHelper.Lerp(pulseScaleOffset, 0f, 0.05f);
            visualRadius = MathHelper.Lerp(visualRadius, orbitRadius - hitShrinkOffset + pulseScaleOffset, 0.1f);
            currentRotation += rotationSpeed;

            // =========================================================================
            // [LOGIKA PENANGKIS PELURU MULTIPLE SEKALIGUS (NO I-FRAMES)]
            // =========================================================================
            for (int i = 0; i < Main.maxProjectiles; i++) {
                Projectile targetProj = Main.projectile[i];
                if (targetProj.active && targetProj.hostile && targetProj.type != Projectile.type) {
                    float distanceToProj = Vector2.Distance(Projectile.Center, targetProj.Center);
                    
                    if (distanceToProj <= targetOrbitRadius + projectileHitboxTolerance) {
                        
                        hitShrinkOffset = 15f; 
                        targetProj.Kill(); 
                        CurrentBlockCount--; 
                        SoundEngine.PlaySound(SoundID.DD2_CrystalCartImpact, Projectile.Center);
                        
                        if (CurrentBlockCount <= 0) { 
                            isCooldown = true; 
                            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalDryadTouch, Projectile.Center); 
                            break; 
                        }
                    }
                }
            }

            // =========================================================================
            // [LOGIKA DORONGAN MUSUH MELEE]
            // =========================================================================
            for (int i = 0; i < Main.maxNPCs; i++) {
                NPC enemy = Main.npc[i];
                if (enemy.active && !enemy.friendly && enemy.damage > 0 && enemy.type != NPCID.DD2EterniaCrystal) {
                    float distanceToEnemy = Vector2.Distance(Projectile.Center, enemy.Center);
                    if (distanceToEnemy < targetOrbitRadius) {
                        Vector2 pushDirection = enemy.Center - Projectile.Center;
                        if (pushDirection == Vector2.Zero) pushDirection = -Vector2.UnitY;
                        pushDirection.Normalize();
                        pulseScaleOffset = 40f; 
                        enemy.position = Projectile.Center + (pushDirection * (targetOrbitRadius + (30f * 16f))) - new Vector2(enemy.width / 2, enemy.height / 2);
                        enemy.velocity = pushDirection * 10f;
                        CurrentBlockCount -= 2; 
                        SoundEngine.PlaySound(SoundID.DD2_EtherianPortalSpawnEnemy, enemy.Center);
                        
                        if (CurrentBlockCount <= 0) { 
                            isCooldown = true; 
                            SoundEngine.PlaySound(SoundID.DD2_EtherianPortalDryadTouch, Projectile.Center); 
                            break; 
                        }
                    }
                }
            }

            // =========================================================================
            // [LOGIKA KEKEBALAN PLAYER DI DALAM SHIELD (TANPA KEDIP-KEDIP)]
            // =========================================================================
            for (int i = 0; i < Main.maxPlayers; i++) {
                Player p = Main.player[i];
                if (p.active && !p.dead) {
                    float distanceToPlayer = Vector2.Distance(Projectile.Center, p.Center);
                    
                    // Jika Player berada di dalam radius Tameng, aktifkan mode proteksi kustom kita
                    if (distanceToPlayer <= targetOrbitRadius) {
                        p.GetModPlayer<ShieldPlayer>().IsProtectedByShield = true;
                    }
                }
            }
        }

        public override bool PreDraw(ref Color lightColor) {
            Texture2D texture = TextureAssets.Projectile[ProjectileID.CultistRitual].Value;
            if (texture == null) return false;
            Vector2 origin = texture.Size() / 2f;
            Vector2 drawPos = Projectile.Center - Main.screenPosition;
            float radiusToUse = visualRadius;
            if (radiusToUse < 4f) radiusToUse = 4f; 
            
            float scaleRatio = (radiusToUse / 204f) * 1.05f; 

            Color glowColor = new Color(170, 70, 255, 0) * ((255f - Projectile.alpha) / 255f);
            Main.spriteBatch.Draw(texture, drawPos, null, glowColor * 0.35f, -currentRotation * 0.4f, origin, scaleRatio * 1.08f, SpriteEffects.None, 0f);
            Main.spriteBatch.Draw(texture, drawPos, null, glowColor, currentRotation, origin, scaleRatio, SpriteEffects.None, 0f);
            return false; 
        }
    }
}