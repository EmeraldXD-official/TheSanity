using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;
using TheSanity.Items.ArmorBoss.DukeFishron.Content.Projectiles;

namespace TheSanity.Items.ArmorBoss.DukeFishron.Content.Players
{
    // Holds the state for both DukeFishron Armor set bonuses:
    //   1) 5% chance on a ranged hit to fire a DukeFishronChargeProjectile
    //      that launches from just in front of the held weapon, dashes toward
    //      the target Phase-3 style, and pops into a bubble-burst on its
    //      first hit instead of the old 3x dash combo.
    //   2) Double-tap Down to summon a DukeFishronCompanion that circles the
    //      player and spawns 15 protective bubbles. 30s cooldown.
    // NOTE ON API VERSION: OnHitNPCWithProj / OnHitNPCWithItem below use the current
    // stable tModLoader 1.4.4+ signatures (NPC.HitInfo hit, int damageDone). Older
    // tModLoader versions used (int damage, float knockback, bool crit) instead - if
    // this doesn't compile against your installed tModLoader, swap the parameter list
    // to match whatever IntelliSense/the compiler suggests for those two hooks.
    public class DukeFishronPlayer : ModPlayer
    {
        // Set by DukeFishronHead.UpdateArmorSet() every tick the full set is worn.
        public bool setBonus;

        private int downTapTimer;
        private int bubbleRingCooldown;

        // ~1/3 of a second, matches the feel of vanilla's own double-tap-Down checks.
        private const int DoubleTapWindow = 20;
        // 30 seconds, as requested.
        public const int BubbleRingCooldown = 1800;

        // Exposed so the UI / an accessory tooltip could show time remaining if you want it later.
        public int BubbleRingCooldownRemaining => bubbleRingCooldown;

        // ~22% ammo conservation, between Shroomite's 20% and Vortex's 25% (see Breastplate).
        private const float AmmoConservationChance = 0.22f;

        public override bool CanConsumeAmmo(Item weapon, Item ammo)
        {
            if (!setBonus) return true;
            // Returning false here means "don't consume this ammo".
            return Main.rand.NextFloat() >= AmmoConservationChance;
        }

        public override void ResetEffects()
        {
            // Armor UpdateArmorSet hooks run every frame before this, so ResetEffects
            // clearing the flag first (via the normal ModPlayer call order) and armor
            // re-setting it is the standard vanilla-safe pattern.
            setBonus = false;
        }

        public override void PostUpdateMiscEffects()
        {
            if (downTapTimer > 0) downTapTimer--;
            if (bubbleRingCooldown > 0) bubbleRingCooldown--;
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (!setBonus) return;
            if (!PlayerInput.Triggers.JustPressed.Down) return;

            if (downTapTimer > 0)
            {
                downTapTimer = 0;
                TryActivateBubbleRing();
            }
            else
            {
                downTapTimer = DoubleTapWindow;
            }
        }

        private void TryActivateBubbleRing()
        {
            if (bubbleRingCooldown > 0) return;
            bubbleRingCooldown = BubbleRingCooldown;

            Projectile.NewProjectile(
                Player.GetSource_Misc("DukeFishronBubbleRing"),
                Player.Center,
                Vector2.Zero,
                ModContent.ProjectileType<DukeFishronCompanion>(),
                0, 0f, Player.whoAmI);

            SoundEngine.PlaySound(SoundID.Splash, Player.Center);
        }

        // Covers guns/bows/launchers - anything that deals damage through a projectile.
        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            TryFireDashProjectile(proj.DamageType, target);
        }

        // Covers the rare ranged weapon that hits directly without spawning a projectile.
        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone)
        {
            TryFireDashProjectile(item.DamageType, target);
        }

        // Bumped up from the old 12f - now that it's a single decisive hit instead of a
        // 3-hit dash combo, it needs to actually read as a fast "dash" on its own.
        private const float DashProjectileSpeed = 22f;
        // Roughly a held weapon's muzzle length, so the projectile visually leaves from
        // the gun/bow tip instead of popping out of the player's chest.
        private const float DashProjectileSpawnOffset = 36f;

        private void TryFireDashProjectile(DamageClass damageType, NPC target)
        {
            if (!setBonus) return;
            if (damageType != DamageClass.Ranged) return;
            if (Main.rand.NextFloat() >= 0.05f) return;

            Vector2 aimDirection = (Main.MouseWorld - Player.Center).SafeNormalize(Vector2.UnitX);
            int damage = System.Math.Max(10, Player.HeldItem.damage / 2);
            Vector2 spawnPos = Player.Center + aimDirection * DashProjectileSpawnOffset;

            Projectile.NewProjectile(
                Player.GetSource_OnHit(target),
                spawnPos,
                aimDirection * DashProjectileSpeed,
                ModContent.ProjectileType<DukeFishronChargeProjectile>(),
                damage, 2f, Player.whoAmI);

            // Same "surfacing" cue as the vanilla Razorblade Typhoon, since this is
            // Duke Fishron's own signature projectile launching.
            SoundEngine.PlaySound(SoundID.Item84, spawnPos);
        }
    }
}