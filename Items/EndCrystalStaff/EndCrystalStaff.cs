using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

// TODO: change "YourModName" to your actual mod's root namespace / folder structure.
namespace TheSanity.Items.EndCrystalStaff
{
    // The .png for this item MUST be named "EndCrystalStaff.png" and sit right next to this .cs file.
    public class EndCrystalStaff : ModItem
    {
        public override void SetDefaults()
        {
            // --- How it's held/used ---
            // DefaultToStaff is tModLoader's own helper for staff-style magic weapons. It sets
            // useStyle = Shoot, DamageType = Magic, autoReuse = true, noMelee = true, and - the
            // important part - switches the item to the STAFF hold system instead of the GUN hold
            // system (directly setting "Item.staff = true" hit a compile error, so this sidesteps
            // that entirely; DefaultToStaff is the public, documented way to get the same result).
            //
            // Both hold systems use useStyle 5 (Shoot), but they pivot completely differently:
            //  - gun-style   reads HoldoutOffset() (a flat pixel nudge, doesn't reliably pivot
            //                from a chosen point)
            //  - staff-style reads HoldoutOrigin() (the exact point on the sprite that gets
            //                pinned to the player's hand and rotated around - see below)
            // Without the staff hold system active, HoldoutOrigin() below is simply ignored,
            // which is why the sprite looked like it was floating instead of being gripped.
            Item.DefaultToStaff(
                projType: ModContent.ProjectileType<EndCrystalProjectile>(),
                pushForwardSpeed: 12f, // shootSpeed
                singleShotTime: 72,    // useTime / useAnimation, 1.2s * 60 ticks/sec
                manaPerShot: 8);

            // DefaultToStaff assumes a 40x40 sprite and Item43 as the use sound - override both
            // back to this item's real values (also required so HoldoutOrigin's math below is
            // measured against the correct sprite size).
            //
            // UPDATED: EndCrystalStaff.png was re-exported rotated ~46 degrees so the handle
            // sits horizontal (tip-left, crystal-right) instead of diagonal - this is what
            // makes it render pointing straight right when the player aims right, instead of
            // always looking tilted. Cropped to its new content bounds, the sprite is now
            // 121x54 (was 94x101).
            Item.width = 121;
            Item.height = 54;
            Item.UseSound = SoundID.Item20;

            // Scale used both when held in hand AND when lying on the ground.
            // 1f = sprite's native size (94x101, which is huge for a hand item) - scaled down
            // so it doesn't dwarf the character. Nudge this single value to taste; both the
            // held sprite and the dropped-on-ground sprite share it.
            Item.scale = 0.65f;

            // --- Combat stats ---
            Item.damage = 230;
            Item.knockBack = 4f;

            // --- Placeholders you'll likely want to adjust ---
            Item.value = Item.sellPrice(gold: 5);
            Item.rare = ItemRarityID.LightRed;
        }

        // --- Hand/grip alignment ---
        // This is the actual fix for "should be held at the very end of the handle, and point
        // straight when firing". HoldoutOrigin() (only used because SetDefaults() calls
        // Item.DefaultToStaff(), see above) is the point on the sprite that gets pinned to the
        // player's hand and rotated around
        // when aiming - get this wrong and the staff either swings around the wrong pivot or
        // appears to float away from the hand.
        //
        // UPDATED for the rotated sprite: EndCrystalStaff.png now has the handle horizontal
        // (tip on the LEFT, crystal head on the RIGHT), 121x54 pixels. The handle's very tip
        // sits at roughly pixel (0, 25) counting from the TOP-left. tModLoader measures
        // HoldoutOrigin from the BOTTOM-left corner instead, so it converts as:
        //   x = 0             (unchanged - horizontal doesn't flip)
        //   y = 54 - 25 = 29  (flipped - bottom-left origin counts upward from the bottom)
        //
        // Because the sprite's "rest" pose now points straight right, this origin point is
        // also what gets pinned to the player's hand and used as the pivot when rotating to
        // aim - so with the handle already horizontal in the art, aiming right should now
        // render the staff straight instead of tilted.
        //
        // If it still looks slightly off in-game, nudge x/y by a few pixels in the direction
        // that needs correcting - this is measured from the raw sprite, not tuned in-engine.
        public override Vector2? HoldoutOrigin()
        {
            // Increase Y to pull the grip point up off the very bottom edge of the handle tip
            // toward the shaft's center - useful if it still looks slightly low/floating.
            // X shifts the grip left/right along the handle's length (0 = very tip).
            //
            // UPDATE: bumped from 29 -> 45. Because the sprite is only 54px tall (thin
            // horizontal strip), a HIGHER Y (measured from the bottom) pins a point closer to
            // the TOP of that strip to the hand - which pushes more of the strip's visible area
            // BELOW the hand, making the whole staff render lower on screen. If it's still too
            // high, keep raising this toward 54 (the sprite's full height); if it overshoots
            // and goes too low, bring it back down toward 29.
            return new Vector2(0f, 45f);
        }
    }
}