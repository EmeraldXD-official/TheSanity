using Microsoft.Xna.Framework;
using System;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace TheSanity.Items.Armor.Aetherfin
{
    public class AetherfinPlayer : ModPlayer
    {
        public bool aetherfinSetEquipped = false;
        
        // Status Skill Active "Floodgate Expansion"
        public int expansionTimer = 0;    // Durasi aktif (10 detik = 600 ticks)
        public int expansionCooldown = 0; // Cooldown total (30 detik = 1800 ticks)

        public override void ResetEffects() {
            aetherfinSetEquipped = false;
        }

        public override void PostUpdateEquips() {
            if (!aetherfinSetEquipped) return;

            // Timer & Cooldown
            if (expansionTimer > 0) expansionTimer--;
            if (expansionCooldown > 0) expansionCooldown--;

            // Radius Aura (Normal = 256 pixel / 16 blok, Expansion = 512 pixel / 32 blok)
            float auraRadius = (expansionTimer > 0) ? 512f : 256f;

            // ==========================================
            // 🌀 VISUAL AURA LINGKARAN (Opacity ~70%)
            // ==========================================
            int circlePoints = (expansionTimer > 0) ? 36 : 24; 
            
            // PERBAIKAN: Menggunakan Main.GameUpdateCount untuk efek rotasi
            float rotationOffset = Main.GameUpdateCount * 0.03f;

            for (int i = 0; i < circlePoints; i++) {
                float angle = (i * MathHelper.TwoPi / circlePoints) + rotationOffset;
                Vector2 dustOffset = new Vector2((float)Math.Cos(angle), (float)Math.Sin(angle)) * auraRadius;
                Vector2 dustPos = Player.Center + dustOffset;

                Dust d = Dust.NewDustPerfect(dustPos, DustID.Water, Vector2.Zero, 100, default, (expansionTimer > 0) ? 1.4f : 1.1f);
                
                d.noGravity = true;
                d.velocity = Vector2.Zero; // Diam di keliling lingkaran
                d.alpha = (expansionTimer > 0) ? 50 : 75; // Alpha 75 ≈ Opacity 70%
            }

            // Partikel gelembung melayang acak di dalam aura
            if (Main.rand.NextBool(expansionTimer > 0 ? 3 : 8)) {
                Vector2 randomInnerPos = Player.Center + Main.rand.NextVector2Circular(auraRadius * 0.9f, auraRadius * 0.9f);
                Dust innerDust = Dust.NewDustPerfect(randomInnerPos, DustID.Water, new Vector2(0, -0.4f), 100, default, 0.9f);
                innerDust.noGravity = true;
                innerDust.alpha = 80;
            }

            // ==========================================
            // 🌊 EFEK KE MUSUH (Debuff Slow saat Expansion)
            // ==========================================
            if (expansionTimer > 0) {
                foreach (NPC npc in Main.npc) {
                    if (npc.active && !npc.friendly && !npc.dontTakeDamage && npc.Distance(Player.Center) <= auraRadius) {
                        npc.velocity *= 0.85f; // Perlambatan 15%
                    }
                }
            }

            // ==========================================
            // 🐠 EFEK KE MINION (Movement Speed)
            // ==========================================
            foreach (Projectile proj in Main.projectile) {
                if (proj.active && proj.owner == Player.whoAmI && (proj.minion || proj.sentry || proj.DamageType == DamageClass.Summon)) {
                    if (proj.Distance(Player.Center) <= auraRadius) {
                        proj.velocity *= 1.025f; // +50% tracking/speed
                    }
                }
            }
        }

        public override void ProcessTriggers(Terraria.GameInput.TriggersSet triggersSet) {
            if (aetherfinSetEquipped && SanityKeybinds.AetherfinFloodgateKey != null && SanityKeybinds.AetherfinFloodgateKey.JustPressed) {
                if (expansionCooldown <= 0) {
                    expansionTimer = 600;     // 10 Detik
                    expansionCooldown = 1800; // 30 Detik Cooldown
                    
                    SoundEngine.PlaySound(SoundID.Item21, Player.Center);

                    // Burst Ledakan Air
                    for (int i = 0; i < 50; i++) {
                        Vector2 speed = Main.rand.NextVector2Circular(12f, 12f);
                        Dust d = Dust.NewDustDirect(Player.Center, 0, 0, DustID.Water, speed.X, speed.Y, 50, default, 2f);
                        d.noGravity = true;
                    }

                    CombatText.NewText(Player.getRect(), Color.DeepSkyBlue, "FLOODGATE OVERDRIVE!", true);
                }
                else {
                    int secondsLeft = (expansionCooldown / 60);
                    CombatText.NewText(Player.getRect(), Color.Gray, $"Cooldown: {secondsLeft}s", false);
                }
            }
        }

        public override void ModifyHitNPCWithProj(Projectile proj, NPC target, ref NPC.HitModifiers modifiers) {
            if (!aetherfinSetEquipped) return;

            float auraRadius = (expansionTimer > 0) ? 512f : 256f;

            if ((proj.minion || proj.sentry || proj.DamageType == DamageClass.Summon) && proj.Distance(Player.Center) <= auraRadius) {
                modifiers.ArmorPenetration += 8f;

                if (expansionTimer > 0) {
                    modifiers.SourceDamage += 0.15f;
                }
            }
        }
    }
}