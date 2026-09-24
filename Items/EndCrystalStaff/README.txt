END CRYSTAL STAFF - tModLoader weapon
=====================================

FILES
-----
EndCrystalStaff.cs        + EndCrystalStaff.png        -> the weapon (ModItem)
EndCrystalProjectile.cs   + EndCrystalProjectile.png   -> the crystal shot (ModProjectile)
EndCrystalExplosion.cs    + EndCrystalExplosion.png    -> the on-hit explosion visual (ModProjectile)

Each .cs file MUST sit in the same folder as its matching .png, with the exact same
filename (this is how tModLoader auto-links a class to its texture).

Suggested layout in your mod project:
  Content/Items/Weapons/EndCrystalStaff.cs
  Content/Items/Weapons/EndCrystalStaff.png
  Content/Projectiles/EndCrystalProjectile.cs
  Content/Projectiles/EndCrystalProjectile.png
  Content/Projectiles/EndCrystalExplosion.cs
  Content/Projectiles/EndCrystalExplosion.png

BEFORE IT COMPILES
------------------
Every .cs file currently uses the placeholder namespace "YourModName". Change:
  namespace YourModName.Content.Items.Weapons
  namespace YourModName.Content.Projectiles
to match your actual mod's namespace/folder structure, or it won't compile.

STATS (as requested)
---------------------
Damage: 230 (Magic)
Mana cost: 8
Use time / effective cooldown: 72 ticks = 1.2 seconds
Hold style: Shoot (character holds the staff pointed straight at the aim direction)
On enemy hit: spawns the explosion animation at the impact point (visual only, no extra damage)

A FEW THINGS I ADJUSTED / ASSUMED
----------------------------------
1. EndCrystalStaff.png - used exactly as uploaded. Item.width/height are set to its
   real pixel size (94x101) so the "held in hand" position lines up correctly. If it still
   looks off-center in-game, nudge Item.scale, not width/height.

2. EndCrystalProjectile.png - now uses the pink/white crystal cube sprite (cropped to its
   content bounding box, 116x138). It's a single static image, not a frame sheet - the spin
   is done purely via a fixed Projectile.rotation increment each tick (0.25f, no randomness),
   so it reads as a smooth constant spin with no jiggle/jitter. Projectile.scale is set to
   0.55f since the source image is fairly large relative to a normal bolt.

   Added a fading pink/white after-image trail behind it, drawn via Projectile.oldPos[] in
   a custom PreDraw override (TrailLength = 8 controls how many afterimages / how long the
   trail is - raise or lower that constant to taste). The trail color is a Color.Lerp
   between white and pink - tweak that blend or the fade multiplier to shift the look.

3. ExplodeProj.png (explosion effect) - a clean, well-formed 16-frame sheet, laid out
   horizontally (16 x 185x185 frames side-by-side). Re-sliced into a vertical strip
   (EndCrystalExplosion.png) since that's the layout tModLoader expects. The source art is
   plain grey/white, so the code tints it pink/purple to match the crystal theme - change
   `drawColor` in EndCrystalExplosion.cs if you want a different tint.

   Playback speed: TicksPerFrame = 1 (sped up per request - previously 3). This plays all
   16 frames in ~16 ticks (~0.27s). Raise this constant again if it ends up too fast/snappy.

4. Rarity, sell price, use sound, and knockback are placeholder values - search
   ItemRarityID / SoundID in tModLoader docs to swap in whatever fits your mod.

5. Projectile.scale = 0.5f on the explosion controls its visual size (source frames are
   185x185, which is large) - adjust until the blast radius looks right for your crystal.
